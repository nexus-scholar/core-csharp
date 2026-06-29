using System.Text;
using NexusScholar.Deduplication;
using NexusScholar.Search;

namespace NexusScholar.Cli;

public static class LocalDemoCommand
{
    public static readonly IReadOnlyList<string> RequiredOutputLines = new[]
    {
        "Nexus Scholar Core local demo",
        "Mode: deterministic local sample",
        "Network: none",
        "Live providers: none",
        "Persistence: none",
        "Import source: scopus-csv",
        "Imported records: 5",
        "Search sightings: 4",
        "Parser warnings: 2",
        "Source digest scope: raw-artifact-bytes",
        "Dedup raw candidates: 4",
        "Dedup exact clusters: 1",
        "Dedup review-required pairs: 1",
        "Non-claims: no live providers; no provider SDKs; no persistence/API/cloud; no PDF/OCR; no PHP compatibility"
    };

    private const string DemoCsv = """
        eid,title,author names,year,source title,doi
        2-s2.0-demo-001,Evidence preserving duplicate review,Alpha One,2024,Demo Journal,10.1000/demo-duplicate
        2-s2.0-demo-002,Evidence-preserving duplicate review,Alpha One,2024,Demo Journal,10.1000/demo-duplicate
        2-s2.0-demo-003,Title only candidate without stable id,Beta Two,2023,Demo Journal,
        2-s2.0-demo-004,Title only candidate without stable id,Beta Two,2023,Demo Journal,
        2-s2.0-demo-005,,Gamma Three,2022,Demo Journal,
        """;

    public static int Run(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var lines = BuildOutputLines();
        foreach (var line in lines)
        {
            output.WriteLine(line);
        }

        return 0;
    }

    public static string FormatOutput()
    {
        return string.Join(Environment.NewLine, BuildOutputLines()) + Environment.NewLine;
    }

    private static IReadOnlyList<string> BuildOutputLines()
    {
        var sourceBytes = Encoding.UTF8.GetBytes(DemoCsv);
        var request = new SearchImportRequest(
            "scopus-csv",
            "scopus-csv",
            "cli-local-demo-parser",
            "1.0.0",
            "cli-demo-user",
            "2026-06-29T00:00:00Z",
            "nexus scholar local demo",
            "2026-06-29T00:00:00Z");

        var importTrace = new SearchImportService().Parse(
            "cli-local-demo-import",
            request,
            sourceBytes);
        var dedupResult = new DeduplicationService().Execute(
            "cli-local-demo-dedup",
            Array.Empty<SearchTrace>(),
            new[] { importTrace });

        var exactClusters = dedupResult.Clusters.Count(cluster =>
            cluster.Evidence.Any(evidence => evidence.Kind == DedupEvidenceKind.ExactIdentifier));
        var userFacingParserWarningCategories = importTrace.ParserWarnings
            .Where(warning =>
                string.Equals(warning.Category, SearchImportErrorCodes.MissingRequiredField, StringComparison.Ordinal) ||
                string.Equals(warning.Category, SearchImportErrorCodes.SkippedRecord, StringComparison.Ordinal))
            .Select(warning => warning.Category)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new[]
        {
            "Nexus Scholar Core local demo",
            "Mode: deterministic local sample",
            "Network: none",
            "Live providers: none",
            "Persistence: none",
            $"Import source: {importTrace.Metadata.ExportFormat}",
            $"Imported records: {importTrace.Metadata.RecordCount}",
            $"Search sightings: {importTrace.Sightings.Count}",
            $"Parser warnings: {userFacingParserWarningCategories}",
            $"Source digest scope: {importTrace.Metadata.SourceFileDigestScope}",
            $"Dedup raw candidates: {dedupResult.RawCandidates.Count}",
            $"Dedup exact clusters: {exactClusters}",
            $"Dedup review-required pairs: {dedupResult.ReviewRequiredCandidates.Count}",
            "Non-claims: no live providers; no provider SDKs; no persistence/API/cloud; no PDF/OCR; no PHP compatibility"
        };
    }
}
