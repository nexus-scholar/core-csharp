using System.Text.Json;
using NexusScholar.UiContracts;

namespace NexusScholar.ResearchWorkspace;

public static class WorkspacePlanReader
{
    public static LoadedWorkspacePlan Read(string workingDirectory, bool requireDeduplicationResult = false)
    {
        var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
        if (location is null)
        {
            throw new WorkspacePlanReadException(
                "No Nexus research workspace found in the current folder or its parents.",
                ResearchWorkspaceExitCodes.MissingProjectOrInput);
        }

        var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
        if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
        {
            throw new WorkspacePlanReadException(
                $"Unsupported Nexus project schema: {project.Schema}",
                ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat);
        }

        var planPath = ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.CurrentWorkspacePlan);
        if (!File.Exists(planPath))
        {
            throw new WorkspacePlanReadException(
                $"Generated workspace plan not found: {ResearchWorkspacePaths.CurrentWorkspacePlan}",
                ResearchWorkspaceExitCodes.MissingProjectOrInput);
        }

        if (requireDeduplicationResult)
        {
            var deduplicationResultPath = ResearchWorkspacePaths.InProject(location.RootDirectory, ResearchWorkspacePaths.CurrentDeduplicationResult);
            if (!File.Exists(deduplicationResultPath))
            {
                throw new WorkspacePlanReadException(
                    $"Generated deduplication result not found: {ResearchWorkspacePaths.CurrentDeduplicationResult}",
                    ResearchWorkspaceExitCodes.MissingProjectOrInput);
            }
        }

        var plan = JsonSerializer.Deserialize<WorkspacePlan>(
            File.ReadAllText(planPath),
            UiContractJson.SerializerOptions);
        if (plan is null)
        {
            throw new JsonException("Workspace plan file did not contain an object.");
        }

        return new LoadedWorkspacePlan(location, project, plan);
    }
}

public sealed record LoadedWorkspacePlan(
    ResearchWorkspaceLocation Location,
    ResearchWorkspaceProject Project,
    WorkspacePlan Plan);

public sealed class WorkspacePlanReadException : Exception
{
    public WorkspacePlanReadException(string message, int exitCode)
        : base(message)
    {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
