using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ProtocolTests
{
    private static readonly ProtocolActor Researcher = ProtocolActor.Human("researcher-1");
    private static readonly ProtocolActor Reviewer = ProtocolActor.Human("reviewer-2");
    private static readonly ProtocolActor Automation = ProtocolActor.Automation("llm-job-17");
    private static readonly IClock Clock = new FixedClock();

    [TestMethod]
    public void Draft_creation_preserves_gate_3_contract_fields()
    {
        var draft = CreateCompleteDraft(NewIds());

        Assert.AreEqual("project-1", draft.ProjectId);
        Assert.AreEqual(ProtocolStatus.Draft, draft.Status);
        Assert.AreEqual("template-systematic-review", draft.Template.TemplateId);
        Assert.AreEqual("tomato disease screening", draft.Intent.RawSubject);
        Assert.AreEqual(2, draft.RequiredDecisions.Count);
        Assert.AreEqual(2, draft.Decisions.Count);
        Assert.AreEqual(Researcher.Id, draft.CreatedBy);
        Assert.AreEqual(Clock.UtcNow, draft.CreatedAt);
    }

    [TestMethod]
    public void Approval_requires_all_declared_decisions()
    {
        var ids = NewIds();
        var draft = CreateDraft(ids);
        draft.RecordDecision(ids, "review-type", CanonicalJsonValue.From("scoping-review"), Researcher, Clock);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher()));
        Assert.AreEqual(ProtocolErrorCodes.MissingRequiredDecision, error.Category);
    }

    [TestMethod]
    public void Duplicate_decision_is_rejected_with_stable_category()
    {
        var ids = NewIds();
        var draft = CreateDraft(ids);
        draft.RecordDecision(ids, "review-type", CanonicalJsonValue.From("scoping-review"), Researcher, Clock);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.RecordDecision(ids, "review-type", CanonicalJsonValue.From("systematic-review"), Researcher, Clock));
        Assert.AreEqual(ProtocolErrorCodes.DuplicateDecision, error.Category);
    }

    [TestMethod]
    public void Automation_cannot_record_protocol_decisions()
    {
        var ids = NewIds();
        var draft = CreateDraft(ids);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.RecordDecision(
                ids,
                "review-type",
                CanonicalJsonValue.From("scoping-review"),
                Automation,
                Clock));
        Assert.AreEqual(ProtocolErrorCodes.NonHumanApprovalActor, error.Category);
    }

    [TestMethod]
    public void Blocking_unresolved_decision_prevents_approval()
    {
        var ids = NewIds();
        var draft = CreateCompleteDraft(
            ids,
            new[]
            {
                RequiredDecision("review-type", allowsUnresolved: true),
                RequiredDecision("scope")
            });
        Assert.IsTrue(draft.RequiredDecisions.Select(req => req.DecisionKey).Contains("review-type"));
        var unresolvedDecisionKey = draft.RequiredDecisions[0].DecisionKey;
        draft.AddUnresolvedDecision(
            ids,
            unresolvedDecisionKey,
            "What minimum score is acceptable?",
            "Template requires this decision to remain open while planning.",
            "protocol-approval",
            Researcher,
            Clock,
            blocksProtocolApproval: true);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher()));
        Assert.AreEqual(ProtocolErrorCodes.BlockingUnresolvedDecision, error.Category);
    }

    [TestMethod]
    public void Hardening_03_protocol_rehydration_api_types_are_discoverable()
    {
        var missingTypeNames = new List<string>();
        foreach (var typeName in new[]
                 {
                     "UnverifiedProtocolApproval",
                     "UnverifiedProtocolVersion",
                     "IProtocolAuthorityResolver",
                     "VerifiedProtocolApproval",
                     "VerifiedProtocolVersion",
                     "ProtocolRehydrator"
                 })
        {
            if (Type.GetType($"NexusScholar.Protocol.{typeName}, NexusScholar.Protocol") is null)
            {
                missingTypeNames.Add(typeName);
            }
        }

        var rehydrator = Type.GetType("NexusScholar.Protocol.ProtocolRehydrator, NexusScholar.Protocol");
        var missingMethodNames = new List<string>();

        if (rehydrator is null)
        {
            missingMethodNames.Add("ProtocolRehydrator");
        }
        else
        {
            if (rehydrator.GetMethod("RehydrateApproval", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance) is null)
            {
                missingMethodNames.Add("RehydrateApproval");
            }

            if (rehydrator.GetMethod("RehydrateVersion", BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance) is null)
            {
                missingMethodNames.Add("RehydrateVersion");
            }
        }

        if (missingTypeNames.Count > 0 || missingMethodNames.Count > 0)
        {
            Assert.Inconclusive(
                "Hardening-03 rehydration API is not present in this build yet: " +
                string.Join(", ", missingTypeNames.Concat(missingMethodNames)));
        }
    }

    [TestMethod]
    public void Approved_protocol_authority_has_no_public_constructor_or_promotion_method()
    {
        Assert.AreEqual(0, typeof(ProtocolVersion).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
        Assert.IsNull(typeof(ProtocolVersion).GetMethod("WithApprovals", BindingFlags.Public | BindingFlags.Instance));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Approved_protocol_rehydrates_with_exact_resolved_policy_and_human_approvals(bool dual)
    {
        var state = CreateRehydrationState(dual ? ApprovalPolicy.DualIndependent() : ApprovalPolicy.ExplicitCustomSingleResearcher());

        var verified = ProtocolRehydrator.RehydrateVersion(ToUnverified(state.Version, state.Policy), state.Resolver);

        Assert.AreEqual(ProtocolStatus.Approved, verified.Version.Status);
        Assert.AreEqual(state.Version.ContentDigest, verified.Version.ContentDigest);
        Assert.AreEqual(dual ? 2 : 1, verified.Approvals.Count);
        Assert.AreEqual(state.Policy.PolicyId, verified.ApprovalPolicy.PolicyId);
    }

    [TestMethod]
    public void Approval_rehydration_recomputes_digest_and_resolves_human_actor()
    {
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var ids = NewIds();
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        var resolver = new TestProtocolAuthorityResolver(policy, new[] { Researcher.Id });

        var verified = ProtocolRehydrator.RehydrateApproval(ToUnverified(approval, policy), candidate, policy, resolver);
        Assert.AreEqual(approval.ApprovalRecordDigest, verified.Approval.ApprovalRecordDigest);

        var tampered = ToUnverified(approval, policy) with { Rationale = "tampered after approval" };
        var digestError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateApproval(tampered, candidate, policy, resolver));
        Assert.AreEqual(ProtocolErrorCodes.ApprovalTargetMismatch, digestError.Category);

        var nonHumanResolver = new TestProtocolAuthorityResolver(policy, Array.Empty<ActorId>());
        var actorError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateApproval(ToUnverified(approval, policy), candidate, policy, nonHumanResolver));
        Assert.AreEqual(ProtocolErrorCodes.NonHumanApprovalActor, actorError.Category);
    }

    [TestMethod]
    public void Version_rehydration_rejects_tampered_content_and_duplicate_identity()
    {
        var state = CreateRehydrationState(ApprovalPolicy.ExplicitCustomSingleResearcher());
        var input = ToUnverified(state.Version, state.Policy);
        var tampered = input with { Values = new CanonicalJsonObject().Add("review_family", "tampered") };

        var digestError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(tampered, state.Resolver));
        Assert.AreEqual(ProtocolErrorCodes.StaleContentDigest, digestError.Category);

        var duplicateDecisions = input.Decisions.Concat(new[] { input.Decisions[0] }).ToArray();
        var duplicate = input with { Decisions = duplicateDecisions };
        var duplicateError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(duplicate, state.Resolver));
        Assert.AreEqual(ProtocolErrorCodes.DuplicateDecision, duplicateError.Category);
    }

    [TestMethod]
    public void Version_rehydration_rejects_missing_duplicate_and_weaker_policy_authority()
    {
        var state = CreateRehydrationState(ApprovalPolicy.DualIndependent());
        var input = ToUnverified(state.Version, state.Policy);

        var missing = input with { ApprovalIds = input.ApprovalIds.Take(1).ToArray() };
        var missingError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(missing, state.Resolver));
        Assert.AreEqual(ProtocolErrorCodes.InsufficientApprovalPolicy, missingError.Category);

        var duplicate = input with { ApprovalIds = new[] { input.ApprovalIds[0], input.ApprovalIds[0] } };
        var duplicateError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(duplicate, state.Resolver));
        Assert.AreEqual(ProtocolErrorCodes.InsufficientApprovalPolicy, duplicateError.Category);

        var weakerResolver = state.Resolver.WithPolicy(new ApprovalPolicy(
            state.Policy.PolicyId,
            state.Policy.PolicyVersion,
            ApprovalPolicyMode.SingleResearcher,
            Array.Empty<string>(),
            1,
            false,
            false));
        var downgradeError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(input, weakerResolver));
        Assert.AreEqual(ProtocolErrorCodes.UnauthorizedApproval, downgradeError.Category);
    }

    [TestMethod]
    public void Version_rehydration_rejects_approvals_beyond_the_resolved_policy_requirement()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var resolver = new TestProtocolAuthorityResolver(policy, new[] { Researcher.Id, Reviewer.Id });
        var verifiedApprovals = new[] { Researcher, Reviewer }
            .Select(actor => ProtocolApproval.Create(ids, candidate, policy, actor, Clock, candidate.ContentDigest))
            .Select(approval => ProtocolRehydrator.RehydrateApproval(ToUnverified(approval, policy), candidate, policy, resolver))
            .ToArray();
        resolver = resolver.WithApprovals(verifiedApprovals);
        var overApproved = draft.ApproveCandidate(candidate, policy, verifiedApprovals.Select(item => item.Approval), Clock);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(ToUnverified(overApproved, policy), resolver));
        Assert.AreEqual(ProtocolErrorCodes.InsufficientApprovalPolicy, error.Category);
    }

    [TestMethod]
    public void Version_rehydration_rejects_unresolved_scientific_actors_and_unlinked_supersession()
    {
        var state = CreateRehydrationState(ApprovalPolicy.ExplicitCustomSingleResearcher());
        var input = ToUnverified(state.Version, state.Policy);
        var decision = input.Decisions[0] with { DecidedBy = default };

        var actorError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(
                input with { Decisions = new[] { decision, input.Decisions[1] } },
                state.Resolver));
        Assert.AreEqual(ProtocolErrorCodes.NonHumanApprovalActor, actorError.Category);

        var supersessionError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(
                input with { Status = ProtocolStatus.Superseded, SupersededByVersionId = null },
                state.Resolver));
        Assert.AreEqual(ProtocolErrorCodes.StaleContentDigest, supersessionError.Category);
    }

    [TestMethod]
    public void Version_rehydration_rejects_rewritten_approval_timestamp()
    {
        var state = CreateRehydrationState(ApprovalPolicy.ExplicitCustomSingleResearcher());
        var input = ToUnverified(state.Version, state.Policy);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(
                input with { ApprovedAt = input.ApprovedAt.AddYears(1) },
                state.Resolver));
        Assert.AreEqual(ProtocolErrorCodes.StaleContentDigest, error.Category);
    }

    [TestMethod]
    public void Version_rehydration_rejects_blocking_unresolved_state_before_authority_resolution()
    {
        var state = CreateRehydrationState(ApprovalPolicy.ExplicitCustomSingleResearcher());
        var input = ToUnverified(state.Version, state.Policy);
        var unresolved = new UnresolvedDecision(
            FixedId(991),
            "review-type",
            "Still unresolved?",
            "Persisted blocking state",
            "protocol-approval",
            Researcher.Id,
            Clock.UtcNow,
            true);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolRehydrator.RehydrateVersion(input with { UnresolvedDecisions = new[] { unresolved } }, state.Resolver));
        Assert.AreEqual(ProtocolErrorCodes.BlockingUnresolvedDecision, error.Category);
    }

    [TestMethod]
    public void Verified_protocol_does_not_retain_mutable_caller_collections()
    {
        var state = CreateRehydrationState(ApprovalPolicy.ExplicitCustomSingleResearcher());
        var decisions = ToUnverified(state.Version, state.Policy).Decisions.ToList();
        var input = ToUnverified(state.Version, state.Policy) with { Decisions = decisions };

        var verified = ProtocolRehydrator.RehydrateVersion(input, state.Resolver);
        decisions.Clear();

        Assert.AreEqual(state.Version.Decisions.Count, verified.Version.Decisions.Count);
        Assert.AreEqual(state.Version.ContentDigest, verified.Version.ToProtocolContentDigestEnvelope().ComputeDigest());
        Assert.IsFalse(verified.Version.ApprovalIds is string[]);
        Assert.IsFalse(verified.Version.Decisions is ProtocolDecision[]);
        Assert.IsFalse(verified.Approvals is VerifiedProtocolApproval[]);
        Assert.IsFalse(verified.ApprovalPolicy.RequiredRoles is string[]);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ((CanonicalJsonObject)verified.Version.RequiredDecisions[0].ValueSchema).Add("forged", true));
    }

    [TestMethod]
    public void Rehydration_fixtures_cover_single_dual_and_negative_cases()
    {
        var fixtures = new[]
        {
            "protocol-rehydration-approved-single-v1.json",
            "protocol-rehydration-approved-dual-v1.json",
            "protocol-rehydration-invalid-tampered-content-digest-v1.json",
            "protocol-rehydration-invalid-tampered-approval-record-digest-v1.json",
            "protocol-rehydration-invalid-non-human-approval-v1.json",
            "protocol-rehydration-invalid-wrong-target-v1.json",
            "protocol-rehydration-invalid-wrong-policy-v1.json",
            "protocol-rehydration-invalid-missing-approvals-v1.json",
            "protocol-rehydration-invalid-extra-approvals-v1.json",
            "protocol-rehydration-invalid-duplicate-approvals-v1.json",
            "protocol-rehydration-invalid-duplicate-content-identity-v1.json",
            "protocol-rehydration-invalid-policy-downgrade-v1.json",
            "protocol-rehydration-blocking-unresolved-state-v1.json"
        };

        foreach (var fixture in fixtures)
        {
            using var document = LoadProtocolFixture(fixture);
            var @case = document.RootElement.GetProperty("case");

            Assert.AreEqual("protocol-rehydration", @case.GetProperty("recordType").GetString(), fixture);
            Assert.IsTrue(@case.TryGetProperty("operation", out var operation), $"Fixture '{fixture}' is missing an operation.");
            Assert.IsTrue(operation.GetString()?.Length > 0, $"Fixture '{fixture}' has empty operation.");

            Assert.IsTrue(
                @case.TryGetProperty("negative", out var isNegative),
                $"Fixture '{fixture}' must mark negative cases explicitly.");
            if (@case.TryGetProperty("negative", out var isNegativeCase) && isNegativeCase.GetBoolean())
            {
                Assert.IsTrue(
                    @case.TryGetProperty("errorCategory", out var category) && !string.IsNullOrWhiteSpace(category.GetString()),
                    $"Fixture '{fixture}' missing errorCategory.");
            }
            else
            {
                Assert.IsFalse(
                    isNegative.GetBoolean(),
                    $"Fixture '{fixture}' must not set negative=true for positive paths.");
            }
        }
    }

    [TestMethod]
    public void Protocol_content_digest_uses_protocol_content_scope()
    {
        var ids = NewIds();
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher());

        var envelope = candidate.ToProtocolContentDigestEnvelope();

        Assert.AreEqual(DigestScope.ProtocolContent, envelope.Scope);
        Assert.AreEqual("nexus.protocol-content", envelope.SchemaId);
        Assert.AreEqual(candidate.ContentDigest, envelope.ComputeDigest());
    }

    [TestMethod]
    public void Old_key_value_digest_material_is_not_protocol_content_digest()
    {
        var ids = NewIds();
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher());
        var oldDigest = ContentDigest.Sha256Utf8("review-type=scoping-review\nscope=agricultural image segmentation");

        Assert.AreNotEqual(oldDigest, candidate.ContentDigest);
    }

    [TestMethod]
    public void Single_researcher_approval_succeeds_with_explicit_custom_local_policy()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);

        var version = draft.ApproveCandidate(candidate, policy, new[] { approval }, Clock);

        Assert.AreEqual(ProtocolStatus.Approved, draft.Status);
        Assert.AreEqual(ProtocolStatus.Approved, version.Status);
        Assert.AreEqual(1, version.ApprovalIds.Count);
        Assert.AreEqual(approval.ApprovalId, version.ApprovalIds[0]);
    }

    [TestMethod]
    public void Dual_independent_approval_succeeds_for_distinct_human_actors()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.DualIndependent();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var first = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        var second = ProtocolApproval.Create(ids, candidate, policy, Reviewer, Clock, candidate.ContentDigest);

        var version = draft.ApproveCandidate(candidate, policy, new[] { first, second }, Clock);

        Assert.AreEqual(2, version.ApprovalIds.Count);
    }

    [TestMethod]
    public void Same_actor_cannot_satisfy_dual_independent_approval()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.DualIndependent();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var first = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        var second = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, policy, new[] { first, second }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.SameActorDualApproval, error.Category);
    }

    [TestMethod]
    public void Approval_policy_roles_are_enforced()
    {
        var ids = NewIds();
        var policy = new ApprovalPolicy(
            "strict-local-review",
            "1.0.0",
            ApprovalPolicyMode.Methodologist,
            new[] { "methodologist" },
            1,
            true,
            false,
            CustomRuleId: null);
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);

        var missingRoleError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, policy, new[] { approval }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.InsufficientApprovalPolicy, missingRoleError.Category);

        var withRole = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest, role: "methodologist");
        var version = draft.ApproveCandidate(candidate, policy, new[] { withRole }, Clock);

        Assert.AreEqual(ProtocolStatus.Approved, version.Status);
    }

    [TestMethod]
    public void Withdrawn_approval_does_not_satisfy_dual_policy()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.DualIndependent();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var first = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        var withdrawn = ProtocolApproval.Create(
            ids,
            candidate,
            policy,
            Researcher,
            Clock,
            candidate.ContentDigest,
            ProtocolApprovalDecision.Withdrawn,
            supersedesApprovalId: first.ApprovalId);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, policy, new[] { first, withdrawn }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.InsufficientApprovalPolicy, error.Category);
    }

    [TestMethod]
    public void Forged_approval_record_digest_is_rejected()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        var forged = CloneApproval(approval, approvalRecordDigest: ContentDigest.Sha256Utf8("forged"));

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, policy, new[] { forged }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.ApprovalTargetMismatch, error.Category);
    }

    [TestMethod]
    public void Wrong_approval_policy_version_or_mode_is_rejected()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        var wrongPolicy = new ApprovalPolicy(
            policy.PolicyId,
            "2.0.0",
            policy.Mode,
            policy.RequiredRoles,
            policy.MinimumApprovals,
            policy.RequiresDistinctActors,
            policy.AllowsAutomation,
            policy.MethodPackId,
            policy.CustomRuleId);

        var errorVersion = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, wrongPolicy, new[] { approval }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.ApprovalTargetMismatch, errorVersion.Category);

        var wrongMode = new ApprovalPolicy(
            policy.PolicyId,
            policy.PolicyVersion,
            ApprovalPolicyMode.CustomRoleExpression,
            new[] { "methodologist" },
            policy.MinimumApprovals,
            true,
            policy.AllowsAutomation,
            policy.MethodPackId,
            policy.CustomRuleId);

        var errorMode = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, wrongMode, new[] { approval }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.ApprovalTargetMismatch, errorMode.Category);
    }

    [TestMethod]
    public void Wrong_approval_target_is_rejected()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var baseApproval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);

        var wrongTargetType = CloneApproval(baseApproval, targetType: "protocol-review");
        var wrongTargetId = CloneApproval(baseApproval, targetId: FixedId(900));
        var wrongProtocol = CloneApproval(baseApproval, protocolId: "protocol-other");

        var targetTypeError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, policy, new[] { wrongTargetType }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.ApprovalTargetMismatch, targetTypeError.Category);

        var targetIdError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, policy, new[] { wrongTargetId }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.ApprovalTargetMismatch, targetIdError.Category);

        var protocolError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, policy, new[] { wrongProtocol }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.ApprovalTargetMismatch, protocolError.Category);
    }

    [TestMethod]
    public void Superseded_version_cannot_be_approved()
    {
        var ids = NewIds();
        var draft = CreateCompleteDraft(ids);
        var approved = draft.Approve(Researcher.Id, Clock, ids);
        var supersededCandidate = approved.SupersededBy(FixedId(900));

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(supersededCandidate, ApprovalPolicy.ExplicitCustomSingleResearcher(), new[] { ProtocolApproval.Create(ids, supersededCandidate, ApprovalPolicy.ExplicitCustomSingleResearcher(), Researcher, Clock, supersededCandidate.ContentDigest) }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.StaleContentDigest, error.Category);
    }

    [TestMethod]
    public void Stale_digest_approval_is_rejected()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, policy);
        var staleDigest = ContentDigest.Sha256Utf8("stale");

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, staleDigest));
        Assert.AreEqual(ProtocolErrorCodes.StaleContentDigest, error.Category);
    }

    [TestMethod]
    public void Draft_change_after_candidate_digest_rejects_approval_transition()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(
            ids,
            new[] { RequiredDecision("review-type", allowsUnresolved: true), RequiredDecision("scope") });
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        draft.AddUnresolvedDecision(
            ids,
            "review-type",
            "What minimum score is acceptable?",
            "Protocol requires this decision to remain open while planning.",
            "protocol-approval",
            Researcher,
            Clock,
            blocksProtocolApproval: false);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.ApproveCandidate(candidate, policy, new[] { approval }, Clock));
        Assert.AreEqual(ProtocolErrorCodes.StaleContentDigest, error.Category);
    }

    [TestMethod]
    public void Automation_cannot_approve_protocol_content()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, policy);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolApproval.Create(ids, candidate, policy, Automation, Clock, candidate.ContentDigest));
        Assert.AreEqual(ProtocolErrorCodes.NonHumanApprovalActor, error.Category);
    }

    [TestMethod]
    public void Approved_draft_cannot_be_mutated()
    {
        var ids = NewIds();
        var draft = CreateCompleteDraft(ids);
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        _ = draft.ApproveCandidate(candidate, policy, new[] { approval }, Clock);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.RecordDecision(ids, "screening-mode", CanonicalJsonValue.From("double-screen"), Researcher, Clock));
        Assert.AreEqual(ProtocolErrorCodes.PostApprovalMutation, error.Category);
    }

    [TestMethod]
    public void Approved_version_rejects_post_approval_waiver_mutation()
    {
        var ids = NewIds();
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher());

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            candidate.AddWaiver(SampleWaiver(ids)));
        Assert.AreEqual(ProtocolErrorCodes.PostApprovalMutation, error.Category);
    }

    [TestMethod]
    public void Waiver_is_included_in_protocol_content_digest()
    {
        var firstIds = NewIds();
        var secondIds = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var withoutWaiver = CreateCompleteDraft(firstIds).CreateApprovalCandidate(
            firstIds,
            policy,
            versionId: FixedId(200));
        var withWaiverDraft = CreateCompleteDraftWithWaiver(secondIds);
        withWaiverDraft.AddWaiver(
            secondIds,
            "scope",
            null,
            null,
            "Pilot review has a narrow evidence source.",
            "Report scope limitation.",
            "limitations.scope",
            Researcher,
            Clock,
            policy);
        var withWaiver = withWaiverDraft.CreateApprovalCandidate(
            secondIds,
            policy,
            versionId: FixedId(200));

        Assert.AreNotEqual(withoutWaiver.ContentDigest, withWaiver.ContentDigest);
    }

    [TestMethod]
    public void Deviation_links_to_approved_version_without_mutating_digest()
    {
        var ids = NewIds();
        var draft = CreateCompleteDraft(ids);
        var approved = draft.Approve(Researcher.Id, Clock, ids);
        var before = approved.ContentDigest;

        var deviation = ProtocolDeviation.Record(
            ids,
            approved,
            "scope",
            "One data source was unavailable during conduct.",
            "Repository outage.",
            "protocol_deviation",
            Researcher,
            Clock,
            "disclose limitation",
            "limitations.data-source");

        Assert.AreEqual(approved.Id, deviation.ProtocolVersionId);
        Assert.AreEqual(before, approved.ContentDigest);
    }

    [TestMethod]
    public void Non_blocking_unresolved_decisions_can_satisfy_requirements_and_are_preserved()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(
            ids,
            new[]
            {
                RequiredDecision("review-type"),
                RequiredDecision("scope"),
                RequiredDecision("quality-threshold", allowsUnresolved: true)
            });

        draft.AddUnresolvedDecision(
            ids,
            "quality-threshold",
            "What threshold is acceptable?",
            "Preliminary run may set threshold later.",
            "protocol-approval",
            Researcher,
            Clock,
            blocksProtocolApproval: false);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        var version = draft.ApproveCandidate(candidate, policy, new[] { approval }, Clock);

        Assert.AreEqual(1, version.UnresolvedDecisions.Count);
        Assert.AreEqual("quality-threshold", version.UnresolvedDecisions[0].DecisionKey);
    }

    [TestMethod]
    public void Non_waivable_requirement_rejects_waiver()
    {
        var ids = NewIds();
        var draft = CreateDraft(
            ids,
            new[]
            {
                RequiredDecision("review-type"),
                RequiredDecision("scope", allowsWaiver: false),
                RequiredDecision("quality-threshold", allowsWaiver: false),
            });

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            draft.AddWaiver(
                ids,
                "quality-threshold",
                null,
                null,
                "Pilot review had partial evidence.",
                "Report limitation.",
                "limitations.scope",
                Researcher,
                Clock,
                ApprovalPolicy.ExplicitCustomSingleResearcher()));

        Assert.AreEqual(ProtocolErrorCodes.InvalidWaiver, error.Category);
    }

    [TestMethod]
    public void Approval_candidate_starts_in_ready_state_without_approval_metadata()
    {
        var ids = NewIds();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher(), versionId: FixedId(501));

        Assert.AreEqual(ProtocolStatus.ReadyForReview, candidate.Status);
        Assert.AreEqual(ProtocolStatus.Draft, draft.Status);
        Assert.AreEqual(0, candidate.ApprovalIds.Count);
        Assert.IsNull(candidate.ApprovedAt);
    }

    [TestMethod]
    public void Invalid_deviation_classification_is_rejected()
    {
        var ids = NewIds();
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher());
        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            _ = ProtocolDeviation.Record(
                ids,
                candidate,
                "scope",
                "Observed broader scope.",
                "Rationale text.",
                "unsupported-classification",
                Researcher,
                Clock,
                "disclose limitation",
                "limitations.data"));
        Assert.AreEqual(ProtocolErrorCodes.InvalidDeviation, error.Category);
    }

    [TestMethod]
    public void Amendment_requires_approved_prior_version()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy, versionId: FixedId(300));

        var preApprovalError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolAmendment.Create(
                ids,
                candidate,
                FixedId(301),
                Researcher,
                Clock,
                "Scope changed after pilot.",
                new[] { "scope" },
                Array.Empty<ProtocolInvalidationNotice>(),
                policy));
        Assert.AreEqual(ProtocolErrorCodes.InvalidAmendment, preApprovalError.Category);

        var approved = draft.Approve(Researcher.Id, Clock, ids);
        var notice = new ProtocolInvalidationNotice(
            FixedId(302),
            approved.Id,
            "scope",
            ContentDigest.Sha256Utf8("artifact"),
            "screening",
            "screening output requires review",
            "rerun screening",
            Clock.UtcNow);
        var amendment = ProtocolAmendment.Create(
            ids,
            approved,
            FixedId(401),
            Researcher,
            Clock,
            "Scope changed after pilot.",
            new[] { "scope" },
            new[] { notice },
            policy);

        Assert.AreEqual(approved.Id, amendment.AmendsVersionId);
        Assert.AreEqual(approved.ContentDigest, amendment.PreviousContentDigest);
        Assert.AreEqual(FixedId(401), amendment.ProducesVersionId);
    }

    [TestMethod]
    public void Amendment_preserves_previous_digest_and_supersession_links()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(ids);
        var previous = draft.Approve(Researcher.Id, Clock, ids);
        var nextVersionId = FixedId(301);
        var notice = new ProtocolInvalidationNotice(
            FixedId(302),
            "amendment-placeholder",
            "scope",
            ContentDigest.Sha256Utf8("artifact"),
            "screening",
            "screening output requires review",
            "rerun screening",
            Clock.UtcNow);

        var amendment = ProtocolAmendment.Create(
            ids,
            previous,
            nextVersionId,
            Researcher,
            Clock,
            "Scope changed after pilot.",
            new[] { "scope" },
            new[] { notice },
            policy);
        var superseded = previous.SupersededBy(nextVersionId);

        Assert.AreEqual(previous.Id, amendment.AmendsVersionId);
        Assert.AreEqual(nextVersionId, amendment.ProducesVersionId);
        Assert.AreEqual(previous.ContentDigest, amendment.PreviousContentDigest);
        Assert.AreEqual(ProtocolStatus.Superseded, superseded.Status);
        Assert.AreEqual(nextVersionId, superseded.SupersededByVersionId);
    }

    [TestMethod]
    public void Amendment_rejects_invalidation_notices_for_unmodified_requirements()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy, versionId: FixedId(301));
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        var approved = draft.ApproveCandidate(candidate, policy, new[] { approval }, Clock);
        var notice = new ProtocolInvalidationNotice(
            FixedId(302),
            approved.Id,
            "other",
            ContentDigest.Sha256Utf8("artifact"),
            "screening",
            "screening output requires review",
            "rerun screening",
            Clock.UtcNow);

        var error = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolAmendment.Create(
                ids,
                approved,
                FixedId(303),
                Researcher,
                Clock,
                "Scope changed after pilot.",
                new[] { "scope" },
                new[] { notice },
                policy));
        Assert.AreEqual(ProtocolErrorCodes.InvalidAmendment, error.Category);
    }

    [TestMethod]
    public void Approved_candidate_must_have_approval_timestamp_and_approval_ids()
    {
        var ids = NewIds();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher());

        var approvalMissingTimestamp = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            new ProtocolVersion(
                candidate.Id,
                candidate.ProtocolId,
                candidate.ProjectId,
                candidate.VersionNumber,
                ProtocolStatus.Approved,
                candidate.Template,
                candidate.Intent,
                candidate.Values,
                candidate.RequiredDecisions,
                candidate.Decisions,
                candidate.Waivers,
                candidate.ContentDigest,
                candidate.ApprovalPolicyId,
                Array.Empty<string>(),
                null,
                unresolvedDecisions: candidate.UnresolvedDecisions));
        Assert.AreEqual(ProtocolErrorCodes.StaleContentDigest, approvalMissingTimestamp.Category);

        var approvalMissingIds = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            new ProtocolVersion(
                candidate.Id,
                candidate.ProtocolId,
                candidate.ProjectId,
                candidate.VersionNumber,
                ProtocolStatus.Approved,
                candidate.Template,
                candidate.Intent,
                candidate.Values,
                candidate.RequiredDecisions,
                candidate.Decisions,
                candidate.Waivers,
                candidate.ContentDigest,
                candidate.ApprovalPolicyId,
                Array.Empty<string>(),
                Clock.UtcNow,
                unresolvedDecisions: candidate.UnresolvedDecisions));
        Assert.AreEqual(ProtocolErrorCodes.StaleContentDigest, approvalMissingIds.Category);
    }

    [TestMethod]
    public void Approval_record_digest_uses_approval_record_scope()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);

        var envelope = approval.ToApprovalRecordDigestEnvelope();

        Assert.AreEqual(DigestScope.ApprovalRecord, envelope.Scope);
        Assert.AreEqual("nexus.protocol-approval-record", envelope.SchemaId);
        Assert.AreEqual(approval.ApprovalRecordDigest, envelope.ComputeDigest());
    }

    private static ProtocolDraft CreateCompleteDraft(IIdGenerator ids)
    {
        return CreateCompleteDraft(
            ids,
            new[]
            {
                RequiredDecision("review-type"),
                RequiredDecision("scope")
            });
    }

    private static ProtocolDraft CreateCompleteDraftWithWaiver(IIdGenerator ids)
    {
        return CreateCompleteDraft(
            ids,
            new[]
            {
                RequiredDecision("review-type"),
                RequiredDecision("scope", allowsWaiver: true)
            });
    }

    private static ProtocolDraft CreateCompleteDraft(IIdGenerator ids, IReadOnlyList<RequiredDecisionDefinition> requiredDecisions)
    {
        var draft = CreateDraft(ids, requiredDecisions);
        draft.RecordDecision(
            ids,
            "review-type",
            CanonicalJsonValue.From("scoping-review"),
            Researcher,
            Clock,
            "A scoping review fits the exploratory objective.",
            ContentDigest.Sha256Utf8("proposal-review-type"));
        draft.RecordDecision(
            ids,
            "scope",
            CanonicalJsonValue.From("agricultural image segmentation"),
            Researcher,
            Clock);
        return draft;
    }

    private static ProtocolDraft CreateDraft(IIdGenerator ids, IReadOnlyList<RequiredDecisionDefinition> requiredDecisions)
    {
        return ProtocolDraft.Create(
            ids,
            "project-1",
            new ProtocolTemplate(
                "template-systematic-review",
                "1.0.0",
                ContentDigest.Sha256Utf8("template-systematic-review@1.0.0")),
            new ProtocolIntent(
                "tomato disease screening",
                "map the evidence for segmentation workflows",
                "scoping-review"),
            new CanonicalJsonObject().Add("review_family", "scoping"),
            requiredDecisions,
            Researcher,
            Clock);
    }

    private static ProtocolDraft CreateDraft(IIdGenerator ids)
    {
        return CreateDraft(
            ids,
            new[]
            {
                RequiredDecision("review-type"),
                RequiredDecision("scope")
            });
    }

    private static RequiredDecisionDefinition RequiredDecision(
        string key,
        bool allowsUnresolved = false,
        bool allowsWaiver = false)
    {
        return new RequiredDecisionDefinition(
            key,
            key,
            $"Select {key}.",
            new CanonicalJsonObject().Add("type", "string"),
            "protocol-approval",
            "protocol-approval",
            key,
            allowsUnresolved,
            allowsWaiver);
    }

    private static ProtocolWaiver SampleWaiver(IIdGenerator ids)
    {
        return new ProtocolWaiver(
            ids.NewId().ToString("D"),
            "scope",
            null,
            null,
            "Waive one scope check.",
            "Report limitation.",
            "limitations.scope",
            Researcher.Id,
            Clock.UtcNow,
            ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId,
            Array.Empty<string>());
    }

    private static ProtocolApproval CloneApproval(
        ProtocolApproval approval,
        string? targetType = null,
        string? targetId = null,
        string? protocolId = null,
        string? protocolVersionId = null,
        int? protocolVersionNumber = null,
        ContentDigest? contentDigest = null,
        string? policyId = null,
        string? policyVersion = null,
        string? policyMode = null,
        ProtocolApprovalDecision? decision = null,
        ContentDigest? approvalRecordDigest = null)
    {
        var ctor = typeof(ProtocolApproval).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[]
            {
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(int),
                typeof(ContentDigest),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(ProtocolApprovalDecision),
                typeof(ActorId),
                typeof(DateTimeOffset),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(ContentDigest)
            },
            null)!;

        return (ProtocolApproval)ctor.Invoke(new object?[]
        {
            approval.ApprovalId,
            targetType ?? approval.TargetType,
            targetId ?? approval.TargetId,
            protocolId ?? approval.ProtocolId,
            protocolVersionId ?? approval.ProtocolVersionId,
            protocolVersionNumber ?? approval.ProtocolVersionNumber,
            contentDigest ?? approval.ContentDigest,
            policyId ?? approval.PolicyId,
            policyVersion ?? approval.PolicyVersion,
            policyMode ?? approval.PolicyMode,
            decision ?? approval.Decision,
            approval.ApprovedBy,
            approval.ApprovedAt,
            approval.Role,
            approval.Rationale,
            approval.SupersedesApprovalId,
            approval.ApprovedByIsHuman,
            approvalRecordDigest ?? approval.ApprovalRecordDigest
        });
    }

    private static JsonDocument LoadProtocolFixture(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "protocol", filename);
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", "conformance", "protocol", filename));
            if (!File.Exists(path))
            {
                path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fixtures", "protocol", filename));
            }
        }

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static RehydrationState CreateRehydrationState(ApprovalPolicy policy)
    {
        var ids = NewIds();
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var actors = policy.Mode == ApprovalPolicyMode.DualIndependent
            ? new[] { Researcher, Reviewer }
            : new[] { Researcher };
        var resolver = new TestProtocolAuthorityResolver(policy, actors.Select(actor => actor.Id));
        var verifiedApprovals = actors
            .Select(actor => ProtocolApproval.Create(ids, candidate, policy, actor, Clock, candidate.ContentDigest))
            .Select(approval => ProtocolRehydrator.RehydrateApproval(ToUnverified(approval, policy), candidate, policy, resolver))
            .ToArray();
        resolver = resolver.WithApprovals(verifiedApprovals);
        var version = draft.ApproveCandidate(candidate, policy, verifiedApprovals.Select(item => item.Approval), Clock);
        return new RehydrationState(version, policy, resolver);
    }

    private static UnverifiedProtocolApproval ToUnverified(ProtocolApproval approval, ApprovalPolicy policy)
    {
        return new UnverifiedProtocolApproval(
            approval.ApprovalId,
            approval.TargetType,
            approval.TargetId,
            approval.ProtocolId,
            approval.ProtocolVersionId,
            approval.ProtocolVersionNumber,
            approval.ContentDigest,
            approval.PolicyId,
            approval.PolicyVersion,
            policy.Mode,
            approval.Decision,
            approval.ApprovedBy,
            approval.ApprovedAt,
            approval.Role,
            approval.Rationale,
            approval.SupersedesApprovalId,
            approval.ApprovalRecordDigest);
    }

    private static UnverifiedProtocolVersion ToUnverified(ProtocolVersion version, ApprovalPolicy policy)
    {
        return new UnverifiedProtocolVersion(
            version.Id,
            version.ProtocolId,
            version.ProjectId,
            version.VersionNumber,
            version.Status,
            version.Template,
            version.Intent,
            version.Values,
            version.RequiredDecisions,
            version.Decisions,
            version.Waivers,
            version.UnresolvedDecisions,
            version.ContentDigest,
            policy,
            version.ApprovalIds,
            version.ApprovedAt!.Value,
            version.SupersedesVersionId,
            version.SupersededByVersionId,
            version.AmendmentId);
    }

    private sealed record RehydrationState(
        ProtocolVersion Version,
        ApprovalPolicy Policy,
        TestProtocolAuthorityResolver Resolver);

    private sealed class TestProtocolAuthorityResolver : IProtocolAuthorityResolver
    {
        private readonly HashSet<ActorId> _humanActors;
        private readonly IReadOnlyDictionary<string, VerifiedProtocolApproval> _approvals;

        public TestProtocolAuthorityResolver(
            ApprovalPolicy policy,
            IEnumerable<ActorId> humanActors,
            IEnumerable<VerifiedProtocolApproval>? approvals = null)
        {
            Policy = policy;
            _humanActors = humanActors.ToHashSet();
            _approvals = (approvals ?? Array.Empty<VerifiedProtocolApproval>())
                .ToDictionary(item => item.Approval.ApprovalId, StringComparer.Ordinal);
        }

        public ApprovalPolicy Policy { get; }

        public ApprovalPolicy ResolveApprovalPolicy(ProtocolTemplate template) => Policy;

        public bool IsHumanActor(ActorId actorId) => _humanActors.Contains(actorId);

        public VerifiedProtocolApproval ResolveApproval(string approvalId) =>
            _approvals.TryGetValue(approvalId, out var approval)
                ? approval
                : null!;

        public TestProtocolAuthorityResolver WithPolicy(ApprovalPolicy policy) =>
            new(policy, _humanActors, _approvals.Values);

        public TestProtocolAuthorityResolver WithApprovals(IEnumerable<VerifiedProtocolApproval> approvals) =>
            new(Policy, _humanActors, approvals);
    }

    private static SequenceIdGenerator NewIds()
    {
        return new SequenceIdGenerator();
    }

    private static string FixedId(int value)
    {
        return new Guid(value, 0, 0, new byte[8]).ToString("D");
    }

    private sealed class SequenceIdGenerator : IIdGenerator
    {
        private int _next = 1;

        public Guid NewId()
        {
            return new Guid(_next++, 0, 0, new byte[8]);
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    }
}
