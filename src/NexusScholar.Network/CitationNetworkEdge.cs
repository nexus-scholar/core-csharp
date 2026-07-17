using NexusScholar.Kernel;

namespace NexusScholar.Network;

public static class CitationEdgeKind
{
    public const string DirectCitation = "direct-citation";
}

public sealed class CitationNetworkEdge
{
    internal CitationNetworkEdge(string sourceNodeId, string kind, CitationNetworkCitationTarget target, CitationNetworkEvidenceRef evidence)
    {
        CitingNodeId = Guard.NotBlank(sourceNodeId, nameof(sourceNodeId));
        Kind = NormalizeKind(kind);
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        if (!Evidence.Digest.IsValid)
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.InvalidEvidence,
                "Citation evidence digest must be a valid content digest.");
        }
    }

    public string Kind { get; }

    public string CitingNodeId { get; }

    public CitationNetworkCitationTarget Target { get; }

    public CitationNetworkEvidenceRef Evidence { get; }

    public static CitationNetworkEdge DirectCitation(
        string sourceNodeId,
        CitationNetworkCitationTarget target,
        CitationNetworkEvidenceRef evidence)
    {
        return new CitationNetworkEdge(sourceNodeId, CitationEdgeKind.DirectCitation, target, evidence);
    }

    public bool IsDirect => string.Equals(Kind, CitationEdgeKind.DirectCitation, StringComparison.Ordinal);

    public string NormalizedEdgeKey => $"{CitingNodeId}|{Kind}|{Target.NormalizedKey}";

    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("source_node_id", CitingNodeId)
        .Add("kind", Kind)
        .Add("target", Target.ToCanonicalJson())
        .Add("evidence", Evidence.ToCanonicalJson());

    private static string NormalizeKind(string kind)
    {
        if (!string.Equals(Guard.NotBlank(kind, nameof(kind)).ToLowerInvariant(), CitationEdgeKind.DirectCitation, StringComparison.Ordinal))
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.UnsupportedEdgeKind,
                "Only direct-citation edges are supported.");
        }

        return CitationEdgeKind.DirectCitation;
    }
}
