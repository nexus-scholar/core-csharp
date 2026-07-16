using System.Globalization;
using NexusScholar.Deduplication;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;

namespace NexusScholar.Screening.FullText;

public static class FullTextScreeningConductSchema
{
    public const string PolicySchemaId = "nexus.fulltext.screening-policy";
    public const string HeaderSchemaId = "nexus.fulltext.screening-header";
    public const string DecisionSchemaId = "nexus.fulltext.screening-decision";
    public const string InvalidationSchemaId = "nexus.fulltext.invalidation";
    public const string HandoffSchemaId = "nexus.fulltext.handoff";
    public const string SchemaVersion = "1.0.0";
}

public static class FullTextScreeningConductActorKinds
{
    public const string Human = ScreeningConductActorKinds.Human;
}

public static class FullTextScreeningConductEvidenceKinds
{
    public const string FullTextArtifact = "full-text-artifact";
    public const string FullTextExtractionAttempt = "full-text-extraction-attempt";
    public const string FullTextAdmission = "full-text-admission";
    public const string Criteria = "criteria";
    public const string ProtocolVersion = "protocol-version";
}

public static class FullTextScreeningConductErrorCodes
{
    public const string InvalidAuthorityChain = "invalid-fulltext-screening-authority";
    public const string MissingExtractionAttempt = "missing-fulltext-extraction-attempt";
    public const string InvalidActor = "invalid-fulltext-screening-actor";
    public const string InvalidStageEvidence = "invalid-fulltext-screening-stage-evidence";
    public const string AutomationFinalization = "automation-finalization-not-allowed";
    public const string MissingFullTextAdmission = "missing-fulltext-admission";
}

public interface IFullTextScreeningConductEntry
{
    int Ordinal { get; }
    ContentDigest Digest { get; }
}

public sealed class FullTextScreeningConductPolicy
{
    public const string SchemaId = FullTextScreeningConductSchema.PolicySchemaId;
    public const string SchemaVersion = FullTextScreeningConductSchema.SchemaVersion;

