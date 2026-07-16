using System.Globalization;
using System.Text.Json;
using NexusScholar.AppServices;
using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Reporting;

namespace NexusScholar.ResearchWorkspace;

public static class WorkspaceExportSchemas
{
    public const string LedgerEntryId = "nexus.workspace-export-ledger-entry";
    public const string LedgerHeadId = "nexus.workspace-export-ledger-head";
    public const string Version = "1.0.0";
    public const string EntryFileName = "export-ledger-entry.json";
    public const string RequestFileName = "export-request.json";
    public const string GenesisPreviousDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";
}

public static class WorkspaceExportErrorCodes
{
    public const string InvalidLedger = "invalid-workspace-export-ledger";
    public const string StaleHead = "stale-workspace-export-head";
    public const string InvalidInventory = "invalid-workspace-export-inventory";
    public const string ExportCollision = "workspace-export-collision";
    public const string SourceDrift = "workspace-export-source-drift";
}

public sealed class WorkspaceExportException : InvalidOperationException
{
    public WorkspaceExportException(string category, string message) : base(message) => Category = category;
    public WorkspaceExportException(string category, string message, Exception inner) : base(message, inner) => Category = category;
    public string Category { get; }
}

public sealed record WorkspaceExportSourceBinding(
    string Role,
    string GenerationId,
    ContentDigest ManifestDigest,
    string? CandidateId);

public sealed class VerifiedWorkspaceExportLedgerEntry
{
    internal VerifiedWorkspaceExportLedgerEntry(
        long ordinal,
        ContentDigest previousEntryDigest,
        string exportId,
        ContentDigest requestDigest,
        string actor,
        string actorKind,
        string recordedAt,
        string workspaceId,
        long projectRevision,
        ContentDigest workspaceCutDigest,
        IReadOnlyList<WorkspaceExportSourceBinding> sources,
        ContentDigest reportDigest,
        ContentDigest bundleManifestDigest,
        ContentDigest observedInventoryDigest,
        ContentDigest? archiveTransportDigest,
        DigestEnvelope envelope)
    {
        Ordinal = ordinal; PreviousEntryDigest = previousEntryDigest; ExportId = exportId;
        RequestDigest = requestDigest; Actor = actor; ActorKind = actorKind; RecordedAt = recordedAt; WorkspaceId = workspaceId;
        ProjectRevision = projectRevision; WorkspaceCutDigest = workspaceCutDigest;
        Sources = Array.AsReadOnly(sources.Select(item => item with { }).ToArray());
        ReportDigest = reportDigest; BundleManifestDigest = bundleManifestDigest;
        ObservedInventoryDigest = observedInventoryDigest; ArchiveTransportDigest = archiveTransportDigest;
        Envelope = envelope;
    }

    public long Ordinal { get; }
    public ContentDigest PreviousEntryDigest { get; }
    public string ExportId { get; }
    public ContentDigest RequestDigest { get; }
    public string Actor { get; }
    public string ActorKind { get; }
    public string RecordedAt { get; }
    public string WorkspaceId { get; }
    public long ProjectRevision { get; }
    public ContentDigest WorkspaceCutDigest { get; }
    public IReadOnlyList<WorkspaceExportSourceBinding> Sources { get; }
    public ContentDigest ReportDigest { get; }
    public ContentDigest BundleManifestDigest { get; }
    public ContentDigest ObservedInventoryDigest { get; }
    public ContentDigest? ArchiveTransportDigest { get; }
    public DigestEnvelope Envelope { get; }
    public ContentDigest Digest => Envelope.ComputeDigest();
}

