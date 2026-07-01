using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AppServices;
using NexusScholar.Deduplication;
using NexusScholar.Search;
using NexusScholar.Shared;
using NexusScholar.UiContracts;

namespace NexusScholar.AppServices.Tests;

[TestClass]
public sealed class SearchDedupWorkspacePlanFixtureTests
{
    [TestMethod]
    public void Compose_UsesStableBlockOrderingForCombinedImportAndDedupInput()
    {
        var warning = new SearchImportParserNotice(SearchImportErrorCodes.MissingRequiredField, "Title missing.", 1, "record-warning");
        var trace = Trace(warning);
        var result = Result();

        var plan = new SearchDedupWorkspacePlanComposer().Compose(
            new SearchDedupWorkspacePlanInput("workspace-combined", "Combined review", trace, result));

        CollectionAssert.AreEqual(
            new[]
            {
                KnownBlockKinds.ImportSummary,
                KnownBlockKinds.ImportWarningSummary,
                KnownBlockKinds.DedupCandidateCluster,
                KnownBlockKinds.DedupRecordComparison,
                KnownBlockKinds.HumanGateMergeDecision
            },
            plan.Blocks.Select(block => block.Kind).ToArray());
    }

    [TestMethod]
    public void Compose_PayloadJsonIsObjectRootedForEveryBlock()
    {
        var plan = new SearchDedupWorkspacePlanComposer().Compose(
            new SearchDedupWorkspacePlanInput("workspace-combined", "Combined review", Trace(), Result()));

        foreach (var block in plan.Blocks)
        {
            Assert.IsNotNull(block.PayloadJson);
            using var document = JsonDocument.Parse(block.PayloadJson);
            Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind);
        }
    }

    private static SearchImportTrace Trace(SearchImportParserNotice? warning = null)
    {
        var notices = warning is null ? Array.Empty<SearchImportParserNotice>() : new[] { warning };
        var record = new SearchImportRecord(
            "FixtureExport",
            "record-warning",
            null,
            Array.Empty<string>(),
            ScholarlyWork.UnresolvedCandidate("Potential duplicate article", "record-warning"),
            Array.Empty<string>(),
            null,
            null,
            null,
            Array.Empty<string>(),
            "sha256:raw-record-warning",
            "raw text omitted from projection",
            false,
            null,
            notices);

        return new SearchImportTrace(
            "trace-combined",
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
                warning is null ? Array.Empty<SearchImportParserNotice>() : new[] { warning }),
            new[] { record },
            Array.Empty<SearchSighting>(),
            warning is null ? Array.Empty<SearchImportParserNotice>() : new[] { warning },
            SearchImportTrace.DefaultNonClaims);
    }

    private static DeduplicationResult Result()
    {
        var candidateA = Candidate("candidate-a", "Potential duplicate article", "record-a");
        var candidateB = Candidate("candidate-b", "Potential duplicate article revised", "record-b");
        var exactA = Candidate("candidate-c", "Exact duplicate article", "record-c", "doi:10.1000/exact");
        var exactB = Candidate("candidate-d", "Exact duplicate article", "record-d", "doi:10.1000/exact");
        var clusterEvidence = new DedupEvidence(
            "evidence-exact",
            DedupEvidenceKind.ExactIdentifier,
            exactA.CandidateId,
            exactB.CandidateId,
            "Stable identifier overlap.");
        var cluster = new DedupCluster(
            "cluster-exact",
            new[] { exactA, exactB },
            new DedupRepresentativeResult(
                exactA.CandidateId,
                exactA.Title,
                exactA.PrimaryWorkId,
                exactA.WorkIds,
                new[] { exactA.Source.SourceSightingId, exactB.Source.SourceSightingId },
                1.0,
                new[] { "stable-identifier-overlap" }),
            new[] { clusterEvidence });
        var reviewEvidence = new DedupEvidence(
            "evidence-review",
            DedupEvidenceKind.FuzzyTitle,
            candidateA.CandidateId,
            candidateB.CandidateId,
            "Title similarity requires review.",
            ReviewRequired: true,
            Score: 0.94,
            PolicyId: "dedup-policy",
            PolicyVersion: "1.0.0");

        return new DeduplicationResult(
            "dedup-result-combined",
            "nexus.deduplication.result",
            "1.0.0",
            "dedup-policy",
            "1.0.0",
            0.92,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Array.Empty<string>(),
            new[] { "trace-combined" },
            new[] { candidateA, candidateB, exactA, exactB },
            new[] { cluster },
            new[] { clusterEvidence, reviewEvidence },
            Array.Empty<DedupCandidateRecord>(),
            new[] { new DedupReviewCandidate(candidateA.CandidateId, candidateB.CandidateId, 0.94, 0.92) },
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            new[] { "no-live-provider-network" });
    }

    private static DedupCandidateRecord Candidate(string id, string title, string sourceRecordId, string? primaryWorkId = null)
    {
        var workIds = primaryWorkId is null ? Array.Empty<string>() : new[] { primaryWorkId };
        return new DedupCandidateRecord(
            id,
            title,
            primaryWorkId is not null,
            primaryWorkId,
            workIds,
            new[] { sourceRecordId },
            new DedupSightingRef(
                "import",
                "trace-combined",
                $"sighting-{sourceRecordId}",
                SourceDatabaseOrTool: "FixtureExport",
                SourceRecordId: sourceRecordId,
                SourceFileDigest: "sha256:source-digest",
                SourceFileDigestScope: "raw-artifact-bytes",
                RawRecordDigest: $"sha256:raw-{sourceRecordId}"));
    }
}