    private FullTextScreeningConductPolicy(
        string policyId,
        ScreeningCandidateSet candidateSet,
        ScreeningCriteria criteria,
        VerifiedProtocolVersion protocol,
        int requiredReviewCount,
        IReadOnlyList<ScreeningConductRoleAssignment> assignments,
        IReadOnlyList<string> adjudicatorRoles,
        IReadOnlyList<ScreeningExclusionReason> exclusionReasons,
        ScreeningConductActor approvedBy,
        DateTimeOffset approvedAt,
        string admissionConductId,
        string admissionHandoffId,
        ContentDigest admissionDigest,
        string admissionCandidateSetId,
        string admissionCandidateId,
        ContentDigest fullTextArtifactDigest,
        ContentDigest? extractionAttemptDigest)
    {
        PolicyId = Guard.NotBlank(policyId, nameof(policyId));
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
        AdmissionConductId = Guard.NotBlank(admissionConductId, nameof(admissionConductId));
        AdmissionHandoffId = Guard.NotBlank(admissionHandoffId, nameof(admissionHandoffId));
        AdmissionDigest = admissionDigest;
        AdmissionCandidateSetId = Guard.NotBlank(admissionCandidateSetId, nameof(admissionCandidateSetId));
        AdmissionCandidateId = Guard.NotBlank(admissionCandidateId, nameof(admissionCandidateId));
        FullTextArtifactDigest = fullTextArtifactDigest;
        ExtractionAttemptDigest = extractionAttemptDigest;
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
    public string AdmissionConductId { get; }
    public string AdmissionHandoffId { get; }
    public ContentDigest AdmissionDigest { get; }
    public string AdmissionCandidateSetId { get; }
    public string AdmissionCandidateId { get; }
    public ContentDigest FullTextArtifactDigest { get; }
    public ContentDigest? ExtractionAttemptDigest { get; }
    public ContentDigest Digest { get; }

    public static FullTextScreeningConductPolicy Create(
        string policyId,
        string candidateSetId,
        VerifiedDeduplicationResult deduplication,
        VerifiedProtocolVersion protocol,
        ScreeningCriteria criteria,
        VerifiedFullTextAdmission admission,
        int requiredReviewCount,
        IEnumerable<ScreeningConductRoleAssignment> assignments,
        IEnumerable<string>? adjudicatorRoles,
        IEnumerable<ScreeningExclusionReason>? exclusionReasons,
        ScreeningConductActor approvedBy,
        DateTimeOffset approvedAt,
        ContentDigest fullTextArtifactDigest,
        ContentDigest? extractionAttemptDigest = null)
    {
        ArgumentNullException.ThrowIfNull(deduplication);
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(approvedBy);
        if (admission is null)
            throw Rule(FullTextScreeningConductErrorCodes.MissingFullTextAdmission, "Full-text conduct requires a verified FE-04 admission.");
        if (!admission.PolicyDigest.IsValid)
            throw Rule(FullTextScreeningConductErrorCodes.MissingFullTextAdmission, "FE-04 admission must be verifiable.");
        if (!string.Equals(ScreeningStages.FullText, criteria.Stage, StringComparison.Ordinal))
            throw Rule(ScreeningErrorCodes.UnknownScreeningStage, "Full-text conduct requires full-text stage criteria.");
        if (!criteria.RequiresProtocolBinding ||
            !string.Equals(criteria.ApprovedProtocolBinding, protocol.Version.Id, StringComparison.Ordinal) ||
            !string.Equals(criteria.ApprovedProtocolDigest, protocol.Version.ContentDigest.ToString(), StringComparison.Ordinal) ||
            !string.Equals(criteria.CurrentProtocolContentDigest, protocol.Version.ContentDigest.ToString(), StringComparison.Ordinal) ||
            !string.Equals(criteria.ApprovedProtocolDigestScope, DigestScope.ProtocolContent.ToString(), StringComparison.Ordinal) ||
            !ScreeningProtocolBindingStatus.IsApproved(criteria.ApprovedProtocolStatus))
            throw Rule(ScreeningErrorCodes.InvalidProtocolBinding, "Full-text conduct criteria do not bind the verified approved Protocol.");
        if (!fullTextArtifactDigest.IsValid)
            throw Rule(FullTextScreeningConductErrorCodes.InvalidAuthorityChain, "Full-text conduct requires a valid raw artifact digest.");
        if (extractionAttemptDigest is not null && extractionAttemptDigest is not { IsValid: true })
            throw Rule(FullTextScreeningConductErrorCodes.InvalidAuthorityChain, "Extraction attempt digest must be valid.");

        var candidateSet = ScreeningCandidateSet.CreateFromDedupResult(
            Guard.NotBlank(candidateSetId, nameof(candidateSetId)),
            deduplication.Result,
            true,
            ScreeningSourceKinds.DeduplicationResult,
            [deduplication.Result.ResultId],
            null,
            deduplication.Result.NonClaims);

        if (!candidateSet.Candidates.Any(item => string.Equals(item.CandidateId, admission.CandidateId, StringComparison.Ordinal)))
            throw Rule(ScreeningErrorCodes.CandidateNotInSet, "Admission candidate must belong to the conduct candidate set.");
        if (!string.Equals(admission.CandidateSetId, candidateSet.CandidateSetId, StringComparison.Ordinal))
            throw Rule(FullTextScreeningConductErrorCodes.InvalidAuthorityChain, "Admission candidate-set does not match conduct candidate-set.");

        var normalizedAssignments = assignments
            .Select(item => new ScreeningConductRoleAssignment(
                Guard.NotBlank(item.ActorId, nameof(item.ActorId)),
                Guard.NotBlank(item.Role, nameof(item.Role))))
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ToArray();
        if (requiredReviewCount is < 1 or > 2)
            throw Rule(ScreeningErrorCodes.InsufficientReview, "Full-text conduct requires one or two independent reviews.");
        if (normalizedAssignments.Length < requiredReviewCount || normalizedAssignments.Distinct().Count() != normalizedAssignments.Length)
            throw Rule(ScreeningErrorCodes.UnauthorizedReviewer, "Full-text assignments must be unique and satisfy the review count.");
        if (!string.Equals(approvedBy.Kind, ScreeningConductActorKinds.Human, StringComparison.OrdinalIgnoreCase))
            throw Rule(ScreeningErrorCodes.AutomationCannotFinalize, "Conduct policy approval requires a human actor.");
        if (!normalizedAssignments.Any(item => item.ActorId == approvedBy.ActorId && item.Role == approvedBy.Role))
            throw Rule(ScreeningErrorCodes.UnauthorizedReviewer, "Policy approver must be an assigned reviewer.");

        var roles = (adjudicatorRoles ?? Array.Empty<string>())
            .Select(item => Guard.NotBlank(item, nameof(adjudicatorRoles)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
        var reasons = (exclusionReasons ?? Array.Empty<ScreeningExclusionReason>())
            .OrderBy(item => item.Code, StringComparer.Ordinal)
            .ToArray();
        if (reasons.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count() != reasons.Length ||
            reasons.Any(item => !string.Equals(item.Stage, ScreeningStages.FullText, StringComparison.Ordinal)))
            throw Rule(ScreeningErrorCodes.InvalidExclusionReason, "Exclusion reasons must be unique and full-text scoped.");

        return new FullTextScreeningConductPolicy(
            Guard.NotBlank(policyId, nameof(policyId)),
            candidateSet,
            criteria,
            protocol,
            requiredReviewCount,
            Array.AsReadOnly(normalizedAssignments),
            Array.AsReadOnly(roles),
            Array.AsReadOnly(reasons),
            approvedBy,
            approvedAt,
            admission.ConductId,
            admission.HandoffId,
            admission.Digest,
            admission.CandidateSetId,
            admission.CandidateId,
            fullTextArtifactDigest,
            extractionAttemptDigest);
    }

    public bool Authorizes(ScreeningConductActor actor) =>
        string.Equals(actor.Kind, ScreeningConductActorKinds.Human, StringComparison.OrdinalIgnoreCase) &&
        Assignments.Any(item => item.ActorId == actor.ActorId && item.Role == actor.Role);

    public bool AuthorizesAdjudication(ScreeningConductActor actor) =>
        Authorizes(actor) && AdjudicatorRoles.Contains(actor.Role, StringComparer.Ordinal);

    public bool AllowsReason(string code) => ExclusionReasons.Any(item => item.Code == code && item.Stage == ScreeningStages.FullText);

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private DigestEnvelope Envelope()
    {
        var content = new CanonicalJsonObject()
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
            .AddTimestamp("approved_at", ApprovedAt)
            .Add("admission_conduct_id", AdmissionConductId)
            .Add("admission_handoff_id", AdmissionHandoffId)
            .Add("admission_digest", AdmissionDigest.ToString())
            .Add("admission_candidate_set_id", AdmissionCandidateSetId)
            .Add("admission_candidate_id", AdmissionCandidateId)
            .Add("full_text_artifact_digest", FullTextArtifactDigest.ToString());
        if (ExtractionAttemptDigest is not null)
            content.Add("full_text_extraction_attempt_digest", ExtractionAttemptDigest.Value.ToString());
        return new DigestEnvelope(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion, content);
    }

    private static ScreeningRuleException Rule(string category, string message) => new(category, message);

    private static ContentDigest ComputeCandidateSetDigest(ScreeningCandidateSet candidateSet) =>
        new DigestEnvelope(DigestScope.CanonicalJsonRecord, ScreeningSchema.CandidateSetSchemaId, ScreeningSchema.CandidateSetSchemaVersion,
            new CanonicalJsonObject()
                .Add("candidate_set_id", candidateSet.CandidateSetId)
                .Add("source_kind", candidateSet.SourceKind)
                .Add("source_refs", CanonicalJsonValue.Array(candidateSet.SourceRefs.Select(value => CanonicalJsonValue.From(value)).ToArray()))
                .Add("locked", candidateSet.Locked)
                .Add("created_from_dedup_result_id", candidateSet.CreatedFromDedupResultId!)
                .Add("candidate_ids", CanonicalJsonValue.Array(candidateSet.Candidates.Select(item => item.CandidateId).OrderBy(value => value, StringComparer.Ordinal).Select(CanonicalJsonValue.From).ToArray()))
                .Add("unresolved_candidate_ids", CanonicalJsonValue.Array(candidateSet.UnresolvedCandidates.Select(item => item.CandidateId).OrderBy(value => value, StringComparer.Ordinal).Select(CanonicalJsonValue.From).ToArray())))
            .ComputeDigest();
}

public sealed class FullTextScreeningConductHeader
{
    public string ConductId { get; }
    public string PolicyId { get; }
    public ContentDigest PolicyDigest { get; }
    public string CandidateSetId { get; }
    public ContentDigest CandidateSetDigest { get; }
    public IReadOnlyList<string> CandidateIds { get; }
    public ScreeningConductActor CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    public string AdmissionConductId { get; }
    public string AdmissionHandoffId { get; }
    public ContentDigest AdmissionDigest { get; }
    public ContentDigest FullTextArtifactDigest { get; }
    public ContentDigest? ExtractionAttemptDigest { get; }
    public ContentDigest Digest { get; }

    internal FullTextScreeningConductHeader(
        string conductId,
        FullTextScreeningConductPolicy policy,
        ScreeningConductActor createdBy,
        DateTimeOffset createdAt)
    {
        ConductId = Guard.NotBlank(conductId, nameof(conductId));
        PolicyId = policy.PolicyId;
        PolicyDigest = policy.Digest;
        CandidateSetId = policy.CandidateSet.CandidateSetId;
        CandidateSetDigest = policy.CandidateSetDigest;
        CandidateIds = Array.AsReadOnly(new[] { policy.AdmissionCandidateId });
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        AdmissionConductId = policy.AdmissionConductId;
        AdmissionHandoffId = policy.AdmissionHandoffId;
        AdmissionDigest = policy.AdmissionDigest;
        FullTextArtifactDigest = policy.FullTextArtifactDigest;
        ExtractionAttemptDigest = policy.ExtractionAttemptDigest;
        Digest = Envelope().ComputeDigest();
    }

    public static FullTextScreeningConductHeader Create(string conductId, FullTextScreeningConductPolicy policy, ScreeningConductActor createdBy, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(createdBy);
        if (!policy.Authorizes(createdBy))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Conduct creation requires an authorized human reviewer.");
        return new FullTextScreeningConductHeader(Guard.NotBlank(conductId, nameof(conductId)), policy, createdBy, createdAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    internal DigestEnvelope Envelope()
    {
        var content = new CanonicalJsonObject()
            .Add("conduct_id", ConductId)
            .Add("policy_id", PolicyId)
            .Add("policy_digest", PolicyDigest.ToString())
            .Add("candidate_set_id", CandidateSetId)
            .Add("candidate_set_digest", CandidateSetDigest.ToString())
            .Add("candidate_ids", CanonicalJsonValue.Array(CandidateIds.Select(CanonicalJsonValue.From).ToArray()))
            .Add("created_by", CreatedBy.ToCanonicalJson())
            .AddTimestamp("created_at", CreatedAt)
            .Add("admission_conduct_id", AdmissionConductId)
            .Add("admission_handoff_id", AdmissionHandoffId)
            .Add("admission_digest", AdmissionDigest.ToString())
            .Add("full_text_artifact_digest", FullTextArtifactDigest.ToString());
        if (ExtractionAttemptDigest is not null)
            content.Add("full_text_extraction_attempt_digest", ExtractionAttemptDigest.Value.ToString());
        return new DigestEnvelope(DigestScope.CanonicalJsonRecord, FullTextScreeningConductSchema.HeaderSchemaId,
            FullTextScreeningConductSchema.SchemaVersion, content);
    }
}

public sealed class FullTextScreeningConductDecision : IFullTextScreeningConductEntry
{
    public const string SchemaId = FullTextScreeningConductSchema.DecisionSchemaId;
    public const string SchemaVersion = FullTextScreeningConductSchema.SchemaVersion;

    private FullTextScreeningConductDecision(string conductId, string policyId, ContentDigest policyDigest, int ordinal,
        ContentDigest previousDigest, string requestId, string candidateId, ScreeningConductDecisionKind kind,
        string verdict, ScreeningConductActor actor, string rationale, string? exclusionReasonCode,
        ContentDigest? supersedesDecisionDigest, string? resolvedConflictId,
        IReadOnlyList<ContentDigest> sourceDecisionDigests, IReadOnlyList<ScreeningConductEvidenceRef> evidence,
        DateTimeOffset decidedAt, ContentDigest? extractionAttemptDigest, bool usedExtractionFailure)
    {
        ConductId = conductId;
        PolicyId = policyId;
        PolicyDigest = policyDigest;
        Ordinal = ordinal;
        PreviousDigest = previousDigest;
        RequestId = requestId;
        CandidateId = candidateId;
        Kind = kind;
        Verdict = verdict;
        Actor = actor;
        Rationale = rationale;
        ExclusionReasonCode = exclusionReasonCode;
        SupersedesDecisionDigest = supersedesDecisionDigest;
        ResolvedConflictId = resolvedConflictId;
        SourceDecisionDigests = sourceDecisionDigests;
        Evidence = evidence;
        DecidedAt = decidedAt;
        ExtractionAttemptDigest = extractionAttemptDigest;
        UsedExtractionFailure = usedExtractionFailure;
        DecisionId = $"{conductId}:decision:{ordinal.ToString(CultureInfo.InvariantCulture)}";
        Digest = Envelope().ComputeDigest();
    }

    public ContentDigest? ExtractionAttemptDigest { get; }
    public bool UsedExtractionFailure { get; }
    public string ConductId { get; }
    public string PolicyId { get; }
    public ContentDigest PolicyDigest { get; }
    public int Ordinal { get; }
    public ContentDigest PreviousDigest { get; }
    public string RequestId { get; }
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
    public string DecisionId { get; }
    public ContentDigest Digest { get; }

    public static FullTextScreeningConductDecision Create(
        FullTextScreeningConductHeader header,
        int ordinal,
        ContentDigest previousDigest,
        string requestId,
        string candidateId,
        ScreeningConductDecisionKind kind,
        string verdict,
        ScreeningConductActor actor,
        string rationale,
        DateTimeOffset decidedAt,
        string? exclusionReasonCode = null,
        string? supersedesDecisionDigest = null,
        string? resolvedConflictId = null,
        IEnumerable<ContentDigest>? sourceDecisionDigests = null,
        IEnumerable<ScreeningConductEvidenceRef>? evidence = null,
        FullTextExtractionAttempt? extractionAttempt = null,
        ContentDigest? extractionAttemptDigest = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(actor);
        if (!string.Equals(actor.Kind, FullTextScreeningConductActorKinds.Human, StringComparison.OrdinalIgnoreCase))
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingHumanActor, "Full-text decisions require a human actor.");
        if (!header.PolicyDigest.IsValid || !header.FullTextArtifactDigest.IsValid)
            throw new ScreeningRuleException(FullTextScreeningConductErrorCodes.InvalidAuthorityChain, "Decision requires a valid full-text conduct policy and artifact digest.");
        if (!header.AdmissionDigest.IsValid)
            throw new ScreeningRuleException(FullTextScreeningConductErrorCodes.MissingFullTextAdmission, "Decision requires a verified FE-04 admission digest.");

        var effectiveExtractionAttemptDigest = extractionAttemptDigest ?? extractionAttempt?.Digest;
        if (effectiveExtractionAttemptDigest != header.ExtractionAttemptDigest)
            throw new ScreeningRuleException(FullTextScreeningConductErrorCodes.InvalidAuthorityChain, "Decision extraction evidence must match the policy extraction digest.");
        if (effectiveExtractionAttemptDigest.HasValue &&
            (extractionAttempt is null || extractionAttempt.Digest != effectiveExtractionAttemptDigest.Value))
            throw new ScreeningRuleException(FullTextScreeningConductErrorCodes.InvalidAuthorityChain,
                "Decision extraction evidence requires the exact verified extraction attempt.");

        var evidenceValues = (evidence ?? Array.Empty<ScreeningConductEvidenceRef>()).ToArray();
        var normalizedEvidence = evidenceValues.Distinct().ToList();
        if (extractionAttempt is not null)
        {
            var attemptDigest = effectiveExtractionAttemptDigest ?? extractionAttempt.Digest;
            var attemptRef = new ScreeningConductEvidenceRef(
                FullTextScreeningConductEvidenceKinds.FullTextExtractionAttempt,
                extractionAttempt.AttemptId,
                attemptDigest);
            if (!normalizedEvidence.Contains(attemptRef))
                normalizedEvidence.Add(attemptRef);
        }

        var extractionEvidence = normalizedEvidence
            .Where(item => item.Kind == FullTextScreeningConductEvidenceKinds.FullTextExtractionAttempt)
            .ToArray();
        if (effectiveExtractionAttemptDigest is null)
        {
            if (extractionEvidence.Length != 0)
                throw new ScreeningRuleException(FullTextScreeningConductErrorCodes.InvalidStageEvidence,
                    "Extraction evidence requires a policy-bound verified extraction attempt.");
        }
        else if (extractionAttempt is null || extractionEvidence.Length != 1 ||
            extractionEvidence[0].Id != extractionAttempt.AttemptId ||
            extractionEvidence[0].Digest != extractionAttempt.Digest)
        {
            throw new ScreeningRuleException(FullTextScreeningConductErrorCodes.InvalidStageEvidence,
                "Decision extraction evidence must identify exactly the policy-bound verified extraction attempt.");
        }

        if (extractionAttempt is not null &&
            IsExtractionFailure(extractionAttempt.Status) &&
            string.Equals(verdict, ScreeningVerdicts.Exclude, StringComparison.Ordinal))
        {
            throw new ScreeningRuleException(FullTextErrorCodes.ExtractionFailure,
                "A failed or unsupported extraction attempt cannot support a final exclusion.");
        }

        if (!normalizedEvidence.Any(item =>
            item.Kind == FullTextScreeningConductEvidenceKinds.FullTextArtifact &&
            item.Digest == header.FullTextArtifactDigest))
            throw new ScreeningRuleException(FullTextScreeningConductErrorCodes.InvalidStageEvidence,
                "Full-text decisions must identify the exact raw artifact evidence reviewed.");

        if (ordinal < 1 || !previousDigest.IsValid || !header.CandidateIds.Contains(candidateId, StringComparer.Ordinal))
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Decision chain position or candidate is invalid.");
        if (!ScreeningVerdicts.IsKnown(verdict))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnknownScreeningVerdict, "Unknown full-text verdict.");
        if (string.Equals(verdict, ScreeningVerdicts.Exclude, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(exclusionReasonCode))
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidExclusionReason, "Exclusion requires a reason code.");
        if (!string.Equals(verdict, ScreeningVerdicts.Exclude, StringComparison.Ordinal) && exclusionReasonCode is not null)
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidExclusionReason, "Only exclusion may carry a reason code.");

        var sources = (sourceDecisionDigests ?? Array.Empty<ContentDigest>()).Distinct()
            .OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
        ContentDigest? supersedes = supersedesDecisionDigest is null ? null : ContentDigest.Parse(supersedesDecisionDigest);
        if (kind == ScreeningConductDecisionKind.Correction && supersedes is null ||
            kind != ScreeningConductDecisionKind.Correction && supersedes is not null)
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision,
                "A Full Text correction must supersede exactly one current decision.");
        if (kind == ScreeningConductDecisionKind.Adjudication && (sources.Length == 0 || string.IsNullOrWhiteSpace(resolvedConflictId)) ||
            kind != ScreeningConductDecisionKind.Adjudication && (sources.Length != 0 || resolvedConflictId is not null))
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision,
                "A Full Text adjudication must identify the exact conflict and source decisions.");
        return new FullTextScreeningConductDecision(header.ConductId, header.PolicyId, header.PolicyDigest, ordinal,
            previousDigest, Guard.NotBlank(requestId, nameof(requestId)), Guard.NotBlank(candidateId, nameof(candidateId)),
            kind, verdict, actor, Guard.NotBlank(rationale, nameof(rationale)), exclusionReasonCode, supersedes,
            resolvedConflictId, Array.AsReadOnly(sources), Array.AsReadOnly(normalizedEvidence.OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Id, StringComparer.Ordinal).ToArray()), decidedAt, effectiveExtractionAttemptDigest,
            extractionAttempt is not null && IsExtractionFailure(extractionAttempt.Status));
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private static bool IsExtractionFailure(string extractionStatus) =>
        string.Equals(extractionStatus, FullTextExtractionAttemptStatuses.Failure, StringComparison.Ordinal) ||
        string.Equals(extractionStatus, FullTextExtractionAttemptStatuses.Unsupported, StringComparison.Ordinal);

