using System.Collections.ObjectModel;
using System.Linq;

namespace NexusScholar.Deduplication;

public sealed record DedupSightingRef(
    string SourceKind,
    string SourceTraceId,
    string SourceSightingId,
    string? ProviderAlias = null,
    string? SourceDatabaseOrTool = null,
    string? SourceRecordId = null,
    string? SourceFileDigest = null,
    string? SourceFileDigestScope = null,
    string? RawRecordDigest = null,
    string? SourceContext = null,
    IReadOnlyList<DedupParserNotice>? ParserWarnings = null,
    IReadOnlyList<DedupParserNotice>? RecordNotices = null)
{
    public IReadOnlyList<DedupParserNotice> ParserWarnings { get; init; } =
        Freeze(ParserWarnings);

    public IReadOnlyList<DedupParserNotice> RecordNotices { get; init; } =
        Freeze(RecordNotices);

    private static IReadOnlyList<DedupParserNotice> Freeze(IReadOnlyList<DedupParserNotice>? notices)
    {
        return new ReadOnlyCollection<DedupParserNotice>((notices ?? Array.Empty<DedupParserNotice>()).ToArray());
    }
}

public sealed record DedupParserNotice(
    string Category,
    string Message,
    int? RecordIndex = null,
    string? SourceRecordId = null);

public sealed record DedupCandidateRecord(
    string CandidateId,
    string Title,
    bool HasStableIdentifier,
    string? PrimaryWorkId,
    IReadOnlyList<string> WorkIds,
    IReadOnlyList<string> SourceSpecificIds,
    DedupSightingRef Source,
    IReadOnlyList<string>? Authors = null,
    int? Year = null,
    string? Venue = null,
    string? Abstract = null,
    IReadOnlyList<string>? Keywords = null)
{
    public IReadOnlyList<string> Authors { get; init; } = Freeze(Authors);
    public IReadOnlyList<string> Keywords { get; init; } = Freeze(Keywords);

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string>? values) =>
        new ReadOnlyCollection<string>((values ?? Array.Empty<string>()).ToArray());
}

public sealed record DedupEvidence(
    string EvidenceId,
    DedupEvidenceKind Kind,
    string SubjectCandidateId,
    string? ObjectCandidateId,
    string? Reason,
    bool ReviewRequired = false,
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
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string>? SourceFileDigests = null,
    IReadOnlyList<string>? SourceFileDigestScopes = null,
    IReadOnlyList<string>? RawRecordDigests = null,
    IReadOnlyList<DedupParserNotice>? ParserWarnings = null,
    IReadOnlyList<DedupParserNotice>? RecordNotices = null,
    IReadOnlyList<string>? Authors = null,
    int? Year = null,
    string? Venue = null,
    string? Abstract = null,
    IReadOnlyList<string>? Keywords = null)
{
    public IReadOnlyList<string> SourceFileDigests { get; init; } =
        Freeze(SourceFileDigests);

    public IReadOnlyList<string> SourceFileDigestScopes { get; init; } =
        Freeze(SourceFileDigestScopes);

    public IReadOnlyList<string> RawRecordDigests { get; init; } =
        Freeze(RawRecordDigests);

    public IReadOnlyList<DedupParserNotice> ParserWarnings { get; init; } =
        Freeze(ParserWarnings);

    public IReadOnlyList<DedupParserNotice> RecordNotices { get; init; } =
        Freeze(RecordNotices);

    public IReadOnlyList<string> Authors { get; init; } = Freeze(Authors);

    public IReadOnlyList<string> Keywords { get; init; } = Freeze(Keywords);

    private static IReadOnlyList<string> Freeze(IReadOnlyList<string>? values)
    {
        return new ReadOnlyCollection<string>((values ?? Array.Empty<string>()).ToArray());
    }

    private static IReadOnlyList<DedupParserNotice> Freeze(IReadOnlyList<DedupParserNotice>? notices)
    {
        return new ReadOnlyCollection<DedupParserNotice>((notices ?? Array.Empty<DedupParserNotice>()).ToArray());
    }
}

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
