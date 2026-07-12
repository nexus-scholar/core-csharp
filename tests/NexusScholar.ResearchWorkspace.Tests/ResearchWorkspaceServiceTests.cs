using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.ResearchWorkspace;
using NexusScholar.UiContracts;

namespace NexusScholar.ResearchWorkspace.Tests;

[TestClass]
public sealed class ResearchWorkspaceServiceTests
{
    [TestMethod]
    public void Store_finds_workspace_from_child_folder()
    {
        using var workspace = TemporaryWorkspace.Create();
        var child = Path.Combine(workspace.Root, "inputs", "search");
        Directory.CreateDirectory(child);

        var found = ResearchWorkspaceStore.FindFrom(child);

        Assert.IsNotNull(found);
        Assert.AreEqual(Path.GetFullPath(workspace.Root), Path.GetFullPath(found.RootDirectory));
        Assert.AreEqual(Path.Combine(workspace.Root, ResearchWorkspacePaths.ProjectFileName), found.ProjectFilePath);
    }

    [TestMethod]
    public void Verifier_reports_digest_mismatch_with_project_relative_path()
    {
        using var workspace = TemporaryWorkspace.Create();
        var relativePath = $"{ResearchWorkspacePaths.SearchInputs}/search-001-scopus.csv";
        var fullPath = ResearchWorkspacePaths.InProject(workspace.Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "first version", Encoding.UTF8);
        var originalDigest = Sha256(File.ReadAllBytes(fullPath));
        File.WriteAllText(fullPath, "changed version", Encoding.UTF8);

        var project = workspace.Project.WithInput(new ResearchWorkspaceInput
        {
            InputId = "search-001",
            Kind = "search-export",
            Source = "scopus",
            Format = "csv",
            RelativePath = relativePath,
            Sha256 = originalDigest,
            QueryId = "search-001"
        });

        var report = ResearchWorkspaceVerifier.Verify(workspace.Location, project);

        CollectionAssert.AreEqual(new[] { relativePath }, report.DigestMismatches.ToArray());
        Assert.IsFalse(report.DigestMismatches[0].Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(0, report.FilesUnchanged);
    }

    [TestMethod]
    public void Analyzer_composes_workspace_plan_from_local_search_exports()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = AddSearchExport(workspace, workspace.Project, "search-001", "scopus", "csv", ScopusCsv);

        var result = ResearchWorkspaceAnalyzer.Analyze(workspace.Location, project);

        Assert.AreEqual(1, result.ImportTraces.Count);
        Assert.AreEqual(2, result.ImportedRecordCount);
        Assert.AreEqual(BlockSourceKind.AppProjection, result.WorkspacePlan.Blocks[0].SourceKind);
        Assert.IsTrue(result.WorkspacePlan.Blocks.All(block => block.SourceKind == BlockSourceKind.AppProjection));
        Assert.IsTrue(result.WorkspacePlan.Blocks.Count > 0);
    }

