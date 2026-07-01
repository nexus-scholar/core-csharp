using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AppServices;
using NexusScholar.Deduplication;
using NexusScholar.Search;
using NexusScholar.Shared;
using NexusScholar.UiContracts;

namespace NexusScholar.AppServices.Tests;

[TestClass]
public sealed class SearchDedupWorkspacePlanComposerTests
{
    [TestMethod]
    public void Compose_UsesAppProjectionForEveryBlock()
    {
        var plan = Composer().Compose(Input(WarningTrace(), ReviewRequiredResult()));

        Assert.IsTrue(plan.Blocks.Count > 0);
        Assert.IsTrue(plan.Blocks.All(block => block.SourceKind == BlockSourceKind.AppProjection));
    }

    [TestMethod]
    public void Compose_ProducesImportSummaryForCleanImport()
    {
        var plan = Composer().Compose(Input(CleanTrace(), EmptyDedupResult()));

        var block = SingleBlock(plan, KnownBlockKinds.ImportSummary);
        Assert.AreEqual("block.import.summary", block.BlockId);
        Assert.AreEqual(BlockSeverity.Success, block.Severity);
        StringAssert.Contains(block.PayloadJson!, "\"record_count\":1");
        StringAssert.Contains(block.PayloadJson!, "\"projection_authority\":\"app-projection-only\"");
    }

    [TestMethod]
    public void Compose_UsesReviewModeWhenImportWarningsExist()
    {
        var plan = Composer().Compose(Input(WarningTrace(), EmptyDedupResult()));

        Assert.AreEqual(BlockMode.Review, plan.Mode);
        Assert.IsTrue(plan.Blocks.All(block => block.Mode == BlockMode.Review));
    }

    [TestMethod]
    public void Compose_UsesAuditModeWhenNoWarningsOrReviewCandidatesExist()
    {
        var plan = Composer().Compose(Input(CleanTrace(), EmptyDedupResult()));

        Assert.AreEqual(BlockMode.Audit, plan.Mode);
        Assert.IsTrue(plan.Blocks.All(block => block.Mode == BlockMode.Audit));
    }

