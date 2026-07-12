using System.Globalization;
using System.Text.RegularExpressions;
using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;
using NexusScholar.Search;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class SearchImportWorkspaceCommand
{
    private const string Usage = "Usage: nexus import search <path> --source <source> --format <format> [--query-id <id>] [--query <text>]";
    private const string ParserId = "nexus.cli.search-import";
    private const string ParserVersion = "1.0.0";
    private const string ImportedBy = "nexus-cli-local";

    private static readonly Regex SafeInputId = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        Func<DateTimeOffset> utcNow)
    {
        try
        {
            if (args.Length == 0 || !string.Equals(args[0], "search", StringComparison.Ordinal))
            {
                error.WriteLine(Usage);
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            var options = Parse(args.Skip(1).ToArray());
            if (string.IsNullOrWhiteSpace(options.InputPath) ||
                string.IsNullOrWhiteSpace(options.Source) ||
                string.IsNullOrWhiteSpace(options.Format))
            {
                error.WriteLine(Usage);
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            var location = ResearchWorkspaceStore.FindFrom(workingDirectory);
            if (location is null)
            {
                error.WriteLine("No Nexus research workspace found in the current folder or its parents.");
                error.WriteLine("Run: nexus init --title \"<research title>\"");
                return ResearchWorkspaceExitCodes.MissingProjectOrInput;
            }

            var sourcePath = Path.GetFullPath(options.InputPath, workingDirectory);
            if (!File.Exists(sourcePath))
            {
                error.WriteLine($"Search export file not found: {DisplayPath(options.InputPath)}");
                return ResearchWorkspaceExitCodes.MissingProjectOrInput;
            }

            var project = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
            {
                error.WriteLine($"Unsupported Nexus project schema: {project.Schema}");
                return ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat;
            }

            var source = SearchImportAliases.NormalizeSource(options.Source);
            var format = SearchImportAliases.NormalizeFormat(options.Format);
            var inputId = string.IsNullOrWhiteSpace(options.QueryId)
                ? NextInputId(project)
                : options.QueryId.Trim();
            ValidateInputId(inputId);

            if (project.Inputs.Any(input => string.Equals(input.EffectiveInputId, inputId, StringComparison.Ordinal)))
            {
                error.WriteLine($"A search export with input id '{inputId}' already exists.");
                error.WriteLine("Reimport requires a future explicit --replace flag.");
                return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
            }

            var sourceBytes = File.ReadAllBytes(sourcePath);
            var traceId = $"{inputId}.import-trace";
            var trace = new SearchImportService().Parse(
                traceId,
                new SearchImportRequest(
                    source,
                    SearchImportAliases.ParserFormatFor(format),
                    ParserId,
                    ParserVersion,
                    ImportedBy,
                    FormatUtc(utcNow()),
                    options.Query),
                sourceBytes);

            var updatedProject = ResearchWorkspaceTransaction.CommitImport(location, project, new ResearchWorkspaceInput
            {
                InputId = inputId,
                Kind = "search-export",
                Source = source,
                Format = format,
                Sha256 = ContentDigest.Sha256(sourceBytes).ToString(),
                QueryId = inputId,
                QueryText = string.IsNullOrWhiteSpace(options.Query) ? null : options.Query.Trim(),
            }, sourceBytes, trace, SearchImportAliases.ExtensionFor(format));
            var committedInput = updatedProject.Inputs.Single(input => string.Equals(input.EffectiveInputId, inputId, StringComparison.Ordinal));
            var relativeSourcePath = committedInput.EffectiveRelativePath;
            var traceRelativePath = committedInput.ImportTracePath!;

            output.WriteLine($"Imported search export: {inputId}");
            output.WriteLine($"Source: {source}");
            output.WriteLine($"Format: {format}");
            output.WriteLine($"Copied to: {relativeSourcePath}");
            output.WriteLine($"Source digest: {trace.Metadata.SourceFileDigest}");
            output.WriteLine($"Imported records: {trace.Sightings.Count}");
            output.WriteLine($"Parser warnings: {trace.ParserWarnings.Count}");
            output.WriteLine($"Skipped records: {trace.ImportedRecords.Count(record => record.IsSkipped)}");
            output.WriteLine($"Trace: {traceRelativePath}");
            output.WriteLine("Next: nexus verify");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (SearchRuleException exception)
        {
            error.WriteLine($"Search import failed: {exception.Message}");
            return string.Equals(exception.Category, SearchImportErrorCodes.UnsupportedFormat, StringComparison.Ordinal)
                ? ResearchWorkspaceExitCodes.UnsupportedSchemaOrFormat
                : ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Unable to import search export: {exception.Message}");
            return ResearchWorkspaceExitCodes.UnexpectedRuntimeFailure;
        }
    }

    private static ImportOptions Parse(string[] args)
    {
        string? inputPath = null;
        string? source = null;
        string? format = null;
        string? queryId = null;
        string? query = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--source":
                    source = ReadOptionValue(args, ref index, "--source");
                    break;
                case "--format":
                    format = ReadOptionValue(args, ref index, "--format");
                    break;
                case "--query-id":
                    queryId = ReadOptionValue(args, ref index, "--query-id");
                    break;
                case "--query":
                    query = ReadOptionValue(args, ref index, "--query");
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"Unknown option for import search: {arg}");
                    }

                    if (inputPath is not null)
                    {
                        throw new ArgumentException($"Unexpected extra path for import search: {arg}");
                    }

                    inputPath = arg;
                    break;
            }
        }

        return new ImportOptions(inputPath, source, format, queryId, query);
    }

    private static string ReadOptionValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for option: {optionName}");
        }

        index++;
        return args[index];
    }

    private static string NextInputId(ResearchWorkspaceProject project)
    {
        var next = project.Inputs
            .Select(input => input.EffectiveInputId)
            .Select(ParseSearchInputNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;
        return string.Create(CultureInfo.InvariantCulture, $"search-{next:000}");
    }

    private static int ParseSearchInputNumber(string inputId)
    {
        return inputId.StartsWith("search-", StringComparison.Ordinal) &&
               int.TryParse(inputId["search-".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : 0;
    }

    private static void ValidateInputId(string inputId)
    {
        if (!SafeInputId.IsMatch(inputId) ||
            inputId.Contains("..", StringComparison.Ordinal) ||
            inputId.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            inputId.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Search query id must be a safe local file segment.");
        }
    }

    private static string FormatUtc(DateTimeOffset timestamp)
    {
        return timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static string DisplayPath(string inputPath)
    {
        return Path.IsPathFullyQualified(inputPath) ? Path.GetFileName(inputPath) : inputPath;
    }

    private sealed record ImportOptions(
        string? InputPath,
        string? Source,
        string? Format,
        string? QueryId,
        string? Query);
}
