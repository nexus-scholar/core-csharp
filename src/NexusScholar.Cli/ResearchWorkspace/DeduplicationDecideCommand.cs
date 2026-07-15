using System.Text.Json;
using NexusScholar.AppServices;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class DeduplicationDecideCommand
{
    internal static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        Func<DateTimeOffset> utcNow)
    {
        try
        {
            var options = Parse(args);
            var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
            if (location is null)
            {
                error.WriteLine("No Nexus research workspace found in the current folder or its parents.");
                return ResearchWorkspaceExitCodes.MissingProjectOrInput;
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (project.CurrentAuthorityGenerationId is null)
            {
                error.WriteLine("An initialized authority generation is required before recording a decision.");
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }
            var resultPath = ResearchWorkspacePaths.InProject(location.RootDirectory,
                project.Outputs.GetValueOrDefault("deduplicationResult") ?? ResearchWorkspacePaths.CurrentDeduplicationResult);
            var result = JsonSerializer.Deserialize<DeduplicationResult>(File.ReadAllBytes(resultPath),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new JsonException("The current deduplication result is empty.");
            var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
            var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(location, project, source);
            var target = ResolveTarget(source, options.TargetId);
            var superseded = options.SupersedesDecisionId is null ? null : chain.ActiveDecisions.SingleOrDefault(item =>
                string.Equals(item.DecisionId, options.SupersedesDecisionId, StringComparison.Ordinal))
                ?? throw new ArgumentException("--supersedes must identify an active decision.");
            var material = new UnverifiedDeduplicationReviewCommand(
                DeduplicationReviewCommandConstants.SchemaId,
                DeduplicationReviewCommandConstants.SchemaVersion,
                chain.GenerationId,
                ContentDigest.Parse(project.AuthorityGenerationManifestSha256!),
                chain.CurrentSnapshot.DecisionSetDigest,
                source.Result.ResultId,
                source.ResultDigest,
                chain.CurrentSnapshot.SnapshotId,
                chain.CurrentSnapshot.RecordDigest,
                target.TargetKind,
                target.TargetId,
                target.TargetDigest,
                chain.Policy.PolicyId,
                chain.Policy.PolicyVersion,
                chain.Policy.PolicyDigest,
                options.Action,
                options.Reason,
                options.Rationale,
                options.Actor,
                options.Role,
                superseded?.DecisionId,
                superseded?.DecisionDigest);
            var command = DeduplicationReviewCommand.Create(
                material, chain.Policy, source, target, chain.CurrentSnapshot.DecisionSetDigest,
                chain.GenerationId, ContentDigest.Parse(project.AuthorityGenerationManifestSha256!),
                chain.CurrentSnapshot.SnapshotId, chain.CurrentSnapshot.RecordDigest);
            var clock = new DelegateClock(utcNow);
            var preview = DeduplicationReviewApplicationService.Preview(new DeduplicationReviewPreviewRequest(
                command, target, source, chain.Policy, chain.CurrentSnapshot, chain.ActiveDecisions,
                chain.KnownDecisions, chain.KnownSnapshots), clock);
            WritePreview(output, preview);
            if (!options.Confirm)
            {
                output.WriteLine("Status: preview-only");
                return ResearchWorkspaceExitCodes.Success;
            }

            var commit = ResearchWorkspaceTransaction.CommitDeduplicationDecision(
                location, project, source, command, target, clock, new GuidV7IdGenerator());
            output.WriteLine($"Status: {(commit.AlreadyApplied ? "already-applied" : "committed")}");
            output.WriteLine($"Decision: {commit.Decision.DecisionId}");
            output.WriteLine($"Snapshot: {commit.Snapshot.SnapshotId}");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (ResearchWorkspaceAuthorityTransitionException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (DeduplicationAuthorityException exception)
        {
            error.WriteLine($"{exception.Category}: {exception.Message}");
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or IOException)
        {
            error.WriteLine($"Unable to decide deduplication review: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }

    private static VerifiedDeduplicationAuthorityReviewTargetDigest ResolveTarget(
        VerifiedDeduplicationAuthorityResultDigest source,
        string targetId)
    {
        foreach (var pair in source.Result.ReviewRequiredCandidates)
        {
            var ids = new[] { pair.CandidateAId, pair.CandidateBId }.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            var evidence = source.Result.Evidence.Where(item => item.ObjectCandidateId is not null &&
                ids.Contains(item.SubjectCandidateId, StringComparer.Ordinal) &&
                ids.Contains(item.ObjectCandidateId, StringComparer.Ordinal) &&
                !string.Equals(item.SubjectCandidateId, item.ObjectCandidateId, StringComparison.Ordinal)).ToArray();
            var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(source, pair, ids, evidence);
            if (string.Equals(target.TargetId, targetId, StringComparison.Ordinal)) return target;
        }
        throw new ArgumentException("--target must identify an exact current review candidate pair.");
    }

    private static void WritePreview(TextWriter output, DeduplicationReviewPreview preview)
    {
        output.WriteLine("Deduplication decision preview");
        output.WriteLine($"Request: {preview.RequestId}");
        output.WriteLine($"Digest: {preview.RequestDigest}");
        output.WriteLine($"Action: {preview.Action}");
        output.WriteLine($"Candidates: {string.Join(", ", preview.AffectedCandidateIds)}");
        output.WriteLine($"Membership changes: {preview.MembershipChanges.ToString().ToLowerInvariant()}");
        output.WriteLine($"Representative: {preview.RepresentativeCandidateId ?? "unchanged"}");
        output.WriteLine($"Invalidates: {string.Join(", ", preview.RecordsToInvalidate.Select(item => $"{item.RecordKind}:{item.RecordId}"))}");
        output.WriteLine($"Unresolved work remains: {preview.UnresolvedWorkRemains.ToString().ToLowerInvariant()}");
    }

    private static Options Parse(string[] args)
    {
        string? target = null, action = null, reason = null, rationale = null, actor = null, role = null, supersedes = null;
        var confirm = false;
        for (var index = 0; index < args.Length; index++)
        {
            var name = args[index];
            if (name == "--confirm") { confirm = true; continue; }
            if (index + 1 >= args.Length) throw new ArgumentException($"Missing value for option: {name}");
            var value = args[++index];
            switch (name)
            {
                case "--target": target = value; break;
                case "--action": action = value; break;
                case "--reason": reason = value; break;
                case "--rationale": rationale = value; break;
                case "--actor": actor = value; break;
                case "--role": role = value; break;
                case "--supersedes": supersedes = value; break;
                default: throw new ArgumentException($"Unknown option: {name}");
            }
        }
        if (new[] { target, action, reason, actor, role }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Required: --target, --action, --reason, --actor, and --role.");
        return new Options(target!, action!, reason!, rationale, actor!, role!, supersedes, confirm);
    }

    private sealed record Options(string TargetId, string Action, string Reason, string? Rationale,
        string Actor, string Role, string? SupersedesDecisionId, bool Confirm);

    private sealed class DelegateClock(Func<DateTimeOffset> utcNow) : IClock
    {
        public DateTimeOffset UtcNow => utcNow();
    }
}
