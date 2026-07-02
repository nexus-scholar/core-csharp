using System.Text.Json;
using NexusScholar.ResearchWorkspace;
using NexusScholar.Search;
using NexusScholar.UiContracts;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceAnalyzeCommand
{
    private const string Usage = "Usage: nexus analyze";

    public static int Run(string[] args, TextWriter output, TextWriter error, string workingDirectory)
    {
        if (args.Length > 0)
        {
            error.WriteLine(Usage);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }

        try
        {
            var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
            if (location is null)
            {
                error.WriteLine("No Nexus research workspace found in the current folder or its parents.");
                error.WriteLine("Run: nexus init --title \"<research title>\"");
                return ResearchWorkspaceExitCodes.MissingProjectOrInput;
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
            {
                error.WriteLine($"Unsupported Nexus project schema: {project.Schema}");
                return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
            }

            var result = ResearchWorkspaceAnalyzer.Analyze(location, project);
            WriteOutputs(location, result);
            UpdateProjectOutputs(location, project);
            WriteSummary(output, result);
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (JsonException exception)
        {
            error.WriteLine($"Malformed Nexus project file: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
        }
        catch (SearchRuleException exception)
        {
            error.WriteLine($"Analyze failed while parsing imported search evidence: {exception.Message}");
            return IsUnsupportedSearchRule(exception)
                ? ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat
                : ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (ResearchWorkspaceMissingInputException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.MissingProjectOrInput;
        }
        catch (ResearchWorkspaceDigestMismatchException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.DigestMismatch;
        }
        catch (InvalidOperationException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Unable to analyze Nexus research workspace: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
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

    private static void WriteSummary(TextWriter output, ResearchWorkspaceAnalysisResult result)
    {
        output.WriteLine("Workspace analysis complete");
        output.WriteLine($"Mode: {result.WorkspacePlan.Mode}");
        output.WriteLine($"Import traces: {result.ImportTraces.Count}");
        output.WriteLine($"Imported records: {result.ImportedRecordCount}");
        output.WriteLine($"Parser warnings: {result.ParserWarningCount}");
        output.WriteLine($"Exact duplicate clusters: {result.DeduplicationResult.Clusters.Count}");
        output.WriteLine($"Review-required duplicate candidates: {result.DeduplicationResult.ReviewRequiredCandidates.Count}");
        output.WriteLine($"WorkspacePlan: {ResearchWorkspaceAnalyzer.WorkspacePlanPath}");
        output.WriteLine($"Deduplication result: {ResearchWorkspaceAnalyzer.DeduplicationResultPath}");
        output.WriteLine($"Review report: {ResearchWorkspaceAnalyzer.ReviewReportPath}");
        output.WriteLine("Next: nexus review");
    }

    private static bool IsUnsupportedSearchRule(SearchRuleException exception) =>
        exception.Message.Contains("Unsupported", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase);
}
