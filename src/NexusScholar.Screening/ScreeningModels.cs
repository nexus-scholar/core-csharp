using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.Screening;

public static class ScreeningErrorCodes
{
    public const string InvalidScreeningInput = "invalid-screening-input";
    public const string RawSearchTraceNotScreenable = "raw-search-trace-not-screenable";
    public const string CandidateSetNotLocked = "candidate-set-not-locked";
    public const string CandidateNotInSet = "candidate-not-in-set";
    public const string UnknownScreeningStage = "unknown-screening-stage";
    public const string UnknownScreeningVerdict = "unknown-screening-verdict";
    public const string MissingHumanActor = "missing-human-actor";
    public const string AutomationCannotFinalize = "automation-cannot-finalize";
    public const string MissingRationale = "missing-rationale";
    public const string InvalidConfidence = "invalid-confidence";
    public const string InvalidProtocolBinding = "invalid-protocol-binding";
    public const string MissingCriteriaDigest = "missing-criteria-digest";
    public const string InvalidCriteriaDigestScope = "invalid-criteria-digest-scope";
    public const string CriteriaDigestMismatch = "criteria-digest-mismatch";
    public const string DuplicateDecisionId = "duplicate-decision-id";
    public const string DecisionNotAppendOnly = "decision-not-append-only";
    public const string UnresolvedConflict = "unresolved-conflict";
    public const string MissingSourceDecision = "missing-source-decision";
    public const string AdjudicationSourceMismatch = "adjudication-source-mismatch";
    public const string AppProjectionNotCoreAuthority = "app-projection-not-core-authority";
    public const string FullTextArtifactRequired = "full-text-artifact-required";
    public const string LocalPathNotArtifactIdentity = "local-path-not-artifact-identity";
}

public static class ScreeningSchema
{
    public const string CandidateSetSchemaId = "nexus.screening.candidate-set";
    public const string CandidateSetSchemaVersion = "1.0.0";
    public const string CriteriaSchemaId = "nexus.screening.criteria";
    public const string CriteriaSchemaVersion = "1.0.0";
    public const string DecisionSchemaId = "nexus.screening.decision";
    public const string DecisionSchemaVersion = "1.0.0";
}

public static class ScreeningSourceKinds
{
    public const string DeduplicationResult = "deduplication-result";
    public const string LockedReviewableCandidateSet = "locked-reviewable-candidate-set";

    public static bool IsKnown(string sourceKind)
    {
        return sourceKind.Equals(DeduplicationResult, StringComparison.Ordinal)
            || sourceKind.Equals(LockedReviewableCandidateSet, StringComparison.Ordinal);
    }
}

public static class ScreeningStages
{
    public const string TitleAbstract = "title_abstract";
    public const string FullText = "full_text";
    public const string HumanAdjudication = "human_adjudication";

    private static readonly string[] AllStages =
    [
        TitleAbstract,
        FullText,
        HumanAdjudication
    ];

    public static bool IsKnown(string stage)
    {
        return !string.IsNullOrWhiteSpace(stage) && AllStages.Contains(stage.Trim(), StringComparer.Ordinal);
    }

    public static int Ordinal(string stage)
    {
        return stage switch
        {
            TitleAbstract => 0,
            FullText => 1,
            HumanAdjudication => 2,
            _ => -1
        };
    }
}

public static class ScreeningVerdicts
{
    public const string Include = "include";
    public const string Exclude = "exclude";
    public const string NeedsReview = "needs_review";

    private static readonly string[] AllVerdicts =
    [
        Include,
        Exclude,
        NeedsReview
    ];

    public static bool IsKnown(string verdict)
    {
        return !string.IsNullOrWhiteSpace(verdict) && AllVerdicts.Contains(verdict.Trim(), StringComparer.Ordinal);
    }
}

public static class ScreeningProtocolBindingStatus
{
    public const string Approved = "approved";

    public static bool IsApproved(string? status)
    {
        return string.Equals(status, Approved, StringComparison.Ordinal);
    }
}

public enum ScreeningDecisionKind
{
    Suggestion = 0,
    Human,
    Adjudication
}

public sealed class ScreeningActor
{
    private ScreeningActor(string actorId, bool isHuman)
    {
        ActorId = Guard.NotBlank(actorId, nameof(actorId));
        IsHuman = isHuman;
    }

