using System.Text.Json;

using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceClustersCommand
{
    private const string Usage = "Usage: nexus clusters [exact|review|show <cluster-or-candidate-id>]";

    public static int Run(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (!IsValidShape(args))
        {
            error.WriteLine(Usage);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }

        try
        {
            var loaded = WorkspacePlanReader.Read(workingDirectory, requireDeduplicationResult: true);
            var rendered = args.Length switch
            {
                0 => WorkspacePlanTextRenderer.RenderClustersSummary(loaded.Plan),
                1 when string.Equals(args[0], "exact", StringComparison.Ordinal) => WorkspacePlanTextRenderer.RenderClustersExact(loaded.Plan),
                1 when string.Equals(args[0], "review", StringComparison.Ordinal) => WorkspacePlanTextRenderer.RenderClustersReview(loaded.Plan),
                2 when string.Equals(args[0], "show", StringComparison.Ordinal) => WorkspacePlanTextRenderer.RenderClusterShow(loaded.Plan, args[1]),
                _ => throw new InvalidOperationException("Unreachable clusters command shape.")
            };

            if (rendered is null)
            {
                error.WriteLine($"Cluster or review candidate not found: {args[1]}");
                error.WriteLine("Run: nexus clusters review");
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            output.Write(rendered);
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (WorkspacePlanReadException exception)
        {
            error.WriteLine(exception.Message);
            if (exception.ExitCode == ResearchWorkspaceExitCodes.MissingProjectOrInput)
            {
                error.WriteLine("Run: nexus analyze");
            }

            return exception.ExitCode;
        }
        catch (JsonException exception)
        {
            error.WriteLine($"Malformed workspace plan: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Unable to read workspace clusters: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }

    private static bool IsValidShape(string[] args)
    {
        return args.Length == 0 ||
            args.Length == 1 && (string.Equals(args[0], "exact", StringComparison.Ordinal) || string.Equals(args[0], "review", StringComparison.Ordinal)) ||
            args.Length == 2 && string.Equals(args[0], "show", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(args[1]);
    }
}
