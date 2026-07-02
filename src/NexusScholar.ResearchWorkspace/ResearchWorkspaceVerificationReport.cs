namespace NexusScholar.ResearchWorkspace;

public sealed record ResearchWorkspaceVerificationReport(
    int InputCount,
    int FilesUnchanged,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> DigestMismatches,
    IReadOnlyList<string> InvalidPaths,
    IReadOnlyList<string> MissingImportTraces,
    int ParserWarningCount,
    int SkippedRecordCount,
    IReadOnlyDictionary<string, int> ParserWarningCategories)
{
    public bool IsValid =>
        MissingFiles.Count == 0 &&
        DigestMismatches.Count == 0 &&
        InvalidPaths.Count == 0 &&
        MissingImportTraces.Count == 0 &&
        SkippedRecordCount == 0;
}
