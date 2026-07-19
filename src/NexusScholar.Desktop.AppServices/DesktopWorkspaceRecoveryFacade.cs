using System.Security.Cryptography;

using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Desktop.AppServices;

public sealed partial class DesktopWorkspaceCommandFacade
{
    private static readonly string[] RecoveryNonClaims =
    {
        "operational-recovery-only",
        "not-scientific-authority",
        "no-history-rewrite",
        "no-cloud-sync",
        "no-existing-workspace-merge"
    };

    public DesktopWorkspaceRecoveryPreviewResult PreviewBackup(
        DesktopWorkspaceBackupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var location = ResearchWorkspaceStore.FindFrom(RequiredPath(request.WorkspaceDirectory));
            if (location is null)
            {
                return RecoveryPreviewFailure("No Nexus research workspace was found in the selected folder.");
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            var target = RequiredPath(request.DestinationArchivePath);
            if (File.Exists(target) || Directory.Exists(target))
            {
                return RecoveryPreviewFailure("The backup destination must not already exist.");
            }

            var effects = new[]
            {
                "acquire the local workspace mutation lock",
                "verify and inventory every admitted workspace file",
                "create one manifest-verified backup archive outside the workspace"
            };
            return RecoveryReady(CreateRecoveryPreview(
                DesktopWorkspaceRecoveryKinds.Backup,
                location.RootDirectory,
                project.WorkspaceId,
                project.Revision,
                null,
                null,
                null,
                target,
                request.OccurredAt,
                effects));
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return RecoveryPreviewFailure(SafeFailure("Backup preview failed", exception));
        }
    }

    public DesktopWorkspaceRecoveryResult ExecuteBackup(
        DesktopWorkspaceRecoveryPreview preview)
    {
        if (!ValidateRecoveryPreview(preview, DesktopWorkspaceRecoveryKinds.Backup, out var failure))
        {
            return failure!;
        }

        try
        {
            var location = ResearchWorkspaceStore.FindFrom(preview.WorkspaceDirectory);
            if (location is null)
            {
                return RecoveryStale("stale-workspace: the workspace no longer exists.");
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (!string.Equals(project.WorkspaceId, preview.WorkspaceId, StringComparison.Ordinal) ||
                project.Revision != preview.ExpectedProjectRevision)
            {
                return RecoveryStale(
                    $"stale-workspace-revision: expected revision {preview.ExpectedProjectRevision}, but found {project.Revision}.");
            }

            var result = ResearchWorkspaceBackupService.Create(
                location.RootDirectory,
                preview.TargetPath,
                preview.OccurredAt);
            return new DesktopWorkspaceRecoveryResult(
                DesktopWorkspaceCommandStatus.Succeeded,
                $"Verified backup created. Manifest: {result.ManifestDigest}.",
                location.RootDirectory,
                result.ArchivePath,
                result.ManifestDigest,
                Project(ResearchWorkspaceReadModelBuilder.Build(location.RootDirectory)));
        }
        catch (ResearchWorkspaceConcurrencyException exception)
        {
            return RecoveryRequired(exception.Message);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or
                UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return RecoveryFailed(SafeRecoveryFailure("Backup failed", exception));
        }
    }

    public DesktopWorkspaceRecoveryPreviewResult PreviewRestore(
        DesktopWorkspaceRestoreRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var archive = RequiredPath(request.BackupArchivePath);
            if (!File.Exists(archive))
            {
                return RecoveryPreviewFailure("The selected backup archive does not exist.");
            }

            var target = RequiredPath(request.TargetWorkspaceDirectory);
            if (File.Exists(target) || Directory.Exists(target))
            {
                return RecoveryPreviewFailure("The restore target must not exist.");
            }

            var archiveInfo = new FileInfo(archive);
            if (archiveInfo.Length < 1 ||
                archiveInfo.Length > ResearchWorkspaceBackupService.MaxBackupTotalBytes +
                    ResearchWorkspaceBackupService.MaxManifestBytes)
            {
                return RecoveryPreviewFailure("The selected backup archive exceeds the admitted size.");
            }
            var archiveDigest = FileDigest(archive);
            var effects = new[]
            {
                "verify the complete backup manifest and archive inventory",
                "restore exact bytes into a new sibling staging directory",
                "promote the new workspace only after verification succeeds"
            };
            return RecoveryReady(CreateRecoveryPreview(
                DesktopWorkspaceRecoveryKinds.Restore,
                target,
                null,
                null,
                archive,
                archiveInfo.Length,
                archiveDigest,
                target,
                request.OccurredAt,
                effects));
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return RecoveryPreviewFailure(SafeFailure("Restore preview failed", exception));
        }
    }

    public DesktopWorkspaceRecoveryResult ExecuteRestore(
        DesktopWorkspaceRecoveryPreview preview)
    {
        if (!ValidateRecoveryPreview(preview, DesktopWorkspaceRecoveryKinds.Restore, out var failure))
        {
            return failure!;
        }

        try
        {
            if (File.Exists(preview.TargetPath) || Directory.Exists(preview.TargetPath))
            {
                return RecoveryStale("stale-restore-target: the selected target now exists.");
            }
            if (!File.Exists(preview.BackupArchivePath))
            {
                return RecoveryStale("stale-backup-archive: the selected backup no longer exists.");
            }

            var archive = new FileInfo(preview.BackupArchivePath);
            var digest = FileDigest(archive.FullName);
            if (archive.Length != preview.ExpectedArchiveLength ||
                !string.Equals(digest, preview.ExpectedArchiveDigest, StringComparison.Ordinal))
            {
                return RecoveryStale("stale-backup-archive: the selected backup bytes changed after preview.");
            }

            var result = ResearchWorkspaceBackupService.Restore(archive.FullName, preview.TargetPath);
            var overview = Project(ResearchWorkspaceReadModelBuilder.Build(result.WorkspaceRoot));
            return new DesktopWorkspaceRecoveryResult(
                DesktopWorkspaceCommandStatus.Succeeded,
                $"Verified workspace restored at revision {result.ProjectRevision}.",
                result.WorkspaceRoot,
                archive.FullName,
                null,
                overview);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or
                UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return RecoveryFailed(SafeRecoveryFailure("Restore failed", exception));
        }
    }

