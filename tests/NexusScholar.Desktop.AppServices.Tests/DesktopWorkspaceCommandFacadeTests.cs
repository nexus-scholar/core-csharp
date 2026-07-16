using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Desktop.AppServices;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Desktop.AppServices.Tests;

[TestClass]
public sealed class DesktopWorkspaceCommandFacadeTests
{
    [TestMethod]
    public void OpenWorkspace_reports_success_for_valid_workspace()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();

        var result = facade.OpenWorkspace(workspace.Root);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, result.Status);
        Assert.IsTrue(result.Message.StartsWith("Opened ", StringComparison.Ordinal));
        Assert.IsNotNull(result.Overview);
        Assert.IsFalse(result.Message.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        AssertNoAbsolutePathsIn(result.Message);
    }

    [TestMethod]
    public void OpenWorkspace_reports_failed_for_missing_workspace()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        var missingRoot = Path.Combine(Path.GetTempPath(), $"nexus-desktop-appservices-missing-{Guid.NewGuid():N}");

        var result = facade.OpenWorkspace(missingRoot);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Failed, result.Status);
        Assert.AreEqual("No Nexus research workspace was found in the selected folder.", result.Message);
        AssertNoAbsolutePathsIn(result.Message);
    }

    [TestMethod]
    public void PreviewInitialize_is_non_mutating_and_execute_writes_workspace()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = new TemporaryDirectory();

        var preview = facade.PreviewInitialize(new DesktopInitializeRequest(
            workspace.Path, "Desktop analysis", "desktop-analysis", FixedTime));
        var projectPath = ResearchWorkspacePaths.ProjectFile(workspace.Path);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Ready, preview.Status);
        Assert.IsFalse(File.Exists(projectPath));
        AssertNoAbsolutePathsIn(preview.Message);

        var result = facade.ExecuteInitialize(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, result.Status);
        Assert.IsNotNull(result.Overview);
        Assert.IsTrue(File.Exists(projectPath));
        AssertNoAbsolutePathsIn(result.Message);
    }

    [TestMethod]
    public void ExecuteInitialize_reports_stale_when_confirmation_is_tampered()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = new TemporaryDirectory();

        var preview = facade.PreviewInitialize(new DesktopInitializeRequest(
            workspace.Path, "Desktop tamper test", null, FixedTime));
        var tampered = preview.Preview! with { ConfirmationToken = "tampered-token" };

        var result = facade.ExecuteInitialize(tampered);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, result.Status);
        Assert.AreEqual("stale-confirmation-preview: preview material or confirmation token changed.", result.Message);
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.ProjectFile(workspace.Path)));
        AssertNoAbsolutePathsIn(result.Message);
    }

    [TestMethod]
    public void ExecuteInitialize_rejects_newline_delimited_preview_field_smuggling()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = new TemporaryDirectory();
        var preview = facade.PreviewInitialize(new DesktopInitializeRequest(
            workspace.Path, "Review\nworkspace-alpha", "operator-a", FixedTime)).Preview!;
        var tampered = preview with
        {
            Title = "Review",
            RequestedWorkspaceId = "workspace-alpha\noperator-a"
        };

        var result = facade.ExecuteInitialize(tampered);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, result.Status);
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.ProjectFile(workspace.Path)));
    }

    [TestMethod]
    public void ExecuteInitialize_rejects_effect_pipe_delimiter_smuggling()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = new TemporaryDirectory();
        var preview = facade.PreviewInitialize(new DesktopInitializeRequest(
            workspace.Path, "Effect boundary", null, FixedTime)).Preview!;
        var tampered = preview with
        {
            ExpectedEffects = new[] { string.Join("|", preview.ExpectedEffects) }
        };

        var result = facade.ExecuteInitialize(tampered);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, result.Status);
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.ProjectFile(workspace.Path)));
    }

    [TestMethod]
    public void ExecuteInitialize_rejects_unsafe_workspace_id_without_writing_project()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = new TemporaryDirectory();
        var preview = facade.PreviewInitialize(new DesktopInitializeRequest(
            workspace.Path, "Unsafe workspace", "..\\bad", FixedTime));

        var result = facade.ExecuteInitialize(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Failed, result.Status);
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.ProjectFile(workspace.Path)));
    }

    [TestMethod]
    public void PreviewImportSearch_binds_workspace_revision_and_source_digest()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);

        var result = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root,
            sourcePath,
            "scopus",
            "csv",
            "search-001",
            "desktop query",
            FixedTime));

        var preview = result.Preview;

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Ready, result.Status);
        Assert.IsNotNull(preview);
        Assert.AreEqual(workspace.Project.WorkspaceId, preview.WorkspaceId);
        Assert.AreEqual(0L, preview.ExpectedProjectRevision);
        Assert.AreEqual(Path.GetFullPath(sourcePath), preview.SourcePath);
        Assert.AreEqual(ComputeSha256(File.ReadAllBytes(sourcePath)), preview.SourceDigest);
        CollectionAssert.Contains(preview.ExpectedEffects.ToArray(), "copy selected source bytes");
        CollectionAssert.Contains(preview.ExpectedEffects.ToArray(), "append one Search import trace");
        CollectionAssert.Contains(preview.ExpectedEffects.ToArray(), "advance project revision");
        AssertNoAbsolutePathsIn(result.Message);
    }

    [TestMethod]
    public void ExecuteImportSearch_reports_stale_when_source_bytes_change()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);
        var preview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root,
            sourcePath,
            "scopus",
            "csv",
            "search-001",
            null,
            FixedTime));

        File.AppendAllText(sourcePath, "changed-row,example\n");

        var result = facade.ExecuteImportSearch(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, result.Status);
        StringAssert.Contains(result.Message, "stale-import-source: the selected Search export changed after preview.");
        Assert.AreEqual(0, ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath).Inputs.Count);
        AssertNoAbsolutePathsIn(result.Message);
    }

    [TestMethod]
    public void ExecuteImportSearch_reports_stale_when_expected_revision_changes()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();

        var firstSourcePath = CreateSourceFile(workspace.Root, "first.csv", ScopusCsv);
        var firstPreview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root,
            firstSourcePath,
            "scopus",
            "csv",
            "search-001",
            null,
            FixedTime));
        var firstResult = facade.ExecuteImportSearch(firstPreview.Preview!);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, firstResult.Status);

        var secondSourcePath = CreateSourceFile(workspace.Root, "second.csv", ScopusCsv);
        var secondPreview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root,
            secondSourcePath,
            "scopus",
            "csv",
            "search-002",
            null,
            FixedTime));
        var currentProject = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        ResearchWorkspaceStore.WriteProject(workspace.Location, currentProject with { Revision = 2 });

        var staleResult = facade.ExecuteImportSearch(secondPreview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, staleResult.Status);
        StringAssert.Contains(staleResult.Message, "stale-workspace-revision: expected revision 1, but found 2.");
        Assert.AreEqual(1, ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath).Inputs.Count);
        AssertNoAbsolutePathsIn(staleResult.Message);
    }

    [TestMethod]
    public void ExecuteImportSearch_rejects_replaced_workspace_with_same_revision()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);
        var preview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root, sourcePath, "scopus", "csv", "search-001", null, FixedTime));
        var replacement = ResearchWorkspaceProject.Create("Replacement", FixedTime, "replacement-workspace") with
        {
            Revision = workspace.Project.Revision
        };
        ResearchWorkspaceStore.WriteProject(workspace.Location, replacement);

        var result = facade.ExecuteImportSearch(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, result.Status);
        StringAssert.Contains(result.Message, "stale-workspace-identity");
        Assert.AreEqual(0, ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath).Inputs.Count);
    }

    [TestMethod]
    public void ExecuteImportSearch_maps_parser_warnings_to_attention()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "warnings.ris", WarningFixtureContent);

        var preview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root,
            sourcePath,
            "web-of-science",
            "ris",
            "search-warning",
            "warning query",
            FixedTime));
        var result = facade.ExecuteImportSearch(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Attention, result.Status);
        Assert.IsNotNull(result.Overview);
        Assert.IsTrue(result.Overview.ParserWarningCount > 0);
        AssertNoAbsolutePathsIn(result.Message);
    }

    [TestMethod]
    public void ExecuteImportSearch_persists_neutral_local_workspace_import_trace_identity()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);
        var preview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root, sourcePath, "scopus", "csv", "search-001", null, FixedTime));

        var result = facade.ExecuteImportSearch(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, result.Status);
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var tracePath = ResearchWorkspacePaths.InProject(workspace.Root, project.Inputs.Single().ImportTracePath!);
        using var trace = JsonDocument.Parse(File.ReadAllText(tracePath));
        var metadata = trace.RootElement.GetProperty("metadata");
        Assert.AreEqual("nexus.local-workspace.search-import", metadata.GetProperty("parserId").GetString());
        Assert.AreEqual("nexus-desktop-local", metadata.GetProperty("importedBy").GetString());
    }

    [TestMethod]
    public void ExecuteImportSearch_reports_recovery_required_when_workspace_lock_is_held()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);
        var preview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root, sourcePath, "scopus", "csv", "search-001", null, FixedTime));
        using var heldLock = HoldWorkspaceLock(workspace.Root);

        var result = facade.ExecuteImportSearch(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.RecoveryRequired, result.Status);
        Assert.AreEqual(0, ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath).Inputs.Count);
    }

    [TestMethod]
    public void ExecuteImportSearch_reports_recovery_required_when_authority_generation_is_active()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);
        var preview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root, sourcePath, "scopus", "csv", "search-001", null, FixedTime));
        ActivateAuthority(workspace);

        var result = facade.ExecuteImportSearch(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.RecoveryRequired, result.Status);
    }

    [TestMethod]
    public void VerifyWorkspace_reports_attention_for_parser_warning_workspace()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "warnings.ris", WarningFixtureContent);
        var preview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root,
            sourcePath,
            "web-of-science",
            "ris",
            "search-warning",
            "warning query",
            FixedTime));
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Attention, facade.ExecuteImportSearch(preview.Preview!).Status);

        var result = facade.VerifyWorkspace(workspace.Root);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Attention, result.Status);
        Assert.IsNotNull(result.Overview);
        Assert.IsTrue(result.Message.Contains("needs attention", StringComparison.Ordinal));
        AssertNoAbsolutePathsIn(result.Message);
    }

    [TestMethod]
    public void PreviewAnalyze_is_non_mutating_and_execute_produces_review_outputs_and_stale_revision()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);

        var firstImportPreview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root,
            sourcePath,
            "scopus",
            "csv",
            "search-001",
            null,
            FixedTime));
        var importResult = facade.ExecuteImportSearch(firstImportPreview.Preview!);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, importResult.Status);

        var beforeFiles = SnapshotFiles(workspace.Root);

        var preview = facade.PreviewAnalyze(workspace.Root, FixedTime);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Ready, preview.Status);
        CollectionAssert.AreEqual(beforeFiles, SnapshotFiles(workspace.Root));
        Assert.IsNotNull(preview.Preview);

        var execute = facade.ExecuteAnalyze(preview.Preview!);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, execute.Status);
        Assert.IsNotNull(execute.Overview);
        var committedProject = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.IsTrue(committedProject.Outputs.Count >= 3);
        Assert.IsTrue(committedProject.Outputs.Values.All(path =>
            File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, path))));

        var staleExecute = facade.ExecuteAnalyze(preview.Preview!);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Stale, staleExecute.Status);
        StringAssert.Contains(staleExecute.Message, "stale-workspace-revision");
        AssertNoAbsolutePathsIn(execute.Message);
    }

    [TestMethod]
    public void ExecuteAnalyze_reports_recovery_required_for_lock_and_active_authority()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);
        var importPreview = facade.PreviewImportSearch(new DesktopImportSearchRequest(
            workspace.Root, sourcePath, "scopus", "csv", "search-001", null, FixedTime));
        Assert.IsTrue(facade.ExecuteImportSearch(importPreview.Preview!).Completed);

        var lockPreview = facade.PreviewAnalyze(workspace.Root, FixedTime).Preview!;
        using (HoldWorkspaceLock(workspace.Root))
        {
            var locked = facade.ExecuteAnalyze(lockPreview);
            Assert.AreEqual(DesktopWorkspaceCommandStatus.RecoveryRequired, locked.Status);
        }

        var authorityPreview = facade.PreviewAnalyze(workspace.Root, FixedTime).Preview!;
        ActivateAuthority(workspace);
        var activeAuthority = facade.ExecuteAnalyze(authorityPreview);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.RecoveryRequired, activeAuthority.Status);
    }

    [TestMethod]
    public void ExecuteInitialize_does_not_write_ui_preview_fields_into_project_file()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        using var workspace = new TemporaryDirectory();

        var preview = facade.PreviewInitialize(new DesktopInitializeRequest(
            workspace.Path, "Desktop ui fields", null, FixedTime));
        var executeResult = facade.ExecuteInitialize(preview.Preview!);

        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, executeResult.Status);
        var json = File.ReadAllText(ResearchWorkspacePaths.ProjectFile(workspace.Path));
        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().Select(item => item.Name).ToArray();

        string[] uiFields =
        {
            "commandKind",
            "workspaceDirectory",
            "expectedProjectRevision",
            "sourcePath",
            "sourceDigest",
            "requestedWorkspaceId",
            "source",
            "format",
            "inputId",
            "query",
            "occurredAt",
            "expectedEffects",
            "confirmationToken"
        };

        foreach (var uiField in uiFields)
        {
            CollectionAssert.DoesNotContain(properties, uiField);
        }
    }

    [TestMethod]
    public void Command_messages_do_not_contain_absolute_paths()
    {
        var facade = new DesktopWorkspaceCommandFacade();
        var missingRoot = Path.Combine(Path.GetTempPath(), $"nexus-desktop-appservices-abs-{Guid.NewGuid():N}");

        AssertNoAbsolutePathsIn(facade.OpenWorkspace(missingRoot).Message);

        using var workspace = TemporaryInitializedWorkspace.Create();
        var sourcePath = CreateSourceFile(workspace.Root, "search.csv", ScopusCsv);
        var importResult = facade.ExecuteImportSearch(
            facade.PreviewImportSearch(new DesktopImportSearchRequest(
                workspace.Root,
                sourcePath,
                "scopus",
                "csv",
                "search-001",
                null,
                FixedTime)).Preview!);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Succeeded, importResult.Status);

        var analyzePreview = facade.PreviewAnalyze(workspace.Root, FixedTime);
        Assert.AreEqual(DesktopWorkspaceCommandStatus.Ready, analyzePreview.Status);
        AssertNoAbsolutePathsIn(analyzePreview.Message);
    }

    private static readonly DateTimeOffset FixedTime = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly string ScopusCsv = """
        eid,title,author names,year,source title,doi
        2-s2.0-pr03-001,"Rayyan: a web and mobile app for systematic reviews","Ouzzani M; Hammady H; Fedorowicz Z; Elmagarmid A",2016,Systematic Reviews,10.1186/s13643-016-0384-4
        2-s2.0-pr03-002,"ASReview: active learning for systematic reviews","van de Schoot R; de Bruin J; Schram R",2021,Nature Machine Intelligence,10.1038/s42256-020-00287-7

        """;
    private static readonly string WarningFixtureContent = File.ReadAllText(Path.Combine(RepositoryRoot(), "tests", "NexusScholar.Cli.Tests", "Fixtures", "ResearchWorkspaceImportSearch", "pr03-warning-missing-title.ris"));

    private static string ComputeSha256(byte[] bytes) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";

    private static string CreateSourceFile(string root, string fileName, string content)
    {
        var path = Path.Combine(root, fileName);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }

    private static FileStream HoldWorkspaceLock(string root) => new(
        Path.Combine(root, ResearchWorkspacePaths.ProjectLockFileName),
        FileMode.OpenOrCreate,
        FileAccess.ReadWrite,
        FileShare.None);

    private static void ActivateAuthority(TemporaryInitializedWorkspace workspace)
    {
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath) with
        {
            CurrentAuthorityGenerationId = "authority-active",
            AuthorityGenerationManifestPath = "nexus-output/authority-generations/authority-active/manifest.json",
            AuthorityGenerationManifestSha256 = ComputeSha256(Encoding.UTF8.GetBytes("authority-active"))
        };
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);
    }

    private static string[] SnapshotFiles(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

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

    private static void AssertNoAbsolutePathsIn(string message)
    {
        Assert.IsFalse(Regex.IsMatch(message, "[A-Za-z]:\\\\"));
        Assert.IsFalse(Regex.IsMatch(message, "[A-Za-z]:/"));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexus-desktop-appservices-tests-{Guid.NewGuid():N}");
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

    private sealed class TemporaryInitializedWorkspace : IDisposable
    {
        private TemporaryInitializedWorkspace(string root)
        {
            Root = root;
            Project = ResearchWorkspaceProject.Create("APP-01 service test", FixedTime);
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

        public static TemporaryInitializedWorkspace Create()
        {
            var root = Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nexus-desktop-appservices-inited-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TemporaryInitializedWorkspace(root);
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
