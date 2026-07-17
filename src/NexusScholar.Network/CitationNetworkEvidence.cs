using NexusScholar.Kernel;

namespace NexusScholar.Network;

public sealed record CitationNetworkEvidenceRef(string Kind, string Id, ContentDigest Digest)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        if (!Digest.IsValid)
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.InvalidEvidence,
                "Citation evidence digest must be a valid content digest.");
        }

        return new CanonicalJsonObject()
            .Add("kind", Guard.NotBlank(Kind, nameof(Kind)))
            .Add("id", Guard.NotBlank(Id, nameof(Id)))
            .Add("digest", Digest.ToString());
    }
}
