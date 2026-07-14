using System.Collections.ObjectModel;
using System.Linq;
using NexusScholar.Kernel;
using NexusScholar.Shared;

namespace NexusScholar.Deduplication;

public static class DeduplicationAuthorityDigestErrorCodes
{
    public const string DuplicateAuthorityMaterial = "duplicate-deduplication-authority-material";
    public const string NonCanonicalAuthorityMaterial = "non-canonical-deduplication-authority-material";
    public const string StaleAuthoritySourceBinding = "stale-deduplication-authority-source-binding";
    public const string InvalidAuthorityTarget = "invalid-deduplication-review-target";
}

public sealed record UnverifiedDeduplicationAuthorityResultDigest(
    DeduplicationResult Result,
    ContentDigest ResultDigest);

public sealed class VerifiedDeduplicationAuthorityResultDigest
{
    internal VerifiedDeduplicationAuthorityResultDigest(DeduplicationResult result, DigestEnvelope digestEnvelope)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
        DigestEnvelope = digestEnvelope ?? throw new ArgumentNullException(nameof(digestEnvelope));
    }

    public DeduplicationResult Result { get; }

    public DigestEnvelope DigestEnvelope { get; }

    public ContentDigest ResultDigest => DigestEnvelope.ComputeDigest();
}

public sealed record UnverifiedDeduplicationAuthorityCandidateDigest(DedupCandidateRecord Candidate, ContentDigest CandidateDigest);

public sealed class VerifiedDeduplicationAuthorityCandidateDigest
{
    internal VerifiedDeduplicationAuthorityCandidateDigest(DedupCandidateRecord candidate, DigestEnvelope digestEnvelope)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        DigestEnvelope = digestEnvelope ?? throw new ArgumentNullException(nameof(digestEnvelope));
    }

    public DedupCandidateRecord Candidate { get; }

    public DigestEnvelope DigestEnvelope { get; }

    public ContentDigest CandidateDigest => DigestEnvelope.ComputeDigest();
}

public sealed record UnverifiedDeduplicationAuthorityEvidenceDigest(DedupEvidence Evidence, ContentDigest EvidenceDigest);

public sealed class VerifiedDeduplicationAuthorityEvidenceDigest
{
    internal VerifiedDeduplicationAuthorityEvidenceDigest(DedupEvidence evidence, DigestEnvelope digestEnvelope)
    {
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        DigestEnvelope = digestEnvelope ?? throw new ArgumentNullException(nameof(digestEnvelope));
    }

    public DedupEvidence Evidence { get; }

    public DigestEnvelope DigestEnvelope { get; }

    public ContentDigest EvidenceDigest => DigestEnvelope.ComputeDigest();
}

public sealed record UnverifiedDeduplicationAuthorityReviewTargetDigest(
    string SchemaId,
    string SchemaVersion,
    string TargetKind,
    string TargetId,
    string SourceResultId,
    ContentDigest SourceResultDigest,
    IReadOnlyList<string> CandidateIds,
    DedupReviewCandidate ReviewPair,
    IReadOnlyList<DedupEvidence> Evidence,
    ContentDigest TargetDigest);

public sealed class VerifiedDeduplicationAuthorityReviewTargetDigest
{
    internal VerifiedDeduplicationAuthorityReviewTargetDigest(
        string targetId,
        string targetKind,
        IReadOnlyList<string> candidateIds,
        DedupReviewCandidate reviewPair,
        IReadOnlyList<DedupEvidence> evidence,
        ContentDigest targetDigest,
        DigestEnvelope digestEnvelope)
    {
        TargetId = Guard.NotBlank(targetId, nameof(targetId));
        TargetKind = Guard.NotBlank(targetKind, nameof(targetKind));
        CandidateIds = Array.AsReadOnly((candidateIds ?? throw new ArgumentNullException(nameof(candidateIds))).ToArray());
        ReviewPair = reviewPair ?? throw new ArgumentNullException(nameof(reviewPair));
        Evidence = Array.AsReadOnly((evidence ?? throw new ArgumentNullException(nameof(evidence))).ToArray());
        TargetDigest = targetDigest;
        DigestEnvelope = digestEnvelope ?? throw new ArgumentNullException(nameof(digestEnvelope));
    }

    public string TargetId { get; }

    public string TargetKind { get; }

    public IReadOnlyList<string> CandidateIds { get; }

    public DedupReviewCandidate ReviewPair { get; }

    public IReadOnlyList<DedupEvidence> Evidence { get; }

    public ContentDigest TargetDigest { get; }

    public DigestEnvelope DigestEnvelope { get; }
}

public static class DeduplicationAuthorityDigests
{
    public const string ResultSchemaId = DeduplicationService.ResultSchemaId;
    public const string ResultSchemaVersion = DeduplicationService.ResultSchemaVersion;
    public const string CandidateSchemaId = "nexus.deduplication.candidate";
    public const string CandidateSchemaVersion = "1.0.0";
    public const string EvidenceSchemaId = "nexus.deduplication.evidence";
    public const string EvidenceSchemaVersion = "1.0.0";
    public const string ReviewTargetSchemaId = "nexus.deduplication.review-target";
    public const string ReviewTargetSchemaVersion = "1.0.0";