    public string ActorId { get; }
    public bool IsHuman { get; }
    public static ScreeningActor Human(string actorId) => new(Guard.NotBlank(actorId, nameof(actorId)), true);
    public static ScreeningActor Automation(string actorId) => new(Guard.NotBlank(actorId, nameof(actorId)), false);
}

public sealed class ScreeningCandidateSet
{
    public ScreeningCandidateSet(
        string schemaId,
        string schemaVersion,
        string candidateSetId,
        string sourceKind,
        IReadOnlyList<string> sourceRefs,
        bool locked,
        string? createdFromDedupResultId,
        string? createdFromDedupResultDigest,
        IReadOnlyList<DedupCandidateRecord> candidates,
        IReadOnlyList<DedupCandidateRecord> unresolvedCandidates,
        IReadOnlyList<string> nonClaims)
    {
        SchemaId = Guard.NotBlank(schemaId, nameof(schemaId));
        SchemaVersion = Guard.NotBlank(schemaVersion, nameof(schemaVersion));
        CandidateSetId = Guard.NotBlank(candidateSetId, nameof(candidateSetId));
        SourceKind = Guard.NotBlank(sourceKind, nameof(sourceKind));

        if (!ScreeningSourceKinds.IsKnown(SourceKind))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.InvalidScreeningInput,
                "Unsupported screening source kind.");
        }

        SourceRefs = FreezeSourceRefs(sourceRefs);
        Locked = locked;
        CreatedFromDedupResultId = createdFromDedupResultId;
        CreatedFromDedupResultDigest = createdFromDedupResultDigest;
        Candidates = FreezeCandidates(candidates);
        UnresolvedCandidates = FreezeCandidates(unresolvedCandidates);
        NonClaims = FreezeClaims(nonClaims);
    }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public string CandidateSetId { get; }

    public string SourceKind { get; }

    public IReadOnlyList<string> SourceRefs { get; }

    public bool Locked { get; }

    public string? CreatedFromDedupResultId { get; }

    public string? CreatedFromDedupResultDigest { get; }

    public IReadOnlyList<DedupCandidateRecord> Candidates { get; }

    public IReadOnlyList<DedupCandidateRecord> UnresolvedCandidates { get; }

    public IReadOnlyList<string> NonClaims { get; }

    public bool HasCandidate(string candidateId)
    {
        return Candidates.Any(item => string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal))
            || UnresolvedCandidates.Any(item => string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal));
    }

    private static IReadOnlyList<string> FreezeSourceRefs(IReadOnlyList<string> sourceRefs)
    {
        return new System.Collections.ObjectModel.ReadOnlyCollection<string>((sourceRefs ?? Array.Empty<string>()).ToArray());
    }

    private static IReadOnlyList<string> FreezeClaims(IReadOnlyList<string> nonClaims)
    {
        return new System.Collections.ObjectModel.ReadOnlyCollection<string>((nonClaims ?? Array.Empty<string>()).ToArray());
    }

    private static IReadOnlyList<DedupCandidateRecord> FreezeCandidates(IReadOnlyList<DedupCandidateRecord> candidates)
    {
        return new System.Collections.ObjectModel.ReadOnlyCollection<DedupCandidateRecord>((candidates ?? Array.Empty<DedupCandidateRecord>()).ToArray());
    }

    public static ScreeningCandidateSet CreateFromDedupResult(
        string candidateSetId,
        DeduplicationResult dedupResult,
        bool locked,
        string sourceKind,
        IReadOnlyList<string>? sourceRefs = null,
        string? createdFromDedupResultDigest = null,
        IReadOnlyList<string>? nonClaims = null)
    {
        if (dedupResult is null)
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidScreeningInput, "Deduplication result is required.");
        }

        if (!ScreeningSourceKinds.IsKnown(sourceKind))
        {
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidScreeningInput, "Unsupported screening source kind.");
        }

        return new ScreeningCandidateSet(
            ScreeningSchema.CandidateSetSchemaId,
            ScreeningSchema.CandidateSetSchemaVersion,
            Guard.NotBlank(candidateSetId, nameof(candidateSetId)),
            Guard.NotBlank(sourceKind, nameof(sourceKind)),
            sourceRefs ?? [dedupResult.SchemaId],
            locked,
            Guard.NotBlank(dedupResult.ResultId, nameof(dedupResult.ResultId)),
            createdFromDedupResultDigest,
            dedupResult.RawCandidates,
            dedupResult.UnresolvedCandidates,
            nonClaims ?? Array.Empty<string>());
    }

    public static ScreeningCandidateSet CreateLockedReviewableCandidateSet(
        string candidateSetId,
        IReadOnlyList<DedupCandidateRecord> candidates,
        IReadOnlyList<DedupCandidateRecord>? unresolvedCandidates = null,
        IReadOnlyList<string>? sourceRefs = null,
        bool locked = true,
        IReadOnlyList<string>? nonClaims = null)
    {
        return new ScreeningCandidateSet(
            ScreeningSchema.CandidateSetSchemaId,
            ScreeningSchema.CandidateSetSchemaVersion,
            Guard.NotBlank(candidateSetId, nameof(candidateSetId)),
            ScreeningSourceKinds.LockedReviewableCandidateSet,
            sourceRefs ?? Array.Empty<string>(),
            locked,
            null,
            null,
            candidates ?? throw new ScreeningRuleException(ScreeningErrorCodes.InvalidScreeningInput, "Reviewable candidates are required."),
            unresolvedCandidates ?? Array.Empty<DedupCandidateRecord>(),
            nonClaims ?? Array.Empty<string>());
    }
}

