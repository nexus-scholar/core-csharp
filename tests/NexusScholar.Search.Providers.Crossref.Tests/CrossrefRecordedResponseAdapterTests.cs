using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Search.Providers.Crossref;

namespace NexusScholar.Search.Providers.Crossref.Tests;

[TestClass]
public sealed class CrossrefRecordedResponseAdapterTests
{
    private static readonly DateTimeOffset RequestedAt = DateTimeOffset.Parse("2026-07-17T10:00:00Z");
    private static readonly DateTimeOffset AcceptedAt = DateTimeOffset.Parse("2026-07-17T10:01:00Z");
    private static readonly DateTimeOffset ReceivedAt = DateTimeOffset.Parse("2026-07-17T10:02:00Z");

    [TestMethod]
    public void Request_descriptor_is_sanitized_and_bound_to_page()
    {
        var (request, page) = Request();
        var descriptor = new CrossrefRecordedResponseAdapter().DescribeRequest(request, page);

        Assert.AreEqual("GET", descriptor.Method);
        Assert.AreEqual(
            "/works?query.bibliographic=artificial%20intelligence&rows=2&offset=0&filter=from-pub-date%3A2022%2Cuntil-pub-date%3A2023",
            descriptor.EndpointPathAndQuery);
        Assert.AreEqual(page.Digest.ToString(), descriptor.PageRequestDigest);
        Assert.IsFalse(descriptor.EndpointPathAndQuery.Contains("mailto", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Recorded_page_preserves_duplicate_sightings_and_exact_raw_evidence()
    {
        var bytes = Fixture("search-crossref-recorded-page.response.json");
        var (request, page) = Request(includeRawData: true);
        var fixture = Accept("crossref-recorded-page", bytes, "search-crossref-recorded-page.response.json");

        var result = new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
            request,
            page,
            fixture,
            bytes,
            200,
            "application/json",
            ReceivedAt,
            new Dictionary<string, string>
            {
                ["x-rate-limit-limit"] = "10",
                ["x-rate-limit-interval"] = "1s"
            });

        Assert.AreEqual(2, result.Sightings.Count);
        Assert.AreEqual(result.Sightings[0].ProviderWorkId, result.Sightings[1].ProviderWorkId);
        Assert.AreEqual(2, result.NextOffset);
        Assert.IsFalse(result.IsComplete);
        Assert.IsFalse(result.IsPartial);
        Assert.AreEqual(fixture.RawResponseDigest, result.RawResponse.RawResponseDigest);
        Assert.AreEqual("10", result.Attempt.RateLimitLimit);
        Assert.IsTrue(result.Sightings.All(item =>
            item.Work.RawData.ContainsKey("raw_provider_response_digest") &&
            item.Work.RawData.ContainsKey("raw_provider_item_index") &&
            !item.Work.RawData.ContainsKey("raw_provider_payload")));
        Assert.AreEqual(CrossrefRecordedResponseAdapter.ProviderAlias, result.RawResponse.ProviderAlias);
        Assert.AreEqual(fixture.FixtureId, result.RawResponse.FixtureId);
        Assert.IsTrue(result.Attempt.Digest.IsValid);
        Assert.IsTrue(result.Digest.IsValid);
        var replay = new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
            request, page, fixture, bytes, 200, "application/json", ReceivedAt,
            new Dictionary<string, string>
            {
                ["x-rate-limit-limit"] = "10",
                ["x-rate-limit-interval"] = "1s"
            });
        Assert.AreEqual(result.Digest, replay.Digest);
    }

    [TestMethod]
    public void Mutated_fixture_bytes_are_rejected_before_parsing()
    {
        var bytes = Fixture("search-crossref-recorded-page.response.json");
        var fixture = Accept("crossref-recorded-page", bytes, "search-crossref-recorded-page.response.json");
        var mutated = bytes.ToArray();
        mutated[^2] ^= 1;
        var (request, page) = Request();

        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
                request, page, fixture, mutated, 200, "application/json", ReceivedAt));

