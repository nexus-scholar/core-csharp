using NexusScholar.Desktop.AppServices;

namespace NexusScholar.Desktop;

public sealed class DesktopWorkspaceViewModel
{
    private readonly DesktopWorkspaceCommandFacade _facade;

    public DesktopWorkspaceViewModel(DesktopWorkspaceCommandFacade facade)
    {
        _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        Status = "Open an existing workspace or initialize a new local folder.";
    }

    public string WorkspacePath { get; private set; } = string.Empty;

    public DesktopWorkspaceOverview? Overview { get; private set; }

    public DesktopWorkspaceCommandPreview? PendingPreview { get; private set; }

    public string Status { get; private set; }

    public DesktopWorkspaceCommandStatus StatusKind { get; private set; } = DesktopWorkspaceCommandStatus.Ready;

    public bool HasWorkspace => Overview is not null;

    public bool HasPendingConfirmation => PendingPreview is not null;

    public IReadOnlyList<string> PendingEffects => PendingPreview?.ExpectedEffects ?? Array.Empty<string>();

    public string PendingCommandLabel => PendingPreview?.CommandKind switch
    {
        DesktopWorkspaceCommandKinds.Initialize => "Initialize local workspace",
        DesktopWorkspaceCommandKinds.ImportSearch => "Import local Search export",
        DesktopWorkspaceCommandKinds.Analyze => "Analyze imported evidence",
        _ => "No command pending"
    };

    public void Open(string path)
    {
        CancelPending();
        var result = _facade.OpenWorkspace(path);
        Apply(result);
        if (result.Completed)
        {
            WorkspacePath = Path.GetFullPath(path);
        }
        else
        {
            WorkspacePath = string.Empty;
            Overview = null;
        }
    }

    public void PreviewInitialize(string path, string title, string? workspaceId, DateTimeOffset occurredAt)
    {
        var result = _facade.PreviewInitialize(new DesktopInitializeRequest(path, title, workspaceId, occurredAt));
        ApplyPreview(result);
    }

    public void PreviewImport(
        string sourcePath,
        string source,
        string format,
        string? inputId,
        string? query,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            ApplyFailure("Open a workspace before importing evidence.");
            return;
        }

        var result = _facade.PreviewImportSearch(new DesktopImportSearchRequest(
            WorkspacePath, sourcePath, source, format, inputId, query, occurredAt));
        ApplyPreview(result);
    }

    public void PreviewAnalyze(DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            ApplyFailure("Open a workspace before analysis.");
            return;
        }

        ApplyPreview(_facade.PreviewAnalyze(WorkspacePath, occurredAt));
    }

    public void Verify()
    {
        CancelPending();
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            ApplyFailure("Open a workspace before verification.");
            return;
        }

        Apply(_facade.VerifyWorkspace(WorkspacePath));
    }

    public void ConfirmPending()
    {
        var preview = PendingPreview;
        PendingPreview = null;
        if (preview is null)
        {
            ApplyFailure("No exact command preview is pending confirmation.");
            return;
        }

        var result = preview.CommandKind switch
        {
            DesktopWorkspaceCommandKinds.Initialize => _facade.ExecuteInitialize(preview),
            DesktopWorkspaceCommandKinds.ImportSearch => _facade.ExecuteImportSearch(preview),
            DesktopWorkspaceCommandKinds.Analyze => _facade.ExecuteAnalyze(preview),
            _ => new DesktopWorkspaceCommandResult(
                DesktopWorkspaceCommandStatus.Failed,
                "The pending command kind is not admitted by this product slice.")
        };
        Apply(result);
        if (result.Completed && preview.CommandKind == DesktopWorkspaceCommandKinds.Initialize)
        {
            WorkspacePath = preview.WorkspaceDirectory;
        }
    }

    public void CancelPending()
    {
        PendingPreview = null;
    }

    private void ApplyPreview(DesktopWorkspacePreviewResult result)
    {
        PendingPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    private void Apply(DesktopWorkspaceCommandResult result)
    {
        Status = result.Message;
        StatusKind = result.Status;
        if (result.Overview is not null)
        {
            Overview = result.Overview;
        }
    }

    private void ApplyFailure(string message)
    {
        PendingPreview = null;
        Status = message;
        StatusKind = DesktopWorkspaceCommandStatus.Failed;
    }
}