public sealed class ScreeningCriteria
{
    public ScreeningCriteria(
        string criteriaId,
        string criteriaVersion,
        string stage,
        CanonicalJsonValue includeCriteria,
        CanonicalJsonValue excludeCriteria,
        bool requiresProtocolBinding,
        string? approvedProtocolBinding,
        string? approvedProtocolDigest = null,
        string? workflowBinding = null,
        string? reviewGuidance = null,
        CanonicalJsonValue? fullTextRequirements = null,
        string? digestScopeHint = null,
        string? approvedProtocolDigestScope = null,
        string? approvedProtocolStatus = null,
        string? currentProtocolContentDigest = null)
    {
        CriteriaId = Guard.NotBlank(criteriaId, nameof(criteriaId));
        CriteriaVersion = Guard.NotBlank(criteriaVersion, nameof(criteriaVersion));
        Stage = Guard.NotBlank(stage, nameof(stage));
        IncludeCriteria = includeCriteria;
        ExcludeCriteria = excludeCriteria;
        RequiresProtocolBinding = requiresProtocolBinding;
        ApprovedProtocolBinding = approvedProtocolBinding;
        ApprovedProtocolDigest = approvedProtocolDigest;
        WorkflowBinding = workflowBinding;
        ReviewGuidance = reviewGuidance;
        FullTextRequirements = fullTextRequirements;
        DigestScopeHint = digestScopeHint;
        ApprovedProtocolDigestScope = approvedProtocolDigestScope;
        ApprovedProtocolStatus = approvedProtocolStatus;
        CurrentProtocolContentDigest = currentProtocolContentDigest;

        if (!ScreeningStages.IsKnown(Stage))
        {
            throw new ScreeningRuleException(
                ScreeningErrorCodes.UnknownScreeningStage,
                "Screening criteria stage must be a known screening stage.");
        }
    }

    public string SchemaId => ScreeningSchema.CriteriaSchemaId;

    public string SchemaVersion => ScreeningSchema.CriteriaSchemaVersion;

    public string CriteriaId { get; }

    public string CriteriaVersion { get; }

    public string Stage { get; }

    public CanonicalJsonValue IncludeCriteria { get; }

    public CanonicalJsonValue ExcludeCriteria { get; }

    public bool RequiresProtocolBinding { get; }

    public string? ApprovedProtocolBinding { get; }

    public string? ApprovedProtocolDigest { get; }

    public string? ApprovedProtocolDigestScope { get; }

    public string? ApprovedProtocolStatus { get; }

    public string? CurrentProtocolContentDigest { get; }

    public string? WorkflowBinding { get; }

    public string? ReviewGuidance { get; }

    public CanonicalJsonValue? FullTextRequirements { get; }

    public string? DigestScopeHint { get; }

