using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace NexusScholar.Desktop;

internal static class DesktopCrashDiagnostics
{
    public const string DefaultDiagnosticsDirectoryName = "diagnostics";
    public const string DiagnosticsSchema = "nexus.scholar.desktop.crash";
    public const int MaxReportsKept = 10;

    private const string DefaultApplicationDataDirectory = "NexusScholar";
    private const string ReportFilePrefix = "nexus-scholar-crash-";
    private const string ReportFileSuffix = ".json";
    private const string ReportSchemaVersion = "1";
    private static long _lastReportUtcTicks;
    private static readonly HashSet<string> AllowedSources = new(StringComparer.Ordinal)
    {
        "startup",
        "app-domain",
        "task-scheduler",
        "ui-dispatcher"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool TryRecordFailure(Exception? exception, string source, out string? reportPath)
    {
        return TryRecordFailureCore(exception, source, GetDiagnosticsDirectory(), out reportPath);
    }

    internal static bool TryRecordFailureForTests(
        Exception? exception,
        string source,
        string diagnosticsDirectory,
        out string? reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticsDirectory);
        return TryRecordFailureCore(exception, source, diagnosticsDirectory, out reportPath);
    }

    private static bool TryRecordFailureCore(
        Exception? exception,
        string source,
        string? directory,
        out string? reportPath)
    {
        reportPath = null;

        if (exception is null)
        {
            return false;
        }

        if (directory is null)
        {
            return false;
        }

        var now = GetMonotonicUtc();
        var safeSource = AllowedSources.Contains(source) ? source : "startup";

        var fileName = $"{ReportFilePrefix}{now:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}{ReportFileSuffix}";
        var finalPath = Path.Combine(directory, fileName);
        var tempPath = $"{finalPath}.{Guid.NewGuid():N}.tmp";

        reportPath = finalPath;

        try
        {
            Directory.CreateDirectory(directory);

            var payload = new CrashReport(
                schema: DiagnosticsSchema,
                version: ReportSchemaVersion,
                appVersion: GetAppVersion(),
                utc: now.ToString("O", CultureInfo.InvariantCulture),
                source: safeSource,
                exceptionType: exception.GetType().FullName ?? exception.GetType().Name,
                innerExceptionType: exception.InnerException?.GetType().FullName);

            var json = JsonSerializer.Serialize(payload, SerializerOptions);

            File.WriteAllText(tempPath, json, new UTF8Encoding(false));
            File.Move(tempPath, finalPath, overwrite: false);

            EnforceRetention(directory);
            return true;
        }
        catch
        {
            SafeDelete(tempPath);
            reportPath = null;
            return false;
        }
    }

    public static (string Notice, string? ReportPath) GetLatestSafeNoticeAndPath()
    {
        return GetLatestSafeNoticeAndPathCore(GetDiagnosticsDirectory());
    }

    internal static (string Notice, string? ReportPath) GetLatestSafeNoticeAndPathForTests(
        string diagnosticsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticsDirectory);
        return GetLatestSafeNoticeAndPathCore(diagnosticsDirectory);
    }

    private static (string Notice, string? ReportPath) GetLatestSafeNoticeAndPathCore(string? directory)
    {
        var path = GetLatestReportPathCore(directory);
        if (string.IsNullOrWhiteSpace(path))
        {
            return ("No local crash reports found.", null);
        }

        return ($"Latest crash report available at: {path}", path);
    }

    public static string? GetLatestReportPath()
    {
        return GetLatestReportPathCore(GetDiagnosticsDirectory());
    }

    internal static string? GetLatestReportPathForTests(string diagnosticsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticsDirectory);
        return GetLatestReportPathCore(diagnosticsDirectory);
    }

    private static string? GetLatestReportPathCore(string? directory)
    {
        try
        {
            if (directory is null || !Directory.Exists(directory))
            {
                return null;
            }

            return Directory.EnumerateFiles(directory, $"{ReportFilePrefix}*{ReportFileSuffix}")
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string? GetDiagnosticsDirectory()
    {
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData))
        {
            return null;
        }

        return Path.Combine(localData, DefaultApplicationDataDirectory, DefaultDiagnosticsDirectoryName);
    }

    private static DateTimeOffset GetMonotonicUtc()
    {
        while (true)
        {
            var previous = Volatile.Read(ref _lastReportUtcTicks);
            var observed = DateTimeOffset.UtcNow.UtcTicks;
            var next = observed > previous ? observed : previous + 1;
            if (Interlocked.CompareExchange(ref _lastReportUtcTicks, next, previous) == previous)
            {
                return new DateTimeOffset(next, TimeSpan.Zero);
            }
        }
    }

    private static void EnforceRetention(string directory)
    {
        try
        {
            var reports = Directory.EnumerateFiles(directory, $"{ReportFilePrefix}*{ReportFileSuffix}")
                .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                .Skip(MaxReportsKept)
                .ToArray();

            foreach (var obsolete in reports)
            {
                SafeDelete(obsolete);
            }
        }
        catch
        {
            // diagnostics must not fail the app path
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // diagnostics must not fail the app path
        }
    }

    private static string GetAppVersion()
    {
        var assembly = typeof(DesktopCrashDiagnostics).Assembly;
        return assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    private sealed record CrashReport(
        string schema,
        string version,
        string appVersion,
        string utc,
        string source,
        string exceptionType,
        string? innerExceptionType);
}
