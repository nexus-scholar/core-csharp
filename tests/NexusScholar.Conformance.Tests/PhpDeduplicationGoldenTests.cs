using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Shared;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class PhpDeduplicationGoldenTests
{
    private const string FixtureSetId = "php-deduplication-v1";
    private const int ValidationYear = 2026;

    private static readonly string[] ExpectedCaseIds =
    {
        "corpus-slice-construction",
        "empty-handler",
        "exact-arxiv-policy",
        "exact-doi-policy",
        "exact-openalex-policy",
        "exact-pubmed-policy",
        "exact-s2-policy",
        "lock-export-with-snapshot",
        "lock-export-without-snapshot",
        "locked-dedup-rejected",
        "no-id-runtime-fallback",
        "representative-merge-handler",
        "singleton-handler",
        "title-default-92-vs-95",
        "title-explicit-95-auto-cluster",
        "transitive-handler"
    };

    private static readonly string[] ExactCaseIds =
    {
        "exact-doi-policy",
        "exact-arxiv-policy",
        "exact-openalex-policy",
        "exact-pubmed-policy",
        "exact-s2-policy",
        "empty-handler",
        "transitive-handler",
        "representative-merge-handler"
    };

    private static readonly string[] IntentionalCaseIds =
    {
        "singleton-handler",
        "title-default-92-vs-95",
        "title-explicit-95-auto-cluster",
        "no-id-runtime-fallback",
        "corpus-slice-construction",
        "locked-dedup-rejected",
        "lock-export-with-snapshot",
        "lock-export-without-snapshot"
    };

    private static readonly string[] SourceRefs =
    {
        "src/Deduplication/Application/DeduplicateCorpus.php",
        "src/Deduplication/Application/DeduplicateCorpusHandler.php",
        "src/Deduplication/Application/DeduplicateCorpusResult.php",
        "src/Deduplication/Domain/DedupCluster.php",
        "src/Deduplication/Domain/DedupClusterCollection.php",
        "src/Deduplication/Domain/Duplicate.php",
        "src/Deduplication/Domain/DuplicateReason.php",
        "src/Deduplication/Domain/Port/DeduplicationPolicyPort.php",
        "src/Deduplication/Domain/Port/RepresentativeElectionPort.php",
        "src/Deduplication/Infrastructure/CompletenessElectionPolicy.php",
        "src/Deduplication/Infrastructure/DoiMatchPolicy.php",
        "src/Deduplication/Infrastructure/NamespaceMatchPolicy.php",
        "src/Deduplication/Infrastructure/TitleFuzzyPolicy.php",
        "src/Deduplication/Infrastructure/TitleNormalizer.php",
        "src/Deduplication/Infrastructure/UnionFind.php",
        "src/Shared/Application/CorpusLockPolicy.php",
        "src/Shared/Exception/ProjectLockedException.php",
        "src/Shared/Port/CorpusSnapshotRepositoryPort.php",
        "src/Shared/Port/ProjectLockLifecyclePort.php",
        "src/Shared/Port/ProjectLockPort.php",
        "src/Shared/Port/ProjectWorkMembershipPort.php",
        "src/Shared/ValueObject/CorpusSnapshot.php",
        "src/Shared/ValueObject/CorpusOperation.php",
        "src/Shared/ValueObject/ProjectLockState.php",
        "src/Shared/Domain/CorpusSlice.php",
        "src/Shared/Domain/ScholarlyWork.php",
        "src/Shared/ValueObject/Author.php",
        "src/Shared/ValueObject/AuthorList.php",
        "src/Shared/ValueObject/WorkId.php",
        "src/Shared/ValueObject/WorkIdNamespace.php",
        "src/Shared/ValueObject/WorkIdSet.php",
        "src/Shared/ValueObject/Venue.php",
        "tests/Unit/Deduplication/DeduplicationTest.php",
        "tests/Unit/Deduplication/UnionFindTest.php",
        "tests/Unit/Shared/CorpusLockPolicyTest.php"
    };

    [TestMethod]
    public void Manifest_binds_pinned_source_lock_and_all_evidence_files()
    {
        using var manifest = Load("manifest.json");
        using var sourceLock = JsonDocument.Parse(File.ReadAllBytes(SourceLockPath()));
        var root = manifest.RootElement;
        var phpReference = sourceLock.RootElement.GetProperty("php_reference");

        Assert.AreEqual(FixtureSetId, root.GetProperty("fixtureSetId").GetString());
        Assert.AreEqual("pinned-php-observable-behavior", root.GetProperty("sourceKind").GetString());
        Assert.AreEqual(phpReference.GetProperty("repository").GetString(), root.GetProperty("sourceRepository").GetString());
        Assert.AreEqual(phpReference.GetProperty("commit").GetString(), root.GetProperty("sourceCommit").GetString());
        Assert.AreEqual("1.0.0", root.GetProperty("schemaVersion").GetString());
        Assert.AreEqual("deduplication-v1", root.GetProperty("generatorVersion").GetString());
        Assert.AreEqual(
            "php scripts/php-golden/deduplication-export.php --php-reference \"$PHP_REFERENCE\" --source-lock specs/SOURCE.lock.json --input fixtures/php-golden/deduplication/v1/input.json --comparison fixtures/php-golden/deduplication/v1/comparison.json --output fixtures/php-golden/deduplication/v1/expected.json --manifest fixtures/php-golden/deduplication/v1/manifest.json",
            root.GetProperty("generatorCommand").GetString());
        CollectionAssert.AreEqual(SourceRefs, ReadStrings(root.GetProperty("sourceRefs")));
        CollectionAssert.AreEqual(
            new[]
            {
                "PHP 8.3 or later",
                "git is available",
                "PHP reference tracked files are clean",
                "no network access or Composer dependencies are required",
                "title-fuzzy defaults use TitleFuzzyPolicy threshold 92 unless overridden",
                "stable fixture seed data avoids runtime-only identifiers",
                "UTF-8 JSON with LF line endings"
            },
            ReadStrings(root.GetProperty("environmentAssumptions")));
        CollectionAssert.AreEqual(
            new[]
            {
                "generated cluster ids",
                "runtime object hashes used for internal keying",
                "retrieved timestamps",
                "durationMs"
            },
            ReadStrings(root.GetProperty("ignoredNondeterminism")));
        CollectionAssert.AreEqual(
            new[]
            {
                "compare serialized member identifiers as normalized identifier sets",
                "compare duplicate evidence as normalized (primary, secondary, reason, confidence) tuples",
                "compare cluster member sets and counts; reason/confidence values are exact",
                "ignore generated cluster IDs and object hashes in semantic classification",
                "ignore retrieved timestamp fields unless the fixture explicitly pins them"
            },
            ReadStrings(root.GetProperty("comparisonRules")));
        Assert.AreEqual(DigestFixture("input.json"), root.GetProperty("inputDigest").GetString());
        Assert.AreEqual(DigestFixture("expected.json"), root.GetProperty("outputDigest").GetString());
        Assert.AreEqual(DigestFile(SourceLockPath()), root.GetProperty("sourceLockDigest").GetString());
        Assert.AreEqual(DigestFixture("comparison.json"), root.GetProperty("classificationDigest").GetString());
    }

    [TestMethod]
    public void Every_php_case_is_reviewed_and_inventory_is_pinned()
    {
        using var expected = Load("expected.json");
        using var comparison = Load("comparison.json");

        var expectedCaseIds = expected.RootElement.GetProperty("cases").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var classifications = comparison.RootElement.GetProperty("classifications").EnumerateArray()
            .ToArray();
        var classifiedCaseIds = classifications
            .Select(item => item.GetProperty("caseId").GetString()!)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var exactIds = classifications
            .Where(item => item.GetProperty("classification").GetString() == "equivalent_serialization")
            .Select(item => item.GetProperty("caseId").GetString()!)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var intentionalIds = classifications
            .Where(item => item.GetProperty("classification").GetString() == "intentional_change")
            .Select(item => item.GetProperty("caseId").GetString()!)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(ExpectedCaseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(), expectedCaseIds);
        CollectionAssert.AreEqual(ExpectedCaseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(), classifiedCaseIds);
        CollectionAssert.AreEqual(ExactCaseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(), exactIds);
        CollectionAssert.AreEqual(IntentionalCaseIds.OrderBy(item => item, StringComparer.Ordinal).ToArray(), intentionalIds);
        Assert.AreEqual(16, expectedCaseIds.Length);
        Assert.AreEqual(8, exactIds.Length);
        Assert.AreEqual(8, intentionalIds.Length);

        CollectionAssert.Contains(DeduplicationService.DefaultNonClaims.ToArray(), "no-broad-php-deduplication-compatibility");
        CollectionAssert.Contains(DeduplicationService.DefaultNonClaims.ToArray(), "no-corpus-lock-snapshot-compatibility");
        Assert.IsFalse(DeduplicationService.DefaultNonClaims.Contains("no-generated-php-fixture", StringComparer.Ordinal));

        foreach (var classification in classifications)
        {
            var kind = classification.GetProperty("classification").GetString();
            CollectionAssert.Contains(
                new[]
                {
                    "equivalent_serialization",
                    "intentional_change",
                    "php_defect",
                    "csharp_defect",
                    "unresolved_specification_conflict"
                },
                kind);
            Assert.AreNotEqual("csharp_defect", kind, "H27 cannot close with a known C# defect.");
            Assert.AreNotEqual("unresolved_specification_conflict", kind, "H27 cannot close with an unresolved specification conflict.");
            Assert.IsTrue(classification.GetProperty("authorityRefs").GetArrayLength() > 0);
        }
    }

    [TestMethod]
    public void Equivalent_php_cases_replay_semantically_with_csharp_behavior()
    {
        using var input = Load("input.json");
        using var expected = Load("expected.json");
        using var comparison = Load("comparison.json");
        var service = new DeduplicationService();

        var inputs = input.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.Clone(), StringComparer.Ordinal);
        var expectedCases = expected.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.Clone(), StringComparer.Ordinal);

        foreach (var classification in comparison.RootElement.GetProperty("classifications").EnumerateArray()
                     .Where(item => item.GetProperty("classification").GetString() == "equivalent_serialization"))
        {
            var caseId = classification.GetProperty("caseId").GetString()!;
            var fixtureCase = inputs[caseId];
            var operation = fixtureCase.GetProperty("operation").GetString()!;
            var expectedResult = expectedCases[caseId].GetProperty("result");

            switch (operation)
            {
                case "exact-doi-policy":
                    AssertEquivalentExactNamespaceCase(caseId, expectedResult, ReplayEquivalentCase(service, caseId, operation, fixtureCase), "doi_match");
                    break;
                case "exact-arxiv-policy":
                    AssertEquivalentExactNamespaceCase(caseId, expectedResult, ReplayEquivalentCase(service, caseId, operation, fixtureCase), "arxiv_match");
                    break;
                case "exact-openalex-policy":
                    AssertEquivalentExactNamespaceCase(caseId, expectedResult, ReplayEquivalentCase(service, caseId, operation, fixtureCase), "openalex_match");
                    break;
                case "exact-s2-policy":
                    AssertEquivalentExactNamespaceCase(caseId, expectedResult, ReplayEquivalentCase(service, caseId, operation, fixtureCase), "s2_match");
                    break;
                case "exact-pubmed-policy":
                    AssertEquivalentExactNamespaceCase(caseId, expectedResult, ReplayEquivalentCase(service, caseId, operation, fixtureCase), "pubmed_match");
                    break;
                case "empty-handler":
                    AssertEquivalentEmptyCase(caseId, expectedResult, ReplayEquivalentCase(service, caseId, operation, fixtureCase));
                    break;
                case "transitive-handler":
                    AssertEquivalentTransitiveCase(caseId, expectedResult, ReplayEquivalentCase(service, caseId, operation, fixtureCase));
                    break;
                case "representative-merge-handler":
                    AssertEquivalentRepresentativeMergeCase(caseId, fixtureCase, expectedResult, ReplayEquivalentCase(service, caseId, operation, fixtureCase));
                    break;
                default:
                    throw new AssertFailedException($"Unsupported equivalent operation '{operation}'.");
            }
        }
    }

    [TestMethod]
    public void Intentional_php_cases_preserve_documented_divergence_and_boundaries()
    {
        using var input = Load("input.json");
        using var expected = Load("expected.json");
        using var comparison = Load("comparison.json");
        var service = new DeduplicationService();

        var inputs = input.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.Clone(), StringComparer.Ordinal);
        var expectedCases = expected.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.Clone(), StringComparer.Ordinal);
        var classifications = comparison.RootElement.GetProperty("classifications").EnumerateArray()
            .ToDictionary(item => item.GetProperty("caseId").GetString()!, item => item, StringComparer.Ordinal);

        var singletonResult = expectedCases["singleton-handler"].GetProperty("result");
        Assert.AreEqual(1, singletonResult.GetProperty("clusterCount").GetInt32());
        var singletonActual = ReplayEquivalentCase(service, "singleton-handler", "singleton-handler", inputs["singleton-handler"]);
        Assert.AreEqual(0, singletonActual.Clusters.Count);
        Assert.AreEqual(1, singletonActual.RawCandidates.Count);
        CollectionAssert.Contains(AuthoritiesFor(classifications["singleton-handler"]), "docs/adr/0012-deduplication-evidence-and-cluster-contract.md");
        CollectionAssert.Contains(AuthoritiesFor(classifications["singleton-handler"]), "docs/adr/0007-shared-scientific-identity.md");

        var thresholdResult = expectedCases["title-default-92-vs-95"].GetProperty("result");
        Assert.AreEqual(1, thresholdResult.GetProperty("threshold92DuplicateCount").GetInt32());
        Assert.AreEqual(0, thresholdResult.GetProperty("threshold95DuplicateCount").GetInt32());
        var thresholdActual = ReplayEquivalentCase(service, "title-default-92-vs-95", "title-default-92-vs-95", inputs["title-default-92-vs-95"]);
        Assert.AreEqual(0, thresholdActual.Clusters.Count);
        Assert.AreEqual(0, thresholdActual.ReviewRequiredCandidates.Count);
        Assert.AreEqual(DeduplicationService.DefaultFuzzyTitleThreshold, thresholdActual.FuzzyTitleThreshold);
        CollectionAssert.Contains(AuthoritiesFor(classifications["title-default-92-vs-95"]), "docs/adr/0012-deduplication-evidence-and-cluster-contract.md");
        CollectionAssert.Contains(AuthoritiesFor(classifications["title-default-92-vs-95"]), "docs/adr/0007-shared-scientific-identity.md");

        var autoClusterPhp = expectedCases["title-explicit-95-auto-cluster"].GetProperty("result");
        Assert.AreEqual(1, autoClusterPhp.GetProperty("clusterCount").GetInt32());
        var autoClusterActual = ReplayEquivalentCase(service, "title-explicit-95-auto-cluster", "title-explicit-95-auto-cluster", inputs["title-explicit-95-auto-cluster"]);
        Assert.AreEqual(0, autoClusterActual.Clusters.Count);
        Assert.AreEqual(1, autoClusterActual.ReviewRequiredCandidates.Count);
        Assert.AreEqual(1, autoClusterActual.Evidence.Count(item => item.Kind == DedupEvidenceKind.FuzzyTitle));
        CollectionAssert.Contains(AuthoritiesFor(classifications["title-explicit-95-auto-cluster"]), "docs/adr/0012-deduplication-evidence-and-cluster-contract.md");
        CollectionAssert.Contains(AuthoritiesFor(classifications["title-explicit-95-auto-cluster"]), "docs/adr/0007-shared-scientific-identity.md");

        var noIdPhp = expectedCases["no-id-runtime-fallback"].GetProperty("result");
        Assert.AreEqual(3, noIdPhp.GetProperty("clusterCount").GetInt32());
        var noIdActual = ReplayEquivalentCase(service, "no-id-runtime-fallback", "no-id-runtime-fallback", inputs["no-id-runtime-fallback"]);
        Assert.AreEqual(0, noIdActual.Clusters.Count);
        Assert.AreEqual(3, noIdActual.RawCandidates.Count);
        Assert.AreEqual(3, noIdActual.UnresolvedCandidates.Count);
        Assert.AreEqual(0, noIdActual.ReviewRequiredCandidates.Count);
        CollectionAssert.Contains(AuthoritiesFor(classifications["no-id-runtime-fallback"]), "docs/adr/0012-deduplication-evidence-and-cluster-contract.md");
        CollectionAssert.Contains(AuthoritiesFor(classifications["no-id-runtime-fallback"]), "docs/adr/0007-shared-scientific-identity.md");

        var slicePhp = expectedCases["corpus-slice-construction"].GetProperty("result");
        Assert.AreEqual(1, slicePhp.GetProperty("safeCount").GetInt32());
        Assert.AreEqual(2, slicePhp.GetProperty("unsafeCount").GetInt32());
        var sliceActual = ReplayEquivalentCase(service, "corpus-slice-construction", "corpus-slice-construction", inputs["corpus-slice-construction"]);
        Assert.AreEqual(2, sliceActual.RawCandidates.Count);
        Assert.AreEqual(2, sliceActual.RawCandidates.Count(candidate => candidate.Source.SourceKind == "search"));
        CollectionAssert.Contains(AuthoritiesFor(classifications["corpus-slice-construction"]), "docs/adr/0012-deduplication-evidence-and-cluster-contract.md");

        var lockedPhp = expectedCases["locked-dedup-rejected"].GetProperty("result");
        Assert.AreEqual(false, lockedPhp.GetProperty("accepted").GetBoolean());
        Assert.AreEqual("project-locked", lockedPhp.GetProperty("errorCategory").GetString());
        AssertLockBoundary(
            classifications["locked-dedup-rejected"],
            "lock-gate-semantic-subset");

        var snapshotPhp = expectedCases["lock-export-with-snapshot"].GetProperty("result");
        var snapshotMetadata = snapshotPhp.GetProperty("lockMetadata");
        Assert.IsTrue(snapshotMetadata.GetProperty("project_locked").GetBoolean());
        Assert.AreEqual("locked", snapshotMetadata.GetProperty("lock_status").GetString());
        Assert.IsTrue(snapshotMetadata.GetProperty("snapshot_present").GetBoolean());
        Assert.AreEqual(2, snapshotMetadata.GetProperty("snapshot_work_count").GetInt32());
        Assert.IsTrue(snapshotMetadata.GetProperty("citable").GetBoolean());
        Assert.IsTrue(snapshotMetadata.GetProperty("final").GetBoolean());
        AssertLockBoundary(
            classifications["lock-export-with-snapshot"],
            "lock-metadata-subset");

        var noSnapshotPhp = expectedCases["lock-export-without-snapshot"].GetProperty("result");
        var noSnapshotMetadata = noSnapshotPhp.GetProperty("lockMetadata");
        Assert.IsTrue(noSnapshotMetadata.GetProperty("project_locked").GetBoolean());
        Assert.AreEqual("locked", noSnapshotMetadata.GetProperty("lock_status").GetString());
        Assert.IsFalse(noSnapshotMetadata.GetProperty("snapshot_present").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, noSnapshotMetadata.GetProperty("snapshot_work_count").ValueKind);
        Assert.IsFalse(noSnapshotMetadata.GetProperty("citable").GetBoolean());
        Assert.IsFalse(noSnapshotMetadata.GetProperty("final").GetBoolean());
        AssertLockBoundary(
            classifications["lock-export-without-snapshot"],
            "lock-metadata-subset");
    }

    private static void AssertLockBoundary(JsonElement classification, string expectedComparisonRule)
    {
        Assert.AreEqual("intentional_change", classification.GetProperty("classification").GetString());
        Assert.AreEqual(expectedComparisonRule, classification.GetProperty("comparisonRule").GetString());
        CollectionAssert.AreEqual(
            new[] { "docs/adr/0026-phase-7-corpus-lock-snapshot-compatibility-boundary.md" },
            AuthoritiesFor(classification));
        StringAssert.Contains(
            classification.GetProperty("rationale").GetString(),
            "intentionally not adopted as C#");
    }

    private static DeduplicationResult ReplayEquivalentCase(
        DeduplicationService service,
        string caseId,
        string operation,
        JsonElement fixtureCase)
    {
        var works = ReadWorks(caseId, fixtureCase);

        if (operation == "representative-merge-handler")
        {
            var importTrace = BuildImportTrace(caseId, works);
            return service.Execute("dedup-replay", Array.Empty<SearchTrace>(), new[] { importTrace });
        }

        if (operation == "title-explicit-95-auto-cluster")
        {
            return service.Execute("dedup-replay", new[] { BuildSearchTrace(caseId, works) }, Array.Empty<SearchImportTrace>());
        }

        return service.Execute("dedup-replay", new[] { BuildSearchTrace(caseId, works) }, Array.Empty<SearchImportTrace>());
    }

    private static void AssertEquivalentExactNamespaceCase(string caseId, JsonElement expected, DeduplicationResult result, string expectedPolicy)
    {
        Assert.AreEqual(1, result.Clusters.Count, caseId);
        Assert.AreEqual(2, result.Clusters[0].Members.Count, caseId);

        var exactEvidence = result.Clusters[0].Evidence.Where(item => item.Kind == DedupEvidenceKind.ExactIdentifier).ToArray();
        Assert.AreEqual(expected.GetProperty("duplicateCount").GetInt32(), exactEvidence.Length, caseId);

        var evidence = exactEvidence.Single();
        var subject = result.RawCandidates.Single(item => item.CandidateId == evidence.SubjectCandidateId);
        var @object = result.RawCandidates.Single(item => item.CandidateId == evidence.ObjectCandidateId);
        var expectedDuplicate = expected.GetProperty("duplicates").EnumerateArray().Single();
        var expectedPrimary = expectedDuplicate.GetProperty("primary").GetString()!;
        var expectedSecondary = expectedDuplicate.GetProperty("secondary").GetString()!;
        var pairFromExpected = OrderedPair(expectedPrimary, expectedSecondary);
        var pairObserved = OrderedPair(PrimaryWorkId(subject), PrimaryWorkId(@object));
        Assert.AreEqual(pairFromExpected, pairObserved, caseId);
        Assert.AreEqual(expectedPolicy, InferPolicyFromOverlap(subject, @object), caseId);
        Assert.AreEqual(1d, evidence.Score ?? 0d, 0.0001d, caseId);

        Assert.AreEqual(2, result.RawCandidates.Count, caseId);
        Assert.AreEqual(0, result.ReviewRequiredCandidates.Count, caseId);
        Assert.AreEqual(0, result.UnresolvedCandidates.Count, caseId);
    }

    private static void AssertEquivalentEmptyCase(string caseId, JsonElement expected, DeduplicationResult result)
    {
        Assert.AreEqual(expected.GetProperty("inputCount").GetInt32(), result.RawCandidates.Count, caseId);
        Assert.AreEqual(0, result.Clusters.Count, caseId);
        Assert.AreEqual(expected.GetProperty("clusterCount").GetInt32(), result.Clusters.Count, caseId);
        Assert.AreEqual(0, expected.GetProperty("uniqueCount").GetInt32(), caseId);
        Assert.AreEqual(0, expected.GetProperty("duplicatesRemoved").GetInt32(), caseId);
    }

    private static void AssertEquivalentTransitiveCase(string caseId, JsonElement expected, DeduplicationResult result)
    {
        Assert.AreEqual(1, result.Clusters.Count, caseId);
        var cluster = result.Clusters.Single();
        Assert.AreEqual(3, cluster.Members.Count, caseId);
        var expectedCluster = expected.GetProperty("clusters")[0];

        var exactEvidence = cluster.Evidence.Where(item => item.Kind == DedupEvidenceKind.ExactIdentifier).ToArray();
        Assert.AreEqual(expectedCluster.GetProperty("duplicateCount").GetInt32(), exactEvidence.Length, caseId);

        var observedPairs = exactEvidence
            .Select(item =>
            {
                var left = result.RawCandidates.Single(candidate => candidate.CandidateId == item.SubjectCandidateId);
                var right = result.RawCandidates.Single(candidate => candidate.CandidateId == item.ObjectCandidateId);
                return new
                {
                    Pair = OrderedPair(PrimaryWorkId(left), PrimaryWorkId(right)),
                    Reason = InferPolicyFromOverlap(left, right),
                    Confidence = item.Score ?? 0d
                };
            })
            .ToArray();

        foreach (var expectedItem in expectedCluster.GetProperty("evidence").EnumerateArray())
        {
            var expectedPair = OrderedPair(
                expectedItem.GetProperty("primary").GetString()!,
                expectedItem.GetProperty("secondary").GetString()!);
            var expectedReason = expectedItem.GetProperty("reason").GetString()!;
            var expectedConfidence = expectedItem.GetProperty("confidence").GetDouble();

            Assert.IsTrue(observedPairs.Any(observed =>
                    observed.Pair == expectedPair &&
                    observed.Reason == expectedReason &&
                    expectedConfidence.Equals(observed.Confidence)),
                caseId);
        }

        Assert.AreEqual(expectedCluster.GetProperty("memberCount").GetInt32(), cluster.Members.Count, caseId);
    }

    private static void AssertEquivalentRepresentativeMergeCase(
        string caseId,
        JsonElement fixtureCase,
        JsonElement expected,
        DeduplicationResult result)
    {
        Assert.AreEqual(1, result.Clusters.Count, caseId);
        var cluster = result.Clusters.Single();
        Assert.AreEqual(2, cluster.Members.Count, caseId);
        Assert.AreEqual(1, cluster.Evidence.Count(item => item.Kind == DedupEvidenceKind.ExactIdentifier), caseId);

        var representative = cluster.Representative;
        var works = fixtureCase.GetProperty("works").EnumerateArray().ToArray();
        var baseWork = works[0];
        var fillWork = works[1];
        var baseVenue = ReadVenue(baseWork);
        var fillVenue = ReadVenue(fillWork);
        var baseAbstract = baseWork.TryGetProperty("abstract", out var baseAbstractElement) && baseAbstractElement.ValueKind != JsonValueKind.Null
            ? baseAbstractElement.GetString()
            : null;
        int? baseYear = baseWork.TryGetProperty("year", out var baseYearElement) && baseYearElement.ValueKind != JsonValueKind.Null
            ? baseYearElement.GetInt32()
            : null;

        Assert.AreEqual("Representative Merge Base", representative.Title, caseId);
        Assert.AreEqual(baseYear, representative.Year, caseId);
        Assert.AreEqual(baseAbstract, representative.Abstract, caseId);
        if (string.IsNullOrWhiteSpace(baseVenue))
        {
            Assert.AreEqual(fillVenue, representative.Venue, caseId);
        }
        else
        {
            Assert.AreEqual(baseVenue, representative.Venue, caseId);
        }
        var expectedRepresentative = expected.GetProperty("clusters")[0].GetProperty("representative");
        var expectedRepIds = expectedRepresentative.GetProperty("ids").EnumerateArray().Select(id => id.GetString()!).ToArray();
        foreach (var expectedRepId in expectedRepIds)
        {
            CollectionAssert.Contains(representative.WorkIds.ToArray(), expectedRepId, caseId);
        }
        CollectionAssert.Contains(representative.ReasonCodes.ToArray(), "missing-field-fill");

        var expectedFused = expected.GetProperty("fusedRepresentative");
        CollectionAssert.AreEquivalent(
            expectedFused.GetProperty("ids").EnumerateArray().Select(id => id.GetString()!).ToArray(),
            representative.WorkIds.ToArray(),
            caseId);
        Assert.AreEqual(expectedFused.GetProperty("title").GetString(), representative.Title, caseId);
        Assert.AreEqual(expectedFused.GetProperty("year").GetInt32(), representative.Year, caseId);
        Assert.AreEqual(expectedFused.GetProperty("hasAbstract").GetBoolean(), !string.IsNullOrWhiteSpace(representative.Abstract), caseId);
        Assert.AreEqual(expectedFused.GetProperty("hasVenue").GetBoolean(), !string.IsNullOrWhiteSpace(representative.Venue), caseId);
    }

    private static SearchTrace BuildSearchTrace(string caseId, WorkInput[] works)
    {
        var request = new SearchTraceRequest(
            "dedup",
            SearchYearRange.Validate(null, null, ValidationYear),
            null,
            25,
            0,
            false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null);

        var cacheIdentity = SearchCacheIdentity.Compute(
            new SearchQueryInput("dedup", null, null, null, 25, 0, false, Array.Empty<string>()),
            ValidationYear,
            Array.Empty<string>());
        var sightings = works.Select((work, index) => BuildSearchSighting(work, index + 1)).ToArray();

        return new SearchTrace(
            caseId,
            SearchTrace.TraceSchemaId,
            SearchTrace.TraceSchemaVersion,
            request,
            cacheIdentity,
            new ReadOnlyCollection<SearchProviderAttempt>(Array.Empty<SearchProviderAttempt>()),
            new ReadOnlyCollection<SearchProviderStat>(Array.Empty<SearchProviderStat>()),
            new ReadOnlyCollection<SearchSighting>(sightings),
            new SearchSummary(sightings.Length, 0, 0, sightings.Length, false),
            SearchTrace.DefaultNonClaims);
    }

    private static SearchSighting BuildSearchSighting(WorkInput work, int index)
    {
        if (work.WorkIds.Length == 0)
        {
            return new SearchSighting(
                work.Provider,
                index,
                1,
                ScholarlyWork.UnresolvedCandidate(work.Title, work.SourceContext));
        }

        return new SearchSighting(
            work.Provider,
            index,
            1,
            ScholarlyWork.Identified(work.Title, WorkIdSet.From(work.WorkIds), work.SourceContext));
    }

    private static SearchImportTrace BuildImportTrace(string caseId, WorkInput[] works)
    {
        var records = works
            .Select((work, index) => BuildImportRecord(caseId, work, index + 1))
            .ToArray();

        var metadata = new SearchImportMetadata(
            SearchImportMetadata.AcquisitionKindImportedExport,
            "fixture",
            "dedup-golden",
            "fixture-parser",
            "1.0.0",
            "sha256:111111111111111111111111111111111111111111111111111111111111111111",
            "raw-artifact-bytes",
            "fixture-operator",
            "2026-06-27T00:00:00Z",
            null,
            null,
            records.Length,
            Array.Empty<SearchImportParserNotice>());

        return new SearchImportTrace(
            caseId,
            "nexus.search.import.trace",
            "1.0.0",
            metadata,
            new ReadOnlyCollection<SearchImportRecord>(records),
            Array.Empty<SearchSighting>(),
            new ReadOnlyCollection<SearchImportParserNotice>(Array.Empty<SearchImportParserNotice>()),
            SearchImportTrace.DefaultNonClaims);
    }

    private static SearchImportRecord BuildImportRecord(string caseId, WorkInput work, int index)
    {
        var sourceRecordId = $"{caseId}-{index}";
        if (work.WorkIds.Length == 0)
        {
            return new SearchImportRecord(
                work.Provider,
                sourceRecordId,
                null,
                Array.Empty<string>(),
                ScholarlyWork.UnresolvedCandidate(work.Title, $"import:{sourceRecordId}"),
                Array.Empty<string>(),
                work.Year,
                work.Venue,
                work.Abstract,
                Array.Empty<string>(),
                null,
                null,
                false,
                null,
                Array.Empty<SearchImportParserNotice>());
        }

        return new SearchImportRecord(
            work.Provider,
            sourceRecordId,
            null,
            Array.Empty<string>(),
            ScholarlyWork.Identified(work.Title, WorkIdSet.From(work.WorkIds), $"import:{caseId}:{index}"),
            Array.Empty<string>(),
            work.Year,
            work.Venue,
            work.Abstract,
            Array.Empty<string>(),
            null,
            null,
            false,
            null,
            Array.Empty<SearchImportParserNotice>());
    }

    private static string? ReadVenue(JsonElement work)
    {
        if (work.TryGetProperty("venue", out var venueElement) && venueElement.ValueKind == JsonValueKind.Object &&
            venueElement.TryGetProperty("name", out var venueName))
        {
            return venueName.GetString();
        }

        return null;
    }

    private static WorkInput[] ReadWorks(string caseId, JsonElement fixtureCase)
    {
        if (!fixtureCase.TryGetProperty("works", out var worksElement))
        {
            return Array.Empty<WorkInput>();
        }

        return worksElement
            .EnumerateArray()
            .Select((item, index) =>
            {
                var provider = item.GetProperty("sourceProvider").GetString()!;
                var title = item.GetProperty("title").GetString()!;
                var year = item.TryGetProperty("year", out var yearElement) && yearElement.ValueKind != JsonValueKind.Null
                ? yearElement.GetInt32()
                : (int?)null;
                var abstractText = item.TryGetProperty("abstract", out var abstractElement) && abstractElement.ValueKind != JsonValueKind.Null
                    ? abstractElement.GetString()
                    : null;
                var venue = item.TryGetProperty("venue", out var venueElement) && venueElement.ValueKind != JsonValueKind.Null
                    ? venueElement.GetProperty("name").GetString()
                    : null;
                var workIds = item.TryGetProperty("ids", out var idsElement)
                    ? idsElement.EnumerateArray()
                        .Select(id => WorkId.From(id.GetProperty("namespace").GetString()!, id.GetProperty("value").GetString()!))
                        .ToArray()
                    : Array.Empty<WorkId>();

                return new WorkInput(
                    provider,
                    title,
                    year,
                    venue,
                    abstractText,
                    workIds,
                    $"{caseId}:{index + 1}");
            })
            .ToArray();
    }

    private static string InferPolicyFromOverlap(DedupCandidateRecord left, DedupCandidateRecord right)
    {
        var overlap = left.WorkIds.Intersect(right.WorkIds, StringComparer.Ordinal).ToArray();
        if (overlap.Any(value => value.StartsWith("doi:", StringComparison.Ordinal)))
        {
            return "doi_match";
        }

        if (overlap.Any(value => value.StartsWith("arxiv:", StringComparison.Ordinal)))
        {
            return "arxiv_match";
        }

        if (overlap.Any(value => value.StartsWith("openalex:", StringComparison.Ordinal)))
        {
            return "openalex_match";
        }

        if (overlap.Any(value => value.StartsWith("s2:", StringComparison.Ordinal)))
        {
            return "s2_match";
        }

        if (overlap.Any(value => value.StartsWith("pubmed:", StringComparison.Ordinal)))
        {
            return "pubmed_match";
        }

        return "unknown-policy";
    }

    private static string PrimaryWorkId(DedupCandidateRecord candidate)
        => candidate.PrimaryWorkId ?? candidate.WorkIds.First();

    private static string OrderedPair(string left, string right)
            => string.Compare(left, right, StringComparison.Ordinal) <= 0 ? $"{left}:{right}" : $"{right}:{left}";

    private static string[] AuthoritiesFor(JsonElement classification) =>
        classification.GetProperty("authorityRefs").EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();

    private static string[] ReadStrings(JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static string DigestFixture(string fileName) => DigestFile(Path.Combine(FixtureDirectory(), fileName));

    private static string DigestFile(string path) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))}";

    private static JsonDocument Load(string fileName) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureDirectory(), fileName)));

    private static string FixtureDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "php-golden", "deduplication", "v1");

    private static string SourceLockPath() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "php-golden", "SOURCE.lock.json");

    private readonly record struct WorkInput(
        string Provider,
        string Title,
        int? Year,
        string? Venue,
        string? Abstract,
        WorkId[] WorkIds,
        string SourceContext);
}
