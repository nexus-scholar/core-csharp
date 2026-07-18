using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using NexusScholar.Kernel;
using NexusScholar.Search;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceVerifier
{
    private const string ParserId = "nexus.cli.verify";
    private const string ParserVersion = "1.0.0";
    private const string ImportedBy = "nexus-cli-local-verify";

    public static ResearchWorkspaceVerificationReport Verify(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(project);

        var filesUnchanged = 0;
        var missingFiles = new List<string>();
        var digestMismatches = new List<string>();
        var invalidPaths = new List<string>();
        var missingImportTraces = new List<string>();
        var parserWarningCategories = new Dictionary<string, int>(StringComparer.Ordinal);
        var parserWarningCount = 0;
        var skippedRecordCount = 0;

        foreach (var input in project.Inputs.Where(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal)))
        {
            var inputId = input.EffectiveInputId;
            var relativePath = input.EffectiveRelativePath;

            if (!TryResolveWorkspaceRelativePath(location.RootDirectory, relativePath, out var sourcePath))
            {
                invalidPaths.Add(DisplayInput(inputId));
                continue;
            }

            if (!File.Exists(sourcePath))
            {
                missingFiles.Add(relativePath);
                continue;
            }

            var sourceBytes = File.ReadAllBytes(sourcePath);
            var digest = ContentDigest.Sha256(sourceBytes).ToString();
            if (!string.Equals(digest, input.Sha256, StringComparison.Ordinal))
            {
                digestMismatches.Add(relativePath);
                continue;
            }

            filesUnchanged++;

            if (!string.IsNullOrWhiteSpace(input.ImportTracePath))
            {
                if (!TryResolveWorkspaceRelativePath(location.RootDirectory, input.ImportTracePath, out var tracePath))
                {
                    invalidPaths.Add($"{DisplayInput(inputId)} import trace");
                }
                else if (!File.Exists(tracePath))
                {
                    missingImportTraces.Add(input.ImportTracePath);
                }
            }

            var source = SearchImportAliases.NormalizeSource(input.Source);
            var format = SearchImportAliases.NormalizeFormat(input.Format);
            var trace = new SearchImportService().Parse(
                $"{inputId}.verify-import-trace",
                new SearchImportRequest(
                    source,
                    SearchImportAliases.ParserFormatFor(format),
                    ParserId,
                    ParserVersion,
                    ImportedBy,
                    project.CreatedAt,
                    input.QueryText),
                sourceBytes);

            parserWarningCount += trace.ParserWarnings.Count;
            skippedRecordCount += trace.ImportedRecords.Count(record => record.IsSkipped);
            foreach (var group in trace.ParserWarnings.GroupBy(warning => warning.Category, StringComparer.Ordinal))
            {
                parserWarningCategories[group.Key] = parserWarningCategories.GetValueOrDefault(group.Key) + group.Count();
            }
        }

        return new ResearchWorkspaceVerificationReport(
            project.Inputs.Count(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal)),
            filesUnchanged,
            missingFiles,
            digestMismatches,
            invalidPaths,
            missingImportTraces,
            parserWarningCount,
            skippedRecordCount,
            parserWarningCategories);
    }

    public static bool TryResolveWorkspaceRelativePath(string rootDirectory, string? relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            Path.IsPathFullyQualified(relativePath))
        {
            return false;
        }

        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var declaredSegments = normalizedRelativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (declaredSegments.Length == 0 ||
            declaredSegments.Any(segment => segment is "." or ".."))
        {
            return false;
        }

        string rootFullPath;
        string candidate;
        try
        {
            rootFullPath = Path.GetFullPath(rootDirectory);
            candidate = Path.GetFullPath(Path.Combine(rootFullPath, normalizedRelativePath));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (IsExistingReparsePointOrUninspectable(rootFullPath))
        {
            return false;
        }

        var rootWithSeparator = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!candidate.StartsWith(rootWithSeparator, pathComparison))
        {
            return false;
        }

        var relativeFromRoot = Path.GetRelativePath(rootFullPath, candidate);
        var segments = relativeFromRoot.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Contains("..", StringComparer.Ordinal))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows() && !HasCaseSafeRelativePath(rootFullPath, normalizedRelativePath))
        {
            return false;
        }

        var current = rootFullPath;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (IsExistingReparsePointOrUninspectable(current))
            {
                return false;
            }
        }

        fullPath = candidate;
        return true;
    }

    internal static bool IsOpenFileAtExpectedPath(FileStream stream, string expectedPath)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (string.IsNullOrWhiteSpace(expectedPath) || stream.SafeFileHandle.IsInvalid)
        {
            return false;
        }

        try
        {
            var openPath = OperatingSystem.IsWindows()
                ? GetWindowsOpenPath(stream.SafeFileHandle)
                : OperatingSystem.IsLinux()
                    ? GetLinuxOpenPath(stream.SafeFileHandle)
                    : OperatingSystem.IsMacOS()
                        ? GetMacOsOpenPath(stream.SafeFileHandle)
                        : null;
            if (string.IsNullOrWhiteSpace(openPath))
            {
                return false;
            }

            var comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return string.Equals(
                Path.GetFullPath(openPath),
                Path.GetFullPath(expectedPath),
                comparison);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }

    private static string? GetWindowsOpenPath(SafeFileHandle handle)
    {
        var capacity = 512;
        while (capacity <= 32768)
        {
            var buffer = new StringBuilder(capacity);
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0)
            {
                return null;
            }

            if (length < buffer.Capacity)
            {
                var path = buffer.ToString();
                if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                {
                    return @"\\" + path[8..];
                }

                return path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
                    ? path[4..]
                    : path;
            }

            capacity = checked((int)length + 1);
        }

        return null;
    }

    private static string? GetLinuxOpenPath(SafeFileHandle handle)
    {
        var descriptor = handle.DangerousGetHandle().ToInt64();
        var target = new FileInfo($"/proc/self/fd/{descriptor}").ResolveLinkTarget(returnFinalTarget: true);
        return target?.FullName;
    }

    private static string? GetMacOsOpenPath(SafeFileHandle handle)
    {
        const int fGetPath = 50;
        var buffer = new byte[4096];
        if (FcntlGetPath(handle.DangerousGetHandle().ToInt32(), fGetPath, buffer) != 0)
        {
            return null;
        }

        var terminator = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, terminator < 0 ? buffer.Length : terminator);
    }

    private static bool HasCaseSafeRelativePath(string rootFullPath, string normalizedRelativePath)
    {
        var current = rootFullPath;
        foreach (var segment in normalizedRelativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "." || segment == "..")
            {
                return false;
            }

            if (File.Exists(current))
            {
                return false;
            }

            if (!Directory.Exists(current))
            {
                return true;
            }

            var existsByCase = false;
            var existsByFoldedCase = false;
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var name = Path.GetFileName(entry);
                if (string.Equals(name, segment, StringComparison.Ordinal))
                {
                    existsByCase = true;
                    break;
                }

                if (string.Equals(name, segment, StringComparison.OrdinalIgnoreCase))
                {
                    existsByFoldedCase = true;
                }
            }

            if (existsByFoldedCase && !existsByCase)
            {
                return false;
            }

            current = Path.Combine(current, segment);
        }

        return true;
    }

    private static bool IsExistingReparsePointOrUninspectable(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return false;
        }

        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return true;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder path,
        uint pathLength,
        uint flags);

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int FcntlGetPath(int fileDescriptor, int command, byte[] path);

    private static string DisplayInput(string inputId)
    {
        return string.IsNullOrWhiteSpace(inputId) ? "input entry" : inputId;
    }
}
