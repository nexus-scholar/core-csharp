using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Appraisal;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class AppraisalTests
{
    private static readonly ProtocolActor HumanActor = ProtocolActor.Human("reviewer-1");
    private static readonly ProtocolActor AutomationActor = ProtocolActor.Automation("llm-appraisal");
    private static readonly IClock Clock = new FixedClock();

    [TestMethod]
    public void Appraisal_record_canonical_digest_is_deterministic()
    {
        var protocol = CreateVerifiedProtocol();
        var instrument = CreateInstrument();
        var evidence = CreateEvidence(protocol);
        var answers = new[]
        {
            new AppraisalAnswer("clarity", "high", [evidence]),
            new AppraisalAnswer("reproducibility", "yes", [evidence])
        };

        var first = AppraisalRecord.CreateFinal(
            "record-1",
            protocol,
            instrument,
            "candidate-1",
            answers,
            "supported",
            "rationale",
            HumanActor,
            Clock,
            [evidence]);

        var second = AppraisalRecord.CreateFinal(
            "record-1",
            protocol,
            instrument,
            "candidate-1",
            answers,
            "supported",
            "rationale",
            HumanActor,
            Clock,
            [evidence]);

        Assert.AreEqual(first.Digest, second.Digest);
    }

    [TestMethod]
    [DataRow(null, AppraisalErrorCodes.MissingInstrumentVersion)]
    [DataRow("0.0.1", AppraisalErrorCodes.UnknownInstrumentVersion)]
    public void Appraisal_instrument_create_validates_version(string version, string expectedCode)
    {
        var error = Assert.ThrowsExactly<AppraisalRuleException>(() =>
            AppraisalInstrument.Create(
                "instrument-1",
                version!,
                "meta-analytic",
                [new AppraisalQuestion("clarity", "Is reporting clear?")],
                ["yes", "no"],
                ["supported", "unsupported"]));

        Assert.AreEqual(expectedCode, error.Category);
    }

    [TestMethod]
    public void Appraisal_final_record_requires_complete_answers()
    {
        var protocol = CreateVerifiedProtocol();
        var instrument = CreateInstrument();
        var evidence = CreateEvidence(protocol);
        var partialAnswers = new[] { new AppraisalAnswer("clarity", "high", [evidence]) };

        var error = Assert.ThrowsExactly<AppraisalRuleException>(() =>
            AppraisalRecord.CreateFinal(
                "record-2",
                protocol,
                instrument,
                "candidate-1",
                partialAnswers,
                "supported",
                "partial",
                HumanActor,
                Clock,
                [evidence]));

        Assert.AreEqual(AppraisalErrorCodes.IncompleteAnswers, error.Category);
    }

    [TestMethod]
    public void Appraisal_final_record_requires_evidence()
    {
        var protocol = CreateVerifiedProtocol();
        var instrument = CreateInstrument();
        var evidence = CreateEvidence(protocol);
        var answers = new[]
        {
            new AppraisalAnswer("clarity", "high", [evidence]),
            new AppraisalAnswer("reproducibility", "yes", [evidence])
        };

        var error = Assert.ThrowsExactly<AppraisalRuleException>(() =>
            AppraisalRecord.CreateFinal(
                "record-3",
                protocol,
                instrument,
                "candidate-1",
                answers,
                "supported",
                "missing evidence",
                HumanActor,
                Clock,
                Array.Empty<FullTextEvidenceLocation>()));

        Assert.AreEqual(AppraisalErrorCodes.MissingEvidence, error.Category);
    }

    [TestMethod]
    public void Appraisal_finalization_is_human_only()
    {
        var protocol = CreateVerifiedProtocol();
        var instrument = CreateInstrument();
        var evidence = CreateEvidence(protocol);
        var answers = new[]
        {
            new AppraisalAnswer("clarity", "high", [evidence]),
            new AppraisalAnswer("reproducibility", "yes", [evidence])
        };

        var error = Assert.ThrowsExactly<AppraisalRuleException>(() =>
            AppraisalRecord.CreateFinal(
                "record-4",
                protocol,
                instrument,
                "candidate-1",
                answers,
                "supported",
                "automation denied",
                AutomationActor,
                Clock,
                [evidence]));

        Assert.AreEqual(AppraisalErrorCodes.AutomationFinalization, error.Category);
    }

    [TestMethod]
    public void Correction_requires_explicit_supersession_binding()
    {
        var protocol = CreateVerifiedProtocol();
        var instrument = CreateInstrument();
        var evidence = CreateEvidence(protocol);
        var answers = new[]
        {
            new AppraisalAnswer("clarity", "high", [evidence]),
            new AppraisalAnswer("reproducibility", "yes", [evidence])
        };

        var original = AppraisalRecord.CreateFinal(
            "record-5",
            protocol,
            instrument,
            "candidate-1",
            answers,
            "supported",
            "first",
            HumanActor,
            Clock,
            [evidence]);

        var correction = AppraisalRecord.CreateCorrection(
            "record-6",
            protocol,
            instrument,
            "candidate-1",
            answers,
            "unsupported",
            "correction",
            HumanActor,
            Clock,
            [evidence],
            original.RecordId,
            original.Digest);

        var journal = new AppraisalJournal();
        journal.Append(original);
        journal.Append(correction);

        Assert.AreEqual(original.RecordId, correction.SupersedesRecordId);
        Assert.AreEqual(original.Digest, correction.SupersedesRecordDigest);
        CollectionAssert.AreEqual(new[] { correction.Digest }, journal.CurrentRecords.Select(item => item.Digest).ToArray());
    }

    [TestMethod]
    public void Correction_without_supersession_record_is_rejected()
    {
        var protocol = CreateVerifiedProtocol();
        var instrument = CreateInstrument();
        var evidence = CreateEvidence(protocol);
        var answers = new[]
        {
            new AppraisalAnswer("clarity", "high", [evidence]),
            new AppraisalAnswer("reproducibility", "yes", [evidence])
        };

        var error = Assert.ThrowsExactly<AppraisalRuleException>(() =>
            AppraisalRecord.CreateCorrection(
                "record-7",
                protocol,
                instrument,
                "candidate-1",
                answers,
                "supported",
                "should fail",
                HumanActor,
                Clock,
                [evidence],
                string.Empty,
                default));

        Assert.AreEqual(AppraisalErrorCodes.MissingCorrectionTarget, error.Category);
    }

    [TestMethod]
    public void Appraisal_rejects_superseded_protocol_authority()
    {
        var approved = CreateVerifiedProtocol();
        var superseded = new VerifiedProtocolVersion(approved.Version.SupersededBy("protocol-version-2"), approved.ApprovalPolicy, approved.Approvals);
        var instrument = CreateInstrument();
        var evidence = CreateEvidence(approved);
        var answers = new[]
        {
            new AppraisalAnswer("clarity", "high", [evidence]),
            new AppraisalAnswer("reproducibility", "yes", [evidence])
        };
        Assert.ThrowsExactly<AppraisalRuleException>(() => AppraisalRecord.CreateFinal(
            "stale-record", superseded, instrument, "candidate-1", answers, "supported", "stale",
            HumanActor, Clock, [evidence]));
    }

    [TestMethod]
    public void Appraisal_invalidation_rejects_foreign_protocol_amendment()
    {
        var protocol = CreateVerifiedProtocol(); var instrument = CreateInstrument(); var evidence = CreateEvidence(protocol);
        var record = AppraisalRecord.CreateFinal("record-foreign", protocol, instrument, "candidate-1",
            [new AppraisalAnswer("clarity", "high", [evidence]), new AppraisalAnswer("reproducibility", "yes", [evidence])],
            "supported", "rationale", HumanActor, Clock, [evidence]);
        var journal = new AppraisalJournal(); journal.Append(record);
        var amendment = Fe07TestAuthority.Foreign(Fe07TestAuthority.CreateAmendment(protocol, "clarity-question", Clock));
        Assert.AreEqual(AppraisalErrorCodes.InvalidInvalidation,
            Assert.ThrowsExactly<AppraisalRuleException>(() => AppraisalAmendmentInvalidation.Create(
                "invalid-foreign", amendment, journal, [record], "wrong Protocol", HumanActor, Clock)).Category);
    }

    private static AppraisalInstrument CreateInstrument()
    {
        return AppraisalInstrument.Create(
            "risk-of-bias",
            AppraisalSchemas.SupportedInstrumentVersion,
            "systematic-review",
            [
                new AppraisalQuestion("clarity", "Is methodological reporting clear?"),
                new AppraisalQuestion("reproducibility", "Are methods reproducible?")
            ],
            ["low", "medium", "high", "yes", "no"],
            ["supported", "unsupported"]);
    }

    private static VerifiedProtocolVersion CreateVerifiedProtocol()
    {
        var ids = new SequenceIdGenerator();
        var draft = ProtocolDraft.Create(
            ids,
            "appraisal-protocol",
            ["clarity-question", "reproducibility-question"]);
        draft.RecordDecision(ids, "clarity-question", CanonicalJsonValue.From("required"), HumanActor, Clock);
        draft.RecordDecision(ids, "reproducibility-question", CanonicalJsonValue.From("required"), HumanActor, Clock);

        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = draft.CreateApprovalCandidate(ids, policy, versionNumber: 1, versionId: "protocol-version-1");
        var approval = ProtocolApproval.Create(ids, candidate, policy, HumanActor, Clock, candidate.ContentDigest);

        return draft.ApproveCandidateVerified(candidate, policy, new[] { approval }, Clock);
    }

    private static FullTextEvidenceLocation CreateEvidence(VerifiedProtocolVersion protocol)
    {
        var input = FullTextInput.FromScreeningDecision(
            "input-1",
            "candidate-set-1",
            protocol.Version.Id,
            "decision-1",
            "stage-1",
            FullTextScreeningVerdicts.Include,
            needsReviewRetrievable: true);
        var attempts = new List<FullTextSourceAttempt>
        {
            new(
                "attempt-1",
                "local-stub",
                1,
                FullTextAcquisitionKinds.DeterministicStubArtifact,
                FullTextAttemptStatuses.Success,
                artifactKind: FullTextArtifactKinds.Text,
                mediaType: "text/plain",
                artifactEvidenceId: "artifact-1")
        };
        var acquisition = new FullTextAcquisitionRecord(
            "acquisition-1",
            input,
            FullTextAcquisitionKinds.DeterministicStubArtifact,
            "local-stub",
            "stub://artifact/1",
            new FullTextActor("reviewer-1", "human"),
            Clock.UtcNow,
            FullTextAttemptStatuses.Success,
            attempts,
            sourceUrl: "stub://artifact/1");

        var bytes = Encoding.UTF8.GetBytes("evidence paragraph one\nsecond paragraph");
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-1",
            input,
            acquisition,
            FullTextArtifactKinds.Text,
            "text/plain",
            bytes,
            1024);
        var chain = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024));
        var extraction = new FullTextExtractionRecord(
            "extraction-1",
            artifact.ArtifactId,
            artifact.RawByteDigest,
            artifact.RawByteDigestScope,
            "extractor-local",
            "1.0.0",
            Clock.UtcNow,
            "text",
            FullTextExtractionStatuses.Success,
            FullTextExtractionRecord.ComputeRepresentationDigest(FullTextExtractionRepresentations.PageText, ["evidence paragraph one", "second paragraph"]).ToString(),
            DigestScope.CanonicalJsonRecord.ToString(),
            ["evidence paragraph one", "second paragraph"],
            representationKind: FullTextExtractionRepresentations.PageText);
        var verifiedExtraction = FullTextExtractionRehydrator.Rehydrate(chain, extraction);
        return FullTextEvidenceLocation.Create("location-1", verifiedExtraction, FullTextEvidenceLocationKinds.Page, 1, "page-1", "evidence paragraph one");
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class SequenceIdGenerator : IIdGenerator
    {
        private int _next = 1;

        public Guid NewId()
        {
            return new Guid(_next++, 0, 0, new byte[8]);
        }
    }
}
