using System.Text.Json;
using NexusScholar.AppServices;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.ResearchWorkspace;

public sealed record ResearchWorkspaceDeduplicationReviewRequest(
    string WorkingDirectory,
    string TargetId,
    string Action,
    string Reason,
    string? Rationale,
    string ActorId,
    string ActorRole,
    string? SupersedesDecisionId,
    DateTimeOffset OccurredAt);

public sealed record ResearchWorkspaceDeduplicationReviewTarget(
    string TargetId,
    string TargetDigest,
    IReadOnlyList<string> CandidateIds,
    IReadOnlyList<string> EvidenceIds,
    IReadOnlyList<ResearchWorkspaceActiveDeduplicationDecision> ActiveDecisions);

public sealed record ResearchWorkspaceActiveDeduplicationDecision(
    string DecisionId,
    string DecisionDigest,
    string Action,
    string ActorId,
    string ActorRole);

public sealed record ResearchWorkspaceDeduplicationReviewPolicy(
    string PolicyId,
    string PolicyVersion,
    string PolicyDigest,
    bool RequiresRationale,
    IReadOnlyList<string> AllowedActions,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ReasonCodesByAction,
    IReadOnlyList<string> AssignedActorRoles);

public sealed record ResearchWorkspaceDeduplicationReviewQueue(
    ResearchWorkspaceOperationStatus Status,
    int ExitCode,
    string Message,
    string? WorkspaceId,
    long? ProjectRevision,
    string? AuthorityGenerationId,
    string? AuthorityManifestDigest,
    ResearchWorkspaceDeduplicationReviewPolicy? Policy,
    IReadOnlyList<ResearchWorkspaceDeduplicationReviewTarget> Targets)
{
    public bool Completed => Status == ResearchWorkspaceOperationStatus.Succeeded;
}

public sealed record ResearchWorkspaceDeduplicationReviewPreview(
    ResearchWorkspaceOperationStatus Status,
    int ExitCode,
    string Message,
    string WorkspaceDirectory,
    string? WorkspaceId,
    long? ExpectedProjectRevision,
    string? AuthorityGenerationId,
    string? AuthorityManifestDigest,
    string? ActiveDecisionSetDigest,
    string? SourceResultId,
    string? SourceResultDigest,
    string? SourceSnapshotId,
    string? SourceSnapshotDigest,
    string? TargetId,
    string? TargetDigest,
    string? PolicyId,
    string? PolicyVersion,
    string? PolicyDigest,
    string? RequestId,
    string? RequestDigest,
    string? Action,
    string? Reason,
    string? Rationale,
    string? ActorId,
    string? ActorRole,
    string? SupersedesDecisionId,
    string? SupersedesDecisionDigest,
    DateTimeOffset OccurredAt,
    IReadOnlyList<string> AffectedCandidateIds,
    bool MembershipChanges,
    string? RepresentativeCandidateId,
    IReadOnlyList<string> InvalidatedRecords,
    bool UnresolvedWorkRemains)
{
    public bool IsReady => Status == ResearchWorkspaceOperationStatus.Succeeded && RequestDigest is not null;
}

public sealed record ResearchWorkspaceDeduplicationReviewCommitResult(
    ResearchWorkspaceOperationStatus Status,
    int ExitCode,
    string Message,
    ResearchWorkspaceProject? Project,
    string? DecisionId,
    string? SnapshotId,
    bool AlreadyApplied)
{
    public bool Completed => Status == ResearchWorkspaceOperationStatus.Succeeded;
}

