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
    public void Blocking_unresolved_decision_prevents_approval()
    {
        var ids = NewIds();
        var draft = CreateCompleteDraft(ids);
        draft.AddUnresolvedDecision(
            ids,
            "quality-threshold",
            "What minimum score is acceptable?",
            "Template requires a threshold before conduct.",
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
        var draft = CreateCompleteDraft(ids);
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, candidate, policy, Researcher, Clock, candidate.ContentDigest);
        draft.AddWaiver(
            ids,
            "scope",
            null,
            null,
            "Pilot review has a narrow evidence source.",
            "Report scope limitation.",
            "limitations.scope",
            Researcher,
            Clock,
            policy);

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
        Assert.AreEqual(ProtocolErrorCodes.AutomationCannotApprove, error.Category);
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
        var withWaiverDraft = CreateCompleteDraft(secondIds);
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
        var candidate = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher());
        var before = candidate.ContentDigest;

        var deviation = ProtocolDeviation.Record(
            ids,
            candidate,
            "scope",
            "One data source was unavailable during conduct.",
            "Repository outage.",
            "protocol_deviation",
            Researcher,
            Clock,
            "disclose limitation",
            "limitations.data-source");

        Assert.AreEqual(candidate.Id, deviation.ProtocolVersionId);
        Assert.AreEqual(before, candidate.ContentDigest);
    }

    [TestMethod]
    public void Amendment_preserves_previous_digest_and_supersession_links()
    {
        var ids = NewIds();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var previous = CreateCompleteDraft(ids).CreateApprovalCandidate(ids, policy, versionId: FixedId(300));
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
        var draft = CreateDraft(ids);
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

    private static ProtocolDraft CreateDraft(IIdGenerator ids)
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
            new[]
            {
                RequiredDecision("review-type"),
                RequiredDecision("scope")
            },
            Researcher,
            Clock);
    }

    private static RequiredDecisionDefinition RequiredDecision(string key)
    {
        return new RequiredDecisionDefinition(
            key,
            key,
            $"Select {key}.",
            new CanonicalJsonObject().Add("type", "string"),
            "protocol-approval",
            "protocol-approval",
            key,
            false);
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
