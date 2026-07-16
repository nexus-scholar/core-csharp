using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class SearchImportWorkspaceCommand
{
    private const string Usage = "Usage: nexus import search <path> --source <source> --format <format> [--query-id <id>] [--query <text>]";
    private const string ImportedBy = "nexus-cli-local";

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

            var result = ResearchWorkspaceLocalOperations.ImportSearch(new ResearchWorkspaceSearchImportRequest(
                workingDirectory,
                options.InputPath,
                options.Source,
                options.Format,
                options.QueryId,
                options.Query,
                ImportedBy,
                utcNow()));
            if (!result.Completed)
            {
                error.WriteLine(result.Message);
                if (result.ExitCode == ResearchWorkspaceExitCodes.MissingProjectOrInput &&
                    ResearchWorkspaceStore.FindFrom(workingDirectory) is null)
                {
                    error.WriteLine("Run: nexus init --title \"<research title>\"");
                }
                else if (result.Message.Contains("already exists", StringComparison.Ordinal))
                {
                    error.WriteLine("Reimport requires a future explicit --replace flag.");
                }

                return result.ExitCode;
            }

            output.WriteLine(result.Message);
            output.WriteLine($"Source: {result.Source}");
            output.WriteLine($"Format: {result.Format}");
            output.WriteLine($"Copied to: {result.RelativeSourcePath}");
            output.WriteLine($"Source digest: {result.SourceDigest}");
            output.WriteLine($"Imported records: {result.ImportedRecordCount}");
            output.WriteLine($"Parser warnings: {result.ParserWarningCount}");
            output.WriteLine($"Skipped records: {result.SkippedRecordCount}");
            output.WriteLine($"Trace: {result.TraceRelativePath}");
            output.WriteLine("Next: nexus verify");
            return ResearchWorkspaceExitCodes.Success;
        }
        catch (ArgumentException exception)
        {
            error.WriteLine(exception.Message);
            return ResearchWorkspaceExitCodes.UsageOrValidationFailure;
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

    private sealed record ImportOptions(
        string? InputPath,
        string? Source,
        string? Format,
        string? QueryId,
        string? Query);
}