public static class WorkspaceExportLedgerEntryAuthority
{
    public static VerifiedWorkspaceExportLedgerEntry Create(
        long ordinal,
        ContentDigest previousEntryDigest,
        VerifiedReviewExportRequest request,
        ContentDigest? archiveTransportDigest = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (ordinal <= 0 || !previousEntryDigest.IsValid || archiveTransportDigest is { IsValid: false })
            throw Invalid("Export ledger ordinal or digest is invalid.");
        var sources = request.Sources.Select(item => new WorkspaceExportSourceBinding(
            item.Role, item.GenerationId, item.ManifestDigest, item.CandidateId))
            .OrderBy(item => item.Role, StringComparer.Ordinal).ThenBy(item => item.CandidateId, StringComparer.Ordinal).ToArray();
        var content = BuildContent(ordinal, previousEntryDigest, request.ExportId, request.RequestDigest, request.Actor,
            request.ActorKind, request.OccurredAt, request.WorkspaceId, request.ProjectRevision, request.WorkspaceCutDigest, sources,
            request.ReportDigest, request.BundleManifestDigest, request.ObservedInventoryDigest, archiveTransportDigest);
        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, WorkspaceExportSchemas.LedgerEntryId,
            WorkspaceExportSchemas.Version, content);
        return new VerifiedWorkspaceExportLedgerEntry(ordinal, previousEntryDigest, request.ExportId,
            request.RequestDigest, request.Actor, request.ActorKind, request.OccurredAt, request.WorkspaceId, request.ProjectRevision,
            request.WorkspaceCutDigest, sources, request.ReportDigest, request.BundleManifestDigest,
            request.ObservedInventoryDigest, archiveTransportDigest, envelope);
    }

    internal static CanonicalJsonObject BuildContent(
        long ordinal, ContentDigest previousEntryDigest, string exportId, ContentDigest requestDigest,
        string actor, string actorKind, string recordedAt, string workspaceId, long projectRevision, ContentDigest workspaceCutDigest,
        IEnumerable<WorkspaceExportSourceBinding> sources, ContentDigest reportDigest,
        ContentDigest bundleManifestDigest, ContentDigest observedInventoryDigest, ContentDigest? archiveTransportDigest)
    {
        var content = new CanonicalJsonObject().Add("ordinal", ordinal)
            .Add("previous_entry_digest", previousEntryDigest.ToString()).Add("export_id", exportId)
            .Add("request_digest", requestDigest.ToString()).Add("actor_id", actor).Add("actor_kind", actorKind).Add("recorded_at", recordedAt)
            .Add("workspace_id", workspaceId).Add("project_revision", projectRevision)
            .Add("workspace_cut_digest", workspaceCutDigest.ToString())
            .Add("source_generations", CanonicalJsonValue.Array(sources.Select(SourceJson).ToArray()))
            .Add("report_digest", reportDigest.ToString()).Add("bundle_manifest_digest", bundleManifestDigest.ToString())
            .Add("observed_inventory_digest", observedInventoryDigest.ToString());
        if (archiveTransportDigest is not null)
            content.Add("archive_transport", new CanonicalJsonObject().Add("scope", "non-scientific-transport-bytes")
                .Add("digest", archiveTransportDigest.Value.ToString()));
        return content;
    }

    private static CanonicalJsonObject SourceJson(WorkspaceExportSourceBinding source)
    {
        var value = new CanonicalJsonObject().Add("role", source.Role).Add("generation_id", source.GenerationId)
            .Add("manifest_digest", source.ManifestDigest.ToString());
        if (source.CandidateId is not null) value.Add("candidate_id", source.CandidateId);
        return value;
    }

    private static WorkspaceExportException Invalid(string message) =>
        new(WorkspaceExportErrorCodes.InvalidLedger, message);
}

public static class WorkspaceExportLedgerEntryCodec
{
    public static byte[] Serialize(VerifiedWorkspaceExportLedgerEntry entry) => entry.Envelope.ToCanonicalJsonBytes();

