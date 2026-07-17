using NexusScholar.Kernel;
using NexusScholar.Shared;

namespace NexusScholar.Network;

public sealed record CitationNetworkCitationTarget
{
    private CitationNetworkCitationTarget(string value, bool resolved)
    {
        Value = Guard.NotBlank(value, nameof(value));
        IsResolved = resolved;
    }

    public bool IsResolved { get; }

    public string Value { get; }

    public string NormalizedKey => IsResolved ? $"resolved:{Value}" : $"unresolved:{Value}";

    public static CitationNetworkCitationTarget Resolved(string nodeId)
    {
        _ = WorkId.Parse(Guard.NotBlank(nodeId, nameof(nodeId)));
        return new CitationNetworkCitationTarget(nodeId, true);
    }

    public static CitationNetworkCitationTarget Unresolved(string unresolvedTarget)
    {
        return new CitationNetworkCitationTarget(unresolvedTarget, false);
    }

    public CanonicalJsonObject ToCanonicalJson() => IsResolved
        ? new CanonicalJsonObject().Add("kind", "resolved").Add("node_id", Value)
        : new CanonicalJsonObject().Add("kind", "unresolved").Add("target_id", Value);
}
