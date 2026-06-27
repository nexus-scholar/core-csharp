using System.Collections.ObjectModel;

namespace NexusScholar.Deduplication;

public sealed record DedupSightingRef(
    string SourceKind,
    string SourceTraceId,
    string SourceSightingId,
    string? ProviderAlias = null,
    string? SourceDatabaseOrTool = null,
    string? SourceRecordId = null,
    string? SourceFileDigest = null,
    string? RawRecordDigest = null,
    string? SourceContext = null);

public sealed record DedupCandidateRecord(
    string CandidateId,
    string Title,
    bool HasStableIdentifier,
    string? PrimaryWorkId,
    IReadOnlyList<string> WorkIds,
    IReadOnlyList<string> SourceSpecificIds,
    DedupSightingRef Source);

public sealed record DedupEvidence(
    string EvidenceId,
    DedupEvidenceKind Kind,
    string SubjectCandidateId,
    string? ObjectCandidateId,
    string? Reason,
    double? Score = null,
    string? PolicyId = null,
    string? PolicyVersion = null);

public sealed record DedupRepresentativeResult(
    string CandidateId,
    string Title,
    string? PrimaryWorkId,
    IReadOnlyList<string> WorkIds,
    IReadOnlyList<string> SourceSightingIds,
    double CompletenessScore,
    IReadOnlyList<string> ReasonCodes);

public sealed record DedupReviewCandidate(
    string CandidateAId,
    string CandidateBId,
    double TitleSimilarity,
    double ThresholdUsed);

public sealed record DedupCluster(
    string ClusterId,
    IReadOnlyList<DedupCandidateRecord> Members,
    DedupRepresentativeResult Representative,
    IReadOnlyList<DedupEvidence> Evidence);

public sealed record DedupMessage(
    string Category,
    string Message);

public sealed record DeduplicationResult(
    string ResultId,
    string SchemaId,
    string SchemaVersion,
    string? PolicyId,
    string? PolicyVersion,
    double? FuzzyTitleThreshold,
    IReadOnlyDictionary<string, int> ProviderPriority,
    IReadOnlyList<string> SourceSearchTraceIds,
    IReadOnlyList<string> SourceImportTraceIds,
    IReadOnlyList<DedupCandidateRecord> RawCandidates,
    IReadOnlyList<DedupCluster> Clusters,
    IReadOnlyList<DedupEvidence> Evidence,
    IReadOnlyList<DedupCandidateRecord> UnresolvedCandidates,
    IReadOnlyList<DedupReviewCandidate> ReviewRequiredCandidates,
    IReadOnlyList<DedupMessage> Warnings,
    IReadOnlyList<DedupMessage> Errors,
    IReadOnlyList<string> NonClaims);

public enum DedupEvidenceKind
{
    SourceSighting,
    ExactIdentifier,
    FuzzyTitle,
    NoIdCandidate,
    SourceSpecificIdentifier
}
