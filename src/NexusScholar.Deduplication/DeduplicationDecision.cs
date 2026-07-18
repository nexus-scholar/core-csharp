using System.Globalization;
using System.Linq;
using System.Text;
using NexusScholar.Kernel;

namespace NexusScholar.Deduplication;

public static class DeduplicationDecisionErrorCodes
{
    public const string InvalidDecision = "invalid-deduplication-decision";
    public const string DuplicateAuthorityMaterial = "duplicate-deduplication-authority-material";
    public const string DuplicateDecisionMaterial = "duplicate-deduplication-decision-material";
    public const string NonCanonicalAuthorityMaterial = "non-canonical-deduplication-decision";
    public const string StaleAuthoritySourceBinding = "stale-deduplication-authority-source-binding";
    public const string UnauthorizedDecisionActor = "unauthorized-deduplication-decision-actor";
    public const string UnsupportedAction = "unsupported-deduplication-decision-action";
    public const string UnsupportedReasonCode = "unsupported-deduplication-decision-reason";
    public const string UnsupportedInvalidationKind = "unsupported-deduplication-invalidation-kind";
}

public static class DeduplicationDecisionConstants
{
    public const string SchemaId = "nexus.deduplication.decision";
    public const string SchemaVersion = "1.0.0";
    public const string InvalidationDecisionKind = "deduplication-decision";
    public const string InvalidationSnapshotKind = "corpus-snapshot";
}

public sealed record DeduplicationAuthorityDecisionEvidenceReference(
    string Kind,
    string EvidenceId,
    string DigestScope,
    ContentDigest Digest);

public sealed record DeduplicationAuthorityDecisionInvalidationEffect(
    string RecordKind,
    string RecordId,
    ContentDigest RecordDigest);

public sealed record UnverifiedDeduplicationAuthorityDecision(
    string SchemaId,
    string SchemaVersion,
    string DecisionId,
    string ActionType,
    string PolicyId,
    string PolicyVersion,
    string TargetKind,
    string TargetId,
    ContentDigest TargetContentDigest,
    string SourceResultId,
    ContentDigest SourceResultDigest,
    string? SourceSnapshotId,
    ContentDigest? SourceSnapshotRecordDigest,
    IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> EvidenceReferences,
    string ActorId,
    string ActorRole,
    string AuthoritySourceId,
    string AuthoritySourceKind,
    ContentDigest AuthoritySourceDigest,
    string? Rationale,
    string ReasonCode,
    DateTimeOffset DecidedAt,
    string? SupersedesDecisionId,
    IReadOnlyList<DeduplicationAuthorityDecisionInvalidationEffect> InvalidationEffects,
    ContentDigest? DecisionDigest = null);

public sealed class VerifiedDeduplicationAuthorityDecision
{
    internal VerifiedDeduplicationAuthorityDecision(
        string decisionId,
        string actionType,
        string policyId,
        string policyVersion,
        string targetKind,
        string targetId,
        ContentDigest targetContentDigest,
        string sourceResultId,
        ContentDigest sourceResultDigest,
        string? sourceSnapshotId,
        ContentDigest? sourceSnapshotRecordDigest,
        IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> evidenceReferences,
        string actorId,
        string actorRole,
        string authoritySourceId,
        string authoritySourceKind,
        ContentDigest authoritySourceDigest,
        string? rationale,
        string reasonCode,
        DateTimeOffset decidedAt,
        string? supersedesDecisionId,
        IReadOnlyList<DeduplicationAuthorityDecisionInvalidationEffect> invalidationEffects,
        ContentDigest decisionDigest,
        DigestEnvelope decisionDigestEnvelope)
    {
        DecisionId = decisionId;
        ActionType = actionType;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        TargetKind = targetKind;
        TargetId = targetId;
        TargetContentDigest = targetContentDigest;
        SourceResultId = sourceResultId;
        SourceResultDigest = sourceResultDigest;
        SourceSnapshotId = sourceSnapshotId;
        SourceSnapshotRecordDigest = sourceSnapshotRecordDigest;
        EvidenceReferences = evidenceReferences;
        ActorId = actorId;
        ActorRole = actorRole;
        AuthoritySourceId = authoritySourceId;
        AuthoritySourceKind = authoritySourceKind;
        AuthoritySourceDigest = authoritySourceDigest;
        Rationale = rationale;
        ReasonCode = reasonCode;
        DecidedAt = decidedAt;
        SupersedesDecisionId = supersedesDecisionId;
        InvalidationEffects = invalidationEffects;
        DecisionDigest = decisionDigest;
        DecisionDigestEnvelope = decisionDigestEnvelope;
    }