    [TestMethod]
    public void Workflow_verify_action_does_not_write_generated_outputs()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = AddSearchExport(workspace, workspace.Project, "search-001", "scopus", "csv", ScopusCsv);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);
        var beforeFiles = SnapshotFiles(workspace.Root);

        var result = ResearchWorkspaceWorkflowActions.Verify(workspace.Root);
        var afterFiles = SnapshotFiles(workspace.Root);

        Assert.IsTrue(result.Completed);
        Assert.IsTrue(result.RequiresAttention);
        Assert.AreEqual(ResearchWorkspaceExitCodes.MissingProjectOrInput, result.ExitCode);
        Assert.IsTrue(result.Message.Contains("Workspace verification", StringComparison.Ordinal));
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.DeduplicationResultPath)));
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.WorkspacePlanPath)));
        Assert.IsFalse(File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspaceAnalyzer.ReviewReportPath)));
        CollectionAssert.AreEqual(beforeFiles, afterFiles);
    }

    [TestMethod]
    public void Workflow_analyze_action_persists_outputs_and_project_references()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = AddSearchExport(workspace, workspace.Project, "search-001", "scopus", "csv", ScopusCsv);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);

        var result = ResearchWorkspaceWorkflowActions.Analyze(workspace.Root);
        var updatedProject = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        Assert.IsTrue(result.Completed);
        Assert.IsFalse(result.RequiresAttention);
        Assert.AreEqual(ResearchWorkspaceExitCodes.Success, result.ExitCode);
        Assert.IsTrue(updatedProject.Outputs.Values.All(path => File.Exists(ResearchWorkspacePaths.InProject(workspace.Root, path))));
        Assert.AreEqual(1L, updatedProject.Revision);
        Assert.IsNotNull(updatedProject.CurrentGenerationId);
        Assert.IsNotNull(ResearchWorkspaceGenerationVerifier.VerifyCurrent(workspace.Location, updatedProject));
        Assert.IsFalse(result.Message.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Store_rejects_duplicate_inputs_and_malformed_digest()
    {
        using var workspace = TemporaryWorkspace.Create();
        var input = InputFor("search-001", "scopus", "csv", "inputs/search/source.csv", Array.Empty<byte>());
        var malformed = workspace.Project with
        {
            Inputs = new[] { input, input with { Sha256 = "not-a-digest" } }
        };

        Assert.ThrowsExactly<JsonException>(() => ResearchWorkspaceStore.WriteProject(workspace.Location, malformed));
    }

    [TestMethod]
    public void Transaction_rejects_stale_project_revision_and_preserves_current_generation()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = AddSearchExport(workspace, workspace.Project, "search-001", "scopus", "csv", ScopusCsv);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);
        var first = ResearchWorkspaceTransaction.AnalyzeAndCommit(workspace.Location, project);

        Assert.ThrowsExactly<ResearchWorkspaceConcurrencyException>(() =>
            ResearchWorkspaceTransaction.AnalyzeAndCommit(workspace.Location, project));

        var current = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.AreEqual(first.Project.CurrentGenerationId, current.CurrentGenerationId);
        Assert.IsNotNull(ResearchWorkspaceGenerationVerifier.VerifyCurrent(workspace.Location, current));
    }

    [TestMethod]
    public void Generation_verifier_rejects_corrupt_output()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = AddSearchExport(workspace, workspace.Project, "search-001", "scopus", "csv", ScopusCsv);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);
        var commit = ResearchWorkspaceTransaction.AnalyzeAndCommit(workspace.Location, project);
        File.AppendAllText(ResearchWorkspacePaths.InProject(workspace.Root, commit.Project.Outputs["workspacePlan"]), "corrupt");

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceGenerationVerifier.VerifyCurrent(workspace.Location, commit.Project));
    }

    [TestMethod]
    public void Path_resolver_rejects_reparse_point_ancestors()
    {
        using var workspace = TemporaryWorkspace.Create();
        var external = Path.Combine(Path.GetTempPath(), $"nexus-rw-external-{Guid.NewGuid():N}");
        Directory.CreateDirectory(external);
        var link = Path.Combine(workspace.Root, "linked");
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{external}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                process!.WaitForExit();
                Assert.AreEqual(0, process.ExitCode);
            }
            else
            {
                Directory.CreateSymbolicLink(link, external);
            }
            Assert.IsFalse(ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(workspace.Root, "linked/file.txt", out _));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
            Directory.Delete(external, recursive: true);
        }
    }

    [TestMethod]
    public void Read_models_report_initialized_workspace_without_absolute_paths()
    {
        using var workspace = TemporaryWorkspace.Create();

        var model = ResearchWorkspaceReadModelBuilder.Build(workspace.Root);

        Assert.AreEqual(WorkspaceState.Initialized, model.State);
        Assert.AreEqual("current folder", model.ProjectLocation);
        Assert.AreEqual(0, model.Imports.Count);
        Assert.IsTrue(model.WorkflowSteps.Any(step => step.StepId == "import" && step.State == "current"));
        AssertDoesNotContainWorkspaceRoot(model, workspace.Root);
    }

    [TestMethod]
    public void Read_models_report_digest_mismatch_with_project_relative_attention()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = AddSearchExport(workspace, workspace.Project, "search-001", "scopus", "csv", ScopusCsv);
        var relativePath = project.Inputs[0].EffectiveRelativePath;
        File.WriteAllText(ResearchWorkspacePaths.InProject(workspace.Root, relativePath), "changed bytes", Encoding.UTF8);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);

        var model = ResearchWorkspaceReadModelBuilder.Build(workspace.Root);

        Assert.AreEqual(WorkspaceState.NeedsAttention, model.State);
        Assert.AreEqual(1, model.Verification.DigestMismatchCount);
        Assert.IsTrue(model.AttentionItems.Any(item =>
            item.Code == "digest-mismatch" &&
            item.Target == relativePath));
        AssertDoesNotContainWorkspaceRoot(model, workspace.Root);
    }

    [TestMethod]
    public void Read_models_surface_review_ready_workspace_and_locked_actions()
    {
        using var workspace = TemporaryWorkspace.Create();
        var project = workspace.Project;
        project = AddBundleFile(workspace, project, "search-001", "scopus", "csv", "combined_scopus_like.csv");
        project = AddBundleFile(workspace, project, "search-002", "web-of-science", "ris", "combined_wos_like.ris");
        project = AddBundleFile(workspace, project, "search-003", "google-scholar", "bibtex", "combined_scholar_style.bib");
        project = AddBundleFile(workspace, project, "search-004", "other", "csv", "combined_wos_like_source_specific.csv");
        AnalyzeAndPersist(workspace, project);

        var model = ResearchWorkspaceReadModelBuilder.Build(workspace.Root);

        Assert.AreEqual(WorkspaceState.ReviewReady, model.State);
        Assert.IsTrue(model.EvidenceRecords.Count >= 10);
        Assert.IsTrue(model.DuplicateClusters.Count > 0);
        Assert.IsTrue(model.ReviewQueue.Count > 0);
        Assert.IsTrue(model.DuplicateCandidateDetails.Count > 0);
        Assert.IsTrue(model.LockedDecisionActions.Count > 0);
        Assert.IsTrue(model.LockedDecisionActions.All(action => !action.IsExecutable));
        Assert.IsTrue(model.LockedDecisionActions.All(action => action.CommandKind is null));
        Assert.IsTrue(model.LockedDecisionActions.All(action => action.Label.Contains("locked", StringComparison.OrdinalIgnoreCase)));
        AssertDoesNotContainWorkspaceRoot(model, workspace.Root);
    }

    private const string ScopusCsv = """
eid,title,author names,year,source title,doi
2-s2.0-pr03-001,"Rayyan: a web and mobile app for systematic reviews","Ouzzani M; Hammady H; Fedorowicz Z; Elmagarmid A",2016,Systematic Reviews,10.1186/s13643-016-0384-4
2-s2.0-pr03-002,"ASReview: active learning for systematic reviews","van de Schoot R; de Bruin J; Schram R",2021,Nature Machine Intelligence,10.1038/s42256-020-00287-7

""";

    private static ResearchWorkspaceProject AddSearchExport(
        TemporaryWorkspace workspace,
        ResearchWorkspaceProject project,
        string inputId,
        string source,
        string format,
        string content)
    {
        var relativePath = $"{ResearchWorkspacePaths.SearchInputs}/{inputId}-{source}.{SearchImportAliases.ExtensionFor(format)}";
        var fullPath = ResearchWorkspacePaths.InProject(workspace.Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content, Encoding.UTF8);
        return project.WithInput(InputFor(inputId, source, format, relativePath, File.ReadAllBytes(fullPath)));
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
            QueryText = "read-model test",
            ImportTracePath = $"{ResearchWorkspacePaths.ImportOutputs}/{inputId}.import-trace.json"
        };
    }

    private static void AnalyzeAndPersist(TemporaryWorkspace workspace, ResearchWorkspaceProject project)
    {
        var result = ResearchWorkspaceAnalyzer.Analyze(workspace.Location, project);
        foreach (var trace in result.ImportTraces)
        {
            var inputId = trace.TraceId.EndsWith(".import-trace", StringComparison.Ordinal)
                ? trace.TraceId[..^".import-trace".Length]
                : trace.TraceId;
            ResearchWorkspaceJson.WriteJsonFile(
                ResearchWorkspacePaths.InProject(workspace.Root, $"{ResearchWorkspacePaths.ImportOutputs}/{inputId}.import-trace.json"),
                trace);
        }

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

    private static void AssertDoesNotContainWorkspaceRoot(WorkspaceOverviewReadModel model, string workspaceRoot)
    {
        var json = JsonSerializer.Serialize(model);
        Assert.IsFalse(json.Contains(workspaceRoot, StringComparison.OrdinalIgnoreCase), json);
    }

    private static string[] SnapshotFiles(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
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
                "APP-01 service test",
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
            var root = Path.Combine(Path.GetTempPath(), $"nexus-rw-tests-{Guid.NewGuid():N}");
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
