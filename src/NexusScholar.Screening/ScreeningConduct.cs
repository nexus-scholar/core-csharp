using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Screening;

public static class ScreeningConductActorKinds
{
    public const string Human = "human";
    public const string Automation = "automation";
}

public enum ScreeningConductDecisionKind
{
    Review,
    Correction,
    Adjudication
}

public interface IScreeningConductEntry
{
    int Ordinal { get; }
    ContentDigest PreviousDigest { get; }
    ContentDigest Digest { get; }
}

public sealed record ScreeningConductActor(string ActorId, string Kind, string Role)
{
    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("actor_id", Guard.NotBlank(ActorId, nameof(ActorId)))
        .Add("kind", NormalizeKind(Kind))
        .Add("role", Guard.NotBlank(Role, nameof(Role)));

    internal static string NormalizeKind(string value)
    {
        var normalized = Guard.NotBlank(value, nameof(value)).ToLowerInvariant();
        return normalized is ScreeningConductActorKinds.Human or ScreeningConductActorKinds.Automation
            ? normalized
            : throw Rule(ScreeningErrorCodes.UnauthorizedReviewer, "Unknown Screening actor kind.");
    }

    private static ScreeningRuleException Rule(string category, string message) => new(category, message);
}

public sealed record ScreeningConductRoleAssignment(string ActorId, string Role)
{
    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("actor_id", Guard.NotBlank(ActorId, nameof(ActorId)))
        .Add("role", Guard.NotBlank(Role, nameof(Role)));
}

public sealed record ScreeningExclusionReason(string Code, string Stage)
{
    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("code", Guard.NotBlank(Code, nameof(Code)))
        .Add("stage", Guard.NotBlank(Stage, nameof(Stage)));
}

public sealed class ScreeningConductPolicy
{
    public const string SchemaId = "nexus.screening.conduct-policy";
    public const string SchemaVersion = "1.0.0";

    private ScreeningConductPolicy(
        string policyId,
        ScreeningCandidateSet candidateSet,
        ScreeningCriteria criteria,
        VerifiedProtocolVersion protocol,
        int requiredReviewCount,
        IReadOnlyList<ScreeningConductRoleAssignment> assignments,
        IReadOnlyList<string> adjudicatorRoles,
        IReadOnlyList<ScreeningExclusionReason> exclusionReasons,
        ScreeningConductActor approvedBy,
        DateTimeOffset approvedAt)
    {
        PolicyId = policyId;
        CandidateSet = candidateSet;
        CandidateSetDigest = ComputeCandidateSetDigest(candidateSet);
        Criteria = criteria;
        CriteriaDigest = criteria.ComputeDigest();
        ProtocolVersionId = protocol.Version.Id;
        ProtocolContentDigest = protocol.Version.ContentDigest;
        RequiredReviewCount = requiredReviewCount;
        Assignments = assignments;
        AdjudicatorRoles = adjudicatorRoles;
        ExclusionReasons = exclusionReasons;
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        Digest = Envelope().ComputeDigest();
    }

