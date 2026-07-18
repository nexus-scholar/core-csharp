using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AI;
using NexusScholar.Bundles;
using NexusScholar.Extensibility;
using NexusScholar.Kernel;
using NexusScholar.Provenance;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class GovernanceTests
{
    [TestMethod]
    public void Scientific_model_proposal_requires_human_approval_policy()
    {
        Assert.ThrowsExactly<DomainRuleException>(() => AiTaskPolicy.Create(
            "screen-title-abstract",
            AiAuthority.ScientificDecisionProposal,
            humanApprovalRequired: false,
            evidenceRequired: true,
            externalDataTransferAllowed: false));
    }

    [TestMethod]
    public void Ai_proposals_are_immutable_evidence_and_expose_no_authority_transition()
    {
        var evidence = new List<ContentDigest> { ContentDigest.Sha256Utf8("source-evidence") };
        var policy = AiTaskPolicy.Create(
            "screen-title-abstract",
            AiAuthority.ScientificDecisionProposal,
            humanApprovalRequired: true,
            evidenceRequired: true,
            externalDataTransferAllowed: false);
        var proposal = new AiProposal<string>(
            policy,
            "include suggestion",
            evidence,
            new FixedClock().UtcNow);

        evidence.Clear();

        Assert.AreEqual(1, proposal.Evidence.Count);
        Assert.IsNull(typeof(AiProposal<string>).GetMethod("Accept", BindingFlags.Public | BindingFlags.Instance));
        Assert.IsNull(typeof(AiProposal<>).Assembly.GetType("NexusScholar.AI.AcceptedAiProposal`1"));
    }

    [TestMethod]
    public void Ai_proposals_snapshot_mutable_values_and_return_defensive_copies()
    {
        var policy = Policy();
        var source = new List<string> { "include suggestion" };
        var proposal = new AiProposal<List<string>>(
            policy,
            source,
            [ContentDigest.Sha256Utf8("source-evidence")],
            new FixedClock().UtcNow);

        source.Add("caller mutation");
        var firstRead = proposal.Value;
        firstRead.Add("consumer mutation");

        CollectionAssert.AreEqual(new[] { "include suggestion" }, proposal.Value);
    }

    [TestMethod]
    public void Ai_proposals_snapshot_supports_public_fields_and_rejects_private_state()
    {
        var policy = Policy();

        var source = new FieldBackedProposal { Suggestion = "original", Score = 4 };
        var proposal = new AiProposal<FieldBackedProposal>(
            policy,
            source,
            [ContentDigest.Sha256Utf8("source-evidence")],
            new FixedClock().UtcNow);

        source.Suggestion = "mutated";
        source.Score = 99;
        source.Candidates.Clear();

        var mutableRead = proposal.Value;
        mutableRead.Score = 0;
        mutableRead.Candidates.Add("attacker");

        var recovered = proposal.Value;
        Assert.AreEqual("original", recovered.Suggestion);
        CollectionAssert.AreEqual(new[] { "initial" }, recovered.Candidates);
        Assert.AreEqual(4, recovered.Score);

        var privateState = new PrivateStateProposal();
        privateState.SetStatus("reviewed");

        Assert.ThrowsExactly<DomainRuleException>(() => new AiProposal<PrivateStateProposal>(
            policy,
            privateState,
            [ContentDigest.Sha256Utf8("source-evidence")],
            new FixedClock().UtcNow));

        Assert.ThrowsExactly<DomainRuleException>(() => new AiProposal<object>(
            policy,
            new FieldBackedProposal(),
            [ContentDigest.Sha256Utf8("source-evidence")],
            new FixedClock().UtcNow));

        Assert.ThrowsExactly<DomainRuleException>(() => new AiProposal<PrivateAutoStateProposal>(
            policy,
            new PrivateAutoStateProposal(),
            [ContentDigest.Sha256Utf8("source-evidence")],
            new FixedClock().UtcNow));
    }

    [TestMethod]
    public void Ai_proposals_reject_null_or_unsnapshotable_values()
    {
        var policy = Policy();
        var evidence = new[] { ContentDigest.Sha256Utf8("source-evidence") };

        Assert.ThrowsExactly<DomainRuleException>(() => new AiProposal<string>(
            policy,
            null!,
            evidence,
            new FixedClock().UtcNow));
        Assert.ThrowsExactly<DomainRuleException>(() => new AiProposal<Func<int>>(
            policy,
            () => 1,
            evidence,
            new FixedClock().UtcNow));
    }

    [TestMethod]
    public void Ai_proposals_reject_invalid_evidence_digests()
    {
        var policy = AiTaskPolicy.Create(
            "screen-title-abstract",
            AiAuthority.ScientificDecisionProposal,
            humanApprovalRequired: true,
            evidenceRequired: true,
            externalDataTransferAllowed: false);
        Assert.ThrowsExactly<DomainRuleException>(() => new AiProposal<string>(
            policy,
            "include suggestion",
            [default],
            new FixedClock().UtcNow));
    }

    [TestMethod]
    public void Ai_proposals_bind_policy_evidence_and_utc_timestamp_requirements()
    {
        var policy = AiTaskPolicy.Create(
            "screen-title-abstract",
            AiAuthority.ScientificDecisionProposal,
            humanApprovalRequired: true,
            evidenceRequired: true,
            externalDataTransferAllowed: false);

        Assert.ThrowsExactly<DomainRuleException>(() => new AiProposal<string>(
            policy,
            "include suggestion",
            [],
            new FixedClock().UtcNow));
        Assert.ThrowsExactly<DomainRuleException>(() => new AiProposal<string>(
            policy,
            "include suggestion",
            [ContentDigest.Sha256Utf8("source-evidence")],
            default));
        Assert.AreEqual(0, typeof(AiTaskPolicy).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    [TestMethod]
    public void Extension_contracts_validate_construction_and_snapshot_capabilities()
    {
        var requested = new HashSet<ExtensionCapability> { ExtensionCapability.ReadProtocol };
        var manifest = ExtensionManifest.Create("extension-1", "1.0.0", "entry.dll", requested);
        var selection = CapabilitySelection.Create(manifest, requested);

        requested.Add(ExtensionCapability.RenderExport);

        CollectionAssert.AreEquivalent(
            new[] { ExtensionCapability.ReadProtocol },
            manifest.RequestedCapabilities.ToArray());
        CollectionAssert.AreEquivalent(
            new[] { ExtensionCapability.ReadProtocol },
            selection.Capabilities.ToArray());
        Assert.IsTrue(((ICollection<ExtensionCapability>)manifest.RequestedCapabilities).IsReadOnly);
        Assert.IsTrue(((ICollection<ExtensionCapability>)selection.Capabilities).IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ISet<ExtensionCapability>)manifest.RequestedCapabilities).Add(ExtensionCapability.RenderExport));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((ISet<ExtensionCapability>)selection.Capabilities).Add(ExtensionCapability.RenderExport));
        Assert.AreEqual(0, typeof(ExtensionManifest).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
        Assert.AreEqual(0, typeof(CapabilitySelection).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
    }

    [TestMethod]
    public void Extension_contracts_reject_invalid_identity_and_capabilities()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            ExtensionManifest.Create(" ", "1.0.0", "entry.dll", []));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ExtensionManifest.Create("extension-1", "1.0.0", "entry.dll", null!));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            ExtensionManifest.Create("extension-1", "1.0.0", "entry.dll", [(ExtensionCapability)999]));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            CapabilitySelection.Create(
                ExtensionManifest.Create(
                    "extension-1",
                    "1.0.0",
                    "entry.dll",
                    [ExtensionCapability.ReadProtocol]),
                [(ExtensionCapability)999]));
        Assert.ThrowsExactly<DomainRuleException>(() =>
            CapabilitySelection.Create(
                ExtensionManifest.Create(
                    "extension-1",
                    "1.0.0",
                    "entry.dll",
                    [ExtensionCapability.ReadProtocol]),
                [ExtensionCapability.RenderExport]));
    }

    [TestMethod]
    public void Provenance_store_is_append_only_and_rejects_duplicate_identity()
    {
        var ids = new GuidV7IdGenerator();
        var clock = new FixedClock();
        var researchEvent = ResearchEventFactory.Create(
            ids,
            clock,
            "protocol-approved",
            "protocol",
            "p-1",
            ActorId.From("researcher-1"));
        var store = new InMemoryProvenanceStore();
        store.Append(researchEvent);

        var error = Assert.ThrowsExactly<ProvenanceRuleException>(() => store.Append(researchEvent));
        Assert.AreEqual(ProvenanceErrorCodes.DuplicateEventId, error.Category);
    }

    [TestMethod]
    public void Bundle_verifier_rejects_duplicate_paths()
    {
        var digest = ContentDigest.Sha256Utf8("same content");
        var manifest = new ReviewBundleManifest(
            "nexus.review-bundle/v1",
            "project-1",
            digest,
            "workflow-1",
            new FixedClock().UtcNow,
            new[]
            {
                new BundleArtifact("protocol/protocol.json", "application/json", 12, digest),
                new BundleArtifact("protocol/protocol.json", "application/json", 12, digest)
            });

        Assert.IsFalse(new BundleVerifier().Verify(manifest).IsValid);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FieldBackedProposal
    {
        public string? Suggestion = "original";
        public int Score = 4;
        public List<string> Candidates = ["initial"];
    }

    private sealed class PrivateStateProposal
    {
        private string status = "new";
        public string Status => status;
        public void SetStatus(string value) => status = value;
    }

    private sealed class PrivateAutoStateProposal
    {
        public string Suggestion { get; set; } = "visible";
        private string Status { get; set; } = "hidden";
    }

    private static AiTaskPolicy Policy() => AiTaskPolicy.Create(
        "screen-title-abstract",
        AiAuthority.ScientificDecisionProposal,
        humanApprovalRequired: true,
        evidenceRequired: true,
        externalDataTransferAllowed: false);
}