    private DigestEnvelope Envelope()
    {
        var content = new CanonicalJsonObject().Add("conduct_id", ConductId).Add("policy_id", PolicyId)
            .Add("policy_digest", PolicyDigest.ToString()).Add("ordinal", Ordinal).Add("previous_digest", PreviousDigest.ToString())
            .Add("request_id", RequestId).Add("decision_id", DecisionId).Add("candidate_id", CandidateId)
            .Add("kind", Kind.ToString().ToLowerInvariant()).Add("verdict", Verdict).Add("actor", Actor.ToCanonicalJson())
            .Add("rationale", Rationale)
            .Add("source_decision_digests", CanonicalJsonValue.Array(SourceDecisionDigests.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()))
            .Add("evidence", CanonicalJsonValue.Array(Evidence.Select(item => item.ToCanonicalJson()).ToArray()))
            .AddTimestamp("decided_at", DecidedAt);
        if (ExclusionReasonCode is not null) content.Add("exclusion_reason_code", ExclusionReasonCode);
        if (SupersedesDecisionDigest is not null) content.Add("supersedes_decision_digest", SupersedesDecisionDigest.Value.ToString());
        if (ResolvedConflictId is not null) content.Add("resolved_conflict_id", ResolvedConflictId);
        if (ExtractionAttemptDigest is not null) content.Add("full_text_extraction_attempt_digest", ExtractionAttemptDigest.Value.ToString());
        return new DigestEnvelope(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion, content);
    }
}

