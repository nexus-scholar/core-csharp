using Avalonia.Controls;
using NexusScholar.Deduplication;
using NexusScholar.Desktop.AppServices;
using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

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

    [TestMethod]
    public void Human_deduplication_review_requires_preview_and_cancel_is_non_mutating()
    {
        using var workspace = TemporaryAuthorityWorkspace.Create();
        var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
        viewModel.Open(workspace.Root);
        var target = viewModel.ReviewQueue!.Targets.Single();
        var before = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        viewModel.PreviewDeduplicationReview(
            target.TargetId,
            DeduplicationAuthorityPolicyConstants.KeepSeparateAction,
            "different",
            "Distinct records.",
            "alice",
            "owner",
            null,
            FixedTime);

        Assert.IsTrue(viewModel.HasPendingConfirmation);
        Assert.AreEqual("Record human deduplication review", viewModel.PendingCommandLabel);
        Assert.IsNotNull(viewModel.PendingReviewPreview);
        viewModel.CancelPending();
        var after = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.IsFalse(viewModel.HasPendingConfirmation);
        Assert.AreEqual(before.Revision, after.Revision);
    }

    [TestMethod]
    public void Human_deduplication_review_confirm_commits_and_refreshes_active_decision()
    {
        using var workspace = TemporaryAuthorityWorkspace.Create();
        var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
        viewModel.Open(workspace.Root);
        var target = viewModel.ReviewQueue!.Targets.Single();

        viewModel.PreviewDeduplicationReview(
            target.TargetId,
            DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction,
            "uncertain",
            null,
            "alice",
            "owner",
            null,
            FixedTime);
        viewModel.ConfirmPending();

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, viewModel.StatusKind);
        Assert.IsFalse(viewModel.HasPendingConfirmation);
        Assert.AreEqual(1, viewModel.ReviewQueue!.Targets.Single().ActiveDecisions.Count);
        Assert.IsNotNull(viewModel.Overview);
    }

    [TestMethod]
    public void Human_review_preview_requires_explicit_target_action_reason_actor_and_role()
    {
        Assert.IsFalse(MainWindow.CanPreviewDeduplicationReview(null, "merge", "duplicate", "alice", "owner"));
        Assert.IsFalse(MainWindow.CanPreviewDeduplicationReview("target", null, "duplicate", "alice", "owner"));
        Assert.IsFalse(MainWindow.CanPreviewDeduplicationReview("target", "merge", null, "alice", "owner"));
        Assert.IsFalse(MainWindow.CanPreviewDeduplicationReview("target", "merge", "duplicate", null, "owner"));
        Assert.IsFalse(MainWindow.CanPreviewDeduplicationReview("target", "merge", "duplicate", "alice", null));
        Assert.IsTrue(MainWindow.CanPreviewDeduplicationReview("target", "merge", "duplicate", "alice", "owner"));
    }

    [TestMethod]
    public void Reusable_control_is_detached_before_a_render_tree_is_rebuilt()
    {
        var control = new TextBox();
        var firstRender = new Grid();
        var secondRender = new Grid();
        firstRender.Children.Add(control);

        MainWindow.DetachFromParent(control);
        secondRender.Children.Add(control);

        Assert.IsFalse(firstRender.Children.Contains(control));
        Assert.IsTrue(secondRender.Children.Contains(control));
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

    private sealed class TemporaryAuthorityWorkspace : IDisposable
    {
        private TemporaryAuthorityWorkspace(string root, ResearchWorkspaceLocation location)
        {
            Root = root;
            Location = location;
        }

        public string Root { get; }

        public ResearchWorkspaceLocation Location { get; }

        public static TemporaryAuthorityWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"nexus-desktop-vm-review-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var location = new ResearchWorkspaceLocation(root, ResearchWorkspacePaths.ProjectFile(root));
            foreach (var directory in ResearchWorkspacePaths.RequiredDirectories)
            {
                Directory.CreateDirectory(ResearchWorkspacePaths.InProject(root, directory));
            }

            var project = ResearchWorkspaceProject.Create("Desktop review", FixedTime);
            var relative = $"{ResearchWorkspacePaths.SearchInputs}/input.csv";
            var bytes = System.Text.Encoding.UTF8.GetBytes(
                "eid,title,doi\n1,Example record,10.1000/example-a\n2,Example record,10.1000/example-b\n");
            File.WriteAllBytes(ResearchWorkspacePaths.InProject(root, relative), bytes);
            project = project.WithInput(new ResearchWorkspaceInput
            {
                InputId = "input",
                Kind = "search-export",
                Source = "scopus",
                Format = "csv",
                RelativePath = relative,
                Sha256 = ContentDigest.Sha256(bytes).ToString(),
                QueryId = "input",
                ImportTracePath = $"{ResearchWorkspacePaths.ImportOutputs}/input.import-trace.json"
            });
            ResearchWorkspaceStore.WriteProject(location, project);
            var analysis = ResearchWorkspaceTransaction.AnalyzeAndCommit(location, project);
            var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(analysis.Analysis.DeduplicationResult);
            var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(new UnverifiedDeduplicationAuthorityPolicy(
                DeduplicationAuthorityPolicyConstants.SchemaId,
                DeduplicationAuthorityPolicyConstants.SchemaVersion,
                DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
                source.Result.PolicyId!,
                DeduplicationService.PolicyVersion,
                new[] { new DeduplicationAuthorityPolicyActorRole("alice", "owner") },
                DeduplicationAuthorityPolicyConstants.ClosedActions,
                new[]
                {
                    new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, new[] { "duplicate" }),
                    new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, new[] { "different" }),
                    new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, new[] { "uncertain" })
                },
                false,
                "alice",
                "owner",
                FixedTime));
            var manifestBytes = File.ReadAllBytes(ResearchWorkspacePaths.InProject(root, analysis.Project.GenerationManifestPath!));
            _ = ResearchWorkspaceTransaction.InitializeAuthorityGeneration(
                location,
                analysis.Project,
                analysis.Project.CurrentGenerationId!,
                ContentDigest.Sha256(manifestBytes).ToString(),
                "snapshot-desktop-vm-baseline",
                source,
                policy,
                "alice",
                "owner",
                new TestClock(),
                new TestIdGenerator());
            return new TemporaryAuthorityWorkspace(root, location);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private sealed class TestClock : IClock
        {
            public DateTimeOffset UtcNow => FixedTime;
        }

        private sealed class TestIdGenerator : IIdGenerator
        {
            private int _value = 910;

            public Guid NewId() => Guid.Parse($"00000000-0000-0000-0000-{_value++:000000000000}");
        }
    }
}