    public string DecisionId { get; }
    public string ActionType { get; }
    public string PolicyId { get; }
    public string PolicyVersion { get; }
    public string TargetKind { get; }
    public string TargetId { get; }
    public ContentDigest TargetContentDigest { get; }
    public string SourceResultId { get; }
    public ContentDigest SourceResultDigest { get; }
    public string? SourceSnapshotId { get; }
    public ContentDigest? SourceSnapshotRecordDigest { get; }
    public IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> EvidenceReferences { get; }
    public string ActorId { get; }
    public string ActorRole { get; }
    public string AuthoritySourceId { get; }
    public string AuthoritySourceKind { get; }
    public ContentDigest AuthoritySourceDigest { get; }
    public string? Rationale { get; }
    public string ReasonCode { get; }
    public DateTimeOffset DecidedAt { get; }
    public string? SupersedesDecisionId { get; }
    public IReadOnlyList<DeduplicationAuthorityDecisionInvalidationEffect> InvalidationEffects { get; }
    public ContentDigest DecisionDigest { get; }
    public DigestEnvelope DecisionDigestEnvelope { get; }
}

public static class DeduplicationDecision
{

    public static VerifiedDeduplicationAuthorityDecision CreateDecisionMaterial(
        UnverifiedDeduplicationAuthorityDecision input,
        IClock clock,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityReviewTargetDigest target)
    {
        ArgumentNullException.ThrowIfNull(clock);
        var commandMaterial = input with
        {
            SchemaId = DeduplicationDecisionConstants.SchemaId,
            SchemaVersion = DeduplicationDecisionConstants.SchemaVersion,
            DecidedAt = clock.UtcNow,
            DecisionDigest = null
        };
        var normalized = NormalizeDecision(commandMaterial, policy, sourceResult, target, requireCanonicalText: false);
        var canonical = BuildDecisionContent(normalized, canonicalizeCollections: true);
        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            DeduplicationDecisionConstants.SchemaId,
            DeduplicationDecisionConstants.SchemaVersion,
            canonical);
        var decisionDigest = envelope.ComputeDigest();

