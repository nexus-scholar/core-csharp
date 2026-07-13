using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Shared;

namespace NexusScholar.Deduplication;

public sealed class DeduplicationService
{
    public const string ResultSchemaId = "nexus.deduplication.result";
    public const string ResultSchemaVersion = "1.0.0";
    public const string PolicyId = "local-deduplication-v1";
    public const string PolicyVersion = "1.0.0";
    public const double DefaultFuzzyTitleThreshold = 0.95d;

    public static readonly IReadOnlyDictionary<string, int> DefaultProviderPriority =
        new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["openalex"] = 5,
            ["crossref"] = 4,
            ["scopus"] = 4,
            ["scopus-csv"] = 4,
            ["semantic_scholar"] = 3,
            ["arxiv"] = 2,
            ["pubmed"] = 2,
            ["pmcid"] = 2,
            ["ieee"] = 1,
            ["doaj"] = 1,
            ["ris"] = 1,
            ["bibtex"] = 1
        });

    public static readonly IReadOnlyList<string> DefaultNonClaims =
        new ReadOnlyCollection<string>(
            new[]
            {
                "no-broad-php-deduplication-compatibility",
                "no-corpus-lock-snapshot-compatibility",
                "no-screening",
                "no-search-screening-claim",
                "no-live-provider-network",
                "no-persistence-api-ui-cloud",
                "no-app-projection-authority"
            });

    public DeduplicationResult Execute(
        string resultId,
        IReadOnlyList<SearchTrace> searchTraces,
        IReadOnlyList<SearchImportTrace> importTraces,
        double fuzzyTitleThreshold = DefaultFuzzyTitleThreshold)
    {
        Guard.NotBlank(resultId, nameof(resultId));
        ArgumentNullException.ThrowIfNull(searchTraces);
        ArgumentNullException.ThrowIfNull(importTraces);

        if (!double.IsFinite(fuzzyTitleThreshold) || fuzzyTitleThreshold < 0 || fuzzyTitleThreshold > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fuzzyTitleThreshold),
                "Fuzzy title threshold must be between 0 and 1.");
        }

        var sourceTraceIds = new HashSet<string>(StringComparer.Ordinal);
        var importTraceIds = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<DedupCandidateRecord>();
        var evidence = new List<DedupEvidence>();
        var exactEvidence = new List<DedupEvidence>();
        var warnings = new List<DedupMessage>();
        var errors = new List<DedupMessage>();

        foreach (var trace in searchTraces)
        {
            sourceTraceIds.Add(trace.TraceId);
            var index = 0;
            foreach (var sighting in trace.Sightings)
            {
                index++;
                var candidate = BuildFromSearchSighting(trace.TraceId, index, sighting);
                candidates.Add(candidate);
                evidence.Add(CreateSourceEvidence(candidate));

                if (!candidate.HasStableIdentifier)
                {
                    evidence.Add(CreateNoIdEvidence(candidate));
                }
            }
        }

        foreach (var trace in importTraces)
        {
            importTraceIds.Add(trace.TraceId);
            var index = 0;
            foreach (var record in trace.ImportedRecords)
            {
                if (record.IsSkipped)
                {
                    continue;
                }

                index++;
                var candidate = BuildFromImportRecord(trace.TraceId, index, record, trace.Metadata, trace.ParserWarnings);
                candidates.Add(candidate);
                evidence.Add(CreateSourceEvidence(candidate));
                AddSourceSpecificEvidence(candidate, evidence);

                if (!candidate.HasStableIdentifier)
                {
                    evidence.Add(CreateNoIdEvidence(candidate));
                }
            }
        }

        var orderedCandidates = candidates
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var rawCandidates = new ReadOnlyCollection<DedupCandidateRecord>(orderedCandidates);

        var exactUnion = new UnionFind(rawCandidates.Count);
        var candidateByWorkId = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var index = 0; index < rawCandidates.Count; index++)
        {
            var candidate = rawCandidates[index];

            foreach (var workId in candidate.WorkIds)
            {
                if (!candidateByWorkId.TryGetValue(workId, out var priorIndexes))
                {
                    priorIndexes = new List<int>();
                    candidateByWorkId[workId] = priorIndexes;
                }

                foreach (var priorIndex in priorIndexes)
                {
                    exactUnion.Union(index, priorIndex);
                    exactEvidence.Add(new DedupEvidence(
                        BuildEvidenceId("exact-id", candidate.CandidateId, rawCandidates[priorIndex].CandidateId, workId),
                        DedupEvidenceKind.ExactIdentifier,
                        candidate.CandidateId,
                        rawCandidates[priorIndex].CandidateId,
                        BuildEvidenceReason("exact-work-id-overlap", workId),
                        ReviewRequired: false,
                        1.0,
                        PolicyId,
                        PolicyVersion));
                }

                priorIndexes.Add(index);
            }
        }

        var groups = new Dictionary<int, List<int>>();
        for (var index = 0; index < rawCandidates.Count; index++)
        {
            var root = exactUnion.Find(index);
            if (!groups.TryGetValue(root, out var memberIndexes))
            {
                memberIndexes = new List<int>();
                groups[root] = memberIndexes;
            }

            memberIndexes.Add(index);
        }

        var clusters = new List<DedupCluster>();
        foreach (var group in groups.Values)
        {
            if (group.Count <= 1)
            {
                continue;
            }

            var members = group.Select(index => rawCandidates[index]).ToArray();
            Array.Sort(
                members,
                (left, right) => string.Compare(left.CandidateId, right.CandidateId, StringComparison.Ordinal));

            var representative = ElectRepresentative(members);
            var fullEvidence = evidence
                .Where(item => ContainsCandidate(item, members))
                .ToArray();
            var exactEvidenceForMembers = exactEvidence
                .Where(item => ContainsCandidate(item, members))
                .ToArray();

            var clusterEvidence = fullEvidence
                .Concat(exactEvidenceForMembers)
                .Distinct()
                .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
                .ToArray();

            clusters.Add(new DedupCluster(
                BuildClusterId(group),
                new ReadOnlyCollection<DedupCandidateRecord>(members),
                representative,
                new ReadOnlyCollection<DedupEvidence>(clusterEvidence)));
        }

        var unresolvedCandidates = rawCandidates
            .Where(candidate => !candidate.HasStableIdentifier)
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();

        var reviewPairs = new HashSet<string>(StringComparer.Ordinal);
        var reviewRequired = new List<DedupReviewCandidate>();
        var reviewEvidence = new List<DedupEvidence>();

        for (var left = 0; left < rawCandidates.Count; left++)
        {
            for (var right = left + 1; right < rawCandidates.Count; right++)
            {
                var leftCandidate = rawCandidates[left];
                var rightCandidate = rawCandidates[right];
                var pairKey = BuildEvidencePairKey(leftCandidate.CandidateId, rightCandidate.CandidateId);

                if (reviewPairs.Contains(pairKey))
                {
                    continue;
                }

                if (exactUnion.Find(left) == exactUnion.Find(right))
                {
                    continue;
                }

                var hasSourceSpecific = HasSourceSpecificOverlap(leftCandidate, rightCandidate);
                var similarity = ComputeTitleSimilarity(leftCandidate.Title, rightCandidate.Title);
                var hasExactId = leftCandidate.HasStableIdentifier &&
                                 rightCandidate.HasStableIdentifier &&
                                 HasExactWorkIdOverlap(leftCandidate, rightCandidate);

                if (hasSourceSpecific)
                {
                    reviewPairs.Add(pairKey);
                    reviewRequired.Add(new DedupReviewCandidate(
                        leftCandidate.CandidateId,
                        rightCandidate.CandidateId,
                        similarity,
                        fuzzyTitleThreshold));
                    reviewEvidence.Add(CreateSourceSpecificPairEvidence(leftCandidate, rightCandidate, similarity));
                    continue;
                }

                if (hasExactId || similarity < fuzzyTitleThreshold)
                {
                    continue;
                }

                reviewPairs.Add(pairKey);
                reviewRequired.Add(new DedupReviewCandidate(
                    leftCandidate.CandidateId,
                    rightCandidate.CandidateId,
                    similarity,
                    fuzzyTitleThreshold));
                reviewEvidence.Add(new DedupEvidence(
                    BuildEvidenceId("fuzzy-title", leftCandidate.CandidateId, rightCandidate.CandidateId),
                    DedupEvidenceKind.FuzzyTitle,
                    leftCandidate.CandidateId,
                    rightCandidate.CandidateId,
                    BuildEvidenceReason(
                        "title-similarity",
                        similarity.ToString("0.####", CultureInfo.InvariantCulture)),
                    ReviewRequired: true,
                    similarity,
                    PolicyId,
                    PolicyVersion));
            }
        }

        if (!clusters.Any())
        {
            warnings.Add(new DedupMessage("no-auto-clusters", "No exact-identifier clusters were formed."));
        }

        var allEvidence = evidence
            .Concat(exactEvidence)
            .Concat(reviewEvidence)
            .Distinct()
            .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();

        return new DeduplicationResult(
            resultId,
            ResultSchemaId,
            ResultSchemaVersion,
            PolicyId,
            PolicyVersion,
            fuzzyTitleThreshold,
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(DefaultProviderPriority)),
            new ReadOnlyCollection<string>(sourceTraceIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()),
            new ReadOnlyCollection<string>(importTraceIds.OrderBy(id => id, StringComparer.Ordinal).ToArray()),
            rawCandidates,
            new ReadOnlyCollection<DedupCluster>(clusters.OrderBy(cluster => cluster.ClusterId, StringComparer.Ordinal).ToArray()),
            new ReadOnlyCollection<DedupEvidence>(allEvidence),
            new ReadOnlyCollection<DedupCandidateRecord>(unresolvedCandidates),
            new ReadOnlyCollection<DedupReviewCandidate>(reviewRequired
                .OrderBy(candidate => candidate.CandidateAId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.CandidateBId, StringComparer.Ordinal)
                .ToArray()),
            new ReadOnlyCollection<DedupMessage>(warnings),
            new ReadOnlyCollection<DedupMessage>(errors),
            DefaultNonClaims);
    }

    private static DedupCandidateRecord BuildFromSearchSighting(string traceId, int sightingIndex, SearchSighting sighting)
    {
        var work = sighting.Work;

        return new DedupCandidateRecord(
            $"search:{traceId}:{sightingIndex}:{sighting.ProviderAlias}:{sighting.ProviderOrder}:{sighting.ProviderLocalRank}",
            work.Title,
            work.HasStableIdentifier,
            work.PrimaryWorkId?.ToString(),
            work.WorkIds.Ids.Select(id => id.ToString()).ToArray(),
            Array.Empty<string>(),
            new DedupSightingRef(
                SourceKind: "search",
                SourceTraceId: traceId,
                SourceSightingId: $"search:{traceId}:{sightingIndex}:{sighting.ProviderAlias}:{sighting.ProviderOrder}:{sighting.ProviderLocalRank}",
                ProviderAlias: sighting.ProviderAlias,
                SourceContext: work.SourceContext));
    }

    private static DedupCandidateRecord BuildFromImportRecord(
        string traceId,
        int recordIndex,
        SearchImportRecord record,
        SearchImportMetadata metadata,
        IReadOnlyList<SearchImportParserNotice> traceParserWarnings)
    {
        var work = record.Work;

        return new DedupCandidateRecord(
            $"import:{traceId}:{recordIndex}:{record.SourceRecordId}",
            work.Title,
            work.HasStableIdentifier,
            work.PrimaryWorkId?.ToString(),
            work.WorkIds.Ids.Select(id => id.ToString()).ToArray(),
            record.SourceIdentifiers.Select(identifier => identifier.Trim())
                .Where(identifier => identifier.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            new DedupSightingRef(
                SourceKind: "import",
                SourceTraceId: traceId,
                SourceSightingId: $"import:{traceId}:{recordIndex}:{record.SourceRecordId}",
                SourceDatabaseOrTool: record.SourceDatabaseOrTool,
                SourceRecordId: record.SourceRecordId,
                SourceFileDigest: metadata.SourceFileDigest,
                SourceFileDigestScope: metadata.SourceFileDigestScope,
                RawRecordDigest: record.RawRecordDigest,
                SourceContext: work.SourceContext,
                ParserWarnings: ConvertNotices(metadata.ParserWarnings.Concat(traceParserWarnings)),
                RecordNotices: ConvertNotices(record.Notices)),
            record.Authors,
            record.Year,
            record.Venue,
            record.Abstract,
            record.Keywords);
    }

    private static bool HasExactWorkIdOverlap(DedupCandidateRecord left, DedupCandidateRecord right)
    {
        return left.WorkIds.Any(item => right.WorkIds.Any(other => string.Equals(item, other, StringComparison.Ordinal)));
    }

    private static bool HasSourceSpecificOverlap(DedupCandidateRecord left, DedupCandidateRecord right)
    {
        return left.SourceSpecificIds.Any(item => right.SourceSpecificIds.Any(other => string.Equals(item, other, StringComparison.Ordinal)));
    }

    private static DedupRepresentativeResult ElectRepresentative(DedupCandidateRecord[] members)
    {
        var ranked = members
            .Select(member => new
            {
                Candidate = member,
                Completeness = ComputeCompletenessScore(member),
                AcceptedStableIdentifier = member.HasStableIdentifier,
                HasDoiId = member.WorkIds.Any(id => id.StartsWith("doi:", StringComparison.Ordinal)),
                ProviderPriority = GetProviderPriority(member.Source),
                NormalizedPrimaryIdentifier = ComputePrimaryIdentifierSortKey(member),
                CandidateId = member.CandidateId
            })
            .OrderByDescending(item => item.Completeness)
            .ThenByDescending(item => item.AcceptedStableIdentifier)
            .ThenByDescending(item => item.ProviderPriority)
            .ThenByDescending(item => item.HasDoiId)
            .ThenBy(item => item.NormalizedPrimaryIdentifier, StringComparer.Ordinal)
            .ThenBy(item => item.Candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var elected = ranked[0];

        var unionWorkIds = members
            .SelectMany(member => member.WorkIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var sourceFileDigests = DistinctNonBlank(members.Select(member => member.Source.SourceFileDigest));
        var sourceFileDigestScopes = DistinctNonBlank(members.Select(member => member.Source.SourceFileDigestScope));
        var rawRecordDigests = DistinctNonBlank(members.Select(member => member.Source.RawRecordDigest));
        var parserWarnings = DistinctNotices(members.SelectMany(member => member.Source.ParserWarnings));
        var recordNotices = DistinctNotices(members.SelectMany(member => member.Source.RecordNotices));

        var representativeTitle = !string.IsNullOrWhiteSpace(elected.Candidate.Title)
            ? elected.Candidate.Title
            : members.FirstOrDefault(member => !string.IsNullOrWhiteSpace(member.Title))?.Title ?? string.Empty;

        var representativePrimaryWorkId = !string.IsNullOrWhiteSpace(elected.Candidate.PrimaryWorkId)
            ? elected.Candidate.PrimaryWorkId
            : unionWorkIds.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        var representativeAuthors = ranked.Select(item => item.Candidate.Authors).FirstOrDefault(values => values.Count > 0)
            ?? Array.Empty<string>();
        var representativeYear = ranked.Select(item => item.Candidate.Year).FirstOrDefault(value => value.HasValue);
        var representativeVenue = ranked.Select(item => item.Candidate.Venue).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var representativeAbstract = ranked.Select(item => item.Candidate.Abstract).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var representativeKeywords = ranked.Select(item => item.Candidate.Keywords).FirstOrDefault(values => values.Count > 0)
            ?? Array.Empty<string>();

        var reasonCodes = new List<string>
        {
            "completeness-score",
            "accepted-stable-id",
            "provider-priority",
            "doi-preference",
            "primary-identifier",
            "candidate-id",
            "workid-union",
            "scholarly-metadata-completeness",
            "missing-field-fill"
        };

        if (sourceFileDigests.Length > 0 ||
            rawRecordDigests.Length > 0 ||
            parserWarnings.Count > 0 ||
            recordNotices.Count > 0)
        {
            reasonCodes.Add("import-evidence-projection");
        }

        return new DedupRepresentativeResult(
            elected.Candidate.CandidateId,
            representativeTitle,
            representativePrimaryWorkId,
            new ReadOnlyCollection<string>(unionWorkIds),
            members.Select(member => member.Source.SourceSightingId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            elected.Completeness,
            new ReadOnlyCollection<string>(reasonCodes),
            new ReadOnlyCollection<string>(sourceFileDigests),
            new ReadOnlyCollection<string>(sourceFileDigestScopes),
            new ReadOnlyCollection<string>(rawRecordDigests),
            parserWarnings,
            recordNotices,
            representativeAuthors,
            representativeYear,
            representativeVenue,
            representativeAbstract,
            representativeKeywords);
    }

    private static int GetProviderPriority(DedupSightingRef source)
    {
        var sourceName = string.IsNullOrWhiteSpace(source.ProviderAlias)
            ? source.SourceDatabaseOrTool
            : source.ProviderAlias;

        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return 0;
        }

        var normalizedSourceName = sourceName.Trim().ToLowerInvariant();
        return DefaultProviderPriority.TryGetValue(normalizedSourceName, out var priority) ? priority : 0;
    }

    private static double ComputeCompletenessScore(DedupCandidateRecord candidate)
    {
        var score = 0.0;
        if (candidate.HasStableIdentifier)
        {
            score += 4.0;
        }

        if (!string.IsNullOrWhiteSpace(candidate.PrimaryWorkId))
        {
            score += 1.5;
        }

        score += candidate.WorkIds.Count * 0.25;

        if (!string.IsNullOrWhiteSpace(candidate.Title))
        {
            score += 1.0;
        }

        if (candidate.Authors.Count > 0)
        {
            score += 1.5;
        }

        if (candidate.Year.HasValue)
        {
            score += 0.75;
        }

        if (!string.IsNullOrWhiteSpace(candidate.Venue))
        {
            score += 0.75;
        }

        if (!string.IsNullOrWhiteSpace(candidate.Abstract))
        {
            score += 1.0;
        }

        if (candidate.Keywords.Count > 0)
        {
            score += 0.5;
        }

        return Math.Round(score, 4, MidpointRounding.AwayFromZero);
    }

    private static double ComputeTitleSimilarity(string left, string right)
    {
        var normalizedLeft = NormalizeForSimilarity(left);
        var normalizedRight = NormalizeForSimilarity(right);

        if (normalizedLeft.Length == 0 || normalizedRight.Length == 0)
        {
            return 0.0;
        }

        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal))
        {
            return 1.0;
        }

        var maxLength = Math.Max(normalizedLeft.Length, normalizedRight.Length);
        var distance = LevenshteinDistance(normalizedLeft, normalizedRight);
        return Math.Round(1.0 - (distance / (double)maxLength), 4, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeForSimilarity(string value)
    {
        var lowered = (value ?? string.Empty).ToLowerInvariant();
        var alnumOnly = Regex.Replace(lowered, "[^a-z0-9 ]", " ", RegexOptions.CultureInvariant);
        var collapsed = Regex.Replace(alnumOnly, "\\s+", " ", RegexOptions.CultureInvariant).Trim();
        return collapsed;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var leftLength = left.Length;
        var rightLength = right.Length;

        if (leftLength == 0)
        {
            return rightLength;
        }

        if (rightLength == 0)
        {
            return leftLength;
        }

        var previousRow = new int[rightLength + 1];
        var currentRow = new int[rightLength + 1];

        for (var column = 0; column <= rightLength; column++)
        {
            previousRow[column] = column;
        }

        for (var leftIndex = 0; leftIndex < leftLength; leftIndex++)
        {
            currentRow[0] = leftIndex + 1;
            for (var rightIndex = 0; rightIndex < rightLength; rightIndex++)
            {
                var cost = left[leftIndex] == right[rightIndex] ? 0 : 1;
                currentRow[rightIndex + 1] = Math.Min(
                    Math.Min(currentRow[rightIndex] + 1, previousRow[rightIndex + 1] + 1),
                    previousRow[rightIndex] + cost);
            }

            var temp = previousRow;
            previousRow = currentRow;
            currentRow = temp;
        }

        return previousRow[rightLength];
    }

    private static string BuildClusterId(IEnumerable<int> indexes)
    {
        var orderedIndexes = indexes.OrderBy(index => index);
        var material = string.Join('|', orderedIndexes);
        var digest = ContentDigest.Sha256Utf8(material).ToString();
        return "cluster-" + digest.AsSpan("sha256:".Length).ToString()[..12];
    }

    private static string BuildEvidenceId(string source, string leftCandidateId, string rightCandidateId, string? context = null)
    {
        var pair = BuildEvidencePairKey(leftCandidateId, rightCandidateId);
        return context is null
            ? $"{source}:{pair}"
            : $"{source}:{pair}:{context}";
    }

    private static string BuildEvidencePairKey(string leftCandidateId, string rightCandidateId)
    {
        return string.Compare(leftCandidateId, rightCandidateId, StringComparison.Ordinal) <= 0
            ? $"{leftCandidateId}:{rightCandidateId}"
            : $"{rightCandidateId}:{leftCandidateId}";
    }

    private static string BuildEvidenceReason(string code, string context)
    {
        return $"{code}:{context}|policy={PolicyId}/{PolicyVersion}";
    }

    private static string ComputePrimaryIdentifierSortKey(DedupCandidateRecord candidate)
    {
        return candidate.PrimaryWorkId?.ToString() ?? string.Empty;
    }

    private static DedupEvidence CreateSourceEvidence(DedupCandidateRecord candidate)
    {
        var reasonParts = new List<string>
        {
            $"source-trace:{candidate.Source.SourceTraceId}",
            $"source:{candidate.Source.SourceSightingId}"
        };

        if (!string.IsNullOrWhiteSpace(candidate.Source.SourceFileDigest))
        {
            reasonParts.Add($"source-file-digest:{candidate.Source.SourceFileDigest}");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Source.SourceFileDigestScope))
        {
            reasonParts.Add($"source-file-digest-scope:{candidate.Source.SourceFileDigestScope}");
        }

        if (!string.IsNullOrWhiteSpace(candidate.Source.RawRecordDigest))
        {
            reasonParts.Add($"raw-record-digest:{candidate.Source.RawRecordDigest}");
        }

        if (candidate.Source.ParserWarnings.Count > 0)
        {
            reasonParts.Add($"parser-warnings:{candidate.Source.ParserWarnings.Count}");
        }

        if (candidate.Source.RecordNotices.Count > 0)
        {
            reasonParts.Add($"record-notices:{candidate.Source.RecordNotices.Count}");
        }

        reasonParts.Add($"policy={PolicyId}/{PolicyVersion}");

        return new DedupEvidence(
            BuildEvidenceId("source", candidate.CandidateId, candidate.CandidateId),
            DedupEvidenceKind.SourceSighting,
            candidate.CandidateId,
            candidate.CandidateId,
            string.Join('|', reasonParts),
            ReviewRequired: false,
            0.0,
            PolicyId,
            PolicyVersion);
    }

    private static void AddSourceSpecificEvidence(DedupCandidateRecord candidate, ICollection<DedupEvidence> evidence)
    {
        foreach (var sourceSpecificId in candidate.SourceSpecificIds)
        {
            evidence.Add(new DedupEvidence(
                BuildEvidenceId("source-specific-id", candidate.CandidateId, sourceSpecificId),
                DedupEvidenceKind.SourceSpecificIdentifier,
                candidate.CandidateId,
                null,
                $"source-specific-id:{sourceSpecificId}|policy={PolicyId}/{PolicyVersion}",
                ReviewRequired: true,
                0.0,
                PolicyId,
                PolicyVersion));
        }
    }

    private static DedupEvidence CreateSourceSpecificPairEvidence(
        DedupCandidateRecord leftCandidate,
        DedupCandidateRecord rightCandidate,
        double similarity)
    {
        return new DedupEvidence(
            BuildEvidenceId("source-specific", leftCandidate.CandidateId, rightCandidate.CandidateId),
            DedupEvidenceKind.SourceSpecificIdentifier,
            leftCandidate.CandidateId,
            rightCandidate.CandidateId,
            $"source-specific-id-overlap:{similarity.ToString("0.####", CultureInfo.InvariantCulture)}|policy={PolicyId}/{PolicyVersion}",
            ReviewRequired: true,
            1.0,
            PolicyId,
            PolicyVersion);
    }

    private static DedupEvidence CreateNoIdEvidence(DedupCandidateRecord candidate)
    {
        return new DedupEvidence(
            BuildEvidenceId("no-id", candidate.CandidateId, candidate.CandidateId),
            DedupEvidenceKind.NoIdCandidate,
            candidate.CandidateId,
            null,
            "no-stable-identifier",
            ReviewRequired: true,
            0.0,
            PolicyId,
            PolicyVersion);
    }

    private static bool ContainsCandidate(DedupEvidence evidence, IReadOnlyList<DedupCandidateRecord> members)
    {
        if (!members.Any(member => string.Equals(member.CandidateId, evidence.SubjectCandidateId, StringComparison.Ordinal)))
        {
            return false;
        }

        return evidence.ObjectCandidateId is null
            || members.Any(member => string.Equals(member.CandidateId, evidence.ObjectCandidateId, StringComparison.Ordinal));
    }

    private static string[] DistinctNonBlank(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<DedupParserNotice> ConvertNotices(IEnumerable<SearchImportParserNotice> notices)
    {
        return DistinctNotices(notices.Select(notice => new DedupParserNotice(
            notice.Category,
            notice.Message,
            notice.RecordIndex,
            notice.SourceRecordId)));
    }

    private static IReadOnlyList<DedupParserNotice> DistinctNotices(IEnumerable<DedupParserNotice> notices)
    {
        return new ReadOnlyCollection<DedupParserNotice>(notices
            .GroupBy(NoticeKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(notice => notice.Category, StringComparer.Ordinal)
            .ThenBy(notice => notice.Message, StringComparer.Ordinal)
            .ThenBy(notice => notice.RecordIndex ?? -1)
            .ThenBy(notice => notice.SourceRecordId, StringComparer.Ordinal)
            .ToArray());
    }

    private static string NoticeKey(DedupParserNotice notice)
    {
        return string.Join(
            '\u001f',
            notice.Category,
            notice.Message,
            notice.RecordIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            notice.SourceRecordId ?? string.Empty);
    }

    private sealed class UnionFind
    {
        private readonly int[] _parent;
        private readonly int[] _rank;

        public UnionFind(int count)
        {
            _parent = Enumerable.Range(0, count).ToArray();
            _rank = new int[count];
        }

        public int Find(int index)
        {
            var current = index;
            while (_parent[current] != current)
            {
                current = _parent[current];
            }

            var root = current;
            while (_parent[index] != index)
            {
                var next = _parent[index];
                _parent[index] = root;
                index = next;
            }

            return root;
        }

        public void Union(int left, int right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot == rightRoot)
            {
                return;
            }

            if (_rank[leftRoot] < _rank[rightRoot])
            {
                (leftRoot, rightRoot) = (rightRoot, leftRoot);
            }

            _parent[rightRoot] = leftRoot;
            if (_rank[leftRoot] == _rank[rightRoot])
            {
                _rank[leftRoot]++;
            }
        }
    }
}
