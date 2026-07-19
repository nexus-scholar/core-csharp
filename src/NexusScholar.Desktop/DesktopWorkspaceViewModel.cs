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

    public DesktopDeduplicationReviewQueue? ReviewQueue { get; private set; }

    public DesktopDeduplicationReviewPreview? PendingReviewPreview { get; private set; }

    public DesktopScreeningReviewQueue? ScreeningQueue { get; private set; }

    public DesktopScreeningReviewPreview? PendingScreeningPreview { get; private set; }

    public DesktopScreeningResolutionPreview? PendingScreeningResolutionPreview { get; private set; }

    public DesktopScreeningHandoffPreview? PendingScreeningHandoffPreview { get; private set; }
    public DesktopFullTextIntakePreview? PendingFullTextIntakePreview { get; private set; }
    public DesktopFullTextReviewPreview? PendingFullTextReviewPreview { get; private set; }
    public DesktopReportingWorkflowPreview? PendingReportingWorkflowPreview { get; private set; }
    public DesktopReviewExportPreview? PendingReviewExportPreview { get; private set; }
    public DesktopWorkspaceRecoveryPreview? PendingRecoveryPreview { get; private set; }

    public string Status { get; private set; }

    public DesktopWorkspaceCommandStatus StatusKind { get; private set; } = DesktopWorkspaceCommandStatus.Ready;

    public bool HasWorkspace => Overview is not null;

    public bool HasPendingConfirmation => PendingPreview is not null ||
        PendingReviewPreview is not null || PendingScreeningPreview is not null ||
        PendingScreeningResolutionPreview is not null ||
        PendingScreeningHandoffPreview is not null ||
        PendingFullTextIntakePreview is not null ||
        PendingFullTextReviewPreview is not null ||
        PendingReportingWorkflowPreview is not null ||
        PendingReviewExportPreview is not null ||
        PendingRecoveryPreview is not null;

    public IReadOnlyList<string> PendingEffects =>
        PendingRecoveryPreview?.ExpectedEffects ??
        PendingReviewExportPreview?.ExpectedEffects ??
        PendingReportingWorkflowPreview?.ExpectedEffects ??
        PendingFullTextReviewPreview?.ExpectedEffects ??
        PendingFullTextIntakePreview?.ExpectedEffects ??
        PendingScreeningHandoffPreview?.ExpectedEffects ??
        PendingScreeningResolutionPreview?.ExpectedEffects ??
        PendingScreeningPreview?.ExpectedEffects ??
        PendingReviewPreview?.ExpectedEffects ??
        PendingPreview?.ExpectedEffects ?? Array.Empty<string>();

    public string? PendingConfirmationToken =>
        PendingRecoveryPreview?.ConfirmationToken ??
        PendingReviewExportPreview?.ConfirmationToken ??
        PendingReportingWorkflowPreview?.ConfirmationToken ??
        PendingFullTextReviewPreview?.ConfirmationToken ??
        PendingFullTextIntakePreview?.ConfirmationToken ??
        PendingScreeningHandoffPreview?.ConfirmationToken ??
        PendingScreeningResolutionPreview?.ConfirmationToken ??
        PendingScreeningPreview?.ConfirmationToken ??
        PendingReviewPreview?.ConfirmationToken ??
        PendingPreview?.ConfirmationToken;

    public string PendingCommandLabel => PendingRecoveryPreview?.OperationKind switch
    {
        DesktopWorkspaceRecoveryKinds.Backup => "Create verified workspace backup",
        DesktopWorkspaceRecoveryKinds.Restore => "Restore verified workspace backup",
        _ => PendingReviewExportPreview is not null
        ? "Publish verified report and Bundle v2 export"
        : PendingReportingWorkflowPreview is not null
        ? "Publish reporting Workflow authority"
        : PendingFullTextReviewPreview is not null
        ? "Record human Full Text decision"
        : PendingFullTextIntakePreview is not null
        ? "Import local Full Text evidence"
        : PendingScreeningHandoffPreview is not null
        ? "Publish title/abstract Screening handoff"
        : PendingScreeningResolutionPreview is not null
        ? "Record human Screening correction or adjudication"
        : PendingScreeningPreview is not null
        ? "Record human title/abstract decision"
        : PendingReviewPreview is not null
        ? "Record human deduplication review"
        : PendingPreview?.CommandKind switch
        {
            DesktopWorkspaceCommandKinds.Initialize => "Initialize local workspace",
            DesktopWorkspaceCommandKinds.ImportSearch => "Import local Search export",
            DesktopWorkspaceCommandKinds.Analyze => "Analyze imported evidence",
            _ => "No command pending"
        }
    };

    public void Open(string path)
    {
        CancelPending();
        var result = _facade.OpenWorkspace(path);
        Apply(result);
        if (result.Completed)
        {
            WorkspacePath = Path.GetFullPath(path);
            RefreshReviewQueue(applyStatus: false);
            RefreshScreeningQueue(applyStatus: false);
        }
        else
        {
            WorkspacePath = string.Empty;
            Overview = null;
            ReviewQueue = null;
            ScreeningQueue = null;
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

    internal void ApplyStartupDiagnosticNotice(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        Status = $"The previous desktop session ended unexpectedly. A sanitized local report is available at {reportPath}.";
        StatusKind = DesktopWorkspaceCommandStatus.Attention;
    }

    public void PreviewDeduplicationReview(
        string targetId,
        string action,
        string reason,
        string? rationale,
        string actorId,
        string actorRole,
        string? supersedesDecisionId,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            ApplyFailure("Open a workspace before recording a review decision.");
            return;
        }

        PendingPreview = null;
        PendingRecoveryPreview = null;
        var result = _facade.PreviewDeduplicationReview(new DesktopDeduplicationReviewRequest(
            WorkspacePath,
            targetId,
            action,
            reason,
            rationale,
            actorId,
            actorRole,
            supersedesDecisionId,
            occurredAt));
        PendingReviewPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewScreeningReview(
        string candidateId,
        string verdict,
        string actorId,
        string actorRole,
        string rationale,
        string? exclusionReasonCode,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            ApplyFailure("Open a workspace before recording a Screening decision.");
            return;
        }

        PendingPreview = null;
        PendingReviewPreview = null;
        PendingRecoveryPreview = null;
        var result = _facade.PreviewScreeningReview(new DesktopScreeningReviewRequest(
            WorkspacePath, candidateId, "review", verdict, actorId, "human", actorRole,
            rationale, exclusionReasonCode, occurredAt));
        PendingScreeningPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewScreeningResolution(
        string candidateId,
        string decisionKind,
        string verdict,
        string actorId,
        string actorRole,
        string rationale,
        string? exclusionReasonCode,
        string? supersedesDecisionDigest,
        string? resolvedConflictId,
        IReadOnlyList<string> sourceDecisionDigests,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            ApplyFailure("Open a workspace before recording a Screening resolution.");
            return;
        }

        CancelPending();
        var result = _facade.PreviewScreeningResolution(
            new DesktopScreeningResolutionRequest(
                WorkspacePath, candidateId, decisionKind, verdict, actorId, "human",
                actorRole, rationale, exclusionReasonCode, supersedesDecisionDigest,
                resolvedConflictId, sourceDecisionDigests, occurredAt));
        PendingScreeningResolutionPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewScreeningHandoff(
        string actorId,
        string actorRole,
        string rationale,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            ApplyFailure("Open a workspace before publishing a Screening handoff.");
            return;
        }

        CancelPending();
        var result = _facade.PreviewScreeningHandoff(
            new DesktopScreeningHandoffRequest(
                WorkspacePath, actorId, "human", actorRole, rationale, occurredAt));
        PendingScreeningHandoffPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewFullTextIntake(
        string candidateId, string localPath, string actorId, DateTimeOffset occurredAt)
    {
        CancelPending();
        var result = _facade.PreviewFullTextIntake(new DesktopFullTextIntakeRequest(
            WorkspacePath, candidateId, localPath, "text", "text/plain",
            actorId, "human", occurredAt, 50L * 1024 * 1024));
        PendingFullTextIntakePreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewFullTextReview(
        string candidateId, string verdict, string actorId, string actorRole,
        string rationale, string inclusionCriteria, string exclusionCriteria,
        string exclusionReasonCode, string? selectedReason, DateTimeOffset occurredAt)
    {
        CancelPending();
        var result = _facade.PreviewFullTextReview(new DesktopFullTextReviewRequest(
            WorkspacePath, verdict, actorId, "human", actorRole, rationale,
            inclusionCriteria, exclusionCriteria, exclusionReasonCode,
            selectedReason, occurredAt, candidateId));
        PendingFullTextReviewPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewReportingWorkflow()
    {
        CancelPending();
        var result = _facade.PreviewReportingWorkflow(WorkspacePath);
        PendingReportingWorkflowPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewReviewExport(
        string exportId, string actorId, string actorRole, DateTimeOffset occurredAt)
    {
        CancelPending();
        var result = _facade.PreviewReviewExport(new DesktopReviewExportRequest(
            WorkspacePath, exportId, actorId, actorRole, occurredAt,
            ["Local-only review generated from verified authority records."],
            ["No PRISMA certification claim.", "No external compatibility claim."]));
        PendingReviewExportPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewBackup(string destinationArchivePath, DateTimeOffset occurredAt)
    {
        CancelPending();
        if (string.IsNullOrWhiteSpace(WorkspacePath))
        {
            ApplyFailure("Open a workspace before creating a backup.");
            return;
        }

        var result = _facade.PreviewBackup(new DesktopWorkspaceBackupRequest(
            WorkspacePath,
            destinationArchivePath,
            occurredAt));
        PendingRecoveryPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void PreviewRestore(
        string backupArchivePath,
        string targetWorkspaceDirectory,
        DateTimeOffset occurredAt)
    {
        CancelPending();
        var result = _facade.PreviewRestore(new DesktopWorkspaceRestoreRequest(
            backupArchivePath,
            targetWorkspaceDirectory,
            occurredAt));
        PendingRecoveryPreview = result.Preview;
        Status = result.Message;
        StatusKind = result.Status;
    }

    public void ConfirmPending()
    {
        var recoveryPreview = PendingRecoveryPreview;
        PendingRecoveryPreview = null;
        if (recoveryPreview is not null)
        {
            var result = recoveryPreview.OperationKind switch
            {
                DesktopWorkspaceRecoveryKinds.Backup => _facade.ExecuteBackup(recoveryPreview),
                DesktopWorkspaceRecoveryKinds.Restore => _facade.ExecuteRestore(recoveryPreview),
                _ => new DesktopWorkspaceRecoveryResult(
                    DesktopWorkspaceCommandStatus.Failed,
                    "The pending recovery operation is not admitted.")
            };
            Status = result.Message;
            StatusKind = result.Status;
            if (result.Overview is not null)
            {
                Overview = result.Overview;
            }
            if (result.Completed && recoveryPreview.OperationKind == DesktopWorkspaceRecoveryKinds.Restore &&
                result.WorkspaceDirectory is not null)
            {
                WorkspacePath = result.WorkspaceDirectory;
                RefreshReviewQueue(applyStatus: false);
                RefreshScreeningQueue(applyStatus: false);
            }
            return;
        }

        var exportPreview = PendingReviewExportPreview;
        PendingReviewExportPreview = null;
        PendingRecoveryPreview = null;
        if (exportPreview is not null)
        {
            var result = _facade.ExecuteReviewExport(exportPreview);
            Status = result.Message; StatusKind = result.Status;
            if (result.Overview is not null) Overview = result.Overview;
            return;
        }

        var reportingPreview = PendingReportingWorkflowPreview;
        PendingReportingWorkflowPreview = null;
        if (reportingPreview is not null)
        {
            var result = _facade.ExecuteReportingWorkflow(reportingPreview);
            Status = result.Message; StatusKind = result.Status;
            if (result.Overview is not null) Overview = result.Overview;
            return;
        }

        var fullTextReview = PendingFullTextReviewPreview;
        PendingFullTextReviewPreview = null;
        if (fullTextReview is not null)
        {
            var result = _facade.ExecuteFullTextReview(fullTextReview);
            Status = result.Message; StatusKind = result.Status;
            if (result.Overview is not null) Overview = result.Overview;
            return;
        }

        var fullTextIntake = PendingFullTextIntakePreview;
        PendingFullTextIntakePreview = null;
        if (fullTextIntake is not null)
        {
            var result = _facade.ExecuteFullTextIntake(fullTextIntake);
            Status = result.Message; StatusKind = result.Status;
            if (result.Overview is not null) Overview = result.Overview;
            return;
        }

        var handoffPreview = PendingScreeningHandoffPreview;
        PendingScreeningHandoffPreview = null;
        PendingFullTextIntakePreview = null;
        PendingFullTextReviewPreview = null;
        PendingReportingWorkflowPreview = null;
        PendingReviewExportPreview = null;
        PendingRecoveryPreview = null;
        if (handoffPreview is not null)
        {
            var handoffResult = _facade.ExecuteScreeningHandoff(handoffPreview);
            Status = handoffResult.Message;
            StatusKind = handoffResult.Status;
            if (handoffResult.Overview is not null)
            {
                Overview = handoffResult.Overview;
            }
            RefreshScreeningQueue(applyStatus: false);
            return;
        }

        var resolutionPreview = PendingScreeningResolutionPreview;
        PendingScreeningResolutionPreview = null;
        if (resolutionPreview is not null)
        {
            var resolutionResult = _facade.ExecuteScreeningResolution(resolutionPreview);
            Status = resolutionResult.Message;
            StatusKind = resolutionResult.Status;
            if (resolutionResult.Overview is not null)
            {
                Overview = resolutionResult.Overview;
            }
            RefreshScreeningQueue(applyStatus: false);
            return;
        }

        var screeningPreview = PendingScreeningPreview;
        PendingScreeningPreview = null;
        if (screeningPreview is not null)
        {
            var screeningResult = _facade.ExecuteScreeningReview(screeningPreview);
            Status = screeningResult.Message;
            StatusKind = screeningResult.Status;
            if (screeningResult.Overview is not null)
            {
                Overview = screeningResult.Overview;
            }
            if (screeningResult.Queue is not null)
            {
                ScreeningQueue = screeningResult.Queue;
            }
            return;
        }

        var reviewPreview = PendingReviewPreview;
        PendingReviewPreview = null;
        if (reviewPreview is not null)
        {
            var reviewResult = _facade.ExecuteDeduplicationReview(reviewPreview);
            Status = reviewResult.Message;
            StatusKind = reviewResult.Status;
            if (reviewResult.Overview is not null)
            {
                Overview = reviewResult.Overview;
            }
            if (reviewResult.Queue is not null)
            {
                ReviewQueue = reviewResult.Queue;
            }
            return;
        }

        var preview = PendingPreview;
        PendingPreview = null;
        if (preview is null)
        {
            ApplyFailure("No exact command preview is pending confirmation.");
            return;
        }

        var commandResult = preview.CommandKind switch
        {
            DesktopWorkspaceCommandKinds.Initialize => _facade.ExecuteInitialize(preview),
            DesktopWorkspaceCommandKinds.ImportSearch => _facade.ExecuteImportSearch(preview),
            DesktopWorkspaceCommandKinds.Analyze => _facade.ExecuteAnalyze(preview),
            _ => new DesktopWorkspaceCommandResult(
                DesktopWorkspaceCommandStatus.Failed,
                "The pending command kind is not admitted by this product slice.")
        };
        Apply(commandResult);
        if (commandResult.Completed && preview.CommandKind == DesktopWorkspaceCommandKinds.Initialize)
        {
            WorkspacePath = preview.WorkspaceDirectory;
        }
    }

    public void CancelPending()
    {
        PendingPreview = null;
        PendingReviewPreview = null;
        PendingScreeningPreview = null;
        PendingScreeningResolutionPreview = null;
        PendingScreeningHandoffPreview = null;
        PendingFullTextIntakePreview = null;
        PendingFullTextReviewPreview = null;
        PendingReportingWorkflowPreview = null;
        PendingReviewExportPreview = null;
        PendingRecoveryPreview = null;
    }

    private void ApplyPreview(DesktopWorkspacePreviewResult result)
    {
        PendingReviewPreview = null;
        PendingScreeningPreview = null;
        PendingScreeningResolutionPreview = null;
        PendingScreeningHandoffPreview = null;
        PendingFullTextIntakePreview = null;
        PendingFullTextReviewPreview = null;
        PendingReportingWorkflowPreview = null;
        PendingReviewExportPreview = null;
        PendingRecoveryPreview = null;
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
        PendingReviewPreview = null;
        PendingScreeningPreview = null;
        PendingScreeningResolutionPreview = null;
        PendingScreeningHandoffPreview = null;
        PendingFullTextIntakePreview = null;
        PendingFullTextReviewPreview = null;
        PendingReportingWorkflowPreview = null;
        PendingReviewExportPreview = null;
        PendingRecoveryPreview = null;
        Status = message;
        StatusKind = DesktopWorkspaceCommandStatus.Failed;
    }

    private void RefreshReviewQueue(bool applyStatus)
    {
        var result = _facade.LoadDeduplicationReviewQueue(WorkspacePath);
        ReviewQueue = result.Queue;
        if (applyStatus)
        {
            Status = result.Message;
            StatusKind = result.Status;
        }
    }

    private void RefreshScreeningQueue(bool applyStatus)
    {
        var result = _facade.LoadScreeningReviewQueue(WorkspacePath);
        ScreeningQueue = result.Queue;
        if (applyStatus)
        {
            Status = result.Message;
            StatusKind = result.Status;
        }
    }
}
