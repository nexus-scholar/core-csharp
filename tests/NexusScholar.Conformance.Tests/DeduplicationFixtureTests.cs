using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Shared;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class DeduplicationFixtureTests
{
    private const int ValidationYear = 2026;
    private const string FixtureSourceKind = "local-gate-9-dedup-implementation";
    private const string FixtureSourceCommit = "local-gate-9-dedup-local";
    private const string SearchImportSchemaId = "nexus.search.import.trace";
    private const string SearchImportSchemaVersion = "1.0.0";
    private static readonly string FixtureDirectory =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "deduplication");

    private static readonly string[] RequiredFixtureIds =
    {
        "dedup-exact-doi-cluster",
        "dedup-exact-cross-provider-id-cluster",
        "dedup-transitive-cluster",
        "dedup-fuzzy-title-review-required",
        "dedup-threshold-95-boundary",
        "dedup-no-id-title-only-no-auto-merge",
        "dedup-representative-election",
        "dedup-representative-merge-preserves-evidence",
        "dedup-raw-sightings-preserved",
        "dedup-web-app-projection-not-authority",
        "dedup-source-specific-id-not-workid-review-only"
    };

    [TestMethod]
    public void Gate_9_dedup_fixture_files_are_present()
    {
        Directory.CreateDirectory(FixtureDirectory);
        var files = Directory.GetFiles(FixtureDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fixtureId in RequiredFixtureIds)
        {
            Assert.IsTrue(files.Contains(fixtureId), $"Missing fixture '{fixtureId}.json'.");
        }
    }

    [TestMethod]
    public void Gate_9_dedup_fixtures_have_required_local_metadata()
    {
        foreach (var fixtureId in RequiredFixtureIds)
        {
            using var document = LoadFixture($"{fixtureId}.json");
            var root = document.RootElement;

            Assert.AreEqual(FixtureSourceKind, root.GetProperty("sourceKind").GetString(), fixtureId);
            Assert.AreEqual(FixtureSourceCommit, root.GetProperty("sourceCommit").GetString(), fixtureId);
            Assert.AreEqual("hand-authored local Gate 9 Dedup fixture", root.GetProperty("generatorCommand").GetString(), fixtureId);
            Assert.AreEqual("gate-9-dedup-v1", root.GetProperty("generatorVersion").GetString(), fixtureId);

            var sourceRefs = root.GetProperty("sourceRefs").EnumerateArray().Select(value => value.GetString()).ToArray();
            Assert.IsTrue(sourceRefs.Contains("docs/adr/0012-deduplication-evidence-and-cluster-contract.md", StringComparer.Ordinal), fixtureId);
            var comparisonRules = root.GetProperty("comparisonRules").EnumerateArray().Select(value => value.GetString()).ToArray();
            Assert.IsTrue(comparisonRules.Contains("no-php-compatibility-claim", StringComparer.Ordinal), fixtureId);
            Assert.IsTrue(comparisonRules.Contains("no-generated-php-fixture", StringComparer.Ordinal), fixtureId);

            _ = ContentDigest.Parse(root.GetProperty("inputDigest").GetString()!);
            _ = ContentDigest.Parse(root.GetProperty("outputDigest").GetString()!);
            Assert.IsTrue(root.TryGetProperty("case", out _), fixtureId);
        }
    }

    [TestMethod]
    public void Gate_9_dedup_fixtures_replay_and_match_expected_clustering_and_evidence_shape()
    {
        var service = new DeduplicationService();

        foreach (var fixtureId in RequiredFixtureIds)
        {
            using var document = LoadFixture($"{fixtureId}.json");
            var root = document.RootElement;
            var @case = root.GetProperty("case");

            var searchTraces = ReadSearchTraces(fixtureId, @case);
            var importTraces = ReadImportTraces(fixtureId, @case);
            var threshold = @case.TryGetProperty("expected", out var expectedElement) &&
                expectedElement.ValueKind == JsonValueKind.Object &&
                expectedElement.TryGetProperty("threshold", out var thresholdElement)
                ? thresholdElement.GetDouble()
                : 0.95d;

            var result = service.Execute(
                $"dedup-result-{fixtureId}",
                searchTraces,
                importTraces,
                threshold);

            var expected = @case.GetProperty("expected");
            Assert.AreEqual(GetOptionalInt(expected, "clusters"), result.Clusters.Count, fixtureId);
            Assert.AreEqual(GetOptionalInt(expected, "reviewRequired"), result.ReviewRequiredCandidates.Count, fixtureId);
            Assert.AreEqual(GetOptionalInt(expected, "unresolved"), result.UnresolvedCandidates.Count, fixtureId);

            if (expected.TryGetProperty("rawCandidates", out var rawCandidates))
            {
                Assert.AreEqual(rawCandidates.GetInt32(), result.RawCandidates.Count, fixtureId);
            }

            if (expected.TryGetProperty("sourceSearchTraces", out var sourceSearchTraceCount))
            {
                Assert.AreEqual(sourceSearchTraceCount.GetInt32(), result.SourceSearchTraceIds.Count, fixtureId);
            }

            if (expected.TryGetProperty("sourceImportTraces", out var sourceImportTraceCount))
            {
                Assert.AreEqual(sourceImportTraceCount.GetInt32(), result.SourceImportTraceIds.Count, fixtureId);
            }

            if (expected.TryGetProperty("clusterSize", out var clusterSizeElement))
            {
                Assert.IsTrue(result.Clusters.Count >= 1, fixtureId);
                Assert.AreEqual(clusterSizeElement.GetInt32(), result.Clusters[0].Members.Count, fixtureId);
            }

            if (expected.TryGetProperty("members", out var membersElement))
            {
                Assert.IsTrue(result.Clusters.Count >= 1, fixtureId);
                Assert.AreEqual(membersElement.GetInt32(), result.Clusters[0].Members.Count, fixtureId);
            }

            foreach (var cluster in result.Clusters)
            {
                var memberWorkIds = cluster.Members
                    .SelectMany(member => member.WorkIds)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

                var expectedMemberWorkIds = cluster.Representative.WorkIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

                Assert.AreEqual(
                    memberWorkIds.Length,
                    expectedMemberWorkIds.Length,
                    $"{fixtureId} representative should union all member work ids");

                CollectionAssert.AreEqual(
                    expectedMemberWorkIds,
                    memberWorkIds,
                    $"{fixtureId}: representative-work-id-union");

                var expectedSightingIds = cluster.Members
                    .Select(member => member.Source.SourceSightingId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

                var representativeSightingIds = cluster.Representative.SourceSightingIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();

                CollectionAssert.AreEquivalent(
                    expectedSightingIds,
                    representativeSightingIds,
                    $"{fixtureId}: representative should project all source sightings");

                Assert.AreEqual(0, cluster.Representative.ReasonCodes.Count(code => string.IsNullOrWhiteSpace(code)), $"{fixtureId}: representative reasons");
            }

            if (expected.TryGetProperty("containsEvidenceTypes", out var expectedEvidenceTypes))
            {
                var observedEvidenceTypes = result
                    .Clusters
                    .SelectMany(cluster => cluster.Evidence)
                    .Select(item => item.Kind.ToString())
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var expectedEvidenceType in expectedEvidenceTypes.EnumerateArray().Select(item => item.GetString()))
                {
                    Assert.IsTrue(
                        observedEvidenceTypes.Contains(expectedEvidenceType, StringComparer.Ordinal),
                        $"Fixture '{fixtureId}' missing evidence type '{expectedEvidenceType}'.");
                }
            }

            Assert.AreEqual(
                threshold,
                result.FuzzyTitleThreshold.GetValueOrDefault(),
                0.0001d,
                $"{fixtureId}: threshold passthrough");

            foreach (var comparisonRule in root.GetProperty("comparisonRules").EnumerateArray())
            {
                var expectedRule = comparisonRule.GetString();
                if (expectedRule is not null && expectedRule.StartsWith("no-", StringComparison.Ordinal))
                {
                    CollectionAssert.Contains(result.NonClaims.ToArray(), expectedRule, $"{fixtureId} non-claim expectation");
                }
            }

            if (expected.TryGetProperty("expectedRepresentativeByTieBreaks", out var representativeByTieBreak))
            {
                Assert.AreEqual(1, result.Clusters.Count, fixtureId);
                Assert.AreEqual(
                    representativeByTieBreak.GetString(),
                    result.Clusters[0].Representative.CandidateId,
                    fixtureId);
            }
        }
    }

    private static IReadOnlyList<SearchTrace> ReadSearchTraces(string fixtureId, JsonElement caseElement)
    {
        if (!caseElement.TryGetProperty("searchTraces", out var searchTracesElement))
        {
            return Array.Empty<SearchTrace>();
        }

        return searchTracesElement
            .EnumerateArray()
            .Select((traceElement, traceIndex) =>
            {
                var traceId = traceElement.GetProperty("traceId").GetString() ?? $"search-trace-{traceIndex}";
                var request = BuildSearchTraceRequest("dedup");
                var cacheIdentity = SearchCacheIdentity.Compute(
                    new SearchQueryInput("dedup", null, null, null, 25, 0, false, Array.Empty<string>()),
                    ValidationYear,
                    Array.Empty<string>());
                var sightings = ReadSearchSightings(fixtureId, traceId, traceElement);

                return new SearchTrace(
                    traceId,
                    SearchTrace.TraceSchemaId,
                    SearchTrace.TraceSchemaVersion,
                    request,
                    cacheIdentity,
                    Array.Empty<SearchProviderAttempt>(),
                    Array.Empty<SearchProviderStat>(),
                    sightings,
                    new SearchSummary(
                        AttemptedProviders: 0,
                        SucceededProviders: 0,
                        FailedProviders: 0,
                        RawSightingCount: sightings.Count,
                        AllFailed: false),
                    SearchTrace.DefaultNonClaims);
            })
            .ToArray();
    }

    private static IReadOnlyList<SearchImportTrace> ReadImportTraces(string fixtureId, JsonElement caseElement)
    {
        if (!caseElement.TryGetProperty("importTraces", out var importTracesElement))
        {
            return Array.Empty<SearchImportTrace>();
        }

        return importTracesElement
            .EnumerateArray()
            .Select((traceElement, traceIndex) =>
            {
                var traceId = traceElement.GetProperty("traceId").GetString() ?? $"import-trace-{traceIndex}";
                var records = ReadImportRecords(fixtureId, traceId, traceElement);

                var metadata = new SearchImportMetadata(
                    SearchImportMetadata.AcquisitionKindImportedExport,
                    "fixture-dedup",
                    "dedup-fixture",
                    "dedup-parser",
                    "1.0.0",
                    $"sha256:{traceId}-source-file-digest",
                    DigestScope.RawArtifactBytes.ToString(),
                    "import-operator",
                    "2026-06-27T00:00:00Z",
                    null,
                    null,
                    records.Count,
                    Array.Empty<SearchImportParserNotice>());

                return new SearchImportTrace(
                    traceId,
                    SearchImportSchemaId,
                    SearchImportSchemaVersion,
                    metadata,
                    records,
                    Array.Empty<SearchSighting>().AsReadOnly(),
                    Array.Empty<SearchImportParserNotice>(),
                    SearchImportTrace.DefaultNonClaims);
            })
            .ToArray();
    }

    private static SearchTraceRequest BuildSearchTraceRequest(string query)
    {
        return new SearchTraceRequest(
            query,
            null,
            null,
            25,
            0,
            false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null);
    }

    private static IReadOnlyList<SearchSighting> ReadSearchSightings(string fixtureId, string traceId, JsonElement traceElement)
    {
        if (!traceElement.TryGetProperty("sightings", out var sightingsElement))
        {
            return Array.Empty<SearchSighting>();
        }

        var index = 0;
        return sightingsElement
            .EnumerateArray()
            .Select(sightingElement =>
            {
                var providerAlias = sightingElement.GetProperty("providerAlias").GetString() ?? "openalex";
                var providerOrder = sightingElement.GetProperty("providerOrder").GetInt32();
                var providerLocalRank = sightingElement.GetProperty("providerLocalRank").GetInt32();
                var title = sightingElement.GetProperty("title").GetString() ?? string.Empty;
                var isUnresolved = sightingElement.GetProperty("isUnresolved").GetBoolean();
                var workIds = ReadWorkIdSet(fixtureId, sightingElement);
                var sourceContext = $"search:{traceId}:{++index}:{providerAlias}:{providerOrder}:{providerLocalRank}";

                var work = isUnresolved || workIds.Length == 0
                    ? ScholarlyWork.UnresolvedCandidate(title, sourceContext)
                    : ScholarlyWork.Identified(title, WorkIdSet.From(workIds));

                return new SearchSighting(providerAlias, providerOrder, providerLocalRank, work);
            })
            .ToArray();
    }

    private static IReadOnlyList<SearchImportRecord> ReadImportRecords(string fixtureId, string traceId, JsonElement traceElement)
    {
        if (!traceElement.TryGetProperty("records", out var recordsElement))
        {
            return Array.Empty<SearchImportRecord>();
        }

        return recordsElement
            .EnumerateArray()
            .Select((recordElement, recordIndex) =>
            {
                var sourceDatabaseOrTool = recordElement.GetProperty("sourceDatabaseOrTool").GetString() ?? "import";
                var sourceRecordId = recordElement.GetProperty("sourceRecordId").GetString() ?? $"record-{traceId}-{recordIndex}";
                var title = recordElement.GetProperty("title").GetString() ?? string.Empty;
                var isUnresolved = recordElement.TryGetProperty("isUnresolved", out var unresolvedElement) && unresolvedElement.GetBoolean();
                var workIds = ReadWorkIdSet(fixtureId, recordElement);
                var sourceIdentifiers = recordElement.TryGetProperty("sourceIdentifiers", out var sourceIdentifierElement)
                    ? ReadStringArray(sourceIdentifierElement)
                    : Array.Empty<string>();

                var work = isUnresolved || workIds.Length == 0
                    ? ScholarlyWork.UnresolvedCandidate(title, $"import:{sourceRecordId}")
                    : ScholarlyWork.Identified(title, WorkIdSet.From(workIds));

                return new SearchImportRecord(
                    sourceDatabaseOrTool,
                    sourceRecordId,
                    null,
                    new System.Collections.ObjectModel.ReadOnlyCollection<string>(sourceIdentifiers),
                    work,
                    Array.Empty<string>(),
                    null,
                    null,
                    null,
                    Array.Empty<string>(),
                    null,
                    null,
                    false,
                    null,
                    Array.Empty<SearchImportParserNotice>());
            })
            .ToArray();
    }

    private static WorkId[] ReadWorkIdSet(string fixtureId, JsonElement sourceElement)
    {
        if (!sourceElement.TryGetProperty("workIds", out var workIdsElement))
        {
            return Array.Empty<WorkId>();
        }

        return workIdsElement
            .EnumerateArray()
            .Select(item =>
            {
                var value = item.GetString();
                try
                {
                    return WorkId.Parse(value ?? string.Empty);
                }
                catch (Exception ex) when (ex is SharedIdentityRuleException || ex is ArgumentException)
                {
                    throw new InvalidOperationException(
                        $"Fixture '{fixtureId}' contains invalid work id '{value}'.",
                        ex);
                }
            })
            .ToArray();
    }

    private static string[] ReadStringArray(JsonElement arrayElement) =>
        arrayElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

    private static int GetOptionalInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            ? value.GetInt32()
            : 0;
    }

    private static JsonDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(FixtureDirectory, fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
