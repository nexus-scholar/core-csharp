using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ProtocolDeviationAuthorityTests
{
    [TestMethod]
    public void Verified_deviation_round_trips_exact_human_approval_and_canonical_bytes()
    {
        var state = BuildState(ProtocolDeviationConstants.Deviation);
        var verified = ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(state.Record, state.Digest), state.Resolver);
        var bytes = ProtocolDeviationCanonicalCodec.Serialize(verified);

        Assert.AreEqual(state.Digest, verified.DeviationDigest);
        Assert.IsFalse(verified.BlocksFinalReporting);
        Assert.AreEqual(state.Digest, ProtocolDeviationCanonicalCodec.Rehydrate(
            bytes, state.Digest, new UnverifiedProtocolDeviation(state.Record, state.Digest), state.Resolver).DeviationDigest);
        Assert.IsFalse(verified.Deviation.ApprovalIds is string[]);
    }

    [TestMethod]
    public void Unresolved_inconsistency_is_verified_but_blocks_final_reporting()
    {
        var state = BuildState(ProtocolDeviationConstants.UnresolvedInconsistency);
        var verified = ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(state.Record, state.Digest), state.Resolver);
        Assert.IsTrue(verified.BlocksFinalReporting);
    }

    [TestMethod]
    public void Deviation_rejects_nonhuman_stale_incomplete_and_amendmentless_authority()
    {
        var state = BuildState(ProtocolDeviationConstants.Deviation);
        var nonhuman = state.Resolver with { Human = false };
        Assert.ThrowsExactly<ProtocolRuleException>(() => ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(state.Record, state.Digest), nonhuman));
        Assert.ThrowsExactly<ProtocolRuleException>(() => ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(state.Record with { ProtocolContentDigest = ContentDigest.Sha256Utf8("stale") }, state.Digest), state.Resolver));
        Assert.ThrowsExactly<ProtocolRuleException>(() => ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(state.Record with { InvalidationEffects = [] }, state.Digest), state.Resolver));
        Assert.ThrowsExactly<ProtocolRuleException>(() => ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(state.Record with { ApprovalIds = null! }, state.Digest), state.Resolver));

        var amendment = BuildState(ProtocolDeviationConstants.ApprovedAmendmentRequired);
        Assert.ThrowsExactly<ProtocolRuleException>(() => ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(amendment.Record, amendment.Digest), amendment.Resolver));
    }

    [TestMethod]
    public void Deviation_rejects_wrong_approval_target_and_noncanonical_bytes()
    {
        var state = BuildState(ProtocolDeviationConstants.Deviation);
        var wrongApproval = CreateApproval("approval-deviation", "other-deviation", state.Digest, state.Policy);
        var wrongResolver = state.Resolver with { Approval = wrongApproval };
        Assert.ThrowsExactly<ProtocolRuleException>(() => ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(state.Record, state.Digest), wrongResolver));

        var verified = ProtocolSupplementalAuthorityRehydrator.RehydrateDeviation(
            new UnverifiedProtocolDeviation(state.Record, state.Digest), state.Resolver);
        var altered = ProtocolDeviationCanonicalCodec.Serialize(verified).Concat([(byte)' ']).ToArray();
        Assert.ThrowsExactly<ProtocolRuleException>(() => ProtocolDeviationCanonicalCodec.Rehydrate(
            altered, state.Digest, new UnverifiedProtocolDeviation(state.Record, state.Digest), state.Resolver));
    }

    private static State BuildState(string classification)
    {
        var protocol = BuildProtocol();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher("deviation-policy");
        var record = new ProtocolDeviationRecord(
            "deviation-1", protocol.Version.ProtocolId, protocol.Version.Id, protocol.Version.ContentDigest, "scope",
            null, null, null, "One source was unavailable.", "Local outage.", classification,
            "Coverage may be reduced.", "Repeated the search from a preserved trace.",
            [new ProtocolDeviationEvidenceReference("search-trace", "trace-1", ContentDigest.Sha256Utf8("trace"))],
            "Disclose reduced coverage.", "limitations.data-source", Actor, Now, policy.PolicyId, ["approval-deviation"],
            [new ProtocolDeviationInvalidationEffect("screening-snapshot", "snapshot-1", ContentDigest.Sha256Utf8("snapshot"), "rebuild-report")],
            null);
        var digest = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ProtocolDeviationConstants.SchemaId,
            ProtocolDeviationConstants.SchemaVersion, record.ToCanonicalJson()).ComputeDigest();
        var approval = CreateApproval(record.ApprovalIds[0], record.DeviationId, digest, policy);
        return new State(record, digest, policy, new Resolver(protocol, policy, approval, true));
    }

    private static VerifiedProtocolSupplementalApproval CreateApproval(string approvalId, string targetId, ContentDigest targetDigest, ApprovalPolicy policy)
    {
        var seed = new ProtocolSupplementalApproval(approvalId, ProtocolSupplementalTargetTypes.Deviation, targetId, targetDigest,
            policy.PolicyId, policy.PolicyVersion, policy.Mode, ProtocolApprovalDecision.Approved, Actor, Now, null, "Approved.", null,
            ContentDigest.Sha256Utf8("placeholder"));
        return new VerifiedProtocolSupplementalApproval(new ProtocolSupplementalApproval(approvalId, seed.TargetType, targetId, targetDigest,
            policy.PolicyId, policy.PolicyVersion, policy.Mode, ProtocolApprovalDecision.Approved, Actor, Now, null, "Approved.", null,
            seed.ToDigestEnvelope().ComputeDigest()));
    }

    private static VerifiedProtocolVersion BuildProtocol()
    {
        var seed = new ProtocolVersion("protocol-deviation-v1", "protocol-deviation", "project-deviation", 1, ProtocolStatus.Approved,
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("template")),
            new ProtocolIntent("deviation", "record conduct deviation"), new CanonicalJsonObject(), [], [], [], ContentDigest.Sha256Utf8("placeholder"),
            ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId, ["approval-1"], Now);
        var version = new ProtocolVersion(seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template, seed.Intent,
            seed.Values, seed.RequiredDecisions, seed.Decisions, seed.Waivers, seed.ToProtocolContentDigestEnvelope().ComputeDigest(),
            seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt);
        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), []);
    }

    private sealed record Resolver(VerifiedProtocolVersion Protocol, ApprovalPolicy Policy,
        VerifiedProtocolSupplementalApproval Approval, bool Human) : IProtocolDeviationAuthorityResolver
    {
        public ApprovalPolicy ResolvePolicy(string targetType, string targetId) => Policy;
        public bool IsHumanActor(ActorId actorId) => Human && actorId == Actor;
        public VerifiedProtocolSupplementalApproval ResolveApproval(string approvalId) => Approval;
        public VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId) => Protocol;
        public VerifiedProtocolAmendment ResolveProtocolAmendment(string amendmentId) => throw new KeyNotFoundException();
    }

    private sealed record State(ProtocolDeviationRecord Record, ContentDigest Digest, ApprovalPolicy Policy, Resolver Resolver);
    private static readonly ActorId Actor = ActorId.From("researcher-1");
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 11, 0, 0, TimeSpan.Zero);
}