public sealed class FullTextScreeningConductInvalidation : IFullTextScreeningConductEntry
{
    public const string SchemaId = FullTextScreeningConductSchema.InvalidationSchemaId;
    public const string SchemaVersion = FullTextScreeningConductSchema.SchemaVersion;

    private FullTextScreeningConductInvalidation(string conductId, int ordinal, ContentDigest previousDigest,
        string invalidationId, ScreeningConductEvidenceRef source, IReadOnlyList<ContentDigest> affected,
        ScreeningConductActor actor, string reason, DateTimeOffset invalidatedAt)
    {
        ConductId = conductId; Ordinal = ordinal; PreviousDigest = previousDigest; InvalidationId = invalidationId;
        Source = source; AffectedDecisionDigests = affected; Actor = actor; Reason = reason; InvalidatedAt = invalidatedAt;
        Digest = Envelope().ComputeDigest();
    }

    public ContentDigest Digest { get; }

    public string ConductId { get; }
    public int Ordinal { get; }
    public string InvalidationId { get; }
    public ContentDigest PreviousDigest { get; }
    public ScreeningConductEvidenceRef Source { get; }
    public IReadOnlyList<ContentDigest> AffectedDecisionDigests { get; }
    public ScreeningConductActor Actor { get; }
    public string Reason { get; }
    public DateTimeOffset InvalidatedAt { get; }

