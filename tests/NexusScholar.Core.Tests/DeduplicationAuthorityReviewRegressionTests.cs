using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class DeduplicationAuthorityReviewRegressionTests
{
    [TestMethod]
    public void Create_policy_material_rejects_wrong_schema_id_version_or_source_kind()
    {
        var wrongSchemaPolicy = BuildPolicy(
            policyId: "policy-review-regression-1",
            schemaId: "wrong.schema.id",
            schemaVersion: "0.0.0",
            authoritySourceKind: "wrong-kind");

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.CreatePolicyMaterial(wrongSchemaPolicy));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy, error.Category);
    }

    [TestMethod]
    public void Rehydrate_decision_material_rejects_wrong_schema()
    {
        var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(BuildPolicy(policyId: "policy-review-regression-2"));
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(policy, sourceResult);
        var material = BuildDecision(policy, sourceResult, target, BuildEvidenceReferences(target));

        var wrongSchema = material with
        {
            SchemaId = "wrong.schema.id",
            SchemaVersion = "0.0.0"
        };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.RehydrateDecisionMaterial(wrongSchema, policy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.InvalidDecision, error.Category);
    }

    [TestMethod]
    public void Rehydrate_decision_material_rejects_non_canonical_invalidation_order_as_non_canonical()
    {
        var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(BuildPolicy(policyId: "policy-review-regression-3"));
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(policy, sourceResult);
        var material = BuildDecision(policy, sourceResult, target, BuildEvidenceReferences(target));
        var canonical = DeduplicationDecision.CreateDecisionMaterial(material, Clock, policy, sourceResult, target);
        var nonCanonical = material with { DecisionDigest = canonical.DecisionDigest };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.RehydrateDecisionMaterial(nonCanonical, policy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.NonCanonicalAuthorityMaterial, error.Category);
    }

    [TestMethod]
    public void Create_policy_material_rejects_self_supersession()
    {
        var selfSupersedingPolicy = BuildPolicy(
            policyId: "policy-review-regression-4",
            supersedesPolicyId: "policy-review-regression-4",
            supersedesPolicyDigest: ContentDigest.Sha256Utf8("policy-review-regression-4"));

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.CreatePolicyMaterial(selfSupersedingPolicy));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy, error.Category);
    }

    [TestMethod]
    public void Create_decision_material_rejects_superseding_without_source_snapshot_pair()
    {
        var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(BuildPolicy(policyId: "policy-review-regression-5"));
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(policy, sourceResult);

        var material = BuildDecision(
            policy,
            sourceResult,
            target,
            BuildEvidenceReferences(target),
            decisionId: "decision-review-regression-5",
            supersedesDecisionId: "decision-review-regression-4");

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(material, Clock, policy, sourceResult, target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.InvalidDecision, error.Category);
    }

    [TestMethod]
    public void Create_policy_material_rejects_non_human_subject_kind()
    {
        var nonHumanPolicy = BuildPolicy(
            policyId: "policy-review-regression-6",
            actorRoles: new[]
            {
                new DeduplicationAuthorityPolicyActorRole("alice", "owner", "automated")
            });

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.CreatePolicyMaterial(nonHumanPolicy));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.UnauthorizedAuthorityActor, error.Category);
    }

    [TestMethod]
    public void Create_policy_material_with_canonical_subset_allows_only_configured_actions()
    {
        var subsetPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(
            BuildPolicy(
                policyId: "policy-review-regression-7",
                allowedActions: new[] { DeduplicationAuthorityPolicyConstants.MergeAction },
                reasonCodesByAction: new[]
                {
                    new DeduplicationAuthorityPolicyReasonGroup(
                        DeduplicationAuthorityPolicyConstants.MergeAction,
                        new[] { "duplicate" })
                }));

        Assert.AreEqual(1, subsetPolicy.AllowedActions.Count);
        CollectionAssert.AreEqual(new[] { "merge" }, subsetPolicy.AllowedActions.ToArray());
        Assert.AreEqual(1, subsetPolicy.ReasonCodesForAction(DeduplicationAuthorityPolicyConstants.MergeAction).Count);
        CollectionAssert.AreEqual(new[] { "duplicate" }, subsetPolicy.ReasonCodesForAction(DeduplicationAuthorityPolicyConstants.MergeAction).ToArray());

        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(subsetPolicy, sourceResult);
        var accepted = DeduplicationDecision.CreateDecisionMaterial(
            BuildDecision(
                subsetPolicy,
                sourceResult,
                target,
                BuildEvidenceReferences(target)),
            Clock,
            subsetPolicy,
            sourceResult,
            target);
        Assert.AreEqual(DeduplicationAuthorityPolicyConstants.MergeAction, accepted.ActionType);

        var rejected = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(
                BuildDecision(
                    subsetPolicy,
                    sourceResult,
                    target,
                    BuildEvidenceReferences(target),
                    actionType: DeduplicationAuthorityPolicyConstants.KeepSeparateAction,
                    reasonCode: "disputed",
                    decisionId: "decision-review-regression-7"),
                Clock,
                subsetPolicy,
                sourceResult,
                target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.UnsupportedReasonCode, rejected.Category);
    }

    [TestMethod]
    public void Create_decision_material_requires_exact_verified_evidence_set()
    {
        var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(BuildPolicy(policyId: "policy-review-regression-8"));
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(policy, sourceResult);
        var fullEvidence = BuildEvidenceReferences(target);

        var partial = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(
                BuildDecision(
                    policy,
                    sourceResult,
                    target,
                    fullEvidence.Take(1).ToArray(),
                    decisionId: "decision-review-regression-8"),
                Clock,
                policy,
                sourceResult,
                target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.InvalidDecision, partial.Category);

        var unrelated = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(
                BuildDecision(
                    policy,
                    sourceResult,
                    target,
                    new[]
                    {
                        fullEvidence[0],
                        new DeduplicationAuthorityDecisionEvidenceReference(
                            fullEvidence[0].Kind,
                            "evidence-unrelated",
                            DigestScope.CanonicalJsonRecord.ToString(),
                            ContentDigest.Sha256Utf8("evidence-unrelated"))
                    },
                    decisionId: "decision-review-regression-8b"),
                Clock,
                policy,
                sourceResult,
                target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.InvalidDecision, unrelated.Category);
    }

    [TestMethod]
    public void Create_decision_material_prefers_clock_timestamp_over_payload_timestamp()
    {
        var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(BuildPolicy(policyId: "policy-review-regression-9"));
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(policy, sourceResult);
        var material = BuildDecision(
            policy,
            sourceResult,
            target,
            BuildEvidenceReferences(target),
            decisionId: "decision-review-regression-9",
            decidedAt: new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var fixedClock = new FixedClock(new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero));
        var decision = DeduplicationDecision.CreateDecisionMaterial(material, fixedClock, policy, sourceResult, target);

        Assert.AreEqual(fixedClock.UtcNow, decision.DecidedAt);
        Assert.AreNotEqual(material.DecidedAt, decision.DecidedAt);
    }

    [TestMethod]
    public void Null_collections_fail_with_deduplication_authority_exception()
    {
        var policy = BuildPolicy(policyId: "policy-review-regression-10", actorRoles: null, useDefaultActorRoles: false);
        var policyError = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy, policyError.Category);

        var verifiedPolicy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(BuildPolicy(policyId: "policy-review-regression-10b"));
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(verifiedPolicy, sourceResult);
        var decisionError = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(
                BuildDecision(
                    verifiedPolicy,
                    sourceResult,
                    target,
                    (IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference>?)null!,
                    decisionId: "decision-review-regression-10"),
                Clock,
                verifiedPolicy,
                sourceResult,
                target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.InvalidDecision, decisionError.Category);
    }

    [TestMethod]
    public void Blank_and_null_authority_collection_entries_use_stable_error_categories()
    {
        var invalidPolicies = new[]
        {
            BuildPolicy("policy-blank-actor", actorRoles: new[] { new DeduplicationAuthorityPolicyActorRole("", "owner") }),
            BuildPolicy("policy-blank-role", actorRoles: new[] { new DeduplicationAuthorityPolicyActorRole("alice", "") }),
            BuildPolicy("policy-null-actor", actorRoles: new DeduplicationAuthorityPolicyActorRole[] { null! }),
            BuildPolicy("policy-null-reason", reasonCodesByAction: new DeduplicationAuthorityPolicyReasonGroup[] { null! })
        };

        foreach (var invalidPolicy in invalidPolicies)
        {
            var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
                DeduplicationAuthorityPolicy.CreatePolicyMaterial(invalidPolicy));
            Assert.IsTrue(
                error.Category is DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy or
                    DeduplicationAuthorityPolicyErrorCodes.UnauthorizedAuthorityActor);
        }

        var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(BuildPolicy("policy-null-decision-entry"));
        var sourceResult = BuildVerifiedResult();
        var target = BuildReviewTarget(policy, sourceResult);
        var valid = BuildDecision(policy, sourceResult, target, BuildEvidenceReferences(target), "decision-null-entry");

        var nullEvidence = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(
                valid with { EvidenceReferences = new DeduplicationAuthorityDecisionEvidenceReference[] { null! } },
                Clock,
                policy,
                sourceResult,
                target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.InvalidDecision, nullEvidence.Category);

        var nullInvalidation = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationDecision.CreateDecisionMaterial(
                valid with { InvalidationEffects = new DeduplicationAuthorityDecisionInvalidationEffect[] { null! } },
                Clock,
                policy,
                sourceResult,
                target));
        Assert.AreEqual(DeduplicationDecisionErrorCodes.InvalidDecision, nullInvalidation.Category);
    }

    [TestMethod]
    public void Malformed_policy_reason_groups_use_policy_error_category()
    {
        var mismatched = BuildPolicy(
            "policy-mismatched-groups",
            allowedActions: new[] { DeduplicationAuthorityPolicyConstants.MergeAction },
            reasonCodesByAction: new[]
            {
                new DeduplicationAuthorityPolicyReasonGroup(
                    DeduplicationAuthorityPolicyConstants.KeepSeparateAction,
                    new[] { "disputed" })
            });
        var duplicateReasons = BuildPolicy(
            "policy-duplicate-reasons",
            allowedActions: new[] { DeduplicationAuthorityPolicyConstants.MergeAction },
            reasonCodesByAction: new[]
            {
                new DeduplicationAuthorityPolicyReasonGroup(
                    DeduplicationAuthorityPolicyConstants.MergeAction,
                    new[] { "duplicate", "duplicate" })
            });

        foreach (var malformed in new[] { mismatched, duplicateReasons })
        {
            var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
                DeduplicationAuthorityPolicy.CreatePolicyMaterial(malformed));
            Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy, error.Category);
        }
    }

    private static UnverifiedDeduplicationAuthorityPolicy BuildPolicy(
        string policyId,
        bool requiresRationale = true,
        string? schemaId = null,
        string? schemaVersion = null,
        string? authoritySourceKind = null,
        IReadOnlyList<DeduplicationAuthorityPolicyActorRole>? actorRoles = null,
        IReadOnlyList<string>? allowedActions = null,
        IReadOnlyList<DeduplicationAuthorityPolicyReasonGroup>? reasonCodesByAction = null,
        string issuedByActorId = "alice",
        string issuedByRole = "owner",
        string? supersedesPolicyId = null,
        ContentDigest? supersedesPolicyDigest = null,
        bool useDefaultActorRoles = true)
    {
        var actorRolesValue = useDefaultActorRoles && actorRoles is null
            ? new[] { new DeduplicationAuthorityPolicyActorRole("alice", "owner") }
            : actorRoles;
        return new UnverifiedDeduplicationAuthorityPolicy(
            SchemaId: schemaId ?? DeduplicationAuthorityPolicyConstants.SchemaId,
            SchemaVersion: schemaVersion ?? DeduplicationAuthorityPolicyConstants.SchemaVersion,
            AuthoritySourceKind: authoritySourceKind ?? DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
            PolicyId: policyId,
            PolicyVersion: "1.0.0",
            AuthorizedActorRoles: actorRolesValue!,
            AllowedActions: allowedActions ?? DeduplicationAuthorityPolicyConstants.ClosedActions.ToArray(),
            ReasonCodesByAction: reasonCodesByAction ?? new[]
            {
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, new[] { "duplicate" }),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, new[] { "disputed" }),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, new[] { "uncertain" })
            },
            RequiresRationale: requiresRationale,
            IssuedByActorId: issuedByActorId,
            IssuedByRole: issuedByRole,
            IssuedAt: new DateTimeOffset(2026, 7, 1, 11, 0, 0, TimeSpan.Zero),
            SupersedesPolicyId: supersedesPolicyId,
            SupersedesPolicyDigest: supersedesPolicyDigest,
            PolicyDigest: null);
    }

    private static UnverifiedDeduplicationAuthorityDecision BuildDecision(
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityReviewTargetDigest target,
        IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> evidenceReferences,
        string decisionId = "decision-review-regression",
        string actionType = DeduplicationAuthorityPolicyConstants.MergeAction,
        string? actorId = null,
        string? actorRole = null,
        string reasonCode = "duplicate",
        string? sourceSnapshotId = null,
        ContentDigest? sourceSnapshotRecordDigest = null,
        string? supersedesDecisionId = null,
        DateTimeOffset? decidedAt = null,
        string invalidationDecisionKind = DeduplicationDecisionConstants.InvalidationDecisionKind,
        string invalidationSnapshotKind = DeduplicationDecisionConstants.InvalidationSnapshotKind,
        bool includeRationale = true)
    {
        ContentDigest? sourceSnapshotRecordDigestToUse = string.IsNullOrWhiteSpace(sourceSnapshotId)
            ? null
            : sourceSnapshotRecordDigest ?? sourceResult.ResultDigest;
        var digestPair = DeduplicationAuthorityDigests.CreateEvidenceDigestMaterial(sourceResult.Result.Evidence[0]);
        return new UnverifiedDeduplicationAuthorityDecision(
            SchemaId: DeduplicationDecisionConstants.SchemaId,
            SchemaVersion: DeduplicationDecisionConstants.SchemaVersion,
            DecisionId: decisionId,
            ActionType: actionType,
            PolicyId: policy.PolicyId,
            PolicyVersion: policy.PolicyVersion,
            TargetKind: target.TargetKind,
            TargetId: target.TargetId,
            TargetContentDigest: target.TargetDigest,
            SourceResultId: sourceResult.Result.ResultId,
            SourceResultDigest: sourceResult.ResultDigest,
            SourceSnapshotId: sourceSnapshotId,
            SourceSnapshotRecordDigest: sourceSnapshotRecordDigestToUse,
            EvidenceReferences: evidenceReferences,
            ActorId: actorId ?? policy.AuthorizedActorRoles[0].ActorId,
            ActorRole: actorRole ?? policy.AuthorizedActorRoles[0].Role,
            AuthoritySourceId: policy.PolicyId,
            AuthoritySourceKind: DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
            AuthoritySourceDigest: policy.PolicyDigest,
            Rationale: includeRationale && policy.RequiresRationale ? "Reviewed by a human decision" : null,
            ReasonCode: reasonCode,
            DecidedAt: decidedAt ?? new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero),
            SupersedesDecisionId: supersedesDecisionId,
            InvalidationEffects: new[]
            {
                new DeduplicationAuthorityDecisionInvalidationEffect(
                    invalidationDecisionKind,
                    $"{invalidationDecisionKind}-target",
                    digestPair.EvidenceDigest),
                new DeduplicationAuthorityDecisionInvalidationEffect(
                    invalidationSnapshotKind,
                    $"{DeduplicationDecisionConstants.InvalidationSnapshotKind}-target",
                    target.TargetDigest)
            },
            DecisionDigest: null);
    }

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

    private static VerifiedDeduplicationAuthorityResultDigest BuildVerifiedResult()
    {
        var policyId = DeduplicationService.PolicyId;
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

    private static IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> BuildEvidenceReferences(
        VerifiedDeduplicationAuthorityReviewTargetDigest target) =>
        target.Evidence.Select((evidence, index) => BuildEvidenceReference(evidence, index)).ToArray();

    private static DeduplicationAuthorityDecisionEvidenceReference BuildEvidenceReference(DedupEvidence evidence, int sequence)
    {
        _ = sequence;
        return new DeduplicationAuthorityDecisionEvidenceReference(
            evidence.Kind.ToString(),
            evidence.EvidenceId,
            DigestScope.CanonicalJsonRecord.ToString(),
            DeduplicationAuthorityDigests.CreateEvidenceDigestMaterial(evidence).EvidenceDigest);
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
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private static readonly IClock Clock = new FixedClock(new DateTimeOffset(2026, 7, 2, 9, 0, 0, TimeSpan.Zero));
}
