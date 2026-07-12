namespace NexusScholar.ResearchWorkspace;

public sealed record ResearchWorkspaceGenerationManifest(
    string Schema,
    string GenerationId,
    string WorkspaceId,
    long ProjectRevision,
    IReadOnlyList<ResearchWorkspaceGenerationArtifact> Inputs,
    IReadOnlyList<ResearchWorkspaceGenerationArtifact> ImportTraces,
    IReadOnlyList<ResearchWorkspaceGenerationArtifact> Outputs)
{
    public const string CurrentSchema = "nexus.workspace-generation.v1";
}

public sealed record ResearchWorkspaceGenerationArtifact(string Name, string RelativePath, string Sha256);

public sealed record ResearchWorkspaceAnalysisCommit(
    ResearchWorkspaceAnalysisResult Analysis,
    ResearchWorkspaceProject Project,
    ResearchWorkspaceGenerationManifest Manifest);
