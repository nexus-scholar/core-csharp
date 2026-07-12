using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.FullText;
using NexusScholar.Kernel;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class FullTextTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Verified_chain_rehydrates_only_exact_input_acquisition_attempt_and_bytes()
    {
        var input = BuildInput("candidate-authority");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
        var bytes = Encoding.UTF8.GetBytes("authoritative full text");
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-authority", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);

        var verified = FullTextRehydrator.Rehydrate(
            new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024));

        Assert.AreSame(input, verified.Input);
        Assert.AreEqual(acquisition.AcquisitionId, verified.Acquisition.AcquisitionId);
        Assert.AreEqual(artifact.ArtifactId, verified.Artifact.ArtifactId);
    }

    [TestMethod]
    public void Full_text_authority_rejects_mismatched_input_and_persisted_artifact_binding()
    {
        var input = BuildInput("candidate-authority-a");
        var otherInput = BuildInput("candidate-authority-b");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
        var bytes = Encoding.UTF8.GetBytes("authoritative full text");

        Assert.AreEqual(
            FullTextErrorCodes.InvalidAuthorityChain,
            Assert.ThrowsExactly<FullTextRuleException>(() => FullTextArtifactEvidence.FromBytes(
                "artifact-mismatch", otherInput, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024)).Category);

        var artifact = new FullTextArtifactEvidence(
            "artifact-persisted", input, "different-candidate", acquisition.AcquisitionId,
            acquisition.AcquisitionKind, acquisition.SourceAlias, FullTextArtifactKinds.Text, "text/plain",
            bytes.LongLength, ContentDigest.Sha256(bytes).ToString(), DigestScope.RawArtifactBytes.ToString(),
            FullTextAttemptStatuses.Success, bytes);
        Assert.AreEqual(
            FullTextErrorCodes.InvalidAuthorityChain,
            Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
                new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024))).Category);
    }

    [TestMethod]
    public void Raw_authority_constructors_are_not_public_and_input_values_are_allowlisted()
    {
        Assert.AreEqual(0, typeof(FullTextInput).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
        Assert.AreEqual(0, typeof(FullTextAcquisitionRecord).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
        Assert.AreEqual(0, typeof(FullTextArtifactEvidence).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);

        Assert.ThrowsExactly<FullTextRuleException>(() => new FullTextInput(
            "input-invalid-source", "invented-source", "set", "candidate", FullTextEligibility.Retrievable));
        Assert.ThrowsExactly<FullTextRuleException>(() => new FullTextInput(
            "input-invalid-eligibility", FullTextSourceKinds.ScreeningHandoff, "set", "candidate", "invented-eligibility"));
    }

    [TestMethod]
    public void Verified_chain_derives_success_from_contiguous_attempt_history()
    {
        var input = BuildInput("candidate-state");
        var bytes = Encoding.UTF8.GetBytes("full text state");
        var acquisition = new FullTextAcquisitionRecord(
            "acquisition-state", input, FullTextAcquisitionKinds.ManualAcquisition, "manual", "source",
            FullTextActor(), FixedTime, FullTextAttemptStatuses.Success,
            [new FullTextSourceAttempt("attempt-state", "manual", 2, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success)]);
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-state", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);

        var error = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
            new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024)));

        Assert.AreEqual(FullTextErrorCodes.InvalidAcquisitionState, error.Category);
    }

    [TestMethod]
    public void Valid_screening_include_and_needs_review_inputs_are_accepted()
    {
        var include = FullTextInput.FromScreeningDecision(
            "fulltext-input-include",
            "candidate-set-001",
            "candidate-001",
            "screening-decision-include",
            "title_abstract",
            FullTextScreeningVerdicts.Include);

        var needsReview = FullTextInput.FromScreeningDecision(
            "fulltext-input-review",
            "candidate-set-001",
            "candidate-002",
            "screening-decision-review",
            "title_abstract",
            FullTextScreeningVerdicts.NeedsReview);

        Assert.AreEqual(FullTextSchemas.InputSchemaId, include.SchemaId);
        Assert.AreEqual(FullTextEligibility.Retrievable, include.Eligibility);
        Assert.AreEqual(FullTextEligibility.ReviewableRetrievable, needsReview.Eligibility);
    }

    [TestMethod]
    public void Raw_search_trace_and_raw_dedup_member_inputs_are_rejected()
    {
        var rawSearch = Assert.ThrowsExactly<FullTextRuleException>(() =>
            new FullTextInput(
                "fulltext-input-search",
                FullTextSourceKinds.RawSearchTrace,
                "candidate-set-001",
                "candidate-001",
                FullTextEligibility.Retrievable));

        var rawDedup = Assert.ThrowsExactly<FullTextRuleException>(() =>
            new FullTextInput(
                "fulltext-input-dedup",
                FullTextSourceKinds.RawDedupMember,
                "candidate-set-001",
                "candidate-001",
                FullTextEligibility.Retrievable));

        Assert.AreEqual(FullTextErrorCodes.RawSearchTraceNotFullTextInput, rawSearch.Category);
        Assert.AreEqual(FullTextErrorCodes.RawDedupRecordNotFullTextInput, rawDedup.Category);
    }

    [TestMethod]
    public void Final_exclude_is_not_retrievable_by_default()
    {
        var error = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextInput.FromScreeningDecision(
                "fulltext-input-exclude",
                "candidate-set-001",
                "candidate-001",
                "screening-decision-exclude",
                "title_abstract",
                FullTextScreeningVerdicts.Exclude));

        Assert.AreEqual(FullTextErrorCodes.ExcludedCandidateNotRetrievable, error.Category);
    }

    [TestMethod]
    public void User_supplied_bytes_produce_raw_artifact_bytes_digest()
    {
        var input = BuildInput("candidate-pdf");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.UserSuppliedLocalFile);
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nbody");

        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-pdf-001",
            input,
            acquisition,
            FullTextArtifactKinds.Pdf,
            "application/pdf",
            bytes,
            maxBytes: 128,
            logicalPath: "fulltext/artifact-pdf-001.pdf",
            originalFileName: "paper.pdf");

        Assert.AreEqual(ContentDigest.Sha256(bytes).ToString(), artifact.RawByteDigest);
        Assert.AreEqual(DigestScope.RawArtifactBytes.ToString(), artifact.RawByteDigestScope);
        Assert.AreEqual("fulltext/artifact-pdf-001.pdf", artifact.LogicalPath);
        Assert.AreEqual("candidate-pdf", artifact.CandidateId);
    }

    [TestMethod]
    public void Missing_wrong_scope_and_mismatched_digests_are_rejected_by_category()
    {
        var input = BuildInput("candidate-digest");
        var bytes = Encoding.UTF8.GetBytes("full text");
        var goodDigest = ContentDigest.Sha256(bytes).ToString();
        var otherDigest = ContentDigest.Sha256Utf8("other").ToString();

        Assert.AreEqual(
            FullTextErrorCodes.MissingRawArtifactDigest,
            Assert.ThrowsExactly<FullTextRuleException>(() => BuildArtifact(input, bytes, string.Empty, DigestScope.RawArtifactBytes.ToString())).Category);

        Assert.AreEqual(
            FullTextErrorCodes.InvalidRawArtifactDigestScope,
            Assert.ThrowsExactly<FullTextRuleException>(() => BuildArtifact(input, bytes, goodDigest, DigestScope.CanonicalJsonRecord.ToString())).Category);

        Assert.AreEqual(
            FullTextErrorCodes.RawArtifactDigestMismatch,
            Assert.ThrowsExactly<FullTextRuleException>(() => BuildArtifact(input, bytes, otherDigest, DigestScope.RawArtifactBytes.ToString())).Category);
    }

    [TestMethod]
    public void Local_paths_and_app_projection_ids_are_not_artifact_identity()
    {
        var localPath = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextArtifactEvidence.RejectArtifactIdentityProjection("local_path"));
        var appProjection = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextArtifactEvidence.RejectArtifactIdentityProjection("project_full_text_item_id"));

        Assert.AreEqual(FullTextErrorCodes.LocalPathNotArtifactIdentity, localPath.Category);
        Assert.AreEqual(FullTextErrorCodes.AppProjectionNotCoreAuthority, appProjection.Category);
    }

    [TestMethod]
    public void Pdf_xml_and_text_validators_classify_errors_correctly()
    {
        Assert.AreEqual(
            FullTextErrorCodes.InvalidPdfSignature,
            Assert.ThrowsExactly<FullTextRuleException>(() =>
                FullTextArtifactValidator.Validate(FullTextArtifactKinds.Pdf, Encoding.ASCII.GetBytes("not-pdf"), 32, "application/pdf")).Category);

        Assert.AreEqual(
            FullTextErrorCodes.HtmlNotFullTextXml,
            Assert.ThrowsExactly<FullTextRuleException>(() =>
                FullTextArtifactValidator.Validate(FullTextArtifactKinds.Xml, Encoding.UTF8.GetBytes("<html><body>x</body></html>"), 64, "application/xml")).Category);

        Assert.AreEqual(
            FullTextErrorCodes.EmptyTextArtifact,
            Assert.ThrowsExactly<FullTextRuleException>(() =>
                FullTextArtifactValidator.Validate(FullTextArtifactKinds.Text, Encoding.UTF8.GetBytes("   "), 64, "text/plain")).Category);

        Assert.AreEqual(
            FullTextErrorCodes.ArtifactTooLarge,
            Assert.ThrowsExactly<FullTextRuleException>(() =>
                FullTextArtifactValidator.Validate(FullTextArtifactKinds.Text, Encoding.UTF8.GetBytes("large"), 2, "text/plain")).Category);

        Assert.AreEqual(
            FullTextErrorCodes.InvalidMediaType,
            Assert.ThrowsExactly<FullTextRuleException>(() =>
                FullTextArtifactValidator.Validate(FullTextArtifactKinds.Pdf, Encoding.ASCII.GetBytes("%PDF-1.7"), 64, "text/html")).Category);
    }

    [TestMethod]
    public void Source_attempts_preserve_failures_skips_and_later_success()
    {
        var input = BuildInput("candidate-attempts");
        var acquisition = new FullTextAcquisitionRecord(
            "acquisition-attempts",
            input,
            FullTextAcquisitionKinds.ManualAcquisition,
            "manual",
            "operator-notes",
            FullTextActor(),
            FixedTime,
            FullTextAttemptStatuses.Success,
            new[]
            {
                new FullTextSourceAttempt("attempt-1", "source-reference-a", 1, FullTextAcquisitionKinds.OpenAccessSourceReference, FullTextAttemptStatuses.Failure, errorCategory: FullTextErrorCodes.InaccessibleFullText),
                new FullTextSourceAttempt("attempt-2", "source-reference-b", 2, FullTextAcquisitionKinds.OpenAccessSourceReference, FullTextAttemptStatuses.Skipped, errorCategory: FullTextErrorCodes.MissingFullText),
                new FullTextSourceAttempt("attempt-3", "manual", 3, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success, artifactEvidenceId: "artifact-attempts")
            });

        CollectionAssert.AreEqual(
            new[] { FullTextAttemptStatuses.Failure, FullTextAttemptStatuses.Skipped, FullTextAttemptStatuses.Success },
            acquisition.SourceAttempts.Select(attempt => attempt.Status).ToArray());
    }

    [TestMethod]
    public void Duplicate_artifact_digest_does_not_merge_candidates()
    {
        var bytes = Encoding.UTF8.GetBytes("same accepted text bytes");
        var first = BuildArtifactEvidence("artifact-1", "candidate-1", bytes);
        var second = BuildArtifactEvidence("artifact-2", "candidate-2", bytes);

        var duplicates = FullTextDuplicatePolicy.FindDuplicateArtifacts(new[] { first, second });

        Assert.AreEqual(1, duplicates.Count);
        Assert.AreEqual(FullTextErrorCodes.DuplicateArtifact, duplicates[0].Category);
        CollectionAssert.AreEqual(new[] { "candidate-1", "candidate-2" }, duplicates[0].CandidateIds.ToArray());
    }

    [TestMethod]
    public void Derived_extraction_binds_to_source_artifact_id_and_raw_digest()
    {
        var sourceDigest = ContentDigest.Sha256Utf8("source-pdf").ToString();
        var extraction = new FullTextExtractionRecord(
            "extraction-001",
            "artifact-source-001",
            sourceDigest,
            DigestScope.RawArtifactBytes.ToString(),
            "deterministic-stub-extractor",
            "1.0.0",
            FixedTime,
            "user-supplied-derived-text",
            FullTextExtractionStatuses.Success,
            extractedTextDigest: ContentDigest.Sha256Utf8("extracted text").ToString(),
            extractedTextDigestScope: DigestScope.RawArtifactBytes.ToString(),
            pageText: ["extracted text"]);

        Assert.AreEqual("artifact-source-001", extraction.SourceArtifactId);
        Assert.AreEqual(sourceDigest, extraction.SourceRawByteDigest);
        Assert.AreEqual(DigestScope.RawArtifactBytes.ToString(), extraction.SourceRawByteDigestScope);

        Assert.AreEqual(
            FullTextErrorCodes.DerivedTextMissingSourceDigest,
            Assert.ThrowsExactly<FullTextRuleException>(() =>
                new FullTextExtractionRecord(
                    "extraction-bad",
                    "artifact-source-001",
                    "sha256:not-a-digest",
                    DigestScope.RawArtifactBytes.ToString(),
                    "deterministic-stub-extractor",
                    "1.0.0",
                    FixedTime,
                    "user-supplied-derived-text",
                    FullTextExtractionStatuses.Success)).Category);
    }

    [TestMethod]
    public void Partial_extraction_requires_warning_category()
    {
        var sourceDigest = ContentDigest.Sha256Utf8("source-pdf").ToString();
        var extraction = new FullTextExtractionRecord(
            "extraction-partial",
            "artifact-source-001",
            sourceDigest,
            DigestScope.RawArtifactBytes.ToString(),
            "deterministic-stub-extractor",
            "1.0.0",
            FixedTime,
            "user-supplied-derived-text",
            FullTextExtractionStatuses.Partial,
            warnings: [FullTextErrorCodes.PartialExtraction]);

        Assert.IsTrue(extraction.Warnings.Contains(FullTextErrorCodes.PartialExtraction));

        Assert.AreEqual(
            FullTextErrorCodes.PartialExtraction,
            Assert.ThrowsExactly<FullTextRuleException>(() =>
                new FullTextExtractionRecord(
                    "extraction-partial-bad",
                    "artifact-source-001",
                    sourceDigest,
                    DigestScope.RawArtifactBytes.ToString(),
                    "deterministic-stub-extractor",
                    "1.0.0",
                    FixedTime,
                    "user-supplied-derived-text",
                    FullTextExtractionStatuses.Partial)).Category);
    }

    private static FullTextInput BuildInput(string candidateId)
    {
        return FullTextInput.FromScreeningDecision(
            $"input-{candidateId}",
            "candidate-set-001",
            candidateId,
            $"screening-decision-{candidateId}",
            "title_abstract",
            FullTextScreeningVerdicts.Include,
            dedupResultId: "dedup-result-001",
            dedupClusterId: $"cluster-{candidateId}",
            workId: $"doi:10.1000/{candidateId}");
    }

    private static FullTextAcquisitionRecord BuildAcquisition(FullTextInput input, string acquisitionKind)
    {
        return new FullTextAcquisitionRecord(
            $"acquisition-{input.CandidateId}",
            input,
            acquisitionKind,
            "manual",
            "operator-supplied-bytes",
            FullTextActor(),
            FixedTime,
            FullTextAttemptStatuses.Success,
            [new FullTextSourceAttempt("attempt-manual", "manual", 1, acquisitionKind, FullTextAttemptStatuses.Success)]);
    }

    private static FullTextActor FullTextActor() => new("human-fulltext-1", "human");

    private static FullTextArtifactEvidence BuildArtifact(
        FullTextInput input,
        byte[] bytes,
        string rawByteDigest,
        string rawByteDigestScope)
    {
        return new FullTextArtifactEvidence(
            "artifact-digest",
            input,
            input.CandidateId,
            "acquisition-digest",
            FullTextAcquisitionKinds.ManualAcquisition,
            "manual",
            FullTextArtifactKinds.Text,
            "text/plain",
            bytes.LongLength,
            rawByteDigest,
            rawByteDigestScope,
            FullTextAttemptStatuses.Success,
            bytes);
    }

    private static FullTextArtifactEvidence BuildArtifactEvidence(string artifactId, string candidateId, byte[] bytes)
    {
        var input = BuildInput(candidateId);
        return FullTextArtifactEvidence.FromBytes(
            artifactId,
            input,
            BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition),
            FullTextArtifactKinds.Text,
            "text/plain",
            bytes,
            maxBytes: 256);
    }
}
