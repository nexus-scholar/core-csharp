using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Cli;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class SearchImportWorkspaceCommandTests
{
    private const string QueryText = "systematic review screening software";
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void ImportSearch_requires_initialized_workspace()
    {
        using var workspace = TemporaryWorkspace.Create();

        var exitCode = RunCli(
            workspace.Root,
            new[] { "import", "search", FixturePath("pr03-scopus-small.csv"), "--source", "scopus", "--format", "csv", "--query-id", "search-001" },
            out var output,
            out var error);

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "No Nexus research workspace found");
    }

    [TestMethod]
    public void ImportSearch_copies_source_file_and_records_digest()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();

        var exitCode = ImportScopus(workspace.Root, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "Imported search export: search-001");
        Assert.AreEqual(string.Empty, error);

        var sourceFixture = FixturePath("pr03-scopus-small.csv");
        var copiedPath = ProjectInputPath(workspace.Root, "relativePath");
        CollectionAssert.AreEqual(File.ReadAllBytes(sourceFixture), File.ReadAllBytes(copiedPath));

        var projectJson = File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json"));
        Assert.IsFalse(projectJson.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(projectJson, "\"inputId\": \"search-001\"");
        StringAssert.Contains(projectJson, "\"relativePath\": \"inputs/search/search-001/source.csv\"");
        StringAssert.Contains(projectJson, "\"sha256\": \"sha256:34b3877e416692e7ec25ddc2d052d84e6c00ae44597ee57ad7943ae1869101c7\"");
        StringAssert.Contains(projectJson, "\"importTracePath\": \"inputs/search/search-001/import-trace.json\"");
    }

    [TestMethod]
    public void ImportSearch_writes_import_trace_from_real_parser()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();

        var exitCode = ImportScopus(workspace.Root, out _, out var error);

        Assert.AreEqual(0, exitCode, error);
        var tracePath = ProjectInputPath(workspace.Root, "importTracePath");
        Assert.IsTrue(File.Exists(tracePath));

        using var document = JsonDocument.Parse(File.ReadAllText(tracePath));
        var root = document.RootElement;
        Assert.AreEqual("search-001.import-trace", root.GetProperty("traceId").GetString());
        Assert.AreEqual("scopus", root.GetProperty("metadata").GetProperty("sourceDatabaseOrTool").GetString());
        Assert.AreEqual("scopus-csv", root.GetProperty("metadata").GetProperty("exportFormat").GetString());
        Assert.AreEqual("sha256:34b3877e416692e7ec25ddc2d052d84e6c00ae44597ee57ad7943ae1869101c7", root.GetProperty("metadata").GetProperty("sourceFileDigest").GetString());
        Assert.AreEqual(2, root.GetProperty("metadata").GetProperty("recordCount").GetInt32());
        Assert.AreEqual(2, root.GetProperty("sightings").GetArrayLength());
        Assert.IsTrue(root.GetProperty("nonClaims").EnumerateArray().Any(value => value.GetString() == "no-network-requests"));
    }

    [TestMethod]
    public void ImportSearch_appends_project_input_entries()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();

        Assert.AreEqual(0, ImportScopus(workspace.Root, out _, out var scopusError), scopusError);
        Assert.AreEqual(0, ImportWos(workspace.Root, out _, out var wosError), wosError);
        Assert.AreEqual(0, ImportGoogleScholarBibtex(workspace.Root, out _, out var bibtexError), bibtexError);

        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json")));
        Assert.AreEqual(3, project.RootElement.GetProperty("inputs").GetArrayLength());
        Assert.AreEqual(3, project.RootElement.GetProperty("revision").GetInt64());
    }

    [TestMethod]
    public void ImportSearch_rejects_duplicate_input_id()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();

        Assert.AreEqual(0, ImportScopus(workspace.Root, out _, out var firstError), firstError);
        var secondExit = ImportScopus(workspace.Root, out var output, out var error);

        Assert.AreEqual(1, secondExit);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "already exists");

        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json")));
        Assert.AreEqual(1, project.RootElement.GetProperty("inputs").GetArrayLength());
    }

    [TestMethod]
    public void ImportSearch_locates_workspace_from_child_directory()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();
        var childDirectory = Path.Combine(workspace.Root, "nested", "child");
        Directory.CreateDirectory(childDirectory);

        var exitCode = RunCli(
            childDirectory,
            new[] { "import", "search", FixturePath("pr03-wos-small.ris"), "--source", "wos", "--format", "ris", "--query-id", "search-002", "--query", QueryText },
            out var output,
            out var error);

        Assert.AreEqual(0, exitCode, error);
        StringAssert.Contains(output, "Imported search export: search-002");
        Assert.AreEqual(string.Empty, error);
        Assert.IsTrue(File.Exists(ProjectInputPath(workspace.Root, "relativePath")));
    }

    [TestMethod]
    public void ImportSearch_handles_parser_warnings_without_failing_import()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();

        var exitCode = RunCli(
            workspace.Root,
            new[] { "import", "search", FixturePath("pr03-warning-missing-title.ris"), "--source", "web-of-science", "--format", "ris", "--query-id", "search-warning" },
            out var output,
            out var error);

        Assert.AreEqual(0, exitCode, error);
        Assert.AreEqual(string.Empty, error);
        StringAssert.Contains(output, "Parser warnings: 3");

        var tracePath = ProjectInputPath(workspace.Root, "importTracePath");
        using var trace = JsonDocument.Parse(File.ReadAllText(tracePath));
        Assert.AreEqual(3, trace.RootElement.GetProperty("parserWarnings").GetArrayLength());
    }

    [TestMethod]
    public void ImportSearch_does_not_emit_absolute_paths()
    {
        using var workspace = TemporaryWorkspace.CreateInitialized();

        var exitCode = ImportScopus(workspace.Root, out var output, out var error);

        Assert.AreEqual(0, exitCode, error);
        Assert.IsFalse(output.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(error.Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json")).Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(File.ReadAllText(ProjectInputPath(workspace.Root, "importTracePath")).Contains(workspace.Root, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Usage_includes_import()
    {
        StringAssert.Contains(CliApplication.Usage, "import");
    }

    private static int ImportScopus(string workingDirectory, out string output, out string error)
    {
        return RunCli(
            workingDirectory,
            new[] { "import", "search", FixturePath("pr03-scopus-small.csv"), "--source", "scopus", "--format", "csv", "--query-id", "search-001", "--query", QueryText },
            out output,
            out error);
    }

    private static string ProjectInputPath(string workspaceRoot, string propertyName)
    {
        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspaceRoot, "nexus.project.json")));
        var input = project.RootElement.GetProperty("inputs").EnumerateArray().Last();
        return Path.Combine(workspaceRoot, input.GetProperty(propertyName).GetString()!.Replace('/', Path.DirectorySeparatorChar));
    }

    private static int ImportWos(string workingDirectory, out string output, out string error)
    {
        return RunCli(
            workingDirectory,
            new[] { "import", "search", FixturePath("pr03-wos-small.ris"), "--source", "web-of-science", "--format", "ris", "--query-id", "search-002", "--query", QueryText },
            out output,
            out error);
    }

    private static int ImportGoogleScholarBibtex(string workingDirectory, out string output, out string error)
    {
        return RunCli(
            workingDirectory,
            new[] { "import", "search", FixturePath("pr03-google-scholar-small.bib"), "--source", "google-scholar", "--format", "bibtex", "--query-id", "search-003", "--query", QueryText },
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

    private static string FixturePath(string fileName)
    {
        return Path.Combine(RepositoryRoot(), "tests", "NexusScholar.Cli.Tests", "Fixtures", "ResearchWorkspaceImportSearch", fileName);
    }

    private static string ExpectedPath(string fileName)
    {
        return Path.Combine(RepositoryRoot(), "tests", "NexusScholar.Cli.Tests", "Fixtures", "ResearchWorkspaceImportSearch", "Expected", fileName);
    }

    private static void AssertTextEqual(string expectedPath, string actual)
    {
        Assert.AreEqual(
            NormalizeLineEndings(File.ReadAllText(expectedPath)).TrimEnd('\n'),
            NormalizeLineEndings(actual).TrimEnd('\n'));
    }

    private static void AssertJsonTextEqual(string expectedPath, string actual)
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

        public static TemporaryWorkspace Create()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "nexus-cli-import-tests",
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
