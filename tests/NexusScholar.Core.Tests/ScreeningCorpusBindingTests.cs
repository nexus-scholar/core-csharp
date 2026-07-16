using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;
using NexusScholar.Screening.CorpusSnapshots;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ScreeningCorpusBindingTests
{
    [TestMethod]
    public void Binding_round_trips_and_creates_exact_snapshot_bound_screening_policy()
    {
        var authority = BuildAuthority();
        var binding = ScreeningCorpusBindingAuthority.Create("binding-1", authority.SourceResult, authority.Snapshot);
        var bytes = ScreeningCorpusBindingCanonicalCodec.Serialize(binding);
        var reopened = ScreeningCorpusBindingCanonicalCodec.Rehydrate(
            bytes, binding.BindingDigest, authority.SourceResult, authority.Snapshot);
        var protocol = BuildVerifiedProtocol();
        var snapshotPolicy = ScreeningCorpusBindingAuthority.CreateConductPolicy(
            reopened,
            authority.SourceResult,
            "screening-policy-1",
            "candidate-set-1",
            protocol,
            BuildAuthorityCriteria(protocol),
            1,
            [new ScreeningConductRoleAssignment("reviewer-1", "reviewer")],
            [],
            [new ScreeningExclusionReason("wrong-population", ScreeningStages.TitleAbstract)],
            new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"),
            FixedNow);

        Assert.AreEqual(binding.BindingDigest, reopened.BindingDigest);
        CollectionAssert.AreEqual(new[] { "candidate-a", "candidate-c" }, reopened.ScreeningCandidateIds.ToArray());
        CollectionAssert.AreEqual(
            reopened.ScreeningCandidateIds.ToArray(),
            snapshotPolicy.Policy.CandidateSet.Candidates.Select(item => item.CandidateId).ToArray());
        Assert.AreEqual(0, snapshotPolicy.Policy.CandidateSet.UnresolvedCandidates.Count);
        Assert.IsTrue(snapshotPolicy.Policy.CandidateSet.Locked);
        Assert.AreEqual(ScreeningSourceKinds.LockedReviewableCandidateSet, snapshotPolicy.Policy.CandidateSet.SourceKind);
        Assert.AreEqual(authority.SourceResult.Result.ResultId, snapshotPolicy.Policy.CandidateSet.CreatedFromDedupResultId);
        Assert.AreEqual(authority.SourceResult.ResultDigest.ToString(), snapshotPolicy.Policy.CandidateSet.CreatedFromDedupResultDigest);
        Assert.IsTrue(snapshotPolicy.Policy.CandidateSet.SourceRefs.Any(item =>
            item == $"screening-corpus-binding:{binding.BindingId}:{binding.BindingDigest}"));

        var policyBytes = ScreeningConductCanonicalCodec.Serialize(snapshotPolicy.Policy);
        var policyReopened = ScreeningConductCanonicalCodec.RehydratePolicy(
            policyBytes, snapshotPolicy.Policy.Digest, snapshotPolicy.Policy);
        Assert.AreSame(snapshotPolicy.Policy, policyReopened);
    }

    [TestMethod]
    public void Binding_rejects_stale_source_authority()
    {
        var authority = BuildAuthority();
        var otherSource = BuildSourceResult(authority.Policy.PolicyId, "other-result");

        var error = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingAuthority.Create("binding-stale", otherSource, authority.Snapshot));

        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.StaleSourceBinding, error.Category);
    }

    [TestMethod]
    public void Rehydrate_rejects_altered_missing_and_noncanonical_membership()
    {
        var authority = BuildAuthority();
        var binding = ScreeningCorpusBindingAuthority.Create("binding-tamper", authority.SourceResult, authority.Snapshot);
        var group = binding.GroupUnits.Single();
        var reversedMembers = group.MemberCandidateIds.Reverse().ToArray();
        var altered = ToUnverified(binding, groupUnits: [group with { MemberCandidateIds = reversedMembers }]);
        var missing = ToUnverified(binding, groupUnits: []);
        var representativeNotMember = ToUnverified(binding, groupUnits: [group with { ScreeningCandidateId = "candidate-c" }]);
        var unresolvedDigestMismatch = new UnverifiedScreeningCorpusBinding(
            ScreeningCorpusBindingConstants.SchemaId,
            ScreeningCorpusBindingConstants.SchemaVersion,
            binding.BindingId,
            binding.SourceResultId,
            binding.SourceResultDigest,
            binding.SnapshotId,
            binding.SnapshotRecordDigest,
            binding.DecisionSetDigest,
            binding.GroupUnits,
            [binding.UnresolvedUnits.Single() with { CandidateDigest = ContentDigest.Sha256Utf8("wrong-candidate") }],
            binding.BindingDigest);

        var alteredError = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingAuthority.Rehydrate(altered, authority.SourceResult, authority.Snapshot));
        var missingError = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingAuthority.Rehydrate(missing, authority.SourceResult, authority.Snapshot));
        var representativeError = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingAuthority.Rehydrate(representativeNotMember, authority.SourceResult, authority.Snapshot));
        var unresolvedError = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingAuthority.Rehydrate(unresolvedDigestMismatch, authority.SourceResult, authority.Snapshot));

        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.NonCanonicalBinding, alteredError.Category);
        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.NonCanonicalBinding, missingError.Category);
        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.NonCanonicalBinding, representativeError.Category);
        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.NonCanonicalBinding, unresolvedError.Category);
    }

    [TestMethod]
    public void Canonical_codec_rejects_unknown_fields_and_stale_digest()
    {
        var authority = BuildAuthority();
        var binding = ScreeningCorpusBindingAuthority.Create("binding-codec", authority.SourceResult, authority.Snapshot);
        var bytes = ScreeningCorpusBindingCanonicalCodec.Serialize(binding);
        var unknownContent = AddProperty(binding.DigestEnvelope.Content, "unknown", CanonicalJsonValue.From(true));
        var unknownEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            ScreeningCorpusBindingConstants.SchemaId,
            ScreeningCorpusBindingConstants.SchemaVersion,
            unknownContent);
        var withUnknownField = CanonicalJsonSerializer.SerializeToUtf8Bytes(unknownEnvelope.ToCanonicalJsonObject());

        var groups = (CanonicalJsonArray)binding.DigestEnvelope.Content.Properties["group_units"];
        var firstGroup = (CanonicalJsonObject)groups.Items[0];
        var nestedUnknown = AddProperty(firstGroup, "unknown", CanonicalJsonValue.From(true));
        var nestedGroups = CanonicalJsonValue.Array([nestedUnknown, .. groups.Items.Skip(1)]);
        var nestedContent = ReplaceProperty(binding.DigestEnvelope.Content, "group_units", nestedGroups);
        var nestedEnvelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            ScreeningCorpusBindingConstants.SchemaId,
            ScreeningCorpusBindingConstants.SchemaVersion,
            nestedContent);
        var withNestedUnknown = CanonicalJsonSerializer.SerializeToUtf8Bytes(nestedEnvelope.ToCanonicalJsonObject());

        var unknownError = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingCanonicalCodec.Rehydrate(
                withUnknownField, unknownEnvelope.ComputeDigest(), authority.SourceResult, authority.Snapshot));
        var nestedUnknownError = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingCanonicalCodec.Rehydrate(
                withNestedUnknown, nestedEnvelope.ComputeDigest(), authority.SourceResult, authority.Snapshot));
        var staleError = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingCanonicalCodec.Rehydrate(
                bytes, ContentDigest.Sha256Utf8("wrong"), authority.SourceResult, authority.Snapshot));

        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.InvalidBinding, unknownError.Category);
        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.InvalidBinding, nestedUnknownError.Category);
        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.InvalidBinding, staleError.Category);
    }

    [TestMethod]
    public void Screening_exposes_no_public_arbitrary_candidate_set_policy_factory()
    {
        var exposesCandidateSetFactory = typeof(ScreeningConductPolicy)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Any(method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ScreeningCandidateSet)));

        Assert.IsFalse(exposesCandidateSetFactory);
    }

    [TestMethod]
    public void Legacy_raw_candidate_policy_cannot_be_presented_as_snapshot_bound()
    {
        var authority = BuildAuthority();
        var binding = ScreeningCorpusBindingAuthority.Create("binding-legacy", authority.SourceResult, authority.Snapshot);
        var protocol = BuildVerifiedProtocol();
        var legacy = ScreeningConductPolicy.Create(
            "legacy-policy",
            "legacy-set",
            DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(authority.SourceResult.Result)),
            protocol,
            BuildAuthorityCriteria(protocol),
            1,
            [new ScreeningConductRoleAssignment("reviewer-1", "reviewer")],
            [],
            [],
            new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"),
            FixedNow);

        var error = Assert.ThrowsExactly<ScreeningCorpusBindingException>(() =>
            ScreeningCorpusBindingAuthority.VerifyConductPolicyBinding(binding, legacy));

        Assert.AreEqual(ScreeningCorpusBindingErrorCodes.MembershipMismatch, error.Category);
    }

    private static UnverifiedScreeningCorpusBinding ToUnverified(
        VerifiedScreeningCorpusBinding binding,
        IReadOnlyList<ScreeningCorpusGroupUnit>? groupUnits = null) => new(
            ScreeningCorpusBindingConstants.SchemaId,
            ScreeningCorpusBindingConstants.SchemaVersion,
            binding.BindingId,
            binding.SourceResultId,
            binding.SourceResultDigest,
            binding.SnapshotId,
            binding.SnapshotRecordDigest,
            binding.DecisionSetDigest,
            groupUnits ?? binding.GroupUnits,
            binding.UnresolvedUnits,
            binding.BindingDigest);

    private static CanonicalJsonObject AddProperty(CanonicalJsonObject source, string name, CanonicalJsonValue value)
    {
        var result = new CanonicalJsonObject();
        foreach (var item in source.Properties)
            result.Add(item.Key, item.Value);
        result.Add(name, value);
        return result;
    }

    private static CanonicalJsonObject ReplaceProperty(CanonicalJsonObject source, string name, CanonicalJsonValue value)
    {
        var result = new CanonicalJsonObject();
        foreach (var item in source.Properties)
            result.Add(item.Key, string.Equals(item.Key, name, StringComparison.Ordinal) ? value : item.Value);
        return result;
    }

    private static Authority BuildAuthority()
    {
        var policy = BuildDeduplicationPolicy();
        var sourceResult = BuildSourceResult(policy.PolicyId, "dedup-result-screening-binding");
        var snapshot = CorpusSnapshotService.CreateBaseline(
            "snapshot-screening-binding",
            sourceResult,
            policy,
            policy.IssuedByActorId,
            policy.IssuedByRole,
            new FixedClock(FixedNow));
        return new Authority(policy, sourceResult, snapshot);
    }

    private static VerifiedDeduplicationAuthorityPolicy BuildDeduplicationPolicy() =>
        DeduplicationAuthorityPolicy.CreatePolicyMaterial(new UnverifiedDeduplicationAuthorityPolicy(
            DeduplicationAuthorityPolicyConstants.SchemaId,
            DeduplicationAuthorityPolicyConstants.SchemaVersion,
            DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
            DeduplicationService.PolicyId,
            "1.0.0",
            [new DeduplicationAuthorityPolicyActorRole("alice", "owner", DeduplicationAuthorityPolicyConstants.HumanSubjectKind)],
            DeduplicationAuthorityPolicyConstants.ClosedActions.ToArray(),
            [
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, ["duplicate"]),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, ["distinct"]),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, ["uncertain"])
            ],
            false,
            "alice",
            "owner",
            FixedNow,
            null,
            null,
            null));

    private static VerifiedDeduplicationAuthorityResultDigest BuildSourceResult(string policyId, string resultId)
    {
        var candidateA = BuildCandidate("candidate-a", true);
        var candidateB = BuildCandidate("candidate-b", true);
        var candidateC = BuildCandidate("candidate-c", false);
        var cluster = new DedupCluster(
            "cluster-a-b",
            [candidateA, candidateB],
            new DedupRepresentativeResult(
                candidateA.CandidateId,
                candidateA.Title,
                candidateA.PrimaryWorkId,
                candidateA.WorkIds,
                [candidateA.Source.SourceSightingId],
                1d,
                []),
            [new DedupEvidence(
                "evidence-a-b",
                DedupEvidenceKind.SourceSighting,
                candidateA.CandidateId,
                candidateB.CandidateId,
                "source-sighting",
                true,
                0.99d,
                policyId,
                DeduplicationService.PolicyVersion)]);
        var result = new DeduplicationResult(
            resultId,
            DeduplicationAuthorityDigests.ResultSchemaId,
            DeduplicationAuthorityDigests.ResultSchemaVersion,
            policyId,
            DeduplicationService.PolicyVersion,
            0.95d,
            new Dictionary<string, int>(),
            [],
            [],
            [candidateA, candidateB, candidateC],
            [cluster],
            [],
            [candidateC],
            [],
            [],
            [],
            []);
        return DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
    }

    private static DedupCandidateRecord BuildCandidate(string candidateId, bool stable) => new(
        candidateId,
        $"Title {candidateId}",
        stable,
        stable ? $"doi:{candidateId}" : null,
        stable ? [$"work:{candidateId}"] : [],
        [],
        new DedupSightingRef("search", $"trace-{candidateId}", $"sighting-{candidateId}", "provider", "tool"),
        ["author"],
        2026,
        null,
        null,
        ["keyword"]);

    private static VerifiedProtocolVersion BuildVerifiedProtocol()
    {
        var seed = new ProtocolVersion(
            "protocol-screening-v1", "protocol-screening", "project-1", 1, ProtocolStatus.Approved,
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("template")),
            new ProtocolIntent("screening", "screen records"), new CanonicalJsonObject(),
            [], [], [], ContentDigest.Sha256Utf8("placeholder"),
            ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId, ["approval-1"], FixedNow);
        var version = new ProtocolVersion(
            seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template, seed.Intent,
            seed.Values, seed.RequiredDecisions, seed.Decisions, seed.Waivers,
            seed.ToProtocolContentDigestEnvelope().ComputeDigest(), seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt);
        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), []);
    }

    private static ScreeningCriteria BuildAuthorityCriteria(VerifiedProtocolVersion protocol) => new(
        "criteria-authority", "1.0.0", ScreeningStages.TitleAbstract,
        CanonicalJsonValue.From("include"), CanonicalJsonValue.From("exclude"), true,
        protocol.Version.Id, protocol.Version.ContentDigest.ToString(),
        approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
        approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
        currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed record Authority(
        VerifiedDeduplicationAuthorityPolicy Policy,
        VerifiedDeduplicationAuthorityResultDigest SourceResult,
        VerifiedCorpusSnapshot Snapshot);

    private static readonly DateTimeOffset FixedNow = new(2026, 7, 16, 2, 0, 0, TimeSpan.Zero);
}