public static class ResearchWorkspaceDeduplicationReview
{
    public static ResearchWorkspaceDeduplicationReviewQueue Inspect(string workingDirectory)
    {
        try
        {
            var state = Load(workingDirectory);
            var targets = CurrentTargets(state.Source)
                .Select(item => new ResearchWorkspaceDeduplicationReviewTarget(
                    item.TargetId,
                    item.TargetDigest.ToString(),
                    item.CandidateIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                    item.Evidence.Select(evidence => evidence.EvidenceId).OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                    state.Chain.ActiveDecisions
                        .Where(decision => string.Equals(decision.TargetId, item.TargetId, StringComparison.Ordinal))
                        .OrderBy(decision => decision.DecisionId, StringComparer.Ordinal)
                        .Select(decision => new ResearchWorkspaceActiveDeduplicationDecision(
                            decision.DecisionId,
                            decision.DecisionDigest.ToString(),
                            decision.ActionType,
                            decision.ActorId,
                            decision.ActorRole))
                        .ToArray()))
                .ToArray();
            var policy = state.Chain.Policy;
            return new ResearchWorkspaceDeduplicationReviewQueue(
                ResearchWorkspaceOperationStatus.Succeeded,
                ResearchWorkspaceExitCodes.Success,
                targets.Length == 0 ? "No current deduplication review targets remain." : $"{targets.Length} deduplication review target(s) require attention.",
                state.Project.WorkspaceId,
                state.Project.Revision,
                state.Chain.GenerationId,
                state.Project.AuthorityGenerationManifestSha256,
                new ResearchWorkspaceDeduplicationReviewPolicy(
                    policy.PolicyId,
                    policy.PolicyVersion,
                    policy.PolicyDigest.ToString(),
                    policy.RequiresRationale,
                    policy.AllowedActions.ToArray(),
                    policy.AllowedActions.ToDictionary(
                        action => action,
                        action => (IReadOnlyList<string>)policy.ReasonCodesForAction(action).ToArray(),
                        StringComparer.Ordinal),
                    policy.AuthorizedActorRoles
                        .Select(item => $"{item.ActorId}|{item.Role}")
                        .OrderBy(item => item, StringComparer.Ordinal)
                        .ToArray()),
                targets);
        }
        catch (Exception exception)
        {
            return QueueFailure(exception);
        }
    }