    public CanonicalJsonObject ToCanonicalJson()
    {
        var result = new CanonicalJsonObject()
            .Add("criteria_id", CriteriaId)
            .Add("criteria_version", CriteriaVersion)
            .Add("exclude", ExcludeCriteria)
            .Add("include", IncludeCriteria)
            .Add("require_protocol_binding", RequiresProtocolBinding)
            .Add("schema_id", SchemaId)
            .Add("schema_version", SchemaVersion)
            .Add("stage", Stage);

        if (!string.IsNullOrWhiteSpace(ApprovedProtocolBinding))
        {
            result.Add("approved_protocol_binding", ApprovedProtocolBinding!);
        }

        if (ApprovedProtocolDigest is not null)
        {
            result.Add("approved_protocol_digest", ApprovedProtocolDigest);
        }

        if (ApprovedProtocolDigestScope is not null)
        {
            result.Add("approved_protocol_digest_scope", ApprovedProtocolDigestScope);
        }

        if (ApprovedProtocolStatus is not null)
        {
            result.Add("approved_protocol_status", ApprovedProtocolStatus);
        }

        if (CurrentProtocolContentDigest is not null)
        {
            result.Add("current_protocol_content_digest", CurrentProtocolContentDigest);
        }

        if (WorkflowBinding is not null)
        {
            result.Add("workflow_binding", WorkflowBinding);
        }

        if (!string.IsNullOrWhiteSpace(ReviewGuidance))
        {
            result.Add("review_guidance", ReviewGuidance!);
        }

        if (FullTextRequirements is not null)
        {
            result.Add("full_text_requirements", FullTextRequirements);
        }

        return result;
    }

    public CanonicalJsonObject ToDigestCanonicalJson()
    {
        var result = new CanonicalJsonObject()
            .Add("criteria_id", CriteriaId)
            .Add("criteria_version", CriteriaVersion)
            .Add("exclude", ExcludeCriteria)
            .Add("include", IncludeCriteria)
            .Add("require_protocol_binding", RequiresProtocolBinding)
            .Add("schema_id", SchemaId)
            .Add("schema_version", SchemaVersion)
            .Add("stage", Stage);

        if (!string.IsNullOrWhiteSpace(ApprovedProtocolBinding))
        {
            result.Add("approved_protocol_binding", ApprovedProtocolBinding!);
        }

        if (ApprovedProtocolDigest is not null)
        {
            result.Add("approved_protocol_digest", ApprovedProtocolDigest);
        }

        if (ApprovedProtocolDigestScope is not null)
        {
            result.Add("approved_protocol_digest_scope", ApprovedProtocolDigestScope);
        }

        if (ApprovedProtocolStatus is not null)
        {
            result.Add("approved_protocol_status", ApprovedProtocolStatus);
        }

        if (CurrentProtocolContentDigest is not null)
        {
            result.Add("current_protocol_content_digest", CurrentProtocolContentDigest);
        }

        if (WorkflowBinding is not null)
        {
            result.Add("workflow_binding", WorkflowBinding);
        }

        if (!string.IsNullOrWhiteSpace(ReviewGuidance))
        {
            result.Add("review_guidance", ReviewGuidance!);
        }

        if (FullTextRequirements is not null)
        {
            result.Add("full_text_requirements", FullTextRequirements);
        }

        return result;
    }

    public DigestEnvelope ToDigestEnvelope()
    {
        return new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            SchemaId,
            SchemaVersion,
            ToDigestCanonicalJson());
    }

    public ContentDigest ComputeDigest()
    {
        return ToDigestEnvelope().ComputeDigest();
    }
}

public sealed class ScreeningSuggestion
{
    public ScreeningSuggestion(
        string suggestionId,
        string candidateSetId,
        string candidateId,
        string stage,
        string suggestedVerdict,
        double? confidence,
        string? rationale,
        string? promptDigest,
        string? responseDigest,
        IReadOnlyList<string>? evidenceRefs = null,
        string? sourceDecisionId = null,
        IReadOnlyList<string>? nonClaims = null)
    {
        SuggestionId = Guard.NotBlank(suggestionId, nameof(suggestionId));
        CandidateSetId = Guard.NotBlank(candidateSetId, nameof(candidateSetId));
        CandidateId = Guard.NotBlank(candidateId, nameof(candidateId));
        Stage = Guard.NotBlank(stage, nameof(stage));
        SuggestedVerdict = Guard.NotBlank(suggestedVerdict, nameof(suggestedVerdict));
        Confidence = confidence;
        Rationale = rationale;
        PromptDigest = promptDigest;
        ResponseDigest = responseDigest;
        EvidenceRefs = new System.Collections.ObjectModel.ReadOnlyCollection<string>((evidenceRefs ?? Array.Empty<string>()).ToArray());
        SourceDecisionId = sourceDecisionId;
        NonClaims = new System.Collections.ObjectModel.ReadOnlyCollection<string>((nonClaims ?? Array.Empty<string>()).ToArray());
    }

