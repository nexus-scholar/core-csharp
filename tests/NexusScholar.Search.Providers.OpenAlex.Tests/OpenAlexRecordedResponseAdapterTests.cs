using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Search;

namespace NexusScholar.Search.Providers.OpenAlex.Tests;

[TestClass]
public sealed class OpenAlexRecordedResponseAdapterTests
{
    private static readonly DateTimeOffset RequestedAt = DateTimeOffset.Parse("2026-07-17T10:00:00Z");
    private static readonly DateTimeOffset ReceivedAt = DateTimeOffset.Parse("2026-07-17T10:01:00Z");

    [TestMethod]
    public void Request_descriptor_is_sanitized_and_contains_fixed_select_per_page_and_cursor()
    {
        var (request, page) = Request();
        var descriptor = OpenAlexRecordedResponseAdapter.Describe(request, page);

        Assert.AreEqual("GET", descriptor.Method);
        Assert.AreEqual(OpenAlexRecordedResponseAdapter.ProviderAlias, descriptor.ProviderAlias);
        Assert.AreEqual(page.Digest.ToString(), descriptor.PageRequestDigest);
        Assert.IsFalse(descriptor.EndpointPathAndQuery.Contains("api_key", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(
            "/works?search=artificial%20intelligence&select=id%2Cdoi%2Cdisplay_name%2Cpublication_year%2Ccited_by_count%2Cauthorships%2Cprimary_location&filter=publication_year%3A2022-2023%2Clanguage%3Aen&per_page=2&cursor=%2A",
            descriptor.EndpointPathAndQuery);

        var continuation = ProviderPageRequest.Create(request, 1, 2, 2, "cursor-page-2", page.Digest);
        var continuationDescriptor = OpenAlexRecordedResponseAdapter.Describe(request, continuation);
        Assert.AreEqual(
            "/works?search=artificial%20intelligence&select=id%2Cdoi%2Cdisplay_name%2Cpublication_year%2Ccited_by_count%2Cauthorships%2Cprimary_location&filter=publication_year%3A2022-2023%2Clanguage%3Aen&per_page=2&cursor=cursor-page-2",
            continuationDescriptor.EndpointPathAndQuery);
    }

    [TestMethod]
    public void Recorded_page_preserves_duplicate_sightings_with_openalex_ids_and_doi()
    {
        var bytes = Fixture("search-openalex-recorded-page.response.json");
        var (request, page) = Request(includeRawData: true);
        var evidence = Capture(request, page, bytes, 200, "application/json");

        var result = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(
            request,
            page,
            bytes,
            evidence);

        Assert.AreEqual(2, result.Sightings.Count);
        Assert.AreEqual("doi:10.1000/alpha", result.Sightings[0].ProviderWorkId);
        Assert.AreEqual(result.Sightings[0].ProviderWorkId, result.Sightings[1].ProviderWorkId);
        CollectionAssert.AreEqual(
            new[] { "doi:10.1000/alpha", "openalex:w111" },
            result.Sightings[0].WorkIds.OrderBy(value => value).ToArray());
        CollectionAssert.AreEqual(
            new[] { "doi:10.1000/alpha", "openalex:w111" },
            result.Sightings[1].WorkIds.OrderBy(value => value).ToArray());
        Assert.AreEqual(2, result.NextOffset);
        Assert.AreEqual("cursor-page-2", result.NextCursor);
        Assert.AreEqual("openalex:w111", result.Sightings[0].Work.SourceContext);
        Assert.AreEqual(2, result.Sightings.Count(s =>
            s.Work.RawData.ContainsKey("raw_provider_response_digest") &&
            s.Work.RawData.ContainsKey("raw_provider_item_index") &&
            s.Work.RawData["raw_provider_response_digest"] == evidence.RawResponseDigest.ToString()));
        Assert.AreEqual(evidence.RawResponseDigest, result.Response.RawResponseDigest);
        Assert.AreEqual(OpenAlexRecordedResponseAdapter.ParserId, result.Response.ParserId);
    }

    [TestMethod]
    public void Chain_verification_rejects_cursor_drift()
    {
        var (request, page0) = Request();
        var bytes0 = Fixture("search-openalex-recorded-page.response.json");
        var evidence0 = Capture(request, page0, bytes0, 200, "application/json");
        var first = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(request, page0, bytes0, evidence0);

        var driftPage = ProviderPageRequest.Create(request, 1, 2, 2, "wrong-cursor", first.Digest);
        var bytes1 = Fixture("search-openalex-recorded-page-two.response.json");
        var evidence1 = Capture(request, driftPage, bytes1, 200, "application/json");
        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(
                request,
                driftPage,
                bytes1,
                evidence1,
                first));

        Assert.AreEqual(ProviderAcquisitionErrorCodes.PaginationChainMismatch, exception.Category);
    }

    [TestMethod]
    public void Result_total_drift_returns_chain_failure_evidence()
    {
        var (request, firstPage) = Request();
        var firstBytes = Fixture("search-openalex-recorded-page.response.json");
        var firstEvidence = Capture(request, firstPage, firstBytes, 200, "application/json");
        var first = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(
            request,
            firstPage,
            firstBytes,
            firstEvidence);
        var secondPage = ProviderPageRequest.Create(request, 1, 2, 2, first.NextCursor, first.Digest);
        var secondBytes = Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(Fixture("search-openalex-recorded-page-two.response.json"))
                .Replace("\"count\": 4", "\"count\": 5", StringComparison.Ordinal));
        var secondEvidence = Capture(request, secondPage, secondBytes, 200, "application/json");

        var result = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(
            request,
            secondPage,
            secondBytes,
            secondEvidence,
            first);

        Assert.IsTrue(result.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.PaginationChainMismatch, result.PartialReason);
    }

