using System.Text.Json;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceStatusCommand
{
    public static int Run(TextWriter output, TextWriter error, string workingDirectory)
    {
        try
        {
            var projectFile = ResearchWorkspacePaths.ProjectFile(workingDirectory);
            if (!File.Exists(projectFile))
            {
                error.WriteLine("No Nexus research workspace found in the current folder.");
                error.WriteLine("Run: nexus init --title \"<research title>\"");
                return ResearchWorkspaceExitCodes.MissingProjectOrInput;
            }

            var project = ReadProject(projectFile);
            if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
            {
                error.WriteLine($"Unsupported Nexus project schema: {project.Schema}");
                return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
            }

            output.WriteLine("Nexus research workspace");
            output.WriteLine("Status: initialized");
            output.WriteLine($"Project: {project.Title}");
            output.WriteLine($"Workspace: {project.WorkspaceId}");
            output.WriteLine("Inputs:");
            output.WriteLine($"  search exports: {CountSearchExports(project)}");
            output.WriteLine("Outputs:");
            output.WriteLine($"  import traces: {CountFiles(workingDirectory, ResearchWorkspacePaths.ImportOutputs, "*.json")}");
            output.WriteLine($"  dedup analysis: {Presence(workingDirectory, ResearchWorkspacePaths.CurrentDeduplicationResult)}");
            output.WriteLine($"  workspace plan: {Presence(workingDirectory, ResearchWorkspacePaths.CurrentWorkspacePlan)}");
            output.WriteLine($"  reports: {CountFiles(workingDirectory, ResearchWorkspacePaths.ReportOutputs, "*")}");
            output.WriteLine("Next: nexus import search <file> --source <source> --format <format>");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (JsonException exception)
        {
            error.WriteLine($"Malformed Nexus project file: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Unable to read Nexus research workspace: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }

    private static ResearchWorkspaceProject ReadProject(string projectFile)
    {
        var project = ResearchWorkspaceJson.Deserialize(File.ReadAllText(projectFile));
        if (project is null)
        {
            throw new JsonException("Project file did not contain an object.");
        }

        ValidateProject(project);
        return project;
    }

    private static void ValidateProject(ResearchWorkspaceProject project)
    {
        if (string.IsNullOrWhiteSpace(project.Schema))
        {
            throw new JsonException("Project schema is required.");
        }

        if (string.IsNullOrWhiteSpace(project.WorkspaceId))
        {
            throw new JsonException("Project workspaceId is required.");
        }

        if (string.IsNullOrWhiteSpace(project.Title))
        {
            throw new JsonException("Project title is required.");
        }

        if (project.Inputs is null)
        {
            throw new JsonException("Project inputs must be an array.");
        }

        if (project.Outputs is null)
        {
            throw new JsonException("Project outputs must be an object.");
        }
    }

    private static int CountSearchExports(ResearchWorkspaceProject project)
    {
        return project.Inputs.Count(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal));
    }

    private static int CountFiles(string workingDirectory, string relativeDirectory, string searchPattern)
    {
        var directory = ResearchWorkspacePaths.InProject(workingDirectory, relativeDirectory);
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, searchPattern, SearchOption.TopDirectoryOnly).Count()
            : 0;
    }

    private static string Presence(string workingDirectory, string relativePath)
    {
        return File.Exists(ResearchWorkspacePaths.InProject(workingDirectory, relativePath)) ? "present" : "missing";
    }
}
