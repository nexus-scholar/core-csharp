using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.AppServices;

public sealed record DeduplicationReviewPreviewRequest(
    VerifiedDeduplicationReviewCommand Command,
    VerifiedDeduplicationAuthorityReviewTargetDigest Target,
    VerifiedDeduplicationAuthorityResultDigest SourceResult,
    VerifiedDeduplicationAuthorityPolicy Policy,
    VerifiedCorpusSnapshot CurrentSnapshot,
    IReadOnlyList<VerifiedDeduplicationAuthorityDecision> ActiveDecisions,
    IReadOnlyList<VerifiedDeduplicationAuthorityDecision> KnownDecisions,
    IReadOnlyList<VerifiedCorpusSnapshot> KnownSnapshots);

public sealed record DeduplicationReviewPreview(
    string RequestId,
    ContentDigest RequestDigest,
    string Action,
    IReadOnlyList<string> AffectedCandidateIds,
    bool MembershipChanges,
    string? RepresentativeCandidateId,
    IReadOnlyList<CorpusSnapshotInvalidationReference> RecordsToInvalidate,
    bool UnresolvedWorkRemains);

public sealed record DeduplicationReviewExecutionResult(
    string RequestId,
    string DecisionId,
    string SnapshotId,
    bool AlreadyApplied);

public static class DeduplicationReviewApplicationService
{
    public static DeduplicationReviewPreview Preview(DeduplicationReviewPreviewRequest request, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(clock);
        var decision = DeduplicationDecision.CreateDecisionMaterial(
            DeduplicationReviewCommand.BuildDecisionMaterial(request.Command, request.Target),
            clock, request.Policy, request.SourceResult, request.Target);
        var active = request.ActiveDecisions
            .Where(item => request.Command.Material.SupersedesDecisionId is null ||
                !string.Equals(item.DecisionId, request.Command.Material.SupersedesDecisionId, StringComparison.Ordinal))
            .Append(decision)
            .ToArray();
        var known = request.KnownDecisions.Append(decision).ToArray();
        var reduction = DeduplicationSnapshotReducer.Reduce(
            request.SourceResult, request.CurrentSnapshot, decision, active, known);
        var affected = request.Target.CandidateIds.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var representative = reduction.Groups
            .SingleOrDefault(group => affected.All(id => group.MemberCandidateIds.Contains(id, StringComparer.Ordinal)))
            ?.RepresentativeCandidateId;
        var invalidations = decision.InvalidationEffects.Select(effect => new CorpusSnapshotInvalidationReference(
            effect.RecordKind, effect.RecordId, effect.RecordDigest)).ToArray();
        return new DeduplicationReviewPreview(
            request.Command.RequestId,
            request.Command.RequestDigest,
            request.Command.Material.ActionType,
            affected,
            string.Equals(request.Command.Material.ActionType, DeduplicationAuthorityPolicyConstants.MergeAction, StringComparison.Ordinal),
            representative,
            invalidations,
            reduction.UnresolvedCandidates.Count > 0 ||
                active.Any(item => string.Equals(item.ActionType, DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, StringComparison.Ordinal)));
    }
}
