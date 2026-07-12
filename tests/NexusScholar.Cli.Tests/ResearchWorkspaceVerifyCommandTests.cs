using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Cli;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class ResearchWorkspaceVerifyCommandTests
{
    private const string QueryText = "systematic review screening software";
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Verify_returns_zero_for_valid_workspace()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        Assert.AreEqual(0, ImportWos(workspace.Root, "search-001", out _, out var importError), importError);

        var exitCode = RunCli(workspace.Root, new[] { "verify" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        AssertTextEqual(ExpectedPath("verify-valid.txt"), output);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void Verify_returns_nonzero_when_input_missing()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        Assert.AreEqual(0, ImportScopus(workspace.Root, out _, out var importError), importError);
        File.Delete(InputPath(workspace.Root, "relativePath"));

        var exitCode = RunCli(workspace.Root, new[] { "verify" }, out var output, out var error);

        Assert.AreEqual(2, exitCode);
        StringAssert.Contains(output, "Files missing: 1");
        StringAssert.Contains(output, "inputs/search/search-001/source.csv");
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void Verify_returns_nonzero_when_digest_changed()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        Assert.AreEqual(0, ImportScopus(workspace.Root, out _, out var importError), importError);
        File.AppendAllText(InputPath(workspace.Root, "relativePath"), "changed");

        var exitCode = RunCli(workspace.Root, new[] { "verify" }, out var output, out var error);

        Assert.AreEqual(3, exitCode);
        StringAssert.Contains(output, "Digest mismatches: 1");
        StringAssert.Contains(output, "inputs/search/search-001/source.csv");
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void Verify_summarizes_parser_warnings_without_failing()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        Assert.AreEqual(0, ImportScopus(workspace.Root, out _, out var importError), importError);

        var exitCode = RunCli(workspace.Root, new[] { "verify" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "Status: valid");
        StringAssert.Contains(output, "Parser warnings: 2");
        StringAssert.Contains(output, "Warning category: unknown-identifier-type (2)");
        StringAssert.Contains(output, "Skipped records: 0");
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void Verify_does_not_write_files_by_default()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        Assert.AreEqual(0, ImportWos(workspace.Root, "search-001", out _, out var importError), importError);
        var projectPath = Path.Combine(workspace.Root, "nexus.project.json");
        var tracePath = InputPath(workspace.Root, "importTracePath");
        var projectBefore = File.ReadAllText(projectPath);
        var traceBefore = File.ReadAllText(tracePath);

        var exitCode = RunCli(workspace.Root, new[] { "verify" }, out _, out var error);

        Assert.AreEqual(0, exitCode, error);
        Assert.AreEqual(projectBefore, File.ReadAllText(projectPath));
        Assert.AreEqual(traceBefore, File.ReadAllText(tracePath));
    }

    [TestMethod]
    public void Verify_does_not_emit_absolute_paths()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        Assert.AreEqual(0, ImportScopus(workspace.Root, out _, out var importError), importError);

        var exitCode = RunCli(workspace.Root, new[] { "verify" }, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        Assert.IsFalse(output.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(error.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Verify_rejects_project_with_input_path_escaping_workspace()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        File.WriteAllText(
            Path.Combine(workspace.Root, "nexus.project.json"),
            """
            {
              "schema": "nexus.project.v0",
              "workspaceId": "workspace-ai-screening-tools-review",
              "title": "AI screening tools review",
              "createdAt": "2026-07-01T00:00:00Z",
              "inputs": [
                {
                  "inputId": "search-escape",
                  "kind": "search-export",
                  "source": "scopus",
                  "format": "csv",
                  "relativePath": "../outside.csv",
                  "sha256": "sha256:34b3877e416692e7ec25ddc2d052d84e6c00ae44597ee57ad7943ae1869101c7",
                  "queryId": "search-escape",
                  "importTracePath": "nexus-output/imports/search-escape.import-trace.json"
                }
              ],
              "outputs": {},
              "nonClaims": [
                "local-folder-project",
                "no-live-providers",
                "no-cloud-sync",
                "no-database"
              ]
            }
            """);

        var exitCode = RunCli(workspace.Root, new[] { "verify" }, out var output, out var error);

        Assert.AreEqual(4, exitCode);
        StringAssert.Contains(output, "Malformed Nexus project file");
        StringAssert.Contains(output, "workspace-relative");
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void Verify_returns_nonzero_when_import_trace_missing()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        Assert.AreEqual(0, ImportWos(workspace.Root, "search-001", out _, out var importError), importError);
        var tracePath = InputPath(workspace.Root, "importTracePath");
        File.Delete(tracePath);

        var exitCode = RunCli(workspace.Root, new[] { "verify" }, out var output, out var error);

        Assert.AreEqual(2, exitCode);
        StringAssert.Contains(output, "Import traces missing: 1");
        StringAssert.Contains(output, "Missing trace: inputs/search/search-001/import-trace.json");
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void Usage_includes_verify()
    {
        StringAssert.Contains(CliApplication.Usage, "verify");
    }

    private static int ImportScopus(string workingDirectory, out string output, out string error)
    {
        return RunCli(
            workingDirectory,
            new[] { "import", "search", ImportFixturePath("pr03-scopus-small.csv"), "--source", "scopus", "--format", "csv", "--query-id", "search-001", "--query", QueryText },
            out output,
            out error);
    }

    private static string InputPath(string workspaceRoot, string propertyName)
    {
        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspaceRoot, "nexus.project.json")));
        var relativePath = project.RootElement.GetProperty("inputs")[0].GetProperty(propertyName).GetString()!;
        return Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static int ImportWos(string workingDirectory, string queryId, out string output, out string error)
    {
        return RunCli(
            workingDirectory,
            new[] { "import", "search", ImportFixturePath("pr03-wos-small.ris"), "--source", "web-of-science", "--format", "ris", "--query-id", queryId, "--query", QueryText },
            out output,
            out error);
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

    private static string ExpectedPath(string fileName)
    {
        return Path.Combine(RepositoryRoot(), "tests", "NexusScholar.Cli.Tests", "Fixtures", "ResearchWorkspaceVerify", "Expected", fileName);
    }

    private static void AssertTextEqual(string expectedPath, string actual)
    {
        Assert.AreEqual(
            NormalizeLineEndings(File.ReadAllText(expectedPath)).TrimEnd('\n'),
            NormalizeLineEndings(actual).TrimEnd('\n'));
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.ReplaceLineEndings("\n");
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

        public static TemporaryWorkspace CreateInitialized()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nexus-cli-verify-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var workspace = new TemporaryWorkspace(root);
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
