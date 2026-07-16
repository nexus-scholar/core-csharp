namespace NexusScholar.ResearchWorkspace;

public static class WorkspacePlanReportWriter
{
    public static string Format(ResearchWorkspaceAnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return string.Join(
            "\n",
            "# Nexus workspace review report",
            string.Empty,
            "This report is a read-only local workspace analysis projection. It is not Core scientific authority.",
            string.Empty,
            $"Mode: {result.WorkspacePlan.Mode}",
            $"Import traces: {result.ImportTraces.Count}",
            $"Imported records: {result.ImportedRecordCount}",
            $"Parser warnings: {result.ParserWarningCount}",
            $"Skipped records: {result.SkippedRecordCount}",
            $"Exact duplicate clusters: {result.DeduplicationResult.Clusters.Count}",
            $"Review-required duplicate candidates: {result.DeduplicationResult.ReviewRequiredCandidates.Count}",
            $"Workspace blocks: {result.WorkspacePlan.Blocks.Count}",
            string.Empty);
    }
}