    public static VerifiedWorkspaceExportLedgerEntry Rehydrate(byte[] bytes, ContentDigest expectedDigest)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var verified = DigestEnvelope.RehydrateAndVerify(document.RootElement, expectedDigest,
                DigestScope.CanonicalJsonRecord, WorkspaceExportSchemas.LedgerEntryId, WorkspaceExportSchemas.Version);
            if (!bytes.SequenceEqual(verified.Envelope.ToCanonicalJsonBytes())) throw new InvalidOperationException("Ledger entry is not canonical.");
            var content = document.RootElement.GetProperty("content");
            Exact(content, "actor_id", "actor_kind", "archive_transport", "bundle_manifest_digest", "export_id", "observed_inventory_digest",
                "ordinal", "previous_entry_digest", "project_revision", "recorded_at", "report_digest", "request_digest",
                "source_generations", "workspace_cut_digest", "workspace_id");
            var ordinal = content.GetProperty("ordinal").GetInt64();
            var previous = Digest(content, "previous_entry_digest");
            var sources = content.GetProperty("source_generations").EnumerateArray().Select(Source).ToArray();
            var orderedSources = sources.OrderBy(item => item.Role, StringComparer.Ordinal)
                .ThenBy(item => item.CandidateId, StringComparer.Ordinal).ToArray();
            if (!sources.SequenceEqual(orderedSources) ||
                sources.Select(item => (item.Role, item.CandidateId)).Distinct().Count() != sources.Length)
                throw new InvalidOperationException("Ledger source generations are not canonical and unique.");
            ContentDigest? archive = null;
            if (content.TryGetProperty("archive_transport", out var archiveValue))
            {
                Exact(archiveValue, "digest", "scope");
                if (archiveValue.GetProperty("scope").GetString() != "non-scientific-transport-bytes")
                    throw new InvalidOperationException("Archive transport scope is invalid.");
                archive = Digest(archiveValue, "digest");
            }
            var recordedAt = Text(content, "recorded_at");
            var actorKind = Text(content, "actor_kind");
            if (actorKind != ReviewExportActorKinds.Human) throw new InvalidOperationException("Ledger actor kind is not human.");
            if (!DateTimeOffset.TryParseExact(recordedAt, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
                throw new InvalidOperationException("Ledger time is invalid.");
            return new VerifiedWorkspaceExportLedgerEntry(ordinal, previous, Text(content, "export_id"),
                Digest(content, "request_digest"), Text(content, "actor_id"), actorKind, recordedAt, Text(content, "workspace_id"),
                content.GetProperty("project_revision").GetInt64(), Digest(content, "workspace_cut_digest"), sources,
                Digest(content, "report_digest"), Digest(content, "bundle_manifest_digest"),
                Digest(content, "observed_inventory_digest"), archive, verified.Envelope);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            throw new WorkspaceExportException(WorkspaceExportErrorCodes.InvalidLedger, "Export ledger entry is invalid.", exception);
        }
    }

    private static WorkspaceExportSourceBinding Source(JsonElement value)
    {
        Exact(value, "candidate_id", "generation_id", "manifest_digest", "role");
        return new WorkspaceExportSourceBinding(Text(value, "role"), Text(value, "generation_id"),
            Digest(value, "manifest_digest"), value.TryGetProperty("candidate_id", out var candidate) ? candidate.GetString() : null);
    }

    internal static void Exact(JsonElement value, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        var names = value.EnumerateObject().Select(item => item.Name).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length || names.Any(name => !set.Contains(name)) ||
            allowed.Where(name => name != "candidate_id" && name != "archive_transport").Any(name => !names.Contains(name, StringComparer.Ordinal)))
            throw new InvalidOperationException("Canonical object fields are missing, duplicated, or unknown.");
    }

    internal static string Text(JsonElement value, string name) =>
        value.GetProperty(name).GetString() is { Length: > 0 } text ? text : throw new InvalidOperationException($"{name} is required.");
    internal static ContentDigest Digest(JsonElement value, string name) => ContentDigest.Parse(Text(value, name));
}

public sealed record WorkspaceExportLedgerHead(
    string WorkspaceId,
    long Count,
    string ExportId,
    string EntryPath,
    ContentDigest EntryDigest);

public sealed record WorkspaceExportLedgerReplay(
    WorkspaceExportLedgerHead? Head,
    IReadOnlyList<VerifiedWorkspaceExportLedgerEntry> Entries,
    IReadOnlyList<string> UnreferencedExportIds);

