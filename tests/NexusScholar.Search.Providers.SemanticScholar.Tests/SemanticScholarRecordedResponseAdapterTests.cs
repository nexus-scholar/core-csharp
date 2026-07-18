using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Search.Providers.Live;
using NexusScholar.Search.Providers.SemanticScholar;

namespace NexusScholar.Search.Providers.SemanticScholar.Tests;

[TestClass]
public sealed class SemanticScholarRecordedResponseAdapterTests
{
    private static readonly DateTimeOffset RequestedAt = DateTimeOffset.Parse("2026-07-17T10:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset ReceivedAt = DateTimeOffset.Parse("2026-07-17T10:01:00Z", CultureInfo.InvariantCulture);

    [TestMethod]
    public void Bulk_request_descriptor_is_sanitized_and_translates_boolean_operators()
    {
        var (request, page) = SearchRequest();
        var descriptor = SemanticScholarRecordedResponseAdapter.Describe(request, page);

        Assert.AreEqual("GET", descriptor.Method);
        Assert.AreEqual(SemanticScholarRecordedResponseAdapter.ProviderAlias, descriptor.ProviderAlias);
        Assert.AreEqual(page.Digest.ToString(), descriptor.PageRequestDigest);
        Assert.IsTrue(
            descriptor.EndpointPathAndQuery.Contains(
                "/graph/v1/paper/search/bulk?query=artificial%20intelligence%20%2B%20transformer%20%7C%20graph%20-search&",
                StringComparison.Ordinal),
            descriptor.EndpointPathAndQuery);
        Assert.IsTrue(
            descriptor.EndpointPathAndQuery.Contains("&year=2022-2023", StringComparison.Ordinal),
            descriptor.EndpointPathAndQuery);
        Assert.IsFalse(descriptor.EndpointPathAndQuery.Contains("api_key", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Secret_bearing_queries_and_descriptors_are_rejected()
    {
        var bad = ProviderAcquisitionRequest.Create(
            "request-1",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "token=secret",
            new SearchYearRange(2022, 2022),
            null,
            2,
            0,
            false,
            RequestedAt);
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.Describe(bad, ProviderPageRequest.Create(bad, 0, 2, 0)));

        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.ValidateSanitizedDescriptor("/graph/v1/paper/search/bulk?api_key=abc"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.ValidateSanitizedDescriptor(
                "/graph/v1/paper/search/bulk?query=api_key%3Dsecret"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.ValidateSanitizedDescriptor(
                "/graph/v1/paper/search/bulk?token=not-a-validated-continuation"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.ValidateSanitizedDescriptor(
                "/graph/v1/paper/search/bulk?query=token%253Dsecret"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.ValidateSanitizedDescriptor(
                "/graph/v1/paper/search/bulk?query=%GG"));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.ValidateSanitizedDescriptor(
                "/graph/v1/paper/search/bulk?query=Bearer+abc"));

        var authorization = ProviderAcquisitionRequest.Create(
            "request-authorization",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "authorization=Bearer abc",
            null,
            null,
            2,
            0,
            false,
            RequestedAt);
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.Describe(
                authorization,
                ProviderPageRequest.Create(authorization, 0, 2, 0)));

        var arbitraryToken = ProviderAcquisitionRequest.Create(
            "request-arbitrary-token",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "token=arbitrary",
            null,
            null,
            2,
            0,
            false,
            RequestedAt);
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.Describe(
                arbitraryToken,
                ProviderPageRequest.Create(arbitraryToken, 0, 2, 0)));

        var malformedPercentQuery = ProviderAcquisitionRequest.Create(
            "request-malformed-percent",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "artificial%2intelligence",
            null,
            null,
            2,
            0,
            false,
            RequestedAt);
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.Describe(
                malformedPercentQuery,
                ProviderPageRequest.Create(malformedPercentQuery, 0, 2, 0)));
    }

    [TestMethod]
    public void First_page_rejects_cursor_and_bulk_page_size_is_capped_at_1000()
    {
        var (request, page) = SearchRequest();
        var firstWithCursor = ProviderPageRequest.Create(request, 0, 2, 0, "cursor");
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.Describe(request, firstWithCursor));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.Describe(request, ProviderPageRequest.Create(request, 0, 1001, 0)));
    }