        return BuildVerifiedDecision(normalized, decisionDigest, envelope);
    }

    public static VerifiedDeduplicationAuthorityDecision RehydrateDecisionMaterial(
        UnverifiedDeduplicationAuthorityDecision input,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityReviewTargetDigest target)
    {
        EnsureKnownSchema(input);
        var normalized = NormalizeDecision(input, policy, sourceResult, target, requireCanonicalText: true);
        var canonical = BuildDecisionContent(normalized, canonicalizeCollections: true);
        var provided = BuildDecisionContent(normalized, canonicalizeCollections: false);
        EnsureCanonicalInput("decision", provided, canonical);

        var expectedDigest = input.DecisionDigest;
        if (!expectedDigest.HasValue)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "Decision digest is required for persisted authority material.");
        }

        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            DeduplicationDecisionConstants.SchemaId,
            DeduplicationDecisionConstants.SchemaVersion,
            canonical);
        var computed = envelope.ComputeDigest();
        if (computed != expectedDigest.Value)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "Decision digest does not match persisted authority material.");
        }

        return BuildVerifiedDecision(normalized, expectedDigest.Value, envelope);
    }

    private static VerifiedDeduplicationAuthorityDecision BuildVerifiedDecision(
        NormalizedDeduplicationAuthorityDecision normalized,
        ContentDigest decisionDigest,
        DigestEnvelope envelope)
    {
        return new VerifiedDeduplicationAuthorityDecision(
            normalized.DecisionId,
            normalized.ActionType,
            normalized.PolicyId,
            normalized.PolicyVersion,
            normalized.TargetKind,
            normalized.TargetId,
            normalized.TargetContentDigest,
            normalized.SourceResultId,
            normalized.SourceResultDigest,
            normalized.SourceSnapshotId,
            normalized.SourceSnapshotRecordDigest,
            normalized.CanonicalEvidenceReferences,
            normalized.ActorId,
            normalized.ActorRole,
            normalized.AuthoritySourceId,
            normalized.AuthoritySourceKind,
            normalized.AuthoritySourceDigest,
            normalized.Rationale,
            normalized.ReasonCode,
            normalized.DecidedAt,
            normalized.SupersedesDecisionId,
            normalized.CanonicalInvalidationEffects,
            decisionDigest,
            envelope);
    }

    private static NormalizedDeduplicationAuthorityDecision NormalizeDecision(
        UnverifiedDeduplicationAuthorityDecision input,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityResultDigest sourceResult,
        VerifiedDeduplicationAuthorityReviewTargetDigest target,
        bool requireCanonicalText)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureKnownSchema(input);

        var decisionId = RequireCanonicalText(input.DecisionId, nameof(input.DecisionId), requireCanonicalText);
        var actionType = RequireCanonicalText(input.ActionType, nameof(input.ActionType), requireCanonicalText);
        var policyId = RequireCanonicalText(input.PolicyId, nameof(input.PolicyId), requireCanonicalText);
        var policyVersion = RequireCanonicalText(input.PolicyVersion, nameof(input.PolicyVersion), requireCanonicalText);
        var targetKind = RequireCanonicalText(input.TargetKind, nameof(input.TargetKind), requireCanonicalText);
        var targetId = RequireCanonicalText(input.TargetId, nameof(input.TargetId), requireCanonicalText);
        var targetContentDigest = input.TargetContentDigest;
        var sourceResultId = RequireCanonicalText(input.SourceResultId, nameof(input.SourceResultId), requireCanonicalText);
        var sourceResultDigest = input.SourceResultDigest;
        var actorId = RequireCanonicalText(input.ActorId, nameof(input.ActorId), requireCanonicalText);
        var actorRole = RequireCanonicalText(input.ActorRole, nameof(input.ActorRole), requireCanonicalText);
        var authoritySourceId = RequireCanonicalText(input.AuthoritySourceId, nameof(input.AuthoritySourceId), requireCanonicalText);
        var authoritySourceKind = RequireCanonicalText(input.AuthoritySourceKind, nameof(input.AuthoritySourceKind), requireCanonicalText);
        var authoritySourceDigest = input.AuthoritySourceDigest;
        var decidedAt = RequireUtc(input.DecidedAt, nameof(input.DecidedAt));
        var reasonCode = RequireCanonicalText(input.ReasonCode, nameof(input.ReasonCode), requireCanonicalText);
        var supersedesDecisionId = input.SupersedesDecisionId;

        if (string.IsNullOrWhiteSpace(supersedesDecisionId))
        {
            supersedesDecisionId = null;
        }
        else
        {
            supersedesDecisionId = RequireCanonicalText(supersedesDecisionId, nameof(input.SupersedesDecisionId), requireCanonicalText);
        }

        if (string.Equals(supersedesDecisionId, decisionId, StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "A decision cannot supersede itself.");
        }

        if (!string.Equals(policyId, policy.PolicyId, StringComparison.Ordinal) ||
            !string.Equals(policyVersion, policy.PolicyVersion, StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "Decision policy binding does not match the bound authority policy.");
        }

        if (!string.Equals(authoritySourceKind, DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "Decision authority source kind must be local-deduplication-authority-policy.");
        }

        if (!string.Equals(authoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
            authoritySourceDigest != policy.PolicyDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.StaleAuthoritySourceBinding,
                "Decision authority source binding does not match the active policy.");
        }

        if (!policy.ContainsAuthorizedActor(actorId, actorRole))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.UnauthorizedDecisionActor,
                "Decision actor-role pair is not authorized by the bound policy.");
        }

        if (!DeduplicationAuthorityPolicyConstants.ClosedActions.Contains(actionType, StringComparer.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.UnsupportedAction,
                $"Unsupported deduplication decision action '{actionType}'.");
        }

        var policyReasons = policy.ReasonCodesForAction(actionType);
        if (!policyReasons.Contains(reasonCode, StringComparer.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.UnsupportedReasonCode,
                $"Reason code '{reasonCode}' is not defined for action '{actionType}'.");
        }

        if (policy.RequiresRationale && string.IsNullOrWhiteSpace(input.Rationale))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "A rationale is required by policy.");
        }

        var rationale = string.IsNullOrWhiteSpace(input.Rationale) ? null : RequireCanonicalText(input.Rationale, nameof(input.Rationale), requireCanonicalText);

        if (!string.Equals(targetKind, target.TargetKind, StringComparison.Ordinal) ||
            !string.Equals(targetId, target.TargetId, StringComparison.Ordinal) ||
            targetContentDigest != target.TargetDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "Decision target binding does not match verified target.");
        }

        if (!string.Equals(sourceResultId, sourceResult.Result.ResultId, StringComparison.Ordinal) ||
            sourceResultDigest != sourceResult.ResultDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.StaleAuthoritySourceBinding,
                "Decision source result binding does not match verified source result.");
        }

        var sourceSnapshotId = input.SourceSnapshotId;
        var sourceSnapshotRecordDigest = input.SourceSnapshotRecordDigest;
        var hasSourceSnapshotId = !string.IsNullOrWhiteSpace(sourceSnapshotId);
        var hasSourceSnapshotDigest = sourceSnapshotRecordDigest is { IsValid: true };
        if (hasSourceSnapshotId != hasSourceSnapshotDigest)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "Decision source snapshot id and digest must be provided together.");
        }

        if (hasSourceSnapshotId)
        {
            sourceSnapshotId = RequireCanonicalText(sourceSnapshotId!, nameof(input.SourceSnapshotId), requireCanonicalText);
        }

        if (supersedesDecisionId is not null && !hasSourceSnapshotId)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "A superseding decision must bind the current source snapshot id and record digest.");
        }

        var evidenceReferences = ValidateEvidenceReferences(input.EvidenceReferences, target.Evidence, requireCanonicalText);
        var invalidationEffects = ValidateInvalidationEffects(input.InvalidationEffects, requireCanonicalText);

        return new NormalizedDeduplicationAuthorityDecision(
            decisionId,
            actionType,
            policyId,
            policyVersion,
            targetKind,
            targetId,
            targetContentDigest,
            sourceResultId,
            sourceResultDigest,
            sourceSnapshotId,
            sourceSnapshotRecordDigest,
            evidenceReferences,
            actorId,
            actorRole,
            authoritySourceId,
            authoritySourceKind,
            authoritySourceDigest,
            rationale,
            reasonCode,
            decidedAt,
            supersedesDecisionId,
            invalidationEffects);
    }

    private static IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> ValidateEvidenceReferences(
        IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> references,
        IReadOnlyList<DedupEvidence> resultEvidence,
        bool requireCanonicalText)
    {
        if (references is null)
        {
            throw new DeduplicationAuthorityException(DeduplicationDecisionErrorCodes.InvalidDecision, "Decision evidence references are required.");
        }
        if (references.Any(reference => reference is null))
        {
            throw new DeduplicationAuthorityException(DeduplicationDecisionErrorCodes.InvalidDecision, "Decision evidence references cannot contain null entries.");
        }

        var resultEvidenceById = resultEvidence.ToDictionary(item => item.EvidenceId, StringComparer.Ordinal);
        var normalized = references.Select(reference =>
        {
            var kind = RequireCanonicalText(reference.Kind, nameof(reference.Kind), requireCanonicalText);
            var evidenceId = RequireCanonicalText(reference.EvidenceId, nameof(reference.EvidenceId), requireCanonicalText);
            var digestScope = RequireCanonicalText(reference.DigestScope, nameof(reference.DigestScope), requireCanonicalText);
            if (!string.Equals(digestScope, DigestScope.CanonicalJsonRecord.ToString(), StringComparison.Ordinal))
            {
                throw new DeduplicationAuthorityException(
                    DeduplicationDecisionErrorCodes.InvalidDecision,
                    "Decision evidence references must use canonical-json-record digest scope.");
            }

            if (!resultEvidenceById.TryGetValue(evidenceId, out var sourceEvidence))
            {
                throw new DeduplicationAuthorityException(
                    DeduplicationDecisionErrorCodes.InvalidDecision,
                    $"Decision evidence reference '{evidenceId}' is not part of the source result.");
            }

            var sourceDigest = DeduplicationAuthorityDigests.CreateEvidenceDigestMaterial(sourceEvidence).EvidenceDigest;
            if (sourceDigest != reference.Digest)
            {
                throw new DeduplicationAuthorityException(
                    DeduplicationDecisionErrorCodes.InvalidDecision,
                    $"Decision evidence reference '{evidenceId}' digest does not match source evidence.");
            }

            if (!string.Equals(kind, sourceEvidence.Kind.ToString(), StringComparison.Ordinal))
            {
                throw new DeduplicationAuthorityException(
                    DeduplicationDecisionErrorCodes.InvalidDecision,
                    $"Decision evidence reference '{evidenceId}' kind does not match source evidence.");
            }

            return new DeduplicationAuthorityDecisionEvidenceReference(
                kind,
                evidenceId,
                digestScope,
                reference.Digest);
        }).ToArray();

        if (normalized.Select(item => string.Join('\u001f', item.Kind, item.EvidenceId, item.DigestScope, item.Digest.ToString()))
            .Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.DuplicateDecisionMaterial,
                "Decision evidence references must not contain duplicates.");
        }

        if (normalized.Length != resultEvidence.Count ||
            !normalized.Select(item => item.EvidenceId).ToHashSet(StringComparer.Ordinal)
                .SetEquals(resultEvidence.Select(item => item.EvidenceId)))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "Decision evidence references must bind the exact verified target evidence set.");
        }

        return normalized;
    }

    private static IReadOnlyList<DeduplicationAuthorityDecisionInvalidationEffect> ValidateInvalidationEffects(
        IReadOnlyList<DeduplicationAuthorityDecisionInvalidationEffect> effects,
        bool requireCanonicalText)
    {
        if (effects is null)
        {
            throw new DeduplicationAuthorityException(DeduplicationDecisionErrorCodes.InvalidDecision, "Decision invalidation effects are required.");
        }
        if (effects.Any(effect => effect is null))
        {
            throw new DeduplicationAuthorityException(DeduplicationDecisionErrorCodes.InvalidDecision, "Decision invalidation effects cannot contain null entries.");
        }

        var normalized = effects.Select(effect =>
        {
            var recordKind = RequireCanonicalText(effect.RecordKind, nameof(effect.RecordKind), requireCanonicalText);
            if (!string.Equals(recordKind, DeduplicationDecisionConstants.InvalidationDecisionKind, StringComparison.Ordinal) &&
                !string.Equals(recordKind, DeduplicationDecisionConstants.InvalidationSnapshotKind, StringComparison.Ordinal))
            {
                throw new DeduplicationAuthorityException(
                    DeduplicationDecisionErrorCodes.UnsupportedInvalidationKind,
                    "Decision invalidation kinds are restricted to deduplication-decision and corpus-snapshot.");
            }

            return new DeduplicationAuthorityDecisionInvalidationEffect(
                recordKind,
                RequireCanonicalText(effect.RecordId, nameof(effect.RecordId), requireCanonicalText),
                RequireValidDigest(effect.RecordDigest, nameof(effect.RecordDigest)));
        }).ToArray();

        if (normalized.Select(item => $"{item.RecordKind}\u001f{item.RecordId}\u001f{item.RecordDigest}")
            .Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.DuplicateAuthorityMaterial,
                "Decision invalidation effects must be unique.");
        }

        return Array.AsReadOnly(normalized);
    }

    private static CanonicalJsonObject BuildDecisionContent(NormalizedDeduplicationAuthorityDecision normalized, bool canonicalizeCollections)
    {
        var evidenceReferences = canonicalizeCollections
            ? normalized.EvidenceReferences
                .OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
                .ThenBy(item => item.DigestScope, StringComparer.Ordinal)
                .ThenBy(item => item.Digest.ToString(), StringComparer.Ordinal)
                .ToArray()
            : normalized.EvidenceReferences.ToArray();

        var invalidationEffects = canonicalizeCollections
            ? normalized.InvalidationEffects
                .OrderBy(item => item.RecordKind, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                .ThenBy(item => item.RecordDigest.ToString(), StringComparer.Ordinal)
                .ToArray()
            : normalized.InvalidationEffects.ToArray();

        var builder = new CanonicalJsonObject()
            .Add("decision_id", normalized.DecisionId)
            .Add("schema_id", DeduplicationDecisionConstants.SchemaId)
            .Add("schema_version", DeduplicationDecisionConstants.SchemaVersion)
            .Add("action_type", normalized.ActionType)
            .Add("policy_id", normalized.PolicyId)
            .Add("policy_version", normalized.PolicyVersion)
            .Add("target_kind", normalized.TargetKind)
            .Add("target_id", normalized.TargetId)
            .Add("target_content_digest", normalized.TargetContentDigest.ToString())
            .Add("source_result_id", normalized.SourceResultId)
            .Add("source_result_digest", normalized.SourceResultDigest.ToString())
            .Add("actor_id", normalized.ActorId)
            .Add("actor_role", normalized.ActorRole)
            .Add("authority_source_id", normalized.AuthoritySourceId)
            .Add("authority_source_kind", normalized.AuthoritySourceKind)
            .Add("authority_source_digest", normalized.AuthoritySourceDigest.ToString())
            .Add("reason_code", normalized.ReasonCode)
            .AddTimestamp("decided_at", normalized.DecidedAt)
            .Add("evidence_references", CanonicalJsonValue.Array(
                evidenceReferences.Select(reference => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("kind", reference.Kind)
                    .Add("evidence_id", reference.EvidenceId)
                    .Add("digest_scope", reference.DigestScope)
                    .Add("digest", reference.Digest.ToString()))
                .ToArray()));

        if (normalized.SourceSnapshotId is not null)
        {
            builder = builder
                .Add("source_snapshot_id", normalized.SourceSnapshotId)
                .Add("source_snapshot_record_digest", normalized.SourceSnapshotRecordDigest!.Value.ToString());
        }

        if (normalized.Rationale is not null)
        {
            builder = builder.Add("rationale", normalized.Rationale);
        }

        if (normalized.SupersedesDecisionId is not null)
        {
            builder = builder.Add("supersedes_decision_id", normalized.SupersedesDecisionId);
        }

        return builder.Add("invalidation_effects", CanonicalJsonValue.Array(
            invalidationEffects.Select(effect => (CanonicalJsonValue)new CanonicalJsonObject()
                .Add("record_kind", effect.RecordKind)
                .Add("record_id", effect.RecordId)
                .Add("record_digest", effect.RecordDigest.ToString()))
            .ToArray()));
    }

    private static void EnsureCanonicalInput(string label, CanonicalJsonValue provided, CanonicalJsonValue canonical)
    {
        if (!string.Equals(Canonicalize(provided), Canonicalize(canonical), StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.NonCanonicalAuthorityMaterial,
                $"{label} authority material is not in canonical collection order.");
        }
    }

    private static string Canonicalize(CanonicalJsonValue value) => CanonicalJsonSerializer.Serialize(value);

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name)
    {
        if (!CanonicalTimestamp.IsCanonicalUtc(value, rejectDefault: true))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                $"{name} must be canonical UTC.");
        }

        return value;
    }

    private static void EnsureKnownSchema(UnverifiedDeduplicationAuthorityDecision input)
    {
        if (!string.Equals(input.SchemaId, DeduplicationDecisionConstants.SchemaId, StringComparison.Ordinal) ||
            !string.Equals(input.SchemaVersion, DeduplicationDecisionConstants.SchemaVersion, StringComparison.Ordinal))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                "Decision schema id or version is invalid.");
        }
    }

    private static string RequireCanonicalText(string value, string name, bool enforceNormalization)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DeduplicationAuthorityException(DeduplicationDecisionErrorCodes.InvalidDecision, $"{name} is required.");
        }

        var canonical = value;
        if (enforceNormalization && !canonical.IsNormalized(NormalizationForm.FormC))
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                $"{name} must be NFC-normalized.");
        }

        return canonical;
    }

    private static ContentDigest RequireValidDigest(ContentDigest digest, string name)
    {
        if (!digest.IsValid)
        {
            throw new DeduplicationAuthorityException(
                DeduplicationDecisionErrorCodes.InvalidDecision,
                $"{name} must be a valid content digest.");
        }

        return digest;
    }

    private sealed class NormalizedDeduplicationAuthorityDecision(
        string decisionId,
        string actionType,
        string policyId,
        string policyVersion,
        string targetKind,
        string targetId,
        ContentDigest targetContentDigest,
        string sourceResultId,
        ContentDigest sourceResultDigest,
        string? sourceSnapshotId,
        ContentDigest? sourceSnapshotRecordDigest,
        IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> evidenceReferences,
        string actorId,
        string actorRole,
        string authoritySourceId,
        string authoritySourceKind,
        ContentDigest authoritySourceDigest,
        string? rationale,
        string reasonCode,
        DateTimeOffset decidedAt,
        string? supersedesDecisionId,
        IReadOnlyList<DeduplicationAuthorityDecisionInvalidationEffect> invalidationEffects)
    {
        public string DecisionId { get; } = decisionId;
        public string ActionType { get; } = actionType;
        public string PolicyId { get; } = policyId;
        public string PolicyVersion { get; } = policyVersion;
        public string TargetKind { get; } = targetKind;
        public string TargetId { get; } = targetId;
        public ContentDigest TargetContentDigest { get; } = targetContentDigest;
        public string SourceResultId { get; } = sourceResultId;
        public ContentDigest SourceResultDigest { get; } = sourceResultDigest;
        public string? SourceSnapshotId { get; } = sourceSnapshotId;
        public ContentDigest? SourceSnapshotRecordDigest { get; } = sourceSnapshotRecordDigest;
        public IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> EvidenceReferences { get; } = Array.AsReadOnly(evidenceReferences.ToArray());
        public string ActorId { get; } = actorId;
        public string ActorRole { get; } = actorRole;
        public string AuthoritySourceId { get; } = authoritySourceId;
        public string AuthoritySourceKind { get; } = authoritySourceKind;
        public ContentDigest AuthoritySourceDigest { get; } = authoritySourceDigest;
        public string? Rationale { get; } = rationale;
        public string ReasonCode { get; } = reasonCode;
        public DateTimeOffset DecidedAt { get; } = decidedAt;
        public string? SupersedesDecisionId { get; } = supersedesDecisionId;
        public IReadOnlyList<DeduplicationAuthorityDecisionInvalidationEffect> InvalidationEffects { get; } = Array.AsReadOnly(invalidationEffects.ToArray());

        public IReadOnlyList<DeduplicationAuthorityDecisionEvidenceReference> CanonicalEvidenceReferences
        {
            get
            {
                return Array.AsReadOnly(EvidenceReferences
                    .OrderBy(item => item.Kind, StringComparer.Ordinal)
                    .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
                    .ThenBy(item => item.DigestScope, StringComparer.Ordinal)
                    .ThenBy(item => item.Digest.ToString(), StringComparer.Ordinal)
                    .ToArray());
            }
        }

        public IReadOnlyList<DeduplicationAuthorityDecisionInvalidationEffect> CanonicalInvalidationEffects
        {
            get
            {
                return Array.AsReadOnly(InvalidationEffects
                    .OrderBy(item => item.RecordKind, StringComparer.Ordinal)
                    .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                    .ThenBy(item => item.RecordDigest.ToString(), StringComparer.Ordinal)
                    .ToArray());
            }
        }
    }
}
