using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Shared;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class DeduplicationServiceTests
{
    private const int ValidationYear = 2026;

    [TestMethod]
    public void Exact_identifier_overlap_clusters_automatically()
    {
        var trace = BuildSearchTrace(
            "search-trace-exact",
            BuildSearchSighting("openalex", 1, 1, "Shared title", "doi", "10.1000/ABC"),
            BuildSearchSighting("crossref", 2, 1, "Shared title", "doi", "10.1000/abc"),
            BuildSearchSighting("ieee", 3, 1, "Different", "s2", "other"));

        var result = new DeduplicationService().Execute("dedup-result", new[] { trace }, []);

        Assert.AreEqual(1, result.Clusters.Count);
        Assert.AreEqual(2, result.Clusters[0].Members.Count);
        Assert.AreEqual(1, result.Clusters[0].Evidence.Count(entry => entry.Kind == DedupEvidenceKind.ExactIdentifier));
        CollectionAssert.AreEquivalent(
            result.Clusters[0].Members.Select(member => member.CandidateId).OrderBy(id => id).ToArray(),
            new[]
            {
                "search:search-trace-exact:1:openalex:1:1",
                "search:search-trace-exact:2:crossref:2:1"
            });
        Assert.AreEqual(0, result.ReviewRequiredCandidates.Count);
    }

    [TestMethod]
    public void Cross_namespace_same_value_does_not_cluster()
    {
        var trace = BuildSearchTrace(
            "search-trace-cross-namespace",
            BuildSearchSighting("openalex", 1, 1, "Model A", "doi", "10.1000/ABC"),
            BuildSearchSighting("crossref", 2, 1, "Model A", "arxiv", "10.1000/ABC"));

        var result = new DeduplicationService().Execute("dedup-result", new[] { trace }, []);

        Assert.AreEqual(0, result.Clusters.Count);
        Assert.AreEqual(1, result.ReviewRequiredCandidates.Count);
    }

    [TestMethod]
    public void Transitive_exact_clusters_merge_connected_members()
    {
        var trace = BuildSearchTrace(
            "search-trace-transitive",
            BuildSearchSighting("openalex", 1, 1, "Paper A", "doi", "10.1000/one"),
            BuildSearchSighting(
                "semantic_scholar",
                2,
                1,
                "Paper B",
                WorkIdSet.From(WorkId.From("doi", "10.1000/one"), WorkId.From("openalex", "10.1000/one"))),
            BuildSearchSighting("ieee", 3, 1, "Paper C", "openalex", "10.1000/one"));

        var result = new DeduplicationService().Execute("dedup-result", new[] { trace }, []);

        Assert.AreEqual(1, result.Clusters.Count);
        Assert.AreEqual(3, result.Clusters[0].Members.Count);
        Assert.AreEqual(2, result.Clusters[0].Evidence.Count(entry => entry.Kind == DedupEvidenceKind.ExactIdentifier));
    }

    [TestMethod]
    public void Fuzzy_title_above_threshold_is_review_required_not_auto_cluster()
    {
        var trace = BuildSearchTrace(
            "search-trace-fuzzy",
            BuildSearchSighting("crossref", 1, 1, "Deep learning with transformers", "s2", "10"),
            BuildSearchSighting("crossref", 1, 2, "Deep-learning with transformers!", "s2", "11"));

        var result = new DeduplicationService().Execute("dedup-result", new[] { trace }, [], 0.95d);

        Assert.AreEqual(0, result.Clusters.Count);
        Assert.AreEqual(1, result.ReviewRequiredCandidates.Count);
        Assert.IsTrue(result.ReviewRequiredCandidates[0].TitleSimilarity >= 0.95d);
        Assert.AreEqual(1, result.Evidence.Count(entry => entry.Kind == DedupEvidenceKind.FuzzyTitle));
    }

    [TestMethod]
    public void No_id_title_only_records_are_not_auto_merged()
    {
        var trace = BuildSearchTrace(
            "search-trace-noid",
            BuildSearchSighting("openalex", 1, 1, "Transformer survey", hasIds: false),
            BuildSearchSighting("openalex", 1, 2, "Transformer survey", hasIds: false));

        var result = new DeduplicationService().Execute("dedup-result", new[] { trace }, []);

        Assert.AreEqual(0, result.Clusters.Count);
        Assert.AreEqual(2, result.UnresolvedCandidates.Count);
        Assert.AreEqual(1, result.ReviewRequiredCandidates.Count);
        Assert.AreEqual(2, result.Evidence.Count(entry => entry.Kind == DedupEvidenceKind.NoIdCandidate));
        Assert.AreEqual(2, result.Evidence.Count(entry => entry.Kind == DedupEvidenceKind.SourceSighting));
    }

    [TestMethod]
    public void Fuzzy_threshold_boundary_follows_local_default_of_95()
    {
        var trace = BuildSearchTrace(
            "search-trace-boundary",
            BuildSearchSighting("openalex", 1, 1, "Machine-learning systems", "doi", "10.1"),
            BuildSearchSighting("openalex", 1, 2, "Machine learning systems!", "doi", "10.2"));

        var result = new DeduplicationService().Execute("dedup-result", new[] { trace }, []);

        Assert.AreEqual(0.95d, result.FuzzyTitleThreshold);
        Assert.AreEqual(1, result.ReviewRequiredCandidates.Count);
        Assert.AreEqual(0, result.Clusters.Count);
    }

    [TestMethod]
    public void Representative_election_is_deterministic()
    {
        var first = BuildSearchSighting("openalex", 1, 1, "Rep title", WorkIdSet.From(WorkId.From("doi", "10.1000/1")));
        var second = BuildSearchSighting("crossref", 1, 2, "Rep title", WorkIdSet.From(WorkId.From("doi", "10.1000/1")));

        var trace = BuildSearchTrace("search-trace-rep", second, first);
        var service = new DeduplicationService();
        var firstRun = service.Execute("dedup-result", new[] { trace }, []);
        var secondRun = service.Execute("dedup-result", new[] { trace }, []);

        var firstResultCluster = firstRun.Clusters.Single();
        var secondResultCluster = secondRun.Clusters.Single();
        Assert.AreEqual(firstResultCluster.Representative.CandidateId, secondResultCluster.Representative.CandidateId);
        Assert.IsTrue(firstResultCluster.Members.Any(member => member.CandidateId == firstResultCluster.Representative.CandidateId));
        Assert.AreEqual(first.Work.Title, firstResultCluster.Representative.Title);
        Assert.AreEqual("doi:10.1000/1", firstResultCluster.Representative.PrimaryWorkId);
        CollectionAssert.AreEquivalent(
            new[] { "doi:10.1000/1" },
            firstResultCluster.Representative.WorkIds.ToArray());
    }

    [TestMethod]
    public void Evidence_links_are_preserved_and_raw_sightings_survive_clustering()
    {
        var first = BuildSearchSighting("openalex", 1, 1, "Representative paper", WorkIdSet.From(WorkId.From("doi", "10.1000/rep")));
        var second = BuildSearchSighting("semantic_scholar", 2, 1, "Raw evidence one", WorkIdSet.From(WorkId.From("doi", "10.1000/rep")));
        var unresolved = BuildSearchSighting("crossref", 3, 1, "Supplemental", hasIds: false);

        var trace = BuildSearchTrace("search-trace-evidence", first, second, unresolved);
        var result = new DeduplicationService().Execute("dedup-result", new[] { trace }, []);

        Assert.AreEqual(3, result.RawCandidates.Count);
        Assert.AreEqual(1, result.Clusters.Count);

        var cluster = result.Clusters.Single();
        Assert.IsTrue(cluster.Members.Any(member => member.CandidateId.Contains("search-trace-evidence:1:openalex:1:1")));
        Assert.IsTrue(result.UnresolvedCandidates.Any(candidate => candidate.CandidateId.Contains("search-trace-evidence:3:crossref:3:1")));
        Assert.AreEqual(3, cluster.Evidence.Count(entry => entry.Kind == DedupEvidenceKind.SourceSighting || entry.Kind == DedupEvidenceKind.ExactIdentifier));
    }

    [TestMethod]
    public void Source_specific_identifiers_are_review_required_and_do_not_auto_cluster()
    {
        var import = BuildImportTrace(
            "import-trace-source-specific",
            BuildImportRecord(
                "scopus-csv",
                "Source specific evidence paper",
                "s1",
                identifier: null,
                sourceIdentifiers: new[] { "scopus:EID:2-s2.0-8500000000" },
                unresolved: true),
            BuildImportRecord(
                "scopus-csv",
                "Source specific evidence paper",
                "s2",
                identifier: null,
                sourceIdentifiers: new[] { "scopus:EID:2-s2.0-8500000000" },
                unresolved: true));

        var result = new DeduplicationService().Execute("dedup-result", [], [import]);

        Assert.AreEqual(0, result.Clusters.Count);
        Assert.AreEqual(2, result.UnresolvedCandidates.Count);
        Assert.AreEqual(1, result.ReviewRequiredCandidates.Count);
        Assert.AreEqual(3, result.Evidence.Count(entry => entry.Kind == DedupEvidenceKind.SourceSpecificIdentifier));
    }

    [TestMethod]
    public void Search_and_import_bindings_and_non_claims_are_preserved_in_output()
    {
        var search = BuildSearchTrace(
            "search-trace-binding",
            BuildSearchSighting("openalex", 1, 1, "Binding alpha", WorkIdSet.From(WorkId.From("doi", "10.1000/b1"))));

        var import = BuildImportTrace(
            "import-trace-binding",
            BuildImportRecord(
                "import-db",
                "alpha",
                "r1",
                WorkId.From("s2", "s2-1")));

        var result = new DeduplicationService().Execute(
            "dedup-result",
            new[] { search },
            new[] { import });

        CollectionAssert.Contains(result.SourceSearchTraceIds.ToArray(), "search-trace-binding");
        CollectionAssert.Contains(result.SourceImportTraceIds.ToArray(), "import-trace-binding");
        Assert.IsTrue(result.RawCandidates.Any(candidate => candidate.Source.SourceKind == "search"));
        Assert.IsTrue(result.RawCandidates.Any(candidate => candidate.Source.SourceKind == "import"));

        var firstSearchCandidate = result.RawCandidates.Single(candidate => candidate.Source.SourceKind == "search");
        Assert.AreEqual("search-trace-binding", firstSearchCandidate.Source.SourceTraceId);
        var firstImportCandidate = result.RawCandidates.Single(candidate => candidate.Source.SourceKind == "import");
        Assert.AreEqual("import-trace-binding", firstImportCandidate.Source.SourceTraceId);

        CollectionAssert.Contains(result.NonClaims.ToArray(), "no-app-projection-authority");
        CollectionAssert.Contains(result.NonClaims.ToArray(), "no-screening");
        CollectionAssert.Contains(result.NonClaims.ToArray(), "no-search-screening-claim");
    }

    [TestMethod]
    public void Output_keeps_source_trace_and_sighting_bindings()
    {
        var search = BuildSearchTrace(
            "search-trace-binding-2",
            BuildSearchSighting("openalex", 1, 1, "Binding alpha", WorkIdSet.From(WorkId.From("doi", "10.1000/b2"))));

        var import = BuildImportTrace(
            "import-trace-binding-2",
            BuildImportRecord(
                "import-db",
                "alpha",
                "r2",
                WorkId.From("s2", "s2-2")));

        var result = new DeduplicationService().Execute(
            "dedup-result",
            new[] { search },
            new[] { import });

        var sourceEvidence = result.Evidence.Where(entry => entry.Kind == DedupEvidenceKind.SourceSighting).ToArray();
        Assert.IsTrue(sourceEvidence.Length >= 2);

        var searchEvidence = sourceEvidence
            .Where(entry => entry.Reason is not null
                && entry.Reason.Contains("source-trace:search-trace-binding-2", StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(1, searchEvidence.Length);

        var importEvidence = sourceEvidence
            .Where(entry => entry.Reason is not null
                && entry.Reason.Contains("source-trace:import-trace-binding-2", StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(1, importEvidence.Length);
    }

    private static SearchTrace BuildSearchTrace(string traceId, params SearchSighting[] sightings)
    {
        var query = new SearchQueryInput("dedup", null, null, null, 25, 0, false, Array.Empty<string>());
        var cacheIdentity = SearchCacheIdentity.Compute(query, ValidationYear, Array.Empty<string>());
        var yearRange = SearchYearRange.Validate(null, null, ValidationYear);
        var request = new SearchTraceRequest(
            query.Query,
            yearRange,
            query.Language,
            query.MaxResults,
            query.Offset,
            query.IncludeRawData,
            Array.Empty<string>(),
            Array.Empty<string>(),
            null);

        return new SearchTrace(
            traceId,
            SearchTrace.TraceSchemaId,
            SearchTrace.TraceSchemaVersion,
            request,
            cacheIdentity,
            new ReadOnlyCollection<SearchProviderAttempt>([]),
            new ReadOnlyCollection<SearchProviderStat>([]),
            new ReadOnlyCollection<SearchSighting>(sightings),
            new SearchSummary(sightings.Length, 0, 0, sightings.Length, false),
            SearchTrace.DefaultNonClaims);
    }

    private static SearchSighting BuildSearchSighting(
        string providerAlias,
        int providerOrder,
        int providerLocalRank,
        string title,
        string? idNamespace = null,
        string? idValue = null,
        bool hasIds = true)
    {
        if (hasIds)
        {
            Guard.NotBlank(idNamespace, nameof(idNamespace));
            Guard.NotBlank(idValue, nameof(idValue));
            return new SearchSighting(
                providerAlias,
                providerOrder,
                providerLocalRank,
                ScholarlyWork.Identified(title, WorkIdSet.From(WorkId.From(idNamespace!, idValue!))));
        }

        return new SearchSighting(
            providerAlias,
            providerOrder,
            providerLocalRank,
            ScholarlyWork.UnresolvedCandidate(title, $"search:{providerAlias}:{providerOrder}:{providerLocalRank}"));
    }

    private static SearchSighting BuildSearchSighting(
        string providerAlias,
        int providerOrder,
        int providerLocalRank,
        string title,
        WorkIdSet workIdSet)
    {
        return new SearchSighting(
            providerAlias,
            providerOrder,
            providerLocalRank,
            ScholarlyWork.Identified(title, workIdSet));
    }

    private static SearchSighting BuildSearchSighting(
        string providerAlias,
        int providerOrder,
        int providerLocalRank,
        string title,
        string firstIdNamespace,
        string firstIdValue,
        string secondIdValue)
    {
        return new SearchSighting(
            providerAlias,
            providerOrder,
            providerLocalRank,
            ScholarlyWork.Identified(
                title,
                WorkIdSet.From(WorkId.From(firstIdNamespace, firstIdValue), WorkId.From("s2", secondIdValue))));
    }

    private static SearchImportRecord BuildImportRecord(
        string sourceDatabaseOrTool,
        string title,
        string sourceRecordId,
        WorkId? identifier,
        IReadOnlyList<string>? sourceIdentifiers = null,
        bool unresolved = false)
    {
        if (unresolved)
        {
            return new SearchImportRecord(
                sourceDatabaseOrTool,
                sourceRecordId,
                null,
                sourceIdentifiers ?? Array.Empty<string>(),
                ScholarlyWork.UnresolvedCandidate(title, $"import:{sourceRecordId}"),
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
        }

        var work = identifier is null
            ? ScholarlyWork.UnresolvedCandidate(title, $"import:{sourceRecordId}")
            : ScholarlyWork.Identified(title, WorkIdSet.From(identifier.Value));

        return new SearchImportRecord(
            sourceDatabaseOrTool,
            sourceRecordId,
            null,
            sourceIdentifiers ?? Array.Empty<string>(),
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
    }

    private static SearchImportTrace BuildImportTrace(string traceId, params SearchImportRecord[] records)
    {
        var metadata = new SearchImportMetadata(
            SearchImportMetadata.AcquisitionKindImportedExport,
            "import-db",
            "ris",
            "parser-id",
            "1.0.0",
            "sha256:111111111111111111111111111111111111111111111111111111111111111111",
            "raw-artifact-bytes",
            "importer",
            "2026-06-27T00:00:00Z",
            null,
            null,
            records.Length,
            Array.Empty<SearchImportParserNotice>());

        return new SearchImportTrace(
            traceId,
            "nexus.search.import.trace",
            "1.0.0",
            metadata,
            new ReadOnlyCollection<SearchImportRecord>(records),
            Array.Empty<SearchSighting>(),
            Array.Empty<SearchImportParserNotice>(),
            SearchImportTrace.DefaultNonClaims);
    }
}
