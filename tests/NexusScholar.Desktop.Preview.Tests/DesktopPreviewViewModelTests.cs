using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Desktop.Preview;
using NexusScholar.ResearchWorkspace;
using NexusScholar.UiContracts;

namespace NexusScholar.Desktop.Preview.Tests;

[TestClass]
public sealed class DesktopPreviewViewModelTests
{
    [TestMethod]
    public void View_model_reports_missing_workspace_without_absolute_path_status()
    {
        var model = new DesktopPreviewViewModel();
        var missingPath = Path.Combine(Path.GetTempPath(), $"nexus-desktop-missing-{Guid.NewGuid():N}");

        model.LoadWorkspace(missingPath);

        Assert.AreEqual(WorkspaceState.Missing, model.Overview.State);
        Assert.AreEqual("welcome", model.SelectedSection.Id);
        Assert.IsTrue(model.StatusMessage.Contains("No local Nexus research workspace", StringComparison.Ordinal));
        Assert.IsFalse(model.StatusMessage.Contains(missingPath, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void View_model_loads_review_ready_workspace_from_generated_local_bundle()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = workspace.Project;
        project = AddBundleFile(workspace, project, "search-001", "scopus", "csv", "combined_scopus_like.csv");
        project = AddBundleFile(workspace, project, "search-002", "web-of-science", "ris", "combined_wos_like.ris");
        project = AddBundleFile(workspace, project, "search-003", "google-scholar", "bibtex", "combined_scholar_style.bib");
        project = AddBundleFile(workspace, project, "search-004", "other", "csv", "combined_wos_like_source_specific.csv");
        AnalyzeAndPersist(workspace, project);

        var model = new DesktopPreviewViewModel();
        model.LoadWorkspace(workspace.Root);

        Assert.AreEqual(WorkspaceState.ReviewReady, model.Overview.State);
        Assert.AreEqual("overview", model.SelectedSection.Id);
        Assert.IsTrue(model.Overview.EvidenceRecords.Count >= 10);
        Assert.IsTrue(model.Overview.ReviewQueue.Count > 0);
        Assert.IsTrue(model.Overview.DuplicateClusters.Count > 0);
        Assert.IsTrue(model.Overview.DuplicateCandidateDetails.Count > 0);
        Assert.IsTrue(model.Overview.LockedDecisionActions.Count > 0);
        Assert.IsTrue(model.StatusMessage.Contains("current folder", StringComparison.Ordinal));
        AssertDoesNotContainWorkspaceRoot(model.Overview, workspace.Root);
    }

    [TestMethod]
    public void Locked_decision_actions_are_noop_status_messages()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = workspace.Project;
        project = AddBundleFile(workspace, project, "search-001", "scopus", "csv", "combined_scopus_like.csv");
        project = AddBundleFile(workspace, project, "search-002", "web-of-science", "ris", "combined_wos_like.ris");
        project = AddBundleFile(workspace, project, "search-003", "google-scholar", "bibtex", "combined_scholar_style.bib");
        project = AddBundleFile(workspace, project, "search-004", "other", "csv", "combined_wos_like_source_specific.csv");
        AnalyzeAndPersist(workspace, project);
        var beforeFiles = Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var model = new DesktopPreviewViewModel();
        model.LoadWorkspace(workspace.Root);
        var action = model.Overview.LockedDecisionActions[0];

        var status = model.InvokeLockedDecisionAction(action.ActionId);
        var afterFiles = Directory.GetFiles(workspace.Root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.IsFalse(action.IsExecutable);
        Assert.IsNull(action.CommandKind);
        Assert.IsTrue(status.Contains("Decision execution is locked", StringComparison.Ordinal));
        CollectionAssert.AreEqual(beforeFiles, afterFiles);
    }

    [TestMethod]
    public void Verify_action_refreshes_status_without_writing_outputs()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = workspace.Project;
        project = AddBundleFile(workspace, project, "search-001", "scopus", "csv", "combined_scopus_like.csv");
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);
        var beforeFiles = SnapshotFiles(workspace.Root);

        var model = new DesktopPreviewViewModel();
        model.LoadWorkspace(workspace.Root);
        var result = model.RunVerify();
        var afterFiles = SnapshotFiles(workspace.Root);

        Assert.IsTrue(result.Completed);
        Assert.AreEqual("verification", model.SelectedSection.Id);
        Assert.IsTrue(model.StatusMessage.Contains("Workspace verification", StringComparison.Ordinal));
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.DeduplicationResultPath)));
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.WorkspacePlanPath)));
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.ReviewReportPath)));
        CollectionAssert.AreEqual(beforeFiles, afterFiles);
        AssertDoesNotContainWorkspaceRoot(model.Overview, workspace.Root);
    }

    [TestMethod]
    public void Analyze_action_writes_expected_generated_outputs_and_refreshes_read_model()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = workspace.Project;
        project = AddBundleFile(workspace, project, "search-001", "scopus", "csv", "combined_scopus_like.csv");
        project = AddBundleFile(workspace, project, "search-002", "web-of-science", "ris", "combined_wos_like.ris");
        project = AddBundleFile(workspace, project, "search-003", "google-scholar", "bibtex", "combined_scholar_style.bib");
        project = AddBundleFile(workspace, project, "search-004", "other", "csv", "combined_wos_like_source_specific.csv");
        WriteImportTraces(workspace, project);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);

        var model = new DesktopPreviewViewModel();
        model.LoadWorkspace(workspace.Root);
        var result = model.RunAnalyze();
        var updatedProject = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        Assert.IsTrue(result.Completed);
        Assert.AreEqual(ResearchWorkspaceExitCodes.Success, result.ExitCode);
        Assert.AreEqual("analysis", model.SelectedSection.Id);
        Assert.AreEqual(WorkspaceState.ReviewReady, model.Overview.State);
        Assert.IsTrue(updatedProject.Outputs.Values.All(path => File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, path))));
        Assert.IsNotNull(updatedProject.CurrentGenerationId);
        Assert.IsNotNull(ResearchWorkspaceGenerationVerifier.VerifyCurrent(workspace.Location, updatedProject));
        Assert.IsTrue(model.StatusMessage.Contains("Workspace analysis complete", StringComparison.Ordinal));
        Assert.IsFalse(model.StatusMessage.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        AssertDoesNotContainWorkspaceRoot(model.Overview, workspace.Root);
    }

    [TestMethod]
    public void Analyze_action_for_missing_workspace_does_not_claim_success_or_leak_path()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"nexus-desktop-action-missing-{Guid.NewGuid():N}");
        var model = new DesktopPreviewViewModel();
        model.LoadWorkspace(missingPath);

        var result = model.RunAnalyze();

        Assert.IsFalse(result.Completed);
        Assert.AreEqual(ResearchWorkspaceExitCodes.MissingProjectOrInput, result.ExitCode);
        Assert.AreEqual("welcome", model.SelectedSection.Id);
        Assert.IsTrue(model.StatusMessage.Contains("No Nexus research workspace", StringComparison.Ordinal));
        Assert.IsFalse(model.StatusMessage.Contains(missingPath, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Sections_include_required_ui01_screens()
    {
        var sectionIds = DesktopPreviewViewModel.Sections.Select(section => section.Id).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "welcome",
                "overview",
                "evidence",
                "imports",
                "verification",
                "analysis",
                "review",
                "clusters",
                "detail",
                "reports",
                "diagnostics"
            },
            sectionIds);
    }

    [TestMethod]
    public void Boundary_badges_preserve_preview_non_claims()
    {
        var model = new DesktopPreviewViewModel();

        CollectionAssert.AreEquivalent(
            new[]
            {
                "local folder",
                "researcher-supplied files",
                "no providers",
                "safe verify/analyze",
                "read-only review",
                "locked merge gates"
            },
            model.BoundaryBadges.ToArray());
    }

    [TestMethod]
    public void Host_layout_keeps_status_bar_outside_scrollable_workspace()
    {
        var root = MainWindow.BuildHostLayout(new Border(), new Border(), new Border(), new Border());
        var layout = Assert.IsInstanceOfType<Grid>(root.Child);

        Assert.AreEqual(3, layout.RowDefinitions.Count);
        Assert.AreEqual(GridUnitType.Auto, layout.RowDefinitions[0].Height.GridUnitType);
        Assert.AreEqual(GridUnitType.Star, layout.RowDefinitions[1].Height.GridUnitType);
        Assert.AreEqual(GridUnitType.Auto, layout.RowDefinitions[2].Height.GridUnitType);

        var workspace = Assert.IsInstanceOfType<Grid>(layout.Children.Single(child => Grid.GetRow(child) == 1));
        var workspaceScroller = Assert.IsInstanceOfType<ScrollViewer>(
            workspace.Children.Single(child => Grid.GetColumn(child) == 1));
        Assert.AreEqual(ScrollBarVisibility.Disabled, workspaceScroller.HorizontalScrollBarVisibility);
        Assert.AreEqual(ScrollBarVisibility.Auto, workspaceScroller.VerticalScrollBarVisibility);
        Assert.AreEqual(VerticalAlignment.Stretch, workspaceScroller.VerticalAlignment);

        var statusChild = layout.Children.Single(child => Grid.GetRow(child) == 2);
        Assert.IsInstanceOfType<Border>(statusChild);
    }

    private static ResearchWorkspaceProject AddBundleFile(
        TemporaryWorkspace workspace,
        ResearchWorkspaceProject project,
        string inputId,
        string source,
        string format,
        string fixtureFileName)
    {
        var fixturePath = Path.Combine(
            RepositoryRoot(),
            "tests",
            "NexusScholar.AppServices.Tests",
            "Fixtures",
            "App01GeneratedLocalBundles",
            "bundles",
            "FB07-combined-app01-demo",
            fixtureFileName);
        var relativePath = $"{ResearchWorkspacePaths.SearchInputs}/{inputId}-{source}.{SearchImportAliases.ExtensionFor(format)}";
        var fullPath = ResearchWorkspacePaths.InProject(workspace.Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.Copy(fixturePath, fullPath, overwrite: true);
        return project.WithInput(InputFor(inputId, source, format, relativePath, File.ReadAllBytes(fullPath)));
    }

    private static ResearchWorkspaceInput InputFor(
        string inputId,
        string source,
        string format,
        string relativePath,
        byte[] sourceBytes)
    {
        return new ResearchWorkspaceInput
        {
            InputId = inputId,
            Kind = "search-export",
            Source = source,
            Format = format,
            RelativePath = relativePath,
            Sha256 = Sha256(sourceBytes),
            QueryId = inputId,
            QueryText = "desktop-preview test",
            ImportTracePath = $"{ResearchWorkspacePaths.ImportOutputs}/{inputId}.import-trace.json"
        };
    }

    private static void AnalyzeAndPersist(TemporaryWorkspace workspace, ResearchWorkspaceProject project)
    {
        var result = ResearchWorkspaceAnalyzer.Analyze(workspace.Location, project);
        WriteImportTraces(workspace, result);

        ResearchWorkspaceJson.WriteJsonFile(
            ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.DeduplicationResultPath),
            result.DeduplicationResult);
        ResearchWorkspaceJson.WriteJsonFile(
            ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.WorkspacePlanPath),
            result.WorkspacePlan,
            UiContractJson.SerializerOptions);
        ResearchWorkspaceJson.WriteTextFile(
            ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.ReviewReportPath),
            WorkspacePlanReportWriter.Format(result));

        ResearchWorkspaceStore.WriteProject(
            workspace.Location,
            project.WithOutputs(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["deduplicationResult"] = ResearchWorkspaceAnalyzer.DeduplicationResultPath,
                ["workspacePlan"] = ResearchWorkspaceAnalyzer.WorkspacePlanPath,
                ["reviewReport"] = ResearchWorkspaceAnalyzer.ReviewReportPath
            }));
    }

    private static void WriteImportTraces(TemporaryWorkspace workspace, ResearchWorkspaceProject project)
    {
        WriteImportTraces(workspace, ResearchWorkspaceAnalyzer.Analyze(workspace.Location, project));
    }

    private static void WriteImportTraces(TemporaryWorkspace workspace, ResearchWorkspaceAnalysisResult result)
    {
        foreach (var trace in result.ImportTraces)
        {
            var inputId = trace.TraceId.EndsWith(".import-trace", StringComparison.Ordinal)
                ? trace.TraceId[..^".import-trace".Length]
                : trace.TraceId;
            ResearchWorkspaceJson.WriteJsonFile(
                ResearchWorkspacePaths.InProject(workspace.Root, $"{ResearchWorkspacePaths.ImportOutputs}/{inputId}.import-trace.json"),
                trace);
        }
    }

    private static string[] SnapshotFiles(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertDoesNotContainWorkspaceRoot(WorkspaceOverviewReadModel model, string workspaceRoot)
    {
        var json = JsonSerializer.Serialize(model);
        Assert.IsFalse(json.Contains(workspaceRoot, StringComparison.OrdinalIgnoreCase), json);
    }

    private static string Sha256(byte[] bytes)
    {
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NexusScholar.Core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string root)
        {
            Root = root;
            Project = ResearchWorkspaceProject.Create(
                "APP-01 desktop preview test",
                new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
            foreach (var relativeDirectory in ResearchWorkspacePaths.RequiredDirectories)
            {
                Directory.CreateDirectory(ResearchWorkspacePaths.InProject(Root, relativeDirectory));
            }

            var projectFile = ResearchWorkspacePaths.ProjectFile(Root);
            ResearchWorkspaceJson.WriteProjectFile(projectFile, Project);
            Location = new ResearchWorkspaceLocation(Root, projectFile);
        }

        public string Root { get; }

        public ResearchWorkspaceProject Project { get; }

        public ResearchWorkspaceLocation Location { get; }

        public static TemporaryWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"nexus-desktop-preview-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TemporaryWorkspace(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
