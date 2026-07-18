using System.Globalization;
using System.Text.Json;
using NexusScholar.Kernel;
using NexusScholar.Search;

namespace NexusScholar.Search.Providers.Cache;

public sealed record ProviderEvidenceCacheLookup(ProviderEvidenceCacheEntry Entry, byte[]? BodyBytes, bool IsFresh);

public sealed class ProviderEvidenceCacheStore
{
    private const string DataDirectoryName = "entries";
    private const string IndexFileName = "index.json";
    private const string EntryManifestFileName = "entry.json";
    private const string BodyFileName = "body.bin";
    private const string LockFileName = "cache.lock";

    private const string IndexSchemaId = "nexus.search.provider-evidence-cache-index";
    private const string IndexSchemaVersion = "1.0.0";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _rootDirectory;
    private readonly string _indexPath;
    private readonly string _entryDirectory;
    private readonly string _lockPath;
    private readonly Func<string, string, ProviderEvidenceCachePolicy> _resolvePolicy;
    private readonly TimeSpan _lockTimeout;

    public ProviderEvidenceCacheStore(string rootDirectory)
        : this(rootDirectory, ProviderEvidenceCachePolicies.Resolve)
    {
    }

    internal ProviderEvidenceCacheStore(
        string rootDirectory,
        Func<string, string, ProviderEvidenceCachePolicy> resolvePolicy,
        TimeSpan? lockTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("Cache root directory is required.", nameof(rootDirectory));
        }

