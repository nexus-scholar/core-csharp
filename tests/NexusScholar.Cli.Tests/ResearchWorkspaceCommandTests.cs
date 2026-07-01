using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Cli;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class ResearchWorkspaceCommandTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Init_creates_project_file_and_expected_directories()
    {
        using var workspace = TemporaryWorkspace.Create();

        var exitCode = RunCli(
            workspace.Root,
            new[] { "init", "--title", "AI screening tools review" },
            out var output,
            out var error);

        Assert.AreEqual(0, exitCode, error);
        Assert.AreEqual(ExpectedInitOutput, output);
        Assert.AreEqual(string.Empty, error);

        Assert.IsTrue(File.Exists(Path.Combine(workspace.Root, "nexus.project.json")));
        Assert.IsTrue(Directory.Exists(Path.Combine(workspace.Root, "inputs", "search")));
        Assert.IsTrue(Directory.Exists(Path.Combine(workspace.Root, "nexus-output", "imports")));
        Assert.IsTrue(Directory.Exists(Path.Combine(workspace.Root, "nexus-output", "dedup")));
        Assert.IsTrue(Directory.Exists(Path.Combine(workspace.Root, "nexus-output", "workspace")));
        Assert.IsTrue(Directory.Exists(Path.Combine(workspace.Root, "nexus-output", "reports")));

        var json = File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json"));
        Assert.IsFalse(json.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(ExpectedProjectJson, json);
    }

    [TestMethod]
    public void Init_returns_nonzero_when_project_already_exists()
    {
        using var workspace = TemporaryWorkspace.Create();

        var firstExit = RunCli(
            workspace.Root,
            new[] { "init", "--title", "AI screening tools review" },
            out _,
            out var firstError);
        Assert.AreEqual(0, firstExit, firstError);

        var secondExit = RunCli(
            workspace.Root,
            new[] { "init", "--title", "AI screening tools review" },
            out var output,
            out var error);

        Assert.AreEqual(1, secondExit);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(
            "A Nexus research workspace already exists in this folder." + Environment.NewLine +
            "Project file: nexus.project.json" + Environment.NewLine +
            "Run: nexus status" + Environment.NewLine,
            error);
    }

    [TestMethod]
    public void Init_requires_non_empty_title()
    {
        using var workspace = TemporaryWorkspace.Create();

        var exitCode = RunCli(
            workspace.Root,
            new[] { "init", "--title", "   " },
            out var output,
            out var error);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(
            "Missing required option: --title" + Environment.NewLine +
            "Usage: nexus init --title \"<research title>\"" + Environment.NewLine,
            error);
        Assert.IsFalse(File.Exists(Path.Combine(workspace.Root, "nexus.project.json")));
    }

    [TestMethod]
    public void Status_reports_initialized_project_without_mutating_files()
    {
        using var workspace = TemporaryWorkspace.Create();

        var initExit = RunCli(
            workspace.Root,
            new[] { "init", "--title", "AI screening tools review" },
            out _,
            out var initError);
        Assert.AreEqual(0, initExit, initError);

        var projectPath = Path.Combine(workspace.Root, "nexus.project.json");
        var before = File.ReadAllText(projectPath);

        var statusExit = RunCli(
            workspace.Root,
            new[] { "status" },
            out var output,
            out var error);

        Assert.AreEqual(0, statusExit, error);
        Assert.AreEqual(ExpectedStatusOutput, output);
        Assert.AreEqual(string.Empty, error);
        Assert.AreEqual(before, File.ReadAllText(projectPath));
    }

    [TestMethod]
    public void Status_returns_missing_project_exit_code_when_not_initialized()
    {
        using var workspace = TemporaryWorkspace.Create();

        var exitCode = RunCli(
            workspace.Root,
            new[] { "status" },
            out var output,
            out var error);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(string.Empty, output);
        Assert.AreEqual(
            "No Nexus research workspace found in the current folder." + Environment.NewLine +
            "Run: nexus init --title \"<research title>\"" + Environment.NewLine,
            error);
    }

    [TestMethod]
    public void Status_returns_unsupported_schema_for_wrong_schema()
    {
        using var workspace = TemporaryWorkspace.Create();
        File.WriteAllText(
            Path.Combine(workspace.Root, "nexus.project.json"),
            """
            {
              "schema": "nexus.project.v999",
              "workspaceId": "workspace-ai-screening-tools-review",
              "title": "AI screening tools review",
              "createdAt": "2026-07-01T00:00:00Z",
              "inputs": [],
              "outputs": {},
              "nonClaims": []
            }
            """);

        var exitCode = RunCli(
            workspace.Root,
            new[] { "status" },
            out var output,
            out var error);

        Assert.AreEqual(4, exitCode);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "Unsupported Nexus project schema");
    }

    [TestMethod]
    public void Status_returns_malformed_project_for_missing_required_fields()
    {
        using var workspace = TemporaryWorkspace.Create();
        File.WriteAllText(
            Path.Combine(workspace.Root, "nexus.project.json"),
            """
            {
              "schema": "nexus.project.v0"
            }
            """);

        var exitCode = RunCli(
            workspace.Root,
            new[] { "status" },
            out var output,
            out var error);

        Assert.AreEqual(4, exitCode);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "Malformed Nexus project file");
    }

    [TestMethod]
    public void Status_does_not_print_absolute_working_directory()
    {
        using var workspace = TemporaryWorkspace.Create();

        var initExit = RunCli(
            workspace.Root,
            new[] { "init", "--title", "AI screening tools review" },
            out _,
            out var initError);
        Assert.AreEqual(0, initExit, initError);

        var statusExit = RunCli(
            workspace.Root,
            new[] { "status" },
            out var output,
            out var error);

        Assert.AreEqual(0, statusExit, error);
        Assert.IsFalse(output.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(error.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Usage_includes_init_and_status()
    {
        StringAssert.Contains(CliApplication.Usage, "init");
        StringAssert.Contains(CliApplication.Usage, "status");
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

    private static readonly string ExpectedInitOutput = JoinLines(
        "Nexus research workspace initialized",
        "Project: AI screening tools review",
        "Workspace: workspace-ai-screening-tools-review",
        "Project file: nexus.project.json",
        "Inputs: inputs/search",
        "Outputs:",
        "  imports: nexus-output/imports",
        "  dedup: nexus-output/dedup",
        "  workspace: nexus-output/workspace",
        "  reports: nexus-output/reports",
        "Next: nexus status");

    private static readonly string ExpectedStatusOutput = JoinLines(
        "Nexus research workspace",
        "Status: initialized",
        "Project: AI screening tools review",
        "Workspace: workspace-ai-screening-tools-review",
        "Inputs:",
        "  search exports: 0",
        "Outputs:",
        "  import traces: 0",
        "  dedup analysis: missing",
        "  workspace plan: missing",
        "  reports: 0",
        "Next: nexus import search <file> --source <source> --format <format>");

    private static readonly string ExpectedProjectJson = """
        {
          "schema": "nexus.project.v0",
          "workspaceId": "workspace-ai-screening-tools-review",
          "title": "AI screening tools review",
          "createdAt": "2026-07-01T00:00:00Z",
          "inputs": [],
          "outputs": {},
          "nonClaims": [
            "local-folder-project",
            "no-live-providers",
            "no-cloud-sync",
            "no-database"
          ]
        }
        """ + "\n";

    private static string JoinLines(params string[] lines)
    {
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string root) => Root = root;

        public string Root { get; }

        public static TemporaryWorkspace Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nexus-cli-tests",
                Guid.NewGuid().ToString("N"));
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
