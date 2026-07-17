namespace NexusScholar.Desktop.AppServices;

public sealed record DesktopDeduplicationReviewRequest(
    string WorkspaceDirectory,
    string TargetId,
    string Action,
    string Reason,
    string? Rationale,
    string ActorId,
    string ActorRole,
    string? SupersedesDecisionId,
    DateTimeOffset OccurredAt);

public sealed record DesktopDeduplicationActiveDecision(
    string DecisionId,
    string DecisionDigest,
    string Action,
    string ActorId,
    string ActorRole);

public sealed record DesktopDeduplicationReviewTarget(
    string TargetId,
    string TargetDigest,
    IReadOnlyList<string> CandidateIds,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<DesktopDeduplicationActiveDecision> ActiveDecisions);

public sealed record DesktopDeduplicationReviewPolicy(
    string PolicyId,
    string PolicyVersion,
    string PolicyDigest,
    bool RequiresRationale,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ReasonCodesByAction,
    IReadOnlyList<string> AssignedActorRoles);

public sealed record DesktopDeduplicationReviewQueue(
    string WorkspaceId,
    long ProjectRevision,
    string AuthorityGenerationId,
    string AuthorityManifestDigest,
    DesktopDeduplicationReviewPolicy Policy,
    IReadOnlyList<DesktopDeduplicationReviewTarget> Targets);

public sealed record DesktopDeduplicationReviewQueueResult(
    DesktopWorkspaceCommandStatus Status,
    string Message,
    DesktopDeduplicationReviewQueue? Queue)
{
    public bool Completed => Status is DesktopWorkspaceCommandStatus.Succeeded or DesktopWorkspaceCommandStatus.Attention;
}

public sealed record DesktopDeduplicationReviewPreview(
    string WorkspaceDirectory,
    string WorkspaceId,
    long ExpectedProjectRevision,
    string AuthorityGenerationId,
    string AuthorityManifestDigest,
    string ActiveDecisionSetDigest,
    string SourceResultId,
    string SourceResultDigest,
    string SourceSnapshotId,
    string SourceSnapshotDigest,
    string TargetId,
    string TargetDigest,
    string PolicyId,
    string PolicyVersion,
    string PolicyDigest,
    string RequestId,
    string RequestDigest,
    string Action,
    string Reason,
    string? Rationale,
    string ActorId,
    string ActorRole,
    string? SupersedesDecisionId,
    string? SupersedesDecisionDigest,
    DateTimeOffset OccurredAt,
    IReadOnlyList<string> AffectedCandidateIds,
    bool MembershipChanges,
    string? RepresentativeCandidateId,
    IReadOnlyList<string> InvalidatedRecords,
    bool UnresolvedWorkRemains,
    IReadOnlyList<string> ExpectedEffects,
    IReadOnlyList<string> NonClaims,
    string ConfirmationToken);

public sealed record DesktopDeduplicationReviewPreviewResult(
    DesktopWorkspaceCommandStatus Status,
    string Message,
    DesktopDeduplicationReviewPreview? Preview)
{
    public bool IsReady => Status == DesktopWorkspaceCommandStatus.Ready && Preview is not null;
}

public sealed record DesktopDeduplicationReviewCommandResult(
    DesktopWorkspaceCommandStatus Status,
    string Message,
    string? DecisionId,
    string? SnapshotId,
    bool AlreadyApplied,
    DesktopWorkspaceOverview? Overview,
    DesktopDeduplicationReviewQueue? Queue)
{
    public bool Completed => Status is DesktopWorkspaceCommandStatus.Succeeded or DesktopWorkspaceCommandStatus.Attention;
}