        Assert.AreEqual(ProviderAcquisitionErrorCodes.FixtureDigestMismatch, exception.Category);
    }

    [TestMethod]
    public void Schema_drift_and_parser_version_mismatch_are_rejected()
    {
        var drift = Fixture("search-crossref-schema-drift.response.json");
        var (request, page) = Request();
        var fixture = Accept("schema-drift", drift, "search-crossref-schema-drift.response.json");
        var adapter = new CrossrefRecordedResponseAdapter();

        var schema = adapter.ParseRecordedResponse(request, page, fixture, drift, 200, "application/json", ReceivedAt);
        Assert.AreEqual(ProviderAcquisitionErrorCodes.ProviderSchemaDrift, schema.PartialReason);
        Assert.AreEqual(fixture.RawResponseDigest, schema.RawResponse.RawResponseDigest);

        var wrongParser = RecordedProviderFixtureEvidence.AcceptRetainedLocal(
            "wrong-parser", "fixtures/conformance/search/search-crossref-schema-drift.response.json",
            "fixture-generator", "fixture-generator", AcceptedAt, "negative fixture",
            CrossrefRecordedResponseAdapter.ParserId, "2.0.0", drift);
        var parser = adapter.ParseRecordedResponse(request, page, wrongParser, drift, 200, "application/json", ReceivedAt);
        Assert.AreEqual("unsupported-parser-version", parser.PartialReason);
        Assert.IsTrue(parser.RawResponse.Digest.IsValid);
    }

    [TestMethod]
    public void Rate_limit_response_preserves_observed_evidence_without_retry_policy()
    {
        var bytes = Fixture("search-crossref-rate-limit-response.response.json");
        var (request, page) = Request();
        var result = new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
            request,
            page,
            Accept("crossref-rate-limit", bytes, "search-crossref-rate-limit-response.response.json"),
            bytes,
            429,
            "application/json",
            ReceivedAt,
            new Dictionary<string, string> { ["Retry-After"] = "3", ["X-Rate-Limit-Limit"] = "5" });

        Assert.IsTrue(result.IsPartial);
        Assert.AreEqual("http-429", result.PartialReason);
        Assert.AreEqual("3", result.Attempt.RetryAfter);
        Assert.AreEqual("5", result.Attempt.RateLimitLimit);
        Assert.AreEqual(0, result.Sightings.Count);
    }

    [TestMethod]
    public void Short_page_before_total_is_explicitly_partial()
    {
        var bytes = Fixture("search-crossref-partial-page.response.json");
        var (request, page) = Request();
        var result = new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
            request, page, Accept("partial", bytes, "search-crossref-partial-page.response.json"),
            bytes, 200, "application/json", ReceivedAt);

        Assert.IsTrue(result.IsPartial);
        Assert.AreEqual("short-page-before-total-results", result.PartialReason);
        Assert.IsNull(result.NextOffset);
    }

    [TestMethod]
    public void Missing_doi_remains_an_unresolved_sighting()
    {
        var bytes = Fixture("search-crossref-no-doi.response.json");
        var (request, page) = Request();
        var result = new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
            request, page, Accept("no-doi", bytes, "search-crossref-no-doi.response.json"),
            bytes, 200, "application/json", ReceivedAt);

        Assert.IsTrue(result.IsComplete);
        Assert.IsTrue(result.Sightings[0].Work.IsUnresolvedCandidate);
        Assert.IsTrue(result.Warnings.Contains("item-1-missing-doi"));
    }

    [TestMethod]
    public void Secret_bearing_or_cross_request_descriptors_are_rejected()
    {
        var secret = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?mailto=user@example.test"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, secret.Category);
        var authorization = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?authorization=Bearer%20abc"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, authorization.Category);
        var apiKey = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?api_key=abc"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, apiKey.Category);
        var contact = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?contact=operator%40example.test"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, contact.Category);
        var email = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?email=operator%40example.test"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, email.Category);
        var token = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?token=opaque-value"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, token.Category);
        var url = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?query=https%3A%2F%2Fexample.test"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, url.Category);
        var doubleEncoded = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?query=token%253Dsecret"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, doubleEncoded.Category);
        var malformedPercent = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?query=%GG"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, malformedPercent.Category);
        var plusBearer = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?query=Bearer+abc"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, plusBearer.Category);
        var bareContact = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?query=operator%40example.test"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, bareContact.Category);
        var signedCredential = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor("/works?x-amz-signature=abc"));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, signedCredential.Category);

        var (request, _) = Request();
        var other = ProviderAcquisitionRequest.Create(
            "other", "crossref", "other query", null, null, 2, 0, false, RequestedAt);
        var foreignPage = ProviderPageRequest.Create(other, 0, 2, 0);
        var mismatch = Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().DescribeRequest(request, foreignPage));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.PaginationChainMismatch, mismatch.Category);

        var secretQuery = ProviderAcquisitionRequest.Create(
            "secret", "crossref", "token=sk-test", null, null, 2, 0, false, RequestedAt);
        var secretPage = ProviderPageRequest.Create(secretQuery, 0, 2, 0);
        var queryException = Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().DescribeRequest(secretQuery, secretPage));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, queryException.Category);

        foreach (var query in new[]
        {
            "authorization=Bearer abc",
            "api_key=top-secret-value",
            "contact=user@example.test"
        })
        {
            var rejected = ProviderAcquisitionRequest.Create(
                "secret-value", "crossref", query, null, null, 2, 0, false, RequestedAt);
            var rejectedPage = ProviderPageRequest.Create(rejected, 0, 2, 0);
            var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
                new CrossrefRecordedResponseAdapter().DescribeRequest(rejected, rejectedPage));
            Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, exception.Category);
        }

        var safe = ProviderAcquisitionRequest.Create(
            "safe-query", "crossref", "tokenization methods", null, null, 2, 0, false, RequestedAt);
        var safePage = ProviderPageRequest.Create(safe, 0, 2, 0);
        StringAssert.Contains(
            new CrossrefRecordedResponseAdapter().DescribeRequest(safe, safePage).EndpointPathAndQuery,
            "tokenization%20methods");

        var malformedQuery = ProviderAcquisitionRequest.Create(
            "malformed-percent",
            "crossref",
            "artificial%2intelligence",
            null,
            null,
            2,
            0,
            false,
            RequestedAt);
        var malformedPercentQueryPage = ProviderPageRequest.Create(malformedQuery, 0, 2, 0);
        var malformedPercentQuery = Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().DescribeRequest(malformedQuery, malformedPercentQueryPage));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, malformedPercentQuery.Category);

        var arbitraryToken = ProviderAcquisitionRequest.Create(
            "arbitrary-token",
            "crossref",
            "token=arbitrary",
            null,
            null,
            2,
            0,
            false,
            RequestedAt);
        var arbitraryTokenPage = ProviderPageRequest.Create(arbitraryToken, 0, 2, 0);
        var arbitraryTokenException = Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().DescribeRequest(arbitraryToken, arbitraryTokenPage));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, arbitraryTokenException.Category);

        var bareContactQuery = ProviderAcquisitionRequest.Create(
            "bare-contact", "crossref", "operator@example.test", null, null, 2, 0, false, RequestedAt);
        var bareContactPage = ProviderPageRequest.Create(bareContactQuery, 0, 2, 0);
        Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().DescribeRequest(bareContactQuery, bareContactPage));
    }

    [TestMethod]
    public void Unadmitted_observed_headers_are_rejected_before_result_creation()
    {
        var bytes = Fixture("search-crossref-recorded-page.response.json");
        var (request, page) = Request();
        foreach (var header in new[] { "Authorization", "X-Api-Key", "X-Contact-Email", "Cookie" })
        {
            var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
                new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
                    request,
                    page,
                    Accept("secret-header", bytes, "search-crossref-recorded-page.response.json"),
                    bytes,
                    200,
                    "application/json",
                    ReceivedAt,
                    new Dictionary<string, string> { [header] = "secret-value" }));

            Assert.AreEqual(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, exception.Category);
        }
    }

    [TestMethod]
    public void Pagination_chain_rejects_page_drift()
    {
        var bytes = Fixture("search-crossref-recorded-page.response.json");
        var (request, page) = Request();
        var current = new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
            request, page, Accept("page-one", bytes, "search-crossref-recorded-page.response.json"),
            bytes, 200, "application/json", ReceivedAt);
        var correctNext = ProviderPageRequest.Create(
            request, 1, 1, 2, previousPageResultDigest: current.Digest);
        CrossrefRecordedResponseAdapter.VerifyNextPage(current, correctNext);

        var drifted = ProviderPageRequest.Create(
            request, 1, 1, 2, previousPageResultDigest: current.Attempt.Digest);
        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            CrossrefRecordedResponseAdapter.VerifyNextPage(current, drifted));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.PaginationChainMismatch, exception.Category);
    }

    [TestMethod]
    public void Later_page_cannot_be_parsed_without_the_preceding_result()
    {
        var bytes = Fixture("search-crossref-recorded-page.response.json");
        var (request, page) = Request();
        var current = new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
            request,
            page,
            Accept("page-one", bytes, "search-crossref-recorded-page.response.json"),
            bytes,
            200,
            "application/json",
            ReceivedAt);
        var next = ProviderPageRequest.Create(
            request,
            1,
            1,
            2,
            previousPageResultDigest: current.Digest);

        var exception = Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
                request,
                next,
                Accept("page-two", bytes, "search-crossref-recorded-page.response.json"),
                bytes,
                200,
                "application/json",
                ReceivedAt));

        Assert.AreEqual(ProviderAcquisitionErrorCodes.PaginationChainMismatch, exception.Category);
    }

    [TestMethod]
    public void Retained_malformed_and_page_window_drift_responses_return_failure_evidence()
    {
        var (request, page) = Request();
        var adapter = new CrossrefRecordedResponseAdapter();
        foreach (var (file, category) in new[]
        {
            ("search-crossref-malformed.response.json", ProviderAcquisitionErrorCodes.ProviderSchemaDrift),
            ("search-crossref-page-window-drift.response.json", ProviderAcquisitionErrorCodes.PaginationChainMismatch)
        })
        {
            var bytes = Fixture(file);
            var result = adapter.ParseRecordedResponse(
                request, page, Accept(Path.GetFileNameWithoutExtension(file), bytes, file),
                bytes, 200, "application/json", ReceivedAt);
            Assert.AreEqual(category, result.PartialReason);
            Assert.AreEqual(ContentDigest.Sha256(bytes), result.RawResponse.RawResponseDigest);
            Assert.IsTrue(result.Attempt.Digest.IsValid);
        }
    }

    [TestMethod]
    public void Empty_nonfinal_page_is_partial_not_complete()
    {
        var bytes = Fixture("search-crossref-empty-page.response.json");
        var (request, page) = Request();
        var result = new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
            request, page, Accept("empty-page", bytes, "search-crossref-empty-page.response.json"),
            bytes, 200, "application/json", ReceivedAt);

        Assert.IsFalse(result.IsComplete);
        Assert.IsTrue(result.IsPartial);
        Assert.AreEqual("short-page-before-total-results", result.PartialReason);
    }

    [TestMethod]
    public void Page_window_cannot_exceed_acquisition_max_results()
    {
        var (request, _) = Request();
        Assert.ThrowsExactly<SearchRuleException>(() => ProviderPageRequest.Create(request, 0, 4, 0));
        Assert.ThrowsExactly<SearchRuleException>(() => ProviderPageRequest.Create(request, 1, 2, 2));
        Assert.ThrowsExactly<SearchRuleException>(() => ProviderPageRequest.Create(request, 0, 2, 1));
    }

    [TestMethod]
    public void Invalid_request_alias_descriptor_and_retention_are_rejected()
    {
        Assert.ThrowsExactly<SearchRuleException>(() =>
            ProviderAcquisitionRequest.Create("bad", "crossref", "q", null, null, 0, 0, false, RequestedAt));
        Assert.ThrowsExactly<SearchRuleException>(() =>
            ProviderAcquisitionRequest.Create("bad", "crossref", "query", null, null, 1, -1, false, RequestedAt));

        var other = ProviderAcquisitionRequest.Create(
            "other", "openalex", "query", null, null, 1, 0, false, RequestedAt);
        var otherPage = ProviderPageRequest.Create(other, 0, 1, 0);
        var alias = Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().DescribeRequest(other, otherPage));
        Assert.AreEqual(SearchErrorCodes.UnknownProviderAlias, alias.Category);

        foreach (var descriptor in new[]
        {
            "https://api.crossref.org/works",
            "/works?api-key=value",
            "/works?authorization=bearer",
            "/works?contactEmail=user%40example.test"
        })
        {
            Assert.ThrowsExactly<SearchRuleException>(() =>
                CrossrefRecordedResponseAdapter.ValidateSanitizedDescriptor(descriptor));
        }

        var bytes = Fixture("search-crossref-recorded-page.response.json");
        var fixture = Accept("retention", bytes, "search-crossref-recorded-page.response.json");
        Assert.ThrowsExactly<SearchRuleException>(() => new ProviderRawResponseEvidence(
            "crossref",
            fixture.FixtureId,
            fixture.FixtureRelativePath,
            fixture.Digest,
            fixture.RawResponseDigest,
            fixture.ByteLength,
            200,
            "application/json",
            ReceivedAt,
            "digest-only-provider-terms"));
    }

    [TestMethod]
    public void Provider_evidence_rejects_default_timestamps()
    {
        var requestError = Assert.ThrowsExactly<SearchRuleException>(() =>
            ProviderAcquisitionRequest.Create(
                "default-time",
                "crossref",
                "query",
                null,
                null,
                1,
                0,
                false,
                default));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, requestError.Category);

        var bytes = Fixture("search-crossref-recorded-page.response.json");
        var (request, page) = Request();
        var responseError = Assert.ThrowsExactly<SearchRuleException>(() =>
            new CrossrefRecordedResponseAdapter().ParseRecordedResponse(
                request,
                page,
                Accept("default-time", bytes, "search-crossref-recorded-page.response.json"),
                bytes,
                200,
                "application/json",
                default));
        Assert.AreEqual(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, responseError.Category);
    }

    [TestMethod]
    public void Raw_response_digest_binds_provider_alias()
    {
        var bytes = Fixture("search-crossref-recorded-page.response.json");
        var fixture = Accept("provider-binding", bytes, "search-crossref-recorded-page.response.json");
        ProviderRawResponseEvidence Create(string providerAlias) => new(
            providerAlias,
            fixture.FixtureId,
            fixture.FixtureRelativePath,
            fixture.Digest,
            fixture.RawResponseDigest,
            fixture.ByteLength,
            200,
            "application/json",
            ReceivedAt,
            RecordedProviderFixtureEvidence.RetentionDisposition);

        Assert.AreNotEqual(Create("crossref").Digest, Create("openalex").Digest);
    }

    [TestMethod]
    public void Declared_retained_fixture_paths_and_digests_match_repository_files()
    {
        var root = FindRepositoryRoot();
        var fixtureDirectory = Path.Combine(root, "fixtures", "conformance", "search");
        foreach (var path in Directory.EnumerateFiles(fixtureDirectory, "search-crossref-*.response.json"))
        {
            var bytes = File.ReadAllBytes(path);
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            var evidence = RecordedProviderFixtureEvidence.AcceptRetainedLocal(
                Path.GetFileNameWithoutExtension(path),
                relative,
                "fixture-generator",
                "fixture-generator",
                AcceptedAt,
                "Repository-retained FE-09A fixture.",
                CrossrefRecordedResponseAdapter.ParserId,
                CrossrefRecordedResponseAdapter.ParserVersion,
                bytes);

            Assert.AreEqual(relative, evidence.FixtureRelativePath);
            Assert.AreEqual(ContentDigest.Sha256(bytes), evidence.RawResponseDigest);
            evidence.Verify(bytes);
        }
    }

    private static (ProviderAcquisitionRequest Request, ProviderPageRequest Page) Request(bool includeRawData = false)
    {
        var request = ProviderAcquisitionRequest.Create(
            "request-1",
            "crossref",
            "artificial intelligence",
            new SearchYearRange(2022, 2023),
            "en",
            3,
            0,
            includeRawData,
            RequestedAt);
        return (request, ProviderPageRequest.Create(request, 0, 2, 0));
    }

    private static RecordedProviderFixtureEvidence Accept(
        string fixtureId,
        byte[] bytes,
        string fileName) =>
        RecordedProviderFixtureEvidence.AcceptRetainedLocal(
            fixtureId,
            $"fixtures/conformance/search/{fileName}",
            "fixture-generator",
            "fixture-generator",
            AcceptedAt,
            "Local FE-09A retained fixture; no provider parity claim.",
            CrossrefRecordedResponseAdapter.ParserId,
            CrossrefRecordedResponseAdapter.ParserVersion,
            bytes);

    private static byte[] Fixture(string fileName) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", fileName));

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NexusScholar.Core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
