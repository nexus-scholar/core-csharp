using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Search.Providers.Cache;

namespace NexusScholar.Search.Providers.Cache.Tests;

[TestClass]
public sealed class ProviderEvidenceCacheStoreTests
{
    private static readonly DateTimeOffset RequestedAt = DateTimeOffset.Parse("2026-07-17T10:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset ReceivedAt = DateTimeOffset.Parse("2026-07-17T10:01:00Z", CultureInfo.InvariantCulture);

    private const string OpenAlexParserId = "cache-openalex-parser";
    private const string OpenAlexParserVersion = "1.0.0";

    [TestMethod]
    public void Openalex_cache_records_body_and_lookup_remains_fresh_until_expiry()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("openalex", "openalex.works", OpenAlexParserId, OpenAlexParserVersion);
        var responseBody = Encoding.UTF8.GetBytes("{\"result\":\"openalex-cache\"}");
        var response = CaptureResponse(key, responseBody, 200, "application/json");

        var entry = store.Record(key, response, responseBody);

        var lookupByFresh = store.TryGet(key, entry.StoredAt.AddHours(1), out var fresh);
        Assert.IsTrue(lookupByFresh);
        Assert.IsNotNull(fresh);
        Assert.IsTrue(fresh.IsFresh);
        Assert.IsTrue(fresh.Entry.IsBodyRetained);
        CollectionAssert.AreEqual(responseBody, fresh.BodyBytes);

        var lookupByExpired = store.TryGet(key, entry.ExpiresAt.AddSeconds(1), out var stale);
        Assert.IsTrue(lookupByExpired);
        Assert.IsNotNull(stale);
        Assert.IsFalse(stale.IsFresh);
        Assert.AreEqual(entry.Key.Identity, stale.Entry.Key.Identity);
        stale.Entry.VerifyBody(stale.BodyBytes ?? []);
        stale.Entry.VerifyResponseEvidence(response);
    }

    [TestMethod]
    public void Digest_only_semantic_scholar_records_reject_body_preservation()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("semantic_scholar", "semantic_scholar.bulk-search", OpenAlexParserId, OpenAlexParserVersion);
        var responseBody = Encoding.UTF8.GetBytes("{\"result\":\"semantic\"}");
        var response = CaptureResponse(key, responseBody, 200, "application/json");

        var bodyException = Assert.ThrowsExactly<SearchRuleException>(() =>
            store.Record(key, response, responseBody));
        Assert.AreEqual(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, bodyException.Category);

        var entry = store.Record(key, response, null);
        Assert.IsFalse(entry.IsBodyRetained);

