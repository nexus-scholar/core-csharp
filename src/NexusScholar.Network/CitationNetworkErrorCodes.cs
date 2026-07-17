namespace NexusScholar.Network;

public static class CitationNetworkErrorCodes
{
    public const string InvalidCorpusSnapshot = "invalid-corpus-snapshot-reference";
    public const string DuplicateResolvedNode = "duplicate-resolved-node";
    public const string DuplicateEdge = "duplicate-direct-citation-edge";
    public const string MissingCitingNode = "missing-citing-node";
    public const string MissingResolvedTarget = "missing-resolved-citation-target";
    public const string InvalidEvidence = "invalid-citation-evidence";
    public const string UnsupportedEdgeKind = "unsupported-citation-edge-kind";
    public const string UnsupportedAlgorithm = "unsupported-citation-algorithm";
}
