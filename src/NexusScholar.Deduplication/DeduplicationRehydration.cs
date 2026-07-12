using System.Collections.ObjectModel;

namespace NexusScholar.Deduplication;

public static class DeduplicationAuthorityErrorCodes
{
    public const string InvalidResult = "invalid-deduplication-result";
    public const string InvalidCandidate = "invalid-deduplication-candidate";
    public const string InvalidCluster = "invalid-deduplication-cluster";
    public const string InvalidEvidence = "invalid-deduplication-evidence";
    public const string NonFiniteScore = "non-finite-deduplication-score";
}

public sealed class DeduplicationAuthorityException : InvalidOperationException
{
    public DeduplicationAuthorityException(string category, string message) : base(message) => Category = category;
    public string Category { get; }
}

public sealed record UnverifiedDeduplicationResult(DeduplicationResult Result);

public sealed class VerifiedDeduplicationResult
{
    internal VerifiedDeduplicationResult(DeduplicationResult result) => Result = result;
    public DeduplicationResult Result { get; }
}

public static class DeduplicationRehydrator
{
    public static VerifiedDeduplicationResult Rehydrate(UnverifiedDeduplicationResult input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var result = input.Result ?? throw Invalid(DeduplicationAuthorityErrorCodes.InvalidResult, "Deduplication result is required.");
        if (string.IsNullOrWhiteSpace(result.ResultId) || result.SchemaId != DeduplicationService.ResultSchemaId ||
            result.SchemaVersion != DeduplicationService.ResultSchemaVersion || result.PolicyId != DeduplicationService.PolicyId ||
            result.PolicyVersion != DeduplicationService.PolicyVersion)
        {
            throw Invalid(DeduplicationAuthorityErrorCodes.InvalidResult, "Deduplication result identity, schema, or policy is invalid.");
        }
        if (result.FuzzyTitleThreshold is not { } threshold || !double.IsFinite(threshold) || threshold < 0 || threshold > 1)
        {
            throw Invalid(DeduplicationAuthorityErrorCodes.NonFiniteScore, "Deduplication threshold must be finite and between zero and one.");
        }

        var candidateIds = Unique(result.RawCandidates, item => item.CandidateId, DeduplicationAuthorityErrorCodes.InvalidCandidate);
        foreach (var candidate in result.RawCandidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Title) || candidate.Source is null ||
                candidate.WorkIds.Any(string.IsNullOrWhiteSpace) || candidate.SourceSpecificIds.Any(string.IsNullOrWhiteSpace))
            {
                throw Invalid(DeduplicationAuthorityErrorCodes.InvalidCandidate, "Deduplication candidate is malformed.");
            }
        }

        var evidenceIds = Unique(result.Evidence, item => item.EvidenceId, DeduplicationAuthorityErrorCodes.InvalidEvidence);
        foreach (var evidence in result.Evidence)
        {
            if (!candidateIds.Contains(evidence.SubjectCandidateId) ||
                (evidence.ObjectCandidateId is not null && !candidateIds.Contains(evidence.ObjectCandidateId)) ||
                (evidence.Score is { } score && !double.IsFinite(score)))
            {
                throw Invalid(evidence.Score is { } value && !double.IsFinite(value)
                    ? DeduplicationAuthorityErrorCodes.NonFiniteScore
                    : DeduplicationAuthorityErrorCodes.InvalidEvidence, "Deduplication evidence is malformed.");
            }
        }

        _ = evidenceIds;
        var clusterIds = Unique(result.Clusters, item => item.ClusterId, DeduplicationAuthorityErrorCodes.InvalidCluster);
        var clustered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cluster in result.Clusters)
        {
            var members = Unique(cluster.Members, item => item.CandidateId, DeduplicationAuthorityErrorCodes.InvalidCluster);
            if (members.Count == 0 || members.Any(id => !candidateIds.Contains(id)) || members.Any(id => !clustered.Add(id)) ||
                !members.Contains(cluster.Representative.CandidateId) || !double.IsFinite(cluster.Representative.CompletenessScore) ||
                cluster.Evidence.Any(item => !members.Contains(item.SubjectCandidateId) ||
                    (item.ObjectCandidateId is not null && !members.Contains(item.ObjectCandidateId))))
            {
                throw Invalid(DeduplicationAuthorityErrorCodes.InvalidCluster, "Deduplication cluster membership or representative is invalid.");
            }
        }
        _ = clusterIds;
        foreach (var review in result.ReviewRequiredCandidates)
        {
            if (!candidateIds.Contains(review.CandidateAId) || !candidateIds.Contains(review.CandidateBId) ||
                review.CandidateAId == review.CandidateBId || !double.IsFinite(review.TitleSimilarity) ||
                !double.IsFinite(review.ThresholdUsed))
            {
                throw Invalid(!double.IsFinite(review.TitleSimilarity) || !double.IsFinite(review.ThresholdUsed)
                    ? DeduplicationAuthorityErrorCodes.NonFiniteScore
                    : DeduplicationAuthorityErrorCodes.InvalidEvidence, "Deduplication review pair is invalid.");
            }
        }
        if (result.UnresolvedCandidates.Any(item => !candidateIds.Contains(item.CandidateId) || item.HasStableIdentifier))
        {
            throw Invalid(DeduplicationAuthorityErrorCodes.InvalidCandidate, "Unresolved candidates must be no-ID raw candidates.");
        }

        return new VerifiedDeduplicationResult(Clone(result));
    }

    private static HashSet<string> Unique<T>(IReadOnlyList<T> items, Func<T, string> id, string category)
    {
        var values = items.Select(id).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw Invalid(category, "Deduplication identities must be present and unique.");
        }
        return values.ToHashSet(StringComparer.Ordinal);
    }

    private static DeduplicationResult Clone(DeduplicationResult source) => source with
    {
        ProviderPriority = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(source.ProviderPriority, StringComparer.Ordinal)),
        SourceSearchTraceIds = Array.AsReadOnly(source.SourceSearchTraceIds.ToArray()),
        SourceImportTraceIds = Array.AsReadOnly(source.SourceImportTraceIds.ToArray()),
        RawCandidates = Array.AsReadOnly(source.RawCandidates.Select(CloneCandidate).ToArray()),
        Clusters = Array.AsReadOnly(source.Clusters.Select(cluster => cluster with
        {
            Members = Array.AsReadOnly(cluster.Members.Select(CloneCandidate).ToArray()),
            Representative = cluster.Representative with { },
            Evidence = Array.AsReadOnly(cluster.Evidence.ToArray())
        }).ToArray()),
        Evidence = Array.AsReadOnly(source.Evidence.ToArray()),
        UnresolvedCandidates = Array.AsReadOnly(source.UnresolvedCandidates.Select(CloneCandidate).ToArray()),
        ReviewRequiredCandidates = Array.AsReadOnly(source.ReviewRequiredCandidates.ToArray()),
        Warnings = Array.AsReadOnly(source.Warnings.ToArray()),
        Errors = Array.AsReadOnly(source.Errors.ToArray()),
        NonClaims = Array.AsReadOnly(source.NonClaims.ToArray())
    };

    private static DedupCandidateRecord CloneCandidate(DedupCandidateRecord item) => item with
    {
        WorkIds = Array.AsReadOnly(item.WorkIds.ToArray()),
        SourceSpecificIds = Array.AsReadOnly(item.SourceSpecificIds.ToArray()),
        Source = item.Source with { }
    };

    private static DeduplicationAuthorityException Invalid(string category, string message) => new(category, message);
}
