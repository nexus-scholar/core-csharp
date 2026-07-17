namespace NexusScholar.Desktop.AppServices;

public sealed record DesktopScreeningAuthorityReadiness(
    DesktopWorkspaceCommandStatus Status,
    string Category,
    string Message,
    string? WorkspaceId,
    long? ProjectRevision,
    string? GenerationId,
    string? ProtocolVersionId,
    string? ProtocolContentDigest,
    string? CriteriaId,
    string? CriteriaDigest,
    string? SourceSnapshotId,
    string? SourceSnapshotDigest,
    bool WorkflowGoverned,
    IReadOnlyList<string> NonClaims)
{
    public bool Ready => Status == DesktopWorkspaceCommandStatus.Succeeded;
}
