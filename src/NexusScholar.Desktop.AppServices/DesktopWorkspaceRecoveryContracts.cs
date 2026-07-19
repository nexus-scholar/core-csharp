namespace NexusScholar.Desktop.AppServices;

public static class DesktopWorkspaceRecoveryKinds
{
    public const string Backup = "backup-workspace";
    public const string Restore = "restore-workspace";
}

public sealed record DesktopWorkspaceBackupRequest(
    string WorkspaceDirectory,
    string DestinationArchivePath,
    DateTimeOffset OccurredAt);

public sealed record DesktopWorkspaceRestoreRequest(
    string BackupArchivePath,
    string TargetWorkspaceDirectory,
    DateTimeOffset OccurredAt);

public sealed record DesktopWorkspaceRecoveryPreview(
    string OperationKind,
    string WorkspaceDirectory,
    string? WorkspaceId,
    long? ExpectedProjectRevision,
    string? BackupArchivePath,
    long? ExpectedArchiveLength,
    string? ExpectedArchiveDigest,
    string TargetPath,
    DateTimeOffset OccurredAt,
    IReadOnlyList<string> ExpectedEffects,
    IReadOnlyList<string> NonClaims,
    string ConfirmationToken);

public sealed record DesktopWorkspaceRecoveryPreviewResult(
    DesktopWorkspaceCommandStatus Status,
    string Message,
    DesktopWorkspaceRecoveryPreview? Preview)
{
    public bool IsReady =>
        Status == DesktopWorkspaceCommandStatus.Ready && Preview is not null;
}

public sealed record DesktopWorkspaceRecoveryResult(
    DesktopWorkspaceCommandStatus Status,
    string Message,
    string? WorkspaceDirectory = null,
    string? ArchivePath = null,
    string? ManifestDigest = null,
    DesktopWorkspaceOverview? Overview = null)
{
    public bool Completed =>
        Status is DesktopWorkspaceCommandStatus.Succeeded or DesktopWorkspaceCommandStatus.Attention;
}
