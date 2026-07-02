using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceInitCommand
{
    public static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        Func<DateTimeOffset> utcNow)
    {
        try
        {
            var options = Parse(args);

            if (string.IsNullOrWhiteSpace(options.Title))
            {
                error.WriteLine("Missing required option: --title");
                error.WriteLine("Usage: nexus init --title \"<research title>\"");
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            if (options.WorkspaceId is not null && string.IsNullOrWhiteSpace(options.WorkspaceId))
            {
                error.WriteLine("Missing value for option: --workspace-id");
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            var projectFile = ResearchWorkspacePaths.ProjectFile(workingDirectory);
            if (File.Exists(projectFile))
            {
                error.WriteLine("A Nexus research workspace already exists in this folder.");
                error.WriteLine("Project file: nexus.project.json");
                error.WriteLine("Run: nexus status");
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            foreach (var relativeDirectory in ResearchWorkspacePaths.RequiredDirectories)
            {
                Directory.CreateDirectory(ResearchWorkspacePaths.InProject(workingDirectory, relativeDirectory));
            }

            var project = ResearchWorkspaceProject.Create(options.Title, utcNow(), options.WorkspaceId);
            ResearchWorkspaceJson.WriteProjectFile(projectFile, project);

            output.WriteLine("Nexus research workspace initialized");
            output.WriteLine($"Project: {project.Title}");
            output.WriteLine($"Workspace: {project.WorkspaceId}");
            output.WriteLine("Project file: nexus.project.json");
            output.WriteLine("Inputs: inputs/search");
            output.WriteLine("Outputs:");
            output.WriteLine("  imports: nexus-output/imports");
            output.WriteLine("  dedup: nexus-output/dedup");
            output.WriteLine("  workspace: nexus-output/workspace");
            output.WriteLine("  reports: nexus-output/reports");
            output.WriteLine("Next: nexus status");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Unable to initialize Nexus research workspace: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }

    private static InitOptions Parse(string[] args)
    {
        string? title = null;
        string? workspaceId = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--title":
                    title = ReadOptionValue(args, ref index, "--title");
                    break;
                case "--workspace-id":
                    workspaceId = ReadOptionValue(args, ref index, "--workspace-id");
                    break;
                default:
                    throw new ArgumentException($"Unknown option for init: {arg}");
            }
        }

        return new InitOptions(title, workspaceId);
    }

    private static string ReadOptionValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for option: {optionName}");
        }

        index++;
        return args[index];
    }

    private sealed record InitOptions(string? Title, string? WorkspaceId);
}
