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
    public void Full_text_authority_rejects_every_mutated_input_linkage_field()
    {
        var input = BuildInput("candidate-input-linkage");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
        var bytes = Encoding.UTF8.GetBytes("input linkage evidence");
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-input-linkage", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);

        foreach (var mutation in new[]
        {
            "input-id", "source-kind", "candidate-set-id", "candidate-id", "eligibility",
            "screening-decision-id", "screening-stage", "dedup-result-id", "dedup-cluster-id",
            "work-id", "source-refs", "non-claims"
        })
        {
            var mutatedInput = MutateInput(input, mutation);
            var acquisitionError = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
                new UnverifiedFullTextChain(input, CloneAcquisition(acquisition, mutatedInput), artifact, bytes, 1024)), mutation);
            var artifactError = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
                new UnverifiedFullTextChain(input, acquisition, CloneArtifact(artifact, bytes, inputRef: mutatedInput), bytes, 1024)), mutation);

            Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, acquisitionError.Category, mutation);
            Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, artifactError.Category, mutation);
        }
    }

    [TestMethod]
    public void Full_text_authority_rejects_mutated_artifact_level_linkage()
    {
        var input = BuildInput("candidate-artifact-linkage");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
        var bytes = Encoding.UTF8.GetBytes("artifact linkage evidence");
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-linkage", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);

        foreach (var mutation in new[]
        {
            "candidate-id", "candidate-set-id", "screening-decision-id", "work-id", "dedup-cluster-id",
            "acquisition-id", "acquisition-kind", "source-alias"
        })
        {
            var error = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
                new UnverifiedFullTextChain(input, acquisition, CloneArtifact(artifact, bytes, mutation), bytes, 1024)), mutation);

            Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, error.Category, mutation);
        }

        var acquisitionLinkError = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
            new UnverifiedFullTextChain(
                input,
                CloneAcquisition(acquisition, input, "different-artifact"),
                artifact,
                bytes,
                1024)));
        Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, acquisitionLinkError.Category);
    }

    [TestMethod]
    public void Full_text_authority_preserves_and_rejects_omitted_artifact_linkage()
    {
        var input = BuildInput("candidate-omitted-artifact-linkage");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
        var bytes = Encoding.UTF8.GetBytes("omitted artifact linkage");
        var artifact = new FullTextArtifactEvidence(
            "artifact-omitted-linkage",
            input,
            input.CandidateId,
            acquisition.AcquisitionId,
            acquisition.AcquisitionKind,
            acquisition.SourceAlias,
            FullTextArtifactKinds.Text,
            "text/plain",
            bytes.LongLength,
            ContentDigest.Sha256(bytes).ToString(),
            DigestScope.RawArtifactBytes.ToString(),
            FullTextAttemptStatuses.Success,
            bytes);

        Assert.IsNull(artifact.CandidateSetId);
        Assert.IsNull(artifact.ScreeningDecisionId);
        Assert.IsNull(artifact.WorkId);
        Assert.IsNull(artifact.DedupClusterId);
        var error = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
            new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024)));
        Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, error.Category);
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
    public void Source_attempts_preserve_failures_skips_and_rehydrate_first_later_success()
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

        var bytes = Encoding.UTF8.GetBytes("mixed source success");
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-attempts", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);
        var verified = FullTextRehydrator.Rehydrate(
            new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024));

        Assert.AreSame(acquisition, verified.Acquisition);
    }

    [TestMethod]
    public void Source_attempts_accept_first_success_with_later_skip_and_reject_a_second_success()
    {
        var input = BuildInput("candidate-invalid-attempts");
        var bytes = Encoding.UTF8.GetBytes("invalid attempt evidence");
        var firstSuccess = new FullTextAcquisitionRecord(
            "acquisition-first-success",
            input,
            FullTextAcquisitionKinds.ManualAcquisition,
            "manual",
            "operator-notes",
            FullTextActor(),
            FixedTime,
            FullTextAttemptStatuses.Success,
            [
                new FullTextSourceAttempt("attempt-1", "open-access", 1, FullTextAcquisitionKinds.OpenAccessSourceReference, FullTextAttemptStatuses.Failure),
                new FullTextSourceAttempt("attempt-2", "manual", 2, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success),
                new FullTextSourceAttempt("attempt-3", "later-source", 3, FullTextAcquisitionKinds.OpenAccessSourceReference, FullTextAttemptStatuses.Skipped)
            ]);
        var firstArtifact = FullTextArtifactEvidence.FromBytes(
            "artifact-first-success", input, firstSuccess, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);

        var verified = FullTextRehydrator.Rehydrate(
            new UnverifiedFullTextChain(input, firstSuccess, firstArtifact, bytes, 1024));
        Assert.AreSame(firstSuccess, verified.Acquisition);

        var secondSuccess = new FullTextAcquisitionRecord(
            "acquisition-second-success",
            input,
            FullTextAcquisitionKinds.ManualAcquisition,
            "manual",
            "operator-notes",
            FullTextActor(),
            FixedTime,
            FullTextAttemptStatuses.Success,
            [
                new FullTextSourceAttempt("attempt-1", "manual", 1, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success),
                new FullTextSourceAttempt("attempt-2", "manual", 2, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success)
            ]);
        var secondArtifact = FullTextArtifactEvidence.FromBytes(
            "artifact-second-success", input, secondSuccess, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);

        var secondError = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
            new UnverifiedFullTextChain(input, secondSuccess, secondArtifact, bytes, 1024)));
        Assert.AreEqual(FullTextErrorCodes.InvalidAcquisitionState, secondError.Category);
    }

    [TestMethod]
    public void Source_attempts_reject_mismatched_accepted_attempt_bindings()
    {
        var input = BuildInput("candidate-attempt-binding");
        var bytes = Encoding.UTF8.GetBytes("attempt binding evidence");
        foreach (var mutation in new[]
        {
            "source-alias", "acquisition-kind", "artifact-evidence-id", "non-success-artifact-id",
            "artifact-kind", "media-type"
        })
        {
            FullTextSourceAttempt[] attempts = mutation switch
            {
                "non-success-artifact-id" =>
                [
                    new FullTextSourceAttempt("attempt-1", "open-access", 1, FullTextAcquisitionKinds.OpenAccessSourceReference, FullTextAttemptStatuses.Failure, artifactEvidenceId: "failed-artifact"),
                    new FullTextSourceAttempt("attempt-2", "manual", 2, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success)
                ],
                _ => new[]
                {
                    new FullTextSourceAttempt(
                        "attempt-1",
                        mutation == "source-alias" ? "different-source" : "manual",
                        1,
                        mutation == "acquisition-kind" ? FullTextAcquisitionKinds.OpenAccessSourceReference : FullTextAcquisitionKinds.ManualAcquisition,
                        FullTextAttemptStatuses.Success,
                        artifactKind: mutation == "artifact-kind" ? FullTextArtifactKinds.Pdf : null,
                        mediaType: mutation == "media-type" ? "application/pdf" : null,
                        artifactEvidenceId: mutation == "artifact-evidence-id" ? "different-artifact" : null)
                }
            };
            var acquisition = new FullTextAcquisitionRecord(
                $"acquisition-{mutation}", input, FullTextAcquisitionKinds.ManualAcquisition, "manual", "operator-notes",
                FullTextActor(), FixedTime, FullTextAttemptStatuses.Success, attempts);
            var artifact = FullTextArtifactEvidence.FromBytes(
                $"artifact-{mutation}", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);

            var error = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
                new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024)), mutation);
            Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, error.Category, mutation);
        }
    }

    [TestMethod]
    public void Source_attempts_reject_mismatched_accepted_source()
    {
        var input = BuildInput("candidate-invalid-source");
        var bytes = Encoding.UTF8.GetBytes("invalid source evidence");

        var wrongFinalSource = new FullTextAcquisitionRecord(
            "acquisition-wrong-final-source",
            input,
            FullTextAcquisitionKinds.ManualAcquisition,
            "manual",
            "operator-notes",
            FullTextActor(),
            FixedTime,
            FullTextAttemptStatuses.Success,
            [new FullTextSourceAttempt("attempt-1", "different-source", 1, FullTextAcquisitionKinds.ManualAcquisition, FullTextAttemptStatuses.Success)]);
        var wrongSourceArtifact = FullTextArtifactEvidence.FromBytes(
            "artifact-wrong-final-source", input, wrongFinalSource, FullTextArtifactKinds.Text, "text/plain", bytes, 1024);

        var sourceError = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextRehydrator.Rehydrate(
            new UnverifiedFullTextChain(input, wrongFinalSource, wrongSourceArtifact, bytes, 1024)));
        Assert.AreEqual(FullTextErrorCodes.InvalidAuthorityChain, sourceError.Category);
    }

    [TestMethod]
    public void Manual_and_user_supplied_acquisitions_require_human_or_import_actor_kind()
    {
        var input = BuildInput("candidate-actor-kind");
        var actorRequiredKinds = new[]
        {
            FullTextAcquisitionKinds.UserUploadedFile,
            FullTextAcquisitionKinds.UserSuppliedLocalFile,
            FullTextAcquisitionKinds.ManualAcquisition
        };
        foreach (var acquisitionKind in actorRequiredKinds)
        {
            foreach (var actorKind in new[] { "automation", "plugin", "system", "unknown" })
            {
                var error = Assert.ThrowsExactly<FullTextRuleException>(() => new FullTextAcquisitionRecord(
                    $"acquisition-{acquisitionKind}-{actorKind}", input, acquisitionKind, "manual", "operator-notes",
                    new FullTextActor("actor-1", actorKind), FixedTime, FullTextAttemptStatuses.Success,
                    [new FullTextSourceAttempt("attempt-1", "manual", 1, acquisitionKind, FullTextAttemptStatuses.Success)]));

                Assert.AreEqual(FullTextErrorCodes.MissingHumanOrImportActor, error.Category, $"{acquisitionKind}:{actorKind}");
            }
            foreach (var actorKind in new[] { FullTextActorKinds.Human, FullTextActorKinds.Import })
            {
                var acquisition = new FullTextAcquisitionRecord(
                    $"acquisition-{acquisitionKind}-{actorKind}", input, acquisitionKind, "manual", "operator-notes",
                    new FullTextActor("actor-1", actorKind), FixedTime, FullTextAttemptStatuses.Success,
                    [new FullTextSourceAttempt("attempt-1", "manual", 1, acquisitionKind, FullTextAttemptStatuses.Success)]);

                Assert.AreEqual(actorKind, acquisition.AcquiredBy!.ActorKind);
            }
        }
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
        var pageText = new[] { "extracted text" };
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
            extractedTextDigest: FullTextExtractionRecord.ComputeRepresentationDigest(FullTextExtractionRepresentations.PageText, pageText).ToString(),
            extractedTextDigestScope: DigestScope.CanonicalJsonRecord.ToString(),
            pageText: pageText,
            representationKind: FullTextExtractionRepresentations.PageText);

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
        var partialText = new[] { "partial text" };
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
            extractedTextDigest: FullTextExtractionRecord.ComputeRepresentationDigest(FullTextExtractionRepresentations.PageText, partialText).ToString(),
            extractedTextDigestScope: DigestScope.CanonicalJsonRecord.ToString(),
            pageText: partialText,
            warnings: [FullTextErrorCodes.PartialExtraction],
            representationKind: FullTextExtractionRepresentations.PageText);

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

    [TestMethod]
    public void Full_text_paths_and_extraction_source_representation_are_verified()
    {
        var input = BuildInput("candidate-extraction-chain");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
        var bytes = Encoding.UTF8.GetBytes("source artifact text");
        Assert.AreEqual(
            FullTextErrorCodes.InvalidLogicalPath,
            Assert.ThrowsExactly<FullTextRuleException>(() => FullTextArtifactEvidence.FromBytes(
                "artifact-bad-path", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024,
                logicalPath: "../outside.txt")).Category);

        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-extraction-chain", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 1024,
            logicalPath: "fulltext/source.txt");
        var chain = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 1024));
        var pageText = new[] { "page one" };
        var extraction = new FullTextExtractionRecord(
            "extraction-chain", artifact.ArtifactId, artifact.RawByteDigest, artifact.RawByteDigestScope,
            "extractor", "1.0.0", FixedTime, "derived-text", FullTextExtractionStatuses.Success,
            FullTextExtractionRecord.ComputeRepresentationDigest(FullTextExtractionRepresentations.PageText, pageText).ToString(),
            DigestScope.CanonicalJsonRecord.ToString(), pageText: pageText,
            representationKind: FullTextExtractionRepresentations.PageText);

        Assert.AreSame(extraction, FullTextExtractionRehydrator.Rehydrate(chain, extraction).Record);
        var mismatch = new FullTextExtractionRecord(
            "extraction-mismatch", "other-artifact", artifact.RawByteDigest, artifact.RawByteDigestScope,
            "extractor", "1.0.0", FixedTime, "derived-text", FullTextExtractionStatuses.Success,
            extraction.ExtractedTextDigest, extraction.ExtractedTextDigestScope, pageText: pageText,
            representationKind: FullTextExtractionRepresentations.PageText);
        Assert.AreEqual(
            FullTextErrorCodes.ExtractionSourceMismatch,
            Assert.ThrowsExactly<FullTextRuleException>(() => FullTextExtractionRehydrator.Rehydrate(chain, mismatch)).Category);

        Assert.AreEqual(
            FullTextErrorCodes.InvalidExtractionRepresentation,
            Assert.ThrowsExactly<FullTextRuleException>(() => new FullTextExtractionRecord(
                "extraction-dual", artifact.ArtifactId, artifact.RawByteDigest, artifact.RawByteDigestScope,
                "extractor", "1.0.0", FixedTime, "derived-text", FullTextExtractionStatuses.Success,
                extraction.ExtractedTextDigest, extraction.ExtractedTextDigestScope,
                pageText: pageText, sections: new[] { "section one" },
                representationKind: FullTextExtractionRepresentations.PageText)).Category);
    }

    [TestMethod]
    public void Deterministic_text_and_xml_extraction_attempts_round_trip_canonically()
    {
        foreach (var sample in new[]
        {
            (Kind: FullTextArtifactKinds.Text, MediaType: "text/plain", Bytes: Encoding.UTF8.GetBytes("accepted full text"), Expected: "accepted full text"),
            (Kind: FullTextArtifactKinds.Xml, MediaType: "application/xml", Bytes: Encoding.UTF8.GetBytes("<article><title>Study</title><p>Result</p></article>"), Expected: "StudyResult")
        })
        {
            var input = BuildInput($"candidate-extract-{sample.Kind}");
            var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
            var artifact = FullTextArtifactEvidence.FromBytes(
                $"artifact-extract-{sample.Kind}", input, acquisition, sample.Kind, sample.MediaType, sample.Bytes, 4096);
            var source = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(input, acquisition, artifact, sample.Bytes, 4096));

            var attempt = FullTextDeterministicExtractor.Extract(
                $"attempt-{sample.Kind}", source, sample.Bytes, FixedTime);
            var reopened = FullTextExtractionAttemptCodec.Rehydrate(
                FullTextExtractionAttemptCodec.Serialize(attempt), attempt.Digest, source);

            Assert.AreEqual(FullTextExtractionAttemptStatuses.Success, reopened.Status);
            Assert.AreEqual(sample.Expected, reopened.Values.Single());
            Assert.AreEqual(attempt.OutputDigest, reopened.OutputDigest);
            Assert.AreEqual(attempt.Configuration.Digest, reopened.Configuration.Digest);
        }
    }

    [TestMethod]
    public void Pdf_extraction_is_explicitly_unsupported_without_losing_raw_authority()
    {
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nbody");
        var input = BuildInput("candidate-pdf-unsupported");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-pdf-unsupported", input, acquisition, FullTextArtifactKinds.Pdf, "application/pdf", bytes, 4096);
        var source = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 4096));

        var attempt = FullTextDeterministicExtractor.Extract("attempt-pdf", source, bytes, FixedTime);

        Assert.AreEqual(FullTextExtractionAttemptStatuses.Unsupported, attempt.Status);
        Assert.AreEqual(FullTextErrorCodes.UnsupportedFileType, attempt.FailureCategory);
        Assert.AreEqual(0, attempt.Values.Count);
        Assert.AreEqual(artifact.RawByteDigest, source.Artifact.RawByteDigest);
    }

    [TestMethod]
    public void Extraction_attempts_reject_source_tamper_invalid_partial_and_noncanonical_bytes()
    {
        var bytes = Encoding.UTF8.GetBytes("source text");
        var input = BuildInput("candidate-extraction-negative");
        var acquisition = BuildAcquisition(input, FullTextAcquisitionKinds.ManualAcquisition);
        var artifact = FullTextArtifactEvidence.FromBytes(
            "artifact-extraction-negative", input, acquisition, FullTextArtifactKinds.Text, "text/plain", bytes, 4096);
        var source = FullTextRehydrator.Rehydrate(new UnverifiedFullTextChain(input, acquisition, artifact, bytes, 4096));
        var configuration = FullTextExtractionConfiguration.Create(
            "extractor", "1.0.0", FullTextExtractionRepresentations.PageText);

        Assert.AreEqual(
            FullTextErrorCodes.ExtractionSourceMismatch,
            Assert.ThrowsExactly<FullTextRuleException>(() => FullTextDeterministicExtractor.Extract(
                "attempt-tamper", source, Encoding.UTF8.GetBytes("changed"), FixedTime)).Category);
        Assert.AreEqual(
            FullTextErrorCodes.PartialExtraction,
            Assert.ThrowsExactly<FullTextRuleException>(() => FullTextExtractionAttempt.Create(
                "attempt-partial", source, configuration, FixedTime, FullTextExtractionAttemptStatuses.Partial,
                ["partial"])).Category);
        Assert.AreEqual(
            FullTextErrorCodes.ExtractionFailure,
            Assert.ThrowsExactly<FullTextRuleException>(() => FullTextExtractionAttempt.Create(
                "attempt-failure", source, configuration, FixedTime, FullTextExtractionAttemptStatuses.Failure)).Category);

        var valid = FullTextDeterministicExtractor.Extract("attempt-valid", source, bytes, FixedTime);
        var noncanonical = FullTextExtractionAttemptCodec.Serialize(valid).Concat([(byte)'\n']).ToArray();
        Assert.AreEqual(
            FullTextErrorCodes.InvalidAuthorityChain,
            Assert.ThrowsExactly<FullTextRuleException>(() => FullTextExtractionAttemptCodec.Rehydrate(
                noncanonical, valid.Digest, source)).Category);
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

    private static FullTextInput MutateInput(FullTextInput input, string mutation) => new(
        mutation == "input-id" ? "mutated-input" : input.InputId,
        mutation == "source-kind" ? FullTextSourceKinds.LockedReviewableCandidateSet : input.SourceKind,
        mutation == "candidate-set-id" ? "mutated-candidate-set" : input.CandidateSetId,
        mutation == "candidate-id" ? "mutated-candidate" : input.CandidateId,
        mutation == "eligibility" ? FullTextEligibility.ReviewableRetrievable : input.Eligibility,
        mutation == "source-refs" ? [new FullTextSourceRef("screening", "mutated-ref")] : input.SourceRefs,
        mutation == "screening-decision-id" ? "mutated-screening-decision" : input.ScreeningDecisionId,
        mutation == "screening-stage" ? "mutated-screening-stage" : input.ScreeningStage,
        mutation == "dedup-result-id" ? "mutated-dedup-result" : input.DedupResultId,
        mutation == "dedup-cluster-id" ? "mutated-dedup-cluster" : input.DedupClusterId,
        mutation == "work-id" ? "doi:mutated" : input.WorkId,
        mutation == "non-claims" ? ["mutated-non-claim"] : input.NonClaims);

    private static FullTextAcquisitionRecord CloneAcquisition(
        FullTextAcquisitionRecord source,
        FullTextInput inputRef,
        string? artifactEvidenceId = null) => new(
            source.AcquisitionId,
            inputRef,
            source.AcquisitionKind,
            source.SourceAlias,
            source.SourceReference,
            source.AcquiredBy,
            source.AcquiredAt,
            source.Status,
            source.SourceAttempts,
            source.SourceUrl,
            source.DoiOrLandingPage,
            source.SourceMetadata,
            artifactEvidenceId ?? source.ArtifactEvidenceId,
            source.Warnings,
            source.Errors,
            source.NonClaims);

    private static FullTextArtifactEvidence CloneArtifact(
        FullTextArtifactEvidence source,
        byte[] bytes,
        string? mutation = null,
        FullTextInput? inputRef = null) => new(
            source.ArtifactId,
            inputRef ?? source.InputRef,
            mutation == "candidate-id" ? "mutated-candidate" : source.CandidateId,
            mutation == "acquisition-id" ? "mutated-acquisition" : source.AcquisitionId,
            mutation == "acquisition-kind" ? FullTextAcquisitionKinds.OpenAccessSourceReference : source.AcquisitionKind,
            mutation == "source-alias" ? "mutated-source" : source.SourceAlias,
            source.ArtifactKind,
            source.MediaType,
            source.SizeBytes,
            source.RawByteDigest,
            source.RawByteDigestScope,
            source.ValidationStatus,
            bytes,
            mutation == "candidate-set-id" ? "mutated-candidate-set" : source.CandidateSetId,
            mutation == "screening-decision-id" ? "mutated-screening-decision" : source.ScreeningDecisionId,
            mutation == "work-id" ? "doi:mutated" : source.WorkId,
            mutation == "dedup-cluster-id" ? "mutated-dedup-cluster" : source.DedupClusterId,
            source.SourceReference,
            source.SourceMetadata,
            source.LogicalPath,
            source.OriginalFileName,
            source.Warnings,
            source.Errors,
            source.NonClaims);

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
