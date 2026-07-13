using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Screening;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class PhpScreeningFullTextGoldenTests
{
    private const string FixtureSetId = "php-screening-fulltext-v1";
    private const string FixtureSchemaVersion = "1.0.0";
    private const string FullTextCandidateSetId = "screening-fulltext-candidate-set";
    private const string FullTextDecisionSourceAlias = "llm-council";
    private const string CriteriaVersion = "1.0.0";

    private static readonly string ApprovedProtocolBinding = "protocol-screening-fulltext-v1";
    private static readonly string ApprovedProtocolDigest = ContentDigest.Sha256Utf8("protocol-screening-fulltext-v1").ToString();
    private static readonly string ApprovedProtocolDigestScope = DigestScope.ProtocolContent.ToString();
    private static readonly string ApprovedProtocolStatus = ScreeningProtocolBindingStatus.Approved;

    private static readonly DateTimeOffset FixedTime = new(2026, 06, 28, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] ExpectedSourceRefs =
    [
        "src/Dissemination/Application/Dto/FullTextResult.php",
        "src/Dissemination/Application/UseCase/RetrieveFullTextHandler.php",
        "src/Dissemination/Domain/FullText.php",
        "src/Dissemination/Domain/FullTextArtifactType.php",
        "src/Dissemination/Domain/FullTextStatus.php",
        "src/Dissemination/Domain/Port/DownloadResult.php",
        "src/Screening/Domain/CouncilDecisionAggregator.php",
        "src/Screening/Domain/ScreeningCriteria.php",
        "src/Screening/Domain/ScreeningDecision.php",
        "src/Screening/Domain/ScreeningRationale.php",
        "src/Screening/Domain/ScreeningStage.php",
        "src/Screening/Domain/ScreeningVote.php",
        "src/Screening/Domain/ScreeningVerdict.php"
    ];

    private static readonly string[] ExpectedEnvironmentAssumptions =
    [
        "PHP 8.3 or later",
        "git is available",
        "PHP reference tracked files are clean",
        "no network access and no Composer/network mode",
        "no live provider/download invocation",
        "output uses UTF-8 JSON with LF line endings"
    ];

    private static readonly string[] ExpectedNondeterminism =
    [
        "generated council verdict ids",
        "council verdict timestamps",
        "handler durations"
    ];

    private static readonly string[] ExpectedComparisonRules =
    [
        "compare deterministic enum values and normalized hashes",
        "compare screening verdicts as {decision, confidence, source, rationale, voteCounts, votes} only",
        "exercise private RetrieveFullTextHandler validation via ReflectionClass::newInstanceWithoutConstructor",
        "normalize non-finite numeric values before JSON serialization",
        "no runtime network calls in fixture generation"
    ];

    private static readonly string[] AllowedClassifications =
    [
        "equivalent_serialization",
        "intentional_change",
        "php_defect",
        "csharp_defect",
        "unresolved_specification_conflict"
    ];

    [TestMethod]
    public void Manifest_binds_exact_source_and_fixture_digests()
    {
        using var fixture = Load("manifest.json");
        using var sourceLock = JsonDocument.Parse(File.ReadAllBytes(SourceLockPath()));

        var root = fixture.RootElement;
        var phpReference = sourceLock.RootElement.GetProperty("php_reference");
        Assert.AreEqual(FixtureSetId, root.GetProperty("fixtureSetId").GetString());
        Assert.AreEqual(FixtureSchemaVersion, root.GetProperty("schemaVersion").GetString());
        Assert.AreEqual("pinned-php-observable-behavior", root.GetProperty("sourceKind").GetString());
        Assert.AreEqual(phpReference.GetProperty("repository").GetString(), root.GetProperty("sourceRepository").GetString());
        Assert.AreEqual(phpReference.GetProperty("commit").GetString(), root.GetProperty("sourceCommit").GetString());
        Assert.AreEqual("screening-fulltext-v1", root.GetProperty("generatorVersion").GetString());
        Assert.AreEqual(
            "php scripts/php-golden/screening-fulltext-export.php --php-reference \"$PHP_REFERENCE\" --source-lock specs/SOURCE.lock.json --input fixtures/php-golden/screening-fulltext/v1/input.json --comparison fixtures/php-golden/screening-fulltext/v1/comparison.json --output fixtures/php-golden/screening-fulltext/v1/expected.json --manifest fixtures/php-golden/screening-fulltext/v1/manifest.json",
            root.GetProperty("generatorCommand").GetString());

        CollectionAssert.AreEqual(ExpectedSourceRefs, ReadStrings(root.GetProperty("sourceRefs")));
        CollectionAssert.AreEqual(ExpectedEnvironmentAssumptions, ReadStrings(root.GetProperty("environmentAssumptions")));
        CollectionAssert.AreEqual(ExpectedNondeterminism, ReadStrings(root.GetProperty("ignoredNondeterminism")));
        CollectionAssert.AreEqual(ExpectedComparisonRules, ReadStrings(root.GetProperty("comparisonRules")));

        Assert.AreEqual(DigestFixture("input.json"), root.GetProperty("inputDigest").GetString());
        Assert.AreEqual(DigestFixture("expected.json"), root.GetProperty("outputDigest").GetString());
        Assert.AreEqual(DigestFile(SourceLockPath()), root.GetProperty("sourceLockDigest").GetString());
        Assert.AreEqual(DigestFixture("comparison.json"), root.GetProperty("classificationDigest").GetString());
    }

    [TestMethod]
    public void Screening_fulltext_fixtures_have_exact_26_case_inventory_and_boundaries()
    {
        using var input = Load("input.json");
        using var expected = Load("expected.json");
        using var comparison = Load("comparison.json");

        var inputCaseIds = input.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Select(c => c.GetProperty("id").GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var expectedCaseIds = expected.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Select(c => c.GetProperty("id").GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var expectedCaseCount = expectedCaseIds.Length;
        var inputCaseCount = inputCaseIds.Length;

        CollectionAssert.AreEqual(expectedCaseIds, inputCaseIds);
        Assert.AreEqual(26, expectedCaseCount, "fixture must contain exactly 26 cases");
        Assert.AreEqual(expectedCaseCount, inputCaseCount);

        var classifications = comparison.RootElement.GetProperty("classifications").EnumerateArray().ToArray();
        Assert.AreEqual(26, classifications.Length);

        var equivalent = classifications.Where(x => x.GetProperty("classification").GetString() == "equivalent_serialization").ToArray();
        var intentional = classifications.Where(x => x.GetProperty("classification").GetString() == "intentional_change").ToArray();
        var phpDefects = classifications.Where(x => x.GetProperty("classification").GetString() == "php_defect").ToArray();
        var csharpDefects = classifications.Where(x => x.GetProperty("classification").GetString() == "csharp_defect").ToArray();
        var unresolved = classifications.Where(x => x.GetProperty("classification").GetString() == "unresolved_specification_conflict").ToArray();

        Assert.AreEqual(16, equivalent.Length);
        Assert.AreEqual(9, intentional.Length);
        Assert.AreEqual(1, phpDefects.Length);
        Assert.AreEqual(0, csharpDefects.Length);
        Assert.AreEqual(0, unresolved.Length);

        var expectedIntentionalIds = new HashSet<string>
        {
            "screening-criteria-raw-hash-vs-envelope",
            "screening-council-unanimous-final",
            "screening-council-majority-final",
            "screening-council-conflict-final",
            "screening-council-all-failed-final",
            "fulltext-success-path-projection",
            "fulltext-success-missing-byte-digest",
            "fulltext-runtime-retrieval-projection",
            "fulltext-derived-extraction-absent"
        };

        foreach (var item in classifications)
        {
            var caseId = item.GetProperty("caseId").GetString()!;
            var classification = item.GetProperty("classification").GetString()!;
            CollectionAssert.Contains(AllowedClassifications, classification, $"Unexpected classification for '{caseId}'");
            CollectionAssert.AllItemsAreUnique(item.GetProperty("authorityRefs").EnumerateArray().Select(x => x.GetString()!).ToArray(), $"No authorities for '{caseId}'");
            Assert.IsTrue(item.GetProperty("comparisonRule").GetString()!.Length > 0, $"Missing comparisonRule for '{caseId}'");

            if (classification == "equivalent_serialization" || classification == "php_defect")
            {
                CollectionAssert.Contains(
                    expectedCaseIds,
                    caseId,
                    $"Expected case ids and classifs diverged at '{caseId}'");
            }

            var isIntentional = expectedIntentionalIds.Contains(caseId);
            if (classification == "intentional_change")
            {
                Assert.IsTrue(isIntentional, $"Case '{caseId}' expected to be intentional.");
            }
            else
            {
                Assert.IsFalse(isIntentional, $"Case '{caseId}' expected as intentional but classified as '{classification}'.");
            }
        }

        Assert.AreEqual("screening-nonfinite-confidence-accepted", phpDefects.Single().GetProperty("caseId").GetString());
    }

    [TestMethod]
    public void Equivalent_cases_follow_exact_cpp_semantics()
    {
        using var input = Load("input.json");
        using var expected = Load("expected.json");
        using var comparison = Load("comparison.json");

        var inputById = BuildIndex(input.RootElement.GetProperty("cases"));
        var expectedById = BuildIndex(expected.RootElement.GetProperty("cases"));
        var equivalent = classificationIndex(comparison.RootElement.GetProperty("classifications"))
            .Where(item => item.Value.GetProperty("classification").GetString() == "equivalent_serialization")
            .ToDictionary(item => item.Key, item => item.Value);

        var candidateIds = input.RootElement
            .GetProperty("cases")
            .EnumerateArray()
            .Where(c => c.TryGetProperty("candidateId", out _))
            .Select(c => c.GetProperty("candidateId").GetString()!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var screeningService = BuildScreeningService(candidateIds);

        var titleCriteria = BuildCriteria(
            "criteria-title-abstract-equivalent",
            ScreeningStages.TitleAbstract,
            CanonicalIncludeSet(),
            CanonicalExcludeSet());
        var fullTextCriteria = BuildCriteria(
            "criteria-fulltext-equivalent",
            ScreeningStages.FullText,
            CanonicalIncludeSet(),
            CanonicalExcludeSet());

        var expectedCaseMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["screening-decision-vocabulary"] = "equivalent",
            ["screening-stage-vocabulary"] = "equivalent",
            ["screening-criteria-key-order-stable"] = "equivalent",
            ["screening-criteria-list-order-semantic"] = "equivalent",
            ["screening-confidence-below-zero-rejected"] = "equivalent",
            ["screening-confidence-above-one-rejected"] = "equivalent",
            ["fulltext-artifact-type-vocabulary"] = "equivalent",
            ["fulltext-status-vocabulary"] = "equivalent",
            ["fulltext-failure-result"] = "equivalent",
            ["fulltext-skipped-result"] = "equivalent",
            ["fulltext-valid-pdf-validation"] = "equivalent",
            ["fulltext-invalid-pdf-signature"] = "equivalent",
            ["fulltext-oversized-artifact"] = "equivalent",
            ["fulltext-valid-xml-validation"] = "equivalent",
            ["fulltext-html-rejected-as-xml"] = "equivalent",
            ["fulltext-empty-text-rejected"] = "equivalent"
        };

        foreach (var caseId in expectedCaseMap.Keys)
        {
            Assert.IsTrue(equivalent.ContainsKey(caseId), $"missing equivalent classification for '{caseId}'");
            var fixtureCase = inputById[caseId];
            var expectedCase = expectedById[caseId].GetProperty("result");
            var classification = equivalent[caseId];

            switch (caseId)
            {
                case "screening-decision-vocabulary":
                    var expectedVerdicts = ReadStrings(expectedCase.GetProperty("values"));
                    var csharpVerdicts = new[]
                    {
                        ScreeningVerdicts.Include,
                        ScreeningVerdicts.Exclude,
                        ScreeningVerdicts.NeedsReview
                    };
                    CollectionAssert.AreEqual(
                        expectedVerdicts.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                        csharpVerdicts.OrderBy(value => value, StringComparer.Ordinal).ToArray());
                    CollectionAssert.AreEquivalent(
                        expectedVerdicts,
                        csharpVerdicts.OrderBy(value => value, StringComparer.Ordinal).ToArray());
                    break;

                case "screening-stage-vocabulary":
                    var expectedStages = ReadStrings(expectedCase.GetProperty("values"));
                    var csharpStages = new[]
                    {
                        ScreeningStages.TitleAbstract,
                        ScreeningStages.FullText,
                        ScreeningStages.HumanAdjudication
                    };
                    CollectionAssert.AreEqual(
                        expectedStages.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                        csharpStages.OrderBy(value => value, StringComparer.Ordinal).ToArray());
                    CollectionAssert.AreEquivalent(
                        expectedStages,
                        csharpStages.OrderBy(value => value, StringComparer.Ordinal).ToArray());
                    break;

                case "screening-criteria-key-order-stable":
                    var stableLeftRight = BuildCriteriaPair(
                        fixtureCase.GetProperty("criteriaA").GetProperty("include"),
                        fixtureCase.GetProperty("criteriaA").GetProperty("exclude"),
                        fixtureCase.GetProperty("criteriaB").GetProperty("include"),
                        fixtureCase.GetProperty("criteriaB").GetProperty("exclude"));
                    Assert.IsTrue(expectedCase.GetProperty("equal").GetBoolean());
                    CollectionAssert.AreEqual(
                        ReadCanonicalArray(expectedCase.GetProperty("normalizedLeft").GetProperty("include")),
                        ReadCanonicalArray(stableLeftRight.left.IncludeCriteria),
                        $"{caseId} left include should match normalized criteria.");
                    CollectionAssert.AreEqual(
                        ReadCanonicalArray(expectedCase.GetProperty("normalizedLeft").GetProperty("exclude")),
                        ReadCanonicalArray(stableLeftRight.left.ExcludeCriteria),
                        $"{caseId} left exclude should match normalized criteria.");
                    CollectionAssert.AreEqual(
                        ReadCanonicalArray(stableLeftRight.left.IncludeCriteria),
                        ReadCanonicalArray(stableLeftRight.right.IncludeCriteria),
                        $"{caseId} criteria include relation should ignore key order.");
                    CollectionAssert.AreEqual(
                        ReadCanonicalArray(stableLeftRight.left.ExcludeCriteria),
                        ReadCanonicalArray(stableLeftRight.right.ExcludeCriteria),
                        $"{caseId} criteria exclude relation should ignore key order.");
                    break;

                case "screening-criteria-list-order-semantic":
                    var semanticLeftRight = BuildCriteriaPair(
                        fixtureCase.GetProperty("criteriaA").GetProperty("include"),
                        fixtureCase.GetProperty("criteriaA").GetProperty("exclude"),
                        fixtureCase.GetProperty("criteriaB").GetProperty("include"),
                        fixtureCase.GetProperty("criteriaB").GetProperty("exclude"));
                    Assert.AreNotEqual(
                        semanticLeftRight.left.ComputeDigest(),
                        semanticLeftRight.right.ComputeDigest(),
                        $"{caseId} should differ by list ordering.");
                    Assert.IsFalse(expectedCase.GetProperty("equal").GetBoolean());
                    break;

                case "screening-confidence-below-zero-rejected":
                    {
                        var confidence = ParseFixtureConfidence(fixtureCase.GetProperty("confidence"));
                        var decision = BuildDecision(
                            caseId,
                            fixtureCase.GetProperty("candidateId").GetString()!,
                            titleCriteria,
                            fixtureCase.GetProperty("decision").GetString()!,
                            fixtureCase.GetProperty("stage").GetString()!,
                            ScreeningActor.Human("human-below-one"),
                            confidence,
                            EvidenceRefFixture());
                        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => screeningService.AddDecision(decision));
                        Assert.AreEqual(ScreeningErrorCodes.InvalidConfidence, error.Category, caseId);
                        Assert.AreEqual("vote-confidence-rejected", expectedCase.GetProperty("errorCategory").GetString());
                        break;
                    }

                case "screening-confidence-above-one-rejected":
                    {
                        var confidence = ParseFixtureConfidence(fixtureCase.GetProperty("confidence"));
                        var decision = BuildDecision(
                            caseId,
                            fixtureCase.GetProperty("candidateId").GetString()!,
                            titleCriteria,
                            fixtureCase.GetProperty("decision").GetString()!,
                            fixtureCase.GetProperty("stage").GetString()!,
                            ScreeningActor.Human("human-above-one"),
                            confidence,
                            EvidenceRefFixture());
                        var error = Assert.ThrowsExactly<ScreeningRuleException>(() => screeningService.AddDecision(decision));
                        Assert.AreEqual(ScreeningErrorCodes.InvalidConfidence, error.Category, caseId);
                        Assert.AreEqual("vote-confidence-rejected", expectedCase.GetProperty("errorCategory").GetString());
                        break;
                    }

                case "fulltext-artifact-type-vocabulary":
                    {
                        var expectedKinds = expectedCase.GetProperty("values").EnumerateArray().Select(x => x.GetString()!).OrderBy(v => v, StringComparer.Ordinal).ToArray();
                        var csharpKinds = new[]
                        {
                            FullTextArtifactKinds.Pdf,
                            FullTextArtifactKinds.Xml,
                            FullTextArtifactKinds.Text
                        };
                        CollectionAssert.AreEquivalent(
                            expectedKinds,
                            csharpKinds.OrderBy(v => v, StringComparer.Ordinal).ToArray());
                        Assert.AreEqual("shared-raw-artifact-subset", classification.GetProperty("comparisonRule").GetString());
                        var allCsharpKinds = typeof(FullTextArtifactKinds)
                            .GetFields()
                            .Where(field => field.IsLiteral && !field.IsInitOnly)
                            .Select(field => (string)field.GetRawConstantValue()!)
                            .ToArray();
                        CollectionAssert.DoesNotContain(expectedKinds, FullTextArtifactKinds.DerivedText);
                        CollectionAssert.Contains(allCsharpKinds, FullTextArtifactKinds.DerivedText);
                        break;
                    }

                case "fulltext-status-vocabulary":
                    {
                        var expectedStatuses = expectedCase.GetProperty("values").EnumerateArray().Select(x => x.GetString()!).OrderBy(v => v, StringComparer.Ordinal).ToArray();
                        var csharpStatuses = new[]
                        {
                        FullTextAttemptStatuses.Success,
                        FullTextAttemptStatuses.Failure,
                        FullTextAttemptStatuses.Skipped
                    };
                        CollectionAssert.AreEquivalent(
                            expectedStatuses,
                            csharpStatuses.OrderBy(v => v, StringComparer.Ordinal).ToArray());
                        break;
                    }

                case "fulltext-failure-result":
                    AssertSourceAttemptMapping(fixtureCase, expectedCase, FullTextAttemptStatuses.Failure, "inaccessible-full-text");
                    break;

                case "fulltext-skipped-result":
                    AssertSourceAttemptMapping(fixtureCase, expectedCase, FullTextAttemptStatuses.Skipped, "skipped");
                    break;

                case "fulltext-valid-pdf-validation":
                    AssertFullTextValidator(fixtureCase, expectedCase, shouldAccept: true);
                    break;

                case "fulltext-invalid-pdf-signature":
                    AssertFullTextValidator(fixtureCase, expectedCase, shouldAccept: false);
                    break;

                case "fulltext-oversized-artifact":
                    AssertFullTextValidator(fixtureCase, expectedCase, shouldAccept: false);
                    break;

                case "fulltext-valid-xml-validation":
                    AssertFullTextValidator(fixtureCase, expectedCase, shouldAccept: true);
                    break;

                case "fulltext-html-rejected-as-xml":
                    AssertFullTextValidator(fixtureCase, expectedCase, shouldAccept: false);
                    break;

                case "fulltext-empty-text-rejected":
                    AssertFullTextValidator(fixtureCase, expectedCase, shouldAccept: false);
                    break;

                default:
                    Assert.Fail($"{caseId} not included in deterministic equivalent case map.");
                    break;
            }

            // classification and authority pins:
            Assert.IsTrue(classification.GetProperty("comparisonRule").GetString()!.Length > 0);
            Assert.AreEqual(
                classificationGetExpectedRule(classification, caseId),
                classification.GetProperty("comparisonRule").GetString(),
                caseId);
        }
    }

    [TestMethod]
    public void Intentional_and_defect_boundaries_are_pinned()
    {
        using var input = Load("input.json");
        using var expected = Load("expected.json");
        using var comparison = Load("comparison.json");

        var inputById = BuildIndex(input.RootElement.GetProperty("cases"));
        var expectedById = BuildIndex(expected.RootElement.GetProperty("cases"));
        var classifications = classificationIndex(comparison.RootElement.GetProperty("classifications"));
        var expectedCaseCount = expected.RootElement.GetProperty("cases").GetArrayLength();

        // php fixture keeps nonfinite confidence as proposal evidence; C# must reject.
        var phpDefect = classifications["screening-nonfinite-confidence-accepted"];
        Assert.AreEqual("php_defect", phpDefect.GetProperty("classification").GetString());
        Assert.AreEqual("finite-confidence-validation", phpDefect.GetProperty("comparisonRule").GetString());
        var nonFiniteCase = inputById["screening-nonfinite-confidence-accepted"];
        Assert.IsTrue(expectedById["screening-nonfinite-confidence-accepted"].GetProperty("result").GetProperty("accepted").GetBoolean());
        Assert.IsFalse(expectedById["screening-nonfinite-confidence-accepted"].GetProperty("result").GetProperty("confidenceFinite").GetBoolean());
        var nonFiniteDecision = BuildDecision(
            nonFiniteCase.GetProperty("id").GetString()!,
            nonFiniteCase.GetProperty("candidateId").GetString()!,
            BuildCriteria(
                "criteria-nonfinite",
                ScreeningStages.TitleAbstract,
                CanonicalIncludeSet(),
                CanonicalExcludeSet()),
            nonFiniteCase.GetProperty("decision").GetString()!,
            nonFiniteCase.GetProperty("stage").GetString()!,
            ScreeningActor.Human("human-nonfinite"),
            double.NaN,
            EvidenceRefFixture());
        var nonFiniteError = Assert.ThrowsExactly<ScreeningRuleException>(() => BuildScreeningService([nonFiniteCase.GetProperty("candidateId").GetString()!]).AddDecision(nonFiniteDecision));
        Assert.AreEqual(ScreeningErrorCodes.InvalidConfidence, nonFiniteError.Category);

        // raw hash boundary:
        var rawHashCase = inputById["screening-criteria-raw-hash-vs-envelope"];
        var rawHashExpected = expectedById["screening-criteria-raw-hash-vs-envelope"].GetProperty("result");
        var envelopeCriteriaHash = rawHashExpected.GetProperty("criteriaHash").GetString()!;
        var canonicalCriteria = BuildCriteria(
            "criteria-envelope",
            ScreeningStages.TitleAbstract,
            rawHashCase.GetProperty("criteria").GetProperty("include"),
            rawHashCase.GetProperty("criteria").GetProperty("exclude"),
            requiresProtocolBinding: true);
        var canonicalCriteriaDigest = canonicalCriteria.ComputeDigest().ToString();
        if (canonicalCriteriaDigest.StartsWith("sha256:", StringComparison.Ordinal))
        {
            canonicalCriteriaDigest = canonicalCriteriaDigest.Substring("sha256:".Length);
        }

        Assert.AreEqual(64, envelopeCriteriaHash.Length, "fixture criteriaHash should be a 64-char hex digest");
        Assert.AreNotEqual(envelopeCriteriaHash, canonicalCriteriaDigest, "PHP criteria hash should not match C# digest envelope");
        var rawCriteriaDigest = DigestUtf8(rawHashCase.GetProperty("criteria").GetRawText());
        var rawCriteriaDigestHex = rawCriteriaDigest.StartsWith("sha256:", StringComparison.Ordinal)
            ? rawCriteriaDigest.Substring("sha256:".Length)
            : rawCriteriaDigest;
        Assert.AreNotEqual(rawCriteriaDigestHex, canonicalCriteriaDigest, "raw criteria hash should not be canonical digest");
        Assert.AreEqual(
            rawHashExpected.GetProperty("normalized").GetProperty("include").GetArrayLength(),
            canonicalCriteria.IncludeCriteria is CanonicalJsonArray include ? include.Items.Count : 0);
        Assert.AreEqual(
            rawHashExpected.GetProperty("normalized").GetProperty("exclude").GetArrayLength(),
            canonicalCriteria.ExcludeCriteria is CanonicalJsonArray exclude ? exclude.Items.Count : 0);
        CollectionAssert.AreEquivalent(
            ReadStrings(rawHashExpected.GetProperty("normalized").GetProperty("include")),
            canonicalCriteria.IncludeCriteria is CanonicalJsonArray includeCriteria ? ReadCanonicalArray(includeCriteria) : [],
            $"{rawHashCase.GetProperty("id").GetString()} includes");
        CollectionAssert.AreEquivalent(
            ReadStrings(rawHashExpected.GetProperty("normalized").GetProperty("exclude")),
            canonicalCriteria.ExcludeCriteria is CanonicalJsonArray excludeCriteria ? ReadCanonicalArray(excludeCriteria) : [],
            $"{rawHashCase.GetProperty("id").GetString()} excludes");

        // authority-bound council output cannot be finalized by automation
        foreach (var caseId in new[]
                 {
                     "screening-council-unanimous-final",
                     "screening-council-majority-final",
                     "screening-council-conflict-final",
                     "screening-council-all-failed-final"
                 })
        {
            var caseClassification = classifications[caseId];
            Assert.AreEqual("intentional_change", caseClassification.GetProperty("classification").GetString());
            Assert.AreEqual("council_projection_boundary", caseClassification.GetProperty("comparisonRule").GetString());
            AssertBoundaryAuthorityRefs(caseClassification,
                "docs/adr/0013-screening-decision-and-conflict-contract.md",
                "docs/adr/0021-screening-authority-dependency-direction.md");
            var councilCase = inputById[caseId];
            var service = BuildScreeningService([councilCase.GetProperty("candidateId").GetString()!]);

            var criteria = BuildCriteria(
                $"criteria-{caseId}",
                ScreeningStages.FullText,
                CanonicalIncludeSet(),
                CanonicalExcludeSet());

            AddCouncilSuggestions(service, criteria, councilCase, expectedById);
            var result = expectedById[caseId].GetProperty("result");
            var stage = councilCase.GetProperty("stage").GetString()!;
            var decision = BuildDecision(
                caseId,
                councilCase.GetProperty("candidateId").GetString()!,
                criteria,
                result.TryGetProperty("decision", out var decisionValue) ? decisionValue.GetString()! : ScreeningVerdicts.NeedsReview,
                stage,
                ScreeningActor.Automation(FullTextDecisionSourceAlias),
                result.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0d,
                EvidenceRefFixture());
            var boundaryError = Assert.ThrowsExactly<ScreeningRuleException>(() => service.AddDecision(decision));
            Assert.AreEqual(ScreeningErrorCodes.AutomationCannotFinalize, boundaryError.Category, caseId);
        }

        // artifact identity projection cannot rely on path/runtime
        AssertFullTextProjectionBoundary(
            inputById["fulltext-success-path-projection"].GetProperty("path").GetString()!,
            "local-path-not-artifact-identity",
            caseId: "fulltext-success-path-projection");
        AssertFullTextProjectionBoundary(
            inputById["fulltext-runtime-retrieval-projection"].GetProperty("path").GetString()!,
            "app-projection-not-core-authority",
            caseId: "fulltext-runtime-retrieval-projection");

        // missing raw-byte digest is rejected and cannot produce unclaimed evidence
        AssertMissingRawByteDigestRejected(inputById["fulltext-success-missing-byte-digest"]);

        // no artifact-level extraction digest is accepted by PHP fixture, yet C# keeps extraction source-bound and optional
        AssertBoundaryAuthorityRefs(
            classifications["fulltext-derived-extraction-absent"],
            "docs/adr/0014-fulltext-acquisition-artifact-and-extraction-contract.md",
            "docs/adr/0022-fulltext-artifacts-dependency-direction.md");
        var derivedCase = inputById["fulltext-derived-extraction-absent"];
        var derivedExpected = expectedById["fulltext-derived-extraction-absent"].GetProperty("result");
        Assert.IsFalse(derivedExpected.GetProperty("fullTextExtractionRecordClassExists").GetBoolean());
        var derivedCandidateId = derivedCase.TryGetProperty("candidateId", out var derivedCandidateIdValue)
            ? derivedCandidateIdValue.GetString()!
            : $"{derivedCase.GetProperty("id").GetString()}-candidate";
        var derivedInput = BuildFullTextInputFromCase(
            derivedCandidateId,
            FullTextScreeningVerdicts.Include);
        var derivedAcquisition = BuildAcquisition(
            derivedInput,
            FullTextAttemptStatuses.Success,
            FullTextAcquisitionKinds.ManualAcquisition,
            SourceAliasFromCase(derivedCase),
            derivedCase.GetProperty("http").GetInt32(),
            derivedCase.GetProperty("artifactKind").GetString()!);
        var derivedEvidence = FullTextArtifactEvidence.FromBytes(
            "artifact-derived-extraction-absent",
            derivedInput,
            derivedAcquisition,
            FullTextArtifactKinds.Pdf,
            "application/pdf",
            Encoding.UTF8.GetBytes("%PDF-1.7\r\nfixture-artifact"),
            derivedCase.GetProperty("maxBytes").GetInt64());
        Assert.IsNotNull(derivedEvidence);
    }

    private static void AssertSourceAttemptMapping(
        JsonElement fixtureCase,
        JsonElement expectedCase,
        string expectedStatus,
        string expectedErrorCategory)
    {
        var caseId = fixtureCase.GetProperty("id").GetString()!;
        var sourceAlias = SourceAliasFromCase(fixtureCase);
        var sourceAttempt = new FullTextSourceAttempt(
            $"attempt-{caseId}",
            sourceAlias,
            1,
            FullTextAcquisitionKinds.ManualAcquisition,
            expectedStatus,
            artifactKind: fixtureCase.GetProperty("artifactKind").GetString(),
            sourceReference: sourceAlias,
            mediaType: fixtureCase.GetProperty("mediaType").GetString(),
            httpStatus: fixtureCase.TryGetProperty("http", out var http) ? http.GetInt32() : null,
            errorCategory: expectedErrorCategory,
            errorMessage: expectedCase.GetProperty("errorMessage").GetString());

        Assert.AreEqual(expectedStatus, expectedCase.GetProperty("status").GetString(), caseId);
        Assert.AreEqual(sourceAlias, expectedCase.GetProperty("source").GetString(), caseId);
        Assert.AreEqual(expectedErrorCategory, expectedCase.GetProperty("errorMessage").GetString(), caseId);

        Assert.AreEqual(expectedStatus, sourceAttempt.Status);
        Assert.AreEqual(sourceAlias, sourceAttempt.SourceReference);
        Assert.AreEqual(sourceAlias, sourceAttempt.SourceAlias);
        Assert.AreEqual(expectedErrorCategory, sourceAttempt.ErrorCategory);
    }

    private static void AssertFullTextValidator(JsonElement fixtureCase, JsonElement expectedResult, bool shouldAccept)
    {
        var artifactKind = fixtureCase.GetProperty("artifactKind").GetString()!;
        var maxBytes = fixtureCase.GetProperty("maxBytes").GetInt64();
        var mediaType = fixtureCase.GetProperty("mediaType").GetString();
        var bytes = Encoding.UTF8.GetBytes(fixtureCase.GetProperty("bytes").GetString()!);

        if (shouldAccept)
        {
            Assert.AreEqual(true, expectedResult.GetProperty("accepted").GetBoolean(), "Expected fixture case should be accepted.");
            FullTextArtifactValidator.Validate(artifactKind, bytes, maxBytes, mediaType);
            return;
        }

        Assert.AreEqual(false, expectedResult.GetProperty("accepted").GetBoolean(), "Expected fixture case should be rejected.");
        var expectedCategory = expectedResult.GetProperty("errorCategory").GetString()!;
        var error = Assert.ThrowsExactly<FullTextRuleException>(() => FullTextArtifactValidator.Validate(artifactKind, bytes, maxBytes, mediaType));
        Assert.AreEqual(expectedCategory, error.Category);
    }

    private static void AssertFullTextProjectionBoundary(string projectionValue, string expectedCategory, string caseId)
    {
        var error = Assert.ThrowsExactly<FullTextRuleException>(() =>
            FullTextArtifactEvidence.RejectArtifactIdentityProjection(projectionValue));
        Assert.AreEqual(expectedCategory, error.Category, caseId);
        Assert.IsTrue(
            error.Message.Contains("projection", StringComparison.OrdinalIgnoreCase),
            $"Projection boundary message should explain artifact projection for '{caseId}'.");
    }

    private static void AssertMissingRawByteDigestRejected(JsonElement fixtureCase)
    {
        var caseId = fixtureCase.GetProperty("id").GetString()!;
        var candidateId = fixtureCase.TryGetProperty("candidateId", out var candidateIdValue)
            ? candidateIdValue.GetString()!
            : $"{caseId}-candidate";
        var sourceAlias = SourceAliasFromCase(fixtureCase);
        var input = BuildFullTextInputFromCase(
            candidateId,
            FullTextScreeningVerdicts.Include);
        var acquisition = BuildAcquisition(
            input,
            FullTextAttemptStatuses.Success,
            FullTextAcquisitionKinds.ManualAcquisition,
            sourceAlias,
            fixtureCase.GetProperty("http").GetInt32(),
            fixtureCase.GetProperty("artifactKind").GetString()!);

        var error = Assert.ThrowsExactly<FullTextRuleException>(() => new FullTextArtifactEvidence(
            artifactId: "artifact-missing-digest",
            inputRef: input,
            candidateId: input.CandidateId,
            acquisitionId: acquisition.AcquisitionId,
            acquisitionKind: acquisition.AcquisitionKind,
            sourceAlias: acquisition.SourceAlias,
            artifactKind: fixtureCase.GetProperty("artifactKind").GetString()!,
            mediaType: fixtureCase.GetProperty("mediaType").GetString()!,
            sizeBytes: fixtureCase.GetProperty("maxBytes").GetInt64(),
            rawByteDigest: string.Empty,
            rawByteDigestScope: DigestScope.RawArtifactBytes.ToString(),
            validationStatus: FullTextAttemptStatuses.Success,
            acceptedBytes: null,
            sourceReference: fixtureCase.GetProperty("path").GetString()!));

        Assert.AreEqual(FullTextErrorCodes.MissingRawArtifactDigest, error.Category);
        Assert.IsFalse(fixtureCase.TryGetProperty("rawByteDigest", out var rawDigest) && rawDigest.ValueKind != JsonValueKind.Null);
        Assert.IsFalse(string.IsNullOrWhiteSpace(sourceAlias));
    }

    private static void AssertBoundaryAuthorityRefs(JsonElement classification, params string[] expectedAuthorities)
    {
        var actual = ReadStrings(classification.GetProperty("authorityRefs"));
        CollectionAssert.AreEquivalent(expectedAuthorities, actual);
    }

    private static ScreeningService BuildScreeningService(IReadOnlyList<string> candidateIds)
    {
        var candidates = candidateIds
            .Distinct(StringComparer.Ordinal)
            .Select(BuildCandidate)
            .ToArray();

        var dedupResult = new DeduplicationResult(
            "dedup-result-screening-fulltext",
            "nexus.deduplication.result",
            "1.0.0",
            null,
            null,
            0.95d,
            new Dictionary<string, int>(StringComparer.Ordinal),
            Array.Empty<string>(),
            Array.Empty<string>(),
            candidates,
            Array.Empty<DedupCluster>(),
            Array.Empty<DedupEvidence>(),
            Array.Empty<DedupCandidateRecord>(),
            Array.Empty<DedupReviewCandidate>(),
            Array.Empty<DedupMessage>(),
            Array.Empty<DedupMessage>(),
            Array.Empty<string>());

        var candidateSet = ScreeningService.CreateCandidateSetFromDedupResult(FullTextCandidateSetId, dedupResult, true);
        return new ScreeningService(candidateSet, Array.Empty<ScreeningCriteria>());
    }

    private static DedupCandidateRecord BuildCandidate(string candidateId)
    {
        return new DedupCandidateRecord(
            candidateId,
            $"Candidate {candidateId}",
            true,
            $"work-{candidateId}",
            new[] { $"work-{candidateId}" },
            new[] { "search" },
            new DedupSightingRef("search", "trace-screening-fulltext", $"sighting-{candidateId}", "openalex"));
    }

    private static ScreeningCriteria BuildCriteria(
        string criteriaId,
        string stage,
        JsonElement include,
        JsonElement exclude,
        bool requiresProtocolBinding = true)
    {
        return new ScreeningCriteria(
            criteriaId,
            CriteriaVersion,
            stage,
            CanonicalJsonValue.FromJsonElement(include),
            CanonicalJsonValue.FromJsonElement(exclude),
            requiresProtocolBinding,
            ApprovedProtocolBinding,
            ApprovedProtocolDigest,
            null,
            null,
            null,
            null,
            ApprovedProtocolDigestScope,
            ApprovedProtocolStatus,
            ApprovedProtocolDigest);
    }

    private static (ScreeningCriteria left, ScreeningCriteria right) BuildCriteriaPair(
        JsonElement includeA,
        JsonElement excludeA,
        JsonElement includeB,
        JsonElement excludeB)
    {
        var left = BuildCriteria("left", ScreeningStages.TitleAbstract, includeA, excludeA);
        var right = BuildCriteria("right", ScreeningStages.TitleAbstract, includeB, excludeB);
        return (left, right);
    }

    private static ScreeningDecision BuildDecision(
        string caseId,
        string candidateId,
        ScreeningCriteria criteria,
        string decision,
        string stage,
        ScreeningActor actor,
        double? confidence,
        string evidenceRef)
    {
        return new ScreeningDecision(
            $"decision-{caseId}",
            FullTextCandidateSetId,
            candidateId,
            null,
            null,
            stage,
            decision,
            actor,
            FixedTime,
            "php-golden fixture",
            confidence,
            criteria.CriteriaId,
            criteria.ComputeDigest().ToString(),
            [evidenceRef]);
    }

    private static void AddCouncilSuggestions(
        ScreeningService service,
        ScreeningCriteria criteria,
        JsonElement fixtureCase,
        Dictionary<string, JsonElement> expectedById)
    {
        var caseId = fixtureCase.GetProperty("id").GetString()!;
        var candidateId = fixtureCase.GetProperty("candidateId").GetString()!;
        var stage = fixtureCase.GetProperty("stage").GetString()!;

        var expectedResult = expectedById[caseId].GetProperty("result");
        foreach (var vote in fixtureCase.GetProperty("votes").EnumerateArray())
        {
            var modelId = vote.GetProperty("modelId").GetString()!;
            var verdict = vote.TryGetProperty("decision", out var decisionValue)
                ? decisionValue.GetString()!
                : ScreeningVerdicts.NeedsReview;
            var confidence = vote.TryGetProperty("confidence", out var confidenceValue)
                ? confidenceValue.GetDouble()
                : (double?)null;

            var suggestion = new ScreeningSuggestion(
                $"suggestion-{caseId}-{modelId}",
                FullTextCandidateSetId,
                candidateId,
                stage,
                verdict,
                confidence,
                expectedResult.GetProperty("rationale").GetProperty("reason").GetString()!,
                null,
                null,
                [EvidenceRefFixture()],
                null,
                Array.Empty<string>());

            service.AddSuggestion(suggestion);
        }
    }

    private static FullTextInput BuildFullTextInputFromCase(string candidateId, string verdict)
    {
        return FullTextInput.FromScreeningDecision(
            $"input-{candidateId}",
            FullTextCandidateSetId,
            candidateId,
            $"decision-{candidateId}",
            ScreeningStages.TitleAbstract,
            verdict);
    }

    private static FullTextAcquisitionRecord BuildAcquisition(
        FullTextInput input,
        string status,
        string acquisitionKind,
        string sourceAlias,
        int httpStatus,
        string artifactKind)
    {
        var attempt = new FullTextSourceAttempt(
            $"attempt-{input.CandidateId}",
            sourceAlias,
            1,
            acquisitionKind,
            status,
            artifactKind: artifactKind,
            sourceReference: input.CandidateId,
            mediaType: "application/pdf",
            httpStatus: httpStatus);

        return new FullTextAcquisitionRecord(
            $"acquisition-{input.CandidateId}",
            input,
            acquisitionKind,
            sourceAlias,
            "reference-screening-fulltext",
            new FullTextActor("human-fulltext", "human"),
            FixedTime,
            status,
            [attempt]);
    }

    private static Dictionary<string, JsonElement> BuildIndex(JsonElement cases)
    {
        return cases
            .EnumerateArray()
            .ToDictionary(
                c => c.GetProperty("id").GetString()!,
                c => c.Clone(),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, JsonElement> classificationIndex(JsonElement classifications)
    {
        return classifications
            .EnumerateArray()
            .ToDictionary(
                c => c.GetProperty("caseId").GetString()!,
                c => c.Clone(),
                StringComparer.Ordinal);
    }

    private static string classificationGetExpectedRule(JsonElement classification, string caseId)
    {
        if (caseId is "screening-decision-vocabulary"
            or "screening-stage-vocabulary"
            or "screening-confidence-below-zero-rejected"
            or "screening-confidence-above-one-rejected"
            or "fulltext-status-vocabulary"
            or "fulltext-failure-result"
            or "fulltext-skipped-result"
            or "fulltext-valid-pdf-validation"
            or "fulltext-invalid-pdf-signature"
            or "fulltext-oversized-artifact"
            or "fulltext-valid-xml-validation"
            or "fulltext-html-rejected-as-xml"
            or "fulltext-empty-text-rejected")
        {
            return "exact";
        }

        if (caseId is "fulltext-artifact-type-vocabulary")
        {
            return "shared-raw-artifact-subset";
        }

        if (caseId is "screening-criteria-key-order-stable")
        {
            return "key_order_stable";
        }

        if (caseId is "screening-criteria-list-order-semantic")
        {
            return "list_order_semantic";
        }

        return classification.GetProperty("comparisonRule").GetString()!;
    }

    private static string EvidenceRefFixture() => $"raw-artifact-bytes:{ContentDigest.Sha256Utf8("screening-fulltext-cases").ToString()}";

    private static JsonElement CanonicalIncludeSet() => JsonDocument.Parse("[\"systematic\", \"review\"]").RootElement;

    private static JsonElement CanonicalExcludeSet() => JsonDocument.Parse("[\"predatory\", \"irrelevant\"]").RootElement;

    private static string[] ReadStrings(JsonElement element) =>
        element.EnumerateArray().Select(value => value.GetString()!).ToArray();

    private static string[] ReadCanonicalArray(CanonicalJsonValue? value)
    {
        if (value is not CanonicalJsonArray array)
        {
            return [];
        }

        return array.Items.Select(item => item is CanonicalJsonString s ? s.Value : item.ToString()!).ToArray();
    }

    private static string[] ReadCanonicalArray(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static double ParseFixtureConfidence(JsonElement confidenceElement)
    {
        return confidenceElement.ValueKind == JsonValueKind.Number
            ? confidenceElement.GetDouble()
            : double.Parse(confidenceElement.GetString()!, CultureInfo.InvariantCulture);
    }

    private static string SourceAliasFromCase(JsonElement fixtureCase)
    {
        var source = fixtureCase.GetProperty("source");
        if (source.ValueKind == JsonValueKind.Object && source.TryGetProperty("id", out var sourceId))
        {
            return sourceId.GetString()!;
        }

        return source.GetString()!;
    }

    private static string DigestUtf8(string text) => $"sha256:{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))}";

    private static string DigestFixture(string fileName) => DigestFile(Path.Combine(FixtureDirectory(), fileName));

    private static string DigestFile(string path) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))}";

    private static JsonDocument Load(string fileName) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureDirectory(), fileName)));

    private static string FixtureDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "php-golden", "screening-fulltext", "v1");

    private static string SourceLockPath() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "php-golden", "SOURCE.lock.json");
}
