using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Synthesis;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class SynthesisTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly SynthesisActor Human = new("analyst-1", SynthesisActorKinds.Human, "methodologist");

    [TestMethod]
    public void Synthesis_plan_is_deterministic_and_records_calculation_configuration()
    {
        var protocol = Protocol();
        var source = Source(protocol, isCurrent: true, isInvalidated: false);
        var first = Plan(protocol, source);
        var second = Plan(protocol, source);

        Assert.AreEqual(first.Digest, second.Digest);
        Assert.AreEqual("nexus.synthesis.plan", first.Envelope.SchemaId);
        CollectionAssert.AreEqual(first.ToCanonicalBytes(), second.ToCanonicalBytes());
    }

    [TestMethod]
    public void Synthesis_rejects_stale_or_invalidated_sources()
    {
        var protocol = Protocol();
        foreach (var source in new[] { Source(protocol, false, false), Source(protocol, true, true) })
        {
            var error = Assert.ThrowsExactly<SynthesisRuleException>(() => Plan(protocol, source));
            Assert.AreEqual(SynthesisErrorCodes.StaleSource, error.Category);
        }
    }

    [TestMethod]
    public void Synthesis_requires_explicit_effect_measure_or_unit_transformation()
    {
        var protocol = Protocol();
        var source = Source(protocol, true, false);
        var error = Assert.ThrowsExactly<SynthesisRuleException>(() => SynthesisPlanAuthority.Create(
            "plan-1", protocol, [source],
            [new SynthesisSourceOutcome(source.RecordDigest, "outcome-1", "mean-difference", "mg")],
            [new SynthesisOutcome("outcome-1", "Outcome", "risk-ratio", "ratio", "12 weeks")],
            ["comparable populations"], [], "complete-case only", ["exclude high risk"],
            [new SynthesisCalculationDeclaration("mathnet", "5.0.0", new CanonicalJsonObject().Add("model", "fixed"))], Human, Now));

        Assert.AreEqual(SynthesisErrorCodes.MeasureMismatch, error.Category);
    }

    [TestMethod]
    public void Automation_cannot_authorize_a_synthesis_plan()
    {
        var protocol = Protocol();
        var source = Source(protocol, true, false);
        var error = Assert.ThrowsExactly<SynthesisRuleException>(() => Create(
            protocol, source, new SynthesisActor("model-1", SynthesisActorKinds.Automation, "prefill")));
        Assert.AreEqual(SynthesisErrorCodes.AutomationCannotAuthorize, error.Category);
    }

    [TestMethod]
    public void Synthesis_requires_calculation_library_version_and_configuration()
    {
        var protocol = Protocol();
        var source = Source(protocol, true, false);
        var error = Assert.ThrowsExactly<SynthesisRuleException>(() => SynthesisPlanAuthority.Create(
            "plan-1", protocol, [source],
            [new SynthesisSourceOutcome(source.RecordDigest, "outcome-1", "risk-ratio", "ratio")],
            [new SynthesisOutcome("outcome-1", "Outcome", "risk-ratio", "ratio", "12 weeks")],
            ["comparable populations"], [], "complete-case only", ["exclude high risk"], [], Human, Now));
        Assert.AreEqual(SynthesisErrorCodes.InvalidAuthority, error.Category);
    }

    [TestMethod]
    public void Synthesis_rejects_superseded_protocol_authority()
    {
        var approved = Protocol();
        var superseded = new VerifiedProtocolVersion(approved.Version.SupersededBy("protocol-version-2"), approved.ApprovalPolicy, approved.Approvals);
        var source = new SynthesisEligibleRecord("extraction", "record-1", ContentDigest.Sha256Utf8("record-1"), "candidate-1",
            superseded.Version.Id, superseded.Version.ContentDigest, true, false);
        Assert.ThrowsExactly<SynthesisRuleException>(() => Plan(superseded, source));
    }

    [TestMethod]
    public void Synthesis_invalidation_rejects_foreign_protocol_amendment()
    {
        var protocol = Protocol(); var source = Source(protocol, true, false); var plan = Plan(protocol, source);
        var journal = new SynthesisPlanJournal(); journal.Append(plan);
        var amendment = Fe07TestAuthority.Foreign(Fe07TestAuthority.CreateAmendment(protocol, "outcome-definition", new Clock()));
        Assert.AreEqual(SynthesisErrorCodes.InvalidAuthority,
            Assert.ThrowsExactly<SynthesisRuleException>(() => SynthesisInvalidation.Create(
                "invalid-foreign", amendment, journal, [plan], "wrong Protocol", Human, Now)).Category);
    }

    private static VerifiedSynthesisPlan Plan(VerifiedProtocolVersion protocol, SynthesisEligibleRecord source) => Create(protocol, source, Human);

    private static VerifiedSynthesisPlan Create(VerifiedProtocolVersion protocol, SynthesisEligibleRecord source, SynthesisActor actor) =>
        SynthesisPlanAuthority.Create(
            "plan-1", protocol, [source],
            [new SynthesisSourceOutcome(source.RecordDigest, "outcome-1", "risk-ratio", "ratio")],
            [new SynthesisOutcome("outcome-1", "Outcome", "risk-ratio", "ratio", "12 weeks")],
            ["comparable populations"], [], "complete-case only", ["exclude high risk"],
            [new SynthesisCalculationDeclaration("mathnet", "5.0.0", new CanonicalJsonObject().Add("model", "fixed"))], actor, Now);

    private static SynthesisEligibleRecord Source(VerifiedProtocolVersion protocol, bool isCurrent, bool isInvalidated) => new(
        "extraction", "record-1", ContentDigest.Sha256Utf8("record-1"), "candidate-1",
        protocol.Version.Id, protocol.Version.ContentDigest, isCurrent, isInvalidated);

    private static VerifiedProtocolVersion Protocol()
    {
        var ids = new Ids(); var clock = new Clock(); var actor = ProtocolActor.Human("researcher-1");
        var draft = ProtocolDraft.Create(ids, "synthesis-protocol", ["outcome-definition"]);
        draft.RecordDecision(ids, "outcome-definition", CanonicalJsonValue.From("risk ratio at 12 weeks"), actor, clock);
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = draft.CreateApprovalCandidate(ids, policy, 1, "protocol-version-1");
        var approval = ProtocolApproval.Create(ids, candidate, policy, actor, clock, candidate.ContentDigest);
        return draft.ApproveCandidateVerified(candidate, policy, [approval], clock);
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class Ids : IIdGenerator
    {
        private int _next;
        public Guid NewId() { _next++; return Guid.Parse($"00000000-0000-0000-0000-{_next:000000000000}"); }
    }
}