public static class ResearchWorkspaceExportLedgerVerifier
{
    public static WorkspaceExportLedgerReplay Replay(ResearchWorkspaceLocation location)
    {
        try
        {
            using var workspaceLock = new FileStream(Path.Combine(location.RootDirectory, ResearchWorkspacePaths.ProjectLockFileName),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return ReplayUnderLock(location);
        }
        catch (IOException exception)
        {
            throw new ResearchWorkspaceConcurrencyException("The workspace is locked by another mutation.", exception);
        }
    }

    internal static WorkspaceExportLedgerReplay ReplayUnderLock(ResearchWorkspaceLocation location)
    {
        var exportsRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.Exports);
        var headPath = ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.ExportLedgerHead);
        if (!File.Exists(headPath))
        {
            var unreferenced = Directory.Exists(exportsRoot)
                ? Directory.EnumerateDirectories(exportsRoot).Select(Path.GetFileName).OrderBy(item => item, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            return new WorkspaceExportLedgerReplay(null, Array.Empty<VerifiedWorkspaceExportLedgerEntry>(), unreferenced!);
        }
        var head = ReadHead(File.ReadAllBytes(headPath));
        var directories = Directory.EnumerateDirectories(exportsRoot).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        var candidates = new List<(string Directory, VerifiedWorkspaceExportLedgerEntry Entry)>();
        var unreferencedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in directories)
        {
            var entryPath = Path.Combine(directory, WorkspaceExportSchemas.EntryFileName);
            try
            {
                if (!File.Exists(entryPath)) throw new InvalidOperationException("Export directory is missing its ledger entry.");
                var bytes = File.ReadAllBytes(entryPath);
                var entry = WorkspaceExportLedgerEntryCodec.Rehydrate(bytes, ContentDigest.Sha256(bytes));
                if (entry.ExportId != Path.GetFileName(directory)) throw new InvalidOperationException("Ledger export id does not match its directory.");
                candidates.Add((directory, entry));
            }
            catch (Exception exception) when (exception is WorkspaceExportException or JsonException or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                _ = exception;
                unreferencedIds.Add(Path.GetFileName(directory));
            }
        }
        if (head.Count <= 0) throw Invalid("Export ledger head cannot describe an empty history.");
        var byDigest = candidates.GroupBy(item => item.Entry.Digest).ToDictionary(group => group.Key, group => group.ToArray());
        var reversed = new List<(string Directory, VerifiedWorkspaceExportLedgerEntry Entry)>();
        var nextDigest = head.EntryDigest;
        for (var ordinal = head.Count; ordinal >= 1; ordinal--)
        {
            if (!byDigest.TryGetValue(nextDigest, out var matches) || matches.Length != 1)
                throw Invalid("Export ledger head chain is missing or ambiguous.");
            var match = matches[0];
            if (match.Entry.Ordinal != ordinal) throw Invalid("Export ledger ordinal chain is invalid.");
            reversed.Add(match);
            nextDigest = match.Entry.PreviousEntryDigest;
        }
        if (nextDigest != ContentDigest.Parse(WorkspaceExportSchemas.GenesisPreviousDigest))
            throw Invalid("Export ledger predecessor chain does not terminate at genesis.");
        var chain = reversed.AsEnumerable().Reverse().ToArray();
        foreach (var item in chain)
        {
            try { VerifyExportDirectory(item.Directory, item.Entry); }
            catch (WorkspaceExportException) { throw; }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                throw new WorkspaceExportException(WorkspaceExportErrorCodes.InvalidLedger, "Published export directory verification failed.", exception);
            }
        }
        var publishedDirectories = chain.Select(item => item.Directory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates.Where(item => !publishedDirectories.Contains(item.Directory)))
            unreferencedIds.Add(candidate.Entry.ExportId);
        var ordered = chain.Select(item => item.Entry).ToArray();
        var last = ordered[^1];
        if (head.EntryDigest != last.Digest || head.ExportId != last.ExportId || head.WorkspaceId != last.WorkspaceId ||
            head.EntryPath != $"{ResearchWorkspacePaths.ExportRoot(last.ExportId)}/{WorkspaceExportSchemas.EntryFileName}")
            throw Invalid("Export ledger head does not match replayed history.");
        return new WorkspaceExportLedgerReplay(head, Array.AsReadOnly(ordered),
            Array.AsReadOnly(unreferencedIds.OrderBy(item => item, StringComparer.Ordinal).ToArray()));
    }

    internal static byte[] SerializeHead(WorkspaceExportLedgerHead head) => CanonicalJsonSerializer.SerializeToUtf8Bytes(
        new CanonicalJsonObject().Add("schema", WorkspaceExportSchemas.LedgerHeadId).Add("schema_version", WorkspaceExportSchemas.Version)
            .Add("workspace_id", head.WorkspaceId).Add("count", head.Count).Add("export_id", head.ExportId)
            .Add("entry_path", head.EntryPath).Add("entry_digest", head.EntryDigest.ToString()));

    private static WorkspaceExportLedgerHead ReadHead(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            WorkspaceExportLedgerEntryCodec.Exact(root, "count", "entry_digest", "entry_path", "export_id", "schema", "schema_version", "workspace_id");
            var canonical = CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(root));
            if (!bytes.SequenceEqual(canonical) || WorkspaceExportLedgerEntryCodec.Text(root, "schema") != WorkspaceExportSchemas.LedgerHeadId ||
                WorkspaceExportLedgerEntryCodec.Text(root, "schema_version") != WorkspaceExportSchemas.Version)
                throw new InvalidOperationException("Ledger head is not canonical.");
            return new WorkspaceExportLedgerHead(WorkspaceExportLedgerEntryCodec.Text(root, "workspace_id"), root.GetProperty("count").GetInt64(),
                WorkspaceExportLedgerEntryCodec.Text(root, "export_id"), WorkspaceExportLedgerEntryCodec.Text(root, "entry_path"),
                WorkspaceExportLedgerEntryCodec.Digest(root, "entry_digest"));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            throw new WorkspaceExportException(WorkspaceExportErrorCodes.InvalidLedger, "Export ledger head is invalid.", exception);
        }
    }

    internal static void VerifyExportDirectory(string directory, VerifiedWorkspaceExportLedgerEntry entry)
    {
        if (File.GetAttributes(directory).HasFlag(FileAttributes.ReparsePoint) ||
            Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
            .Any(path => File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)))
            throw Invalid("Export directory contains a reparse point.");
        var requestPath = Path.Combine(directory, WorkspaceExportSchemas.RequestFileName);
        if (!File.Exists(requestPath)) throw Invalid("Export request record is missing.");
        var requestBytes = File.ReadAllBytes(requestPath);
        if (ContentDigest.Sha256(requestBytes) != entry.RequestDigest) throw Invalid("Export request digest does not match the ledger entry.");
        using var request = JsonDocument.Parse(requestBytes);
        var canonical = CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(request.RootElement));
        if (!requestBytes.SequenceEqual(canonical)) throw Invalid("Export request is not canonical.");
        var root = request.RootElement;
        WorkspaceExportLedgerEntryCodec.Exact(root, "actor_id", "actor_kind", "artifacts", "bundle_manifest_digest", "export_id",
            "observed_inventory_digest", "project_revision", "report_digest", "report_markdown_digest", "slice_digest",
            "source_generations", "recorded_at", "workspace_cut_digest", "workspace_id");
        if (WorkspaceExportLedgerEntryCodec.Text(root, "export_id") != entry.ExportId ||
            WorkspaceExportLedgerEntryCodec.Text(root, "workspace_id") != entry.WorkspaceId ||
            root.GetProperty("project_revision").GetInt64() != entry.ProjectRevision ||
            WorkspaceExportLedgerEntryCodec.Digest(root, "report_digest") != entry.ReportDigest ||
            WorkspaceExportLedgerEntryCodec.Digest(root, "workspace_cut_digest") != entry.WorkspaceCutDigest ||
            WorkspaceExportLedgerEntryCodec.Digest(root, "bundle_manifest_digest") != entry.BundleManifestDigest ||
            WorkspaceExportLedgerEntryCodec.Digest(root, "observed_inventory_digest") != entry.ObservedInventoryDigest)
            throw Invalid("Export request bindings do not match the ledger entry.");
        if (WorkspaceExportLedgerEntryCodec.Text(root, "actor_id") != entry.Actor ||
            WorkspaceExportLedgerEntryCodec.Text(root, "actor_kind") != entry.ActorKind ||
            WorkspaceExportLedgerEntryCodec.Text(root, "recorded_at") != entry.RecordedAt)
            throw Invalid("Export request human action does not match the ledger entry.");
        var requestSources = root.GetProperty("source_generations").EnumerateArray().Select(item =>
        {
            WorkspaceExportLedgerEntryCodec.Exact(item, "candidate_id", "generation_id", "manifest_digest", "role");
            return new WorkspaceExportSourceBinding(WorkspaceExportLedgerEntryCodec.Text(item, "role"),
                WorkspaceExportLedgerEntryCodec.Text(item, "generation_id"),
                WorkspaceExportLedgerEntryCodec.Digest(item, "manifest_digest"),
                item.TryGetProperty("candidate_id", out var candidate) ? candidate.GetString() : null);
        }).ToArray();
        if (!requestSources.SequenceEqual(entry.Sources)) throw Invalid("Export source generations do not match the ledger entry.");

        var expected = new Dictionary<string, (long Size, ContentDigest Digest)>(StringComparer.Ordinal)
        {
            [WorkspaceExportSchemas.RequestFileName] = (requestBytes.LongLength, ContentDigest.Sha256(requestBytes)),
            [WorkspaceExportSchemas.EntryFileName] = (new FileInfo(Path.Combine(directory, WorkspaceExportSchemas.EntryFileName)).Length,
                ContentDigest.Sha256(File.ReadAllBytes(Path.Combine(directory, WorkspaceExportSchemas.EntryFileName)))),
            ["review-slice.json"] = ExpectedFile(root, "slice_digest", Path.Combine(directory, "review-slice.json")),
            ["report.json"] = (new FileInfo(Path.Combine(directory, "report.json")).Length, entry.ReportDigest),
            ["report.md"] = ExpectedFile(root, "report_markdown_digest", Path.Combine(directory, "report.md"))
        };
        var inventoryRecords = new List<(string Path, CanonicalJsonObject Record)>();
        foreach (var artifact in root.GetProperty("artifacts").EnumerateArray())
        {
            WorkspaceExportLedgerEntryCodec.Exact(artifact, "digest", "path", "size_bytes");
            var logicalPath = WorkspaceExportLedgerEntryCodec.Text(artifact, "path");
            if (!BundleArtifactPath.TryValidate(logicalPath, out _)) throw Invalid("Stored Bundle artifact path is invalid.");
            var path = logicalPath.Replace('/', Path.DirectorySeparatorChar);
            var size = artifact.GetProperty("size_bytes").GetInt64();
            var digest = WorkspaceExportLedgerEntryCodec.Digest(artifact, "digest");
            expected.Add($"bundle/{path.Replace(Path.DirectorySeparatorChar, '/')}", (size, digest));
            inventoryRecords.Add((logicalPath, new CanonicalJsonObject().Add("path", logicalPath).Add("size_bytes", size)
                .Add("digest", digest.ToString())));
        }
        var recomputedInventoryDigest = ContentDigest.Sha256CanonicalJson(new CanonicalJsonObject().Add("paths",
            CanonicalJsonValue.Array(inventoryRecords.OrderBy(item => item.Path, StringComparer.Ordinal)
                .Select(item => item.Record).ToArray())));
        if (recomputedInventoryDigest != entry.ObservedInventoryDigest)
            throw Invalid("Persisted Bundle inventory does not reproduce the ledger inventory digest.");
        var manifestRecord = inventoryRecords.SingleOrDefault(item => item.Path == BundleV2Constants.ManifestPath);
        if (manifestRecord.Record is null || WorkspaceExportLedgerEntryCodec.Digest(
                root.GetProperty("artifacts").EnumerateArray().Single(item =>
                    WorkspaceExportLedgerEntryCodec.Text(item, "path") == BundleV2Constants.ManifestPath), "digest") != entry.BundleManifestDigest)
            throw Invalid("Persisted Bundle manifest does not reproduce the ledger manifest digest.");
        var bundleRoot = Path.Combine(directory, "bundle");
        var manifestBytes = File.ReadAllBytes(Path.Combine(bundleRoot, BundleV2Constants.ManifestPath));
        var manifest = ReviewBundleV2CanonicalCodec.Rehydrate(manifestBytes, entry.BundleManifestDigest);
        var observedBundle = inventoryRecords.OrderBy(item => item.Path, StringComparer.Ordinal).Select(item =>
            new BundleV2ObservedEntry(item.Path, File.ReadAllBytes(Path.Combine(bundleRoot,
                item.Path.Replace('/', Path.DirectorySeparatorChar))))).ToArray();
        var bundleVerification = ReviewBundleV2Verifier.Verify(manifest, manifestBytes, observedBundle);
        if (!bundleVerification.IsValid || bundleVerification.InventoryDigest != entry.ObservedInventoryDigest)
            throw Invalid("Persisted Bundle v2 authority or exact inventory is invalid.");

        var reportBytes = File.ReadAllBytes(Path.Combine(directory, "report.json"));
        var reportVerification = PersistedReportingVerifier.VerifyReport(reportBytes, entry.ReportDigest);
        var sliceDigest = WorkspaceExportLedgerEntryCodec.Digest(root, "slice_digest");
        if (reportVerification.SliceDigest != sliceDigest)
            throw Invalid("Persisted report does not bind the persisted review slice.");
        _ = PersistedReportingVerifier.VerifySlice(File.ReadAllBytes(Path.Combine(directory, "review-slice.json")), sliceDigest);
        var observedPaths = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/')).ToHashSet(StringComparer.Ordinal);
        if (!observedPaths.SetEquals(expected.Keys)) throw Invalid("Export directory inventory is not exact.");
        foreach (var item in expected)
        {
            var path = Path.Combine(directory, item.Key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || new FileInfo(path).Length != item.Value.Size || ContentDigest.Sha256(File.ReadAllBytes(path)) != item.Value.Digest)
                throw Invalid($"Export artifact '{item.Key}' is altered.");
        }
    }

    private static (long Size, ContentDigest Digest) ExpectedFile(JsonElement root, string digestName, string path) =>
        File.Exists(path) ? (new FileInfo(path).Length, WorkspaceExportLedgerEntryCodec.Digest(root, digestName)) : throw Invalid("Export artifact is missing.");

    private static WorkspaceExportException Invalid(string message) => new(WorkspaceExportErrorCodes.InvalidLedger, message);
}

