namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspacePaths
{
    public const string ProjectFileName = "nexus.project.json";
    public const string SearchInputs = "inputs/search";
    public const string ImportOutputs = "nexus-output/imports";
    public const string DedupOutputs = "nexus-output/dedup";
    public const string WorkspaceOutputs = "nexus-output/workspace";
    public const string ReportOutputs = "nexus-output/reports";
    public const string Generations = "nexus-output/generations";
    public const string GenerationStaging = "nexus-output/.staging";
    public const string GenerationQuarantine = "nexus-output/quarantine";
    public const string ProjectLockFileName = "nexus.project.lock";
    public const string CurrentDeduplicationResult = "nexus-output/dedup/current.deduplication-result.json";
    public const string CurrentWorkspacePlan = "nexus-output/workspace/current.workspace-plan.json";

    public static readonly string[] RequiredDirectories =
    {
        SearchInputs,
        ImportOutputs,
        DedupOutputs,
        WorkspaceOutputs,
        ReportOutputs
    };

    public static string ProjectFile(string workingDirectory) => Path.Combine(workingDirectory, ProjectFileName);

    public static string InProject(string workingDirectory, string relativePath)
    {
        return Path.Combine(workingDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    public static string GenerationRoot(string generationId) => $"{Generations}/{generationId}";
}