    public static ResearchWorkspaceDeduplicationReviewPreview Preview(
        ResearchWorkspaceDeduplicationReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var prepared = Prepare(request);
            return prepared.Preview;
        }
        catch (Exception exception)
        {
            return PreviewFailure(request, exception);
        }
    }

    public static ResearchWorkspaceDeduplicationReviewCommitResult Commit(
        ResearchWorkspaceDeduplicationReviewPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!preview.IsReady)
        {
            return CommitFailure(ResearchWorkspaceOperationStatus.Failed,
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                "An exact successful deduplication review preview is required.");
        }

        try
        {
            var currentState = Load(preview.WorkspaceDirectory);
            var currentTarget = CurrentTargets(currentState.Source).SingleOrDefault(item =>
                string.Equals(item.TargetId, preview.TargetId, StringComparison.Ordinal))
                ?? throw new ResearchWorkspaceAuthorityTransitionException(
                    ResearchWorkspaceAuthorityTransitionException.StaleAuthorityCategory,
                    "The exact review target is no longer current.");
            var replayCommand = RehydratePreviewCommand(preview, currentState, currentTarget);
            var isReplay = currentState.Chain.Transitions.Any(transition =>
                transition.Command.RequestDigest == replayCommand.RequestDigest);
            if (isReplay)
            {
                return FromCommit(ResearchWorkspaceTransaction.CommitDeduplicationDecision(
                    currentState.Location,
                    currentState.Project,
                    currentState.Source,
                    replayCommand,
                    currentTarget,
                    new FixedClock(preview.OccurredAt),
                    new GuidV7IdGenerator()));
            }

            if (currentState.Project.Revision != preview.ExpectedProjectRevision ||
                !string.Equals(currentState.Project.WorkspaceId, preview.WorkspaceId, StringComparison.Ordinal) ||
                !string.Equals(currentState.Chain.GenerationId, preview.AuthorityGenerationId, StringComparison.Ordinal) ||
                !string.Equals(currentState.Project.AuthorityGenerationManifestSha256, preview.AuthorityManifestDigest, StringComparison.Ordinal))
            {
                return CommitFailure(ResearchWorkspaceOperationStatus.Stale,
                    ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                    "stale-deduplication-review-preview: workspace authority advanced after preview.");
            }

            var request = new ResearchWorkspaceDeduplicationReviewRequest(
                preview.WorkspaceDirectory,
                preview.TargetId!,
                preview.Action!,
                preview.Reason!,
                preview.Rationale,
                preview.ActorId!,
                preview.ActorRole!,
                preview.SupersedesDecisionId,
                preview.OccurredAt);
            var prepared = Prepare(request);
            if (!SameBinding(preview, prepared.Preview))
            {
                return CommitFailure(ResearchWorkspaceOperationStatus.Stale,
                    ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                    "stale-deduplication-review-preview: workspace authority or preview material changed.");
            }

            var commit = ResearchWorkspaceTransaction.CommitDeduplicationDecision(
                prepared.State.Location,
                prepared.State.Project,
                prepared.State.Source,
                prepared.Command,
                prepared.Target,
                new FixedClock(preview.OccurredAt),
                new GuidV7IdGenerator());
            return FromCommit(commit);
        }
        catch (Exception exception)
        {
            return CommitException(exception);
        }
    }

    private static VerifiedDeduplicationReviewCommand RehydratePreviewCommand(
        ResearchWorkspaceDeduplicationReviewPreview preview,
        WorkspaceAuthorityState state,
        VerifiedDeduplicationAuthorityReviewTargetDigest target)
    {
        var material = new UnverifiedDeduplicationReviewCommand(
            DeduplicationReviewCommandConstants.SchemaId,
            DeduplicationReviewCommandConstants.SchemaVersion,
            preview.AuthorityGenerationId!,
            ContentDigest.Parse(preview.AuthorityManifestDigest!),
            ContentDigest.Parse(preview.ActiveDecisionSetDigest!),
            preview.SourceResultId!,
            ContentDigest.Parse(preview.SourceResultDigest!),
            preview.SourceSnapshotId!,
            ContentDigest.Parse(preview.SourceSnapshotDigest!),
            target.TargetKind,
            preview.TargetId!,
            ContentDigest.Parse(preview.TargetDigest!),
            preview.PolicyId!,
            preview.PolicyVersion!,
            ContentDigest.Parse(preview.PolicyDigest!),
            preview.Action!,
            preview.Reason!,
            preview.Rationale,
            preview.ActorId!,
            preview.ActorRole!,
            preview.SupersedesDecisionId,
            preview.SupersedesDecisionDigest is null ? null : ContentDigest.Parse(preview.SupersedesDecisionDigest),
            ContentDigest.Parse(preview.RequestDigest!));
        return DeduplicationReviewCommand.Rehydrate(
            material,
            state.Chain.Policy,
            state.Source,
            target,
            ContentDigest.Parse(preview.ActiveDecisionSetDigest!),
            preview.AuthorityGenerationId!,
            ContentDigest.Parse(preview.AuthorityManifestDigest!),
            preview.SourceSnapshotId!,
            ContentDigest.Parse(preview.SourceSnapshotDigest!));
    }

    private static ResearchWorkspaceDeduplicationReviewCommitResult FromCommit(
        ResearchWorkspaceAuthorityTransitionCommit commit) => new(
            ResearchWorkspaceOperationStatus.Succeeded,
            ResearchWorkspaceExitCodes.Success,
            commit.AlreadyApplied ? "Deduplication decision was already applied." : "Deduplication decision committed.",
            commit.Project,
            commit.Decision.DecisionId,
            commit.Snapshot.SnapshotId,
            commit.AlreadyApplied);

    private static PreparedReview Prepare(ResearchWorkspaceDeduplicationReviewRequest request)
    {
        var state = Load(request.WorkingDirectory);
        var target = CurrentTargets(state.Source).SingleOrDefault(item =>
            string.Equals(item.TargetId, Required(request.TargetId, nameof(request.TargetId)), StringComparison.Ordinal))
            ?? throw new ArgumentException("Target must identify an exact current deduplication review candidate pair.");
        var activeForTarget = state.Chain.ActiveDecisions
            .Where(item => string.Equals(item.TargetId, target.TargetId, StringComparison.Ordinal))
            .ToArray();
        if (activeForTarget.Length > 0 && string.IsNullOrWhiteSpace(request.SupersedesDecisionId))
        {
            throw new ArgumentException("An exact active decision id is required to correct this previously decided target.");
        }

        var superseded = string.IsNullOrWhiteSpace(request.SupersedesDecisionId)
            ? null
            : activeForTarget.SingleOrDefault(item =>
                string.Equals(item.DecisionId, request.SupersedesDecisionId.Trim(), StringComparison.Ordinal))
                ?? throw new ArgumentException("Superseded decision must identify an exact active decision for this target.");
        var material = new UnverifiedDeduplicationReviewCommand(
            DeduplicationReviewCommandConstants.SchemaId,
            DeduplicationReviewCommandConstants.SchemaVersion,
            state.Chain.GenerationId,
            ContentDigest.Parse(state.Project.AuthorityGenerationManifestSha256!),
            state.Chain.CurrentSnapshot.DecisionSetDigest,
            state.Source.Result.ResultId,
            state.Source.ResultDigest,
            state.Chain.CurrentSnapshot.SnapshotId,
            state.Chain.CurrentSnapshot.RecordDigest,
            target.TargetKind,
            target.TargetId,
            target.TargetDigest,
            state.Chain.Policy.PolicyId,
            state.Chain.Policy.PolicyVersion,
            state.Chain.Policy.PolicyDigest,
            Required(request.Action, nameof(request.Action)),
            Required(request.Reason, nameof(request.Reason)),
            Optional(request.Rationale),
            Required(request.ActorId, nameof(request.ActorId)),
            Required(request.ActorRole, nameof(request.ActorRole)),
            superseded?.DecisionId,
            superseded?.DecisionDigest);
        var command = DeduplicationReviewCommand.Create(
            material,
            state.Chain.Policy,
            state.Source,
            target,
            state.Chain.CurrentSnapshot.DecisionSetDigest,
            state.Chain.GenerationId,
            ContentDigest.Parse(state.Project.AuthorityGenerationManifestSha256!),
            state.Chain.CurrentSnapshot.SnapshotId,
            state.Chain.CurrentSnapshot.RecordDigest);
        var applicationPreview = DeduplicationReviewApplicationService.Preview(
            new DeduplicationReviewPreviewRequest(
                command,
                target,
                state.Source,
                state.Chain.Policy,
                state.Chain.CurrentSnapshot,
                state.Chain.ActiveDecisions,
                state.Chain.KnownDecisions,
                state.Chain.KnownSnapshots),
            new FixedClock(request.OccurredAt));
        var preview = new ResearchWorkspaceDeduplicationReviewPreview(
            ResearchWorkspaceOperationStatus.Succeeded,
            ResearchWorkspaceExitCodes.Success,
            "Review the exact deduplication authority effects before confirmation.",
            state.Location.RootDirectory,
            state.Project.WorkspaceId,
            state.Project.Revision,
            state.Chain.GenerationId,
            state.Project.AuthorityGenerationManifestSha256,
            state.Chain.CurrentSnapshot.DecisionSetDigest.ToString(),
            state.Source.Result.ResultId,
            state.Source.ResultDigest.ToString(),
            state.Chain.CurrentSnapshot.SnapshotId,
            state.Chain.CurrentSnapshot.RecordDigest.ToString(),
            target.TargetId,
            target.TargetDigest.ToString(),
            state.Chain.Policy.PolicyId,
            state.Chain.Policy.PolicyVersion,
            state.Chain.Policy.PolicyDigest.ToString(),
            applicationPreview.RequestId,
            applicationPreview.RequestDigest.ToString(),
            applicationPreview.Action,
            request.Reason.Trim(),
            Optional(request.Rationale),
            request.ActorId.Trim(),
            request.ActorRole.Trim(),
            superseded?.DecisionId,
            superseded?.DecisionDigest.ToString(),
            request.OccurredAt,
            applicationPreview.AffectedCandidateIds,
            applicationPreview.MembershipChanges,
            applicationPreview.RepresentativeCandidateId,
            applicationPreview.RecordsToInvalidate
                .Select(item => $"{item.RecordKind}:{item.RecordId}:{item.RecordDigest}")
                .ToArray(),
            applicationPreview.UnresolvedWorkRemains);
        return new PreparedReview(state, target, command, preview);
    }

    private static WorkspaceAuthorityState Load(string workingDirectory)
    {
        var location = ResearchWorkspaceStore.FindFrom(Path.GetFullPath(Required(workingDirectory, nameof(workingDirectory))))
            ?? throw new ResearchWorkspaceMissingInputException("No Nexus research workspace was found in the selected folder.");
        var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
        if (project.CurrentAuthorityGenerationId is null || project.AuthorityGenerationManifestSha256 is null)
        {
            throw new ArgumentException("An initialized deduplication authority generation is required before review.");
        }

        var relativePath = project.Outputs.GetValueOrDefault("deduplicationResult")
            ?? ResearchWorkspacePaths.CurrentDeduplicationResult;
        if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var resultPath) ||
            !File.Exists(resultPath))
        {
            throw new ResearchWorkspaceMissingInputException("The current deduplication result is missing or outside the workspace.");
        }

        var result = JsonSerializer.Deserialize<DeduplicationResult>(
            File.ReadAllBytes(resultPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new JsonException("The current deduplication result is empty.");
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
        var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(location, project, source);
        return new WorkspaceAuthorityState(location, project, source, chain);
    }

    private static IReadOnlyList<VerifiedDeduplicationAuthorityReviewTargetDigest> CurrentTargets(
        VerifiedDeduplicationAuthorityResultDigest source) =>
        source.Result.ReviewRequiredCandidates.Select(pair =>
        {
            var ids = new[] { pair.CandidateAId, pair.CandidateBId }
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var evidence = source.Result.Evidence.Where(item =>
                item.ObjectCandidateId is not null &&
                ids.Contains(item.SubjectCandidateId, StringComparer.Ordinal) &&
                ids.Contains(item.ObjectCandidateId, StringComparer.Ordinal) &&
                !string.Equals(item.SubjectCandidateId, item.ObjectCandidateId, StringComparison.Ordinal))
                .ToArray();
            return DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(source, pair, ids, evidence);
        }).OrderBy(item => item.TargetId, StringComparer.Ordinal).ToArray();

    private static bool SameBinding(
        ResearchWorkspaceDeduplicationReviewPreview expected,
        ResearchWorkspaceDeduplicationReviewPreview actual) =>
        expected.WorkspaceId == actual.WorkspaceId &&
        expected.ExpectedProjectRevision == actual.ExpectedProjectRevision &&
        expected.AuthorityGenerationId == actual.AuthorityGenerationId &&
        expected.AuthorityManifestDigest == actual.AuthorityManifestDigest &&
        expected.ActiveDecisionSetDigest == actual.ActiveDecisionSetDigest &&
        expected.SourceResultId == actual.SourceResultId &&
        expected.SourceResultDigest == actual.SourceResultDigest &&
        expected.SourceSnapshotId == actual.SourceSnapshotId &&
        expected.SourceSnapshotDigest == actual.SourceSnapshotDigest &&
        expected.TargetId == actual.TargetId &&
        expected.TargetDigest == actual.TargetDigest &&
        expected.PolicyId == actual.PolicyId &&
        expected.PolicyVersion == actual.PolicyVersion &&
        expected.PolicyDigest == actual.PolicyDigest &&
        expected.RequestId == actual.RequestId &&
        expected.RequestDigest == actual.RequestDigest &&
        expected.Action == actual.Action &&
        expected.Reason == actual.Reason &&
        expected.Rationale == actual.Rationale &&
        expected.ActorId == actual.ActorId &&
        expected.ActorRole == actual.ActorRole &&
        expected.SupersedesDecisionId == actual.SupersedesDecisionId &&
        expected.SupersedesDecisionDigest == actual.SupersedesDecisionDigest &&
        expected.OccurredAt == actual.OccurredAt &&
        expected.AffectedCandidateIds.SequenceEqual(actual.AffectedCandidateIds, StringComparer.Ordinal) &&
        expected.MembershipChanges == actual.MembershipChanges &&
        expected.RepresentativeCandidateId == actual.RepresentativeCandidateId &&
        expected.InvalidatedRecords.SequenceEqual(actual.InvalidatedRecords, StringComparer.Ordinal) &&
        expected.UnresolvedWorkRemains == actual.UnresolvedWorkRemains;

    private static ResearchWorkspaceDeduplicationReviewQueue QueueFailure(Exception exception)
    {
        var (status, exitCode, message) = Classify(exception);
        return new ResearchWorkspaceDeduplicationReviewQueue(
            status, exitCode, message, null, null, null, null, null,
            Array.Empty<ResearchWorkspaceDeduplicationReviewTarget>());
    }

    private static ResearchWorkspaceDeduplicationReviewPreview PreviewFailure(
        ResearchWorkspaceDeduplicationReviewRequest request,
        Exception exception)
    {
        var (status, exitCode, message) = Classify(exception);
        return new ResearchWorkspaceDeduplicationReviewPreview(
            Status: status,
            ExitCode: exitCode,
            Message: message,
            WorkspaceDirectory: Path.GetFullPath(request.WorkingDirectory),
            WorkspaceId: null,
            ExpectedProjectRevision: null,
            AuthorityGenerationId: null,
            AuthorityManifestDigest: null,
            ActiveDecisionSetDigest: null,
            SourceResultId: null,
            SourceResultDigest: null,
            SourceSnapshotId: null,
            SourceSnapshotDigest: null,
            TargetId: null,
            TargetDigest: null,
            PolicyId: null,
            PolicyVersion: null,
            PolicyDigest: null,
            RequestId: null,
            RequestDigest: null,
            Action: request.Action,
            Reason: request.Reason,
            Rationale: request.Rationale,
            ActorId: request.ActorId,
            ActorRole: request.ActorRole,
            SupersedesDecisionId: request.SupersedesDecisionId,
            SupersedesDecisionDigest: null,
            OccurredAt: request.OccurredAt,
            AffectedCandidateIds: Array.Empty<string>(),
            MembershipChanges: false,
            RepresentativeCandidateId: null,
            InvalidatedRecords: Array.Empty<string>(),
            UnresolvedWorkRemains: false);
    }

    private static ResearchWorkspaceDeduplicationReviewCommitResult CommitException(Exception exception)
    {
        var (status, exitCode, message) = Classify(exception);
        return CommitFailure(status, exitCode, message);
    }

    private static ResearchWorkspaceDeduplicationReviewCommitResult CommitFailure(
        ResearchWorkspaceOperationStatus status,
        int exitCode,
        string message) => new(status, exitCode, message, null, null, null, false);

    private static (ResearchWorkspaceOperationStatus Status, int ExitCode, string Message) Classify(Exception exception) =>
        exception switch
        {
            ResearchWorkspaceAuthorityTransitionException transition when
                transition.Category == ResearchWorkspaceAuthorityTransitionException.StaleAuthorityCategory =>
                (ResearchWorkspaceOperationStatus.Stale, ResearchWorkspaceExitCodes.UsageOrValidationFailure, transition.Message),
            ResearchWorkspaceConcurrencyException concurrency when concurrency.InnerException is not IOException =>
                (ResearchWorkspaceOperationStatus.Stale, ResearchWorkspaceExitCodes.UsageOrValidationFailure, concurrency.Message),
            ResearchWorkspaceConcurrencyException concurrency =>
                (ResearchWorkspaceOperationStatus.RecoveryRequired, ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure, concurrency.Message),
            IOException =>
                (ResearchWorkspaceOperationStatus.RecoveryRequired, ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                    "Deduplication review could not access the local workspace safely."),
            UnauthorizedAccessException =>
                (ResearchWorkspaceOperationStatus.RecoveryRequired, ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                    "Deduplication review could not access the local workspace safely."),
            DeduplicationAuthorityException authority =>
                (ResearchWorkspaceOperationStatus.Failed, ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                    $"{authority.Category}: {authority.Message}"),
            ArgumentException =>
                (ResearchWorkspaceOperationStatus.Failed, ResearchWorkspaceExitCodes.UsageOrValidationFailure, exception.Message),
            ResearchWorkspaceMissingInputException =>
                (ResearchWorkspaceOperationStatus.Failed, ResearchWorkspaceExitCodes.MissingProjectOrInput, exception.Message),
            _ =>
                (ResearchWorkspaceOperationStatus.RecoveryRequired, ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                    "Deduplication review authority could not be reconstructed from the local workspace.")
        };

    private static string Required(string? value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record WorkspaceAuthorityState(
        ResearchWorkspaceLocation Location,
        ResearchWorkspaceProject Project,
        VerifiedDeduplicationAuthorityResultDigest Source,
        ResearchWorkspaceVerifiedAuthorityChain Chain);

    private sealed record PreparedReview(
        WorkspaceAuthorityState State,
        VerifiedDeduplicationAuthorityReviewTargetDigest Target,
        VerifiedDeduplicationReviewCommand Command,
        ResearchWorkspaceDeduplicationReviewPreview Preview);

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }
}