    public static FullTextScreeningConductInvalidation Create(
        FullTextScreeningConductHeader header,
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
        ArgumentNullException.ThrowIfNull(actor);
        if (!string.Equals(actor.Kind, FullTextScreeningConductActorKinds.Human, StringComparison.OrdinalIgnoreCase))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Invaliation requires a human actor.");
        if (ordinal < 1 || !previousDigest.IsValid)
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Invalidation chain position is invalid.");
        var affected = affectedDecisionDigests.Distinct().OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
        if (affected.Length == 0 || affected.Any(item => !item.IsValid))
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision, "Invalidation requires exact valid decision digests.");
        return new FullTextScreeningConductInvalidation(header.ConductId, ordinal, previousDigest,
            Guard.NotBlank(invalidationId, nameof(invalidationId)), source, Array.AsReadOnly(affected), actor,
            Guard.NotBlank(reason, nameof(reason)), invalidatedAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private DigestEnvelope Envelope() => new(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion,
        new CanonicalJsonObject().Add("conduct_id", ConductId).Add("ordinal", Ordinal)
            .Add("previous_digest", PreviousDigest.ToString()).Add("invalidation_id", InvalidationId)
            .Add("source", Source.ToCanonicalJson())
            .Add("affected_decision_digests", CanonicalJsonValue.Array(AffectedDecisionDigests.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()))
            .Add("actor", Actor.ToCanonicalJson()).Add("reason", Reason).AddTimestamp("invalidated_at", InvalidatedAt));
}

public sealed class FullTextScreeningConductJournal
{
    private readonly List<FullTextScreeningConductDecision> _decisions = [];
    private readonly List<FullTextScreeningConductInvalidation> _invalidations = [];

