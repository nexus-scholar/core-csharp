using System.Globalization;
using System.Text.Json;

namespace NexusScholar.Desktop.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopCrashDiagnosticsTests
{
    [TestMethod]
    public void RecordFailure_writes_redacted_report_with_expected_fields()
    {
        using var directory = new TemporaryDirectory();

        var inner = new InvalidOperationException("inner secret");
        var failure = new InvalidOperationException("outer secret", inner);

        var recorded = DesktopCrashDiagnostics.TryRecordFailureForTests(
            failure,
            "ui-dispatcher",
            directory.Path,
            out var reportPath);

        Assert.IsTrue(recorded);
        Assert.IsNotNull(reportPath);
        Assert.IsTrue(reportPath.StartsWith(directory.Path, StringComparison.Ordinal));

        var reportJson = File.ReadAllText(reportPath);
        Assert.IsFalse(reportJson.Contains("outer secret", StringComparison.Ordinal), reportJson);
        Assert.IsFalse(reportJson.Contains("inner secret", StringComparison.Ordinal), reportJson);
        Assert.IsFalse(reportJson.Contains("StackTrace", StringComparison.Ordinal), reportJson);

        using var json = JsonDocument.Parse(reportJson);
        var root = json.RootElement;

        var expectedProperties = new[]
        {
            "schema",
            "version",
            "appVersion",
            "utc",
            "source",
            "exceptionType",
            "innerExceptionType"
        };

        var actualProperties = root.EnumerateObject().Select(property => property.Name).ToArray();
        CollectionAssert.AreEquivalent(expectedProperties, actualProperties);
        Assert.AreEqual("nexus.scholar.desktop.crash", root.GetProperty("schema").GetString());
        Assert.AreEqual("1", root.GetProperty("version").GetString());
        Assert.AreEqual("ui-dispatcher", root.GetProperty("source").GetString());
        Assert.AreEqual(typeof(InvalidOperationException).FullName, root.GetProperty("exceptionType").GetString());
        Assert.AreEqual(typeof(InvalidOperationException).FullName, root.GetProperty("innerExceptionType").GetString());
        Assert.IsTrue(DateTimeOffset.TryParse(
            root.GetProperty("utc").GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal,
            out var parsedUtc));
        Assert.AreEqual(DateTimeOffset.UtcNow.Offset, parsedUtc.Offset);
        Assert.IsNotNull(root.GetProperty("appVersion").GetString());
    }

    [TestMethod]
    public void RecordFailure_retains_maximum_of_ten_reports_and_get_latest_returns_most_recent()
    {
        using var directory = new TemporaryDirectory();

        var created = new List<string?>();
        for (var index = 0; index < 12; index++)
        {
            var recorded = DesktopCrashDiagnostics.TryRecordFailureForTests(
                new InvalidOperationException($"failure-{index}"),
                "task-scheduler",
                directory.Path,
                out var reportPath);
            Assert.IsTrue(recorded);
            Assert.IsNotNull(reportPath);
            created.Add(reportPath);
        }

        var files = Directory.EnumerateFiles(directory.Path).ToArray();
        Assert.AreEqual(DesktopCrashDiagnostics.MaxReportsKept, files.Length);

        var expectedLatest = created[^1];
        var latest = DesktopCrashDiagnostics.GetLatestReportPathForTests(directory.Path);
        Assert.AreEqual(expectedLatest, latest);

        var (notice, latestPath) = DesktopCrashDiagnostics.GetLatestSafeNoticeAndPathForTests(directory.Path);
        Assert.AreEqual(expectedLatest, latestPath);
        StringAssert.Contains(notice, Path.GetFileName(expectedLatest!));
    }

    [TestMethod]
    public void RecordFailure_test_hook_uses_only_the_explicit_directory()
    {
        using var directory = new TemporaryDirectory();

        var recorded = DesktopCrashDiagnostics.TryRecordFailureForTests(
            new InvalidOperationException("test"),
            "app-domain",
            directory.Path,
            out var reportPath);

        Assert.IsTrue(recorded);
        Assert.IsNotNull(reportPath);
        Assert.IsTrue(reportPath.StartsWith(directory.Path, StringComparison.Ordinal));
    }

    [TestMethod]
    public void RecordFailure_handles_malformed_directory_without_throwing()
    {
        using var directory = new TemporaryDirectory();
        var malformedPath = Path.Combine(directory.Path, "not-a-directory.txt");
        File.WriteAllText(malformedPath, "blocker");

        var recorded = DesktopCrashDiagnostics.TryRecordFailureForTests(
            new InvalidOperationException("test"),
            "startup",
            malformedPath,
            out var reportPath);

        Assert.IsFalse(recorded);
        Assert.IsNull(reportPath);
    }

    [TestMethod]
    public void RecordFailure_replaces_unrecognized_source_instead_of_persisting_it()
    {
        using var directory = new TemporaryDirectory();

        Assert.IsTrue(DesktopCrashDiagnostics.TryRecordFailureForTests(
            new InvalidOperationException("secret"),
            @"C:\sensitive\workspace\alice",
            directory.Path,
            out var reportPath));

        using var json = JsonDocument.Parse(File.ReadAllText(reportPath!));
        Assert.AreEqual("startup", json.RootElement.GetProperty("source").GetString());
        Assert.IsFalse(File.ReadAllText(reportPath!).Contains("sensitive", StringComparison.Ordinal));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexus-desktop-crash-tests-{Guid.NewGuid():N}");
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

}
