using NexusScholar.Kernel;

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
    public const string AuthorityGenerations = "nexus-output/authority-generations";
    public const string ScreeningGenerations = "nexus-output/screening-generations";
    public const string WorkflowExecutionJournals = "nexus-output/workflow-executions";
    public const string ScreeningConducts = "nexus-output/screening-conducts";
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

    public static string AuthorityGenerationRoot(string generationId) => $"{AuthorityGenerations}/{generationId}";
    public static string ScreeningConductGenerationRoot(string generationId) => $"{ScreeningGenerations}/{generationId}";

    public static string WorkflowExecutionJournalRoot(string executionId, string generationId) =>
        $"{WorkflowExecutionJournals}/execution-{ContentDigest.Sha256Utf8(executionId).Value[..24]}/{generationId}";

    public static string ScreeningConductRoot(string conductId, string generationId) =>
        $"{ScreeningConducts}/conduct-{ContentDigest.Sha256Utf8(conductId).Value[..24]}/{generationId}";
}