    [TestMethod]
    public void Null_result_member_returns_schema_failure_evidence()
    {
        var (request, page) = Request();
        var bytes = Encoding.UTF8.GetBytes(
            """{"meta":{"count":1,"per_page":2,"next_cursor":null},"results":[null]}""");
        var evidence = Capture(request, page, bytes, 200, "application/json");

        var result = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(
            request,
            page,
            bytes,
            evidence);

        Assert.IsTrue(result.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderSchemaDrift, result.PartialReason);
    }

    [TestMethod]
    public void Page_size_is_enforced_to_provider_limit()
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-1",
            "openalex",
            "artificial intelligence",
            new SearchYearRange(2022, 2023),
            "en",
            250,
            0,
            false,
            RequestedAt);
        var page = ProviderPageRequest.Create(request, 0, 101, 0);

        Assert.ThrowsExactly<SearchRuleException>(() =>
            OpenAlexRecordedResponseAdapter.Describe(request, page));
    }

    [TestMethod]
    public void Recorded_unresolved_item_is_preserved_without_stable_identity()
    {
        var bytes = Fixture("search-openalex-unresolved.response.json");
        var (request, page) = Request(1, includeRawData: true);
        var evidence = Capture(request, page, bytes, 200, "application/json");

        var result = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(
            request,
            page,
            bytes,
            evidence);

        Assert.AreEqual(1, result.Sightings.Count);
        Assert.IsTrue(result.Sightings[0].Work.IsUnresolvedCandidate);
        StringAssert.StartsWith(
            result.Sightings[0].Work.SourceContext,
            $"openalex:unresolved:{evidence.RawResponseDigest}:0");
        Assert.IsTrue(result.Sightings[0].Work.RawData.ContainsKey("raw_provider_response_digest"));
        Assert.IsTrue(result.Sightings[0].Work.RawData.ContainsKey("raw_provider_item_index"));
    }

    [TestMethod]
    public void Secret_shaped_queries_and_descriptors_are_rejected()
    {
        var badQuery = ProviderAcquisitionRequest.Create(
            "request-1",
            "openalex",
            "token=secret",
            null,
            null,
            2,
            0,
            false,
            RequestedAt);

        Assert.ThrowsExactly<SearchRuleException>(() =>
            OpenAlexRecordedResponseAdapter.Describe(badQuery, ProviderPageRequest.Create(badQuery, 0, 2, 0)));

        Assert.ThrowsExactly<SearchRuleException>(() =>
            OpenAlexRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?api_key=abc"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            OpenAlexRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?search=ok&api-key=abc"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            OpenAlexRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?search=api_key%3Dsecret"));

        var authorization = ProviderAcquisitionRequest.Create(
            "request-authorization",
            "openalex",
            "authorization=Bearer abc",
            null,
            null,
            2,
            0,
            false,
            RequestedAt);
        Assert.ThrowsExactly<SearchRuleException>(() =>
            OpenAlexRecordedResponseAdapter.Describe(
                authorization,
                ProviderPageRequest.Create(authorization, 0, 2, 0)));
    }

    [TestMethod]
    public void Mutated_response_bytes_fail_digest_verification_before_parsing()
    {
        var bytes = Fixture("search-openalex-recorded-page.response.json");
        var (request, page) = Request();
        var evidence = Capture(request, page, bytes, 200, "application/json");
        var mutated = bytes.ToArray();
        mutated[10] ^= 1;

        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(request, page, mutated, evidence));

        Assert.AreEqual(ProviderAcquisitionErrorCodes.FixtureDigestMismatch, exception.Category);
    }

    [TestMethod]
    public void Malformed_schema_and_drift_and_status_failures_return_partial_evidence()
    {
        var (request, page) = Request();

        var malformed = Fixture("search-openalex-malformed.response.json");
        var malformedEvidence = Capture(request, page, malformed, 200, "application/json");
        var malformedResult = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(request, page, malformed, malformedEvidence);
        Assert.IsTrue(malformedResult.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderSchemaDrift, malformedResult.PartialReason);

        var schema = Fixture("search-openalex-schema-drift.response.json");
        var schemaEvidence = Capture(request, page, schema, 200, "application/json");
        var schemaResult = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(request, page, schema, schemaEvidence);
        Assert.IsTrue(schemaResult.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderSchemaDrift, schemaResult.PartialReason);

        var drift = Fixture("search-openalex-page-drift.response.json");
        var driftEvidence = Capture(request, page, drift, 200, "application/json");
        var driftResult = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(request, page, drift, driftEvidence);
        Assert.IsTrue(driftResult.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.PaginationChainMismatch, driftResult.PartialReason);

        var rateLimited = Fixture("search-openalex-recorded-page.response.json");
        var limitedEvidence = Capture(request, page, rateLimited, 429, "application/json", new Dictionary<string, string> { ["retry-after"] = "3" });
        var limitedResult = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(request, page, rateLimited, limitedEvidence);
        Assert.IsTrue(limitedResult.IsPartial);
        Assert.AreEqual("http-429", limitedResult.PartialReason);
        Assert.AreEqual("3", limitedResult.Attempt.RetryAfter);
    }

    [TestMethod]
    public void Second_page_preserves_cursor_chain_and_marks_completion()
    {
        var (request, page0) = Request();
        var bytes0 = Fixture("search-openalex-recorded-page.response.json");
        var evidence0 = Capture(request, page0, bytes0, 200, "application/json");
        var first = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(request, page0, bytes0, evidence0);

        Assert.AreEqual("cursor-page-2", first.NextCursor);
        Assert.AreEqual(2, first.NextOffset);

        var page1 = ProviderPageRequest.Create(request, 1, 2, 2, first.NextCursor, first.Digest);
        var bytes1 = Fixture("search-openalex-recorded-page-two.response.json");
        var evidence1 = Capture(request, page1, bytes1, 200, "application/json");
        var second = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(request, page1, bytes1, evidence1, first);

        Assert.IsTrue(second.IsComplete);
        Assert.IsFalse(second.IsPartial);
        Assert.AreEqual(2, second.Sightings.Count);
        Assert.IsNull(second.NextCursor);
        Assert.IsNull(second.NextOffset);
        Assert.AreEqual("complete", second.Attempt.CompletionState);
    }

    private static (ProviderAcquisitionRequest Request, ProviderPageRequest Page) Request(
        int pageSize = 2,
        bool includeRawData = false)
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-1",
            "openalex",
            "artificial intelligence",
            new SearchYearRange(2022, 2023),
            "en",
            4,
            0,
            includeRawData,
            RequestedAt);

        var page = ProviderPageRequest.Create(request, 0, pageSize, 0);
        return (request, page);
    }

    private static RuntimeProviderResponseEvidence Capture(
        ProviderAcquisitionRequest request,
        ProviderPageRequest page,
        byte[] bytes,
        int status,
        string mediaType,
        IReadOnlyDictionary<string, string>? headers = null) =>
        OpenAlexRecordedResponseAdapter.CaptureResponse(request, page, bytes, status, mediaType, RequestedAt, ReceivedAt, headers);

    private static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
}
