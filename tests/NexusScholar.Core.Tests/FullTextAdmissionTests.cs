using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;
using NexusScholar.Screening.FullText;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class FullTextAdmissionTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 7, 15, 9, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void Verified_admission_round_trips_from_include_handoff_and_stays_deterministic()
    {
        var (policy, header) = BuildConductAuthority("include", 1);
        var decision = ScreeningConductDecision.Create(
            header, 1, header.Digest, "request-fulltext-1", "candidate-1", ScreeningConductDecisionKind.Review,
            ScreeningVerdicts.Include, new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"),
            "Include this candidate", FixedTime);
        var journal = ScreeningConductJournal.Rehydrate(header, policy, [decision]);
        var handoff = ScreeningConductHandoff.Create("handoff-1", journal, FixedTime);

        var admission = VerifiedFullTextAdmission.Create(journal, handoff, "candidate-1");
        var bytes = VerifiedFullTextAdmissionCanonicalCodec.Serialize(admission);
        var reopened = VerifiedFullTextAdmissionCanonicalCodec.Rehydrate(bytes, admission.Digest, journal, handoff);

        Assert.AreEqual("handoff-1", reopened.HandoffId);
        Assert.AreEqual("candidate-set-include", reopened.CandidateSetId);
        Assert.AreEqual("candidate-1", reopened.CandidateId);
        Assert.AreEqual(ScreeningVerdicts.Include, reopened.Verdict);
        Assert.AreEqual("title_abstract", reopened.Input.ScreeningStage);
        Assert.AreEqual(admission.Digest, reopened.Digest);
        Assert.AreEqual(admission.Input.InputId, reopened.Input.InputId);
        Assert.AreEqual(admission.InputDigest, reopened.InputDigest);

        var replay = VerifiedFullTextAdmission.Create(journal, handoff, "candidate-1");
        Assert.AreEqual(admission.Digest, replay.Digest);
    }

    [TestMethod]
    public void Admission_rejects_excluded_wrong_and_missing_candidate()
    {
        var (policy, header) = BuildConductAuthority("reject", 1);
        var decision = ScreeningConductDecision.Create(
            header, 1, header.Digest, "request-fulltext-2", "candidate-1", ScreeningConductDecisionKind.Review,
            ScreeningVerdicts.Exclude, new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"),
            "Exclude this candidate", FixedTime, exclusionReasonCode: "wrong-population");
        var journal = ScreeningConductJournal.Rehydrate(header, policy, [decision]);
        var handoff = ScreeningConductHandoff.Create("handoff-2", journal, FixedTime);

        var includeError = Assert.ThrowsExactly<FullTextRuleException>(() =>
            VerifiedFullTextAdmission.Create(journal, handoff, "candidate-1"));
        Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, includeError.Category);

        var wrongError = Assert.ThrowsExactly<FullTextRuleException>(() =>
            VerifiedFullTextAdmission.Create(journal, handoff, "candidate-2"));
        Assert.AreEqual(FullTextErrorCodes.MissingCandidateBinding, wrongError.Category);

        var missingError = Assert.ThrowsExactly<FullTextRuleException>(() =>
            VerifiedFullTextAdmission.Create(journal, handoff, "candidate-missing"));
        Assert.AreEqual(FullTextErrorCodes.MissingCandidateBinding, missingError.Category);
    }

    [TestMethod]
    public void Admission_rejects_stale_handoff_after_journal_invalidation()
    {
        var (policy, header) = BuildConductAuthority("stale", 1);
        var decision = ScreeningConductDecision.Create(
            header, 1, header.Digest, "request-fulltext-3", "candidate-1", ScreeningConductDecisionKind.Review,
            ScreeningVerdicts.Include, new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"),
            "Include this candidate", FixedTime);
        var journal = ScreeningConductJournal.Rehydrate(header, policy, [decision]);
        var handoff = ScreeningConductHandoff.Create("handoff-3", journal, FixedTime);

        var invalidation = ScreeningConductInvalidation.Create(
            header, 2, decision.Digest, "invalidation-3",
            new ScreeningConductEvidenceRef("protocol-version", policy.ProtocolVersionId, policy.ProtocolContentDigest),
            [decision.Digest],
            new ScreeningConductActor("chair-1", ScreeningConductActorKinds.Human, "chair"),
            "Handoff refresh required.", FixedTime);
        journal.Append(invalidation);

        var error = Assert.ThrowsExactly<FullTextRuleException>(() =>
            VerifiedFullTextAdmission.Create(journal, handoff, "candidate-1"));
        Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, error.Category);
    }

    [TestMethod]
    public void Admission_rejects_tampered_or_incomplete_support_from_canonical_payload()
    {
        var (policy, header) = BuildConductAuthority("tampered", 2);
        var first = ScreeningConductDecision.Create(
            header, 1, header.Digest, "request-tampered-a", "candidate-1", ScreeningConductDecisionKind.Review,
            ScreeningVerdicts.Include, new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"),
            "Reviewer A includes", FixedTime);
        var second = ScreeningConductDecision.Create(
            header, 2, first.Digest, "request-tampered-b", "candidate-1", ScreeningConductDecisionKind.Review,
            ScreeningVerdicts.Include, new ScreeningConductActor("reviewer-2", ScreeningConductActorKinds.Human, "reviewer"),
            "Reviewer B includes", FixedTime);
        var journal = ScreeningConductJournal.Rehydrate(header, policy, [first, second]);
        var handoff = ScreeningConductHandoff.Create("handoff-4", journal, FixedTime);

        var admission = VerifiedFullTextAdmission.Create(journal, handoff, "candidate-1");
        var bytes = VerifiedFullTextAdmissionCanonicalCodec.Serialize(admission);

        var tampered = Mutate(bytes, root =>
        {
            var digests = (JsonArray)root["content"]!["supporting_decision_digests"]!;
            digests.RemoveAt(0);
        });
        var tamperedDigest = ContentDigest.Sha256(tampered);

        var tamperError = Assert.ThrowsExactly<FullTextRuleException>(() =>
            VerifiedFullTextAdmissionCanonicalCodec.Rehydrate(tampered, tamperedDigest, journal, handoff));
        Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, tamperError.Category);

        var nonCanonical = tampered.Concat([(byte)'\n']).ToArray();
        Assert.ThrowsExactly<FullTextRuleException>(() =>
            VerifiedFullTextAdmissionCanonicalCodec.Rehydrate(nonCanonical, tamperedDigest, journal, handoff));
    }

    [TestMethod]
    public void Admission_digests_are_deterministic_for_identical_authority_inputs()
    {
        var (policy, header) = BuildConductAuthority("deterministic", 1);
        var decision = ScreeningConductDecision.Create(
            header, 1, header.Digest, "request-fulltext-5", "candidate-1", ScreeningConductDecisionKind.Review,
            ScreeningVerdicts.Include, new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"),
            "Include this candidate", FixedTime);
        var journal = ScreeningConductJournal.Rehydrate(header, policy, [decision]);
        var handoff = ScreeningConductHandoff.Create("handoff-5", journal, FixedTime);

        var first = VerifiedFullTextAdmission.Create(journal, handoff, "candidate-1");
        var second = VerifiedFullTextAdmission.Create(journal, handoff, "candidate-1");
        Assert.AreEqual(first.Digest, second.Digest);
    }

    private static (ScreeningConductPolicy Policy, ScreeningConductHeader Header) BuildConductAuthority(string suffix, int reviewCount)
    {
        var protocol = BuildVerifiedProtocol();
        var dedup = BuildDedupResult($"dedup-{suffix}", ["candidate-1"], []);
        var verifiedDedup = DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(dedup));
        var criteria = BuildAuthorityCriteria(protocol);

        var policy = ScreeningConductPolicy.Create(
            $"policy-{suffix}",
            $"candidate-set-{suffix}",
            verifiedDedup,
            protocol,
            criteria,
            reviewCount,
            [
                new ScreeningConductRoleAssignment("reviewer-1", "reviewer"),
                new ScreeningConductRoleAssignment("reviewer-2", "reviewer"),
                new ScreeningConductRoleAssignment("chair-1", "chair")
            ],
            ["chair"],
            [new ScreeningExclusionReason("wrong-population", ScreeningStages.TitleAbstract)],
            new ScreeningConductActor("chair-1", ScreeningConductActorKinds.Human, "chair"),
            FixedTime);

        var header = ScreeningConductHeader.Create(
            $"conduct-{suffix}",
            policy,
            new ScreeningConductActor("reviewer-1", ScreeningConductActorKinds.Human, "reviewer"),
            FixedTime);

        return (policy, header);
    }

    private static DeduplicationResult BuildDedupResult(
        string resultId,
        IReadOnlyList<string> candidateIds,
        IReadOnlyList<string> unresolvedCandidateIds)
    {
        return new DeduplicationResult(
            resultId,
            "nexus.deduplication.result",
            "1.0.0",
            DeduplicationService.PolicyId,
            DeduplicationService.PolicyVersion,
            0.95d,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal)),
            Array.Empty<string>(),
            Array.Empty<string>(),
            candidateIds.Select(id => BuildCandidate(id, true)).ToArray(),
            Array.Empty<DedupCluster>(),
            Array.Empty<DedupEvidence>(),
            unresolvedCandidateIds.Select(id => BuildCandidate(id, false)).ToArray(),
            Array.Empty<DedupReviewCandidate>(),
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            ["no-php-compatibility-claim"]);
    }

    private static VerifiedProtocolVersion BuildVerifiedProtocol()
    {
        var seed = new ProtocolVersion(
            "protocol-screening-v1", "protocol-screening", "project-1", 1, ProtocolStatus.Approved,
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("template")),
            new ProtocolIntent("screening", "project records"),
            new CanonicalJsonObject(),
            Array.Empty<RequiredDecisionDefinition>(), Array.Empty<ProtocolDecision>(), Array.Empty<ProtocolWaiver>(),
            ContentDigest.Sha256Utf8("placeholder"),
            ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId,
            ["approval-1"],
            DateTimeOffset.UtcNow);
        var version = new ProtocolVersion(
            seed.Id,
            seed.ProtocolId,
            seed.ProjectId,
            seed.VersionNumber,
            seed.Status,
            seed.Template,
            seed.Intent,
            seed.Values,
            seed.RequiredDecisions,
            seed.Decisions,
            seed.Waivers,
            seed.ToProtocolContentDigestEnvelope().ComputeDigest(),
            seed.ApprovalPolicyId,
            seed.ApprovalIds,
            seed.ApprovedAt);

        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), Array.Empty<VerifiedProtocolApproval>());
    }

    private static ScreeningCriteria BuildAuthorityCriteria(VerifiedProtocolVersion protocol) => new(
        "criteria-authority",
        "1.0.0",
        ScreeningStages.TitleAbstract,
        CanonicalJsonValue.From("include"),
        CanonicalJsonValue.From("exclude"),
        true,
        protocol.Version.Id,
        protocol.Version.ContentDigest.ToString(),
        approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
        approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
        currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());

    private static DedupCandidateRecord BuildCandidate(string candidateId, bool stableIdentifier)
    {
        return new DedupCandidateRecord(
            candidateId,
            "candidate title",
            stableIdentifier,
            stableIdentifier ? $"work:{candidateId}" : null,
            stableIdentifier ? [candidateId] : Array.Empty<string>(),
            Array.Empty<string>(),
            new DedupSightingRef("search", "trace-001", $"source-{candidateId}"));
    }

    private static byte[] Mutate(byte[] bytes, Action<JsonObject> mutation)
    {
        var root = JsonNode.Parse(bytes)!.AsObject();
        mutation(root);
        using var document = JsonDocument.Parse(root.ToJsonString());
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
    }
}
