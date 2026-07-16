using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Appraisal;
using NexusScholar.Extraction;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Synthesis;
using NexusScholar.WorkflowExecution.ScientificRecords;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class Fe07AuthorityReplayTests
{
    private static readonly FixedClock Clock = new();
    private static readonly ProtocolActor ProtocolHuman = ProtocolActor.Human("researcher-1");
    private static readonly ExtractionActor ExtractionHuman = ExtractionActor.Human("researcher-1", "extractor");

    [TestMethod]
    public void Fe07_authority_fixture_replays_canonical_digests_and_stable_failures()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "fe07", "replay.json")));
        var protocol = CreateProtocol();
        var evidence = CreateEvidence(protocol);
        var form = ExtractionForm.Create("form-1", "candidate-1", protocol, ["question-outcome", "question-risk"],
            [new ExtractionFieldDefinition("outcome", "question-outcome", "string"), new ExtractionFieldDefinition("risk", "question-risk", "string")],
            ExtractionHuman, Clock.UtcNow);
        var values = new[]
        {
            new ExtractionFieldValue("outcome", CanonicalJsonValue.From("include"), evidence),
            new ExtractionFieldValue("risk", CanonicalJsonValue.From("low"), evidence)
        };
        var extraction = ExtractionRecord.Create("extraction-record-1", form.FormId, form.Digest, form.CandidateId,
            ExtractionRecordKind.Review, ExtractionHuman, values, Clock.UtcNow);
        var extractionJournal = ExtractionJournal.Create(form); extractionJournal.Append(extraction);

        var instrument = AppraisalInstrument.Create("instrument-1", AppraisalSchemas.SupportedInstrumentVersion, "review-method",
            [new AppraisalQuestion("clarity", "Is reporting clear?")], ["yes", "no"], ["supported", "unsupported"]);
        var appraisal = AppraisalRecord.CreateFinal("appraisal-record-1", protocol, instrument, "candidate-1",
            [new AppraisalAnswer("clarity", "yes", [evidence])], "supported", "Evidence supports the judgment.",
            ProtocolHuman, Clock, [evidence]);
        var appraisalJournal = new AppraisalJournal(); appraisalJournal.Append(appraisal);

        var source = SynthesisEligibleRecord.FromExtraction(extraction, extractionJournal);
        var plan = SynthesisPlanAuthority.Create("plan-1", protocol, [source],
            [new SynthesisSourceOutcome(source.RecordDigest, "outcome-1", "risk-ratio", "ratio")],
            [new SynthesisOutcome("outcome-1", "Outcome", "risk-ratio", "ratio", "12 weeks")],
            ["comparable populations"], [], "complete-case only", ["exclude high risk"],
            [new SynthesisCalculationDeclaration("mathnet", "5.0.0", new CanonicalJsonObject().Add("model", "fixed"))],
            new SynthesisActor("researcher-1", SynthesisActorKinds.Human, "methodologist"), Clock.UtcNow);
        var synthesisJournal = new SynthesisPlanJournal(); synthesisJournal.Append(plan);

        var amendment = CreateAmendment(protocol);
        var invalidation = ExtractionAmendmentInvalidation.Create("invalidation-1", form, amendment,
            [extraction.Digest], "Protocol changed.", ExtractionHuman, Clock.UtcNow);
        extractionJournal.Append(invalidation);
        var binding = VerifiedScientificRecordInvalidationBinding.Create("binding-1", ["extract", "appraise", "synthesize"], extraction: invalidation);

        var actual = new Dictionary<string, string>
        {
            ["evidenceLocation"] = evidence.Digest.ToString(),
            ["extractionForm"] = form.Digest.ToString(),
            ["extractionRecord"] = extraction.Digest.ToString(),
            ["appraisalInstrument"] = instrument.Digest.ToString(),
            ["appraisalRecord"] = appraisal.Digest.ToString(),
            ["synthesisPlan"] = plan.Digest.ToString(),
            ["workflowInvalidationBinding"] = binding.Digest.ToString()
        };
        var expected = fixture.RootElement.GetProperty("expectedDigests");
        foreach (var item in actual)
            Assert.AreEqual(expected.GetProperty(item.Key).GetString(), item.Value, $"digest:{item.Key}; actual-set={JsonSerializer.Serialize(actual)}");

        var errors = fixture.RootElement.GetProperty("expectedErrors");
        var automation = Assert.ThrowsExactly<ExtractionRuleException>(() => ExtractionRecord.Create("bad", form.FormId, form.Digest,
            form.CandidateId, ExtractionRecordKind.Review, ExtractionActor.Automation("model-1"), values, Clock.UtcNow));
        Assert.AreEqual(errors.GetProperty("automationExtraction").GetString(), automation.Category);
        var version = Assert.ThrowsExactly<AppraisalRuleException>(() => AppraisalInstrument.Create("bad", "9.9.9", "review-method",
            [new AppraisalQuestion("q", "Q?")], ["yes"], ["supported"]));
        Assert.AreEqual(errors.GetProperty("unknownInstrumentVersion").GetString(), version.Category);
        var mismatch = Assert.ThrowsExactly<SynthesisRuleException>(() => SynthesisPlanAuthority.Create("bad-plan", protocol, [source],
            [new SynthesisSourceOutcome(source.RecordDigest, "outcome-1", "mean-difference", "mg")],
            [new SynthesisOutcome("outcome-1", "Outcome", "risk-ratio", "ratio", "12 weeks")], ["assumption"], [], "complete-case",
            ["sensitivity"], [new SynthesisCalculationDeclaration("mathnet", "5.0.0", new CanonicalJsonObject().Add("model", "fixed"))],
            new SynthesisActor("researcher-1", SynthesisActorKinds.Human, "methodologist"), Clock.UtcNow));
        Assert.AreEqual(errors.GetProperty("measureMismatch").GetString(), mismatch.Category);
        var foreignMaterial = amendment.Amendment with
        {
            AmendsVersionId = "foreign-version",
            PreviousContentDigest = ContentDigest.Sha256Utf8("foreign-protocol")
        };
        var foreign = new VerifiedProtocolAmendment(foreignMaterial, ContentDigest.Sha256CanonicalJson(foreignMaterial.ToCanonicalJson()),
            amendment.Policy, amendment.PreviousVersion, amendment.ProducedVersion, amendment.Approvals);
        var crossProtocol = Assert.ThrowsExactly<ExtractionRuleException>(() => ExtractionAmendmentInvalidation.Create(
            "foreign-invalidation", form, foreign, [extraction.Digest], "wrong Protocol", ExtractionHuman, Clock.UtcNow));
        Assert.AreEqual(errors.GetProperty("crossProtocolInvalidation").GetString(), crossProtocol.Category);
    }

    private static VerifiedProtocolVersion CreateProtocol()
    {
        var ids = new SequenceIds(); var draft = ProtocolDraft.Create(ids, "fe07-protocol", ["question-outcome", "question-risk"]);
        draft.RecordDecision(ids, "question-outcome", CanonicalJsonValue.From("required"), ProtocolHuman, Clock);
        draft.RecordDecision(ids, "question-risk", CanonicalJsonValue.From("required"), ProtocolHuman, Clock);
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = draft.CreateApprovalCandidate(ids, policy, 1, "protocol-version-1");
        var approval = ProtocolApproval.Create(ids, candidate, policy, ProtocolHuman, Clock, candidate.ContentDigest);
        return draft.ApproveCandidateVerified(candidate, policy, [approval], Clock);
    }

    private static FullTextEvidenceLocation CreateEvidence(VerifiedProtocolVersion protocol)
    {
        var input = FullTextInput.FromScreeningDecision("input-1", "candidate-set-1", protocol.Version.Id, "decision-1", "stage-1",
            FullTextScreeningVerdicts.Include, needsReviewRetrievable: true);
        var attempts = new[] { new FullTextSourceAttempt("attempt-1", "local-stub", 1, FullTextAcquisitionKinds.DeterministicStubArtifact,
            FullTextAttemptStatuses.Success, artifactKind: FullTextArtifactKinds.Text, mediaType: "text/plain", artifactEvidenceId: "artifact-1") };
        var acquisition = new FullTextAcquisitionRecord("acquisition-1", input, FullTextAcquisitionKinds.DeterministicStubArtifact,
            "local-stub", "stub://artifact/1", new FullTextActor("researcher-1", FullTextActorKinds.Human), Clock.UtcNow,
            FullTextAttemptStatuses.Success, attempts, sourceUrl: "stub://artifact/1");
        var bytes = Encoding.UTF8.GetBytes("Exact evidence excerpt.");
        var artifact = FullTextArtifactEvidence.FromBytes("artifact-1", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);
        var chain = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024));
        var pages = new[] { "Exact evidence excerpt." };
        var record = new FullTextExtractionRecord("fulltext-extraction-1", artifact.ArtifactId, artifact.RawByteDigest, artifact.RawByteDigestScope,
            "extractor", "1.0.0", Clock.UtcNow, "text", FullTextExtractionStatuses.Success,
            FullTextExtractionRecord.ComputeRepresentationDigest(FullTextExtractionRepresentations.PageText, pages).ToString(),
            DigestScope.CanonicalJsonRecord.ToString(), pageText: pages, representationKind: FullTextExtractionRepresentations.PageText);
        return FullTextEvidenceLocation.Create("location-1", FullTextExtractionRehydrator.Rehydrate(chain, record),
            FullTextEvidenceLocationKinds.Page, 1, "page-1", pages[0]);
    }

    private static VerifiedProtocolAmendment CreateAmendment(VerifiedProtocolVersion protocol)
    {
        var ids = new SequenceIds(); var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var notice = new ProtocolInvalidationNotice("notice-1", "placeholder", "question-outcome", protocol.Version.ContentDigest,
            "extract", "replace", "rerun", Clock.UtcNow);
        var amendment = ProtocolAmendment.Create(ids, protocol.Version, "protocol-version-2", ProtocolHuman, Clock,
            "Outcome changed.", ["question-outcome"], [notice], policy);
        var seed = new ProtocolVersion(amendment.ProducesVersionId, protocol.Version.ProtocolId, protocol.Version.ProjectId,
            protocol.Version.VersionNumber + 1, ProtocolStatus.Approved, protocol.Version.Template, protocol.Version.Intent,
            protocol.Version.Values, protocol.Version.RequiredDecisions, protocol.Version.Decisions, protocol.Version.Waivers,
            ContentDigest.Sha256Utf8("placeholder"), protocol.Version.ApprovalPolicyId, protocol.Version.ApprovalIds, Clock.UtcNow,
            protocol.Version.Id, amendmentId: amendment.AmendmentId, unresolvedDecisions: protocol.Version.UnresolvedDecisions);
        var producedVersion = new ProtocolVersion(seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template,
            seed.Intent, seed.Values, seed.RequiredDecisions, seed.Decisions, seed.Waivers, seed.ToProtocolContentDigestEnvelope().ComputeDigest(),
            seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt, seed.SupersedesVersionId, seed.SupersededByVersionId, seed.AmendmentId, seed.UnresolvedDecisions);
        var produced = new VerifiedProtocolVersion(producedVersion, protocol.ApprovalPolicy, protocol.Approvals);
        return new VerifiedProtocolAmendment(amendment, ContentDigest.Sha256CanonicalJson(amendment.ToCanonicalJson()), policy, protocol, produced, []);
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero); }
    private sealed class SequenceIds : IIdGenerator
    {
        private int _next = 1;
        public Guid NewId() => new(_next++, 0, 0, new byte[8]);
    }
}