        _resolvePolicy = resolvePolicy ?? throw new ArgumentNullException(nameof(resolvePolicy));
        _lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(5);
        if (_lockTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockTimeout), "Cache lock timeout must be positive.");
        }

        _rootDirectory = Path.GetFullPath(rootDirectory.Trim());
        Directory.CreateDirectory(_rootDirectory);

        _indexPath = Path.Combine(_rootDirectory, IndexFileName);
        _entryDirectory = Path.Combine(_rootDirectory, DataDirectoryName);
        _lockPath = Path.Combine(_rootDirectory, LockFileName);
        Directory.CreateDirectory(_entryDirectory);
    }

    public bool TryGet(ProviderEvidenceCacheKey key, DateTimeOffset at, out ProviderEvidenceCacheLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(key);
        using var _ = AcquireReadLock();

        var index = LoadIndex();
        var keyDigest = key.Identity.Value;
        if (!index.TryGetValue(keyDigest, out var entryDigest))
        {
            lookup = null!;
            return false;
        }

        var entry = ReadEntry(entryDigest, key);
        var bodyBytes = entry.IsBodyRetained
            ? ReadRetainedBody(entryDigest, entry)
            : null;
        var currentPolicy = _resolvePolicy(key.ProviderAlias, key.Operation);
        var isCurrentPolicy = currentPolicy.IsAllowed &&
            string.Equals(currentPolicy.PolicyIdentity, entry.PolicyIdentity, StringComparison.Ordinal) &&
            currentPolicy.RetentionMode == entry.RetentionMode &&
            currentPolicy.RetentionWindow == entry.RetentionWindow;
        lookup = new ProviderEvidenceCacheLookup(entry, bodyBytes, isCurrentPolicy && entry.IsFresh(at));
        return true;
    }

    public ProviderEvidenceCacheEntry Record(
        ProviderEvidenceCacheKey key,
        RuntimeProviderResponseEvidence response,
        byte[]? bodyBytes,
        DateTimeOffset storedAt)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(response);

        var policy = _resolvePolicy(key.ProviderAlias, key.Operation);
        var entry = ProviderEvidenceCacheEntry.Create(key, policy, response, storedAt);

        if (entry.IsBodyRetained)
        {
            if (bodyBytes is null)
            {
                throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompleteResponse, "Cache writes with body retention require bytes.");
            }

            entry.VerifyBody(bodyBytes);
        }
        else if (bodyBytes is not null && bodyBytes.Length > 0)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, "Digest-only cache policy does not retain response bodies.");
        }

        var keyDigest = key.Identity.Value;
        using var _ = AcquireWriteLock();

        var index = LoadIndex();
        if (index.TryGetValue(keyDigest, out var existingEntryDigest))
        {
            var existing = ReadEntry(existingEntryDigest, key);
            if (IsEquivalent(existing, entry, response, bodyBytes))
            {
                if (existing.IsBodyRetained)
                {
                    ReadRetainedBody(existingEntryDigest, existing);
                }

                return existing;
            }
        }

        var entryDigest = entry.Digest.Value;
        var stagingRoot = Path.Combine(_rootDirectory, "staging", Guid.NewGuid().ToString("N"));
        var stagedEntryRoot = Path.Combine(stagingRoot, entryDigest);
        Directory.CreateDirectory(stagedEntryRoot);

        try
        {
            WriteEntryManifest(stagedEntryRoot, entry);
            if (entry.IsBodyRetained && bodyBytes is not null)
            {
                File.WriteAllBytes(GetBodyPath(stagedEntryRoot), bodyBytes);
            }

            index[keyDigest] = entryDigest;
            var stagedIndexPath = Path.Combine(stagingRoot, IndexFileName);
            WriteIndex(stagedIndexPath, index);

            var finalEntryPath = GetEntryPath(entryDigest);
            if (Directory.Exists(finalEntryPath))
            {
                var existing = ReadEntry(entryDigest, key);
                if (existing.Digest != entry.Digest)
                {
                    throw new SearchRuleException(
                        ProviderEvidenceCacheErrorCodes.IncompatiblePolicy,
                        "Immutable cache entry directory contains different evidence.");
                }

                if (existing.IsBodyRetained)
                {
                    ReadRetainedBody(entryDigest, existing);
                }
            }
            else
            {
                Directory.Move(stagedEntryRoot, finalEntryPath);
            }

            File.Move(stagedIndexPath, _indexPath, overwrite: true);
            return entry;
        }
        finally
        {
            if (Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    public ProviderEvidenceCacheEntry Record(ProviderEvidenceCacheKey key, RuntimeProviderResponseEvidence response, byte[]? bodyBytes) =>
        Record(key, response, bodyBytes, DateTimeOffset.UtcNow);

    public void RebuildIndex()
    {
        using var _ = AcquireWriteLock();
        var latest = new Dictionary<string, (string EntryDigest, DateTimeOffset StoredAt)>(StringComparer.Ordinal);
        foreach (var entryDirectory in Directory.EnumerateDirectories(_entryDirectory))
        {
            var entryDigest = Path.GetFileName(entryDirectory);
            if (!IsSha256Hex(entryDigest))
            {
                continue;
            }

            var entry = ReadEntry(entryDigest);
            if (entry.IsBodyRetained)
            {
                ReadRetainedBody(entryDigest, entry);
            }

            var keyDigest = entry.Key.Identity.Value;
            if (!latest.TryGetValue(keyDigest, out var current) ||
                entry.StoredAt > current.StoredAt ||
                (entry.StoredAt == current.StoredAt &&
                    string.CompareOrdinal(entryDigest, current.EntryDigest) > 0))
            {
                latest[keyDigest] = (entryDigest!, entry.StoredAt);
            }
        }

        var index = latest.ToDictionary(
            item => item.Key,
            item => item.Value.EntryDigest,
            StringComparer.Ordinal);
        WriteIndex(_indexPath, index);
    }

    private static bool IsEquivalent(
        ProviderEvidenceCacheEntry existing,
        ProviderEvidenceCacheEntry candidate,
        RuntimeProviderResponseEvidence response,
        byte[]? bodyBytes)
    {
        try
        {
            if (!string.Equals(existing.PolicyIdentity, candidate.PolicyIdentity, StringComparison.Ordinal) ||
                existing.RetentionMode != candidate.RetentionMode ||
                existing.RetentionWindow != candidate.RetentionWindow ||
                existing.IsBodyRetained != candidate.IsBodyRetained)
            {
                return false;
            }

            existing.VerifyResponseEvidence(response);
            if (existing.IsBodyRetained)
            {
                existing.VerifyBody(bodyBytes ?? []);
            }

            return true;
        }
        catch (SearchRuleException)
        {
            return false;
        }
    }

    private Dictionary<string, string> LoadIndex()
    {
        if (!File.Exists(_indexPath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            using var stream = new FileStream(_indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var payload = JsonSerializer.Deserialize<ProviderEvidenceCacheIndexArtifact>(stream, JsonOptions);
            if (payload is null ||
                payload.SchemaId != IndexSchemaId ||
                payload.SchemaVersion != IndexSchemaVersion ||
                payload.Entries is null)
            {
                throw new SearchRuleException(
                    ProviderEvidenceCacheErrorCodes.StoreIndexCorrupt,
                    "Cache index schema is invalid.");
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in payload.Entries)
            {
                if (!IsSha256Hex(item.KeyDigest) || !IsSha256Hex(item.EntryDigest))
                {
                    throw new SearchRuleException(
                        ProviderEvidenceCacheErrorCodes.StoreIndexCorrupt,
                        "Cache index entry is malformed.");
                }

                values[item.KeyDigest!] = item.EntryDigest!;
            }

            return values;
        }
        catch (JsonException)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.StoreIndexCorrupt, "Cache index file is malformed.");
        }
    }

    private ProviderEvidenceCacheEntry ReadEntry(string entryDigest, ProviderEvidenceCacheKey? expectedKey = null)
    {
        var manifestPath = Path.Combine(GetEntryPath(entryDigest), EntryManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor, "Cached entry manifest is missing.");
        }

        using var stream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var payload = JsonSerializer.Deserialize<ProviderEvidenceCacheEntryArtifact>(stream, JsonOptions);
        if (payload is null ||
            !IsSha256Hex(payload.KeyDigest) ||
            !IsSha256Hex(payload.EntryDigest) ||
            payload.SchemaId != ProviderEvidenceCacheEntry.SchemaId ||
            payload.SchemaVersion != ProviderEvidenceCacheEntry.SchemaVersion)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.StoreIndexCorrupt, "Cached entry manifest is malformed.");
        }

        if (!string.Equals(payload.EntryDigest, entryDigest, StringComparison.Ordinal))
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, "Cached entry digest does not match directory.");
        }

        var key = expectedKey ??
            ProviderEvidenceCacheKey.Restore(
                payload.ProviderAlias,
                payload.Operation,
                ContentDigest.Parse(payload.SanitizedRequestDigest),
                ContentDigest.Parse(payload.PageRequestDigest),
                payload.ParserId,
                payload.ParserVersion);

        if (!string.Equals(key.Identity.Value, payload.KeyDigest, StringComparison.Ordinal) ||
            (expectedKey is not null && key.Identity != expectedKey.Identity))
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, "Cached key does not match request.");
        }

        var entry = ProviderEvidenceCacheEntry.Restore(
            key,
            payload.PolicyIdentity,
            ProviderEvidenceCachePolicy.ParseRetentionMode(payload.RetentionMode),
            TimeSpan.FromSeconds(payload.RetentionWindowSeconds),
            ParseTimestamp(payload.StoredAt),
            ParseTimestamp(payload.RequestedAt),
            ParseTimestamp(payload.ReceivedAt),
            ParseTimestamp(payload.ExpiresAt),
            payload.StatusCode,
            payload.MediaType,
            payload.ByteLength,
            ContentDigest.Parse(payload.RawResponseDigest),
            ContentDigest.Parse(payload.RawResponseEvidenceDigest),
            payload.IsBodyRetained);
        if (!string.Equals(entry.Digest.Value, entryDigest, StringComparison.Ordinal))
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.DigestMismatch,
                "Cached entry manifest digest does not match its immutable directory.");
        }

        return entry;
    }

    private byte[] ReadRetainedBody(string entryDigest, ProviderEvidenceCacheEntry entry)
    {
        var bodyPath = GetBodyPath(GetEntryPath(entryDigest));
        if (!File.Exists(bodyPath))
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.DigestMismatch,
                "Cached response body is missing.");
        }

        byte[] bodyBytes;
        try
        {
            bodyBytes = File.ReadAllBytes(bodyPath);
        }
        catch (IOException)
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.DigestMismatch,
                "Cached response body cannot be read.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.DigestMismatch,
                "Cached response body cannot be read.");
        }
        catch (NotSupportedException)
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.DigestMismatch,
                "Cached response body cannot be read.");
        }

        entry.VerifyBody(bodyBytes);

        return bodyBytes;
    }

    private static DateTimeOffset ParseTimestamp(string value)
    {
        if (!CanonicalTimestamp.IsCanonicalUtc(value, rejectDefault: true))
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.StoreIndexCorrupt, "Cached timestamp is malformed.");
        }

        return DateTimeOffset.ParseExact(
            value,
            CanonicalTimestamp.DefaultUtcFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }

    private static void WriteEntryManifest(string entryDirectory, ProviderEvidenceCacheEntry entry)
    {
        var payload = new ProviderEvidenceCacheEntryArtifact
        {
            SchemaId = ProviderEvidenceCacheEntry.SchemaId,
            SchemaVersion = ProviderEvidenceCacheEntry.SchemaVersion,
            KeyDigest = entry.Key.Identity.Value,
            EntryDigest = entry.Digest.Value,
            ProviderAlias = entry.Key.ProviderAlias,
            Operation = entry.Key.Operation,
            PolicyIdentity = entry.PolicyIdentity,
            RetentionMode = entry.RetentionMode.ToString(),
            RetentionWindowSeconds = (long)entry.RetentionWindow.TotalSeconds,
            SanitizedRequestDigest = entry.Key.SanitizedRequestDigest.ToString(),
            PageRequestDigest = entry.Key.PageRequestDigest.ToString(),
            ParserId = entry.Key.ParserId,
            ParserVersion = entry.Key.ParserVersion,
            StatusCode = entry.StatusCode,
            MediaType = entry.MediaType,
            ByteLength = entry.ByteLength,
            RawResponseDigest = entry.ResponseDigest.ToString(),
            RawResponseEvidenceDigest = entry.ResponseEvidenceDigest.ToString(),
            RequestedAt = CanonicalTimestamp.FormatUtc(entry.RequestedAt),
            ReceivedAt = CanonicalTimestamp.FormatUtc(entry.ReceivedAt),
            StoredAt = CanonicalTimestamp.FormatUtc(entry.StoredAt),
            ExpiresAt = CanonicalTimestamp.FormatUtc(entry.ExpiresAt),
            IsBodyRetained = entry.IsBodyRetained
        };

        var manifestPath = Path.Combine(entryDirectory, EntryManifestFileName);
        Directory.CreateDirectory(entryDirectory);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        File.WriteAllBytes(manifestPath, bytes);
    }

    private static void WriteIndex(string indexPath, IReadOnlyDictionary<string, string> values)
    {
        var payload = new ProviderEvidenceCacheIndexArtifact
        {
            SchemaId = IndexSchemaId,
            SchemaVersion = IndexSchemaVersion,
            Entries = values
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new ProviderEvidenceCacheIndexArtifact.Entry
                {
                    KeyDigest = item.Key,
                    EntryDigest = item.Value
                })
                .ToArray()
        };

        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        File.WriteAllBytes(indexPath, JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));
    }

    private static bool IsSha256Hex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (
                character is < '0' or > '9' &&
                (character is < 'a' or > 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private string GetEntryPath(string keyDigest) => Path.Combine(_entryDirectory, keyDigest);
    private string GetBodyPath(string entryPath) => Path.Combine(entryPath, BodyFileName);

    private FileStream AcquireReadLock() => AcquireLock(FileAccess.Read, FileShare.Read);

    private FileStream AcquireWriteLock() => AcquireLock(FileAccess.ReadWrite, FileShare.None);

    private FileStream AcquireLock(FileAccess access, FileShare share)
    {
        var deadline = DateTimeOffset.UtcNow.Add(_lockTimeout);
        while (true)
        {
            try
            {
                return new FileStream(_lockPath, FileMode.OpenOrCreate, access, share);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }
            catch (UnauthorizedAccessException) when (DateTimeOffset.UtcNow < deadline)
            {
                Thread.Sleep(10);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new SearchRuleException(
                    ProviderEvidenceCacheErrorCodes.StoreBusy,
                    "Provider evidence cache lock could not be acquired within the bounded wait.");
            }
        }
    }
}

internal sealed class ProviderEvidenceCacheIndexArtifact
{
    public string SchemaId { get; set; } = "";
    public string SchemaVersion { get; set; } = "";
    public ProviderEvidenceCacheIndexArtifact.Entry[] Entries { get; set; } = [];

    public sealed class Entry
    {
        public string? KeyDigest { get; set; }
        public string? EntryDigest { get; set; }
    }
}

internal sealed class ProviderEvidenceCacheEntryArtifact
{
    public string SchemaId { get; set; } = "";
    public string SchemaVersion { get; set; } = "";
    public string? KeyDigest { get; set; }
    public string? EntryDigest { get; set; }
    public string ProviderAlias { get; set; } = "";
    public string Operation { get; set; } = "";
    public string PolicyIdentity { get; set; } = "";
    public string RetentionMode { get; set; } = "";
    public long RetentionWindowSeconds { get; set; }
    public string SanitizedRequestDigest { get; set; } = "";
    public string PageRequestDigest { get; set; } = "";
    public string ParserId { get; set; } = "";
    public string ParserVersion { get; set; } = "";
    public string RawResponseDigest { get; set; } = "";
    public string RawResponseEvidenceDigest { get; set; } = "";
    public int StatusCode { get; set; }
    public string MediaType { get; set; } = "";
    public long ByteLength { get; set; }
    public string RequestedAt { get; set; } = "";
    public string ReceivedAt { get; set; } = "";
    public string StoredAt { get; set; } = "";
    public string ExpiresAt { get; set; } = "";
    public bool IsBodyRetained { get; set; }
}
