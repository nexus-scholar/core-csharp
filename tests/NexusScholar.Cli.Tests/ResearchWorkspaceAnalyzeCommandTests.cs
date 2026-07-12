using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Cli;
using NexusScholar.UiContracts;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class ResearchWorkspaceAnalyzeCommandTests
{
    private const string QueryText = "systematic review screening software";
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Analyze_requires_initialized_workspace()
    {
        using var workspace = TemporaryWorkspace.Create();

        var exitCode = RunCli(workspace.Root, new[] { "analyze" }, out var output, out var error);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "No Nexus research workspace found");
    }

    [TestMethod]
    public void Analyze_requires_at_least_one_imported_search_input()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();

        var exitCode = RunCli(workspace.Root, new[] { "analyze" }, out var output, out var error);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "requires at least one imported search export");
    }

    [TestMethod]
    public void Analyze_writes_dedup_result_workspace_plan_report_and_updates_project()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportWos(workspace.Root, "search-001", WosSmallFixture());

        var exitCode = RunCli(workspace.Root, new[] { "analyze" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "Workspace analysis complete");
        StringAssert.Contains(output, "Mode: Audit");
        StringAssert.Contains(output, "Import traces: 1");
        StringAssert.Contains(output, "Exact duplicate clusters: 0");
        StringAssert.Contains(output, "Review-required duplicate candidates: 0");
        Assert.AreEqual(string.Empty, error);

        Assert.IsTrue(File.Exists(OutputPath(workspace.Root, "deduplicationResult")));
        Assert.IsTrue(File.Exists(OutputPath(workspace.Root, "workspacePlan")));
        Assert.IsTrue(File.Exists(OutputPath(workspace.Root, "reviewReport")));

        var projectJson = File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json"));
        StringAssert.Contains(projectJson, "\"currentGenerationId\": \"gen-");
        StringAssert.Contains(projectJson, "\"generationManifestPath\": \"nexus-output/generations/gen-");
    }

    [TestMethod]
    public void Analyze_combined_bundle_runs_dedup_and_composer()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportCombinedBundle(workspace.Root);

        var exitCode = RunCli(workspace.Root, new[] { "analyze" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        Assert.AreEqual(string.Empty, error);
        StringAssert.Contains(output, "Mode: Review");
        AssertSummaryAtLeast(output, "Import traces:", 4);
        AssertSummaryAtLeast(output, "Imported records:", 1);
        AssertSummaryAtLeast(output, "Parser warnings:", 1);
        AssertSummaryAtLeast(output, "Exact duplicate clusters:", 1);
        AssertSummaryAtLeast(output, "Review-required duplicate candidates:", 1);
        StringAssert.Contains(output, "WorkspacePlan: nexus-output/generations/gen-");
        StringAssert.Contains(output, "Deduplication result: nexus-output/generations/gen-");
        StringAssert.Contains(output, "Review report: nexus-output/generations/gen-");
    }

    [TestMethod]
    public void Analyze_uses_app_projection_only_and_descriptor_merge_actions()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportCombinedBundle(workspace.Root);
        Assert.AreEqual(0, RunCli(workspace.Root, new[] { "analyze" }, out _, out var error), error);

        var planJson = File.ReadAllText(OutputPath(workspace.Root, "workspacePlan"));
        var plan = JsonSerializer.Deserialize<WorkspacePlan>(planJson, UiContractJson.SerializerOptions);

        Assert.IsNotNull(plan);
        Assert.AreEqual(BlockMode.Review, plan.Mode);
        Assert.IsTrue(plan.Blocks.All(block => block.SourceKind == BlockSourceKind.AppProjection));
        CollectionAssert.IsSubsetOf(
            RequiredBlockKinds(),
            plan.Blocks.Select(block => block.Kind).Distinct(StringComparer.Ordinal).ToArray());
        Assert.IsTrue(plan.Blocks.Any(block =>
            block.Kind == KnownBlockKinds.HumanGateMergeDecision &&
            block.Actions.Count > 0 &&
            block.Actions.All(action => action.CommandKind is null)));
        Assert.IsFalse(planJson.Contains("\"Sample\"", StringComparison.Ordinal));
        Assert.IsFalse(planJson.Contains("nexus.command.dedup.accept-merge", StringComparison.Ordinal));
        Assert.IsFalse(planJson.Contains("nexus.command.dedup.reject-merge", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Analyze_does_not_emit_absolute_paths_or_current_timestamps()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        ImportCombinedBundle(workspace.Root);
        Assert.AreEqual(0, RunCli(workspace.Root, new[] { "analyze" }, out var output, out var error), error);

        var workspacePlanJson = File.ReadAllText(OutputPath(workspace.Root, "workspacePlan"));
        var dedupJson = File.ReadAllText(OutputPath(workspace.Root, "deduplicationResult"));
        var report = File.ReadAllText(OutputPath(workspace.Root, "reviewReport"));
        var combined = string.Join("\n", output, workspacePlanJson, dedupJson, report);

        Assert.IsFalse(combined.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("C:\\", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("/Users/", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains("/tmp/", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(combined.Contains(DateTimeOffset.UtcNow.ToString("O"), StringComparison.Ordinal));
    }

    [TestMethod]
    public void Usage_includes_analyze()
    {
        StringAssert.Contains(CliApplication.Usage, "analyze");
    }

    private static string OutputPath(string workspaceRoot, string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspaceRoot, "nexus.project.json")));
        var relativePath = document.RootElement.GetProperty("outputs").GetProperty(name).GetString();
        Assert.IsNotNull(relativePath);
        return Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void ImportCombinedBundle(string workspaceRoot)
    {
        ImportSearch(workspaceRoot, "search-001", "scopus", "csv", CombinedBundlePath("combined_scopus_like.csv"));
        ImportSearch(workspaceRoot, "search-002", "web-of-science", "ris", CombinedBundlePath("combined_wos_like.ris"));
        ImportSearch(workspaceRoot, "search-003", "google-scholar", "bibtex", CombinedBundlePath("combined_scholar_style.bib"));
        ImportSearch(workspaceRoot, "search-004", "web-of-science", "csv", CombinedBundlePath("combined_wos_like_source_specific.csv"));
    }

    private static void ImportWos(string workspaceRoot, string queryId, string path)
    {
        ImportSearch(workspaceRoot, queryId, "web-of-science", "ris", path);
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

    private static string WosSmallFixture()
    {
        return Path.Combine(RepositoryRoot(), "tests", "NexusScholar.Cli.Tests", "Fixtures", "ResearchWorkspaceImportSearch", "pr03-wos-small.ris");
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

    private static string[] RequiredBlockKinds() =>
        new[]
        {
            KnownBlockKinds.ImportSummary,
            KnownBlockKinds.ImportWarningSummary,
            KnownBlockKinds.DedupCandidateCluster,
            KnownBlockKinds.DedupRecordComparison,
            KnownBlockKinds.HumanGateMergeDecision
        };

    private static void AssertSummaryAtLeast(string output, string label, int minimum)
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
                "nexus-cli-analyze-tests",
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