    [TestMethod]
    public void Recorded_bulk_page_is_sorted_by_paper_id_and_preserves_chain_metadata()
    {
        var bytes = Fixture("search-s2-bulk-page.response.json");
        var (request, page) = SearchRequest();
        var evidence = Capture(request, page, bytes, 200, "application/json");

        var result = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(request, page, bytes, evidence);
        Assert.AreEqual(2, result.Sightings.Count);
        Assert.AreEqual("s2:s2-a", result.Sightings[0].Work.WorkIds.Ids.Single(id => id.ToString().StartsWith("s2:", StringComparison.Ordinal)).ToString());
        Assert.AreEqual("s2:s2-b", result.Sightings[1].Work.WorkIds.Ids.Single(id => id.ToString().StartsWith("s2:", StringComparison.Ordinal)).ToString());
        Assert.AreEqual("semantic_scholar:s2-a", result.Sightings[0].Work.SourceContext);
        Assert.AreEqual("s2-token-next", result.NextCursor);
        Assert.AreEqual(2, result.NextOffset);
        Assert.AreEqual("doi:10.1000/beta", result.Sightings[0].ProviderWorkId);
        Assert.IsFalse(result.IsPartial);
        Assert.IsTrue(result.Warnings.Count == 0);
    }

    [TestMethod]
    public void Pagination_chain_rejects_cursor_drift()
    {
        var (request, firstPage) = SearchRequest();
        var firstBytes = Fixture("search-s2-bulk-page.response.json");
        var firstEvidence = Capture(request, firstPage, firstBytes, 200, "application/json");
        var firstResult = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(request, firstPage, firstBytes, firstEvidence);
        Assert.AreEqual("s2-token-next", firstResult.NextCursor);

        var driftPage = ProviderPageRequest.Create(request, 1, 1000, 2, "wrong-token", firstResult.Digest);
        var secondBytes = Fixture("search-s2-bulk-final.response.json");
        var secondEvidence = Capture(request, driftPage, secondBytes, 200, "application/json");
        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(request, driftPage, secondBytes, secondEvidence, firstResult));

