using System.Text.Json;

using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceReviewCommand
{
    private const string Usage = "Usage: nexus review";

    public static int Run(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (args.Length > 0)
        {
            error.WriteLine(Usage);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }

        try
        {
            var loaded = WorkspacePlanReader.Read(workingDirectory);
            output.Write(WorkspacePlanTextRenderer.RenderReview(loaded.Plan));
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
            error.WriteLine($"Unable to read workspace review queue: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }
}
