using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Core.Tests;

internal static class Fe07TestAuthority
{
    internal static VerifiedProtocolAmendment CreateAmendment(VerifiedProtocolVersion protocol, string decisionKey, IClock clock)
    {
        var ids = new Ids(); var policy = ApprovalPolicy.ExplicitCustomSingleResearcher(); var actor = ProtocolActor.Human("researcher-1");
        var notice = new ProtocolInvalidationNotice("notice-shared", "placeholder", decisionKey, protocol.Version.ContentDigest,
            "fe07-node", "replace", "rerun", clock.UtcNow);
        var amendment = ProtocolAmendment.Create(ids, protocol.Version, "protocol-version-successor", actor, clock,
            "Scientific definition changed.", [decisionKey], [notice], policy);
        var seed = new ProtocolVersion(amendment.ProducesVersionId, protocol.Version.ProtocolId, protocol.Version.ProjectId,
            protocol.Version.VersionNumber + 1, ProtocolStatus.Approved, protocol.Version.Template, protocol.Version.Intent,
            protocol.Version.Values, protocol.Version.RequiredDecisions, protocol.Version.Decisions, protocol.Version.Waivers,
            ContentDigest.Sha256Utf8("placeholder"), protocol.Version.ApprovalPolicyId, protocol.Version.ApprovalIds, clock.UtcNow,
            protocol.Version.Id, amendmentId: amendment.AmendmentId, unresolvedDecisions: protocol.Version.UnresolvedDecisions);
        var producedVersion = new ProtocolVersion(seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template,
            seed.Intent, seed.Values, seed.RequiredDecisions, seed.Decisions, seed.Waivers, seed.ToProtocolContentDigestEnvelope().ComputeDigest(),
            seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt, seed.SupersedesVersionId, seed.SupersededByVersionId, seed.AmendmentId, seed.UnresolvedDecisions);
        var produced = new VerifiedProtocolVersion(producedVersion, protocol.ApprovalPolicy, protocol.Approvals);
        return new VerifiedProtocolAmendment(amendment, ContentDigest.Sha256CanonicalJson(amendment.ToCanonicalJson()), policy, protocol, produced, []);
    }

    internal static VerifiedProtocolAmendment Foreign(VerifiedProtocolAmendment source)
    {
        var material = source.Amendment with
        {
            AmendsVersionId = "foreign-version",
            PreviousContentDigest = ContentDigest.Sha256Utf8("foreign-protocol")
        };
        return new VerifiedProtocolAmendment(material, ContentDigest.Sha256CanonicalJson(material.ToCanonicalJson()), source.Policy,
            source.PreviousVersion, source.ProducedVersion, source.Approvals);
    }

    private sealed class Ids : IIdGenerator
    {
        private int _next = 200;
        public Guid NewId() => new(_next++, 0, 0, new byte[8]);
    }
}