    private FullTextScreeningConductJournal(FullTextScreeningConductPolicy policy, FullTextScreeningConductHeader header)
    {
        Policy = policy;
        Header = header;
    }

    public FullTextScreeningConductPolicy Policy { get; }
    public FullTextScreeningConductHeader Header { get; }
    public ScreeningConductProjection Projection => Replay();
    public IReadOnlyList<FullTextScreeningConductDecision> Decisions => _decisions.AsReadOnly();
    public IReadOnlyList<FullTextScreeningConductInvalidation> Invalidations => _invalidations.AsReadOnly();

    public static FullTextScreeningConductJournal Create(FullTextScreeningConductPolicy policy, FullTextScreeningConductHeader header)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(header);
        return new FullTextScreeningConductJournal(policy, header);
    }

    public void Append(FullTextScreeningConductDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.PreviousDigest != Projection.HeadDigest || decision.Ordinal != _decisions.Count + _invalidations.Count + 1)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Decision does not extend full-text screening append-only state.");
        }
        if (decision.ConductId != Header.ConductId || decision.PolicyId != Policy.PolicyId || decision.PolicyDigest != Policy.Digest)
            throw new ScreeningRuleException(ScreeningErrorCodes.UnverifiedConductAuthority, "Decision belongs to stale or different Full Text authority.");
        if (!Policy.Authorizes(decision.Actor))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Decision actor is not assigned by policy.");
        if (decision.Verdict == ScreeningVerdicts.Exclude && !Policy.AllowsReason(decision.ExclusionReasonCode!))
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidExclusionReason, "Exclusion reason is not allowed by policy.");
        if (_decisions.Any(item => item.RequestId == decision.RequestId))
            throw new ScreeningRuleException(ScreeningErrorCodes.DuplicateDecisionId, "Decision request id was already used.");
        var current = CurrentDecisionDigests();
        if (decision.Kind == ScreeningConductDecisionKind.Review && _decisions.Any(item =>
            current.Contains(item.Digest) && item.CandidateId == decision.CandidateId &&
            item.Kind is ScreeningConductDecisionKind.Review or ScreeningConductDecisionKind.Correction &&
            item.Actor.ActorId == decision.Actor.ActorId))
            throw new ScreeningRuleException(ScreeningErrorCodes.DuplicateIndependentReviewer,
                "One actor cannot satisfy two independent Full Text review slots.");
        if (decision.Kind == ScreeningConductDecisionKind.Correction &&
            (decision.SupersedesDecisionDigest is null || !current.Contains(decision.SupersedesDecisionDigest.Value) ||
             !_decisions.Any(item => item.Digest == decision.SupersedesDecisionDigest.Value &&
                 item.CandidateId == decision.CandidateId && item.Actor.ActorId == decision.Actor.ActorId &&
                 item.Kind is ScreeningConductDecisionKind.Review or ScreeningConductDecisionKind.Correction)))
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision,
                "A Full Text correction must supersede that actor's current decision for the candidate.");
        if (decision.Kind == ScreeningConductDecisionKind.Adjudication)
        {
            if (!Policy.AuthorizesAdjudication(decision.Actor))
                throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Actor is not authorized to adjudicate.");
            var conflict = Projection.Conflicts.SingleOrDefault(item =>
                item.CandidateId == decision.CandidateId && !item.Resolved && item.ConflictId == decision.ResolvedConflictId);
            if (conflict is null || !conflict.SourceDecisionDigests.SequenceEqual(decision.SourceDecisionDigests))
                throw new ScreeningRuleException(ScreeningErrorCodes.AdjudicationSourceMismatch,
                    "Full Text adjudication must bind the exact unresolved conflict and source decisions.");
        }
        _decisions.Add(decision);
    }

    public void Append(FullTextScreeningConductInvalidation invalidation)
    {
        ArgumentNullException.ThrowIfNull(invalidation);
        if (invalidation.ConductId != Header.ConductId)
            throw new ScreeningRuleException(ScreeningErrorCodes.UnverifiedConductAuthority, "Invalidation belongs to a different full-text conduct.");
        if (invalidation.PreviousDigest != Projection.HeadDigest || invalidation.Ordinal != _decisions.Count + _invalidations.Count + 1)
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Invalidation does not extend full-text screening append-only state.");
        if (!Policy.Authorizes(invalidation.Actor))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Invalidation actor is not assigned by policy.");
        if (_invalidations.Any(item => item.InvalidationId == invalidation.InvalidationId))
            throw new ScreeningRuleException(ScreeningErrorCodes.DuplicateDecisionId, "Invalidation id was already used.");
        if (!IsBoundSource(invalidation.Source))
            throw new ScreeningRuleException(FullTextScreeningConductErrorCodes.InvalidStageEvidence, "Invalidation source is not bound to this full-text policy.");
        var affected = _decisions.Where(item => CurrentDecisionDigests().Contains(item.Digest) && DependsOn(item, invalidation.Source))
            .Select(item => item.Digest).ToHashSet();
        if (affected.Count == 0 || !affected.SetEquals(invalidation.AffectedDecisionDigests))
            throw new ScreeningRuleException(ScreeningErrorCodes.MissingSourceDecision,
                "Invalidation must name the complete current decision set affected by its exact source.");
        _invalidations.Add(invalidation);
    }

    public FullTextScreeningConductHandoff CreateHandoff(string handoffId, DateTimeOffset createdAt) =>
        FullTextScreeningConductHandoff.Create(this, handoffId, createdAt);

    public static FullTextScreeningConductJournal Rehydrate(
        FullTextScreeningConductHeader header,
        FullTextScreeningConductPolicy policy,
        IEnumerable<FullTextScreeningConductDecision> decisions,
        IEnumerable<FullTextScreeningConductInvalidation>? invalidations = null)
    {
        return RehydrateEntries(header, policy,
            (decisions ?? throw new ArgumentNullException(nameof(decisions))).Cast<IFullTextScreeningConductEntry>()
            .Concat(invalidations ?? Array.Empty<FullTextScreeningConductInvalidation>())
            .OrderBy(entry => entry.Ordinal).ToArray());
    }

    public static FullTextScreeningConductJournal RehydrateEntries(
        FullTextScreeningConductHeader header,
        FullTextScreeningConductPolicy policy,
        IEnumerable<IFullTextScreeningConductEntry> entries)
    {
        var journal = Create(policy, header);
        foreach (var entry in entries ?? throw new ArgumentNullException(nameof(entries)))
        {
            switch (entry)
            {
                case FullTextScreeningConductDecision decision: journal.Append(decision); break;
                case FullTextScreeningConductInvalidation invalidation: journal.Append(invalidation); break;
                default: throw new ScreeningRuleException(ScreeningErrorCodes.InvalidConductChain, "Unknown Full Text conduct entry type.");
            }
        }
        return journal;
    }

    private ScreeningConductProjection Replay()
    {
        var invalidated = _invalidations.SelectMany(item => item.AffectedDecisionDigests).ToHashSet();
        var superseded = _decisions.Where(item => item.SupersedesDecisionDigest is not null)
            .Select(item => item.SupersedesDecisionDigest!.Value).ToHashSet();
        var current = _decisions.Where(item => !invalidated.Contains(item.Digest) && !superseded.Contains(item.Digest)).ToArray();
        var outcomes = new Dictionary<string, ScreeningConductOutcome>(StringComparer.Ordinal);
        var conflicts = new List<ScreeningConductConflict>();
        foreach (var candidateId in Header.CandidateIds)
        {
            var candidate = current.Where(item => item.CandidateId == candidateId).ToArray();
            var adjudication = candidate.LastOrDefault(item => item.Kind == ScreeningConductDecisionKind.Adjudication);
            var reviews = candidate.Where(item => item.Kind != ScreeningConductDecisionKind.Adjudication).ToArray();
            if (reviews.Select(item => item.Verdict).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                var conflictSources = reviews.Select(item => item.Digest).OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
                var conflictId = $"fulltext-conflict-{ContentDigest.Sha256Utf8(string.Join("|", conflictSources)).Value[7..23]}";
                var resolved = adjudication is not null && adjudication.ResolvedConflictId == conflictId &&
                    conflictSources.SequenceEqual(adjudication.SourceDecisionDigests);
                conflicts.Add(new ScreeningConductConflict(conflictId, candidateId, Array.AsReadOnly(conflictSources), resolved));
                if (resolved)
                {
                    var support = conflictSources.Append(adjudication!.Digest).OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray();
                    outcomes[candidateId] = new ScreeningConductOutcome(candidateId, adjudication.Verdict,
                        Array.AsReadOnly(support), adjudication.ExclusionReasonCode);
                }
            }
            else if (reviews.Length >= Policy.RequiredReviewCount)
            {
                var latest = reviews[^1];
                outcomes[candidateId] = new ScreeningConductOutcome(candidateId, latest.Verdict,
                    Array.AsReadOnly(reviews.Select(item => item.Digest).OrderBy(item => item.ToString(), StringComparer.Ordinal).ToArray()),
                    latest.ExclusionReasonCode);
            }
        }
        var ready = Header.CandidateIds.All(outcomes.ContainsKey) && conflicts.All(item => item.Resolved) &&
            outcomes.Values.All(item => item.Verdict != ScreeningVerdicts.NeedsReview);
        return new ScreeningConductProjection(CurrentHead(), outcomes, Array.AsReadOnly(conflicts.ToArray()), invalidated, ready);
    }

    private ContentDigest CurrentHead()
    {
        var entry = _decisions.Cast<object>().Concat(_invalidations).OrderBy(item => item switch
        {
            FullTextScreeningConductDecision decision => decision.Ordinal,
            FullTextScreeningConductInvalidation invalidation => invalidation.Ordinal,
            _ => 0
        }).LastOrDefault();
        return entry switch
        {
            FullTextScreeningConductDecision decision => decision.Digest,
            FullTextScreeningConductInvalidation invalidation => invalidation.Digest,
            _ => Header.Digest
        };
    }

    private HashSet<ContentDigest> CurrentDecisionDigests()
    {
        var invalidated = _invalidations.SelectMany(item => item.AffectedDecisionDigests).ToHashSet();
        var superseded = _decisions.Where(item => item.SupersedesDecisionDigest is not null)
            .Select(item => item.SupersedesDecisionDigest!.Value).ToHashSet();
        return _decisions.Where(item => !invalidated.Contains(item.Digest) && !superseded.Contains(item.Digest))
            .Select(item => item.Digest).ToHashSet();
    }

    private bool IsBoundSource(ScreeningConductEvidenceRef source) => source.Kind switch
    {
        FullTextScreeningConductEvidenceKinds.FullTextArtifact => source.Digest == Policy.FullTextArtifactDigest,
        FullTextScreeningConductEvidenceKinds.FullTextExtractionAttempt =>
            Policy.ExtractionAttemptDigest is not null && source.Digest == Policy.ExtractionAttemptDigest.Value,
        FullTextScreeningConductEvidenceKinds.FullTextAdmission =>
            source.Id == Policy.AdmissionHandoffId && source.Digest == Policy.AdmissionDigest,
        FullTextScreeningConductEvidenceKinds.Criteria =>
            source.Id == Policy.Criteria.CriteriaId && source.Digest == Policy.CriteriaDigest,
        FullTextScreeningConductEvidenceKinds.ProtocolVersion =>
            source.Id == Policy.ProtocolVersionId && source.Digest == Policy.ProtocolContentDigest,
        _ => false
    };

    private bool DependsOn(FullTextScreeningConductDecision decision, ScreeningConductEvidenceRef source)
    {
        if (source.Kind is FullTextScreeningConductEvidenceKinds.FullTextArtifact or
            FullTextScreeningConductEvidenceKinds.FullTextAdmission or
            FullTextScreeningConductEvidenceKinds.Criteria or
            FullTextScreeningConductEvidenceKinds.ProtocolVersion)
            return IsBoundSource(source);
        if (source.Kind == FullTextScreeningConductEvidenceKinds.FullTextExtractionAttempt)
            return decision.ExtractionAttemptDigest == source.Digest || decision.Evidence.Any(item => item == source);
        return decision.Evidence.Any(item => item == source);
    }

    internal static ScreeningRuleException Rule(string category, string message) => new(category, message);
}