    public string SuggestionId { get; }

    public string CandidateSetId { get; }

    public string CandidateId { get; }

    public string Stage { get; }

    public string SuggestedVerdict { get; }

    public double? Confidence { get; }

    public string? Rationale { get; }

    public string? PromptDigest { get; }

    public string? ResponseDigest { get; }

    public IReadOnlyList<string> EvidenceRefs { get; }

    public string? SourceDecisionId { get; }

    public IReadOnlyList<string> NonClaims { get; }
}

public sealed class ScreeningDecision
{
    public ScreeningDecision(
        string decisionId,
        string candidateSetId,
        string candidateId,
        string? workId,
        string? dedupClusterId,
        string stage,
        string verdict,
        ScreeningActor? actor,
        DateTimeOffset decidedAt,
        string? rationale,
        double? confidence,
        string criteriaRef,
        string criteriaDigest,
        IReadOnlyList<string>? evidenceRefs = null,
        IReadOnlyList<string>? sourceDecisionIds = null,
        IReadOnlyList<string>? sourceSuggestionIds = null,
        string? conflictId = null,
        string? resolvedConflictId = null,
        ScreeningDecisionKind decisionKind = ScreeningDecisionKind.Human,
        IReadOnlyList<string>? nonClaims = null)
    {
        DecisionId = Guard.NotBlank(decisionId, nameof(decisionId));
        CandidateSetId = Guard.NotBlank(candidateSetId, nameof(candidateSetId));
        CandidateId = Guard.NotBlank(candidateId, nameof(candidateId));
        Stage = Guard.NotBlank(stage, nameof(stage));
        Verdict = Guard.NotBlank(verdict, nameof(verdict));
        CriteriaRef = Guard.NotBlank(criteriaRef, nameof(criteriaRef));
        CriteriaDigest = Guard.NotBlank(criteriaDigest, nameof(criteriaDigest));
        DecidedAt = decidedAt;
        Actor = actor;
        Rationale = rationale;
        Confidence = confidence;
        WorkId = workId;
        DedupClusterId = dedupClusterId;
        EvidenceRefs = new System.Collections.ObjectModel.ReadOnlyCollection<string>((evidenceRefs ?? Array.Empty<string>()).ToArray());
        SourceDecisionIds = new System.Collections.ObjectModel.ReadOnlyCollection<string>((sourceDecisionIds ?? Array.Empty<string>()).ToArray());
        SourceSuggestionIds = new System.Collections.ObjectModel.ReadOnlyCollection<string>((sourceSuggestionIds ?? Array.Empty<string>()).ToArray());
        ConflictId = conflictId;
        ResolvedConflictId = resolvedConflictId;
        DecisionKind = decisionKind;
        NonClaims = new System.Collections.ObjectModel.ReadOnlyCollection<string>((nonClaims ?? Array.Empty<string>()).ToArray());
    }

    public string SchemaId => ScreeningSchema.DecisionSchemaId;

    public string SchemaVersion => ScreeningSchema.DecisionSchemaVersion;

    public string DecisionId { get; }

    public string CandidateSetId { get; }

    public string CandidateId { get; }

    public string? WorkId { get; }

    public string? DedupClusterId { get; }

    public string Stage { get; }

    public string Verdict { get; }

    public ScreeningActor? Actor { get; }

    public DateTimeOffset DecidedAt { get; }

    public string? Rationale { get; }

    public double? Confidence { get; }

    public string CriteriaRef { get; }

    public string CriteriaDigest { get; }

    public IReadOnlyList<string> EvidenceRefs { get; }

    public IReadOnlyList<string> SourceDecisionIds { get; }

    public IReadOnlyList<string> SourceSuggestionIds { get; }

    public string? ConflictId { get; }

    public string? ResolvedConflictId { get; }

    public ScreeningDecisionKind DecisionKind { get; }

    public IReadOnlyList<string> NonClaims { get; }

    public bool IsFinal => DecisionKind is ScreeningDecisionKind.Human or ScreeningDecisionKind.Adjudication;
}

public sealed record ScreeningConflict(
    string ConflictId,
    string CandidateSetId,
    string CandidateId,
    string Stage,
    string CriteriaDigest,
    IReadOnlyList<string> SourceDecisionIds,
    bool Resolved,
    string? ResolvedByDecisionId = null,
    string? ResolutionRationale = null,
    DateTimeOffset? ResolvedAt = null);
