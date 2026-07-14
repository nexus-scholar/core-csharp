using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class DeduplicationDecisionTests
{
    private static readonly IClock Clock = new FixedClock();

    [TestMethod]
    public void Create_decision_material_builds_verified_material_and_accepts_canonical_inputs()
    {
        var policy = BuildPolicy(DeduplicationService.PolicyId, rationale: true);
        var verifiedPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy);
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(verifiedPolicy, sourceResult);
        var evidenceReferences = new[]
        {
            BuildEvidenceReference(target.Evidence[0], 0),
            BuildEvidenceReference(target.Evidence[1], 1)
        };

        var decision = DeduplicationDecision.CreateDecisionMaterial(
            BuildDecision(
                verifiedPolicy,
                sourceResult,
                target,
                evidenceReferences),
            Clock,
            verifiedPolicy,
            sourceResult,
            target);

        Assert.AreEqual("merge", decision.ActionType);
        Assert.AreEqual(target.TargetId, decision.TargetId);
        Assert.AreEqual(verifiedPolicy.PolicyId, decision.PolicyId);
        Assert.AreEqual(2, decision.EvidenceReferences.Count);
        CollectionAssert.AreEquivalent(
            new[] { DeduplicationDecisionConstants.InvalidationDecisionKind, DeduplicationDecisionConstants.InvalidationSnapshotKind },
            decision.InvalidationEffects.Select(effect => effect.RecordKind).ToArray());
    }

    [TestMethod]
    public void Rehydrate_decision_material_rejects_non_canonical_evidence_order()
    {
        var policy = BuildPolicy(DeduplicationService.PolicyId, rationale: false);
        var verifiedPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy);
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(verifiedPolicy, sourceResult);

        var material = BuildDecision(
            verifiedPolicy,
            sourceResult,
            target,
            new[]
            {
                BuildEvidenceReference(target.Evidence[0], 1),
                BuildEvidenceReference(target.Evidence[1], 0)
            },
            reasonCode: "duplicate");
        var canonical = DeduplicationDecision.CreateDecisionMaterial(material, Clock, verifiedPolicy, sourceResult, target);

        var reversed = material with
        {
            EvidenceReferences = material.EvidenceReferences.Reverse().ToArray(),
            DecisionDigest = canonical.DecisionDigest
        };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.RehydrateDecisionMaterial(reversed, verifiedPolicy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.NonCanonicalAuthorityMaterial, error.Category);
    }

    [TestMethod]
    public void Rehydrate_decision_material_rejects_unauthorized_actor()
    {
        var policy = BuildPolicy(DeduplicationService.PolicyId, rationale: true);
        var verifiedPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy);
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(verifiedPolicy, sourceResult);
        var material = BuildDecision(
            verifiedPolicy,
            sourceResult,
            target,
            BuildEvidenceReferences(target),
            actorId: "stranger",
            actorRole: "external");

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(material, Clock, verifiedPolicy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.UnauthorizedDecisionActor, error.Category);
    }

    [TestMethod]
    public void Create_decision_material_rejects_unsupported_reason_code()
    {
        var policy = BuildPolicy(DeduplicationService.PolicyId, rationale: false);
        var verifiedPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy);
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(verifiedPolicy, sourceResult);
        var material = BuildDecision(
            verifiedPolicy,
            sourceResult,
            target,
            BuildEvidenceReferences(target),
            reasonCode: "not-allowed");

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(material, Clock, verifiedPolicy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.UnsupportedReasonCode, error.Category);
    }

    [TestMethod]
    public void Create_decision_material_rejects_partial_source_snapshot_binding()
    {
        var policy = BuildPolicy(DeduplicationService.PolicyId, rationale: false);
        var verifiedPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy);
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(verifiedPolicy, sourceResult);
        var material = BuildDecision(
            verifiedPolicy,
            sourceResult,
            target,
            BuildEvidenceReferences(target),
            sourceSnapshotId: "snapshot-1") with
        {
            SourceSnapshotRecordDigest = null
        };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(material, Clock, verifiedPolicy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.InvalidDecision, error.Category);
    }

    [TestMethod]
    public void Rehydrate_decision_material_rejects_unsupported_invalidation_kind()
    {
        var policy = BuildPolicy(DeduplicationService.PolicyId, rationale: false);
        var verifiedPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy);
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(verifiedPolicy, sourceResult);
        var material = BuildDecision(
            verifiedPolicy,
            sourceResult,
            target,
            BuildEvidenceReferences(target),
            invalidationKind: "unsupported-kind");

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(material, Clock, verifiedPolicy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.UnsupportedInvalidationKind, error.Category);
    }

    [TestMethod]
    public void Rehydrate_decision_material_rejects_stale_authority_source_binding()
    {
        var policy = BuildPolicy(DeduplicationService.PolicyId, rationale: false);
        var verifiedPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy);
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(verifiedPolicy, sourceResult);

        var decision = DeduplicationDecision.CreateDecisionMaterial(
            BuildDecision(
                verifiedPolicy,
                sourceResult,
                target,
                BuildEvidenceReferences(target)),
            Clock,
            verifiedPolicy,
            sourceResult,
            target);

        var mutated = BuildDecision(
                verifiedPolicy,
                sourceResult,
                target,
                BuildEvidenceReferences(target))
            with
        {
            AuthoritySourceId = "policy-other",
            DecisionDigest = decision.DecisionDigest
        };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.RehydrateDecisionMaterial(mutated, verifiedPolicy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.StaleAuthoritySourceBinding, error.Category);
    }

    private static UnverifiedDeduplicationAuthorityDecision BuildDecision(
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityReviewTargetDigest target,
        IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> evidenceReferences,
        int index = 0,
        string? actorId = null,
        string? actorRole = null,
        string reasonCode = "duplicate",
        string? sourceSnapshotId = null,
        string invalidationKind = DeduplicationDecisionConstants.InvalidationDecisionKind,
        bool includeRationale = true)
    {
        _ = index;
        var digestPair = DeduplicationAuthorityDigests.CreateEvidenceDigestMaterial(sourceResult.Result.Evidence[0]);
        return new UnverifiedDeduplicationAuthorityDecision(
            SchemaId: DeduplicationDecisionConstants.SchemaId,
            SchemaVersion: DeduplicationDecisionConstants.SchemaVersion,
            DecisionId: $"decision-{policy.PolicyId}-{index}",
            ActionType: DeduplicationAuthorityPolicyConstants.MergeAction,
            PolicyId: policy.PolicyId,
            PolicyVersion: policy.PolicyVersion,
            TargetKind: target.TargetKind,
            TargetId: target.TargetId,
            TargetContentDigest: target.TargetDigest,
            SourceResultId: sourceResult.Result.ResultId,
            SourceResultDigest: sourceResult.ResultDigest,
            SourceSnapshotId: sourceSnapshotId,
            SourceSnapshotRecordDigest: sourceSnapshotId is null ? null : sourceResult.ResultDigest,
            EvidenceReferences: evidenceReferences,
            ActorId: actorId ?? policy.AuthorizedActorRoles[0].ActorId,
            ActorRole: actorRole ?? policy.AuthorizedActorRoles[0].Role,
            AuthoritySourceId: policy.PolicyId,
            AuthoritySourceKind: DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
            AuthoritySourceDigest: policy.PolicyDigest,
            Rationale: includeRationale && policy.RequiresRationale ? "Reviewed by a human decision" : null,
            ReasonCode: reasonCode,
            DecidedAt: new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero),
            SupersedesDecisionId: null,
            InvalidationEffects: new[]
            {
                new DeduplicationAuthorityDecisionInvalidationEffect(
                    invalidationKind,
                    $"{invalidationKind}-target-{index}",
                    digestPair.EvidenceDigest),
                new DeduplicationAuthorityDecisionInvalidationEffect(
                    DeduplicationDecisionConstants.InvalidationSnapshotKind,
                    $"snapshot-{index}",
                    target.TargetDigest),
            },
            DecisionDigest: null);
    }

    private static DeduplicationAuthorityDecisionEvidenceReference BuildEvidenceReference(DedupEvidence evidence, int sequence)
    {
        _ = sequence;
        return new DeduplicationAuthorityDecisionEvidenceReference(
            evidence.Kind.ToString(),
            evidence.EvidenceId,
            DigestScope.CanonicalJsonRecord.ToString(),
            DeduplicationAuthorityDigests.CreateEvidenceDigestMaterial(evidence).EvidenceDigest);
    }

    private static IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> BuildEvidenceReferences(
        VerifiedDeduplicationAuthorityReviewTargetDigest target) =>
        target.Evidence.Select((evidence, index) => BuildEvidenceReference(evidence, index)).ToArray();

    private static VerifiedDeduplicationAuthorityReviewTargetDigest BuildReviewTarget(
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityResultDigest sourceResult)
    {
        _ = policy;
        var reviewPair = new DedupReviewCandidate("candidate-b", "candidate-a", 0.96, 0.95);
        var evidence = new[]
        {
            BuildEvidence("evidence-a", "candidate-a", "candidate-b", sourceResult.Result.PolicyId!, sourceResult.Result.PolicyVersion!),
            BuildEvidence("evidence-b", "candidate-b", "candidate-a", sourceResult.Result.PolicyId!, sourceResult.Result.PolicyVersion!)
        };
        return DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(
            sourceResult,
            reviewPair,
            new[] { "candidate-a", "candidate-b" },
            evidence);
    }

    private static VerifiedDeduplicationAuthorityResultDigest BuildVerifiedResult(string policyId)
    {
        var candidates = new[]
        {
            BuildCandidate("candidate-a"),
            BuildCandidate("candidate-b")
        };
        var result = new DeduplicationResult(
            "result-1",
            DeduplicationAuthorityDigests.ResultSchemaId,
            DeduplicationAuthorityDigests.ResultSchemaVersion,
            policyId,
            DeduplicationService.PolicyVersion,
            0.95,
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)),
            Array.Empty<string>(),
            Array.Empty<string>(),
            candidates,
            Array.Empty<DedupCluster>(),
            new[]
            {
                BuildEvidence("evidence-a", "candidate-a", "candidate-b", policyId, DeduplicationService.PolicyVersion),
                BuildEvidence("evidence-b", "candidate-b", "candidate-a", policyId, DeduplicationService.PolicyVersion)
            },
            Array.Empty<DedupCandidateRecord>(),
            new[] { new DedupReviewCandidate("candidate-a", "candidate-b", 0.96, 0.95) },
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            Array.Empty<string>());
        return DeduplicationAuthorityDigests.CreateResultDigestMaterial(result);
    }

    private static VerifiedDeduplicationAuthorityResultDigest BuildVerifiedResult()
    {
        return BuildVerifiedResult(DeduplicationService.PolicyId);
    }

    private static UnverifiedDeduplicationAuthorityPolicy BuildPolicy(
        string policyId,
        bool rationale = true)
    {
        return new UnverifiedDeduplicationAuthorityPolicy(
            SchemaId: DeduplicationAuthorityPolicyConstants.SchemaId,
            SchemaVersion: DeduplicationAuthorityPolicyConstants.SchemaVersion,
            AuthoritySourceKind: DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
            PolicyId: policyId,
            PolicyVersion: DeduplicationService.PolicyVersion,
            AuthorizedActorRoles: new[]
            {
                new DeduplicationAuthorityPolicyActorRole("alice", "owner")
            },
            AllowedActions: DeduplicationAuthorityPolicyConstants.ClosedActions,
            ReasonCodesByAction: new[]
            {
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, new[] { "duplicate" }),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, new[] { "disputed" }),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, new[] { "uncertain" })
            },
            RequiresRationale: rationale,
            IssuedByActorId: "alice",
            IssuedByRole: "owner",
            IssuedAt: new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero),
            SupersedesPolicyId: null,
            SupersedesPolicyDigest: null,
            PolicyDigest: null);
    }

    private static DedupCandidateRecord BuildCandidate(string id)
    {
        return new DedupCandidateRecord(
            id,
            $"Title {id}",
            false,
            $"{id}-doi",
            new[] { $"work-{id}" },
            new[] { $"source-{id}" },
            BuildSighting(id),
            new[] { "author" },
            2026,
            null,
            null,
            new[] { "keyword" });
    }

    private static DedupEvidence BuildEvidence(
        string evidenceId,
        string subjectCandidateId,
        string objectCandidateId,
        string policyId,
        string policyVersion)
    {
        return new DedupEvidence(
            evidenceId,
            DedupEvidenceKind.SourceSighting,
            subjectCandidateId,
            objectCandidateId,
            "evidence",
            true,
            0.96,
            policyId,
            policyVersion);
    }

    private static DedupSightingRef BuildSighting(string id) => new(
        SourceKind: "openalex",
        SourceTraceId: $"trace-{id}",
        SourceSightingId: $"sighting-{id}",
        ProviderAlias: "provider",
        SourceDatabaseOrTool: "tool",
        SourceRecordId: $"record-{id}",
        SourceFileDigest: null,
        SourceFileDigestScope: null,
        RawRecordDigest: null,
        SourceContext: "ctx",
        ParserWarnings: Array.Empty<DedupParserNotice>(),
        RecordNotices: Array.Empty<DedupParserNotice>());

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 7, 2, 9, 0, 0, TimeSpan.Zero);
    }
}
