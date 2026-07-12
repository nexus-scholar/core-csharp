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
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathFullyQualified(relativePath))
        {
            return false;
        }

        var rootFullPath = Path.GetFullPath(rootDirectory);
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }


        var current = rootFullPath;
        foreach (var segment in Path.GetRelativePath(rootFullPath, candidate).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
        }

        fullPath = candidate;
        return true;
    }

    private static string DisplayInput(string inputId)
    {
        return string.IsNullOrWhiteSpace(inputId) ? "input entry" : inputId;
    }
}
