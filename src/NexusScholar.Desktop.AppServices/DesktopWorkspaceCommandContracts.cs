namespace NexusScholar.Desktop.AppServices;

public static class DesktopWorkspaceCommandKinds
{
    public const string Initialize = "initialize-workspace";
    public const string ImportSearch = "import-search-export";
    public const string Analyze = "analyze-workspace";
}

public enum DesktopWorkspaceCommandStatus
{
    Ready,
    Succeeded,
    Attention,
    Failed,
    Stale,
    RecoveryRequired
}

public sealed record DesktopInitializeRequest(
    string TargetDirectory,
    string Title,
    string? WorkspaceId,
    DateTimeOffset OccurredAt);

public sealed record DesktopImportSearchRequest(
    string WorkspaceDirectory,
    string SourcePath,
    string Source,
    string Format,
    string? InputId,
    string? Query,
    DateTimeOffset OccurredAt);

public sealed record DesktopWorkspaceCommandPreview(
    string CommandKind,
    string WorkspaceDirectory,
    string? WorkspaceId,
    long? ExpectedProjectRevision,
    string? SourcePath,
    string? SourceDigest,
    string? Title,
    string? RequestedWorkspaceId,
    string? Source,
    string? Format,
    string? InputId,
    string? Query,
    DateTimeOffset OccurredAt,
    IReadOnlyList<string> ExpectedEffects,
    IReadOnlyList<string> NonClaims,
    string ConfirmationToken);

public sealed record DesktopWorkspacePreviewResult(
    DesktopWorkspaceCommandStatus Status,
    string Message,
    DesktopWorkspaceCommandPreview? Preview)
{
    public bool IsReady => Status == DesktopWorkspaceCommandStatus.Ready && Preview is not null;
}

public sealed record DesktopWorkspaceCommandResult(
    DesktopWorkspaceCommandStatus Status,
    string Message,
    DesktopWorkspaceOverview? Overview = null)
{
    public bool Completed => Status is DesktopWorkspaceCommandStatus.Succeeded or DesktopWorkspaceCommandStatus.Attention;
}

public sealed record DesktopWorkspaceOverview(
    string State,
    string? ProjectTitle,
    string? WorkspaceId,
    string ProjectLocation,
    int InputCount,
    int ImportedRecordCount,
    int ParserWarningCount,
    int SkippedRecordCount,
    int ReviewRequiredCount,
    int AttentionCount,
    IReadOnlyList<DesktopWorkspaceImportSummary> Imports,
    IReadOnlyList<DesktopWorkspaceAttention> AttentionItems,
    IReadOnlyList<string> NonClaims);

public sealed record DesktopWorkspaceImportSummary(
    string ImportId,
    string Source,
    string Format,
    int RecordCount,
    int ParserWarningCount,
    int SkippedRecordCount);

public sealed record DesktopWorkspaceAttention(
    string Code,
    string Severity,
    string Message,
    string? Target);