public sealed class FullTextScreeningConductHandoff
{
    public const string SchemaId = FullTextScreeningConductSchema.HandoffSchemaId;
    public const string SchemaVersion = FullTextScreeningConductSchema.SchemaVersion;

    private FullTextScreeningConductHandoff(string handoffId, FullTextScreeningConductJournal journal, DateTimeOffset createdAt)
    {
        HandoffId = Guard.NotBlank(handoffId, nameof(handoffId));
        ConductId = journal.Header.ConductId;
        PolicyDigest = journal.Policy.Digest;
        JournalHeadDigest = journal.Projection.HeadDigest;
        AdmissionConductId = journal.Policy.AdmissionConductId;
        AdmissionHandoffId = journal.Policy.AdmissionHandoffId;
        AdmissionDigest = journal.Policy.AdmissionDigest;
        FullTextArtifactDigest = journal.Policy.FullTextArtifactDigest;
        ExtractionAttemptDigest = journal.Policy.ExtractionAttemptDigest;
        Outcomes = journal.Projection.Outcomes.Values
            .OrderBy(item => item.CandidateId, StringComparer.Ordinal)
            .ToArray()
            .AsReadOnly();
        CreatedAt = createdAt;
        Digest = Envelope().ComputeDigest();
    }

    public string HandoffId { get; }
    public string ConductId { get; }
    public ContentDigest PolicyDigest { get; }
    public ContentDigest JournalHeadDigest { get; }
    public IReadOnlyList<ScreeningConductOutcome> Outcomes { get; }
    public string AdmissionConductId { get; }
    public string AdmissionHandoffId { get; }
    public ContentDigest AdmissionDigest { get; }
    public ContentDigest FullTextArtifactDigest { get; }
    public ContentDigest? ExtractionAttemptDigest { get; }
    public DateTimeOffset CreatedAt { get; }
    public ContentDigest Digest { get; }

