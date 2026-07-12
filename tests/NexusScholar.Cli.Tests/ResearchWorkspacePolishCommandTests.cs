using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Cli;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class ResearchWorkspacePolishCommandTests
{
    private const string QueryText = "systematic review screening software";
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Status_finds_workspace_from_child_folder_without_printing_absolute_paths()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        var child = Path.Combine(workspace.Root, "nested", "child");
        Directory.CreateDirectory(child);

        var exitCode = RunCli(child, new[] { "status" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "State: initialized");
        StringAssert.Contains(output, "Project location: parent workspace");
        AssertNoAbsoluteWorkspacePath(workspace.Root, output, error);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void Status_reports_imported_state_after_clean_import()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportWos(workspace.Root, "search-001");

        var exitCode = RunCli(workspace.Root, new[] { "status" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "State: imported");
        StringAssert.Contains(output, "  search exports: 1");
        StringAssert.Contains(output, "  parser warnings: 0");
        StringAssert.Contains(output, "  import traces: 1");
        StringAssert.Contains(output, "Next: nexus verify");
        AssertNoAbsoluteWorkspacePath(workspace.Root, output, error);
    }

    [TestMethod]
    public void Status_reports_imported_with_warnings_after_warning_import()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportScopus(workspace.Root);

        var exitCode = RunCli(workspace.Root, new[] { "status" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "State: imported-with-warnings");
        StringAssert.Contains(output, "  parser warnings: 2");
        StringAssert.Contains(output, "  skipped records: 0");
        StringAssert.Contains(output, "Next: nexus verify");
        AssertNoAbsoluteWorkspacePath(workspace.Root, output, error);
    }

    [TestMethod]
    public void Status_reports_analyzed_after_audit_analysis()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportWos(workspace.Root, "search-001");
        Assert.AreEqual(0, RunCli(workspace.Root, new[] { "analyze" }, out _, out var analyzeError), analyzeError);

        var exitCode = RunCli(workspace.Root, new[] { "status" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "State: analyzed");
        StringAssert.Contains(output, "  dedup analysis: present");
        StringAssert.Contains(output, "  workspace plan: present");
        StringAssert.Contains(output, "  review report: present");
        StringAssert.Contains(output, "Next: nexus clusters");
        AssertNoAbsoluteWorkspacePath(workspace.Root, output, error);
    }

    [TestMethod]
    public void Status_reports_review_ready_after_combined_analysis()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportCombinedBundle(workspace.Root);
        Assert.AreEqual(0, RunCli(workspace.Root, new[] { "analyze" }, out _, out var analyzeError), analyzeError);

        var exitCode = RunCli(workspace.Root, new[] { "status" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "State: review-ready");
        AssertLineValueAtLeast(output, "  exact duplicate clusters:", 1);
        AssertLineValueAtLeast(output, "  review-required candidates:", 1);
        AssertLineValueAtLeast(output, "  blocking merge gates:", 1);
        StringAssert.Contains(output, "Next: nexus review");
        AssertNoAbsoluteWorkspacePath(workspace.Root, output, error);
    }

    [TestMethod]
    public void Status_reports_needs_attention_and_digest_exit_code_for_changed_input()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportScopus(workspace.Root);
        File.AppendAllText(FirstInputPath(workspace.Root), "changed");

        var exitCode = RunCli(workspace.Root, new[] { "status" }, out var output, out var error);

        Assert.AreEqual(3, exitCode);
        StringAssert.Contains(output, "State: needs-attention");
        StringAssert.Contains(output, "Attention:");
        StringAssert.Contains(output, "  Digest mismatches: 1");
        StringAssert.Contains(output, "Next: restore the changed file or re-import intentionally.");
        AssertNoAbsoluteWorkspacePath(workspace.Root, output, error);
    }

    [TestMethod]
    public void Analyze_returns_digest_mismatch_exit_code_for_changed_input()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportScopus(workspace.Root);
        File.AppendAllText(FirstInputPath(workspace.Root), "changed");

        var exitCode = RunCli(workspace.Root, new[] { "analyze" }, out var output, out var error);

        Assert.AreEqual(3, exitCode);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "Input digest mismatch: inputs/search/search-001/source.csv");
        AssertNoAbsoluteWorkspacePath(workspace.Root, output, error);
    }

    [TestMethod]
    public void Clusters_returns_missing_input_exit_code_when_dedup_result_is_missing()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportCombinedBundle(workspace.Root);
        Assert.AreEqual(0, RunCli(workspace.Root, new[] { "analyze" }, out _, out var analyzeError), analyzeError);
        using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json"))))
        {
            var relativePath = document.RootElement.GetProperty("outputs").GetProperty("deduplicationResult").GetString()!;
            File.Delete(Path.Combine(workspace.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        var exitCode = RunCli(workspace.Root, new[] { "clusters" }, out var output, out var error);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "Generation artifact 'deduplicationResult'");
        StringAssert.Contains(error, "Run: nexus analyze");
        AssertNoAbsoluteWorkspacePath(workspace.Root, output, error);
    }

    private static void ImportCombinedBundle(string workspaceRoot)
    {
        ImportSearch(workspaceRoot, "search-001", "scopus", "csv", CombinedBundlePath("combined_scopus_like.csv"));
        ImportSearch(workspaceRoot, "search-002", "web-of-science", "ris", CombinedBundlePath("combined_wos_like.ris"));
        ImportSearch(workspaceRoot, "search-003", "google-scholar", "bibtex", CombinedBundlePath("combined_scholar_style.bib"));
        ImportSearch(workspaceRoot, "search-004", "web-of-science", "csv", CombinedBundlePath("combined_wos_like_source_specific.csv"));
    }

    private static string FirstInputPath(string workspaceRoot)
    {
        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspaceRoot, "nexus.project.json")));
        var relativePath = project.RootElement.GetProperty("inputs")[0].GetProperty("relativePath").GetString()!;
        return Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void ImportScopus(string workspaceRoot)
    {
        ImportSearch(workspaceRoot, "search-001", "scopus", "csv", ImportFixturePath("pr03-scopus-small.csv"));
    }

    private static void ImportWos(string workspaceRoot, string queryId)
    {
        ImportSearch(workspaceRoot, queryId, "web-of-science", "ris", ImportFixturePath("pr03-wos-small.ris"));
    }

    private static void ImportSearch(string workspaceRoot, string queryId, string source, string format, string path)
    {
        var exitCode = RunCli(
            workspaceRoot,
            new[] { "import", "search", path, "--source", source, "--format", format, "--query-id", queryId, "--query", QueryText },
            out _,
            out var error);
        Assert.AreEqual(0, exitCode, error);
    }

    private static int RunCli(string workingDirectory, string[] args, out string output, out string error)
    {
        using var outputWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        var exitCode = CliApplication.Run(
            args,
            outputWriter,
            errorWriter,
            workingDirectory,
            () => FixedNow);

        output = outputWriter.ToString();
        error = errorWriter.ToString();
        return exitCode;
    }

    private static string ImportFixturePath(string fileName)
    {
        return Path.Combine(RepositoryRoot(), "tests", "NexusScholar.Cli.Tests", "Fixtures", "ResearchWorkspaceImportSearch", fileName);
    }

    private static string CombinedBundlePath(string fileName)
    {
        return Path.Combine(
            RepositoryRoot(),
            "tests",
            "NexusScholar.AppServices.Tests",
            "Fixtures",
            "App01GeneratedLocalBundles",
            "bundles",
            "FB07-combined-app01-demo",
            fileName);
    }

    private static void AssertNoAbsoluteWorkspacePath(string workspaceRoot, string output, string error)
    {
        Assert.IsFalse(output.Contains(workspaceRoot, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(error.Contains(workspaceRoot, StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertLineValueAtLeast(string output, string label, int minimum)
    {
        var line = output
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Single(item => item.StartsWith(label, StringComparison.Ordinal));
        var value = int.Parse(line[label.Length..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsTrue(value >= minimum, $"{label} expected at least {minimum}, got {value}.");
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NexusScholar.Core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string root) => Root = root;

        public string Root { get; }

        public static TemporaryWorkspace Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nexus-cli-polish-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TemporaryWorkspace(root);
        }

        public static TemporaryWorkspace CreateInitialized()
        {
            var workspace = Create();
            var exitCode = RunCli(
                workspace.Root,
                new[] { "init", "--title", "AI screening tools review" },
                out _,
                out var error);
            Assert.AreEqual(0, exitCode, error);
            return workspace;
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
