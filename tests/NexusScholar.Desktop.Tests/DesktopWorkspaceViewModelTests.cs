using Avalonia.Controls;
using NexusScholar.Desktop.AppServices;

namespace NexusScholar.Desktop.Tests;

[TestClass]
public sealed class DesktopWorkspaceViewModelTests
{
    [TestMethod]
    public void Initialize_requires_preview_then_confirmation()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());

        viewModel.PreviewInitialize(directory.Path, "Desktop review", "desktop-review", FixedTime);

        Assert.IsTrue(viewModel.HasPendingConfirmation);
        Assert.IsFalse(File.Exists(System.IO.Path.Combine(directory.Path, "nexus.project.json")));
        Assert.AreEqual("Initialize local workspace", viewModel.PendingCommandLabel);

        viewModel.ConfirmPending();

        Assert.IsTrue(viewModel.HasWorkspace);
        Assert.IsTrue(File.Exists(System.IO.Path.Combine(directory.Path, "nexus.project.json")));
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, viewModel.StatusKind);
    }

    [TestMethod]
    public void Cancel_discards_preview_without_writing()
    {
        using var directory = new TemporaryDirectory();
        var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
        viewModel.PreviewInitialize(directory.Path, "Cancelled review", null, FixedTime);

        viewModel.CancelPending();

        Assert.IsFalse(viewModel.HasPendingConfirmation);
        Assert.IsFalse(File.Exists(System.IO.Path.Combine(directory.Path, "nexus.project.json")));
    }

    [TestMethod]
    public void Verify_without_open_workspace_is_explicit_failure()
    {
        var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());

        viewModel.Verify();

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Failed, viewModel.StatusKind);
        StringAssert.Contains(viewModel.Status, "Open a workspace");
    }

    [TestMethod]
    public void Failed_open_clears_previously_loaded_workspace_projection()
    {
        using var directory = new TemporaryDirectory();
        using var missing = new TemporaryDirectory();
        var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
        viewModel.PreviewInitialize(directory.Path, "Loaded workspace", null, FixedTime);
        viewModel.ConfirmPending();
        Assert.IsTrue(viewModel.HasWorkspace);

        viewModel.Open(missing.Path);

        Assert.IsFalse(viewModel.HasWorkspace);
        Assert.IsNull(viewModel.Overview);
        Assert.AreEqual(string.Empty, viewModel.WorkspacePath);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Failed, viewModel.StatusKind);
    }

    [TestMethod]
    public void Warning_import_remains_pending_until_confirmation_then_surfaces_attention()
    {
        using var directory = new TemporaryDirectory();
        var sourcePath = Path.Combine(directory.Path, "warning.csv");
        File.WriteAllText(sourcePath, "eid,title,author names,year,source title,doi\nwarning-1,,Researcher,2026,Journal,\n");
        var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
        viewModel.PreviewInitialize(directory.Path, "Warning workspace", null, FixedTime);
        viewModel.ConfirmPending();

        viewModel.PreviewImport(sourcePath, "scopus", "csv", "search-warning", null, FixedTime);

        Assert.IsTrue(viewModel.HasPendingConfirmation);
        Assert.AreEqual("Import local Search export", viewModel.PendingCommandLabel);
        Assert.IsTrue(viewModel.PendingEffects.Count > 0);

        viewModel.ConfirmPending();

        Assert.IsFalse(viewModel.HasPendingConfirmation);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Attention, viewModel.StatusKind);
        Assert.IsNotNull(viewModel.Overview);
        Assert.IsTrue(viewModel.Overview.ParserWarningCount > 0);
    }

    [TestMethod]
    public void Shell_grid_has_stable_navigation_workspace_and_inspector_tracks()
    {
        var grid = MainWindow.BuildShellGrid(new Border(), new Border(), new Border());

        Assert.AreEqual(3, grid.ColumnDefinitions.Count);
        Assert.AreEqual(224d, grid.ColumnDefinitions[0].Width.Value);
        Assert.IsTrue(grid.ColumnDefinitions[1].Width.IsStar);
        Assert.AreEqual(300d, grid.ColumnDefinitions[2].Width.Value);
    }

    private static readonly DateTimeOffset FixedTime =
        new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexus-desktop-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
