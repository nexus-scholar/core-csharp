using System.Reflection;
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