    public static FullTextScreeningConductHandoff Create(FullTextScreeningConductJournal journal, string handoffId, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (!journal.Projection.HandoffReady)
            throw new ScreeningRuleException(ScreeningErrorCodes.InsufficientReview, "Full-text conduct handoff is not ready.");
        return new FullTextScreeningConductHandoff(Guard.NotBlank(handoffId, nameof(handoffId)), journal, createdAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private DigestEnvelope Envelope()
    {
        var content = new CanonicalJsonObject()
            .Add("handoff_id", HandoffId)
            .Add("conduct_id", ConductId)
            .Add("policy_digest", PolicyDigest.ToString())
            .Add("journal_head_digest", JournalHeadDigest.ToString())
            .Add("admission_conduct_id", AdmissionConductId)
            .Add("admission_handoff_id", AdmissionHandoffId)
            .Add("admission_digest", AdmissionDigest.ToString())
            .Add("full_text_artifact_digest", FullTextArtifactDigest.ToString())
            .Add("outcomes", CanonicalJsonValue.Array(Outcomes.Select(OutcomeJson).ToArray()))
            .AddTimestamp("created_at", CreatedAt);
        if (ExtractionAttemptDigest is not null)
            content.Add("full_text_extraction_attempt_digest", ExtractionAttemptDigest.Value.ToString());
        return new DigestEnvelope(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion, content);
    }

    private static CanonicalJsonObject OutcomeJson(ScreeningConductOutcome item)
    {
        var content = new CanonicalJsonObject().Add("candidate_id", item.CandidateId).Add("verdict", item.Verdict)
            .Add("supporting_decision_digests", CanonicalJsonValue.Array(item.SupportingDecisionDigests.Select(value => CanonicalJsonValue.From(value.ToString())).ToArray()))
            .Add("screening_stage", ScreeningStages.FullText);
        if (item.ExclusionReasonCode is not null) content.Add("exclusion_reason_code", item.ExclusionReasonCode);
        return content;
    }
}