    public string PolicyId { get; }
    public ScreeningCandidateSet CandidateSet { get; }
    public ContentDigest CandidateSetDigest { get; }
    public ScreeningCriteria Criteria { get; }
    public ContentDigest CriteriaDigest { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public int RequiredReviewCount { get; }
    public IReadOnlyList<ScreeningConductRoleAssignment> Assignments { get; }
    public IReadOnlyList<string> AdjudicatorRoles { get; }
    public IReadOnlyList<ScreeningExclusionReason> ExclusionReasons { get; }
    public ScreeningConductActor ApprovedBy { get; }
    public DateTimeOffset ApprovedAt { get; }
    public ContentDigest Digest { get; }

    public static ScreeningConductPolicy Create(
        string policyId,
        string candidateSetId,
        VerifiedDeduplicationResult deduplication,
        VerifiedProtocolVersion protocol,
        ScreeningCriteria criteria,
        int requiredReviewCount,
        IEnumerable<ScreeningConductRoleAssignment> assignments,
        IEnumerable<string>? adjudicatorRoles,
        IEnumerable<ScreeningExclusionReason>? exclusionReasons,
        ScreeningConductActor approvedBy,
        DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(deduplication);
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(approvedBy);
        if (requiredReviewCount is < 1 or > 2)
            throw Rule(ScreeningErrorCodes.InsufficientReview, "Title/abstract conduct requires one or two independent reviews.");
        if (!string.Equals(criteria.Stage, ScreeningStages.TitleAbstract, StringComparison.Ordinal) ||
            !criteria.RequiresProtocolBinding ||
            !string.Equals(criteria.ApprovedProtocolBinding, protocol.Version.Id, StringComparison.Ordinal) ||
            !string.Equals(criteria.ApprovedProtocolDigest, protocol.Version.ContentDigest.ToString(), StringComparison.Ordinal) ||
            !string.Equals(criteria.CurrentProtocolContentDigest, protocol.Version.ContentDigest.ToString(), StringComparison.Ordinal) ||
            !string.Equals(criteria.ApprovedProtocolDigestScope, DigestScope.ProtocolContent.ToString(), StringComparison.Ordinal) ||
            !ScreeningProtocolBindingStatus.IsApproved(criteria.ApprovedProtocolStatus))
            throw Rule(ScreeningErrorCodes.InvalidProtocolBinding, "Conduct criteria do not bind the verified approved Protocol.");
        if (ScreeningConductActor.NormalizeKind(approvedBy.Kind) != ScreeningConductActorKinds.Human)
            throw Rule(ScreeningErrorCodes.AutomationCannotFinalize, "Conduct policy approval requires a human actor.");

        var normalizedAssignments = assignments
            .Select(item => new ScreeningConductRoleAssignment(Guard.NotBlank(item.ActorId, nameof(item.ActorId)), Guard.NotBlank(item.Role, nameof(item.Role))))
            .OrderBy(item => item.ActorId, StringComparer.Ordinal).ThenBy(item => item.Role, StringComparer.Ordinal).ToArray();
        if (normalizedAssignments.Length < requiredReviewCount || normalizedAssignments.Distinct().Count() != normalizedAssignments.Length)
            throw Rule(ScreeningErrorCodes.UnauthorizedReviewer, "Conduct assignments must be unique and satisfy the review count.");
        var roles = (adjudicatorRoles ?? Array.Empty<string>()).Select(role => Guard.NotBlank(role, nameof(adjudicatorRoles)))
            .Distinct(StringComparer.Ordinal).OrderBy(role => role, StringComparer.Ordinal).ToArray();
        var reasons = (exclusionReasons ?? Array.Empty<ScreeningExclusionReason>())
            .OrderBy(item => item.Code, StringComparer.Ordinal).ToArray();
        if (reasons.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count() != reasons.Length ||
            reasons.Any(item => !string.Equals(item.Stage, ScreeningStages.TitleAbstract, StringComparison.Ordinal)))
            throw Rule(ScreeningErrorCodes.InvalidExclusionReason, "Exclusion reason codes must be unique and title/abstract scoped.");

        var candidateSet = ScreeningCandidateSet.CreateFromDedupResult(
            Guard.NotBlank(candidateSetId, nameof(candidateSetId)), deduplication.Result, true,
            ScreeningSourceKinds.DeduplicationResult, [deduplication.Result.ResultId], null, deduplication.Result.NonClaims);
        return new ScreeningConductPolicy(
            Guard.NotBlank(policyId, nameof(policyId)), candidateSet, criteria, protocol, requiredReviewCount,
            Array.AsReadOnly(normalizedAssignments), Array.AsReadOnly(roles), Array.AsReadOnly(reasons), approvedBy, approvedAt);
    }

    public bool Authorizes(ScreeningConductActor actor) =>
        ScreeningConductActor.NormalizeKind(actor.Kind) == ScreeningConductActorKinds.Human &&
        Assignments.Any(item => item.ActorId == actor.ActorId && item.Role == actor.Role);

    public bool AuthorizesAdjudication(ScreeningConductActor actor) =>
        Authorizes(actor) && AdjudicatorRoles.Contains(actor.Role, StringComparer.Ordinal);

    public bool AllowsReason(string code) => ExclusionReasons.Any(item => item.Code == code && item.Stage == ScreeningStages.TitleAbstract);

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private DigestEnvelope Envelope() => new(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion,
        new CanonicalJsonObject()
            .Add("policy_id", PolicyId)
            .Add("candidate_set_id", CandidateSet.CandidateSetId)
            .Add("candidate_set_digest", CandidateSetDigest.ToString())
            .Add("criteria_id", Criteria.CriteriaId)
            .Add("criteria_digest", CriteriaDigest.ToString())
            .Add("protocol_version_id", ProtocolVersionId)
            .Add("protocol_content_digest", ProtocolContentDigest.ToString())
            .Add("required_review_count", RequiredReviewCount)
            .Add("assignments", CanonicalJsonValue.Array(Assignments.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("adjudicator_roles", CanonicalJsonValue.Array(AdjudicatorRoles.Select(CanonicalJsonValue.From).ToArray()))
            .Add("exclusion_reasons", CanonicalJsonValue.Array(ExclusionReasons.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("approved_by", ApprovedBy.ToCanonicalJson())
            .AddTimestamp("approved_at", ApprovedAt));

    private static ContentDigest ComputeCandidateSetDigest(ScreeningCandidateSet candidateSet) =>
        new DigestEnvelope(DigestScope.CanonicalJsonRecord, ScreeningSchema.CandidateSetSchemaId, ScreeningSchema.CandidateSetSchemaVersion,
            new CanonicalJsonObject()
                .Add("candidate_set_id", candidateSet.CandidateSetId)
                .Add("source_kind", candidateSet.SourceKind)
                .Add("source_refs", CanonicalJsonValue.Array(candidateSet.SourceRefs.OrderBy(value => value, StringComparer.Ordinal).Select(CanonicalJsonValue.From).ToArray()))
                .Add("locked", candidateSet.Locked)
                .Add("created_from_dedup_result_id", candidateSet.CreatedFromDedupResultId!)
                .Add("candidate_ids", CanonicalJsonValue.Array(candidateSet.Candidates.Select(item => item.CandidateId).OrderBy(value => value, StringComparer.Ordinal).Select(CanonicalJsonValue.From).ToArray()))
                .Add("unresolved_candidate_ids", CanonicalJsonValue.Array(candidateSet.UnresolvedCandidates.Select(item => item.CandidateId).OrderBy(value => value, StringComparer.Ordinal).Select(CanonicalJsonValue.From).ToArray())))
        .ComputeDigest();

    private static ScreeningRuleException Rule(string category, string message) => new(category, message);
}

public sealed class ScreeningConductHeader
{
    public const string SchemaId = "nexus.screening.conduct-header";
    public const string SchemaVersion = "1.0.0";

    private ScreeningConductHeader(string conductId, ScreeningConductPolicy policy, ScreeningConductActor createdBy, DateTimeOffset createdAt)
    {
        ConductId = conductId;
        PolicyId = policy.PolicyId;
        PolicyDigest = policy.Digest;
        CandidateSetId = policy.CandidateSet.CandidateSetId;
        CandidateSetDigest = policy.CandidateSetDigest;
        CriteriaId = policy.Criteria.CriteriaId;
        CriteriaDigest = policy.CriteriaDigest;
        ProtocolVersionId = policy.ProtocolVersionId;
        ProtocolContentDigest = policy.ProtocolContentDigest;
        CandidateIds = Array.AsReadOnly(policy.CandidateSet.Candidates.Select(item => item.CandidateId).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Digest = Envelope().ComputeDigest();
    }

    public string ConductId { get; }
    public string PolicyId { get; }
    public ContentDigest PolicyDigest { get; }
    public string CandidateSetId { get; }
    public ContentDigest CandidateSetDigest { get; }
    public string CriteriaId { get; }
    public ContentDigest CriteriaDigest { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public IReadOnlyList<string> CandidateIds { get; }
    public ScreeningConductActor CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    public ContentDigest Digest { get; }

    public static ScreeningConductHeader Create(string conductId, ScreeningConductPolicy policy, ScreeningConductActor createdBy, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(createdBy);
        if (!policy.Authorizes(createdBy))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Conduct creation requires an authorized human reviewer.");
        return new ScreeningConductHeader(Guard.NotBlank(conductId, nameof(conductId)), policy, createdBy, createdAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private DigestEnvelope Envelope() => new(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion,
        new CanonicalJsonObject().Add("conduct_id", ConductId).Add("policy_id", PolicyId).Add("policy_digest", PolicyDigest.ToString())
            .Add("candidate_set_id", CandidateSetId).Add("candidate_set_digest", CandidateSetDigest.ToString())
            .Add("criteria_id", CriteriaId).Add("criteria_digest", CriteriaDigest.ToString())
            .Add("protocol_version_id", ProtocolVersionId).Add("protocol_content_digest", ProtocolContentDigest.ToString())
            .Add("candidate_ids", CanonicalJsonValue.Array(CandidateIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add("created_by", CreatedBy.ToCanonicalJson()).AddTimestamp("created_at", CreatedAt));
}

public sealed record ScreeningConductEvidenceRef(string Kind, string Id, ContentDigest Digest)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        if (!Digest.IsValid)
            throw new ScreeningRuleException(ScreeningErrorCodes.UnverifiedConductAuthority, "Screening evidence digest must be valid.");
        return new CanonicalJsonObject()
            .Add("kind", Guard.NotBlank(Kind, nameof(Kind))).Add("id", Guard.NotBlank(Id, nameof(Id))).Add("digest", Digest.ToString());
    }
}

public sealed class ScreeningConductDecision : IScreeningConductEntry
{
    public const string SchemaId = "nexus.screening.conduct-decision";
    public const string SchemaVersion = "1.0.0";

    private ScreeningConductDecision(ScreeningConductHeader header, int ordinal, ContentDigest previousDigest, string requestId,
        string candidateId, ScreeningConductDecisionKind kind, string verdict, ScreeningConductActor actor, string rationale,
        string? exclusionReasonCode, ContentDigest? supersedesDecisionDigest, string? resolvedConflictId,
        IReadOnlyList<ContentDigest> sourceDecisionDigests, IReadOnlyList<ScreeningConductEvidenceRef> evidence, DateTimeOffset decidedAt)
    {
        ConductId = header.ConductId; PolicyId = header.PolicyId; PolicyDigest = header.PolicyDigest;
        CandidateSetId = header.CandidateSetId; CandidateSetDigest = header.CandidateSetDigest;
        CriteriaId = header.CriteriaId; CriteriaDigest = header.CriteriaDigest; ProtocolVersionId = header.ProtocolVersionId;
        ProtocolContentDigest = header.ProtocolContentDigest; Ordinal = ordinal; PreviousDigest = previousDigest;
        RequestId = requestId; CandidateId = candidateId; Kind = kind; Verdict = verdict; Actor = actor; Rationale = rationale;
        ExclusionReasonCode = exclusionReasonCode; SupersedesDecisionDigest = supersedesDecisionDigest; ResolvedConflictId = resolvedConflictId;
        SourceDecisionDigests = sourceDecisionDigests; Evidence = evidence; DecidedAt = decidedAt;
        RequestDigest = new DigestEnvelope(DigestScope.CanonicalJsonRecord, "nexus.screening.conduct-request", "1.0.0", RequestContent()).ComputeDigest();
        DecisionId = $"screening-decision-{RequestDigest.Value[7..23]}";
        Digest = Envelope().ComputeDigest();
    }

    public string ConductId { get; }
    public string PolicyId { get; }
    public ContentDigest PolicyDigest { get; }
    public string CandidateSetId { get; }
    public ContentDigest CandidateSetDigest { get; }
    public string CriteriaId { get; }
    public ContentDigest CriteriaDigest { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public int Ordinal { get; }
    public ContentDigest PreviousDigest { get; }
    public string RequestId { get; }
    public ContentDigest RequestDigest { get; }
    public string DecisionId { get; }
    public string CandidateId { get; }
    public ScreeningConductDecisionKind Kind { get; }
    public string Verdict { get; }
    public ScreeningConductActor Actor { get; }
    public string Rationale { get; }
    public string? ExclusionReasonCode { get; }
    public ContentDigest? SupersedesDecisionDigest { get; }
    public string? ResolvedConflictId { get; }
    public IReadOnlyList<ContentDigest> SourceDecisionDigests { get; }
    public IReadOnlyList<ScreeningConductEvidenceRef> Evidence { get; }
    public DateTimeOffset DecidedAt { get; }
    public ContentDigest Digest { get; }

    public static ScreeningConductDecision Create(ScreeningConductHeader header, int ordinal, ContentDigest previousDigest,
        string requestId, string candidateId, ScreeningConductDecisionKind kind, string verdict, ScreeningConductActor actor,
        string rationale, DateTimeOffset decidedAt, string? exclusionReasonCode = null, string? supersedesDecisionDigest = null,
        string? resolvedConflictId = null, IEnumerable<ContentDigest>? sourceDecisionDigests = null,
        IEnumerable<ScreeningConductEvidenceRef>? evidence = null)
    {
        ArgumentNullException.ThrowIfNull(header); ArgumentNullException.ThrowIfNull(actor);
        if (ordinal < 1 || !previousDigest.IsValid) throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Decision chain position is invalid.");
        if (!header.CandidateIds.Contains(candidateId, StringComparer.Ordinal)) throw new ScreeningRuleException(ScreeningErrorCodes.CandidateNotInSet, "Candidate is not in the conduct set.");
        if (!ScreeningVerdicts.IsKnown(verdict)) throw new ScreeningRuleException(ScreeningErrorCodes.UnknownScreeningVerdict, "Unknown Screening verdict.");
        if (kind == ScreeningConductDecisionKind.Correction && supersedesDecisionDigest is null)
            throw Rule(ScreeningErrorCodes.MissingSourceDecision, "Correction must supersede one current decision digest.");
        if (kind != ScreeningConductDecisionKind.Correction && supersedesDecisionDigest is not null)
            throw Rule(ScreeningErrorCodes.MissingSourceDecision, "Only a correction may supersede a prior decision.");
        if (kind == ScreeningConductDecisionKind.Adjudication &&
            (sourceDecisionDigests is null || string.IsNullOrWhiteSpace(resolvedConflictId)))
            throw Rule(ScreeningErrorCodes.MissingSourceDecision, "Adjudication must identify all source decision digests.");
        var sources = (sourceDecisionDigests ?? Array.Empty<ContentDigest>())
            .Select(digest => digest.IsValid ? digest : throw Rule(ScreeningErrorCodes.UnverifiedConductAuthority, "Screening conduct source decision digest is invalid."))
            .Distinct()
            .OrderBy(digest => digest.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (kind == ScreeningConductDecisionKind.Adjudication && sources.Length == 0)
            throw Rule(ScreeningErrorCodes.MissingSourceDecision, "Adjudication must identify all source decision digests.");
        if (kind != ScreeningConductDecisionKind.Adjudication && (sources.Length != 0 || resolvedConflictId is not null))
            throw Rule(ScreeningErrorCodes.MissingSourceDecision, "Only adjudication may resolve a conflict or identify conflict source decisions.");
        var supersedes = ParseOptionalDigest(supersedesDecisionDigest);
        var refs = (evidence ?? Array.Empty<ScreeningConductEvidenceRef>()).OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        return new ScreeningConductDecision(header, ordinal, previousDigest, Guard.NotBlank(requestId, nameof(requestId)),
            Guard.NotBlank(candidateId, nameof(candidateId)), kind, verdict, actor, Guard.NotBlank(rationale, nameof(rationale)),
            exclusionReasonCode, supersedes, resolvedConflictId, Array.AsReadOnly(sources), Array.AsReadOnly(refs), decidedAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();
    private DigestEnvelope Envelope() => new(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion,
        RequestContent().Add("decision_id", DecisionId).Add("ordinal", Ordinal).Add("previous_digest", PreviousDigest.ToString())
            .Add("request_digest", RequestDigest.ToString()).AddTimestamp("decided_at", DecidedAt));
    private CanonicalJsonObject RequestContent()
    {
        var value = new CanonicalJsonObject().Add("conduct_id", ConductId).Add("policy_id", PolicyId).Add("policy_digest", PolicyDigest.ToString())
            .Add("candidate_set_id", CandidateSetId).Add("candidate_set_digest", CandidateSetDigest.ToString())
            .Add("criteria_id", CriteriaId).Add("criteria_digest", CriteriaDigest.ToString()).Add("protocol_version_id", ProtocolVersionId)
            .Add("protocol_content_digest", ProtocolContentDigest.ToString()).Add("request_id", RequestId).Add("candidate_id", CandidateId)
            .Add("kind", Kind.ToString().ToLowerInvariant()).Add("verdict", Verdict).Add("actor", Actor.ToCanonicalJson()).Add("rationale", Rationale)
            .Add("source_decision_digests", CanonicalJsonValue.Array(SourceDecisionDigests.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()))
            .Add("evidence", CanonicalJsonValue.Array(Evidence.Select(item => item.ToCanonicalJson()).ToArray()));
        if (ExclusionReasonCode is not null) value.Add("exclusion_reason_code", ExclusionReasonCode);
        if (SupersedesDecisionDigest is not null) value.Add("supersedes_decision_digest", SupersedesDecisionDigest.Value.ToString());
        if (ResolvedConflictId is not null) value.Add("resolved_conflict_id", ResolvedConflictId);
        return value;
    }

    private static ContentDigest? ParseOptionalDigest(string? value)
    {
        if (value is null) return null;
        if (!ContentDigest.TryParse(value, out var digest) || !digest.IsValid)
            throw Rule(ScreeningErrorCodes.UnverifiedConductAuthority, "Screening conduct decision digest is invalid.");
        return digest;
    }

    private static ScreeningRuleException Rule(string category, string message) => new(category, message);
}

public sealed record ScreeningConductConflict(string ConflictId, string CandidateId, IReadOnlyList<ContentDigest> SourceDecisionDigests, bool Resolved);
public sealed record ScreeningConductOutcome(
    string CandidateId,
    string Verdict,
    IReadOnlyList<ContentDigest> SupportingDecisionDigests,
    string? ExclusionReasonCode);
public sealed record ScreeningConductProjection(ContentDigest HeadDigest, IReadOnlyDictionary<string, ScreeningConductOutcome> Outcomes,
    IReadOnlyList<ScreeningConductConflict> Conflicts, IReadOnlySet<ContentDigest> InvalidatedDecisionDigests, bool HandoffReady);

public sealed class ScreeningConductInvalidation : IScreeningConductEntry
{
    public const string SchemaId = "nexus.screening.conduct-invalidation";
    public const string SchemaVersion = "1.0.0";

    private ScreeningConductInvalidation(
        ScreeningConductHeader header,
        int ordinal,
        ContentDigest previousDigest,
        string invalidationId,
        ScreeningConductEvidenceRef source,
        IReadOnlyList<ContentDigest> affectedDecisionDigests,
        ScreeningConductActor actor,
        string reason,
        DateTimeOffset invalidatedAt)
    {
        ConductId = header.ConductId;
        PolicyId = header.PolicyId;
        PolicyDigest = header.PolicyDigest;
        Ordinal = ordinal;
        PreviousDigest = previousDigest;
        InvalidationId = Guard.NotBlank(invalidationId, nameof(invalidationId));
        Source = source;
        AffectedDecisionDigests = affectedDecisionDigests;
        Actor = actor;
        Reason = Guard.NotBlank(reason, nameof(reason));
        InvalidatedAt = invalidatedAt;
        Digest = Envelope().ComputeDigest();
    }

    public string ConductId { get; }
    public string PolicyId { get; }
    public ContentDigest PolicyDigest { get; }
    public int Ordinal { get; }
    public ContentDigest PreviousDigest { get; }
    public string InvalidationId { get; }
    public ScreeningConductEvidenceRef Source { get; }
    public IReadOnlyList<ContentDigest> AffectedDecisionDigests { get; }
    public ScreeningConductActor Actor { get; }
    public string Reason { get; }
    public DateTimeOffset InvalidatedAt { get; }
    public ContentDigest Digest { get; }

    public static ScreeningConductInvalidation Create(
        ScreeningConductHeader header,
        int ordinal,
        ContentDigest previousDigest,
        string invalidationId,
        ScreeningConductEvidenceRef source,
        IEnumerable<ContentDigest> affectedDecisionDigests,
        ScreeningConductActor actor,
        string reason,
        DateTimeOffset invalidatedAt)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(affectedDecisionDigests);
        ArgumentNullException.ThrowIfNull(actor);
        if (ordinal < 1 || !previousDigest.IsValid) throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Invalidation chain position is invalid.");
        _ = source.ToCanonicalJson();
        var affected = affectedDecisionDigests.Select(digest => digest.IsValid
            ? digest
            : throw Rule(ScreeningErrorCodes.UnverifiedConductAuthority, "Screening conduct affected decision digest is invalid."))
            .Distinct()
            .OrderBy(digest => digest.ToString(), StringComparer.Ordinal).ToArray();
        if (affected.Length == 0) throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision, "Invalidation must identify affected decisions.");
        return new ScreeningConductInvalidation(header, ordinal, previousDigest, invalidationId, source,
            Array.AsReadOnly(affected), actor, reason, invalidatedAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private DigestEnvelope Envelope() => new(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion,
        new CanonicalJsonObject().Add("conduct_id", ConductId).Add("policy_id", PolicyId).Add("policy_digest", PolicyDigest.ToString())
            .Add("ordinal", Ordinal).Add("previous_digest", PreviousDigest.ToString()).Add("invalidation_id", InvalidationId)
            .Add("source", Source.ToCanonicalJson())
            .Add("affected_decision_digests", CanonicalJsonValue.Array(AffectedDecisionDigests.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()))
            .Add("actor", Actor.ToCanonicalJson()).Add("reason", Reason).AddTimestamp("invalidated_at", InvalidatedAt));

    private static ScreeningRuleException Rule(string category, string message) => new(category, message);
}

public sealed class ScreeningConductHandoff
{
    public const string SchemaId = "nexus.screening.conduct-handoff";
    public const string SchemaVersion = "1.0.0";

    private ScreeningConductHandoff(string handoffId, ScreeningConductHeader header, ScreeningConductProjection projection, DateTimeOffset createdAt)
    {
        HandoffId = Guard.NotBlank(handoffId, nameof(handoffId));
        ConductId = header.ConductId;
        PolicyDigest = header.PolicyDigest;
        JournalHeadDigest = projection.HeadDigest;
        Outcomes = Array.AsReadOnly(projection.Outcomes.Values.OrderBy(item => item.CandidateId, StringComparer.Ordinal).ToArray());
        CreatedAt = createdAt;
        Digest = Envelope().ComputeDigest();
    }

    public string HandoffId { get; }
    public string ConductId { get; }
    public ContentDigest PolicyDigest { get; }
    public ContentDigest JournalHeadDigest { get; }
    public IReadOnlyList<ScreeningConductOutcome> Outcomes { get; }
    public DateTimeOffset CreatedAt { get; }
    public ContentDigest Digest { get; }

    public static ScreeningConductHandoff Create(string handoffId, ScreeningConductJournal journal, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (!journal.Projection.HandoffReady)
            throw new ScreeningRuleException(ScreeningErrorCodes.InsufficientReview, "Current Screening conduct is not eligible for handoff.");
        return new ScreeningConductHandoff(handoffId, journal.Header, journal.Projection, createdAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private DigestEnvelope Envelope() => new(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion,
        new CanonicalJsonObject().Add("handoff_id", HandoffId).Add("conduct_id", ConductId).Add("policy_digest", PolicyDigest.ToString())
            .Add("journal_head_digest", JournalHeadDigest.ToString())
            .Add("outcomes", CanonicalJsonValue.Array(Outcomes.Select(item =>
            {
                var value = new CanonicalJsonObject().Add("candidate_id", item.CandidateId).Add("verdict", item.Verdict)
                    .Add("supporting_decision_digests", CanonicalJsonValue.Array(item.SupportingDecisionDigests
                        .Select(digest => CanonicalJsonValue.From(digest.ToString())).ToArray()));
                if (item.ExclusionReasonCode is not null) value.Add("exclusion_reason_code", item.ExclusionReasonCode);
                return value;
            }).ToArray())).AddTimestamp("created_at", CreatedAt));
}

public sealed class ScreeningConductJournal
{
    private readonly List<IScreeningConductEntry> _entries = [];
    private readonly List<ScreeningConductDecision> _decisions = [];
    private readonly List<ScreeningConductInvalidation> _invalidations = [];
    private ScreeningConductJournal(ScreeningConductHeader header, ScreeningConductPolicy policy) { Header = header; Policy = policy; Projection = Replay(); }
    public ScreeningConductHeader Header { get; }
    public ScreeningConductPolicy Policy { get; }
    public IReadOnlyList<ScreeningConductDecision> Decisions => _decisions.AsReadOnly();
    public IReadOnlyList<ScreeningConductInvalidation> Invalidations => _invalidations.AsReadOnly();
    public IReadOnlyList<IScreeningConductEntry> Entries => _entries.AsReadOnly();
    public ScreeningConductProjection Projection { get; private set; }

    public static ScreeningConductJournal Create(ScreeningConductHeader header, ScreeningConductPolicy policy)
    {
        EnsureBinding(header, policy); return new ScreeningConductJournal(header, policy);
    }

    public static ScreeningConductJournal Rehydrate(ScreeningConductHeader header, ScreeningConductPolicy policy, IEnumerable<ScreeningConductDecision> decisions)
    {
        var journal = Create(header, policy); foreach (var decision in decisions ?? throw new ArgumentNullException(nameof(decisions))) journal.Append(decision); return journal;
    }

    public static ScreeningConductJournal RehydrateEntries(ScreeningConductHeader header, ScreeningConductPolicy policy, IEnumerable<IScreeningConductEntry> entries)
    {
        var journal = Create(header, policy);
        foreach (var entry in entries ?? throw new ArgumentNullException(nameof(entries)))
        {
            if (entry is ScreeningConductDecision decision) journal.Append(decision);
            else if (entry is ScreeningConductInvalidation invalidation) journal.Append(invalidation);
            else throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Unknown Screening conduct entry type.");
        }
        return journal;
    }

    public void Append(ScreeningConductDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var repeatedRequest = _decisions.FirstOrDefault(item => item.RequestId == decision.RequestId);
        if (repeatedRequest is not null)
        {
            if (repeatedRequest.RequestDigest == decision.RequestDigest) return;
            throw new ScreeningRuleException(ScreeningErrorCodes.DecisionNotAppendOnly, "Screening request id was reused with different content.");
        }
        if (decision.Ordinal != _entries.Count + 1 || decision.PreviousDigest != Projection.HeadDigest)
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Screening decision does not extend the current journal head.");
        if (decision.PolicyDigest != Policy.Digest || decision.ConductId != Header.ConductId || decision.CriteriaDigest != Policy.CriteriaDigest || decision.ProtocolContentDigest != Policy.ProtocolContentDigest)
            throw new ScreeningRuleException(ScreeningErrorCodes.UnverifiedConductAuthority, "Screening decision authority binding is stale or mismatched.");
        if (!Policy.Authorizes(decision.Actor)) throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Actor is not authorized by the conduct policy.");
        if (decision.Kind == ScreeningConductDecisionKind.Adjudication)
        {
            if (!Policy.AuthorizesAdjudication(decision.Actor))
                throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Actor is not authorized to adjudicate.");
            var conflict = Projection.Conflicts.SingleOrDefault(item =>
                item.CandidateId == decision.CandidateId && !item.Resolved && item.ConflictId == decision.ResolvedConflictId);
            if (conflict is null || !conflict.SourceDecisionDigests.SequenceEqual(decision.SourceDecisionDigests))
                throw new ScreeningRuleException(ScreeningErrorCodes.AdjudicationSourceMismatch, "Adjudication must bind the exact unresolved conflict and source decisions.");
        }
        if (decision.Verdict == ScreeningVerdicts.Exclude ? decision.ExclusionReasonCode is null || !Policy.AllowsReason(decision.ExclusionReasonCode) : decision.ExclusionReasonCode is not null)
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidExclusionReason, "Exclusion reason is missing, unknown, or not valid for this verdict.");
        var currentDecisionDigests = CurrentDecisionDigests();
        if (decision.Kind == ScreeningConductDecisionKind.Review && _decisions.Any(item =>
            currentDecisionDigests.Contains(item.Digest) && item.CandidateId == decision.CandidateId &&
            item.Kind is ScreeningConductDecisionKind.Review or ScreeningConductDecisionKind.Correction &&
            item.Actor.ActorId == decision.Actor.ActorId))
            throw new ScreeningRuleException(ScreeningErrorCodes.DuplicateIndependentReviewer, "One actor cannot satisfy two independent review slots.");
        if (decision.Kind == ScreeningConductDecisionKind.Correction &&
            (decision.SupersedesDecisionDigest is null || !currentDecisionDigests.Contains(decision.SupersedesDecisionDigest.Value) ||
            !_decisions.Any(item => item.Digest == decision.SupersedesDecisionDigest && item.CandidateId == decision.CandidateId &&
                item.Kind is ScreeningConductDecisionKind.Review or ScreeningConductDecisionKind.Correction &&
                item.Actor.ActorId == decision.Actor.ActorId)))
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision, "Correction must supersede the actor's current decision for this candidate.");
        _decisions.Add(decision);
        _entries.Add(decision);
        Projection = Replay();
    }

    public void Append(ScreeningConductInvalidation invalidation)
    {
        ArgumentNullException.ThrowIfNull(invalidation);
        if (invalidation.Ordinal != _entries.Count + 1 || invalidation.PreviousDigest != Projection.HeadDigest)
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Screening invalidation does not extend the current journal head.");
        if (invalidation.ConductId != Header.ConductId || invalidation.PolicyId != Policy.PolicyId || invalidation.PolicyDigest != Policy.Digest)
            throw new ScreeningRuleException(ScreeningErrorCodes.UnverifiedConductAuthority, "Screening invalidation authority binding is stale or mismatched.");
        if (!Policy.Authorizes(invalidation.Actor))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Invalidation requires an authorized human actor.");
        var affectedBySource = _decisions.Where(item => CurrentDecisionDigests().Contains(item.Digest) && DependsOn(item, invalidation.Source))
            .Select(item => item.Digest).ToHashSet();
        if (affectedBySource.Count == 0 || !affectedBySource.SetEquals(invalidation.AffectedDecisionDigests))
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision, "Invalidation must identify the complete current decision set affected by its exact source.");
        _invalidations.Add(invalidation);
        _entries.Add(invalidation);
        Projection = Replay();
    }

    private ScreeningConductProjection Replay()
    {
        var superseded = _decisions.Where(item => item.Kind == ScreeningConductDecisionKind.Correction && item.SupersedesDecisionDigest is not null)
            .Select(item => item.SupersedesDecisionDigest!.Value).ToHashSet();
        var invalidated = _invalidations.SelectMany(item => item.AffectedDecisionDigests).ToHashSet();
        var outcomes = new Dictionary<string, ScreeningConductOutcome>(StringComparer.Ordinal);
        var conflicts = new List<ScreeningConductConflict>();
        foreach (var candidateId in Header.CandidateIds)
        {
            var current = _decisions.Where(item => item.CandidateId == candidateId && !superseded.Contains(item.Digest) && !invalidated.Contains(item.Digest)).ToArray();
            var adjudication = current.LastOrDefault(item => item.Kind == ScreeningConductDecisionKind.Adjudication);
            var reviews = current.Where(item => item.Kind != ScreeningConductDecisionKind.Adjudication).ToArray();
            if (reviews.Select(item => item.Verdict).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                var ids = reviews.Select(item => item.Digest).OrderBy(id => id.ToString(), StringComparer.Ordinal).ToArray();
                var conflictId = $"screening-conflict-{ContentDigest.Sha256Utf8(string.Join("|", ids)).Value[7..23]}";
                var resolved = adjudication is not null && adjudication.ResolvedConflictId == conflictId && ids.SequenceEqual(adjudication.SourceDecisionDigests);
                conflicts.Add(new ScreeningConductConflict(conflictId, candidateId, Array.AsReadOnly(ids), resolved));
                if (resolved)
                {
                    var supporting = ids.Append(adjudication!.Digest).OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
                    outcomes[candidateId] = new ScreeningConductOutcome(candidateId, adjudication.Verdict, Array.AsReadOnly(supporting), adjudication.ExclusionReasonCode);
                }
            }
            else if (reviews.Length >= Policy.RequiredReviewCount)
            {
                var latest = reviews[^1];
                var supporting = reviews.Select(item => item.Digest).OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
                outcomes[candidateId] = new ScreeningConductOutcome(candidateId, latest.Verdict, Array.AsReadOnly(supporting), latest.ExclusionReasonCode);
            }
        }
        var ready = Header.CandidateIds.All(outcomes.ContainsKey) && conflicts.All(item => item.Resolved) && outcomes.Values.All(item => item.Verdict != ScreeningVerdicts.NeedsReview);
        return new ScreeningConductProjection(_entries.Count == 0 ? Header.Digest : _entries[^1].Digest,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, ScreeningConductOutcome>(outcomes), conflicts.AsReadOnly(), invalidated, ready);
    }

    private HashSet<ContentDigest> CurrentDecisionDigests()
    {
        var superseded = _decisions.Where(item => item.Kind == ScreeningConductDecisionKind.Correction && item.SupersedesDecisionDigest is not null)
            .Select(item => item.SupersedesDecisionDigest!.Value).ToHashSet();
        var invalidated = _invalidations.SelectMany(item => item.AffectedDecisionDigests).ToHashSet();
        return _decisions.Where(item => !superseded.Contains(item.Digest) && !invalidated.Contains(item.Digest))
            .Select(item => item.Digest).ToHashSet();
    }

    private bool DependsOn(ScreeningConductDecision decision, ScreeningConductEvidenceRef source)
    {
        if (source.Kind == "protocol-version")
            return source.Id == Policy.ProtocolVersionId && source.Digest == Policy.ProtocolContentDigest;
        if (source.Kind == "criteria")
            return source.Id == Policy.Criteria.CriteriaId && source.Digest == Policy.CriteriaDigest;
        if (source.Kind == "candidate-set")
            return source.Id == Policy.CandidateSet.CandidateSetId && source.Digest == Policy.CandidateSetDigest;
        if (decision.Evidence.Any(item => item.Kind == source.Kind && item.Id == source.Id && item.Digest == source.Digest))
            return true;
        var dependencyDigests = decision.SourceDecisionDigests
            .Concat(decision.SupersedesDecisionDigest is null ? [] : [decision.SupersedesDecisionDigest.Value]);
        return dependencyDigests.Any(digest =>
            _decisions.SingleOrDefault(item => item.Digest == digest) is { } dependency && DependsOn(dependency, source));
    }

    private static void EnsureBinding(ScreeningConductHeader header, ScreeningConductPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(header); ArgumentNullException.ThrowIfNull(policy);
        if (header.PolicyId != policy.PolicyId || header.PolicyDigest != policy.Digest || header.CandidateSetDigest != policy.CandidateSetDigest ||
            header.CriteriaDigest != policy.CriteriaDigest || header.ProtocolContentDigest != policy.ProtocolContentDigest)
            throw new ScreeningRuleException(ScreeningErrorCodes.UnverifiedConductAuthority, "Conduct header does not bind the verified policy.");
    }
}