    private static DesktopWorkspaceRecoveryPreview CreateRecoveryPreview(
        string operationKind,
        string workspaceDirectory,
        string? workspaceId,
        long? expectedProjectRevision,
        string? backupArchivePath,
        long? expectedArchiveLength,
        string? expectedArchiveDigest,
        string targetPath,
        DateTimeOffset occurredAt,
        IReadOnlyList<string> effects)
    {
        var token = RecoveryToken(
            operationKind,
            workspaceDirectory,
            workspaceId,
            expectedProjectRevision,
            backupArchivePath,
            expectedArchiveLength,
            expectedArchiveDigest,
            targetPath,
            occurredAt,
            effects,
            RecoveryNonClaims);
        return new DesktopWorkspaceRecoveryPreview(
            operationKind,
            workspaceDirectory,
            workspaceId,
            expectedProjectRevision,
            backupArchivePath,
            expectedArchiveLength,
            expectedArchiveDigest,
            targetPath,
            occurredAt,
            effects,
            RecoveryNonClaims,
            token);
    }

    private static bool ValidateRecoveryPreview(
        DesktopWorkspaceRecoveryPreview? preview,
        string expectedKind,
        out DesktopWorkspaceRecoveryResult? failure)
    {
        if (preview is null ||
            !string.Equals(preview.OperationKind, expectedKind, StringComparison.Ordinal))
        {
            failure = RecoveryFailed("The recovery confirmation preview is missing or has the wrong operation.");
            return false;
        }

        var expectedToken = RecoveryToken(
            preview.OperationKind,
            preview.WorkspaceDirectory,
            preview.WorkspaceId,
            preview.ExpectedProjectRevision,
            preview.BackupArchivePath,
            preview.ExpectedArchiveLength,
            preview.ExpectedArchiveDigest,
            preview.TargetPath,
            preview.OccurredAt,
            preview.ExpectedEffects,
            preview.NonClaims);
        if (!string.Equals(expectedToken, preview.ConfirmationToken, StringComparison.Ordinal))
        {
            failure = RecoveryStale("stale-recovery-preview: preview material or confirmation token changed.");
            return false;
        }

        failure = null;
        return true;
    }

    private static string RecoveryToken(
        string operationKind,
        string workspaceDirectory,
        string? workspaceId,
        long? expectedProjectRevision,
        string? backupArchivePath,
        long? expectedArchiveLength,
        string? expectedArchiveDigest,
        string targetPath,
        DateTimeOffset occurredAt,
        IReadOnlyList<string> effects,
        IReadOnlyList<string> nonClaims)
    {
        var material = new CanonicalJsonObject()
            .Add("schema", "nexus.desktop.workspace-recovery-preview")
            .Add("schema_version", "1.0.0")
            .Add("operation_kind", operationKind)
            .Add("workspace_directory", workspaceDirectory)
            .Add("workspace_id", workspaceId is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(workspaceId))
            .Add("expected_project_revision", expectedProjectRevision is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(expectedProjectRevision.Value))
            .Add("backup_archive_path", backupArchivePath is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(backupArchivePath))
            .Add("expected_archive_length", expectedArchiveLength is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(expectedArchiveLength.Value))
            .Add("expected_archive_digest", expectedArchiveDigest is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(expectedArchiveDigest))
            .Add("target_path", targetPath)
            .Add("occurred_at", occurredAt.UtcDateTime.ToString(
                "O",
                System.Globalization.CultureInfo.InvariantCulture))
            .Add("expected_effects", new CanonicalJsonArray(effects.Select(CanonicalJsonValue.From)))
            .Add("non_claims", new CanonicalJsonArray(nonClaims.Select(CanonicalJsonValue.From)));
        return ContentDigest.Sha256CanonicalJson(material).ToString();
    }

    private static DesktopWorkspaceRecoveryPreviewResult RecoveryReady(
        DesktopWorkspaceRecoveryPreview preview) =>
        new(
            DesktopWorkspaceCommandStatus.Ready,
            "Review the exact local recovery effects before confirmation.",
            preview);

    private static DesktopWorkspaceRecoveryPreviewResult RecoveryPreviewFailure(string message) =>
        new(DesktopWorkspaceCommandStatus.Failed, message, null);

    private static DesktopWorkspaceRecoveryResult RecoveryFailed(string message) =>
        new(DesktopWorkspaceCommandStatus.Failed, message);

    private static DesktopWorkspaceRecoveryResult RecoveryStale(string message) =>
        new(DesktopWorkspaceCommandStatus.Stale, message);

    private static DesktopWorkspaceRecoveryResult RecoveryRequired(string message) =>
        new(DesktopWorkspaceCommandStatus.RecoveryRequired, message);

    private static string SafeRecoveryFailure(string prefix, Exception exception) =>
        exception is UnauthorizedAccessException
            ? $"{prefix}: access was denied."
            : exception is IOException
                ? $"{prefix}: a local file operation failed."
                : $"{prefix}: the selected recovery material was rejected.";

    private static string FileDigest(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ContentDigest.Create(
            DigestAlgorithm.Sha256,
            Convert.ToHexStringLower(SHA256.HashData(stream))).ToString();
    }
}