        var lookup = store.TryGet(key, entry.StoredAt.AddHours(1), out var hit);
        Assert.IsTrue(lookup);
        Assert.IsNull(hit.BodyBytes);
    }

    [TestMethod]
    public void Crossref_records_are_denied_by_policy()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("crossref", "crossref.works", OpenAlexParserId, OpenAlexParserVersion);
        var response = CaptureResponse(key, Encoding.UTF8.GetBytes("{\"result\":\"crossref\"}"), 200, "application/json");

        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            store.Record(key, response, null));

        Assert.AreEqual(ProviderEvidenceCacheErrorCodes.CachePolicyDenied, exception.Category);
    }

    [TestMethod]
    public void Non_200_responses_cannot_be_cached()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("openalex", "openalex.works", OpenAlexParserId, OpenAlexParserVersion);
        var response = CaptureResponse(key, Encoding.UTF8.GetBytes("{\"result\":\"retry\"}"), 429, "application/json");

        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            store.Record(key, response, Encoding.UTF8.GetBytes("{\"result\":\"retry\"}")));

        Assert.AreEqual(ProviderEvidenceCacheErrorCodes.IncompleteResponse, exception.Category);
    }

    [TestMethod]
    public void Expired_cached_entry_is_reported_as_stale_but_retained_for_verification()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("openalex", "openalex.works", OpenAlexParserId, OpenAlexParserVersion);
        var responseBody = Encoding.UTF8.GetBytes("{\"result\":\"expires\"}");
        var response = CaptureResponse(key, responseBody, 200, "application/json");
        var entry = store.Record(key, response, responseBody);

        var staleLookup = store.TryGet(key, entry.ExpiresAt.AddMinutes(1), out var stale);
        Assert.IsTrue(staleLookup);
        Assert.IsNotNull(stale);
        Assert.IsFalse(stale.IsFresh);
        stale.Entry.VerifyBody(stale.BodyBytes ?? []);
        stale.Entry.VerifyResponseEvidence(response);
    }

    [TestMethod]
    public void Changed_response_appends_immutable_entry_and_advances_rebuildable_index()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);
        var key = BuildKey("openalex", "openalex.works", OpenAlexParserId, OpenAlexParserVersion);
        var firstBytes = Encoding.UTF8.GetBytes("{\"result\":\"first\"}");
        var secondBytes = Encoding.UTF8.GetBytes("{\"result\":\"second\"}");

        var first = store.Record(key, CaptureResponse(key, firstBytes, 200, "application/json"), firstBytes, ReceivedAt);
        var secondStoredAt = ReceivedAt.AddMinutes(1);
        var second = store.Record(key, CaptureResponse(key, secondBytes, 200, "application/json"), secondBytes, secondStoredAt);

        Assert.AreNotEqual(first.Digest, second.Digest);
        Assert.IsTrue(Directory.Exists(Path.Combine(workspace.Root, "entries", first.Digest.Value)));
        Assert.IsTrue(Directory.Exists(Path.Combine(workspace.Root, "entries", second.Digest.Value)));
        Assert.IsTrue(store.TryGet(key, secondStoredAt, out var latest));
        Assert.AreEqual(second.Digest, latest.Entry.Digest);

        File.Delete(Path.Combine(workspace.Root, "index.json"));
        store.RebuildIndex();
        Assert.IsTrue(store.TryGet(key, secondStoredAt, out var rebuilt));
        Assert.AreEqual(second.Digest, rebuilt.Entry.Digest);
    }

    [TestMethod]
    public void Parser_mismatch_prevents_cache_record_creation()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("openalex", "openalex.works", OpenAlexParserId, "1.0.0");
        var response = CaptureResponse(key, Encoding.UTF8.GetBytes("{\"result\":\"parser\"}"), 200, "application/json");

        var mismatchedKey = BuildKey("openalex", "openalex.works", "other-parser", "1.0.0");
        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            store.Record(mismatchedKey, response, Encoding.UTF8.GetBytes("{\"result\":\"parser\"}")));

        Assert.AreEqual(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, exception.Category);
    }

    [TestMethod]
    public void Sanitized_request_digest_mismatch_prevents_cache_record_creation()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("openalex", "openalex.works", OpenAlexParserId, OpenAlexParserVersion);
        var response = CaptureResponse(key, Encoding.UTF8.GetBytes("{\"result\":\"request\"}"), 200, "application/json");
        var requestMismatch = BuildKey(
            "openalex",
            "openalex.works",
            OpenAlexParserId,
            OpenAlexParserVersion,
            ContentDigest.Sha256Utf8("sanitized-request-mismatch"));

        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            store.Record(requestMismatch, response, Encoding.UTF8.GetBytes("{\"result\":\"request\"}")));

        Assert.AreEqual(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, exception.Category);
    }

    [TestMethod]
    public void Index_corruption_is_reported_with_store_index_corrupt_code()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("openalex", "openalex.works", OpenAlexParserId, OpenAlexParserVersion);
        var responseBody = Encoding.UTF8.GetBytes("{\"result\":\"index\"}");
        var response = CaptureResponse(key, responseBody, 200, "application/json");

        store.Record(key, response, responseBody);
        var indexPath = Path.Combine(workspace.Root, "index.json");
        File.WriteAllText(indexPath, "{ invalid-json }");

        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            store.TryGet(key, DateTimeOffset.UtcNow, out _));

        Assert.AreEqual(ProviderEvidenceCacheErrorCodes.StoreIndexCorrupt, exception.Category);
    }

    [TestMethod]
    public void Manifest_and_entry_dont_stash_request_urls_or_raw_url_fields()
    {
        using var workspace = new TemporaryDirectory();
        var store = new ProviderEvidenceCacheStore(workspace.Root);

        var key = BuildKey("openalex", "openalex.works", OpenAlexParserId, OpenAlexParserVersion);
        var responseBody = Encoding.UTF8.GetBytes("{\"result\":\"manifest\"}");
        var response = CaptureResponse(key, responseBody, 200, "application/json");
        var entry = store.Record(key, response, responseBody);

        var entryPath = Path.Combine(workspace.Root, "entries", entry.Digest.Value, "entry.json");
        var manifest = File.ReadAllText(entryPath);

        using var manifestJson = JsonDocument.Parse(manifest);
        foreach (var property in manifestJson.RootElement.EnumerateObject())
        {
            Assert.IsFalse(property.Name.Contains("url", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(property.Name.Contains("endpoint", StringComparison.OrdinalIgnoreCase));
        }

        Assert.IsFalse(manifest.Contains("http://", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(manifest.Contains("https://", StringComparison.OrdinalIgnoreCase));
    }

    private static ProviderEvidenceCacheKey BuildKey(
        string providerAlias,
        string operation,
        string parserId,
        string parserVersion,
        ContentDigest? sanitizedDigest = null)
    {
        return ProviderEvidenceCacheKey.Create(
            providerAlias,
            operation,
            sanitizedDigest ?? ContentDigest.Sha256Utf8($"{providerAlias}-{operation}-v1"),
            ContentDigest.Sha256Utf8($"page-{providerAlias}-{operation}"),
            parserId,
            parserVersion);
    }

    private static RuntimeProviderResponseEvidence CaptureResponse(
        ProviderEvidenceCacheKey key,
        byte[] responseBody,
        int statusCode,
        string mediaType)
    {
        var capture = typeof(RuntimeProviderResponseEvidence).GetMethod(
            "Capture",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(capture);

        return (RuntimeProviderResponseEvidence)capture.Invoke(
            null,
            [
                key.ProviderAlias,
                key.SanitizedRequestDigest,
                key.ParserId,
                key.ParserVersion,
                responseBody,
                statusCode,
                mediaType,
                RequestedAt,
                ReceivedAt,
                null
            ])!;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(Path.GetTempPath(), $"nexus-search-cache-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
