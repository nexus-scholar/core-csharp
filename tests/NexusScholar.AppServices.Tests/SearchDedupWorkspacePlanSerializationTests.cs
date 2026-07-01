using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AppServices;
using NexusScholar.Deduplication;
using NexusScholar.Search;
using NexusScholar.Shared;
using NexusScholar.UiContracts;

namespace NexusScholar.AppServices.Tests;

[TestClass]
public sealed class SearchDedupWorkspacePlanSerializationTests
{
    [TestMethod]
    public void Compose_ProducesDeterministicJsonAcrossRepeatedRuns()
    {
        var composer = new SearchDedupWorkspacePlanComposer();
        var input = Input();

        var first = JsonSerializer.Serialize(composer.Compose(input), UiContractJson.SerializerOptions);
        var second = JsonSerializer.Serialize(composer.Compose(input), UiContractJson.SerializerOptions);

        Assert.AreEqual(first, second);
    }

    [TestMethod]
    public void Compose_RoundTripsThroughUiContractJsonOptions()
    {
        var plan = new SearchDedupWorkspacePlanComposer().Compose(Input());
        var json = JsonSerializer.Serialize(plan, UiContractJson.SerializerOptions);
        var roundTrip = JsonSerializer.Deserialize<WorkspacePlan>(json, UiContractJson.SerializerOptions);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(plan.WorkspaceId, roundTrip.WorkspaceId);
        Assert.AreEqual(plan.Blocks.Count, roundTrip.Blocks.Count);
        Assert.IsTrue(roundTrip.Blocks.All(block => block.PayloadJson is null || JsonDocument.Parse(block.PayloadJson).RootElement.ValueKind == JsonValueKind.Object));
    }

    [TestMethod]
    public void Compose_DoesNotEmitMachineLocalPaths()
    {
        var json = JsonSerializer.Serialize(new SearchDedupWorkspacePlanComposer().Compose(Input()), UiContractJson.SerializerOptions);

        Assert.IsFalse(json.Contains("C:\\", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("/Users/", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("/tmp/", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Compose_DoesNotEmitCurrentTimestamps()
    {
        var json = JsonSerializer.Serialize(new SearchDedupWorkspacePlanComposer().Compose(Input()), UiContractJson.SerializerOptions);

        Assert.IsFalse(json.Contains("2026-07-01", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains(DateTimeOffset.UtcNow.ToString("O"), StringComparison.Ordinal));
    }

    private static SearchDedupWorkspacePlanInput Input()
    {
        var record = new SearchImportRecord(
            "FixtureExport",
            "record-1",
            "doi:10.1000/serial",
            new[] { "doi:10.1000/serial" },
            ScholarlyWork.Identified(
                "Serializable app projection",
                WorkIdSet.From(WorkId.From("doi", "10.1000/serial")),
                "record-1"),
            new[] { "Researcher A" },
            2025,
            "Journal of Local Evidence",
            "Abstract omitted.",
            Array.Empty<string>(),
            "sha256:raw-record-1",
            "raw text omitted from projection",
            false,
            null,
            Array.Empty<SearchImportParserNotice>());
        var trace = new SearchImportTrace(
            "trace-serialization",
            "nexus.search.import.trace",
            "1.0.0",
            new SearchImportMetadata(
                SearchImportMetadata.AcquisitionKindImportedExport,
                "FixtureExport",
                "csv",
                "nexus.fixture.parser",
                "1.0.0",
                "sha256:source-digest",
                "raw-artifact-bytes",
                "tester",
                "2026-06-30T12:00:00Z",
                "evidence synthesis",
                "2026-06-30T11:55:00Z",
                1,
                Array.Empty<SearchImportParserNotice>()),
            new[] { record },
            Array.Empty<SearchSighting>(),
            Array.Empty<SearchImportParserNotice>(),
            SearchImportTrace.DefaultNonClaims);
        var result = new DeduplicationResult(
            "dedup-result-serialization",
            "nexus.deduplication.result",
            "1.0.0",
            "dedup-policy",
            "1.0.0",
            0.92,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Array.Empty<string>(),
            new[] { "trace-serialization" },
            Array.Empty<DedupCandidateRecord>(),
            Array.Empty<DedupCluster>(),
            Array.Empty<DedupEvidence>(),
            Array.Empty<DedupCandidateRecord>(),
            Array.Empty<DedupReviewCandidate>(),
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            new[] { "no-php-compatibility-claim", "no-live-provider-network" });

        return new SearchDedupWorkspacePlanInput("workspace-serialization", "Serializable plan", trace, result);
    }
}
