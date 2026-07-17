using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Search.Providers.Live;
using NexusScholar.Search.Providers.OpenAlex;
using NexusScholar.Search.Providers.SemanticScholar;

namespace NexusScholar.Search.Providers.LiveSmoke.Tests;

[TestClass]
[TestCategory("LiveProvider")]
public sealed class LiveProviderSmokeTests
{
    [TestMethod]
    public async Task OpenAlex_bounded_page_matches_the_runtime_contract()
    {
        RequireLiveExecution();
        var credentials = CompositeProviderCredentialResolver.CreateDefault();
        RequireCredential(credentials, OpenAlexRecordedResponseAdapter.ProviderAlias);
        var acquisition = ProviderAcquisitionRequest.Create(
            $"live-openalex-{Guid.NewGuid():N}",
            OpenAlexRecordedResponseAdapter.ProviderAlias,
            "tomato disease",
            null,
            null,
            1,
            0,
            false,
            DateTimeOffset.UtcNow);
        var page = ProviderPageRequest.Create(acquisition, 0, 1, 0, "*");
        var request = OpenAlexRecordedResponseAdapter.DescribeLiveRequest(acquisition, page);

        using var host = new ProviderLiveHost(credentials, new SystemClock());
        using var response = await host.ExecuteAsync(request);
        var body = response.CopyBody();
        var evidence = OpenAlexRecordedResponseAdapter.CaptureResponse(response);
        var result = new OpenAlexRecordedResponseAdapter().ParseRecordedResponse(
            acquisition,
            page,
            body,
            evidence);

        Assert.AreEqual(200, response.StatusCode);
        Assert.IsTrue(result.Sightings.Count <= 1);
        Console.WriteLine(
            $"provider=openalex status={response.StatusCode} count={result.Sightings.Count} " +
            $"digest={evidence.RawResponseDigest} authority=none compatibility_claim=none");
    }

    [TestMethod]
    public async Task SemanticScholar_bulk_and_batch_return_parseable_bounded_shapes()
    {
        RequireLiveExecution();
        var credentials = CompositeProviderCredentialResolver.CreateDefault();
        RequireCredential(credentials, SemanticScholarRecordedResponseAdapter.ProviderAlias);
        var acquisition = ProviderAcquisitionRequest.Create(
            $"live-s2-bulk-{Guid.NewGuid():N}",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "tomato disease",
            null,
            null,
            1000,
            0,
            false,
            DateTimeOffset.UtcNow);
        var page = ProviderPageRequest.Create(acquisition, 0, 1000, 0);
        var request = SemanticScholarRecordedResponseAdapter.DescribeLiveRequest(acquisition, page);

        using var host = new ProviderLiveHost(credentials, new SystemClock());
        using var bulkResponse = await host.ExecuteAsync(request);
        var bulkBody = bulkResponse.CopyBody();
        var bulkEvidence = SemanticScholarRecordedResponseAdapter.CaptureResponse(bulkResponse);
        var bulkResult = new SemanticScholarRecordedResponseAdapter().ParseRecordedResponse(
            acquisition,
            page,
            bulkBody,
            bulkEvidence);

        Assert.AreEqual(200, bulkResponse.StatusCode);
        Assert.IsTrue(bulkResult.Sightings.Count > 0);
        var s2Id = bulkResult.Sightings
            .SelectMany(sighting => sighting.Work.WorkIds.Ids)
            .Select(identifier => identifier.ToString())
            .First(identifier => identifier.StartsWith("s2:", StringComparison.Ordinal))[3..];

        var batchIds = new[] { s2Id };
        var batchRequest = SemanticScholarRecordedResponseAdapter.DescribePaperBatchRequest(batchIds);
        using var batchResponse = await host.ExecuteAsync(batchRequest);
        var batchBody = batchResponse.CopyBody();
        var batchEvidence = SemanticScholarRecordedResponseAdapter.CaptureResponse(batchResponse);
        var batchAcquisition = ProviderAcquisitionRequest.Create(
            $"live-s2-batch-{Guid.NewGuid():N}",
            SemanticScholarRecordedResponseAdapter.ProviderAlias,
            "batch",
            null,
            null,
            1,
            0,
            false,
            DateTimeOffset.UtcNow);
        var batchPage = ProviderPageRequest.Create(batchAcquisition, 0, 1, 0);
        var batchResult = new SemanticScholarRecordedResponseAdapter().ParseRecordedBatchResponse(
            batchAcquisition,
            batchPage,
            batchIds,
            batchBody,
            batchEvidence);

        Assert.AreEqual(200, batchResponse.StatusCode);
        Assert.AreEqual(1, batchResult.Sightings.Count);
        Assert.IsFalse(batchResult.Sightings[0].Work.IsUnresolvedCandidate);
        Console.WriteLine(
            $"provider=semantic_scholar bulk_status={bulkResponse.StatusCode} bulk_count={bulkResult.Sightings.Count} " +
            $"bulk_digest={bulkEvidence.RawResponseDigest} batch_status={batchResponse.StatusCode} " +
            $"batch_count={batchResult.Sightings.Count} batch_digest={batchEvidence.RawResponseDigest} " +
            "authority=none compatibility_claim=none");
    }

    private static void RequireLiveExecution()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI")))
        {
            Assert.Inconclusive("Live provider tests are disabled in CI.");
        }

        if (!string.Equals(
                Environment.GetEnvironmentVariable("RUN_LIVE_PROVIDER_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set RUN_LIVE_PROVIDER_TESTS=1 to enable live provider tests.");
        }
    }

    private static void RequireCredential(IProviderCredentialResolver credentials, string providerAlias)
    {
        if (string.IsNullOrWhiteSpace(credentials.Resolve(providerAlias)))
        {
            Assert.Inconclusive($"No secure credential is configured for provider '{providerAlias}'.");
        }
    }
}
