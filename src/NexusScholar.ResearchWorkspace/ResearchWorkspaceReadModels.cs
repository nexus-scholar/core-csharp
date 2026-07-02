using NexusScholar.UiContracts;

namespace NexusScholar.ResearchWorkspace;

public enum WorkspaceState
{
    Missing,
    Initialized,
    Imported,
    ImportedWithWarnings,
    Analyzed,
    ReviewReady,
    NeedsAttention
}

public sealed record WorkspaceOverviewReadModel(
    WorkspaceState State,
    string? ProjectTitle,
    string? WorkspaceId,
    string ProjectLocation,
    IReadOnlyList<string> NonClaims,
    VerificationHealthReadModel Verification,
    AnalysisSummaryReadModel Analysis,
    IReadOnlyList<WorkspaceAttentionItem> AttentionItems,
    IReadOnlyList<WorkflowStepReadModel> WorkflowSteps,
    IReadOnlyList<ImportSourceSummary> Imports,
    IReadOnlyList<EvidenceRecordRow> EvidenceRecords,
    IReadOnlyList<ReviewQueueItem> ReviewQueue,
    IReadOnlyList<DuplicateClusterSummary> DuplicateClusters,
    IReadOnlyList<DuplicateCandidateSummary> DuplicateCandidates,
    IReadOnlyList<DuplicateCandidateDetail> DuplicateCandidateDetails,
    IReadOnlyList<LockedDecisionAction> LockedDecisionActions)
{
    public bool HasAttention => AttentionItems.Count > 0;
}

public sealed record WorkspaceAttentionItem(
    string Code,
    BlockSeverity Severity,
    string Message,
    string? Target);

public sealed record WorkflowStepReadModel(
    string StepId,
    string Label,
    string State,
    string? NextCommand);

public sealed record EvidenceRecordRow(
    string Title,
    string Creators,
    int? Year,
    string? Venue,
    string Source,
    string? Identifier,
    int WarningCount,
    string DuplicateState,
    string ImportId,
    string SourceRecordId,
    string SourceTraceId,
    string? SourceFileDigest,
    string? RawRecordDigest,
    string? CandidateId,
    string? ClusterId);

public sealed record EvidenceRecordDetail(
    string SourceRecordId,
    string SourceTraceId,
    string Title,
    string? Abstract,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<EvidenceRefReadModel> EvidenceRefs);

public sealed record ImportSourceSummary(
    string ImportId,
    string Source,
    string Format,
    string? RelativePath,
    string? ImportTracePath,
    string? SourceFileDigest,
    int RecordCount,
    int ImportedRecordCount,
    int ParserWarningCount,
    int SkippedRecordCount);

public sealed record VerificationHealthReadModel(
    int InputCount,
    int FilesUnchanged,
    int MissingFileCount,
    int DigestMismatchCount,
    int InvalidPathCount,
    int MissingImportTraceCount,
    int ParserWarningCount,
    int SkippedRecordCount,
    bool IsValid);

public sealed record AnalysisSummaryReadModel(
    bool DeduplicationResultPresent,
    bool WorkspacePlanPresent,
    bool ReviewReportPresent,
    int ExactDuplicateClusterCount,
    int ReviewRequiredCandidateCount,
    int BlockingMergeGateCount);

public sealed record ReviewQueueItem(
    string CandidatePairId,
    string CandidateAId,
    string CandidateBId,
    string Title,
    double TitleSimilarity,
    double ThresholdUsed,
    IReadOnlyList<LockedDecisionAction> LockedActions);

public sealed record DuplicateClusterSummary(
    string ClusterId,
    string RepresentativeTitle,
    string? RepresentativePrimaryWorkId,
    int MemberCount,
    int EvidenceCount,
    bool ReviewRequired);

public sealed record DuplicateCandidateSummary(
    string CandidateId,
    string Title,
    string? PrimaryWorkId,
    string SourceTraceId,
    string SourceRecordId,
    string DuplicateState,
    string? ClusterId);

public sealed record DuplicateCandidateDetail(
    string CandidatePairId,
    DuplicateCandidateSummary CandidateA,
    DuplicateCandidateSummary CandidateB,
    double TitleSimilarity,
    double ThresholdUsed,
    IReadOnlyList<EvidenceRefReadModel> EvidenceRefs,
    IReadOnlyList<LockedDecisionAction> LockedActions);

public sealed record LockedDecisionAction(
    string ActionId,
    string Label,
    BlockActionKind Kind,
    string? TargetRef,
    string? CommandKind,
    bool IsExecutable,
    string LockReason);

public sealed record EvidenceRefReadModel(
    string Kind,
    string Value,
    string? Label,
    string? Digest,
    string? Scope);
