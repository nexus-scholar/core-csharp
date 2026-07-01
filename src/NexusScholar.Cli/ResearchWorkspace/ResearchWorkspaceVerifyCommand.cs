using System.Text.Json;
using NexusScholar.Search;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceVerifyCommand
{
    private const string Usage = "Usage: nexus verify";

    public static int Run(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (args.Length > 0)
        {
            error.WriteLine(Usage);
            return ResearchWorkspaceExitCodes.MissingProjectOrInput;
        }

        try
        {
            var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
            if (location is null)
            {
                output.WriteLine("Workspace verification");
                output.WriteLine("Status: invalid");
                output.WriteLine("No Nexus research workspace found in the current folder or its parents.");
                output.WriteLine("Next: nexus init --title \"<research title>\"");
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
            {
                output.WriteLine("Workspace verification");
                output.WriteLine("Status: invalid");
                output.WriteLine($"Unsupported Nexus project schema: {project.Schema}");
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            var report = ResearchWorkspaceVerifier.Verify(location, project);
            WriteReport(output, report);
            return report.IsValid
                ? ResearchWorkspaceExitCodes.Success
                : ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (JsonException exception)
        {
            output.WriteLine("Workspace verification");
            output.WriteLine("Status: invalid");
            output.WriteLine($"Malformed Nexus project file: {exception.Message}");
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (SearchRuleException exception)
        {
            output.WriteLine("Workspace verification");
            output.WriteLine("Status: invalid");
            output.WriteLine($"Search import parse failed: {exception.Message}");
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Unable to verify Nexus research workspace: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }

    private static void WriteReport(TextWriter output, ResearchWorkspaceVerificationReport report)
    {
        output.WriteLine("Workspace verification");
        output.WriteLine($"Status: {(report.IsValid ? "valid" : "invalid")}");
        output.WriteLine($"Inputs: {report.InputCount}");

        if (report.IsValid)
        {
            output.WriteLine($"Files unchanged: {report.FilesUnchanged}");
            output.WriteLine("Files missing: 0");
            output.WriteLine("Digest mismatches: 0");
            output.WriteLine($"Parser warnings: {report.ParserWarningCount}");
            foreach (var warning in report.ParserWarningCategories.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                output.WriteLine($"Warning category: {warning.Key} ({warning.Value})");
            }

            output.WriteLine($"Skipped records: {report.SkippedRecordCount}");
            output.WriteLine("Next: nexus analyze");
            return;
        }

        if (report.InvalidPaths.Count > 0)
        {
            output.WriteLine($"Invalid paths: {report.InvalidPaths.Count}");
            foreach (var path in report.InvalidPaths)
            {
                output.WriteLine($"Invalid path: {path}");
            }

            output.WriteLine("Next: fix project-relative paths before verification.");
            return;
        }

        if (report.MissingFiles.Count > 0)
        {
            output.WriteLine($"Files unchanged: {report.FilesUnchanged}");
            output.WriteLine($"Files missing: {report.MissingFiles.Count}");
            foreach (var path in report.MissingFiles)
            {
                output.WriteLine($"Missing: {path}");
            }

            output.WriteLine("Next: restore the missing file or remove the input entry intentionally.");
            return;
        }

        if (report.DigestMismatches.Count > 0)
        {
            output.WriteLine($"Digest mismatches: {report.DigestMismatches.Count}");
            foreach (var path in report.DigestMismatches)
            {
                output.WriteLine($"Changed: {path}");
            }

            output.WriteLine("Next: re-import the file intentionally or restore the original bytes.");
            return;
        }

        if (report.MissingImportTraces.Count > 0)
        {
            output.WriteLine($"Import traces missing: {report.MissingImportTraces.Count}");
            foreach (var path in report.MissingImportTraces)
            {
                output.WriteLine($"Missing trace: {path}");
            }

            output.WriteLine("Next: re-import the source export intentionally.");
            return;
        }

        output.WriteLine($"Parser warnings: {report.ParserWarningCount}");
        output.WriteLine($"Skipped records: {report.SkippedRecordCount}");
        output.WriteLine("Next: review parser warnings before analyze.");
    }
}
