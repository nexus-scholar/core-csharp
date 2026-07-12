using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Cli;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class ResearchWorkspaceProcessTests
{
    [TestMethod]
    public async Task Concurrent_cli_import_processes_never_lose_a_committed_input()
    {
        using var workspace = TemporaryDirectory.Create();
        AssertProcessSuccess(await RunCli(workspace.Root, "init", "--title", "Concurrent import test"));
        var firstSource = WriteSource(workspace.Root, "first.csv", "First");
        var secondSource = WriteSource(workspace.Root, "second.csv", "Second");

        var first = RunCli(workspace.Root, "import", "search", firstSource, "--source", "scopus", "--format", "csv", "--query-id", "search-001");
        var second = RunCli(workspace.Root, "import", "search", secondSource, "--source", "scopus", "--format", "csv", "--query-id", "search-002");
        var initial = await Task.WhenAll(first, second);

        foreach (var failed in initial.Where(result => result.ExitCode != 0))
        {
            var inputId = failed.Arguments.Contains("search-001", StringComparison.Ordinal) ? "search-001" : "search-002";
            var source = inputId == "search-001" ? firstSource : secondSource;
            AssertProcessSuccess(await RunCli(workspace.Root, "import", "search", source, "--source", "scopus", "--format", "csv", "--query-id", inputId));
        }

        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json")));
        var inputs = project.RootElement.GetProperty("inputs").EnumerateArray().ToArray();
        Assert.AreEqual(2, inputs.Length);
        Assert.AreEqual(2, project.RootElement.GetProperty("revision").GetInt64());
        CollectionAssert.AreEquivalent(new[] { "search-001", "search-002" }, inputs.Select(input => input.GetProperty("inputId").GetString()).ToArray());
        Assert.IsTrue(inputs.All(input => File.Exists(Path.Combine(workspace.Root, input.GetProperty("relativePath").GetString()!.Replace('/', Path.DirectorySeparatorChar)))));
        Assert.IsTrue(inputs.All(input => File.Exists(Path.Combine(workspace.Root, input.GetProperty("importTracePath").GetString()!.Replace('/', Path.DirectorySeparatorChar)))));
    }

    [TestMethod]
    public async Task Interrupted_staging_and_unreferenced_generation_never_look_committed()
    {
        using var workspace = TemporaryDirectory.Create();
        AssertProcessSuccess(await RunCli(workspace.Root, "init", "--title", "Interrupted generation test"));
        var staging = Path.Combine(workspace.Root, "nexus-output", ".staging", "gen-interrupted");
        var orphan = Path.Combine(workspace.Root, "nexus-output", "generations", "gen-orphan");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(orphan);
        File.WriteAllText(Path.Combine(staging, "partial.json"), "{\"partial\":true}");
        File.WriteAllText(Path.Combine(orphan, "generation.manifest.json"), "{\"schema\":\"nexus.workspace-generation.v1\"}");

        var status = await RunCli(workspace.Root, "status");

        AssertProcessSuccess(status);
        StringAssert.Contains(status.StandardOutput, "State: initialized");
        StringAssert.Contains(status.StandardOutput, "workspace plan: missing");
        using var project = JsonDocument.Parse(File.ReadAllText(Path.Combine(workspace.Root, "nexus.project.json")));
        Assert.AreEqual(0, project.RootElement.GetProperty("outputs").EnumerateObject().Count());
        Assert.IsFalse(project.RootElement.TryGetProperty("currentGenerationId", out _));
    }

    private static string WriteSource(string root, string fileName, string title)
    {
        var path = Path.Combine(root, fileName);
        File.WriteAllText(path, $"eid,title,author names,year\n{fileName},{title},Doe Jane,2026\n");
        return path;
    }

    private static async Task<ProcessResult> RunCli(string workingDirectory, params string[] arguments)
    {
        var assembly = typeof(CliApplication).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(assembly);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start CLI test process.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        return new ProcessResult(process.ExitCode, await standardOutput, await standardError, string.Join(' ', arguments));
    }

    private static void AssertProcessSuccess(ProcessResult result) =>
        Assert.AreEqual(0, result.ExitCode, $"{result.Arguments}\nstdout: {result.StandardOutput}\nstderr: {result.StandardError}");

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, string Arguments);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string root) => Root = root;

        public string Root { get; }

        public static TemporaryDirectory Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"nexus-process-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            return new TemporaryDirectory(root);
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
