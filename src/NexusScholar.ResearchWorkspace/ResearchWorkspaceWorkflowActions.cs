using System.Text.Json;
using NexusScholar.Search;
using NexusScholar.UiContracts;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceWorkflowActions
{
    public static ResearchWorkspaceActionResult Verify(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            var context = LoadContext(workingDirectory);
            if (context is null)
            {
                return ResearchWorkspaceActionResult.Failed(
                    ResearchWorkspaceExitCodes.MissingProjectOrInput,
                    "No Nexus research workspace was found. Open a folder that contains nexus.project.json.");
            }

            var report = ResearchWorkspaceVerifier.Verify(context.Location, context.Project);
            var message = report.IsValid
                ? $"Workspace verification complete. Inputs: {report.InputCount}; files unchanged: {report.FilesUnchanged}; parser warnings: {report.ParserWarningCount}; skipped records: {report.SkippedRecordCount}. Next: Analyze."
                : $"Workspace verification needs attention. Missing files: {report.MissingFiles.Count}; digest mismatches: {report.DigestMismatches.Count}; invalid paths: {report.InvalidPaths.Count}; missing traces: {report.MissingImportTraces.Count}; parser warnings: {report.ParserWarningCount}; skipped records: {report.SkippedRecordCount}.";

            return new ResearchWorkspaceActionResult(
                Completed: true,
                RequiresAttention: !report.IsValid,
                ExitCode: ExitCodeFor(report),
                Message: message);
        }
        catch (JsonException exception)
        {
            return ResearchWorkspaceActionResult.Failed(
                ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat,
                $"Malformed Nexus project file: {exception.Message}");
        }
        catch (SearchRuleException exception)
        {
            return ResearchWorkspaceActionResult.Failed(
                IsUnsupportedSearchRule(exception)
                    ? ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat
                    : ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                $"Search import parse failed: {exception.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ResearchWorkspaceActionResult.Failed(
                ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                "Workspace verification could not read one or more local files.");
        }
    }

    public static ResearchWorkspaceActionResult Analyze(string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        try
        {
            var context = LoadContext(workingDirectory);
            if (context is null)
            {
                return ResearchWorkspaceActionResult.Failed(
                    ResearchWorkspaceExitCodes.MissingProjectOrInput,
                    "No Nexus research workspace was found. Open a folder that contains nexus.project.json.");
            }

            var result = ResearchWorkspaceAnalyzer.Analyze(context.Location, context.Project);
            WriteOutputs(context.Location, result);
            UpdateProjectOutputs(context.Location, context.Project);

            return new ResearchWorkspaceActionResult(
                Completed: true,
                RequiresAttention: false,
                ExitCode: ResearchWorkspaceExitCodes.Success,
                Message:
                    $"Workspace analysis complete. WorkspacePlan: {ResearchWorkspaceAnalyzer.WorkspacePlanPath}; Deduplication result: {ResearchWorkspaceAnalyzer.DeduplicationResultPath}; Review report: {ResearchWorkspaceAnalyzer.ReviewReportPath}. Next: Review queue.");
        }
        catch (JsonException exception)
        {
            return ResearchWorkspaceActionResult.Failed(
                ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat,
                $"Malformed Nexus project file: {exception.Message}");
        }
        catch (SearchRuleException exception)
        {
            return ResearchWorkspaceActionResult.Failed(
                IsUnsupportedSearchRule(exception)
                    ? ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat
                    : ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                $"Analyze failed while parsing imported search evidence: {exception.Message}");
        }
        catch (ResearchWorkspaceMissingInputException exception)
        {
            return ResearchWorkspaceActionResult.Failed(
                ResearchWorkspaceExitCodes.MissingProjectOrInput,
                exception.Message);
        }
        catch (ResearchWorkspaceDigestMismatchException exception)
        {
            return ResearchWorkspaceActionResult.Failed(
                ResearchWorkspaceExitCodes.DigestMismatch,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return ResearchWorkspaceActionResult.Failed(
                ResearchWorkspaceExitCodes.UsageOrValidationFailure,
                exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ResearchWorkspaceActionResult.Failed(
                ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure,
                "Workspace analysis could not write one or more local output files.");
        }
    }

    private static ResearchWorkspaceActionContext? LoadContext(string workingDirectory)
    {
        var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
        if (location is null)
        {
            return null;
        }

        var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
        if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
        {
            throw new JsonException($"Unsupported Nexus project schema: {project.Schema}");
        }

        return new ResearchWorkspaceActionContext(location, project);
    }

    private static void WriteOutputs(ResearchWorkspaceLocation location, ResearchWorkspaceAnalysisResult result)
    {
        Directory.CreateDirectory(ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.DedupOutputs));
        Directory.CreateDirectory(ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.WorkspaceOutputs));
        Directory.CreateDirectory(ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.ReportOutputs));

        ResearchWorkspaceJson.WriteJsonFile(
            ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspaceAnalyzer.DeduplicationResultPath),
            result.DeduplicationResult);
        ResearchWorkspaceJson.WriteJsonFile(
            ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspaceAnalyzer.WorkspacePlanPath),
            result.WorkspacePlan,
            UiContractJson.SerializerOptions);
        ResearchWorkspaceJson.WriteTextFile(
            ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspaceAnalyzer.ReviewReportPath),
            WorkspacePlanReportWriter.Format(result));
    }

    private static void UpdateProjectOutputs(ResearchWorkspaceLocation location, ResearchWorkspaceProject project)
    {
        var outputs = new Dictionary<string, string>(project.Outputs, StringComparer.Ordinal)
        {
            ["deduplicationResult"] = ResearchWorkspaceAnalyzer.DeduplicationResultPath,
            ["workspacePlan"] = ResearchWorkspaceAnalyzer.WorkspacePlanPath,
            ["reviewReport"] = ResearchWorkspaceAnalyzer.ReviewReportPath
        };
        ResearchWorkspaceStore.WriteProject(location, project.WithOutputs(outputs));
    }

    private static int ExitCodeFor(ResearchWorkspaceVerificationReport report)
    {
        if (report.IsValid)
        {
            return ResearchWorkspaceExitCodes.Success;
        }

        if (report.DigestMismatches.Count > 0)
        {
            return ResearchWorkspaceExitCodes.DigestMismatch;
        }

        if (report.MissingFiles.Count > 0 || report.MissingImportTraces.Count > 0)
        {
            return ResearchWorkspaceExitCodes.MissingProjectOrInput;
        }

        return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
    }

    private static bool IsUnsupportedSearchRule(SearchRuleException exception) =>
        exception.Message.Contains("Unsupported", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase);

    private sealed record ResearchWorkspaceActionContext(
        ResearchWorkspaceLocation Location,
        ResearchWorkspaceProject Project);
}

public sealed record ResearchWorkspaceActionResult(
    bool Completed,
    bool RequiresAttention,
    int ExitCode,
    string Message)
{
    public static ResearchWorkspaceActionResult Failed(int exitCode, string message)
    {
        return new ResearchWorkspaceActionResult(
            Completed: false,
            RequiresAttention: true,
            ExitCode: exitCode,
            Message: message);
    }
}
