using System.Security.Cryptography;
using System.Text;
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
        var relativePath = $"{ResearchWorkspacePaths.SearchInputs}/search-001-scopus.csv";
        var fullPath = ResearchWorkspacePaths.InProject(workspace.Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, ScopusCsv, Encoding.UTF8);
        var sourceBytes = File.ReadAllBytes(fullPath);
        var project = workspace.Project.WithInput(new ResearchWorkspaceInput
        {
            InputId = "search-001",
            Kind = "search-export",
            Source = "scopus",
            Format = "csv",
            RelativePath = relativePath,
            Sha256 = Sha256(sourceBytes),
            QueryId = "search-001",
            QueryText = "systematic review software"
        });

        var result = ResearchWorkspaceAnalyzer.Analyze(workspace.Location, project);

        Assert.AreEqual(1, result.ImportTraces.Count);
        Assert.AreEqual(2, result.ImportedRecordCount);
        Assert.AreEqual(BlockSourceKind.AppProjection, result.WorkspacePlan.Blocks[0].SourceKind);
        Assert.IsTrue(result.WorkspacePlan.Blocks.All(block => block.SourceKind == BlockSourceKind.AppProjection));
        Assert.IsTrue(result.WorkspacePlan.Blocks.Count > 0);
    }

    private const string ScopusCsv = """
eid,title,author names,year,source title,doi
2-s2.0-pr03-001,"Rayyan: a web and mobile app for systematic reviews","Ouzzani M; Hammady H; Fedorowicz Z; Elmagarmid A",2016,Systematic Reviews,10.1186/s13643-016-0384-4
2-s2.0-pr03-002,"ASReview: active learning for systematic reviews","van de Schoot R; de Bruin J; Schram R",2021,Nature Machine Intelligence,10.1038/s42256-020-00287-7

""";

    private static string Sha256(byte[] bytes)
    {
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
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