        Assert.AreEqual(ProviderAcquisitionErrorCodes.PaginationChainMismatch, exception.Category);
    }

    [TestMethod]
    public void Parsed_bulk_chain_marks_completion_with_matching_token()
    {
        var (request, firstPage) = SearchRequest();
        var firstBytes = Fixture("search-s2-bulk-page.response.json");
        var firstEvidence = Capture(request, firstPage, firstBytes, 200, "application/json");
        var firstResult = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(request, firstPage, firstBytes, firstEvidence);

        var secondPage = ProviderPageRequest.Create(request, 1, 1000, 2, firstResult.NextCursor, firstResult.Digest);
        StringAssert.Contains(
            SemanticScholarRecordedResponseAdapter.Describe(request, secondPage).EndpointPathAndQuery,
            "token=s2-token-next");
        var secondBytes = Fixture("search-s2-bulk-final.response.json");
        var secondEvidence = Capture(request, secondPage, secondBytes, 200, "application/json");
        var secondResult = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(
            request,
            secondPage,
            secondBytes,
            secondEvidence,
            firstResult);

        Assert.AreEqual(1, secondResult.Sightings.Count);
        Assert.AreEqual("s2:s2-c", secondResult.Sightings[0].Work.WorkIds.Ids.Single(id => id.ToString().StartsWith("s2:", StringComparison.Ordinal)).ToString());
        Assert.IsTrue(secondResult.IsComplete);
        Assert.IsFalse(secondResult.IsPartial);
        Assert.IsNull(secondResult.NextCursor);
        Assert.IsNull(secondResult.NextOffset);
    }

    [TestMethod]
    public void Mutated_bulk_response_is_rejected_before_parsing()
    {
        var bytes = Fixture("search-s2-bulk-page.response.json");
        var (request, page) = SearchRequest();
        var evidence = Capture(request, page, bytes, 200, "application/json");
        var mutated = bytes.ToArray();
        mutated[10] ^= 1;

        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(request, page, mutated, evidence));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.FixtureDigestMismatch, exception.Category);
    }

    [TestMethod]
    public void Schema_drift_and_malformed_bulk_responses_return_partial_evidence()
    {
        var (request, page) = SearchRequest();
        var malformed = Fixture("search-s2-malformed.response.json");
        var malformedEvidence = Capture(request, page, malformed, 200, "application/json");
        var malformedResult = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(request, page, malformed, malformedEvidence);
        Assert.IsTrue(malformedResult.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderSchemaDrift, malformedResult.PartialReason);

        var drift = Fixture("search-s2-schema-drift.response.json");
        var driftEvidence = Capture(request, page, drift, 200, "application/json");
        var driftResult = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(request, page, drift, driftEvidence);
        Assert.IsTrue(driftResult.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderSchemaDrift, driftResult.PartialReason);
    }

    [TestMethod]
    public void Wrong_parser_version_results_in_recorded_failure_evidence()
    {
        var (request, page) = SearchRequest();
        var bytes = Fixture("search-s2-bulk-page.response.json");
        var exact = Capture(request, page, bytes, 200, "application/json", parserVersion: "0.0.0");
        var result = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(request, page, bytes, exact);

        Assert.IsTrue(result.IsPartial);
        Assert.AreEqual("unsupported-parser-version", result.PartialReason);
    }

    [TestMethod]
    public void Batch_request_descriptor_has_fixed_fields_and_unique_ids_in_utf8_json_body()
    {
        var duplicateError = Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(["s2-b", "s2-a", "s2-b"]));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.InvalidProviderPage, duplicateError.Category);

        var batchRequest = SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(["s2-b", "s2-a", "s2-c"]);
        var body = batchRequest.CopySanitizedBody();
        var bodyText = Encoding.UTF8.GetString(body);
        Assert.AreEqual(
            "/graph/v1/paper/batch?fields=paperId%2CexternalIds%2Ctitle%2Cabstract%2Cyear%2Cvenue%2Cauthors%2CcitationCount",
            batchRequest.EndpointPathAndQuery);
        Assert.AreEqual("""{"ids":["s2-b","s2-a","s2-c"]}""", bodyText);
        Assert.IsFalse(bodyText.Contains("\"fields\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Batch_request_rejects_invalid_limits_and_empty_batch()
    {
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(Array.Empty<string>()));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(
                Enumerable.Range(1, 501).Select(index => $"s2-{index}").ToArray()));
    }

    [TestMethod]
    public void Batch_results_are_mapped_by_identifier_and_missing_ids_are_preserved_as_unresolved()
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-1",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "batch",
            null,
            null,
            3,
            0,
            true,
            RequestedAt);
        var page = ProviderPageRequest.Create(request, 0, 3, 0);
        var batchRequest = SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(["s2-beta", "s2-alpha", "s2-missing"]);
        var bytes = Fixture("search-s2-batch-shuffled.response.json");
        var evidence = SemanticScholarRecordedResponseAdapter.CaptureResponse(batchRequest, bytes, 200, "application/json", RequestedAt, ReceivedAt);
        var result = new SemanticScholarRecordedResponseAdapter().ParseRecordedBatchResponse(request, page, ["s2-beta", "s2-alpha", "s2-missing"], bytes, evidence);

        Assert.AreEqual(3, result.Sightings.Count);
        Assert.AreEqual("s2:s2-beta", result.Sightings[0].Work.WorkIds.Ids.Single(id => id.ToString().StartsWith("s2:", StringComparison.Ordinal)).ToString());
        Assert.AreEqual("s2:s2-alpha", result.Sightings[1].Work.WorkIds.Ids.Single(id => id.ToString().StartsWith("s2:", StringComparison.Ordinal)).ToString());
        Assert.IsTrue(result.Sightings[2].Work.IsUnresolvedCandidate);
        StringAssert.StartsWith(
            result.Sightings[2].Work.SourceContext,
            $"semantic_scholar:unresolved:{evidence.RawResponseDigest}:2:s2-missing");
        Assert.IsTrue(result.Warnings.Any(value => value.Contains("missing-batch-result", StringComparison.Ordinal)));
        Assert.AreEqual("0", result.Sightings[0].Work.RawData["raw_provider_item_index"]);
        Assert.AreEqual("1", result.Sightings[1].Work.RawData["raw_provider_item_index"]);
        Assert.AreEqual("2", result.Sightings[2].Work.RawData["raw_provider_item_index"]);
    }

    [TestMethod]
    public void Batch_results_map_requested_external_identifiers_without_assuming_response_order()
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-external",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "batch",
            null,
            null,
            2,
            0,
            false,
            RequestedAt);
        var page = ProviderPageRequest.Create(request, 0, 2, 0);
        var requestedIds = new[] { "ARXIV:2301.00001", "DOI:10.1000/beta" };
        var batchRequest = SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(requestedIds);
        var bytes = Fixture("search-s2-batch-shuffled.response.json");
        var evidence = SemanticScholarRecordedResponseAdapter.CaptureResponse(
            batchRequest,
            bytes,
            200,
            "application/json",
            RequestedAt,
            ReceivedAt);

        var result = new SemanticScholarRecordedResponseAdapter().ParseRecordedBatchResponse(
            request,
            page,
            requestedIds,
            bytes,
            evidence);

        Assert.AreEqual("s2:s2-alpha", result.Sightings[0].Work.WorkIds.Ids.Single(
            id => id.ToString().StartsWith("s2:", StringComparison.Ordinal)).ToString());
        Assert.AreEqual("s2:s2-beta", result.Sightings[1].Work.WorkIds.Ids.Single(
            id => id.ToString().StartsWith("s2:", StringComparison.Ordinal)).ToString());
        Assert.IsFalse(result.Warnings.Any(value => value.Contains("missing-batch-result", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Batch_parse_tolerates_null_and_missing_fields_as_warnings()
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-1",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "batch",
            null,
            null,
            3,
            0,
            true,
            RequestedAt);
        var page = ProviderPageRequest.Create(request, 0, 3, 0);
        var batchRequest = SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(["s2-beta", "s2-alpha"]);
        var bytes = Fixture("search-s2-batch-shuffled.response.json");
        var evidence = SemanticScholarRecordedResponseAdapter.CaptureResponse(batchRequest, bytes, 200, "application/json", RequestedAt, ReceivedAt);
        var result = new SemanticScholarRecordedResponseAdapter().ParseRecordedBatchResponse(request, page, ["s2-beta", "s2-alpha"], bytes, evidence);

        Assert.IsTrue(result.IsComplete);
        Assert.IsFalse(result.IsPartial);
        Assert.IsTrue(result.Warnings.Any(value => value.Contains("missing-title", StringComparison.Ordinal)));
        Assert.IsTrue(result.Warnings.Any(value => value.Contains("missing-year", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Batch_null_member_becomes_missing_result_instead_of_throwing()
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-null",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "batch",
            null,
            null,
            1,
            0,
            false,
            RequestedAt);
        var page = ProviderPageRequest.Create(request, 0, 1, 0);
        var ids = new[] { "s2-missing" };
        var batchRequest = SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(ids);
        var bytes = Encoding.UTF8.GetBytes("[null]");
        var evidence = SemanticScholarRecordedResponseAdapter.CaptureResponse(
            batchRequest,
            bytes,
            200,
            "application/json",
            RequestedAt,
            ReceivedAt);

        var result = new SemanticScholarRecordedResponseAdapter().ParseRecordedBatchResponse(
            request,
            page,
            ids,
            bytes,
            evidence);

        Assert.IsTrue(result.IsComplete);
        Assert.IsTrue(result.Sightings.Single().Work.IsUnresolvedCandidate);
        Assert.IsTrue(result.Warnings.Any(value => value.Contains("missing-batch-result", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Batch_identifier_collision_returns_digest_bound_failure()
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-collision",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "batch",
            null,
            null,
            1,
            0,
            false,
            RequestedAt);
        var page = ProviderPageRequest.Create(request, 0, 1, 0);
        var ids = new[] { "DOI:10.1000/collision" };
        var batchRequest = SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(ids);
        var bytes = Encoding.UTF8.GetBytes(
            """[{"paperId":"s2-a","externalIds":{"DOI":"10.1000/collision"}},{"paperId":"s2-b","externalIds":{"DOI":"10.1000/collision"}}]""");
        var evidence = SemanticScholarRecordedResponseAdapter.CaptureResponse(
            batchRequest,
            bytes,
            200,
            "application/json",
            RequestedAt,
            ReceivedAt);

        var result = new SemanticScholarRecordedResponseAdapter().ParseRecordedBatchResponse(
            request,
            page,
            ids,
            bytes,
            evidence);

        Assert.IsTrue(result.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderSchemaDrift, result.PartialReason);
        Assert.AreEqual(evidence.Digest, result.Response.Digest);
    }

    [TestMethod]
    public void Bulk_null_member_returns_schema_failure_evidence()
    {
        var (request, page) = SearchRequest();
        var bytes = Encoding.UTF8.GetBytes("""{"total":1,"token":null,"data":[null]}""");
        var evidence = Capture(request, page, bytes, 200, "application/json");

        var result = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(
            request,
            page,
            bytes,
            evidence);

        Assert.IsTrue(result.IsPartial);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderSchemaDrift, result.PartialReason);
    }

    [TestMethod]
    public void Batch_mutated_bytes_are_rejected_before_parse()
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-1",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "batch",
            null,
            null,
            2,
            0,
            true,
            RequestedAt);
        var page = ProviderPageRequest.Create(request, 0, 2, 0);
        var batchRequest = SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(["s2-beta", "s2-alpha"]);
        var bytes = Fixture("search-s2-batch-shuffled.response.json");
        var evidence = SemanticScholarRecordedResponseAdapter.CaptureResponse(batchRequest, bytes, 200, "application/json", RequestedAt, ReceivedAt);
        var mutated = bytes.ToArray();
        mutated[^2] ^= 1;

        Assert.ThrowsExactly<SearchRuleException>(() =>
            new SemanticScholarRecordedResponseAdapter().ParseRecordedBatchResponse(
                request,
                page,
                ["s2-beta", "s2-alpha"],
                mutated,
                evidence));
    }

    private static (ProviderAcquisitionRequest Request, ProviderPageRequest Page) SearchRequest()
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-1",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "artificial intelligence AND transformer OR graph NOT search",
            new SearchYearRange(2022, 2023),
            null,
            2000,
            0,
            false,
            RequestedAt);
        return (request, ProviderPageRequest.Create(request, 0, 1000, 0));
    }

    private static RuntimeProviderResponseEvidence Capture(
        ProviderAcquisitionRequest request,
        ProviderPageRequest page,
        byte[] bytes,
        int status,
        string mediaType,
        string? parserVersion = null) =>
        SemanticScholarRecordedResponseAdapter.CaptureResponse(request, page, bytes, status, mediaType, RequestedAt, ReceivedAt, parserVersion: parserVersion);

    private static byte[] Fixture(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", name));
}
