using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Workflow;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class RapidReviewProfileTests
{
    [TestMethod]
    public void Profile_binds_verified_workflow_and_replays_deterministically()
    {
        var workflow = BuildWorkflow();
        var profile = RapidReviewProfileAuthority.Create("rapid-profile-1", workflow, [Shortcut()]);
        var second = RapidReviewProfileAuthority.Create("rapid-profile-1", workflow, [Shortcut()]);
        var bytes = RapidReviewProfileCanonicalCodec.Serialize(profile);

        Assert.AreEqual(profile.RecordDigest, second.RecordDigest);
        CollectionAssert.AreEqual(bytes, RapidReviewProfileCanonicalCodec.Serialize(second));
        Assert.AreEqual(profile.RecordDigest, RapidReviewProfileCanonicalCodec.Rehydrate(
            bytes, profile.RecordDigest, profile.Record, workflow).RecordDigest);
        CollectionAssert.AreEqual(RapidReviewProfileConstants.ProtectedInvariants.ToArray(), profile.Record.ProtectedInvariants.ToArray());
    }

    [TestMethod]
    public void Shortcut_requires_resolved_conduct_mitigation_approval_and_invalidation_refs()
    {
        var workflow = BuildWorkflow();
        foreach (var shortcut in new[]
        {
            Shortcut() with { ActivationInputRef = "missing" },
            Shortcut() with { AffectedRequirementRefs = ["missing"] },
            Shortcut() with { AffectedNodeRefs = ["missing"] },
            Shortcut() with { RequiredMitigationArtifactRefs = ["missing"] },
            Shortcut() with { ApprovalRequirementRef = "missing" },
            Shortcut() with { InvalidationPolicyRef = "missing" },
            Shortcut() with { Consequence = "" },
            Shortcut() with { Mitigation = "" },
            Shortcut() with { ReportingDisclosure = "" }
        })
        {
            Assert.ThrowsExactly<RapidReviewProfileException>(() =>
                RapidReviewProfileAuthority.Create("rapid-invalid", workflow, [shortcut]));
        }
    }

    [TestMethod]
    public void Automation_approval_and_defaulted_activation_are_rejected()
    {
        Assert.AreEqual(WorkflowErrorCodes.AutomationApprovalAuthority,
            Assert.ThrowsExactly<WorkflowRuleException>(() => BuildWorkflow(allowsAutomation: true)).Category);

        var defaulted = BuildWorkflow(defaultedActivation: true);
        Assert.AreEqual(RapidReviewProfileErrorCodes.UnsafeShortcut,
            Assert.ThrowsExactly<RapidReviewProfileException>(() =>
                RapidReviewProfileAuthority.Create("rapid-default", defaulted, [Shortcut()])).Category);
    }

    [TestMethod]
    public void Rehydration_rejects_altered_invariants_digest_and_bytes()
    {
        var workflow = BuildWorkflow();
        var profile = RapidReviewProfileAuthority.Create("rapid-profile-tamper", workflow, [Shortcut()]);
        var altered = profile.Record with { ProtectedInvariants = [.. profile.Record.ProtectedInvariants, "mutable-authority"] };
        Assert.ThrowsExactly<RapidReviewProfileException>(() => RapidReviewProfileAuthority.Rehydrate(altered, workflow));
        var bytes = RapidReviewProfileCanonicalCodec.Serialize(profile).Concat([(byte)' ']).ToArray();
        Assert.ThrowsExactly<RapidReviewProfileException>(() => RapidReviewProfileCanonicalCodec.Rehydrate(
            bytes, profile.RecordDigest, profile.Record, workflow));
    }

    [TestMethod]
    public void Profile_normalizes_all_resolved_reference_identifiers()
    {
        var workflow = BuildWorkflow();
        var profile = RapidReviewProfileAuthority.Create("rapid-normalized", workflow,
            [Shortcut() with { ActivationInputRef = " rapid-mode ", ApprovalRequirementRef = " approve-rapid ", InvalidationPolicyRef = " invalidate-rapid " }]);
        var shortcut = profile.Shortcuts.Single();

        Assert.AreEqual("rapid-mode", shortcut.ActivationInputRef);
        Assert.AreEqual("approve-rapid", shortcut.ApprovalRequirementRef);
        Assert.AreEqual("invalidate-rapid", shortcut.InvalidationPolicyRef);
    }

    private static RapidReviewShortcut Shortcut() => new(
        "shortcut-screening", "rapid-mode", ["rapid-mode"], ["screen"],
        "Lower precision is possible.", "Perform a documented verification sample.", ["mitigation-log"],
        "approve-rapid", "Disclose the activated Rapid Review shortcut and mitigation.", "invalidate-rapid");

    private static VerifiedWorkflowDefinition BuildWorkflow(bool allowsAutomation = false, bool defaultedActivation = false)
    {
        var protocol = BuildProtocol();
        var template = new WorkflowTemplate(
            "rapid-template", "1.0.0", ContentDigest.Sha256Utf8("placeholder"), "nexus.workflow-template", "1.0.0",
            [new WorkflowTemplateInput("rapid-mode", WorkflowTemplateInputKind.ScientificConduct, "nexus.review.decision", "1.0.0", false, null,
                defaultedActivation ? CanonicalJsonValue.From("off") : null)],
            [new WorkflowTemplateNode("screen", WorkflowNodeKind.HumanTask, WorkflowNodeMode.Human, "Screen", ["rapid-mode"], ["mitigation-log"],
                "approve-rapid", [], null, "invalidate-rapid")], [], [],
            [new WorkflowTemplateApprovalRequirement("approve-rapid", "rapid-approval", "1.0.0", "single_researcher", ["reviewer"], 1, false, allowsAutomation)],
            [new WorkflowTemplateRole("reviewer", "Reviewer", "Human scientific authority")], [], [],
            [new WorkflowTemplateArtifactDeclaration("mitigation-log", "rapid-mitigation", "nexus.workflow.artifact", "1.0.0", "screen", [])],
            [new WorkflowTemplateInvalidationPolicy("invalidate-rapid", ["rapid-mode"], ["mitigation-log"], ["screen"], "repeat-screening")]);
        template = template with { TemplateDigest = WorkflowCompiler.ComputeLocalTemplateDigest(template) };
        var definition = new WorkflowCompiler().Compile(new WorkflowCompileInput(protocol, template,
            new Dictionary<string, CanonicalJsonValue>(),
            [new WorkflowSchemaRef("nexus.workflow-template", "1.0.0"), new WorkflowSchemaRef("nexus.workflow-definition", "1.1.0"),
             new WorkflowSchemaRef("nexus.review.decision", "1.0.0"), new WorkflowSchemaRef("nexus.workflow.artifact", "1.0.0")]));
        return WorkflowRehydrator.Rehydrate(WorkflowRehydrator.FromCompiled(definition), new Resolver(protocol, template));
    }

    private static VerifiedProtocolVersion BuildProtocol()
    {
        var seed = new ProtocolVersion("protocol-rapid-v1", "protocol-rapid", "project-rapid", 1, ProtocolStatus.Approved,
            new ProtocolTemplate("protocol-template", "1.0.0", ContentDigest.Sha256Utf8("protocol-template")),
            new ProtocolIntent("rapid review", "test rapid profile"), new CanonicalJsonObject(), [], [], [],
            ContentDigest.Sha256Utf8("placeholder"), ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId, ["approval-1"], Now);
        var version = new ProtocolVersion(seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template,
            seed.Intent, seed.Values, seed.RequiredDecisions, seed.Decisions, seed.Waivers, seed.ToProtocolContentDigestEnvelope().ComputeDigest(),
            seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt);
        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), []);
    }

    private sealed class Resolver(VerifiedProtocolVersion protocol, WorkflowTemplate template) : IWorkflowAuthorityResolver
    {
        public VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId) => protocol;
        public VerifiedProtocolWaiver ResolveProtocolWaiver(string waiverId) => throw new KeyNotFoundException();
        public VerifiedProtocolAmendment ResolveProtocolAmendment(string amendmentId) => throw new KeyNotFoundException();
        public WorkflowTemplate ResolveTemplate(string templateId, string templateVersion, ContentDigest expectedDigest) => template;
        public CanonicalJsonValue ResolveCompileParameter(string inputId, ContentDigest expectedValueDigest) => throw new KeyNotFoundException();
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 16, 10, 0, 0, TimeSpan.Zero);
}
