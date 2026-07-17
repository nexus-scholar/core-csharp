using NexusScholar.Deduplication;
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
            var preview = ResearchWorkspaceDeduplicationReview.Preview(
                new ResearchWorkspaceDeduplicationReviewRequest(
                    workingDirectory,
                    options.TargetId,
                    options.Action,
                    options.Reason,
                    options.Rationale,
                    options.Actor,
                    options.Role,
                    options.SupersedesDecisionId,
                    utcNow()));
            if (!preview.IsReady)
            {
                error.WriteLine(preview.Message);
                return preview.ExitCode;
            }

            WritePreview(output, preview);
            if (!options.Confirm)
            {
                output.WriteLine("Status: preview-only");
                return ResearchWorkspaceExitCodes.Success;
            }

            var commit = ResearchWorkspaceDeduplicationReview.Commit(preview);
            if (!commit.Completed)
            {
                error.WriteLine(commit.Message);
                return commit.ExitCode;
            }

            output.WriteLine($"Status: {(commit.AlreadyApplied ? "already-applied" : "committed")}");
            output.WriteLine($"Decision: {commit.DecisionId}");
            output.WriteLine($"Snapshot: {commit.SnapshotId}");
            return commit.ExitCode;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            error.WriteLine($"Unable to decide deduplication review: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }

    private static void WritePreview(TextWriter output, ResearchWorkspaceDeduplicationReviewPreview preview)
    {
        output.WriteLine("Deduplication decision preview");
        output.WriteLine($"Request: {preview.RequestId}");
        output.WriteLine($"Digest: {preview.RequestDigest}");
        output.WriteLine($"Action: {preview.Action}");
        output.WriteLine($"Candidates: {string.Join(", ", preview.AffectedCandidateIds)}");
        output.WriteLine($"Membership changes: {preview.MembershipChanges.ToString().ToLowerInvariant()}");
        output.WriteLine($"Representative: {preview.RepresentativeCandidateId ?? "unchanged"}");
        output.WriteLine($"Invalidates: {string.Join(", ", preview.InvalidatedRecords.Select(item => string.Join(":", item.Split(':').Take(2))))}");
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

}
