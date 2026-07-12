using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.FullText;
using NexusScholar.Kernel;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class FullTextFixtureTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "fixtures", "fulltext");

    private static readonly string[] ExpectedFixtureFiles =
    {
        "fulltext-input-from-screening-include.json",
        "fulltext-input-from-screening-needs-review.json",
        "fulltext-reject-raw-search-trace.json",
        "fulltext-reject-raw-dedup-member.json",
        "fulltext-exclude-not-retrievable-by-default.json",
        "fulltext-user-supplied-pdf-artifact.json",
        "fulltext-user-supplied-xml-artifact.json",
        "fulltext-user-supplied-text-artifact.json",
        "fulltext-deterministic-stub-artifact.json",
        "fulltext-local-path-not-identity.json",
        "fulltext-missing-raw-digest.json",
        "fulltext-wrong-digest-scope.json",
        "fulltext-digest-mismatch.json",
        "fulltext-invalid-pdf-signature.json",
        "fulltext-html-not-fulltext-xml.json",
        "fulltext-empty-text-artifact.json",
        "fulltext-artifact-too-large.json",
        "fulltext-source-failure-followed-by-success.json",
        "fulltext-duplicate-artifact-digest.json",
        "fulltext-derived-extraction-binds-source-artifact.json",
        "fulltext-partial-extraction-warning.json",
        "fulltext-app-projection-not-authority.json"
    };

    [TestMethod]
    public void FullText_fixture_files_are_present()
    {
        Directory.CreateDirectory(FixtureDirectory);
        var files = Directory.EnumerateFiles(FixtureDirectory, "*.json")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedFile in ExpectedFixtureFiles)
        {
            Assert.IsTrue(files.Contains(expectedFile), $"Missing fixture '{expectedFile}'.");
        }
    }

    [TestMethod]
    public void FullText_fixture_files_have_local_contract_metadata()
    {
        foreach (var expectedFile in ExpectedFixtureFiles)
        {
            using var document = LoadFixture(expectedFile);
            var root = document.RootElement;

            Assert.AreEqual(Path.GetFileNameWithoutExtension(expectedFile), root.GetProperty("fixtureId").GetString());
            Assert.AreEqual("local-gate-9-fulltext-implementation", root.GetProperty("sourceKind").GetString());
            Assert.AreEqual("local-gate-9-fulltext-local", root.GetProperty("sourceCommit").GetString());
            Assert.IsTrue(root.GetProperty("sourceRefs").GetArrayLength() > 0);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(item => item.GetString() == "no-php-compatibility-claim"));
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(item => item.GetString() == "no-network-provider-behavior"));
            Assert.IsTrue(ContentDigest.TryParse(root.GetProperty("inputDigest").GetString(), out _));
            Assert.IsTrue(ContentDigest.TryParse(root.GetProperty("outputDigest").GetString(), out _));
        }
    }

    [TestMethod]
    public void FullText_screening_handoff_fixtures_are_accepted()
    {
        using var includeDocument = LoadFixture("fulltext-input-from-screening-include.json");
        using var reviewDocument = LoadFixture("fulltext-input-from-screening-needs-review.json");

        var include = BuildInputFromFixture(includeDocument.RootElement.GetProperty("case").GetProperty("input"));
        var needsReview = BuildInputFromFixture(reviewDocument.RootElement.GetProperty("case").GetProperty("input"));

        Assert.AreEqual(FullTextEligibility.Retrievable, include.Eligibility);
        Assert.AreEqual(FullTextEligibility.ReviewableRetrievable, needsReview.Eligibility);
    }

    [TestMethod]
    public void FullText_input_negative_fixtures_reject_by_category()
    {
        AssertRejectsInput("fulltext-reject-raw-search-trace.json");
        AssertRejectsInput("fulltext-reject-raw-dedup-member.json");
        AssertRejectsInput("fulltext-exclude-not-retrievable-by-default.json");
    }

    [TestMethod]
    public void FullText_artifact_positive_fixtures_create_raw_byte_evidence()
    {
        foreach (var fixture in new[]
        {
            "fulltext-user-supplied-pdf-artifact.json",
            "fulltext-user-supplied-xml-artifact.json",
            "fulltext-user-supplied-text-artifact.json",
            "fulltext-deterministic-stub-artifact.json"
        })
        {
            using var document = LoadFixture(fixture);
            var artifactElement = document.RootElement.GetProperty("case").GetProperty("artifact");
            var expected = document.RootElement.GetProperty("case").GetProperty("expected");
            var acquisitionKind = document.RootElement.GetProperty("case").TryGetProperty("acquisitionKind", out var kind)
                ? kind.GetString()!
                : FullTextAcquisitionKinds.UserSuppliedLocalFile;
            var bytes = Encoding.UTF8.GetBytes(artifactElement.GetProperty("bytes").GetString()!);
            var input = BuildInput("candidate-artifact");
            var acquisition = BuildAcquisition(input, acquisitionKind);

            var artifact = FullTextArtifactEvidence.FromBytes(
                $"artifact-{Path.GetFileNameWithoutExtension(fixture)}",
                input,
                acquisition,
                artifactElement.GetProperty("artifactKind").GetString()!,
                artifactElement.GetProperty("mediaType").GetString()!,
                bytes,
                maxBytes: 4096);

            Assert.AreEqual(expected.GetProperty("digestScope").GetString(), artifact.RawByteDigestScope);
            Assert.AreEqual(ContentDigest.Sha256(bytes).ToString(), artifact.RawByteDigest);
        }
    }

    [TestMethod]
    public void FullText_artifact_negative_fixtures_reject_by_category()
    {
        AssertProjectionFixture("fulltext-local-path-not-identity.json");
        AssertDigestFixture("fulltext-missing-raw-digest.json");
        AssertDigestFixture("fulltext-wrong-digest-scope.json");
        AssertDigestFixture("fulltext-digest-mismatch.json");
        AssertValidatorFixture("fulltext-invalid-pdf-signature.json");
        AssertValidatorFixture("fulltext-html-not-fulltext-xml.json");
        AssertValidatorFixture("fulltext-empty-text-artifact.json");
        AssertValidatorFixture("fulltext-artifact-too-large.json");
        AssertProjectionFixture("fulltext-app-projection-not-authority.json");
    }

    [TestMethod]
    public void FullText_source_attempts_preserve_failure_skip_and_success()
    {
        using var document = LoadFixture("fulltext-source-failure-followed-by-success.json");
        var statuses = document.RootElement.GetProperty("case").GetProperty("attemptStatuses")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
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
            statuses.Select((status, index) =>
                new FullTextSourceAttempt(
                    $"attempt-{index + 1}",
                    $"source-{index + 1}",
                    index + 1,
                    FullTextAcquisitionKinds.ManualAcquisition,
                    status)).ToArray());

        CollectionAssert.AreEqual(statuses, acquisition.SourceAttempts.Select(attempt => attempt.Status).ToArray());
    }

    [TestMethod]
    public void FullText_duplicate_fixture_detects_digest_without_merging_candidates()
    {
        using var document = LoadFixture("fulltext-duplicate-artifact-digest.json");
        var @case = document.RootElement.GetProperty("case");
        var bytes = Encoding.UTF8.GetBytes(@case.GetProperty("bytes").GetString()!);
        var candidateIds = @case.GetProperty("candidateIds").EnumerateArray().Select(item => item.GetString()!).ToArray();
        var artifacts = candidateIds
            .Select((candidateId, index) => BuildArtifactEvidence($"artifact-{index + 1}", candidateId, bytes))
            .ToArray();

        var duplicates = FullTextDuplicatePolicy.FindDuplicateArtifacts(artifacts);

        Assert.AreEqual(1, duplicates.Count);
        Assert.AreEqual(@case.GetProperty("expected").GetProperty("category").GetString(), duplicates[0].Category);
        CollectionAssert.AreEqual(candidateIds, duplicates[0].CandidateIds.ToArray());
    }

    [TestMethod]
    public void FullText_extraction_fixtures_bind_source_artifact_and_partial_warning()
    {
        using var bindingDocument = LoadFixture("fulltext-derived-extraction-binds-source-artifact.json");
        var binding = bindingDocument.RootElement.GetProperty("case");
        var sourceDigest = ContentDigest.Sha256Utf8("source raw bytes").ToString();
        var pageText = new[] { "extracted text" };
        var extraction = new FullTextExtractionRecord(
            "extraction-binding",
            binding.GetProperty("sourceArtifactId").GetString()!,
            sourceDigest,
            binding.GetProperty("sourceRawByteDigestScope").GetString()!,
            "deterministic-stub-extractor",
            "1.0.0",
            FixedTime,
            "user-supplied-derived-text",
            FullTextExtractionStatuses.Success,
            extractedTextDigest: FullTextExtractionRecord.ComputeRepresentationDigest(FullTextExtractionRepresentations.PageText, pageText).ToString(),
            extractedTextDigestScope: DigestScope.CanonicalJsonRecord.ToString(),
            pageText: pageText,
            representationKind: FullTextExtractionRepresentations.PageText);

        Assert.AreEqual(binding.GetProperty("sourceArtifactId").GetString(), extraction.SourceArtifactId);
        Assert.AreEqual(DigestScope.RawArtifactBytes.ToString(), extraction.SourceRawByteDigestScope);

        using var partialDocument = LoadFixture("fulltext-partial-extraction-warning.json");
        var partialCase = partialDocument.RootElement.GetProperty("case");
        var warnings = partialCase.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()!).ToArray();
        var partialText = new[] { "partial text" };
        var partial = new FullTextExtractionRecord(
            "extraction-partial",
            "artifact-source-001",
            sourceDigest,
            DigestScope.RawArtifactBytes.ToString(),
            "deterministic-stub-extractor",
            "1.0.0",
            FixedTime,
            "user-supplied-derived-text",
            partialCase.GetProperty("status").GetString()!,
            extractedTextDigest: FullTextExtractionRecord.ComputeRepresentationDigest(FullTextExtractionRepresentations.PageText, partialText).ToString(),
            extractedTextDigestScope: DigestScope.CanonicalJsonRecord.ToString(),
            pageText: partialText,
            warnings: warnings,
            representationKind: FullTextExtractionRepresentations.PageText);

        Assert.IsTrue(partial.Warnings.Contains(partialCase.GetProperty("expected").GetProperty("category").GetString()!));
    }

    private static void AssertRejectsInput(string fixture)
    {
        using var document = LoadFixture(fixture);
        var @case = document.RootElement.GetProperty("case");
        var input = @case.GetProperty("input");
        var expectedCategory = @case.GetProperty("expected").GetProperty("category").GetString();

        var error = Assert.ThrowsExactly<FullTextRuleException>(() => BuildInputFromFixture(input));
        Assert.AreEqual(expectedCategory, error.Category);
    }

    private static void AssertProjectionFixture(string fixture)
    {
        using var document = LoadFixture(fixture);
        var @case = document.RootElement.GetProperty("case");
        var expectedCategory = @case.GetProperty("expected").GetProperty("category").GetString();

        var error = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextArtifactEvidence.RejectArtifactIdentityProjection(@case.GetProperty("projectionKind").GetString()!));

        Assert.AreEqual(expectedCategory, error.Category);
    }

    private static void AssertDigestFixture(string fixture)
    {
        using var document = LoadFixture(fixture);
        var @case = document.RootElement.GetProperty("case");
        var input = BuildInput("candidate-digest");
        var bytes = Encoding.UTF8.GetBytes(@case.TryGetProperty("bytes", out var bytesElement) ? bytesElement.GetString()! : "accepted bytes");
        var digest = @case.TryGetProperty("rawByteDigest", out var digestElement)
            ? digestElement.GetString()!
            : @case.TryGetProperty("declaredDigestSource", out var source)
                ? ContentDigest.Sha256Utf8(source.GetString()!).ToString()
                : ContentDigest.Sha256(bytes).ToString();
        var scope = @case.TryGetProperty("rawByteDigestScope", out var scopeElement)
            ? scopeElement.GetString()!
            : DigestScope.RawArtifactBytes.ToString();
        var expectedCategory = @case.GetProperty("expected").GetProperty("category").GetString();

        var error = Assert.ThrowsExactly<FullTextRuleException>(() =>
            new FullTextArtifactEvidence(
                "artifact-digest",
                input,
                input.CandidateId,
                "acquisition-digest",
                FullTextAcquisitionKinds.ManualAcquisition,
                "manual",
                FullTextArtifactKinds.Text,
                "text/plain",
                bytes.LongLength,
                digest,
                scope,
                FullTextAttemptStatuses.Success,
                bytes));

        Assert.AreEqual(expectedCategory, error.Category);
    }

    private static void AssertValidatorFixture(string fixture)
    {
        using var document = LoadFixture(fixture);
        var artifact = document.RootElement.GetProperty("case").GetProperty("artifact");
        var bytes = Encoding.UTF8.GetBytes(artifact.GetProperty("bytes").GetString()!);
        var maxBytes = artifact.TryGetProperty("maxBytes", out var maxBytesElement) ? maxBytesElement.GetInt64() : 4096;
        var expectedCategory = document.RootElement.GetProperty("case").GetProperty("expected").GetProperty("category").GetString();

        var error = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextArtifactValidator.Validate(
                artifact.GetProperty("artifactKind").GetString()!,
                bytes,
                maxBytes,
                artifact.GetProperty("mediaType").GetString()));

        Assert.AreEqual(expectedCategory, error.Category);
    }

    private static FullTextInput BuildInputFromFixture(JsonElement element)
    {
        if (element.TryGetProperty("verdict", out var verdict))
        {
            return FullTextInput.FromScreeningDecision(
                element.GetProperty("inputId").GetString()!,
                element.GetProperty("candidateSetId").GetString()!,
                element.GetProperty("candidateId").GetString()!,
                element.GetProperty("screeningDecisionId").GetString()!,
                element.GetProperty("stage").GetString()!,
                verdict.GetString()!);
        }

        return new FullTextInput(
            "fixture-input",
            element.GetProperty("sourceKind").GetString()!,
            element.GetProperty("candidateSetId").GetString()!,
            element.GetProperty("candidateId").GetString()!,
            FullTextEligibility.Retrievable);
    }

    private static FullTextInput BuildInput(string candidateId)
    {
        return FullTextInput.FromScreeningDecision(
            $"input-{candidateId}",
            "candidate-set-001",
            candidateId,
            $"screening-decision-{candidateId}",
            "title_abstract",
            FullTextScreeningVerdicts.Include);
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
            maxBytes: 4096);
    }

    private static FullTextActor FullTextActor() => new("human-fulltext-1", "human");

    private static JsonDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(FixtureDirectory, fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
