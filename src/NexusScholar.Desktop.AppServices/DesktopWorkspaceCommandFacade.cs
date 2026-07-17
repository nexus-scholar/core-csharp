using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Desktop.AppServices;

public sealed partial class DesktopWorkspaceCommandFacade
{
    private static readonly string[] OperationalNonClaims =
    {
        "not-scientific-authority",
        "no-scientific-decision",
        "no-live-provider",
        "no-network",
        "no-ai"
    };

    public DesktopWorkspaceCommandResult OpenWorkspace(string workspaceDirectory)
    {
        try
        {
            var overview = ResearchWorkspaceReadModelBuilder.Build(RequiredPath(workspaceDirectory));
            return overview.State == WorkspaceState.Missing
                ? Failed("No Nexus research workspace was found in the selected folder.")
                : Succeeded($"Opened {overview.ProjectTitle}.", Project(overview));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return Failed(SafeFailure("The workspace could not be opened", exception));
        }
        catch (InvalidOperationException)
        {
            return Recovery("The workspace authority or generated evidence failed verification.");
        }
    }

    public DesktopWorkspaceCommandResult VerifyWorkspace(string workspaceDirectory)
    {
        var path = RequiredPath(workspaceDirectory);
        var action = ResearchWorkspaceWorkflowActions.Verify(path);
        var overview = SafeBuild(path);
        var status = !action.Completed
            ? action.ExitCode == ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure
                ? DesktopWorkspaceCommandStatus.RecoveryRequired
                : DesktopWorkspaceCommandStatus.Failed
            : action.RequiresAttention || overview is { ParserWarningCount: > 0 } || overview is { SkippedRecordCount: > 0 }
                ? DesktopWorkspaceCommandStatus.Attention
                : DesktopWorkspaceCommandStatus.Succeeded;
        var message = status == DesktopWorkspaceCommandStatus.Attention && !action.RequiresAttention
            ? $"Workspace verification needs attention. Parser warnings: {overview?.ParserWarningCount ?? 0}; skipped records: {overview?.SkippedRecordCount ?? 0}."
            : action.Message;
        return new DesktopWorkspaceCommandResult(status, message, overview);
    }

