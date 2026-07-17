using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Desktop.AppServices;

public sealed partial class DesktopWorkspaceCommandFacade
{
    private static readonly string[] ScreeningAuthorityNonClaims =
    {
        "readiness-only",
        "not-screening-decision",
        "not-protocol-authoring",
        "not-criteria-authoring",
        "not-workflow-completion"
    };

    public DesktopScreeningAuthorityReadiness InspectScreeningAuthority(string workspaceDirectory)
    {
        var readiness = ResearchWorkspaceScreeningAuthorityPackage.Inspect(RequiredPath(workspaceDirectory));
        return new DesktopScreeningAuthorityReadiness(
            MapScreeningAuthorityStatus(readiness.Status),
            readiness.Category,
            readiness.Message,
            readiness.WorkspaceId,
            readiness.ProjectRevision,
            readiness.GenerationId,
            readiness.ProtocolVersionId,
            readiness.ProtocolContentDigest,
            readiness.CriteriaId,
            readiness.CriteriaDigest,
            readiness.SourceSnapshotId,
            readiness.SourceSnapshotDigest,
            readiness.WorkflowGoverned,
            ScreeningAuthorityNonClaims);
    }

    private static DesktopWorkspaceCommandStatus MapScreeningAuthorityStatus(ResearchWorkspaceOperationStatus status) =>
        status switch
        {
            ResearchWorkspaceOperationStatus.Succeeded => DesktopWorkspaceCommandStatus.Succeeded,
            ResearchWorkspaceOperationStatus.Stale => DesktopWorkspaceCommandStatus.Stale,
            ResearchWorkspaceOperationStatus.RecoveryRequired => DesktopWorkspaceCommandStatus.RecoveryRequired,
            _ => DesktopWorkspaceCommandStatus.Failed
        };
}
