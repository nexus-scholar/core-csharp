using NexusScholar.Desktop.AppServices;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Desktop.AppServices.Tests;

[TestClass]
public sealed class DesktopWorkspaceRecoveryFacadeTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 7, 19, 2, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Backup_and_restore_require_exact_preview_then_reopen_new_workspace()
    {
        using var directory = new TemporaryDirectory();
        var workspace = Path.Combine(directory.Path, "workspace");
        Directory.CreateDirectory(workspace);
        var backup = Path.Combine(directory.Path, "workspace.nexus-backup.zip");
        var restored = Path.Combine(directory.Path, "restored");
        var facade = new DesktopWorkspaceCommandFacade();
        Initialize(facade, workspace);

        var backupPreview = facade.PreviewBackup(
            new DesktopWorkspaceBackupRequest(workspace, backup, FixedTime));

        Assert.IsTrue(backupPreview.IsReady);
        Assert.IsFalse(File.Exists(backup));
        var backupResult = facade.ExecuteBackup(backupPreview.Preview!);
        Assert.IsTrue(backupResult.Completed);
        Assert.IsTrue(File.Exists(backup));
        Assert.IsNotNull(backupResult.ManifestDigest);

        var restorePreview = facade.PreviewRestore(
            new DesktopWorkspaceRestoreRequest(backup, restored, FixedTime));

        Assert.IsTrue(restorePreview.IsReady);
        Assert.IsFalse(Directory.Exists(restored));
        var restoreResult = facade.ExecuteRestore(restorePreview.Preview!);
        Assert.IsTrue(restoreResult.Completed);
        Assert.AreEqual(Path.GetFullPath(restored), restoreResult.WorkspaceDirectory);
        Assert.IsNotNull(restoreResult.Overview);
        Assert.AreEqual("Release recovery test", restoreResult.Overview.ProjectTitle);

        CollectionAssert.AreEqual(
            Snapshot(workspace),
            Snapshot(restored));
    }

    [TestMethod]
    public void Backup_confirmation_fails_stale_after_workspace_revision_changes()
    {
        using var directory = new TemporaryDirectory();
        var workspace = Path.Combine(directory.Path, "workspace");
        Directory.CreateDirectory(workspace);
        var facade = new DesktopWorkspaceCommandFacade();
        Initialize(facade, workspace);
        var preview = facade.PreviewBackup(new DesktopWorkspaceBackupRequest(
            workspace,
            Path.Combine(directory.Path, "stale.zip"),
            FixedTime)).Preview!;
        var location = ResearchWorkspaceStore.FindFrom(workspace)!;
        var current = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
        ResearchWorkspaceStore.WriteProject(location, current with { Revision = current.Revision + 1 });

        var result = facade.ExecuteBackup(preview);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, result.Status);
        StringAssert.Contains(result.Message, "stale-workspace-revision");
    }

    [TestMethod]
    public void Restore_confirmation_fails_stale_after_archive_bytes_change()
    {
        using var directory = new TemporaryDirectory();
        var workspace = Path.Combine(directory.Path, "workspace");
        Directory.CreateDirectory(workspace);
        var backup = Path.Combine(directory.Path, "workspace.zip");
        var facade = new DesktopWorkspaceCommandFacade();
        Initialize(facade, workspace);
        var backupPreview = facade.PreviewBackup(
            new DesktopWorkspaceBackupRequest(workspace, backup, FixedTime));
        Assert.IsTrue(facade.ExecuteBackup(backupPreview.Preview!).Completed);
        var restorePreview = facade.PreviewRestore(new DesktopWorkspaceRestoreRequest(
            backup,
            Path.Combine(directory.Path, "restored"),
            FixedTime)).Preview!;
        File.AppendAllText(backup, "changed");

        var result = facade.ExecuteRestore(restorePreview);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, result.Status);
        StringAssert.Contains(result.Message, "selected backup bytes changed");
    }

    [TestMethod]
    public void Recovery_confirmation_token_binds_exact_effects()
    {
        using var directory = new TemporaryDirectory();
        var workspace = Path.Combine(directory.Path, "workspace");
        Directory.CreateDirectory(workspace);
        var facade = new DesktopWorkspaceCommandFacade();
        Initialize(facade, workspace);
        var preview = facade.PreviewBackup(new DesktopWorkspaceBackupRequest(
            workspace,
            Path.Combine(directory.Path, "backup.zip"),
            FixedTime)).Preview!;

        var result = facade.ExecuteBackup(preview with
        {
            ExpectedEffects = ["changed effect"]
        });

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, result.Status);
        StringAssert.Contains(result.Message, "stale-recovery-preview");
    }

    private static void Initialize(DesktopWorkspaceCommandFacade facade, string workspace)
    {
        var preview = facade.PreviewInitialize(new DesktopInitializeRequest(
            workspace,
            "Release recovery test",
            "release-recovery-test",
            FixedTime));
        Assert.IsTrue(preview.IsReady);
        var result = facade.ExecuteInitialize(preview.Preview!);
        Assert.IsTrue(result.Completed);
    }

    private static string[] Snapshot(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != ResearchWorkspacePaths.ProjectLockFileName)
            .Select(path =>
                $"{Path.GetRelativePath(root, path).Replace('\\', '/')}=" +
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nexus-desktop-recovery-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