    public DesktopWorkspacePreviewResult PreviewInitialize(DesktopInitializeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var target = RequiredPath(request.TargetDirectory);
            var title = Required(request.Title, nameof(request.Title));
            if (File.Exists(ResearchWorkspacePaths.ProjectFile(target)))
            {
                return PreviewFailure("A Nexus research workspace already exists in the selected folder.");
            }

            return Ready(CreatePreview(
                DesktopWorkspaceCommandKinds.Initialize,
                target,
                null,
                null,
                null,
                null,
                title,
                NormalizeOptional(request.WorkspaceId),
                null,
                null,
                null,
                null,
                request.OccurredAt,
                new[] { "create nexus.project.json", "create required local workspace directories" }));
        }
        catch (ArgumentException exception)
        {
            return PreviewFailure(exception.Message);
        }
    }

    public DesktopWorkspaceCommandResult ExecuteInitialize(DesktopWorkspaceCommandPreview preview)
    {
        if (!ValidatePreview(preview, DesktopWorkspaceCommandKinds.Initialize, out var failure))
        {
            return failure!;
        }

        if (File.Exists(ResearchWorkspacePaths.ProjectFile(preview.WorkspaceDirectory)))
        {
            return Stale("stale-initialize-target: a workspace now exists in the selected folder.");
        }

        var result = ResearchWorkspaceLocalOperations.Initialize(new ResearchWorkspaceInitializeRequest(
            preview.WorkspaceDirectory,
            preview.Title!,
            preview.RequestedWorkspaceId,
            preview.OccurredAt));
        return FromInitialize(result, preview.WorkspaceDirectory);
    }

    public DesktopWorkspacePreviewResult PreviewImportSearch(DesktopImportSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var workspacePath = RequiredPath(request.WorkspaceDirectory);
            var location = ResearchWorkspaceStore.FindFrom(workspacePath);
            if (location is null)
            {
                return PreviewFailure("No Nexus research workspace was found in the selected folder.");
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            var sourcePath = Path.GetFullPath(Required(request.SourcePath, nameof(request.SourcePath)), workspacePath);
            if (!File.Exists(sourcePath))
            {
                return PreviewFailure("The selected Search export does not exist.");
            }

            var source = SearchImportAliases.NormalizeSource(Required(request.Source, nameof(request.Source)));
            var format = SearchImportAliases.NormalizeFormat(Required(request.Format, nameof(request.Format)));
            var digest = ContentDigest.Sha256(File.ReadAllBytes(sourcePath)).ToString();
            return Ready(CreatePreview(
                DesktopWorkspaceCommandKinds.ImportSearch,
                location.RootDirectory,
                project.WorkspaceId,
                project.Revision,
                sourcePath,
                digest,
                null,
                null,
                source,
                format,
                NormalizeOptional(request.InputId),
                NormalizeOptional(request.Query),
                request.OccurredAt,
                new[] { "copy selected source bytes", "append one Search import trace", "advance project revision" }));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return PreviewFailure(SafeFailure("Import preview failed", exception));
        }
    }

    public DesktopWorkspaceCommandResult ExecuteImportSearch(DesktopWorkspaceCommandPreview preview)
    {
        if (!ValidatePreview(preview, DesktopWorkspaceCommandKinds.ImportSearch, out var failure))
        {
            return failure!;
        }

        var result = ResearchWorkspaceLocalOperations.ImportSearch(new ResearchWorkspaceSearchImportRequest(
            preview.WorkspaceDirectory,
            preview.SourcePath!,
            preview.Source!,
            preview.Format!,
            preview.InputId,
            preview.Query,
            "nexus-desktop-local",
            preview.OccurredAt,
            preview.ExpectedProjectRevision,
            preview.SourceDigest,
            preview.WorkspaceId));
        return FromImport(result, preview.WorkspaceDirectory);
    }

    public DesktopWorkspacePreviewResult PreviewAnalyze(string workspaceDirectory, DateTimeOffset occurredAt)
    {
        try
        {
            var path = RequiredPath(workspaceDirectory);
            var location = ResearchWorkspaceStore.FindFrom(path);
            if (location is null)
            {
                return PreviewFailure("No Nexus research workspace was found in the selected folder.");
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (project.Inputs.Count == 0)
            {
                return PreviewFailure("Analyze requires at least one imported Search export.");
            }

            return Ready(CreatePreview(
                DesktopWorkspaceCommandKinds.Analyze,
                location.RootDirectory,
                project.WorkspaceId,
                project.Revision,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                occurredAt,
                new[] { "publish one local analysis generation", "refresh deduplication, workspace plan, and report outputs" }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return PreviewFailure(SafeFailure("Analysis preview failed", exception));
        }
    }

    public DesktopWorkspaceCommandResult ExecuteAnalyze(DesktopWorkspaceCommandPreview preview)
    {
        if (!ValidatePreview(preview, DesktopWorkspaceCommandKinds.Analyze, out var failure))
        {
            return failure!;
        }

        try
        {
            var location = ResearchWorkspaceStore.FindFrom(preview.WorkspaceDirectory);
            if (location is null)
            {
                return Stale("stale-workspace: the workspace no longer exists.");
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (project.Revision != preview.ExpectedProjectRevision ||
                !string.Equals(project.WorkspaceId, preview.WorkspaceId, StringComparison.Ordinal))
            {
                return Stale($"stale-workspace-revision: expected revision {preview.ExpectedProjectRevision}, but found {project.Revision}.");
            }

            var commit = ResearchWorkspaceTransaction.AnalyzeAndCommit(location, project);
            return Succeeded(
                $"Workspace analysis complete. Generation: {commit.Manifest.GenerationId}.",
                Project(ResearchWorkspaceReadModelBuilder.Build(location.RootDirectory)));
        }
        catch (ResearchWorkspaceConcurrencyException exception)
        {
            return exception.InnerException is IOException
                ? Recovery(exception.Message)
                : Stale(exception.Message);
        }
        catch (ResearchWorkspaceAuthorityGenerationActiveException exception)
        {
            return Recovery(exception.Message);
        }
        catch (ResearchWorkspaceMissingInputException exception)
        {
            return Failed(exception.Message);
        }
        catch (ResearchWorkspaceDigestMismatchException exception)
        {
            return new DesktopWorkspaceCommandResult(DesktopWorkspaceCommandStatus.Attention, exception.Message, SafeBuild(preview.WorkspaceDirectory));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return Recovery(SafeFailure("Workspace analysis could not be committed", exception));
        }
    }

    private static DesktopWorkspaceCommandPreview CreatePreview(
        string commandKind,
        string workspaceDirectory,
        string? workspaceId,
        long? expectedRevision,
        string? sourcePath,
        string? sourceDigest,
        string? title,
        string? requestedWorkspaceId,
        string? source,
        string? format,
        string? inputId,
        string? query,
        DateTimeOffset occurredAt,
        IReadOnlyList<string> effects)
    {
        var token = Token(commandKind, workspaceDirectory, workspaceId, expectedRevision, sourcePath, sourceDigest,
            title, requestedWorkspaceId, source, format, inputId, query, occurredAt, effects, OperationalNonClaims);
        return new DesktopWorkspaceCommandPreview(commandKind, workspaceDirectory, workspaceId, expectedRevision,
            sourcePath, sourceDigest, title, requestedWorkspaceId, source, format, inputId, query, occurredAt,
            effects, OperationalNonClaims, token);
    }

    private static bool ValidatePreview(
        DesktopWorkspaceCommandPreview? preview,
        string expectedKind,
        out DesktopWorkspaceCommandResult? failure)
    {
        if (preview is null || !string.Equals(preview.CommandKind, expectedKind, StringComparison.Ordinal))
        {
            failure = Failed("The confirmation preview is missing or has the wrong command kind.");
            return false;
        }

        var expectedToken = Token(preview.CommandKind, preview.WorkspaceDirectory, preview.WorkspaceId,
            preview.ExpectedProjectRevision, preview.SourcePath, preview.SourceDigest, preview.Title,
            preview.RequestedWorkspaceId, preview.Source, preview.Format, preview.InputId, preview.Query,
            preview.OccurredAt, preview.ExpectedEffects, preview.NonClaims);
        if (!string.Equals(preview.ConfirmationToken, expectedToken, StringComparison.Ordinal))
        {
            failure = Stale("stale-confirmation-preview: preview material or confirmation token changed.");
            return false;
        }

        failure = null;
        return true;
    }

    private static string Token(
        string commandKind,
        string workspaceDirectory,
        string? workspaceId,
        long? expectedRevision,
        string? sourcePath,
        string? sourceDigest,
        string? title,
        string? requestedWorkspaceId,
        string? source,
        string? format,
        string? inputId,
        string? query,
        DateTimeOffset occurredAt,
        IReadOnlyList<string> effects,
        IReadOnlyList<string> nonClaims)
    {
        var material = new CanonicalJsonObject()
            .Add("schema", "nexus.desktop.command-preview")
            .Add("schema_version", "1.0.0")
            .Add("command_kind", commandKind)
            .Add("workspace_directory", workspaceDirectory)
            .Add("workspace_id", workspaceId is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(workspaceId))
            .Add("expected_project_revision", expectedRevision is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(expectedRevision.Value))
            .Add("source_path", sourcePath is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(sourcePath))
            .Add("source_digest", sourceDigest is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(sourceDigest))
            .Add("title", title is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(title))
            .Add("requested_workspace_id", requestedWorkspaceId is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(requestedWorkspaceId))
            .Add("source", source is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(source))
            .Add("format", format is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(format))
            .Add("input_id", inputId is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(inputId))
            .Add("query", query is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(query))
            .Add("occurred_at", occurredAt.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture))
            .Add("expected_effects", new CanonicalJsonArray(effects.Select(CanonicalJsonValue.From)))
            .Add("non_claims", new CanonicalJsonArray(nonClaims.Select(CanonicalJsonValue.From)));
        return ContentDigest.Sha256CanonicalJson(material).ToString();
    }

    private static DesktopWorkspaceCommandResult FromInitialize(ResearchWorkspaceInitializeResult result, string path) =>
        result.Status switch
        {
            ResearchWorkspaceOperationStatus.Succeeded => Succeeded(result.Message, SafeBuild(path)),
            ResearchWorkspaceOperationStatus.Stale => Stale(result.Message),
            ResearchWorkspaceOperationStatus.RecoveryRequired => Recovery(result.Message),
            _ => Failed(result.Message)
        };

    private static DesktopWorkspaceCommandResult FromImport(ResearchWorkspaceSearchImportResult result, string path)
    {
        if (result.Status == ResearchWorkspaceOperationStatus.Succeeded)
        {
            var overview = SafeBuild(path);
            var status = result.ParserWarningCount > 0 || result.SkippedRecordCount > 0
                ? DesktopWorkspaceCommandStatus.Attention
                : DesktopWorkspaceCommandStatus.Succeeded;
            return new DesktopWorkspaceCommandResult(status,
                $"{result.Message}. Records: {result.ImportedRecordCount}; warnings: {result.ParserWarningCount}; skipped: {result.SkippedRecordCount}.",
                overview);
        }

        return result.Status switch
        {
            ResearchWorkspaceOperationStatus.Stale => Stale(result.Message),
            ResearchWorkspaceOperationStatus.RecoveryRequired => Recovery(result.Message),
            _ => Failed(result.Message)
        };
    }

    private static DesktopWorkspaceOverview? SafeBuild(string path)
    {
        try
        {
            return Project(ResearchWorkspaceReadModelBuilder.Build(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static DesktopWorkspacePreviewResult Ready(DesktopWorkspaceCommandPreview preview) =>
        new(DesktopWorkspaceCommandStatus.Ready, "Review the exact local effects before confirmation.", preview);

    private static DesktopWorkspacePreviewResult PreviewFailure(string message) =>
        new(DesktopWorkspaceCommandStatus.Failed, message, null);

    private static DesktopWorkspaceCommandResult Succeeded(string message, DesktopWorkspaceOverview? overview = null) =>
        new(DesktopWorkspaceCommandStatus.Succeeded, message, overview);

    private static DesktopWorkspaceCommandResult Failed(string message) =>
        new(DesktopWorkspaceCommandStatus.Failed, message);

    private static DesktopWorkspaceCommandResult Stale(string message) =>
        new(DesktopWorkspaceCommandStatus.Stale, message);

    private static DesktopWorkspaceCommandResult Recovery(string message) =>
        new(DesktopWorkspaceCommandStatus.RecoveryRequired, message);

    private static string RequiredPath(string value) => Path.GetFullPath(Required(value, nameof(value)));

    private static string Required(string? value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SafeFailure(string prefix, Exception exception) =>
        exception is UnauthorizedAccessException ? $"{prefix}: access was denied." : $"{prefix}: local file operation failed.";

    private static DesktopWorkspaceOverview Project(WorkspaceOverviewReadModel overview) => new(
        overview.State.ToString(),
        overview.ProjectTitle,
        overview.WorkspaceId,
        overview.ProjectLocation,
        overview.Verification.InputCount,
        overview.Imports.Sum(item => item.ImportedRecordCount),
        overview.Verification.ParserWarningCount,
        overview.Verification.SkippedRecordCount,
        overview.Analysis.ReviewRequiredCandidateCount,
        overview.AttentionItems.Count,
        overview.Imports.Select(item => new DesktopWorkspaceImportSummary(
            item.ImportId,
            item.Source,
            item.Format,
            item.ImportedRecordCount,
            item.ParserWarningCount,
            item.SkippedRecordCount)).ToArray(),
        overview.AttentionItems.Select(item => new DesktopWorkspaceAttention(
            item.Code,
            item.Severity.ToString(),
            item.Message,
            item.Target)).ToArray(),
        overview.NonClaims);
}
