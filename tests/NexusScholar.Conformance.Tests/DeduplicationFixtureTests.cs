using System.Text;
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
    [TestMethod]
    public void Hardening_09_result_rehydration_replays_verified_and_non_finite_cases()
    {
        var result = new DeduplicationService().Execute("rehydration-empty", [], []);
        var verified = DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(result));
        Assert.AreEqual("rehydration-empty", verified.Result.ResultId);

        var error = Assert.ThrowsExactly<DeduplicationAuthorityException>(() =>
            DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(result with { FuzzyTitleThreshold = double.NaN })));
        Assert.AreEqual(DeduplicationAuthorityErrorCodes.NonFiniteScore, error.Category);
    }
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
        "dedup-fuzzy-title-below-threshold-no-review",
        "dedup-threshold-95-boundary",
        "dedup-no-id-title-only-no-auto-merge",
        "dedup-representative-election",
        "dedup-representative-merge-preserves-evidence",
        "dedup-representative-metadata-completeness",
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
            Assert.AreEqual(
                ComputeFixtureDigest(root.GetProperty("case")),
                root.GetProperty("inputDigest").GetString(),
                $"{fixtureId}: inputDigest must match canonical case content");
            Assert.AreEqual(
                ComputeFixtureDigest(root.GetProperty("case").GetProperty("expected")),
                root.GetProperty("outputDigest").GetString(),
                $"{fixtureId}: outputDigest must match canonical expected replay summary");
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

            if (expected.TryGetProperty("representativeMetadata", out var representativeMetadata))
            {
                var representative = result.Clusters.Single().Representative;
                CollectionAssert.AreEqual(ReadStringArray(representativeMetadata.GetProperty("authors")), representative.Authors.ToArray(), fixtureId);
                Assert.AreEqual(representativeMetadata.GetProperty("year").GetInt32(), representative.Year, fixtureId);
                Assert.AreEqual(representativeMetadata.GetProperty("venue").GetString(), representative.Venue, fixtureId);
                Assert.AreEqual(representativeMetadata.GetProperty("abstract").GetString(), representative.Abstract, fixtureId);
                CollectionAssert.AreEqual(ReadStringArray(representativeMetadata.GetProperty("keywords")), representative.Keywords.ToArray(), fixtureId);
            }

            Assert.IsTrue(
                result.Evidence.Where(item => item.Kind == DedupEvidenceKind.ExactIdentifier).All(item => !item.ReviewRequired),
                $"{fixtureId}: exact identifier evidence must not require review");

            Assert.IsTrue(
                result.Evidence.Where(item => item.Kind == DedupEvidenceKind.SourceSighting).All(item => !item.ReviewRequired),
                $"{fixtureId}: source sighting evidence must not require review");

            Assert.IsTrue(
                result.Evidence.Where(item => item.Kind == DedupEvidenceKind.FuzzyTitle).All(item => item.ReviewRequired),
                $"{fixtureId}: fuzzy-title evidence must require review");

            Assert.IsTrue(
                result.Evidence.Where(item => item.Kind == DedupEvidenceKind.NoIdCandidate).All(item => item.ReviewRequired),
                $"{fixtureId}: no-id evidence must require review");

            Assert.IsTrue(
                result.Evidence.Where(item => item.Kind == DedupEvidenceKind.SourceSpecificIdentifier).All(item => item.ReviewRequired),
                $"{fixtureId}: source-specific evidence must require review");

            if (expected.TryGetProperty("expectedSourceFileDigest", out var expectedSourceFileDigest))
            {
                var expectedTraceId = expectedSourceFileDigest.GetProperty("traceId").GetString();
                var expectedDigest = expectedSourceFileDigest.GetProperty("digest").GetString();
                var trace = importTraces.SingleOrDefault(item => string.Equals(item.TraceId, expectedTraceId, StringComparison.Ordinal));
                Assert.IsNotNull(trace, $"{fixtureId}: missing expected import trace '{expectedTraceId}'");
                Assert.AreEqual(expectedDigest, trace.Metadata.SourceFileDigest, fixtureId);
                Assert.AreEqual(DigestScope.RawArtifactBytes.ToString(), trace.Metadata.SourceFileDigestScope, fixtureId);
                Assert.IsTrue(
                    result.RawCandidates.Any(candidate =>
                        candidate.Source.SourceKind == "import" &&
                        string.Equals(candidate.Source.SourceTraceId, expectedTraceId, StringComparison.Ordinal) &&
                        string.Equals(candidate.Source.SourceFileDigest, expectedDigest, StringComparison.Ordinal) &&
                        string.Equals(candidate.Source.SourceFileDigestScope, DigestScope.RawArtifactBytes.ToString(), StringComparison.Ordinal)),
                    $"{fixtureId}: result raw candidates must preserve source-file digest and scope");
                Assert.IsTrue(
                    result.Evidence.Any(evidence =>
                        evidence.Kind == DedupEvidenceKind.SourceSighting &&
                        evidence.Reason is not null &&
                        evidence.Reason.Contains($"source-file-digest:{expectedDigest}", StringComparison.Ordinal) &&
                        evidence.Reason.Contains($"source-file-digest-scope:{DigestScope.RawArtifactBytes}", StringComparison.Ordinal)),
                    $"{fixtureId}: source evidence must preserve source-file digest and scope");
            }

            if (expected.TryGetProperty("expectedRawRecordDigests", out var expectedRawRecordDigestsElement))
            {
                foreach (var expectedRawRecordDigest in expectedRawRecordDigestsElement.EnumerateArray())
                {
                    var expectedTraceId = expectedRawRecordDigest.GetProperty("traceId").GetString();
                    var expectedSourceRecordId = expectedRawRecordDigest.GetProperty("sourceRecordId").GetString();
                    var expectedDigest = expectedRawRecordDigest.GetProperty("digest").GetString();
                    var sourceRecord = importTraces
                        .Single(item => string.Equals(item.TraceId, expectedTraceId, StringComparison.Ordinal))
                        .ImportedRecords
                        .Single(record => string.Equals(record.SourceRecordId, expectedSourceRecordId, StringComparison.Ordinal));
                    Assert.AreEqual(expectedDigest, sourceRecord.RawRecordDigest, $"{fixtureId}: raw record digest mismatch");

                    var rawCandidate = result.RawCandidates.SingleOrDefault(candidate =>
                        candidate.Source.SourceKind == "import" &&
                        string.Equals(candidate.Source.SourceTraceId, expectedTraceId, StringComparison.Ordinal) &&
                        string.Equals(candidate.Source.SourceRecordId, expectedSourceRecordId, StringComparison.Ordinal));
                    Assert.IsNotNull(rawCandidate, $"{fixtureId}: missing raw candidate for imported record '{expectedSourceRecordId}'");
                    Assert.AreEqual(expectedDigest, rawCandidate.Source.RawRecordDigest, $"{fixtureId}: result raw candidate raw-record digest mismatch");
                    Assert.IsTrue(
                        result.Evidence.Any(evidence =>
                            evidence.Kind == DedupEvidenceKind.SourceSighting &&
                            string.Equals(evidence.SubjectCandidateId, rawCandidate.CandidateId, StringComparison.Ordinal) &&
                            evidence.Reason is not null &&
                            evidence.Reason.Contains($"raw-record-digest:{expectedDigest}", StringComparison.Ordinal)),
                        $"{fixtureId}: source evidence must preserve raw-record digest");
                }
            }

            if (expected.TryGetProperty("expectedParserWarnings", out var expectedParserWarnings))
            {
                var actualTraceWarnings = importTraces
                    .SelectMany(trace => trace.ParserWarnings.Select(warning => (trace.TraceId, warning)))
                    .ToArray();

                var actualRecordWarnings = importTraces
                    .SelectMany(trace => trace.ImportedRecords.SelectMany(record => record.Notices.Select(notice => (trace.TraceId, record.SourceRecordId, notice))))
                    .ToArray();

                foreach (var expectedParserWarning in expectedParserWarnings.EnumerateArray())
                {
                    var expectedTraceId = expectedParserWarning.GetProperty("traceId").GetString();
                    var expectedCategory = expectedParserWarning.GetProperty("category").GetString();
                    var expectedSourceRecordId = expectedParserWarning.TryGetProperty("sourceRecordId", out var sourceRecordIdElement)
                        ? sourceRecordIdElement.GetString()
                        : null;
                    var expectedMessage = expectedParserWarning.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString()
                        : null;

                    var matched = actualTraceWarnings.Any(item =>
                        string.Equals(item.TraceId, expectedTraceId, StringComparison.Ordinal) &&
                        string.Equals(item.warning.Category, expectedCategory, StringComparison.Ordinal) &&
                        (expectedMessage is null || string.Equals(item.warning.Message, expectedMessage, StringComparison.Ordinal)));

                    if (!matched)
                    {
                        matched = actualRecordWarnings.Any(item =>
                            string.Equals(item.TraceId, expectedTraceId, StringComparison.Ordinal) &&
                            (expectedSourceRecordId is null || string.Equals(item.SourceRecordId, expectedSourceRecordId, StringComparison.Ordinal)) &&
                            string.Equals(item.notice.Category, expectedCategory, StringComparison.Ordinal) &&
                            (expectedMessage is null || string.Equals(item.notice.Message, expectedMessage, StringComparison.Ordinal)));
                    }

                    Assert.IsTrue(matched, $"{fixtureId}: parser warning mismatch for category '{expectedCategory}'.");

                    var resultMatched = result.RawCandidates.Any(candidate =>
                        candidate.Source.SourceKind == "import" &&
                        string.Equals(candidate.Source.SourceTraceId, expectedTraceId, StringComparison.Ordinal) &&
                        (expectedSourceRecordId is null ||
                            string.Equals(candidate.Source.SourceRecordId, expectedSourceRecordId, StringComparison.Ordinal)) &&
                        (candidate.Source.ParserWarnings.Any(warning =>
                            string.Equals(warning.Category, expectedCategory, StringComparison.Ordinal) &&
                            (expectedMessage is null || string.Equals(warning.Message, expectedMessage, StringComparison.Ordinal))) ||
                         candidate.Source.RecordNotices.Any(notice =>
                            string.Equals(notice.Category, expectedCategory, StringComparison.Ordinal) &&
                            (expectedMessage is null || string.Equals(notice.Message, expectedMessage, StringComparison.Ordinal)))));

                    Assert.IsTrue(
                        resultMatched,
                        $"{fixtureId}: result raw candidates must preserve parser warning '{expectedCategory}'.");
                }
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
                var sourceFileDigest = ReadOptionalString(traceElement, "sourceFileDigest")
                    ?? "sha256:704c4ddfcee82cca7263d381d6fd4e0bba616e6791e4de1e4e264ffe3b20a9bf";
                var sourceFileDigestScope = ReadOptionalString(traceElement, "sourceFileDigestScope")
                    ?? DigestScope.RawArtifactBytes.ToString();
                var parserWarnings = traceElement.TryGetProperty("parserWarnings", out var parserWarningsElement)
                    ? ReadParserWarnings(parserWarningsElement)
                    : Array.Empty<SearchImportParserNotice>();

                var metadata = new SearchImportMetadata(
                    SearchImportMetadata.AcquisitionKindImportedExport,
                    "fixture-dedup",
                    "dedup-fixture",
                    "dedup-parser",
                    "1.0.0",
                    sourceFileDigest,
                    sourceFileDigestScope,
                    "import-operator",
                    "2026-06-27T00:00:00Z",
                    null,
                    null,
                    records.Count,
                    parserWarnings);

                return new SearchImportTrace(
                    traceId,
                    SearchImportSchemaId,
                    SearchImportSchemaVersion,
                    metadata,
                    records,
                    Array.Empty<SearchSighting>().AsReadOnly(),
                    parserWarnings,
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
                var sourceNoticeRecords = recordElement.TryGetProperty("parserWarnings", out var parserWarningsElement)
                    ? ReadParserWarnings(parserWarningsElement)
                    : Array.Empty<SearchImportParserNotice>();
                var rawRecordDigest = ReadOptionalString(recordElement, "rawRecordDigest");
                var authors = recordElement.TryGetProperty("authors", out var authorsElement) ? ReadStringArray(authorsElement) : Array.Empty<string>();
                var year = ReadNullableInt(recordElement, "year");
                var venue = ReadOptionalString(recordElement, "venue");
                var abstractText = ReadOptionalString(recordElement, "abstract");
                var keywords = recordElement.TryGetProperty("keywords", out var keywordsElement) ? ReadStringArray(keywordsElement) : Array.Empty<string>();

                var work = isUnresolved || workIds.Length == 0
                    ? ScholarlyWork.UnresolvedCandidate(title, $"import:{sourceRecordId}")
                    : ScholarlyWork.Identified(title, WorkIdSet.From(workIds));

                return new SearchImportRecord(
                    sourceDatabaseOrTool,
                    sourceRecordId,
                    null,
                    new System.Collections.ObjectModel.ReadOnlyCollection<string>(sourceIdentifiers),
                    work,
                    authors,
                    year,
                    venue,
                    abstractText,
                    keywords,
                    rawRecordDigest,
                    null,
                    false,
                    null,
                    sourceNoticeRecords);
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

    private static string? ReadOptionalString(JsonElement sourceElement, string propertyName)
    {
        return sourceElement.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private static int? ReadNullableInt(JsonElement sourceElement, string propertyName)
    {
        return sourceElement.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt32()
            : null;
    }

    private static SearchImportParserNotice[] ReadParserWarnings(JsonElement warningElement)
    {
        return warningElement.EnumerateArray()
            .Select(warning =>
            {
                var category = warning.GetProperty("category").GetString() ?? string.Empty;
                var message = warning.GetProperty("message").GetString() ?? string.Empty;
                var recordIndex = ReadNullableInt(warning, "recordIndex");
                var sourceRecordId = ReadOptionalString(warning, "sourceRecordId");
                return new SearchImportParserNotice(category, message, recordIndex, sourceRecordId);
            })
            .ToArray();
    }

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

    private static string ComputeFixtureDigest(JsonElement element)
    {
        return ContentDigest.Sha256Utf8(Canonicalize(element)).ToString();
    }

    private static string Canonicalize(JsonElement element)
    {
        var builder = new StringBuilder();
        WriteCanonicalJson(element, builder);
        return builder.ToString();
    }

    private static void WriteCanonicalJson(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var firstProperty = true;
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }

                    firstProperty = false;
                    builder.Append(JsonSerializer.Serialize(property.Name));
                    builder.Append(':');
                    WriteCanonicalJson(property.Value, builder);
                }

                builder.Append('}');
                break;

            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    firstItem = false;
                    WriteCanonicalJson(item, builder);
                }

                builder.Append(']');
                break;

            case JsonValueKind.String:
                builder.Append(JsonSerializer.Serialize(element.GetString()));
                break;

            case JsonValueKind.Number:
                builder.Append(element.GetRawText());
                break;

            case JsonValueKind.True:
                builder.Append("true");
                break;

            case JsonValueKind.False:
                builder.Append("false");
                break;

            case JsonValueKind.Null:
                builder.Append("null");
                break;

            default:
                throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }
}
