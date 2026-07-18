using NexusScholar.AppServices;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.UiContracts;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceAnalyzer
{
    public const string DeduplicationResultPath = "nexus-output/dedup/current.deduplication-result.json";
    public const string WorkspacePlanPath = "nexus-output/workspace/current.workspace-plan.json";
    public const string ReviewReportPath = "nexus-output/reports/review.md";

    private const string ParserId = "nexus.local-workspace.analyze";
    private const string ParserVersion = "1.0.0";
    private const string ImportedBy = "nexus-local-workspace-analyze";

    public static ResearchWorkspaceAnalysisResult Analyze(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project) =>
        Analyze(location, project, inputSnapshots: null);

    internal static ResearchWorkspaceAnalysisResult Analyze(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project,
        IReadOnlyDictionary<string, byte[]>? inputSnapshots)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(project);

        var inputs = project.Inputs
            .Where(input => string.Equals(input.Kind, "search-export", StringComparison.Ordinal))
            .ToArray();
        if (inputs.Length == 0)
        {
            throw new ResearchWorkspaceMissingInputException("Analyze requires at least one imported search export.");
        }

        var traces = inputs.Select(input => RegenerateTrace(location, project, input, inputSnapshots)).ToArray();
        var deduplicationResult = new DeduplicationService().Execute(
            $"dedup-{project.WorkspaceId}",
            Array.Empty<SearchTrace>(),
            traces);
        var aggregateTrace = AggregateTrace(project, traces);
        var plan = new SearchDedupWorkspacePlanComposer().Compose(
            new SearchDedupWorkspacePlanInput(
                project.WorkspaceId,
                project.Title,
                aggregateTrace,
                deduplicationResult,
                "Read-only local workspace analysis from imported Search evidence. Not Core authority."));

        return new ResearchWorkspaceAnalysisResult(traces, deduplicationResult, plan);
    }

    private static SearchImportTrace RegenerateTrace(
        ResearchWorkspaceLocation location,
        ResearchWorkspaceProject project,
        ResearchWorkspaceInput input,
        IReadOnlyDictionary<string, byte[]>? inputSnapshots)
    {
        byte[] sourceBytes;
        if (inputSnapshots is not null)
        {
            if (!inputSnapshots.TryGetValue(input.EffectiveInputId, out sourceBytes!))
            {
                throw new ResearchWorkspaceMissingInputException($"Input snapshot is missing: {input.EffectiveInputId}");
            }
        }
        else
        {
            if (!ResearchWorkspaceVerifier.TryResolveWorkspaceRelativePath(location.RootDirectory, input.EffectiveRelativePath, out var sourcePath))
            {
                throw new ResearchWorkspaceMissingInputException($"Input path is not a valid workspace-relative path: {input.EffectiveInputId}");
            }

            if (!File.Exists(sourcePath))
            {
                throw new ResearchWorkspaceMissingInputException($"Input file is missing: {input.EffectiveRelativePath}");
            }

            sourceBytes = File.ReadAllBytes(sourcePath);
        }

        var digest = ContentDigest.Sha256(sourceBytes).ToString();
        if (!string.Equals(digest, input.Sha256, StringComparison.Ordinal))
        {
            throw new ResearchWorkspaceDigestMismatchException($"Input digest mismatch: {input.EffectiveRelativePath}");
        }

        var source = SearchImportAliases.NormalizeSource(input.Source);
        var format = SearchImportAliases.NormalizeFormat(input.Format);
        return new SearchImportService().Parse(
            $"{input.EffectiveInputId}.import-trace",
            new SearchImportRequest(
                source,
                SearchImportAliases.ParserFormatFor(format),
                ParserId,
                ParserVersion,
                ImportedBy,
                project.CreatedAt,
                input.QueryText),
            sourceBytes);
    }

    private static SearchImportTrace AggregateTrace(
        ResearchWorkspaceProject project,
        IReadOnlyList<SearchImportTrace> traces)
    {
        var parserWarnings = traces
            .SelectMany(trace => trace.ParserWarnings)
            .OrderBy(warning => warning.Category, StringComparer.Ordinal)
            .ThenBy(warning => warning.SourceRecordId, StringComparer.Ordinal)
            .ThenBy(warning => warning.RecordIndex)
            .ThenBy(warning => warning.Message, StringComparer.Ordinal)
            .ToArray();
        var importedRecords = traces
            .SelectMany(trace => trace.ImportedRecords)
            .ToArray();
        var sightings = traces
            .SelectMany(trace => trace.Sightings)
            .ToArray();
        var sourceDigest = ContentDigest.Sha256Utf8(string.Join(
            "\n",
            traces.Select(trace => trace.Metadata.SourceFileDigest).OrderBy(value => value, StringComparer.Ordinal))).ToString();
        var metadata = new SearchImportMetadata(
            SearchImportMetadata.AcquisitionKindImportedExport,
            "local-workspace-imports",
            "mixed-local-imports",
            "nexus.local-workspace.analyze.aggregate",
            "1.0.0",
            sourceDigest,
            "local-workspace-import-digests",
            ImportedBy,
            project.CreatedAt,
            project.Title,
            project.CreatedAt,
            importedRecords.Length,
            parserWarnings);

        return new SearchImportTrace(
            $"trace-{project.WorkspaceId}-aggregate",
            "nexus.search.import.trace",
            "1.0.0",
            metadata,
            importedRecords,
            sightings,
            parserWarnings,
            SearchImportTrace.DefaultNonClaims);
    }
}

public sealed record ResearchWorkspaceAnalysisResult(
    IReadOnlyList<SearchImportTrace> ImportTraces,
    DeduplicationResult DeduplicationResult,
    WorkspacePlan WorkspacePlan)
{
    public int ImportedRecordCount => ImportTraces.Sum(trace => trace.ImportedRecords.Count);
    public int ParserWarningCount => ImportTraces.Sum(trace => trace.ParserWarnings.Count);
    public int SkippedRecordCount => ImportTraces.Sum(trace => trace.ImportedRecords.Count(record => record.IsSkipped));
}