    [TestMethod]
    public void Compose_EmitsWarningSummaryBlocksByStableCategory()
    {
        var plan = Composer().Compose(Input(WarningTrace(), EmptyDedupResult()));

        var categories = plan.Blocks
            .Where(block => block.Kind == KnownBlockKinds.ImportWarningSummary)
            .Select(block => JsonDocument.Parse(block.PayloadJson!).RootElement.GetProperty("category").GetString())
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                SearchImportErrorCodes.MissingRequiredField,
                SearchImportErrorCodes.ParserWarning,
                SearchImportErrorCodes.SkippedRecord
            },
            categories);
    }

    [TestMethod]
    public void Compose_EmitsExactDuplicateClustersInStableOrder()
    {
        var plan = Composer().Compose(Input(CleanTrace(), ExactClusterResult()));

        var ids = plan.Blocks
            .Where(block => block.Kind == KnownBlockKinds.DedupCandidateCluster)
            .Select(block => JsonDocument.Parse(block.PayloadJson!).RootElement.GetProperty("cluster_id").GetString())
            .ToArray();

        CollectionAssert.AreEqual(new[] { "cluster-a", "cluster-b" }, ids);
    }

    [TestMethod]
    public void Compose_EmitsReviewRequiredRecordComparisonBlocks()
    {
        var plan = Composer().Compose(Input(CleanTrace(), ReviewRequiredResult()));

        var comparison = SingleBlock(plan, KnownBlockKinds.DedupRecordComparison);

        Assert.AreEqual(BlockSeverity.ReviewRequired, comparison.Severity);
        StringAssert.Contains(comparison.PayloadJson!, "\"candidate_a_id\":\"candidate-a\"");
        StringAssert.Contains(comparison.PayloadJson!, "\"candidate_b_id\":\"candidate-b\"");
        StringAssert.Contains(comparison.PayloadJson!, "\"threshold_used\":0.92");
    }

    [TestMethod]
    public void Compose_EmitsHumanMergeGateForReviewRequiredCandidates()
    {
        var plan = Composer().Compose(Input(CleanTrace(), ReviewRequiredResult()));

        var gate = SingleBlock(plan, KnownBlockKinds.HumanGateMergeDecision);

        Assert.AreEqual(BlockSeverity.Blocking, gate.Severity);
        CollectionAssert.AreEquivalent(
            new[] { BlockActionKind.AcceptMerge, BlockActionKind.RejectMerge, BlockActionKind.MarkUnresolved },
            gate.Actions.Select(action => action.Kind).ToArray());
        Assert.IsTrue(gate.Actions.All(action => action.RequiresHumanConfirmation));
    }

    [TestMethod]
    public void Compose_DoesNotEmitSampleSourceKind()
    {
        var plan = Composer().Compose(Input(CleanTrace(), ReviewRequiredResult()));
        var json = JsonSerializer.Serialize(plan, UiContractJson.SerializerOptions);

        Assert.IsTrue(plan.Blocks.All(block => block.SourceKind != BlockSourceKind.Sample));
        Assert.IsFalse(json.Contains("\"Sample\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Compose_PlaceholderActionsDoNotExecuteCommands()
    {
        var plan = Composer().Compose(Input(CleanTrace(), ReviewRequiredResult()));
        var gate = SingleBlock(plan, KnownBlockKinds.HumanGateMergeDecision);

        Assert.IsTrue(gate.Actions.All(action => action.CommandKind is null));
    }

    private static SearchDedupWorkspacePlanComposer Composer() => new();

    private static SearchDedupWorkspacePlanInput Input(SearchImportTrace trace, DeduplicationResult result) =>
        new("workspace-import-dedup", "Import and dedup review", trace, result);

    private static ResearchBlockDescriptor SingleBlock(WorkspacePlan plan, string kind)
    {
        var blocks = plan.Blocks.Where(block => block.Kind == kind).ToArray();
        Assert.AreEqual(1, blocks.Length);
        return blocks[0];
    }

    private static SearchImportTrace CleanTrace() =>
        Trace(
            "trace-clean",
            new[]
            {
                ImportRecord("record-1", "Audit-grade evidence synthesis", WorkIdSet.From(WorkId.From("doi", "10.1000/clean")))
            });

    private static SearchImportTrace WarningTrace()
    {
        var parserWarning = new SearchImportParserNotice(SearchImportErrorCodes.ParserWarning, "Unknown optional field ignored.", 2, "record-2");
        var missingField = new SearchImportParserNotice(SearchImportErrorCodes.MissingRequiredField, "Record is missing a title.", 1, "record-1");
        return Trace(
            "trace-warning",
            new[]
            {
                ImportRecord(
                    "record-1",
                    "Incomplete imported record",
                    WorkIdSet.Empty,
                    new[] { missingField }),
                SearchImportRecord.Skipped(
                    "FixtureExport",
                    "record-2",
                    "Required fields were absent.",
                    new[] { parserWarning })
            },
            new[] { parserWarning });
    }

    private static SearchImportTrace Trace(
        string traceId,
        IReadOnlyList<SearchImportRecord> records,
        IReadOnlyList<SearchImportParserNotice>? parserWarnings = null)
    {
        parserWarnings ??= Array.Empty<SearchImportParserNotice>();
        var metadata = new SearchImportMetadata(
            SearchImportMetadata.AcquisitionKindImportedExport,
            "FixtureExport",
            "csv",
            "nexus.fixture.parser",
            "1.0.0",
            "sha256:source-digest",
            "raw-artifact-bytes",
            "tester",
            "2026-06-30T12:00:00Z",
            "audit evidence",
            "2026-06-30T11:55:00Z",
            records.Count,
            parserWarnings);

        return new SearchImportTrace(
            traceId,
            "nexus.search.import.trace",
            "1.0.0",
            metadata,
            records,
            Array.Empty<SearchSighting>(),
            parserWarnings,
            SearchImportTrace.DefaultNonClaims);
    }

    private static SearchImportRecord ImportRecord(
        string sourceRecordId,
        string title,
        WorkIdSet workIds,
        IReadOnlyList<SearchImportParserNotice>? notices = null)
    {
        var work = workIds.Ids.Count == 0
            ? ScholarlyWork.UnresolvedCandidate(title, sourceRecordId)
            : ScholarlyWork.Identified(title, workIds, sourceRecordId);

        return new SearchImportRecord(
            "FixtureExport",
            sourceRecordId,
            work.PrimaryWorkId?.ToString(),
            work.WorkIds.Ids.Select(id => id.ToString()).ToArray(),
            work,
            new[] { "Researcher A" },
            2025,
            "Journal of Local Evidence",
            "Abstract omitted.",
            Array.Empty<string>(),
            $"sha256:raw-{sourceRecordId}",
            "raw text is intentionally not projected",
            false,
            null,
            notices ?? Array.Empty<SearchImportParserNotice>());
    }

    private static DeduplicationResult EmptyDedupResult() =>
        DedupResult(
            Array.Empty<DedupCandidateRecord>(),
            Array.Empty<DedupCluster>(),
            Array.Empty<DedupEvidence>(),
            Array.Empty<DedupReviewCandidate>());

    private static DeduplicationResult ExactClusterResult()
    {
        var candidateA = Candidate("candidate-a", "Duplicate evidence", "doi:10.1000/a", "record-a");
        var candidateB = Candidate("candidate-b", "Duplicate evidence", "doi:10.1000/a", "record-b");
        var candidateC = Candidate("candidate-c", "Second duplicate", "doi:10.1000/c", "record-c");
        var candidateD = Candidate("candidate-d", "Second duplicate", "doi:10.1000/c", "record-d");

        var clusterB = Cluster("cluster-b", candidateC, candidateD, "evidence-b");
        var clusterA = Cluster("cluster-a", candidateA, candidateB, "evidence-a");

        return DedupResult(
            new[] { candidateA, candidateB, candidateC, candidateD },
            new[] { clusterB, clusterA },
            clusterA.Evidence.Concat(clusterB.Evidence).ToArray(),
            Array.Empty<DedupReviewCandidate>());
    }

    private static DeduplicationResult ReviewRequiredResult()
    {
        var candidateA = Candidate("candidate-a", "Local first evidence audit", null, "record-a");
        var candidateB = Candidate("candidate-b", "Local-first evidence auditing", null, "record-b");
        var review = new DedupReviewCandidate("candidate-a", "candidate-b", 0.95, 0.92);
        var evidence = new DedupEvidence(
            "evidence-review",
            DedupEvidenceKind.FuzzyTitle,
            "candidate-a",
            "candidate-b",
            "Title similarity requires review.",
            ReviewRequired: true,
            Score: 0.95,
            PolicyId: "policy",
            PolicyVersion: "1.0.0");

        return DedupResult(
            new[] { candidateA, candidateB },
            Array.Empty<DedupCluster>(),
            new[] { evidence },
            new[] { review });
    }

    private static DedupCluster Cluster(string id, DedupCandidateRecord first, DedupCandidateRecord second, string evidenceId)
    {
        var evidence = new DedupEvidence(
            evidenceId,
            DedupEvidenceKind.ExactIdentifier,
            first.CandidateId,
            second.CandidateId,
            "Stable identifier overlap.");
        var representative = new DedupRepresentativeResult(
            first.CandidateId,
            first.Title,
            first.PrimaryWorkId,
            first.WorkIds,
            new[] { first.Source.SourceSightingId, second.Source.SourceSightingId },
            1.0,
            new[] { "stable-identifier-overlap" });

        return new DedupCluster(id, new[] { first, second }, representative, new[] { evidence });
    }

    private static DedupCandidateRecord Candidate(string id, string title, string? primaryWorkId, string sourceRecordId)
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
                "trace-clean",
                $"sighting-{sourceRecordId}",
                SourceDatabaseOrTool: "FixtureExport",
                SourceRecordId: sourceRecordId,
                SourceFileDigest: "sha256:source-digest",
                SourceFileDigestScope: "raw-artifact-bytes",
                RawRecordDigest: $"sha256:raw-{sourceRecordId}"));
    }

    private static DeduplicationResult DedupResult(
        IReadOnlyList<DedupCandidateRecord> rawCandidates,
        IReadOnlyList<DedupCluster> clusters,
        IReadOnlyList<DedupEvidence> evidence,
        IReadOnlyList<DedupReviewCandidate> reviewCandidates) =>
        new(
            "dedup-result-1",
            "nexus.deduplication.result",
            "1.0.0",
            "dedup-policy",
            "1.0.0",
            0.92,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Array.Empty<string>(),
            new[] { "trace-clean" },
            rawCandidates,
            clusters,
            evidence,
            Array.Empty<DedupCandidateRecord>(),
            reviewCandidates,
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            new[] { "no-php-compatibility-claim", "no-live-provider-network" });
}
