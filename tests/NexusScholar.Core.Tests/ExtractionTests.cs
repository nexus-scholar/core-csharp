using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Extraction;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ExtractionTests
{
    private static readonly ExtractionActor HumanOne = ExtractionActor.Human("extractor-human-1");
    private static readonly ExtractionActor HumanTwo = ExtractionActor.Human("extractor-human-2");
    private static readonly ExtractionActor Automation = ExtractionActor.Automation("extractor-bot");
    private static readonly ProtocolActor ProtocolHumanOne = ProtocolActor.Human("extractor-human-1");
    private static readonly ProtocolActor ProtocolHumanTwo = ProtocolActor.Human("extractor-human-2");
    private static readonly IClock Clock = new FixedClock();

    [TestMethod]
    public void Extraction_journal_valid_flow_tracks_current_records_and_order()
    {
        var protocol = CreateVerifiedProtocol();
        var form = CreateForm(protocol);
        var journal = ExtractionJournal.Create(form);
        var evidence = CreateEvidence(protocol, "evidence-1", "population-based meta-analysis");

        var proposal = ExtractionRecord.Create(
            "record-1",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Proposal,
            Automation,
            CreateValues(evidence, "A", "B"),
            Clock.UtcNow);

        var review = ExtractionRecord.Create(
            "record-2",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Review,
            HumanOne,
            CreateValues(evidence, "A", "B"),
            Clock.UtcNow);

        journal.Append(proposal);
        journal.Append(review);

        var current = journal.CurrentRecords(form.CandidateId);
        Assert.AreEqual(1, current.Count);
        Assert.IsTrue(current.Any(item => item.Digest == review.Digest));
        Assert.AreEqual(0, journal.CurrentConflicts(form.CandidateId).Count);
        Assert.AreEqual(review.Digest, journal.Projection.HeadDigest);
    }

    [TestMethod]
    public void Extraction_automation_cannot_finalize_review_records()
    {
        var protocol = CreateVerifiedProtocol();
        var form = CreateForm(protocol);
        var evidence = CreateEvidence(protocol, "evidence-2", "automation cannot review");
        var error = Assert.ThrowsExactly<ExtractionRuleException>(() =>
            ExtractionRecord.Create(
                "record-1",
                form.FormId,
                form.Digest,
                form.CandidateId,
                ExtractionRecordKind.Review,
                Automation,
                CreateValues(evidence, "A", "B"),
                Clock.UtcNow));
        Assert.AreEqual(ExtractionErrorCodes.AutomationCannotFinalize, error.Category);
    }

    [TestMethod]
    public void Extraction_correction_replaces_single_current_record()
    {
        var protocol = CreateVerifiedProtocol();
        var form = CreateForm(protocol);
        var journal = ExtractionJournal.Create(form);
        var evidence = CreateEvidence(protocol, "evidence-3", "correctable extraction");

        var baseline = ExtractionRecord.Create(
            "record-1",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Review,
            HumanOne,
            CreateValues(evidence, "x", "y"),
            Clock.UtcNow);
        journal.Append(baseline);

        var correction = ExtractionRecord.Create(
            "record-2",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Correction,
            HumanOne,
            CreateValues(evidence, "x-corrected", "y"),
            Clock.UtcNow,
            sourceRecordDigest: baseline.Digest.ToString());
        journal.Append(correction);

        var current = journal.CurrentRecords(form.CandidateId);
        Assert.AreEqual(1, current.Count);
        Assert.AreEqual(correction.Digest, current[0].Digest);
        Assert.IsFalse(current.Any(item => item.Digest == baseline.Digest));
    }

    [TestMethod]
    public void Extraction_disagreement_can_be_resolved_by_human_resolution_record()
    {
        var protocol = CreateVerifiedProtocol();
        var form = CreateForm(protocol);
        var journal = ExtractionJournal.Create(form);
        var conflictEvidence = CreateEvidence(protocol, "evidence-4", "methodological disagreement");

        var reviewOne = ExtractionRecord.Create(
            "record-1",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Review,
            HumanOne,
            new[]
            {
                new ExtractionFieldValue("outcome", CanonicalJsonValue.From("include"), conflictEvidence),
                new ExtractionFieldValue("risk", CanonicalJsonValue.From("low"), conflictEvidence)
            },
            Clock.UtcNow);

        var reviewTwo = ExtractionRecord.Create(
            "record-2",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Review,
            HumanTwo,
            new[]
            {
                new ExtractionFieldValue("outcome", CanonicalJsonValue.From("exclude"), conflictEvidence),
                new ExtractionFieldValue("risk", CanonicalJsonValue.From("low"), conflictEvidence)
            },
            Clock.UtcNow);

        journal.Append(reviewOne);
        journal.Append(reviewTwo);

        var conflicts = journal.CurrentConflicts(form.CandidateId);
        Assert.AreEqual(1, conflicts.Count);
        var conflict = conflicts[0];

        var resolution = ExtractionRecord.Create(
            "record-3",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Resolution,
            HumanOne,
            new[] { new ExtractionFieldValue("outcome", CanonicalJsonValue.From("include"), conflictEvidence) },
            Clock.UtcNow,
            sourceRecordDigests: conflict.SourceRecordDigests,
            sourceConflictId: conflict.ConflictId);

        journal.Append(resolution);

        var current = journal.CurrentRecords(form.CandidateId);
        Assert.AreEqual(1, current.Count);
        Assert.AreEqual(resolution.Digest, current[0].Digest);
        Assert.AreEqual(0, journal.CurrentConflicts(form.CandidateId).Count);
    }

    [TestMethod]
    public void Extraction_field_values_require_exact_evidence_location()
    {
        var protocol = CreateVerifiedProtocol();
        var form = CreateForm(protocol);
        var journal = ExtractionJournal.Create(form);
        var evidence = CreateEvidence(protocol, "evidence-5", "missing evidence");

        var invalid = new ExtractionFieldValue("outcome", CanonicalJsonValue.From("include"), null!);
        var proposed = ExtractionRecord.Create(
            "record-1",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Proposal,
            Automation,
            new[] { invalid, new ExtractionFieldValue("risk", CanonicalJsonValue.From("low"), evidence) },
            Clock.UtcNow);

        var error = Assert.ThrowsExactly<ExtractionRuleException>(() => journal.Append(proposed));
        Assert.AreEqual(ExtractionErrorCodes.MissingFieldEvidence, error.Category);
    }

    [TestMethod]
    public void Amendment_invalidation_supersedes_exact_target_records()
    {
        var protocol = CreateVerifiedProtocol();
        var form = CreateForm(protocol);
        var journal = ExtractionJournal.Create(form);
        var evidence = CreateEvidence(protocol, "evidence-6", "needs invalidation");
        var record = ExtractionRecord.Create(
            "record-1",
            form.FormId,
            form.Digest,
            form.CandidateId,
            ExtractionRecordKind.Review,
            HumanOne,
            CreateValues(evidence, "include", "low"),
            Clock.UtcNow);

        journal.Append(record);

        var amendment = CreateVerifiedProtocolAmendment(protocol);
        var foreign = Fe07TestAuthority.Foreign(amendment);
        Assert.AreEqual(ExtractionErrorCodes.InvalidRecordBinding,
            Assert.ThrowsExactly<ExtractionRuleException>(() => ExtractionAmendmentInvalidation.Create(
                "foreign-invalidation", form, foreign, [record.Digest], "wrong Protocol", HumanOne, Clock.UtcNow)).Category);
        var invalidation = ExtractionAmendmentInvalidation.Create(
            "invalidation-1",
            form,
            amendment,
            new[] { record.Digest },
            "Protocol amendment changed evidence requirements.",
            HumanOne,
            Clock.UtcNow);

        journal.Append(invalidation);

        Assert.AreEqual(0, journal.CurrentRecords(form.CandidateId).Count);
        Assert.AreEqual(1, journal.Invalidations.Count);
        Assert.AreEqual(1, journal.Projection.InvalidatedRecordDigests.Count);
        Assert.AreEqual(record.Digest, journal.Projection.InvalidatedRecordDigests[0]);
    }

    [TestMethod]
    public void Appended_records_cannot_be_reused_or_duplicate_logical_ids()
    {
        var protocol = CreateVerifiedProtocol();
        var form = CreateForm(protocol);
        var journal = ExtractionJournal.Create(form);
        var evidence = CreateEvidence(protocol, "evidence-chain", "chain evidence");
        var record = ExtractionRecord.Create("record-chain", form.FormId, form.Digest, form.CandidateId,
            ExtractionRecordKind.Review, HumanOne, CreateValues(evidence, "include", "low"), Clock.UtcNow);
        journal.Append(record);

        Assert.AreEqual(ExtractionErrorCodes.InvalidChain,
            Assert.ThrowsExactly<ExtractionRuleException>(() => journal.Append(record)).Category);
        var duplicateId = ExtractionRecord.Create("record-chain", form.FormId, form.Digest, form.CandidateId,
            ExtractionRecordKind.Review, HumanTwo, CreateValues(evidence, "include", "low"), Clock.UtcNow);
        Assert.AreEqual(ExtractionErrorCodes.InvalidChain,
            Assert.ThrowsExactly<ExtractionRuleException>(() => journal.Append(duplicateId)).Category);
    }

    private static ExtractionForm CreateForm(VerifiedProtocolVersion protocol)
    {
        return ExtractionForm.Create(
            "form-1",
            "candidate-1",
            protocol,
            ["question-outcome", "question-risk"],
            [
                new ExtractionFieldDefinition("outcome", "question-outcome", "string"),
                new ExtractionFieldDefinition("risk", "question-risk", "string")
            ],
            HumanOne,
            Clock.UtcNow);
    }

    private static IReadOnlyList<ExtractionFieldValue> CreateValues(FullTextEvidenceLocation evidence, string outcome, string risk)
    {
        return new[]
        {
            new ExtractionFieldValue("outcome", CanonicalJsonValue.From(outcome), evidence),
            new ExtractionFieldValue("risk", CanonicalJsonValue.From(risk), evidence)
        };
    }

    private static FullTextEvidenceLocation CreateEvidence(VerifiedProtocolVersion protocol, string locationId, string excerpt)
    {
        var input = FullTextInput.FromScreeningDecision(
            "input-evidence-" + locationId,
            "candidate-set-1",
            protocol.Version.Id,
            "decision-id-" + locationId,
            "stage-1",
            FullTextScreeningVerdicts.Include,
            needsReviewRetrievable: true);
        var attempts = new List<FullTextSourceAttempt>
        {
            new(
                "attempt-" + locationId,
                "local-stub",
                1,
                FullTextAcquisitionKinds.DeterministicStubArtifact,
                FullTextAttemptStatuses.Success,
                artifactKind: FullTextArtifactKinds.Text,
                mediaType: "text/plain",
                artifactEvidenceId: "artifact-" + locationId)
        };

        var acquisition = new FullTextAcquisitionRecord(
            "acquisition-" + locationId,
            input,
            FullTextAcquisitionKinds.DeterministicStubArtifact,
            "local-stub",
            "stub://artifact/" + locationId,
            new FullTextActor(HumanOne.ActorId, FullTextActorKinds.Human),
            Clock.UtcNow,
            FullTextAttemptStatuses.Success,
            attempts,
            sourceUrl: "stub://artifact/" + locationId);

        var bytes = Encoding.UTF8.GetBytes("The protocol extracted this paragraph and this sentence includes " + excerpt);
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-" + locationId,
            input,
            acquisition,
            FullTextArtifactKinds.Text,
            "text/plain",
            bytes,
            1024);

        var chain = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024));
        var extraction = new FullTextExtractionRecord(
            "extraction-" + locationId,
            artifact.ArtifactId,
            artifact.RawByteDigest,
            artifact.RawByteDigestScope,
            "extractor-local",
            "1.0.0",
            Clock.UtcNow,
            "text",
            FullTextExtractionStatuses.Success,
            FullTextExtractionRecord.ComputeRepresentationDigest(FullTextExtractionRepresentations.PageText, new[] { excerpt }).ToString(),
            DigestScope.CanonicalJsonRecord.ToString(),
            pageText: new[] { excerpt },
            representationKind: FullTextExtractionRepresentations.PageText);
        var verifiedExtraction = FullTextExtractionRehydrator.Rehydrate(chain, extraction);

        return FullTextEvidenceLocation.Create(
            locationId,
            verifiedExtraction,
            FullTextEvidenceLocationKinds.Page,
            1,
            "page-1",
            excerpt);
    }

    private static VerifiedProtocolVersion CreateVerifiedProtocol()
    {
        var ids = new SequenceIdGenerator();
        var draft = ProtocolDraft.Create(ids, "extraction-protocol", ["question-outcome", "question-risk"]);
        draft.RecordDecision(ids, "question-outcome", CanonicalJsonValue.From("value required"), ProtocolHumanOne, Clock);
        draft.RecordDecision(ids, "question-risk", CanonicalJsonValue.From("value required"), ProtocolHumanTwo, Clock);
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = draft.CreateApprovalCandidate(ids, policy, versionNumber: 1, versionId: "protocol-version-1");
        var approval = ProtocolApproval.Create(ids, candidate, policy, ProtocolHumanOne, Clock, candidate.ContentDigest);
        return draft.ApproveCandidateVerified(candidate, policy, new[] { approval }, Clock);
    }

    private static VerifiedProtocolAmendment CreateVerifiedProtocolAmendment(VerifiedProtocolVersion protocol)
    {
        var ids = new SequenceIdGenerator();
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var notice = new ProtocolInvalidationNotice(
            "notice-1",
            "placeholder-amendment",
            "question-outcome",
            protocol.Version.ContentDigest,
            "node-1",
            "replace",
            "re-evaluate extraction",
            Clock.UtcNow);
        var amendment = ProtocolAmendment.Create(
            ids,
            protocol.Version,
            "protocol-version-2",
            ProtocolHumanOne,
            Clock,
            "Adjusts protocol wording",
            ["question-outcome"],
            [notice],
            policy);

        var producedSeed = new ProtocolVersion(
            amendment.ProducesVersionId, protocol.Version.ProtocolId, protocol.Version.ProjectId, protocol.Version.VersionNumber + 1,
            ProtocolStatus.Approved, protocol.Version.Template, protocol.Version.Intent, protocol.Version.Values,
            protocol.Version.RequiredDecisions, protocol.Version.Decisions, protocol.Version.Waivers,
            ContentDigest.Sha256Utf8("placeholder"), protocol.Version.ApprovalPolicyId, protocol.Version.ApprovalIds,
            Clock.UtcNow, protocol.Version.Id, amendmentId: amendment.AmendmentId, unresolvedDecisions: protocol.Version.UnresolvedDecisions);
        var producedVersion = new ProtocolVersion(
            producedSeed.Id, producedSeed.ProtocolId, producedSeed.ProjectId, producedSeed.VersionNumber, producedSeed.Status, producedSeed.Template,
            producedSeed.Intent, producedSeed.Values, producedSeed.RequiredDecisions, producedSeed.Decisions, producedSeed.Waivers,
            producedSeed.ToProtocolContentDigestEnvelope().ComputeDigest(), producedSeed.ApprovalPolicyId, producedSeed.ApprovalIds,
            producedSeed.ApprovedAt, producedSeed.SupersedesVersionId, producedSeed.SupersededByVersionId,
            producedSeed.AmendmentId, producedSeed.UnresolvedDecisions);
        var replacement = new VerifiedProtocolVersion(producedVersion, protocol.ApprovalPolicy, protocol.Approvals);

        var amendmentDigest = ContentDigest.Sha256CanonicalJson(amendment.ToCanonicalJson());
        return new VerifiedProtocolAmendment(
            amendment,
            amendmentDigest,
            policy,
            protocol,
            replacement,
            Array.Empty<VerifiedProtocolSupplementalApproval>());
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
