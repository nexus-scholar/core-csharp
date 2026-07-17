using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Desktop.AppServices;

public sealed partial class DesktopWorkspaceCommandFacade
{
    private static readonly string[] DeduplicationReviewNonClaims =
    {
        "human-actor-required",
        "policy-assignment-is-authority",
        "not-authentication",
        "not-screening",
        "no-ai",
        "no-network"
    };

    public DesktopDeduplicationReviewQueueResult LoadDeduplicationReviewQueue(string workspaceDirectory)
    {
        var result = ResearchWorkspaceDeduplicationReview.Inspect(workspaceDirectory);
        if (!result.Completed || result.Policy is null || result.WorkspaceId is null ||
            result.ProjectRevision is null || result.AuthorityGenerationId is null || result.AuthorityManifestDigest is null)
        {
            return new DesktopDeduplicationReviewQueueResult(Map(result.Status), result.Message, null);
        }

        var queue = Queue(result);
        var status = queue.Targets.Count == 0
            ? DesktopWorkspaceCommandStatus.Succeeded
            : DesktopWorkspaceCommandStatus.Attention;
        return new DesktopDeduplicationReviewQueueResult(status, result.Message, queue);
    }

    public DesktopDeduplicationReviewPreviewResult PreviewDeduplicationReview(
        DesktopDeduplicationReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                request.WorkspaceDirectory,
                request.TargetId,
                request.Action,
                request.Reason,
                request.Rationale,
                request.ActorId,
                request.ActorRole,
                request.SupersedesDecisionId,
                request.OccurredAt));
        if (!result.IsReady)
        {
            return new DesktopDeduplicationReviewPreviewResult(Map(result.Status), result.Message, null);
        }

        var effects = Effects(result);
        var preview = new DesktopDeduplicationReviewPreview(
            result.WorkspaceDirectory,
            result.WorkspaceId!,
            result.ExpectedProjectRevision!.Value,
            result.AuthorityGenerationId!,
            result.AuthorityManifestDigest!,
            result.ActiveDecisionSetDigest!,
            result.SourceResultId!,
            result.SourceResultDigest!,
            result.SourceSnapshotId!,
            result.SourceSnapshotDigest!,
            result.TargetId!,
            result.TargetDigest!,
            result.PolicyId!,
            result.PolicyVersion!,
            result.PolicyDigest!,
            result.RequestId!,
            result.RequestDigest!,
            result.Action!,
            result.Reason!,
            result.Rationale,
            result.ActorId!,
            result.ActorRole!,
            result.SupersedesDecisionId,
            result.SupersedesDecisionDigest,
            result.OccurredAt,
            result.AffectedCandidateIds,
            result.MembershipChanges,
            result.RepresentativeCandidateId,
            result.InvalidatedRecords,
            result.UnresolvedWorkRemains,
            effects,
            DeduplicationReviewNonClaims,
            ConfirmationToken(result, effects, DeduplicationReviewNonClaims));
        return new DesktopDeduplicationReviewPreviewResult(
            DesktopWorkspaceCommandStatus.Ready,
            result.Message,
            preview);
    }

    public DesktopDeduplicationReviewCommandResult ExecuteDeduplicationReview(
        DesktopDeduplicationReviewPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var operationPreview = OperationPreview(preview);
        var expectedToken = ConfirmationToken(
            operationPreview,
            preview.ExpectedEffects,
            preview.NonClaims);
        if (!string.Equals(preview.ConfirmationToken, expectedToken, StringComparison.Ordinal))
        {
            return ReviewFailure(
                DesktopWorkspaceCommandStatus.Stale,
                "stale-confirmation-preview: deduplication review preview material or token changed.");
        }

        var committed = ResearchWorkspaceDeduplicationReview.Commit(operationPreview);
        if (!committed.Completed)
        {
            return ReviewFailure(Map(committed.Status), committed.Message);
        }

        var refreshed = LoadDeduplicationReviewQueue(preview.WorkspaceDirectory);
        var overview = SafeBuild(preview.WorkspaceDirectory);
        return new DesktopDeduplicationReviewCommandResult(
            DesktopWorkspaceCommandStatus.Succeeded,
            committed.Message,
            committed.DecisionId,
            committed.SnapshotId,
            committed.AlreadyApplied,
            overview,
            refreshed.Queue);
    }

    private static ResearchWorkspaceDeduplicationReviewPreview OperationPreview(
        DesktopDeduplicationReviewPreview value) => new(
            ResearchWorkspaceOperationStatus.Succeeded,
            ResearchWorkspaceExitCodes.Success,
            "Review the exact deduplication authority effects before confirmation.",
            value.WorkspaceDirectory,
            value.WorkspaceId,
            value.ExpectedProjectRevision,
            value.AuthorityGenerationId,
            value.AuthorityManifestDigest,
            value.ActiveDecisionSetDigest,
            value.SourceResultId,
            value.SourceResultDigest,
            value.SourceSnapshotId,
            value.SourceSnapshotDigest,
            value.TargetId,
            value.TargetDigest,
            value.PolicyId,
            value.PolicyVersion,
            value.PolicyDigest,
            value.RequestId,
            value.RequestDigest,
            value.Action,
            value.Reason,
            value.Rationale,
            value.ActorId,
            value.ActorRole,
            value.SupersedesDecisionId,
            value.SupersedesDecisionDigest,
            value.OccurredAt,
            value.AffectedCandidateIds,
            value.MembershipChanges,
            value.RepresentativeCandidateId,
            value.InvalidatedRecords,
            value.UnresolvedWorkRemains);

    private static string ConfirmationToken(
        ResearchWorkspaceDeduplicationReviewPreview value,
        IReadOnlyList<string> effects,
        IReadOnlyList<string> nonClaims)
    {
        CanonicalJsonValue Optional(string? text) => text is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(text);
        var material = new CanonicalJsonObject()
            .Add("schema", "nexus.desktop.deduplication-review-preview")
            .Add("schema_version", "1.0.0")
            .Add("workspace_directory", value.WorkspaceDirectory)
            .Add("workspace_id", value.WorkspaceId!)
            .Add("expected_project_revision", value.ExpectedProjectRevision!.Value)
            .Add("authority_generation_id", value.AuthorityGenerationId!)
            .Add("authority_manifest_digest", value.AuthorityManifestDigest!)
            .Add("active_decision_set_digest", value.ActiveDecisionSetDigest!)
            .Add("source_result_id", value.SourceResultId!)
            .Add("source_result_digest", value.SourceResultDigest!)
            .Add("source_snapshot_id", value.SourceSnapshotId!)
            .Add("source_snapshot_digest", value.SourceSnapshotDigest!)
            .Add("target_id", value.TargetId!)
            .Add("target_digest", value.TargetDigest!)
            .Add("policy_id", value.PolicyId!)
            .Add("policy_version", value.PolicyVersion!)
            .Add("policy_digest", value.PolicyDigest!)
            .Add("request_id", value.RequestId!)
            .Add("request_digest", value.RequestDigest!)
            .Add("action", value.Action!)
            .Add("reason", value.Reason!)
            .Add("rationale", Optional(value.Rationale))
            .Add("actor_id", value.ActorId!)
            .Add("actor_role", value.ActorRole!)
            .Add("supersedes_decision_id", Optional(value.SupersedesDecisionId))
            .Add("supersedes_decision_digest", Optional(value.SupersedesDecisionDigest))
            .Add("occurred_at", value.OccurredAt.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture))
            .Add("affected_candidate_ids", new CanonicalJsonArray(value.AffectedCandidateIds.Select(CanonicalJsonValue.From)))
            .Add("membership_changes", value.MembershipChanges)
            .Add("representative_candidate_id", Optional(value.RepresentativeCandidateId))
            .Add("invalidated_records", new CanonicalJsonArray(value.InvalidatedRecords.Select(CanonicalJsonValue.From)))
            .Add("unresolved_work_remains", value.UnresolvedWorkRemains)
            .Add("expected_effects", new CanonicalJsonArray(effects.Select(CanonicalJsonValue.From)))
            .Add("non_claims", new CanonicalJsonArray(nonClaims.Select(CanonicalJsonValue.From)));
        return ContentDigest.Sha256CanonicalJson(material).ToString();
    }

    private static IReadOnlyList<string> Effects(ResearchWorkspaceDeduplicationReviewPreview value)
    {
        var effects = new List<string>
        {
            $"record {value.Action} for {string.Join(", ", value.AffectedCandidateIds)}",
            "append one verified deduplication authority decision",
            "publish one successor corpus snapshot",
            "advance project and authority generation"
        };
        effects.AddRange(value.InvalidatedRecords.Select(item => $"invalidate {item}"));
        if (value.SupersedesDecisionId is not null)
        {
            effects.Add($"supersede decision {value.SupersedesDecisionId}");
        }
        return effects;
    }

    private static DesktopDeduplicationReviewQueue Queue(ResearchWorkspaceDeduplicationReviewQueue value) => new(
        value.WorkspaceId!,
        value.ProjectRevision!.Value,
        value.AuthorityGenerationId!,
        value.AuthorityManifestDigest!,
        new DesktopDeduplicationReviewPolicy(
            value.Policy!.PolicyId,
            value.Policy.PolicyVersion,
            value.Policy.PolicyDigest,
            value.Policy.RequiresRationale,
            value.Policy.AllowedActions,
            value.Policy.ReasonCodesByAction,
            value.Policy.AssignedActorRoles),
        value.Targets.Select(target => new DesktopDeduplicationReviewTarget(
            target.TargetId,
            target.TargetDigest,
            target.CandidateIds,
            target.EvidenceIds,
            target.ActiveDecisions.Select(decision => new DesktopDeduplicationActiveDecision(
                decision.DecisionId,
                decision.DecisionDigest,
                decision.Action,
                decision.ActorId,
                decision.ActorRole)).ToArray())).ToArray());

    private static DesktopWorkspaceCommandStatus Map(ResearchWorkspaceOperationStatus status) => status switch
    {
        ResearchWorkspaceOperationStatus.Succeeded => DesktopWorkspaceCommandStatus.Succeeded,
        ResearchWorkspaceOperationStatus.Stale => DesktopWorkspaceCommandStatus.Stale,
        ResearchWorkspaceOperationStatus.RecoveryRequired => DesktopWorkspaceCommandStatus.RecoveryRequired,
        _ => DesktopWorkspaceCommandStatus.Failed
    };

    private static DesktopDeduplicationReviewCommandResult ReviewFailure(
        DesktopWorkspaceCommandStatus status,
        string message) => new(status, message, null, null, false, null, null);
}
