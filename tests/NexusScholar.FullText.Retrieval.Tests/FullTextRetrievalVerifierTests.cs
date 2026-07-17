using System.Text;
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
}
