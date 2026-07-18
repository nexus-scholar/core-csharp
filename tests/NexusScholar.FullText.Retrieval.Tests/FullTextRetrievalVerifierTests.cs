using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.FullText;
using NexusScholar.Kernel;

namespace NexusScholar.FullText.Retrieval.Tests;

[TestClass]
public sealed class FullTextRetrievalVerifierTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Verify_Recorded_retrieval_successes_for_admitted_rights_and_exact_bytes()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096, "retrieval-fixture-local");
        var input = BuildInput("candidate-success");
        var bytes = Encoding.UTF8.GetBytes("recorded textual full text");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-1",
            input,
            "open-source-provider",
            "https://api.nexus.test/articles/1",
            FullTextRetrievalAccessRoutes.LandingPage,
            FullTextRetrievalRights.OpenAccess,
            "https://rights.nexus.test/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime,
            responseComplete: true,
            retentionDisposition: "retrieval-fixture-local");

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-1",
            "attempt-1",
            "artifact-1");

        Assert.IsTrue(outcome.IsSuccess);
        Assert.IsNotNull(outcome.Chain);
        Assert.IsNotNull(outcome.Artifact);
        Assert.AreEqual(FullTextAttemptStatuses.Success, outcome.Acquisition.Status);
        Assert.AreEqual(FullTextAttemptStatuses.Success, outcome.SourceAttempt.Status);
        Assert.AreEqual("artifact-1", outcome.Artifact.ArtifactId);
        Assert.AreSame(outcome.Artifact!, outcome.Chain!.Artifact);
        Assert.AreEqual(input.CandidateId, outcome.Chain!.Artifact.CandidateId);
        Assert.AreEqual("api.nexus.test", outcome.Acquisition.SourceReference.Split('/')[2]);
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_closed_rights()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-closed");
        var bytes = Encoding.UTF8.GetBytes("closed");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-2",
            input,
            "closed-provider",
            "https://api.nexus.test/articles/2",
            FullTextRetrievalAccessRoutes.Repository,
            "closed",
            "https://rights.nexus.test/closed",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-2",
            "attempt-2",
            "artifact-2");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.IsNull(outcome.Chain);
        Assert.AreEqual(FullTextRetrievalErrorCodes.RightsNotAdmitted, outcome.FailureCategory);
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_invalid_redirect_host()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-redirect");
        var bytes = Encoding.UTF8.GetBytes("redirect");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-3",
            input,
            "redirect-provider",
            "https://api.nexus.test/articles/3",
            FullTextRetrievalAccessRoutes.DoiLookup,
            FullTextRetrievalRights.Licensed,
            "https://rights.nexus.test/license",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime,
            redirectChain: [
                new("https://forged.example.net/articles/3", 302)
            ]);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-3",
            "attempt-3",
            "artifact-3");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.AreEqual(FullTextRetrievalErrorCodes.RedirectChainViolation, outcome.FailureCategory);
    }

    [TestMethod]
    public void Recorded_retrieval_rejects_credential_bearing_references_before_evidence_creation()
    {
        var input = BuildInput("candidate-secret-url");
        var bytes = Encoding.UTF8.GetBytes("not retained");

        var exception = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextRecordedRetrievalEvidence.Record(
                "evidence-secret",
                input,
                "secret-provider",
                "https://user:secret@api.nexus.test/articles/3",
                FullTextRetrievalAccessRoutes.ProviderApi,
                FullTextRetrievalRights.OpenAccess,
                "https://rights.nexus.test/open-access",
                FullTextArtifactKinds.Text,
                "text/plain",
                200,
                bytes,
                FixedTime,
                FixedTime));

        Assert.AreEqual(FullTextRetrievalErrorCodes.InvalidUriPolicy, exception.Category);
    }

    [TestMethod]
    [DataRow("articles/8?token=secret")]
    [DataRow("articles/8?%2573ig=secret")]
    [DataRow("articles/8?api+key=secret")]
    [DataRow("articles/8?x-goog%2Bsignature=secret")]
    [DataRow("articles/8?%zz=secret")]
    [DataRow("articles/8?redirect=https%3A%2F%2Fstorage.test%2Fpaper.pdf%3FX-Amz-Signature%3Dsecret")]
    [DataRow("articles/8?redirect=https%253A%252F%252Fstorage.test%252Fpaper.pdf%253Ftoken%253Dsecret")]
    [DataRow("articles/8?redirect=%zz")]
    public void Recorded_retrieval_rejects_source_reference_query_names_with_credential_shape(string sourceReference)
    {
        var input = BuildInput("candidate-signed-source-url");
        var bytes = Encoding.UTF8.GetBytes("not retained");

        var exception = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextRecordedRetrievalEvidence.Record(
                "evidence-signed-source-url",
                input,
                "secret-provider",
                sourceReference,
                FullTextRetrievalAccessRoutes.ProviderApi,
                FullTextRetrievalRights.OpenAccess,
                "https://rights.nexus.test/open-access",
                FullTextArtifactKinds.Text,
                "text/plain",
                200,
                bytes,
                FixedTime,
                FixedTime));

        Assert.AreEqual(FullTextRetrievalErrorCodes.InvalidUriPolicy, exception.Category);
    }

    [TestMethod]
    [DataRow("open-access?token=secret")]
    [DataRow("open-access?%2573ig=secret")]
    [DataRow("open-access?api+key=secret")]
    [DataRow("open-access?url=https%3A%2F%2Fstorage.test%2Flicense%3Fcredential%3Dsecret")]
    public void Recorded_retrieval_rejects_rights_reference_query_names_with_credential_shape(string rightsReference)
    {
        var input = BuildInput("candidate-signed-rights-url");
        var bytes = Encoding.UTF8.GetBytes("not retained");

        var exception = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextRecordedRetrievalEvidence.Record(
                "evidence-signed-rights-url",
                input,
                "secret-provider",
                "https://api.nexus.test/articles/8",
                FullTextRetrievalAccessRoutes.ProviderApi,
                FullTextRetrievalRights.OpenAccess,
                rightsReference,
                FullTextArtifactKinds.Text,
                "text/plain",
                200,
                bytes,
                FixedTime,
                FixedTime));

        Assert.AreEqual(FullTextRetrievalErrorCodes.InvalidUriPolicy, exception.Category);
    }

    [TestMethod]
    [DataRow("https://api.nexus.test/articles/8?token=secret")]
    [DataRow("https://api.nexus.test/articles/8?%2573ig=secret")]
    [DataRow("https://api.nexus.test/articles/8?api+key=secret")]
    [DataRow("https://api.nexus.test/articles/8?x-goog%2Bsignature=secret")]
    [DataRow("https://api.nexus.test/articles/8?%zz=secret")]
    [DataRow("https://api.nexus.test/articles/8?next=https%3A%2F%2Fstorage.test%2Fpaper.pdf%3Ftoken%3Dsecret")]
    [DataRow("articles/8?token=secret")]
    public void Recorded_retrieval_redirect_chain_rejects_credential_query_names(string redirectUrl)
    {
        var exception = Assert.ThrowsExactly<FullTextRuleException>(() =>
            new FullTextRecordedRedirect(redirectUrl, 302));

        Assert.AreEqual(FullTextRetrievalErrorCodes.InvalidUriPolicy, exception.Category);
    }

    [TestMethod]
    public void Recorded_retrieval_accepts_non_uri_rights_reference_without_credential_shape()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-non-uri-rights-reference");
        var bytes = Encoding.UTF8.GetBytes("non uri rights");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-non-uri-rights",
            input,
            "open-access-provider",
            "https://api.nexus.test/articles/non-uri-rights",
            FullTextRetrievalAccessRoutes.ProviderApi,
            FullTextRetrievalRights.OpenAccess,
            "local-rights/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-non-uri-rights",
            "attempt-non-uri-rights",
            "artifact-non-uri-rights");

        Assert.IsTrue(outcome.IsSuccess);
        Assert.IsNotNull(outcome.Chain);
        Assert.AreEqual("local-rights/open-access", outcome.SourceAttempt.SourceReference);
    }

    [TestMethod]
    [DataRow("https://api.nexus.test/articles/3?sig=secret")]
    [DataRow("https://api.nexus.test/articles/3?token=secret")]
    [DataRow("https://api.nexus.test/articles/3?key=secret")]
    [DataRow("https://api.nexus.test/articles/3?x-goog-signature=secret")]
    public void Recorded_retrieval_rejects_common_signed_url_parameters(string sourceReference)
    {
        var input = BuildInput("candidate-signed-url");
        var bytes = Encoding.UTF8.GetBytes("not retained");

        var exception = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextRecordedRetrievalEvidence.Record(
                "evidence-signed-url",
                input,
                "secret-provider",
                sourceReference,
                FullTextRetrievalAccessRoutes.ProviderApi,
                FullTextRetrievalRights.OpenAccess,
                "https://rights.nexus.test/open-access",
                FullTextArtifactKinds.Text,
                "text/plain",
                200,
                bytes,
                FixedTime,
                FixedTime));

        Assert.AreEqual(FullTextRetrievalErrorCodes.InvalidUriPolicy, exception.Category);
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_encoded_response()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-encoded");
        var bytes = Encoding.UTF8.GetBytes("compressed");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-4",
            input,
            "encoded-provider",
            "https://api.nexus.test/articles/4",
            FullTextRetrievalAccessRoutes.ProviderApi,
            FullTextRetrievalRights.PublicDomain,
            "https://rights.nexus.test/public-domain",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime,
            contentEncoding: "gzip");

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-4",
            "attempt-4",
            "artifact-4");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.AreEqual(FullTextRetrievalErrorCodes.UnsupportedEncoding, outcome.FailureCategory);
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_incomplete_body()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-incomplete");
        var bytes = Encoding.UTF8.GetBytes("partial");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-5",
            input,
            "partial-provider",
            "https://api.nexus.test/articles/5",
            FullTextRetrievalAccessRoutes.LandingPage,
            FullTextRetrievalRights.OpenAccess,
            "https://rights.nexus.test/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime,
            responseComplete: false);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-5",
            "attempt-5",
            "artifact-5");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.AreEqual(FullTextRetrievalErrorCodes.IncompleteBody, outcome.FailureCategory);
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_oversized_body()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 8);
        var input = BuildInput("candidate-oversized");
        var bytes = Encoding.UTF8.GetBytes("this-is-longer-than-eight");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-6",
            input,
            "oversized-provider",
            "https://api.nexus.test/articles/6",
            FullTextRetrievalAccessRoutes.Repository,
            FullTextRetrievalRights.OpenAccess,
            "https://rights.nexus.test/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-6",
            "attempt-6",
            "artifact-6");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.AreEqual(FullTextRetrievalErrorCodes.OversizedBody, outcome.FailureCategory);
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_digest_mutation()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-digest");
        var recordedBytes = Encoding.UTF8.GetBytes("exact");
        var suppliedBytes = Encoding.UTF8.GetBytes("alter");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-7",
            input,
            "digest-provider",
            "https://api.nexus.test/articles/7",
            FullTextRetrievalAccessRoutes.ProviderApi,
            FullTextRetrievalRights.OpenAccess,
            "https://rights.nexus.test/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            recordedBytes,
            FixedTime,
            FixedTime);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            suppliedBytes,
            policy,
            "acquisition-7",
            "attempt-7",
            "artifact-7");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.AreEqual(FullTextRetrievalErrorCodes.DigestMismatch, outcome.FailureCategory);
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_html_masquerading_as_pdf_and_fails_conversion()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-html");
        var bytes = Encoding.UTF8.GetBytes("<html><body>not pdf</body></html>");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-8",
            input,
            "html-provider",
            "https://api.nexus.test/articles/8",
            FullTextRetrievalAccessRoutes.LandingPage,
            FullTextRetrievalRights.Licensed,
            "https://rights.nexus.test/license",
            FullTextArtifactKinds.Pdf,
            "application/pdf",
            200,
            bytes,
            FixedTime,
            FixedTime);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-8",
            "attempt-8",
            "artifact-8");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.AreEqual(FullTextRetrievalErrorCodes.ConversionFailed, outcome.FailureCategory);
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_terminal_failure()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-failure");
        var bytes = Encoding.UTF8.GetBytes("terminal");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-10",
            input,
            "failure-provider",
            "https://api.nexus.test/articles/10",
            FullTextRetrievalAccessRoutes.Repository,
            FullTextRetrievalRights.OpenAccess,
            "https://rights.nexus.test/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime,
            terminalFailureCategory: "publisher-temporary-failure",
            terminalFailureSummary: "Recorded retrieval failed before artifact conversion.");

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-10",
            "attempt-10",
            "artifact-10");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.AreEqual("publisher-temporary-failure", outcome.FailureCategory);
        Assert.AreEqual("Recorded retrieval failed before artifact conversion.", outcome.FailureSummary);
    }

    [TestMethod]
    public void Verify_Terminal_failure_preserves_transport_category_before_rights_admission()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-terminal-rights");
        var bytes = Encoding.UTF8.GetBytes("terminal");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-terminal-rights",
            input,
            "failure-provider",
            "https://api.nexus.test/articles/terminal",
            FullTextRetrievalAccessRoutes.Repository,
            "rights-not-observed",
            "https://rights.nexus.test/not-observed",
            FullTextArtifactKinds.Text,
            "text/plain",
            503,
            bytes,
            FixedTime,
            FixedTime,
            terminalFailureCategory: "provider-timeout",
            terminalFailureSummary: "Provider timed out before rights could be confirmed.");

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-terminal-rights",
            "attempt-terminal-rights",
            "artifact-terminal-rights");

        Assert.AreEqual("provider-timeout", outcome.FailureCategory);
        Assert.AreEqual(FullTextAcquisitionKinds.ExternalUrlReference, outcome.Acquisition.AcquisitionKind);
    }

    [TestMethod]
    public void Verify_Licensed_repository_retrieval_is_not_labelled_open_access()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-licensed");
        var bytes = Encoding.UTF8.GetBytes("licensed text");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-licensed",
            input,
            "licensed-provider",
            "https://api.nexus.test/articles/licensed",
            FullTextRetrievalAccessRoutes.Repository,
            FullTextRetrievalRights.Licensed,
            "https://rights.nexus.test/license",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-licensed",
            "attempt-licensed",
            "artifact-licensed");

        Assert.IsTrue(outcome.IsSuccess);
        Assert.AreEqual(FullTextAcquisitionKinds.ExternalUrlReference, outcome.Acquisition.AcquisitionKind);
        Assert.AreEqual(FullTextAcquisitionKinds.ExternalUrlReference, outcome.SourceAttempt.AcquisitionKind);
    }

    [TestMethod]
    public void Recorded_retrieval_evidence_canonical_round_trip_binds_input_and_exact_bytes()
    {
        var input = BuildInput("candidate-roundtrip");
        var bytes = Encoding.UTF8.GetBytes("canonical retrieval bytes");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-roundtrip",
            input,
            "roundtrip-provider",
            "https://api.nexus.test/articles/roundtrip",
            FullTextRetrievalAccessRoutes.Repository,
            FullTextRetrievalRights.OpenAccess,
            "https://rights.nexus.test/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime.AddSeconds(1),
            redirectChain: [new("https://api.nexus.test/articles/final", 302)]);

        var serialized = FullTextRecordedRetrievalCanonicalCodec.Serialize(evidence);
        var restored = FullTextRecordedRetrievalCanonicalCodec.Rehydrate(
            serialized,
            evidence.Digest,
            input,
            bytes);

        Assert.AreEqual(evidence.Digest, restored.Digest);
        CollectionAssert.AreEqual(serialized, restored.ToCanonicalBytes());
        Assert.AreEqual(evidence.InputDigest, restored.InputDigest);
        Assert.AreEqual(1, restored.RedirectChain.Count);

        var nonCanonical = serialized.Concat([(byte)'\n']).ToArray();
        Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextRecordedRetrievalCanonicalCodec.Rehydrate(
                nonCanonical,
                evidence.Digest,
                input,
                bytes));
        Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextRecordedRetrievalCanonicalCodec.Rehydrate(
                serialized,
                evidence.Digest,
                BuildInput("candidate-other"),
                bytes));
        Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextRecordedRetrievalCanonicalCodec.Rehydrate(
                serialized,
                evidence.Digest,
                input,
                Encoding.UTF8.GetBytes("mutated retrieval bytes")));
    }

    [TestMethod]
    public void Recorded_retrieval_evidence_rejects_schema_and_scope_tampering()
    {
        var input = BuildInput("candidate-schema-tamper");
        var bytes = Encoding.UTF8.GetBytes("canonical retrieval bytes");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-schema-tamper",
            input,
            "roundtrip-provider",
            "https://api.nexus.test/articles/schema-tamper",
            FullTextRetrievalAccessRoutes.Repository,
            FullTextRetrievalRights.OpenAccess,
            "https://rights.nexus.test/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime.AddSeconds(1));
        var canonical = FullTextRecordedRetrievalCanonicalCodec.Serialize(evidence);

        AssertInvalidCanonicalMutation(canonical, input, bytes, content => content["unexpected"] = "value");
        AssertInvalidCanonicalMutation(canonical, input, bytes, content => content.Remove("media_type"));
        AssertInvalidCanonicalMutation(canonical, input, bytes, content => content["terminal_failure_category"] = "timeout");
        AssertInvalidCanonicalMutation(canonical, input, bytes, content => content["raw_byte_digest_scope"] = "canonical-json-record");
    }

    [TestMethod]
    public void Verify_Recorded_retrieval_rejects_http_and_ip_redirect_steps()
    {
        var policy = new FullTextRecordedRetrievalPolicy(["api.nexus.test"], 4096);
        var input = BuildInput("candidate-host-type");
        var bytes = Encoding.UTF8.GetBytes("host-check");
        var evidence = FullTextRecordedRetrievalEvidence.Record(
            "evidence-9",
            input,
            "host-provider",
            "http://api.nexus.test/articles/9",
            FullTextRetrievalAccessRoutes.DoiLookup,
            FullTextRetrievalRights.OpenAccess,
            "https://rights.nexus.test/open-access",
            FullTextArtifactKinds.Text,
            "text/plain",
            200,
            bytes,
            FixedTime,
            FixedTime,
            redirectChain: [
                new("https://203.0.113.10/articles/9", 301)
            ]);

        var outcome = FullTextRetrievalVerifier.Verify(
            evidence,
            bytes,
            policy,
            "acquisition-9",
            "attempt-9",
            "artifact-9");

        Assert.IsFalse(outcome.IsSuccess);
        Assert.AreEqual(FullTextRetrievalErrorCodes.RedirectChainViolation, outcome.FailureCategory);
    }

    private static FullTextInput BuildInput(string candidateId)
    {
        return FullTextInput.FromScreeningDecision(
            $"input-{candidateId}",
            $"candidate-set-{candidateId}",
            candidateId,
            $"screening-decision-{candidateId}",
            "title_abstract",
            FullTextScreeningVerdicts.Include);
    }

    private static void AssertInvalidCanonicalMutation(
        byte[] canonical,
        FullTextInput input,
        byte[] exactBytes,
        Action<JsonObject> mutation)
    {
        var root = JsonNode.Parse(canonical)!.AsObject();
        mutation(root["content"]!.AsObject());
        using var document = JsonDocument.Parse(root.ToJsonString());
        var mutated = CanonicalJsonSerializer.SerializeToUtf8Bytes(
            CanonicalJsonValue.FromJsonElement(document.RootElement));
        var exception = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextRecordedRetrievalCanonicalCodec.Rehydrate(
                mutated,
                ContentDigest.Sha256(mutated),
                input,
                exactBytes));
        Assert.AreEqual(FullTextRetrievalErrorCodes.InvalidEvidence, exception.Category);
    }
}
