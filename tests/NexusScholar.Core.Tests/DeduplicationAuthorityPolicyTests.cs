using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class DeduplicationAuthorityPolicyTests
{
    [TestMethod]
    public void Create_policy_material_normalizes_actor_and_reason_order()
    {
        var unordered = BuildPolicy(
            actorRoles: new[]
            {
                new DeduplicationAuthorityPolicyActorRole("reviewer-bob", "lead"),
                new DeduplicationAuthorityPolicyActorRole("alice", "owner")
            },
            policyId: "policy-a");

        var ordered = BuildPolicy(
            actorRoles: new[]
            {
                new DeduplicationAuthorityPolicyActorRole("alice", "owner"),
                new DeduplicationAuthorityPolicyActorRole("reviewer-bob", "lead")
            },
            reasonCodesByAction: new[]
            {
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, new[] { "duplicate" }),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, new[] { "disputed" }),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, new[] { "uncertain" })
            },
            policyId: "policy-a");

        var first = DeduplicationAuthorityPolicy.CreatePolicyMaterial(unordered);
        var second = DeduplicationAuthorityPolicy.CreatePolicyMaterial(ordered);

        Assert.AreEqual(first.PolicyDigest, second.PolicyDigest);
        CollectionAssert.AreEqual(new[] { "alice", "reviewer-bob" }, first.AuthorizedActorRoles.Select(item => item.ActorId).ToArray());
        CollectionAssert.AreEqual(
            DeduplicationAuthorityPolicyConstants.ClosedActions.ToArray(),
            first.AllowedActions.ToArray());
        Assert.AreEqual("duplicate", first.ReasonCodesForAction(DeduplicationAuthorityPolicyConstants.MergeAction).Single());
    }

    [TestMethod]
    public void Rehydrate_policy_material_rejects_non_canonical_collection_order()
    {
        var basePolicy = BuildPolicy(
            actorRoles: new[]
            {
                new DeduplicationAuthorityPolicyActorRole("alice", "owner"),
                new DeduplicationAuthorityPolicyActorRole("reviewer-bob", "lead")
            },
            policyId: "policy-b");

        var created = DeduplicationAuthorityPolicy.CreatePolicyMaterial(basePolicy);

        var nonCanonical = basePolicy with
        {
            AuthorizedActorRoles = new[]
            {
                basePolicy.AuthorizedActorRoles[1],
                basePolicy.AuthorizedActorRoles[0]
            }
        };
        var nonCanonicalWithDigest = nonCanonical with
        {
            PolicyDigest = created.PolicyDigest
        };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.RehydratePolicyMaterial(nonCanonicalWithDigest));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.NonCanonicalAuthorityMaterial, error.Category);
    }

    [TestMethod]
    public void Rehydrate_policy_material_rejects_tampered_digest()
    {
        var policy = BuildPolicy(policyId: "policy-c");
        var created = DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy);

        var tampered = policy with
        {
            PolicyDigest = ContentDigest.Sha256Utf8("policy digest tamper")
        };

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.RehydratePolicyMaterial(tampered));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.InvalidAuthorityPolicy, error.Category);
    }

    [TestMethod]
    public void Create_policy_material_rejects_unauthorized_issuer()
    {
        var policy = BuildPolicy(
            actorRoles: new[]
            {
                new DeduplicationAuthorityPolicyActorRole("alice", "owner")
            },
            issuedByActorId: "reviewer-bob",
            policyId: "policy-d");

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.CreatePolicyMaterial(policy));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.UnauthorizedAuthorityActor, error.Category);
    }

    [TestMethod]
    public void Create_policy_material_rejects_non_canonical_or_unsupported_action_layout()
    {
        var reversedActions = BuildPolicy(
            allowedActions: new[]
            {
                DeduplicationAuthorityPolicyConstants.KeepSeparateAction,
                DeduplicationAuthorityPolicyConstants.MergeAction,
                DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction
            },
            policyId: "policy-e");

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.CreatePolicyMaterial(reversedActions));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.UnsupportedAuthorityAction, error.Category);
    }

    [TestMethod]
    public void Create_policy_material_rejects_duplicate_actor_role_pairs()
    {
        var duplicateActors = BuildPolicy(
            actorRoles: new[]
            {
                new DeduplicationAuthorityPolicyActorRole("alice", "owner"),
                new DeduplicationAuthorityPolicyActorRole("alice", "owner")
            },
            policyId: "policy-f");

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationAuthorityPolicy.CreatePolicyMaterial(duplicateActors));
        Assert.AreEqual(DeduplicationAuthorityPolicyErrorCodes.DuplicateAuthorityMaterial, error.Category);
    }

    private static UnverifiedDeduplicationAuthorityPolicy BuildPolicy(
        string policyId,
        bool requiresRationale = true,
        IReadOnlyList<DeduplicationAuthorityPolicyActorRole>? actorRoles = null,
        IReadOnlyList<string>? allowedActions = null,
        IReadOnlyList<DeduplicationAuthorityPolicyReasonGroup>? reasonCodesByAction = null,
        string issuedByActorId = "alice",
        string issuedByRole = "owner")
    {
        return new UnverifiedDeduplicationAuthorityPolicy(
            SchemaId: DeduplicationAuthorityPolicyConstants.SchemaId,
            SchemaVersion: DeduplicationAuthorityPolicyConstants.SchemaVersion,
            AuthoritySourceKind: DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
            PolicyId: policyId,
            PolicyVersion: "1.0.0",
            AuthorizedActorRoles: actorRoles ?? new[]
            {
                new DeduplicationAuthorityPolicyActorRole("alice", "owner"),
                new DeduplicationAuthorityPolicyActorRole("reviewer-bob", "lead")
            },
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
            IssuedAt: new DateTimeOffset(2026, 7, 1, 12, 30, 0, TimeSpan.Zero),
            SupersedesPolicyId: null,
            SupersedesPolicyDigest: null,
            PolicyDigest: null);
    }
}