    public static VerifiedDeduplicationAuthorityResultDigest CreateResultDigestMaterial(DeduplicationResult result)
    {
        var verified = DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(result));
        var content = BuildResultContent(verified.Result, canonicalizeCollections: true);
        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ResultSchemaId, ResultSchemaVersion, content);
        return new VerifiedDeduplicationAuthorityResultDigest(verified.Result, envelope);
    }

    public static VerifiedDeduplicationAuthorityResultDigest RehydrateResultDigestMaterial(
        UnverifiedDeduplicationAuthorityResultDigest input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var verified = DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(input.Result ?? throw new ArgumentNullException(nameof(input.Result))));
        var canonical = BuildResultContent(verified.Result, true);
        var provided = BuildResultContent(verified.Result, false);
        EnsureCanonicalInput("result", provided, canonical);

        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ResultSchemaId, ResultSchemaVersion, canonical);
        var computed = envelope.ComputeDigest();
        if (computed != input.ResultDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.StaleAuthoritySourceBinding,
                "Deduplication result digest does not match persisted authority material.");
        }

        return new VerifiedDeduplicationAuthorityResultDigest(verified.Result, envelope);
    }

    public static VerifiedDeduplicationAuthorityCandidateDigest CreateCandidateDigestMaterial(DedupCandidateRecord candidate)
    {
        ValidateCandidate(candidate);

        var content = BuildCandidateContent(candidate, canonicalizeCollections: true);
        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, CandidateSchemaId, CandidateSchemaVersion, content);
        return new VerifiedDeduplicationAuthorityCandidateDigest(FreezeCandidate(candidate), envelope);
    }

    public static VerifiedDeduplicationAuthorityCandidateDigest RehydrateCandidateDigestMaterial(
        UnverifiedDeduplicationAuthorityCandidateDigest input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateCandidate(input.Candidate);
        var canonical = BuildCandidateContent(input.Candidate, true);
        var provided = BuildCandidateContent(input.Candidate, false);
        EnsureCanonicalInput("candidate", provided, canonical);

        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, CandidateSchemaId, CandidateSchemaVersion, canonical);
        var computed = envelope.ComputeDigest();
        if (computed != input.CandidateDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.StaleAuthoritySourceBinding,
                "Deduplication candidate digest does not match persisted authority material.");
        }

        return new VerifiedDeduplicationAuthorityCandidateDigest(FreezeCandidate(input.Candidate), envelope);
    }

    public static VerifiedDeduplicationAuthorityEvidenceDigest CreateEvidenceDigestMaterial(DedupEvidence evidence)
    {
        ValidateEvidence(evidence);

        var content = BuildEvidenceContent(evidence, canonicalizeCollections: true);
        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, EvidenceSchemaId, EvidenceSchemaVersion, content);
        return new VerifiedDeduplicationAuthorityEvidenceDigest(evidence, envelope);
    }

    public static VerifiedDeduplicationAuthorityEvidenceDigest RehydrateEvidenceDigestMaterial(
        UnverifiedDeduplicationAuthorityEvidenceDigest input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidateEvidence(input.Evidence);
        var canonical = BuildEvidenceContent(input.Evidence, true);
        var provided = BuildEvidenceContent(input.Evidence, false);
        EnsureCanonicalInput("evidence", provided, canonical);

        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, EvidenceSchemaId, EvidenceSchemaVersion, canonical);
        var computed = envelope.ComputeDigest();
        if (computed != input.EvidenceDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.StaleAuthoritySourceBinding,
                "Deduplication evidence digest does not match persisted authority material.");
        }

        return new VerifiedDeduplicationAuthorityEvidenceDigest(input.Evidence, envelope);
    }

    public static VerifiedDeduplicationAuthorityReviewTargetDigest CreateReviewTargetDigestMaterial(
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        DedupReviewCandidate reviewPair,
        IReadOnlyList<string> candidateIds,
        IReadOnlyList<DedupEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(sourceResult);
        ArgumentNullException.ThrowIfNull(reviewPair);
        ArgumentNullException.ThrowIfNull(candidateIds);
        ArgumentNullException.ThrowIfNull(evidence);

        var normalizedPair = NormalizeReviewPair(reviewPair);
        var normalizedCandidateIds = NormalizeAndValidateTargetCandidateIds(candidateIds);
        EnsureCandidateIdsMatchTarget(sourceResult.Result, normalizedPair, normalizedCandidateIds);

        var expectedPairEvidence = sourceResult.Result.Evidence
            .Where(item => IsPairEvidence(normalizedPair, item))
            .ToArray();
        if (expectedPairEvidence.Length == 0)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityErrorCodes.InvalidEvidence,
                "Review target binding has no source evidence for the requested review pair.");
        }

        var expectedEvidenceById = expectedPairEvidence.ToDictionary(item => item.EvidenceId, item => item, StringComparer.Ordinal);
        var candidateDigests = expectedCandidateDigests(sourceResult, normalizedCandidateIds);
        var seenEvidence = new HashSet<string>(StringComparer.Ordinal);
        var normalizedEvidence = (evidence.Count == 0 ? [] : evidence)
            .Select(item =>
            {
                ValidateEvidence(item);
                if (string.IsNullOrWhiteSpace(item.ObjectCandidateId))
                {
                    throw new DeduplicationAuthorityException(
                        DeduplicationAuthorityErrorCodes.InvalidEvidence,
                        "Review target evidence must identify both pair candidates.");
                }

                if (!expectedEvidenceById.ContainsKey(item.EvidenceId))
                {
                    throw new DeduplicationAuthorityException(
                        DeduplicationAuthorityErrorCodes.InvalidEvidence,
                        "Review target evidence is not part of the source result.");
                }

                if (!seenEvidence.Add(item.EvidenceId))
                {
                    throw new DeduplicationAuthorityException(
                        DeduplicationAuthorityDigestErrorCodes.DuplicateAuthorityMaterial,
                        "Review target evidence material contains duplicate evidence entries.");
                }

                var sourceEvidence = expectedEvidenceById[item.EvidenceId];
                if (CreateEvidenceDigestMaterial(sourceEvidence).EvidenceDigest != CreateEvidenceDigestMaterial(item).EvidenceDigest)
                {
                    throw new DeduplicationAuthorityException(
                        DeduplicationAuthorityErrorCodes.InvalidEvidence,
                        "Review target evidence digest does not match the source result evidence.");
                }

                if (!IsPairEvidence(normalizedPair, item))
                {
                    throw new DeduplicationAuthorityException(
                        DeduplicationAuthorityErrorCodes.InvalidEvidence,
                        "Review target evidence does not reference the requested pair of candidates.");
                }

                return item;
            })
            .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();

        if (normalizedEvidence.Length != expectedPairEvidence.Length)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityErrorCodes.InvalidEvidence,
                "Review target evidence does not cover the exact source-pair evidence set.");
        }

        var targetKind = "review-candidate-pair";
        var targetId = BuildReviewTargetId(targetKind, normalizedCandidateIds);
        var canonical = BuildReviewTargetContent(
            targetKind,
            targetId,
            sourceResult.Result.ResultId,
            sourceResult.ResultDigest,
            normalizedCandidateIds,
            normalizedPair,
            normalizedEvidence,
            candidateDigests,
            canonicalizeCollections: true);

        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ReviewTargetSchemaId, ReviewTargetSchemaVersion, canonical);
        return new VerifiedDeduplicationAuthorityReviewTargetDigest(
            targetId,
            targetKind,
            normalizedCandidateIds,
            normalizedPair,
            normalizedEvidence,
            envelope.ComputeDigest(),
            envelope);
    }

    public static VerifiedDeduplicationAuthorityReviewTargetDigest RehydrateReviewTargetDigestMaterial(
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        UnverifiedDeduplicationAuthorityReviewTargetDigest input)
    {
        ArgumentNullException.ThrowIfNull(sourceResult);
        ArgumentNullException.ThrowIfNull(input);

        if (!string.Equals(input.SchemaId, ReviewTargetSchemaId, StringComparison.Ordinal) ||
            !string.Equals(input.SchemaVersion, ReviewTargetSchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(input.TargetKind, "review-candidate-pair", StringComparison.Ordinal) ||
            !string.Equals(input.SourceResultId, sourceResult.Result.ResultId, StringComparison.Ordinal) ||
            input.SourceResultDigest != sourceResult.ResultDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.StaleAuthoritySourceBinding,
                "Persisted review target schema or source-result binding is invalid.");
        }

        var verified = CreateReviewTargetDigestMaterial(sourceResult, input.ReviewPair, input.CandidateIds, input.Evidence);
        if (!string.Equals(input.TargetId, verified.TargetId, StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.InvalidAuthorityTarget,
                "Persisted review target id does not match its candidate membership.");
        }
        var canonical = BuildReviewTargetContent(
            input.TargetKind,
            input.TargetId,
            input.SourceResultId,
            input.SourceResultDigest,
            verified.CandidateIds,
            verified.ReviewPair,
            verified.Evidence,
            expectedCandidateDigests(sourceResult, verified.CandidateIds),
            canonicalizeCollections: true);

        var provided = BuildReviewTargetContent(
            verified.TargetKind,
            verified.TargetId,
            sourceResult.Result.ResultId,
            sourceResult.ResultDigest,
            input.CandidateIds,
            input.ReviewPair,
            input.Evidence,
            expectedCandidateDigests(sourceResult, NormalizeAndValidateTargetCandidateIds(input.CandidateIds)),
            canonicalizeCollections: false);

        EnsureCanonicalInput("review-target", provided, canonical);

        var envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ReviewTargetSchemaId, ReviewTargetSchemaVersion, canonical);
        var computed = envelope.ComputeDigest();
        if (computed != input.TargetDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.StaleAuthoritySourceBinding,
                "Deduplication review target digest does not match persisted authority material.");
        }

        return new VerifiedDeduplicationAuthorityReviewTargetDigest(
            verified.TargetId,
            verified.TargetKind,
            verified.CandidateIds,
            verified.ReviewPair,
            verified.Evidence,
            input.TargetDigest,
            envelope);
    }

    private static CanonicalJsonObject BuildResultContent(DeduplicationResult result, bool canonicalizeCollections)
    {
        var providerPriority = canonicalizeCollections
            ? result.ProviderPriority.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray()
            : result.ProviderPriority.ToArray();

        var rawCandidates = canonicalizeCollections
            ? result.RawCandidates.OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray()
            : result.RawCandidates.ToArray();
        var unresolvedCandidates = canonicalizeCollections
            ? result.UnresolvedCandidates.OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray()
            : result.UnresolvedCandidates.ToArray();

        var clusters = canonicalizeCollections
            ? result.Clusters.OrderBy(item => item.ClusterId, StringComparer.Ordinal).ToArray()
            : result.Clusters.ToArray();

        var evidence = canonicalizeCollections
            ? result.Evidence.OrderBy(item => item.EvidenceId, StringComparer.Ordinal).ToArray()
            : result.Evidence.ToArray();

        var reviewPairs = (canonicalizeCollections
            ? result.ReviewRequiredCandidates
                .Select(NormalizeReviewPair)
                .OrderBy(item => item.CandidateAId, StringComparer.Ordinal)
                .ThenBy(item => item.CandidateBId, StringComparer.Ordinal)
                .ThenBy(item => item.ThresholdUsed, Comparer<double>.Default)
                .ThenBy(item => item.TitleSimilarity, Comparer<double>.Default)
            : result.ReviewRequiredCandidates.Select(ValidateReviewPair));

        return new CanonicalJsonObject()
            .Add("result_id", Guard.NotBlank(result.ResultId, nameof(result.ResultId)))
            .Add("schema_id", Guard.NotBlank(result.SchemaId, nameof(result.SchemaId)))
            .Add("schema_version", Guard.NotBlank(result.SchemaVersion, nameof(result.SchemaVersion)))
            .Add("policy_id", Guard.NotBlank(result.PolicyId, nameof(result.PolicyId)))
            .Add("policy_version", Guard.NotBlank(result.PolicyVersion, nameof(result.PolicyVersion)))
            .Add("fuzzy_title_threshold", result.FuzzyTitleThreshold is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(result.FuzzyTitleThreshold.Value))
            .Add(
                "provider_priority",
                CanonicalJsonValue.Array(providerPriority
                    .Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                        .Add("provider", item.Key)
                        .Add("priority", item.Value))
                    .ToArray()))
            .Add(
                "source_search_trace_ids",
                CanonicalJsonValue.Array((canonicalizeCollections
                    ? result.SourceSearchTraceIds.OrderBy(item => item, StringComparer.Ordinal).ToArray()
                    : result.SourceSearchTraceIds.ToArray()).Select(CanonicalJsonValue.From).ToArray()))
            .Add(
                "source_import_trace_ids",
                CanonicalJsonValue.Array((canonicalizeCollections
                    ? result.SourceImportTraceIds.OrderBy(item => item, StringComparer.Ordinal).ToArray()
                    : result.SourceImportTraceIds.ToArray()).Select(CanonicalJsonValue.From).ToArray()))
            .Add("raw_candidates", CanonicalJsonValue.Array(rawCandidates.Select(candidate => BuildCandidateContent(candidate, canonicalizeCollections)).ToArray()))
            .Add("clusters", CanonicalJsonValue.Array(clusters.Select(cluster => BuildClusterContent(cluster, canonicalizeCollections)).ToArray()))
            .Add("evidence", CanonicalJsonValue.Array(evidence.Select(item => BuildEvidenceContent(item, canonicalizeCollections)).ToArray()))
            .Add("unresolved_candidates", CanonicalJsonValue.Array(unresolvedCandidates.Select(candidate => BuildCandidateContent(candidate, canonicalizeCollections)).ToArray()))
            .Add("review_required_candidates", CanonicalJsonValue.Array(reviewPairs.Select(pair => BuildReviewPairContent(pair, canonicalizeCollections)).ToArray()))
            .Add(
                "warnings",
                CanonicalJsonValue.Array(
                    SortMessages(result.Warnings, canonicalizeCollections)
                        .Select(item => (CanonicalJsonValue)new CanonicalJsonObject().Add("category", Guard.NotBlank(item.Category, "warning.category"))
                            .Add("message", Guard.NotBlank(item.Message, "warning.message")))
                        .ToArray()))
            .Add(
                "errors",
                CanonicalJsonValue.Array(
                    SortMessages(result.Errors, canonicalizeCollections)
                        .Select(item => (CanonicalJsonValue)new CanonicalJsonObject().Add("category", Guard.NotBlank(item.Category, "error.category"))
                            .Add("message", Guard.NotBlank(item.Message, "error.message")))
                        .ToArray()))
            .Add("non_claims", CanonicalJsonValue.Array((canonicalizeCollections
                ? result.NonClaims.OrderBy(item => item, StringComparer.Ordinal).ToArray()
                : result.NonClaims.ToArray())
                .Select(CanonicalJsonValue.From)
                .ToArray()));
    }

    private static CanonicalJsonObject BuildCandidateContent(DedupCandidateRecord candidate, bool canonicalizeCollections)
    {
        ValidateCandidate(candidate);

        var workIds = NormalizeTextCollection(candidate.WorkIds, canonicalizeCollections);
        var sourceSpecificIds = NormalizeTextCollection(candidate.SourceSpecificIds, canonicalizeCollections);
        var keywords = NormalizeTextCollection(candidate.Keywords, canonicalizeCollections);
        var parserWarnings = CandidateNotices(candidate.Source.ParserWarnings, canonicalizeCollections);
        var recordNotices = CandidateNotices(candidate.Source.RecordNotices, canonicalizeCollections);

        return new CanonicalJsonObject()
            .Add("candidate_id", Guard.NotBlank(candidate.CandidateId, nameof(candidate.CandidateId)))
            .Add("title", Guard.NotBlank(candidate.Title, nameof(candidate.Title)))
            .Add("has_stable_identifier", candidate.HasStableIdentifier)
            .Add("primary_work_id", candidate.PrimaryWorkId is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.PrimaryWorkId))
            .Add("work_ids", CanonicalJsonValue.Array(workIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add("source_specific_ids", CanonicalJsonValue.Array(sourceSpecificIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add(
                "source",
                new CanonicalJsonObject()
                    .Add("source_kind", Guard.NotBlank(candidate.Source.SourceKind, nameof(candidate.Source.SourceKind)))
                    .Add("source_trace_id", Guard.NotBlank(candidate.Source.SourceTraceId, nameof(candidate.Source.SourceTraceId)))
                    .Add("source_sighting_id", Guard.NotBlank(candidate.Source.SourceSightingId, nameof(candidate.Source.SourceSightingId)))
                    .Add("provider_alias", candidate.Source.ProviderAlias is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.Source.ProviderAlias))
                    .Add("source_database_or_tool", candidate.Source.SourceDatabaseOrTool is null
                        ? CanonicalJsonValue.Null()
                        : CanonicalJsonValue.From(candidate.Source.SourceDatabaseOrTool))
                    .Add("source_record_id",
                        candidate.Source.SourceRecordId is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.Source.SourceRecordId))
                    .Add(
                        "source_file_digest",
                        candidate.Source.SourceFileDigest is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.Source.SourceFileDigest))
                    .Add(
                        "source_file_digest_scope",
                        candidate.Source.SourceFileDigestScope is null
                            ? CanonicalJsonValue.Null()
                            : CanonicalJsonValue.From(candidate.Source.SourceFileDigestScope))
                    .Add(
                        "raw_record_digest",
                        candidate.Source.RawRecordDigest is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.Source.RawRecordDigest))
                    .Add(
                        "source_context",
                        candidate.Source.SourceContext is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.Source.SourceContext))
                    .Add(
                        "parser_warnings",
                        CanonicalJsonValue.Array(parserWarnings.Select(BuildParserNotice).ToArray()))
                    .Add(
                        "record_notices",
                        CanonicalJsonValue.Array(recordNotices.Select(BuildParserNotice).ToArray())))
            .Add("authors", CanonicalJsonValue.Array(candidate.Authors.Select(CanonicalJsonValue.From).ToArray()))
            .Add("year", candidate.Year is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.Year.Value))
            .Add("venue", candidate.Venue is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.Venue))
            .Add("abstract", candidate.Abstract is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(candidate.Abstract))
            .Add("keywords", CanonicalJsonValue.Array(keywords.Select(CanonicalJsonValue.From).ToArray()));
    }

    private static CanonicalJsonObject BuildClusterContent(DedupCluster cluster, bool canonicalizeCollections)
    {
        var members = canonicalizeCollections
            ? cluster.Members.OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray()
            : cluster.Members.ToArray();

        var evidence = canonicalizeCollections
            ? cluster.Evidence.OrderBy(item => item.EvidenceId, StringComparer.Ordinal).ToArray()
            : cluster.Evidence.ToArray();

        return new CanonicalJsonObject()
            .Add("cluster_id", Guard.NotBlank(cluster.ClusterId, nameof(cluster.ClusterId)))
            .Add("members", CanonicalJsonValue.Array(members.Select(member => BuildClusterMember(member, canonicalizeCollections)).ToArray()))
            .Add("representative", BuildRepresentativeContent(cluster.Representative, canonicalizeCollections))
            .Add("evidence", CanonicalJsonValue.Array(evidence.Select(item => BuildEvidenceContent(item, canonicalizeCollections)).ToArray()));
    }

    private static CanonicalJsonObject BuildClusterMember(DedupCandidateRecord member, bool canonicalizeCollections)
    {
        ValidateCandidate(member);
        return BuildCandidateContent(member, canonicalizeCollections);
    }

    private static CanonicalJsonObject BuildRepresentativeContent(DedupRepresentativeResult representative, bool canonicalizeCollections)
    {
        ValidateDigestCollection(representative.SourceFileDigests, "representative source-file digest");
        ValidateRawArtifactScopes(representative.SourceFileDigestScopes, "representative source-file digest scope");
        ValidateDigestCollection(representative.RawRecordDigests, "representative raw-record digest");

        var workIds = NormalizeTextCollection(representative.WorkIds, canonicalizeCollections);
        var sourceSightingIds = NormalizeTextCollection(representative.SourceSightingIds, canonicalizeCollections);
        var sourceFileDigests = NormalizeTextCollection(representative.SourceFileDigests, canonicalizeCollections);
        var sourceFileDigestScopes = NormalizeTextCollection(representative.SourceFileDigestScopes, canonicalizeCollections);
        var rawRecordDigests = NormalizeTextCollection(representative.RawRecordDigests, canonicalizeCollections);
        var reasonCodes = NormalizeTextCollection(representative.ReasonCodes, canonicalizeCollections);
        var keywords = NormalizeTextCollection(representative.Keywords, canonicalizeCollections);
        var parserWarnings = CandidateNotices(representative.ParserWarnings, canonicalizeCollections);
        var recordNotices = CandidateNotices(representative.RecordNotices, canonicalizeCollections);

        return new CanonicalJsonObject()
            .Add("candidate_id", Guard.NotBlank(representative.CandidateId, nameof(representative.CandidateId)))
            .Add("title", Guard.NotBlank(representative.Title, nameof(representative.Title)))
            .Add("primary_work_id", representative.PrimaryWorkId is null
                ? CanonicalJsonValue.Null()
                : CanonicalJsonValue.From(representative.PrimaryWorkId))
            .Add("work_ids", CanonicalJsonValue.Array(workIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add(
                "source_sighting_ids",
                CanonicalJsonValue.Array(sourceSightingIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add(
                "completeness_score",
                CanonicalJsonValue.From(representative.CompletenessScore))
            .Add("reason_codes", CanonicalJsonValue.Array(reasonCodes.Select(CanonicalJsonValue.From).ToArray()))
            .Add(
                "source_file_digests",
                CanonicalJsonValue.Array(sourceFileDigests.Select(CanonicalJsonValue.From).ToArray()))
            .Add(
                "source_file_digest_scopes",
                CanonicalJsonValue.Array(sourceFileDigestScopes.Select(CanonicalJsonValue.From).ToArray()))
            .Add("raw_record_digests", CanonicalJsonValue.Array(rawRecordDigests.Select(CanonicalJsonValue.From).ToArray()))
            .Add(
                "parser_warnings",
                CanonicalJsonValue.Array(parserWarnings.Select(BuildParserNotice).ToArray()))
            .Add(
                "record_notices",
                CanonicalJsonValue.Array(recordNotices.Select(BuildParserNotice).ToArray()))
            .Add("authors", CanonicalJsonValue.Array(representative.Authors.Select(CanonicalJsonValue.From).ToArray()))
            .Add("year", representative.Year is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(representative.Year.Value))
            .Add("venue", representative.Venue is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(representative.Venue))
            .Add("abstract", representative.Abstract is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(representative.Abstract))
            .Add("keywords", CanonicalJsonValue.Array(keywords.Select(CanonicalJsonValue.From).ToArray()));
    }

    private static CanonicalJsonObject BuildEvidenceContent(DedupEvidence evidence, bool canonicalizeCollections)
    {
        ValidateEvidence(evidence);

        return new CanonicalJsonObject()
            .Add("evidence_id", Guard.NotBlank(evidence.EvidenceId, nameof(evidence.EvidenceId)))
            .Add("kind", evidence.Kind.ToString())
            .Add("subject_candidate_id", Guard.NotBlank(evidence.SubjectCandidateId, nameof(evidence.SubjectCandidateId)))
            .Add("object_candidate_id", evidence.ObjectCandidateId is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(evidence.ObjectCandidateId))
            .Add("reason", evidence.Reason is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(evidence.Reason))
            .Add("review_required", evidence.ReviewRequired)
            .Add("score", evidence.Score is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(evidence.Score.Value))
            .Add("policy_id", Guard.NotBlank(evidence.PolicyId, nameof(evidence.PolicyId)))
            .Add("policy_version", Guard.NotBlank(evidence.PolicyVersion, nameof(evidence.PolicyVersion)));
    }

    private static CanonicalJsonObject BuildReviewPairContent(DedupReviewCandidate pair, bool canonicalizeCollections = true)
    {
        var normalized = canonicalizeCollections ? NormalizeReviewPair(pair) : ValidateReviewPair(pair);
        return new CanonicalJsonObject()
            .Add("candidate_a_id", normalized.CandidateAId)
            .Add("candidate_b_id", normalized.CandidateBId)
            .Add("title_similarity", normalized.TitleSimilarity)
            .Add("threshold_used", normalized.ThresholdUsed);
    }

    private static CanonicalJsonObject BuildReviewTargetContent(
        string targetKind,
        string targetId,
        string sourceResultId,
        ContentDigest sourceResultDigest,
        IReadOnlyList<string> candidateIds,
        DedupReviewCandidate reviewPair,
        IReadOnlyList<DedupEvidence> evidence,
        IReadOnlyList<(string CandidateId, ContentDigest Digest)> candidateDigests,
        bool canonicalizeCollections)
    {
        var normalizedPair = canonicalizeCollections ? NormalizeReviewPair(reviewPair) : ValidateReviewPair(reviewPair);
        var orderedCandidateIds = canonicalizeCollections
            ? candidateIds.OrderBy(item => item, StringComparer.Ordinal).ToArray()
            : candidateIds;

        var evidenceReferences = BuildEvidenceReferences(normalizedPair, evidence, canonicalizeCollections).ToArray();
        var canonicalCandidateDigests = canonicalizeCollections
            ? candidateDigests.OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray()
            : candidateDigests.ToArray();

        var targetContentDigestMaterial = BuildTargetContentDigestMaterial(
            targetKind,
            targetId,
            sourceResultId,
            sourceResultDigest,
            orderedCandidateIds,
            normalizedPair,
            evidenceReferences,
            canonicalCandidateDigests,
            canonicalizeCollections);
        var targetContentDigest = ContentDigest.Sha256CanonicalJson(targetContentDigestMaterial);

        return new CanonicalJsonObject()
            .Add("target_kind", targetKind)
            .Add("target_id", targetId)
            .Add("source_result_id", Guard.NotBlank(sourceResultId, nameof(sourceResultId)))
            .Add("source_result_digest", sourceResultDigest.ToString())
            .Add(
                "candidate_ids",
                CanonicalJsonValue.Array(
                    orderedCandidateIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add("review_pair", BuildReviewPairContent(normalizedPair, canonicalizeCollections))
            .Add("candidate_digests", CanonicalJsonValue.Array(
                (canonicalizeCollections ? canonicalCandidateDigests : candidateDigests)
                    .Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                        .Add("candidate_id", item.CandidateId)
                        .Add("candidate_digest", item.Digest.ToString()))
                    .ToArray()))
            .Add("evidence_references", CanonicalJsonValue.Array(evidenceReferences.Cast<CanonicalJsonValue>().ToArray()))
            .Add("target_content_digest", targetContentDigest.ToString());
    }

    private static CanonicalJsonValue BuildTargetContentDigestMaterial(
        string targetKind,
        string targetId,
        string sourceResultId,
        ContentDigest sourceResultDigest,
        IReadOnlyList<string> candidateIds,
        DedupReviewCandidate reviewPair,
        IReadOnlyList<CanonicalJsonObject> evidenceReferences,
        IEnumerable<(string CandidateId, ContentDigest Digest)> candidateDigests,
        bool canonicalizeCollections)
    {
        var normalizedPair = canonicalizeCollections ? NormalizeReviewPair(reviewPair) : ValidateReviewPair(reviewPair);
        var orderedCandidateDigests = canonicalizeCollections
            ? candidateDigests.OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray()
            : candidateDigests.ToArray();

        return new CanonicalJsonObject()
            .Add("target_kind", Guard.NotBlank(targetKind, nameof(targetKind)))
            .Add("target_id", Guard.NotBlank(targetId, nameof(targetId)))
            .Add("source_result_id", Guard.NotBlank(sourceResultId, nameof(sourceResultId)))
            .Add("source_result_digest", sourceResultDigest.ToString())
            .Add("candidate_ids", CanonicalJsonValue.Array(candidateIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add("review_pair", BuildReviewPairContent(normalizedPair, canonicalizeCollections))
            .Add("evidence_references", CanonicalJsonValue.Array(evidenceReferences.ToArray()))
            .Add("candidate_digests", CanonicalJsonValue.Array(
                orderedCandidateDigests.Select(item => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("candidate_id", item.CandidateId)
                    .Add("candidate_digest", item.Digest.ToString()))
                    .ToArray()));
    }

    private static IReadOnlyList<CanonicalJsonObject> BuildEvidenceReferences(
        DedupReviewCandidate pair,
        IReadOnlyList<DedupEvidence> evidence,
        bool canonicalizeCollections)
    {
        _ = pair;
        var references = evidence.Select(item => new CanonicalJsonObject()
            .Add("evidence_id", Guard.NotBlank(item.EvidenceId, nameof(item.EvidenceId)))
            .Add("kind", item.Kind.ToString())
            .Add("digest_scope", DigestScope.CanonicalJsonRecord.ToString())
            .Add("digest", CreateEvidenceDigestMaterial(item).EvidenceDigest.ToString()));

        return canonicalizeCollections
            ? references.OrderBy(reference => CanonicalStringValue(reference.Properties["kind"]))
                .ThenBy(reference => CanonicalStringValue(reference.Properties["evidence_id"]))
                .ThenBy(reference => CanonicalStringValue(reference.Properties["digest_scope"]))
                .ThenBy(reference => CanonicalStringValue(reference.Properties["digest"]))
                .ToList()
            : references.ToList();
    }

    private static string CanonicalStringValue(CanonicalJsonValue value)
    {
        return value is CanonicalJsonString textValue
            ? textValue.Value
            : CanonicalJsonSerializer.Serialize(value);
    }

    private static IReadOnlyList<(string CandidateId, ContentDigest Digest)> expectedCandidateDigests(
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        IReadOnlyList<string> candidateIds)
    {
        return candidateIds.Select(candidateId =>
            (
                CandidateId: candidateId,
                Digest: CreateCandidateDigestMaterial(
                    sourceResult.Result.RawCandidates.Single(item => string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal))).CandidateDigest))
            .ToArray();
    }

    private static IReadOnlyList<DedupParserNotice> CandidateNotices(IEnumerable<DedupParserNotice> notices, bool canonicalizeCollections)
    {
        var validated = notices.Select(item =>
        {
            if (item is null)
            {
                throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidCandidate, "Parser notice entries must be present.");
            }

            return item;
        });

        return (canonicalizeCollections
                ? validated
                    .OrderBy(item => Guard.NotBlank(item.Category, nameof(item.Category)), StringComparer.Ordinal)
                    .ThenBy(item => item.SourceRecordId ?? string.Empty, StringComparer.Ordinal)
                    .ThenBy(item => item.RecordIndex ?? int.MinValue)
                    .ThenBy(item => Guard.NotBlank(item.Message, nameof(item.Message)), StringComparer.Ordinal)
                : validated)
            .ToArray();
    }

    private static CanonicalJsonObject BuildParserNotice(DedupParserNotice notice)
    {
        return new CanonicalJsonObject()
            .Add("category", Guard.NotBlank(notice.Category, nameof(notice.Category)))
            .Add("message", Guard.NotBlank(notice.Message, nameof(notice.Message)))
            .Add("record_index", notice.RecordIndex is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(notice.RecordIndex.Value))
            .Add("source_record_id", notice.SourceRecordId is null ? CanonicalJsonValue.Null() : CanonicalJsonValue.From(notice.SourceRecordId));
    }

    private static IReadOnlyList<DedupMessage> SortMessages(IReadOnlyList<DedupMessage> messages, bool canonicalizeCollections)
    {
        return (canonicalizeCollections
            ? messages
                .OrderBy(item => Guard.NotBlank(item.Category, nameof(item.Category)), StringComparer.Ordinal)
                .ThenBy(item => Guard.NotBlank(item.Message, nameof(item.Message)), StringComparer.Ordinal)
                .ToArray()
            : messages.ToArray());
    }

    private static IReadOnlyList<string> NormalizeTextCollection(IReadOnlyList<string> values, bool canonicalizeCollections)
    {
        var normalized = values.Select(item => Guard.NotBlank(item, nameof(values)));
        return canonicalizeCollections
            ? normalized.OrderBy(item => item, StringComparer.Ordinal).ToArray()
            : normalized.ToArray();
    }

    private static IReadOnlyList<string> NormalizeAndValidateTargetCandidateIds(IReadOnlyList<string> candidateIds)
    {
        var normalized = candidateIds
            .Select(item => Guard.NotBlank(item, nameof(candidateIds)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length != 2)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityErrorCodes.InvalidCandidate,
                "Review target must contain exactly two distinct candidate IDs.");
        }

        Array.Sort(normalized, StringComparer.Ordinal);
        return normalized;
    }

    private static void EnsureCandidateIdsMatchTarget(
        DeduplicationResult result,
        DedupReviewCandidate normalizedPair,
        IReadOnlyList<string> candidateIds)
    {
        var sourceCandidateIds = result.RawCandidates.Select(item => item.CandidateId).ToHashSet(StringComparer.Ordinal);
        if (!candidateIds.All(id => sourceCandidateIds.Contains(id)))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityErrorCodes.InvalidCandidate,
                "Review target references a candidate that is not present in the source result.");
        }

        if (normalizedPair.CandidateAId != candidateIds[0] || normalizedPair.CandidateBId != candidateIds[1])
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityErrorCodes.InvalidCandidate,
                "Review target pair and candidate IDs do not align.");
        }
    }

    private static DedupReviewCandidate NormalizeReviewPair(DedupReviewCandidate pair)
    {
        pair = ValidateReviewPair(pair);

        if (string.Compare(pair.CandidateAId, pair.CandidateBId, StringComparison.Ordinal) <= 0)
        {
            return pair;
        }

        return new DedupReviewCandidate(pair.CandidateBId, pair.CandidateAId, pair.TitleSimilarity, pair.ThresholdUsed);
    }

    private static DedupReviewCandidate ValidateReviewPair(DedupReviewCandidate pair)
    {
        if (string.IsNullOrWhiteSpace(pair.CandidateAId))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidEvidence, "Review pair candidate A is required.");
        }

        if (string.IsNullOrWhiteSpace(pair.CandidateBId))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidEvidence, "Review pair candidate B is required.");
        }

        if (!double.IsFinite(pair.TitleSimilarity) || !double.IsFinite(pair.ThresholdUsed))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.StaleAuthoritySourceBinding,
                "Review pair scores must be finite.");
        }

        if (string.Equals(pair.CandidateAId, pair.CandidateBId, StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidCandidate, "Review pair candidates must be distinct.");
        }

        return pair;
    }

    private static bool IsPairEvidence(DedupReviewCandidate pair, DedupEvidence evidence)
    {
        return (string.Equals(pair.CandidateAId, evidence.SubjectCandidateId, StringComparison.Ordinal)
                && string.Equals(pair.CandidateBId, evidence.ObjectCandidateId, StringComparison.Ordinal))
            || (string.Equals(pair.CandidateBId, evidence.SubjectCandidateId, StringComparison.Ordinal)
                && string.Equals(pair.CandidateAId, evidence.ObjectCandidateId, StringComparison.Ordinal));
    }

    private static void ValidateCandidate(DedupCandidateRecord candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        _ = Guard.NotBlank(candidate.CandidateId, nameof(candidate.CandidateId));
        _ = Guard.NotBlank(candidate.Title, nameof(candidate.Title));
        _ = Guard.NotBlank(candidate.Source.SourceKind, nameof(candidate.Source.SourceKind));
        _ = Guard.NotBlank(candidate.Source.SourceTraceId, nameof(candidate.Source.SourceTraceId));
        _ = Guard.NotBlank(candidate.Source.SourceSightingId, nameof(candidate.Source.SourceSightingId));

        _ = candidate.WorkIds;
        _ = candidate.SourceSpecificIds;
        _ = candidate.Authors;
        _ = candidate.Keywords;

        if (candidate.WorkIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidCandidate, "Candidate work ids cannot contain blank values.");
        }

        if (candidate.SourceSpecificIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidCandidate, "Candidate source-specific ids cannot contain blank values.");
        }

        if (candidate.Authors.Any(string.IsNullOrWhiteSpace))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidCandidate, "Candidate authors cannot contain blank values.");
        }

        if (candidate.Keywords.Any(string.IsNullOrWhiteSpace))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidCandidate, "Candidate keywords cannot contain blank values.");
        }

        if ((candidate.Source.SourceFileDigest is null) != (candidate.Source.SourceFileDigestScope is null))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial,
                "Candidate source-file digest and scope must either both be present or both be absent.");
        }

        if (candidate.Source.SourceFileDigest is not null)
        {
            ValidateDigest(candidate.Source.SourceFileDigest, "candidate source-file digest");
            ValidateRawArtifactScope(candidate.Source.SourceFileDigestScope!, "candidate source-file digest scope");
        }

        if (candidate.Source.RawRecordDigest is not null)
        {
            ValidateDigest(candidate.Source.RawRecordDigest, "candidate raw-record digest");
        }
    }

    private static void ValidateEvidence(DedupEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        _ = Guard.NotBlank(evidence.EvidenceId, nameof(evidence.EvidenceId));
        _ = Guard.NotBlank(evidence.SubjectCandidateId, nameof(evidence.SubjectCandidateId));
        if (string.IsNullOrWhiteSpace(evidence.PolicyId))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidEvidence, "Evidence policy id is required.");
        }

        if (string.IsNullOrWhiteSpace(evidence.PolicyVersion))
        {
            throw new DeduplicationAuthorityException(DeduplicationAuthorityErrorCodes.InvalidEvidence, "Evidence policy version is required.");
        }

        if (evidence.Score.HasValue && !double.IsFinite(evidence.Score.Value))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityErrorCodes.NonFiniteScore,
                "Evidence score must be finite.");
        }
    }

    private static void EnsureCanonicalInput(string label, CanonicalJsonValue provided, CanonicalJsonValue canonicalized)
    {
        string providedText;
        try
        {
            providedText = CanonicalJsonSerializer.Serialize(
                provided,
                new CanonicalJsonSerializerOptions { StringNormalization = CanonicalStringNormalizationMode.RequireNormalized });
        }
        catch (InvalidOperationException exception)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial,
                $"{label} authority material contains non-NFC text: {exception.Message}");
        }

        if (!string.Equals(providedText, Canonicalize(canonicalized), StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial,
                $"{label} authority material is not in canonical collection order.");
        }
    }

    private static string BuildReviewTargetId(string targetKind, IReadOnlyList<string> candidateIds)
    {
        return $"{targetKind}:{candidateIds[0]}:{candidateIds[1]}";
    }

    private static string Canonicalize(CanonicalJsonValue value) => CanonicalJsonSerializer.Serialize(value);

    private static DedupCandidateRecord FreezeCandidate(DedupCandidateRecord candidate) => candidate with
    {
        WorkIds = Array.AsReadOnly(candidate.WorkIds.ToArray()),
        SourceSpecificIds = Array.AsReadOnly(candidate.SourceSpecificIds.ToArray()),
        Authors = Array.AsReadOnly(candidate.Authors.ToArray()),
        Keywords = Array.AsReadOnly(candidate.Keywords.ToArray()),
        Source = candidate.Source with
        {
            ParserWarnings = Array.AsReadOnly(candidate.Source.ParserWarnings.ToArray()),
            RecordNotices = Array.AsReadOnly(candidate.Source.RecordNotices.ToArray())
        }
    };

    private static void ValidateDigestCollection(IEnumerable<string> values, string label)
    {
        foreach (var value in values)
        {
            ValidateDigest(value, label);
        }
    }

    private static void ValidateRawArtifactScopes(IEnumerable<string> values, string label)
    {
        foreach (var value in values)
        {
            ValidateRawArtifactScope(value, label);
        }
    }

    private static void ValidateDigest(string value, string label)
    {
        if (!ContentDigest.TryParse(value, out _))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial,
                $"{label} must be a canonical lowercase SHA-256 digest.");
        }
    }

    private static void ValidateRawArtifactScope(string value, string label)
    {
        if (!DigestScope.TryParse(value, out var scope) || scope != DigestScope.RawArtifactBytes)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationAuthorityDigestErrorCodes.NonCanonicalAuthorityMaterial,
                $"{label} must be '{DigestScope.RawArtifactBytes}'.");
        }
    }
}