public enum ResearchWorkspaceExportFaultPoint
{
    AfterStaging,
    AfterPromotion,
    BeforeHeadPublication,
    AfterHeadPublication
}

public sealed record ResearchWorkspaceExportCommit(
    VerifiedWorkspaceExportLedgerEntry Entry,
    WorkspaceExportLedgerHead Head,
    bool AlreadyApplied);

public static class ResearchWorkspaceExportTransaction
{
    public static ResearchWorkspaceExportCommit Commit(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject expectedProject,
        VerifiedReviewExportRequest request,
        ContentDigest? expectedPreviousEntryDigest,
        ContentDigest? archiveTransportDigest = null,
        Action<ResearchWorkspaceExportFaultPoint>? faultInjector = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var initial = ResearchWorkspaceExportLedgerVerifier.Replay(location);
        var existing = initial.Entries.SingleOrDefault(item => item.ExportId == request.ExportId);
        if (existing is not null)
        {
            if (existing.RequestDigest != request.RequestDigest) throw Collision("Export id already binds different bytes.");
            return new ResearchWorkspaceExportCommit(existing, initial.Head!, true);
        }
        var observedPrevious = initial.Head?.EntryDigest;
        if (observedPrevious != expectedPreviousEntryDigest) throw Stale();
        var previous = observedPrevious ?? ContentDigest.Parse(WorkspaceExportSchemas.GenesisPreviousDigest);
        var entry = WorkspaceExportLedgerEntryAuthority.Create(initial.Entries.Count + 1L, previous, request, archiveTransportDigest);
        var entryBytes = WorkspaceExportLedgerEntryCodec.Serialize(entry);
        var stagingRoot = ResearchWorkspacePaths.InProject(location.RootDirectory,
            $"{ResearchWorkspacePaths.GenerationStaging}/export-{request.ExportId}-{Guid.NewGuid():N}");
        var finalRoot = ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.ExportRoot(request.ExportId));
        Directory.CreateDirectory(stagingRoot);
        try
        {
            WriteStaging(stagingRoot, request, entryBytes);
            VerifyStaging(stagingRoot, entry);
            faultInjector?.Invoke(ResearchWorkspaceExportFaultPoint.AfterStaging);
            using var workspaceLock = AcquireLock(location);
            var current = ResearchWorkspaceExportLedgerVerifier.ReplayUnderLock(location);
            if (current.Head?.EntryDigest != expectedPreviousEntryDigest || current.Entries.Count != initial.Entries.Count) throw Stale();
            QuarantineUnreferenced(location, current);
            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (project.WorkspaceId != expectedProject.WorkspaceId || project.Revision != expectedProject.Revision ||
                ResearchWorkspaceJson.Serialize(project) != ResearchWorkspaceJson.Serialize(expectedProject) ||
                project.WorkspaceId != request.WorkspaceId || project.Revision != request.ProjectRevision)
                throw new WorkspaceExportException(WorkspaceExportErrorCodes.SourceDrift, "Workspace source revision changed before export publication.");
            if (Directory.Exists(finalRoot)) throw Collision("Export directory already exists.");
            Directory.CreateDirectory(Path.GetDirectoryName(finalRoot)!);
            var head = new WorkspaceExportLedgerHead(request.WorkspaceId, entry.Ordinal, request.ExportId,
                $"{ResearchWorkspacePaths.ExportRoot(request.ExportId)}/{WorkspaceExportSchemas.EntryFileName}", entry.Digest);
            var headPublished = false;
            try
            {
                Directory.Move(stagingRoot, finalRoot);
                faultInjector?.Invoke(ResearchWorkspaceExportFaultPoint.AfterPromotion);
                faultInjector?.Invoke(ResearchWorkspaceExportFaultPoint.BeforeHeadPublication);
                WriteAtomic(ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.ExportLedgerHead),
                    ResearchWorkspaceExportLedgerVerifier.SerializeHead(head));
                headPublished = true;
                faultInjector?.Invoke(ResearchWorkspaceExportFaultPoint.AfterHeadPublication);
            }
            catch
            {
                if (!headPublished && Directory.Exists(finalRoot)) Quarantine(location, finalRoot, request.ExportId);
                throw;
            }
            var replay = ResearchWorkspaceExportLedgerVerifier.ReplayUnderLock(location);
            return new ResearchWorkspaceExportCommit(replay.Entries[^1], replay.Head!, false);
        }
        finally
        {
            if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true);
        }
    }

    private static void WriteStaging(string root, VerifiedReviewExportRequest request, byte[] entryBytes)
    {
        File.WriteAllBytes(Path.Combine(root, WorkspaceExportSchemas.RequestFileName), request.RequestBytes);
        File.WriteAllBytes(Path.Combine(root, "review-slice.json"), request.SliceBytes);
        File.WriteAllBytes(Path.Combine(root, "report.json"), request.ReportBytes);
        File.WriteAllBytes(Path.Combine(root, "report.md"), request.ReportMarkdownBytes);
        foreach (var artifact in request.ObservedInventory)
        {
            if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(Path.Combine(root, "bundle"), artifact.Path, out var path))
                throw new WorkspaceExportException(WorkspaceExportErrorCodes.InvalidInventory, "Bundle artifact path escapes the export.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, artifact.Bytes);
        }
        File.WriteAllBytes(Path.Combine(root, WorkspaceExportSchemas.EntryFileName), entryBytes);
    }

    private static void VerifyStaging(string root, VerifiedWorkspaceExportLedgerEntry entry)
    {
        ResearchWorkspaceExportLedgerVerifier.VerifyExportDirectory(root, entry);
    }

    private static FileStream AcquireLock(ResearchWorkspaceLocation location)
    {
        try
        {
            return new FileStream(Path.Combine(location.RootDirectory, ResearchWorkspacePaths.ProjectLockFileName),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new ResearchWorkspaceConcurrencyException("The workspace is locked by another mutation.", exception);
        }
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void Quarantine(ResearchWorkspaceLocation location, string root, string exportId)
    {
        var target = ResearchWorkspacePaths.InProject(location.RootDirectory,
            $"{ResearchWorkspacePaths.GenerationQuarantine}/export-{exportId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(root, target);
    }

    private static void QuarantineUnreferenced(ResearchWorkspaceLocation location, WorkspaceExportLedgerReplay replay)
    {
        foreach (var exportId in replay.UnreferencedExportIds)
        {
            var root = ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.ExportRoot(exportId));
            if (Directory.Exists(root)) Quarantine(location, root, exportId);
        }
    }

    private static WorkspaceExportException Stale() => new(WorkspaceExportErrorCodes.StaleHead, "Export ledger head changed during publication.");
    private static WorkspaceExportException Collision(string message) => new(WorkspaceExportErrorCodes.ExportCollision, message);
}

public sealed class ResearchWorkspaceExportCommitPort(
    ResearchWorkspaceLocation location,
    ResearchWorkspaceProject expectedProject,
    ContentDigest? archiveTransportDigest = null,
    Action<ResearchWorkspaceExportFaultPoint>? faultInjector = null) : IReviewExportCommitPort
{
    public ReviewExportCommitResult Commit(VerifiedReviewExportRequest request, ContentDigest? expectedPreviousEntryDigest)
    {
        var committed = ResearchWorkspaceExportTransaction.Commit(location, expectedProject, request,
            expectedPreviousEntryDigest, archiveTransportDigest, faultInjector);
        return new ReviewExportCommitResult(committed.Entry.ExportId, committed.Entry.RequestDigest,
            committed.Entry.Digest, committed.Entry.ObservedInventoryDigest, committed.Entry.Ordinal, committed.AlreadyApplied);
    }
}
