using System.Globalization;
using System.Linq;
using System.Text;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;

namespace NexusScholar.CorpusSnapshots;

public static class CorpusSnapshotInvalidationErrorCodes
{
    public const string InvalidInvalidationRecord = "invalid-corpus-snapshot-invalidation";
    public const string DuplicateInvalidationMaterial = "duplicate-corpus-snapshot-invalidation-material";
    public const string NonCanonicalInvalidationMaterial = "non-canonical-corpus-snapshot-invalidation";
    public const string StaleAuthoritySourceBinding = "stale-corpus-snapshot-invalidation-authority-source-binding";
    public const string UnauthorizedInvalidationActor = "unauthorized-corpus-snapshot-invalidation-actor";
    public const string UnsupportedInvalidationKind = "unsupported-corpus-snapshot-invalidated-record-kind";
}

public static class CorpusSnapshotInvalidationConstants
{
    public const string SchemaId = "nexus.corpus.snapshot-invalidation";
    public const string SchemaVersion = "1.0.0";
    public const string InvalidationDecisionKind = "deduplication-decision";
    public const string InvalidationSnapshotKind = "corpus-snapshot";
}

public sealed class CorpusSnapshotInvalidationException : InvalidOperationException
{
    public CorpusSnapshotInvalidationException(string category, string message) : base(message)
    {
        Category = category;
    }

    public string Category { get; }
}

public sealed record CorpusSnapshotInvalidationInvalidatedRecordReference(
    string RecordKind,
    string RecordId,
    ContentDigest RecordDigest);

public sealed record UnverifiedCorpusSnapshotInvalidation(
    string SchemaId,
    string SchemaVersion,
    string InvalidationId,
    string CauseDecisionId,
    ContentDigest CauseDecisionDigest,
    string CauseSnapshotId,
    ContentDigest CauseSnapshotDigest,
    IReadOnlyList<CorpusSnapshotInvalidationInvalidatedRecordReference> InvalidatedRecordReferences,
    string ActorId,
    string ActorRole,
    string AuthoritySourceId,
    string AuthoritySourceKind,
    ContentDigest AuthoritySourceDigest,
    DateTimeOffset InvalidatedAt,
    ContentDigest? RecordDigest = null);

public sealed class VerifiedCorpusSnapshotInvalidation
{
    internal VerifiedCorpusSnapshotInvalidation(
        string invalidationId,
        string causeDecisionId,
        ContentDigest causeDecisionDigest,
        string causeSnapshotId,
        ContentDigest causeSnapshotDigest,
        IReadOnlyList<CorpusSnapshotInvalidationInvalidatedRecordReference> invalidatedRecordReferences,
        string actorId,
        string actorRole,
        string authoritySourceId,
        string authoritySourceKind,
        ContentDigest authoritySourceDigest,
        DateTimeOffset invalidatedAt,
        ContentDigest recordDigest,
        DigestEnvelope recordDigestEnvelope)
    {
        InvalidationId = invalidationId;
        CauseDecisionId = causeDecisionId;
        CauseDecisionDigest = causeDecisionDigest;
        CauseSnapshotId = causeSnapshotId;
        CauseSnapshotDigest = causeSnapshotDigest;
        InvalidatedRecordReferences = invalidatedRecordReferences;
        ActorId = actorId;
        ActorRole = actorRole;
        AuthoritySourceId = authoritySourceId;
        AuthoritySourceKind = authoritySourceKind;
        AuthoritySourceDigest = authoritySourceDigest;
        InvalidatedAt = invalidatedAt;
        RecordDigest = recordDigest;
        RecordDigestEnvelope = recordDigestEnvelope;
    }

    public string InvalidationId { get; }
    public string CauseDecisionId { get; }
    public ContentDigest CauseDecisionDigest { get; }
    public string CauseSnapshotId { get; }
    public ContentDigest CauseSnapshotDigest { get; }
    public IReadOnlyList<CorpusSnapshotInvalidationInvalidatedRecordReference> InvalidatedRecordReferences { get; }
    public string ActorId { get; }
    public string ActorRole { get; }
    public string AuthoritySourceId { get; }
    public string AuthoritySourceKind { get; }
    public ContentDigest AuthoritySourceDigest { get; }
    public DateTimeOffset InvalidatedAt { get; }
    public ContentDigest RecordDigest { get; }
    public DigestEnvelope RecordDigestEnvelope { get; }
}

public static class CorpusSnapshotInvalidation
{
    public static VerifiedCorpusSnapshotInvalidation CreateInvalidationMaterial(
        UnverifiedCorpusSnapshotInvalidation input,
        IClock clock,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityDecision causeDecision,
        VerifiedCorpusSnapshot causeSnapshot,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> knownDecisions,
        IReadOnlyList<VerifiedCorpusSnapshot> knownSnapshots)
    {
        if (input is null)
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, "Invalidation material is required.");
        }

        if (clock is null)
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, "Clock is required.");
        }
        if (policy is null)
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, "Authority policy is required.");
        }

        if (causeDecision is null)
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, "Invalidation cause decision is required.");
        }

        if (causeSnapshot is null)
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, "Invalidation cause snapshot is required.");
        }

        var commandMaterial = input with
        {
            SchemaId = CorpusSnapshotInvalidationConstants.SchemaId,
            SchemaVersion = CorpusSnapshotInvalidationConstants.SchemaVersion,
            InvalidatedAt = clock.UtcNow,
            RecordDigest = null
        };

        var normalized = NormalizeInvalidation(
            commandMaterial,
            policy,
            causeDecision,
            causeSnapshot,
            knownDecisions,
            knownSnapshots,
            requireCanonicalText: true);
        var canonical = BuildInvalidationContent(normalized, canonicalizeCollections: true);
        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotInvalidationConstants.SchemaId,
            CorpusSnapshotInvalidationConstants.SchemaVersion,
            canonical);
        var recordDigest = envelope.ComputeDigest();

        return BuildVerifiedInvalidation(normalized, recordDigest, envelope);
    }

    public static VerifiedCorpusSnapshotInvalidation RehydrateInvalidationMaterial(
        UnverifiedCorpusSnapshotInvalidation input,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityDecision causeDecision,
        VerifiedCorpusSnapshot causeSnapshot,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision> knownDecisions,
        IReadOnlyList<VerifiedCorpusSnapshot> knownSnapshots)
    {
        if (input is null)
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, "Invalidation material is required.");
        }

        if (policy is null)
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, "Authority policy is required.");
        }

        if (causeDecision is null)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Invalidation cause decision is required for rehydration.");
        }

        if (causeSnapshot is null)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Invalidation cause snapshot is required for rehydration.");
        }

        if (knownDecisions is null)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Known decisions are required for rehydration.");
        }

        if (knownSnapshots is null)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Known snapshots are required for rehydration.");
        }

        EnsureKnownSchema(input);
        var normalized = NormalizeInvalidation(
            input,
            policy,
            causeDecision,
            causeSnapshot,
            knownDecisions,
            knownSnapshots,
            requireCanonicalText: true);
        var canonical = BuildInvalidationContent(normalized, canonicalizeCollections: true);
        var provided = BuildInvalidationContent(normalized, canonicalizeCollections: false);
        EnsureCanonicalInput("corpus snapshot invalidation", provided, canonical);

        var expectedDigest = input.RecordDigest;
        if (!expectedDigest.HasValue)
        {
            throw new CorpusSnapshotInvalidationException(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Corpus snapshot invalidation digest is required for persisted material.");
        }

        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            CorpusSnapshotInvalidationConstants.SchemaId,
            CorpusSnapshotInvalidationConstants.SchemaVersion,
            canonical);
        var computed = envelope.ComputeDigest();
        if (computed != expectedDigest.Value)
        {
            throw new CorpusSnapshotInvalidationException(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Corpus snapshot invalidation digest does not match persisted material.");
        }

        return BuildVerifiedInvalidation(normalized, expectedDigest.Value, envelope);
    }

    private static VerifiedCorpusSnapshotInvalidation BuildVerifiedInvalidation(
        NormalizedCorpusSnapshotInvalidation normalized,
        ContentDigest recordDigest,
        DigestEnvelope envelope)
    {
        return new VerifiedCorpusSnapshotInvalidation(
            normalized.InvalidationId,
            normalized.CauseDecisionId,
            normalized.CauseDecisionDigest,
            normalized.CauseSnapshotId,
            normalized.CauseSnapshotDigest,
            normalized.CanonicalInvalidatedRecordReferences,
            normalized.ActorId,
            normalized.ActorRole,
            normalized.AuthoritySourceId,
            normalized.AuthoritySourceKind,
            normalized.AuthoritySourceDigest,
            normalized.InvalidatedAt,
            recordDigest,
            envelope);
    }

    private static NormalizedCorpusSnapshotInvalidation NormalizeInvalidation(
        UnverifiedCorpusSnapshotInvalidation input,
        VerifiedDeduplicationAuthorityPolicy policy,
        VerifiedDeduplicationAuthorityDecision? causeDecision,
        VerifiedCorpusSnapshot? causeSnapshot,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision>? knownDecisions,
        IReadOnlyList<VerifiedCorpusSnapshot>? knownSnapshots,
        bool requireCanonicalText)
    {
        EnsureKnownSchema(input);

        var invalidationId = RequireCanonicalText(input.InvalidationId, nameof(input.InvalidationId), requireCanonicalText);
        var causeDecisionId = RequireCanonicalText(input.CauseDecisionId, nameof(input.CauseDecisionId), requireCanonicalText);
        var causeDecisionDigest = RequireValidDigest(input.CauseDecisionDigest, nameof(input.CauseDecisionDigest));
        var causeSnapshotId = RequireCanonicalText(input.CauseSnapshotId, nameof(input.CauseSnapshotId), requireCanonicalText);
        var causeSnapshotDigest = RequireValidDigest(input.CauseSnapshotDigest, nameof(input.CauseSnapshotDigest));
        var actorId = RequireCanonicalText(input.ActorId, nameof(input.ActorId), requireCanonicalText);
        var actorRole = RequireCanonicalText(input.ActorRole, nameof(input.ActorRole), requireCanonicalText);
        var authoritySourceId = RequireCanonicalText(input.AuthoritySourceId, nameof(input.AuthoritySourceId), requireCanonicalText);
        var authoritySourceKind = RequireCanonicalText(input.AuthoritySourceKind, nameof(input.AuthoritySourceKind), requireCanonicalText);
        var authoritySourceDigest = RequireValidDigest(input.AuthoritySourceDigest, nameof(input.AuthoritySourceDigest));
        var invalidatedAt = RequireUtc(input.InvalidatedAt, nameof(input.InvalidatedAt));

        if (!string.Equals(authoritySourceKind, DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind, StringComparison.Ordinal))
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Invalidation authority source kind must be local-deduplication-authority-policy.");
        }

        if (!string.Equals(authoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
            authoritySourceDigest != policy.PolicyDigest)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.StaleAuthoritySourceBinding,
                "Invalidation authority source binding must exactly match the active local policy id and digest.");
        }

        if (!policy.ContainsAuthorizedActor(actorId, actorRole))
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.UnauthorizedInvalidationActor,
                "Invalidation actor-role pair is not authorized by the active local policy.");
        }

        if (causeDecision is null || causeSnapshot is null)
        {
            if (!string.Equals(causeDecisionId, input.CauseDecisionId, StringComparison.Ordinal) ||
                causeDecisionDigest != input.CauseDecisionDigest ||
                !string.Equals(causeSnapshotId, input.CauseSnapshotId, StringComparison.Ordinal) ||
                causeSnapshotDigest != input.CauseSnapshotDigest)
            {
                throw Invalid(
                    CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                    "Invalidation cause must match the active cause decision and snapshot.");
            }
        }
        else
        {
            if (!string.Equals(causeDecision.AuthoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
                causeDecision.AuthoritySourceDigest != policy.PolicyDigest ||
                !string.Equals(causeDecision.SourceResultId, causeSnapshot.SourceResultId, StringComparison.Ordinal) ||
                causeDecision.SourceResultDigest != causeSnapshot.SourceResultDigest)
            {
                throw Invalid(
                    CorpusSnapshotInvalidationErrorCodes.StaleAuthoritySourceBinding,
                    "Invalidation cause records must be on the active policy and snapshot lineage.");
            }

            if (!string.Equals(causeDecisionId, causeDecision.DecisionId, StringComparison.Ordinal) ||
                causeDecisionDigest != causeDecision.DecisionDigest ||
                !string.Equals(causeSnapshotId, causeSnapshot.SnapshotId, StringComparison.Ordinal) ||
                causeSnapshotDigest != causeSnapshot.RecordDigest)
            {
                throw Invalid(
                    CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                    "Invalidation cause must match the active cause decision and snapshot.");
            }
        }

        var normalizedInvalidatedRecords = ValidateInvalidatedRecordReferences(
            input.InvalidatedRecordReferences,
            causeDecision,
            causeSnapshot,
            policy,
            knownDecisions,
            knownSnapshots,
            requireCanonicalText);

        return new NormalizedCorpusSnapshotInvalidation(
            invalidationId,
            causeDecisionId,
            causeDecisionDigest,
            causeSnapshotId,
            causeSnapshotDigest,
            normalizedInvalidatedRecords,
            actorId,
            actorRole,
            authoritySourceId,
            authoritySourceKind,
            authoritySourceDigest,
            invalidatedAt);
    }

    private static IReadOnlyList<CorpusSnapshotInvalidationInvalidatedRecordReference> ValidateInvalidatedRecordReferences(
        IReadOnlyList<CorpusSnapshotInvalidationInvalidatedRecordReference> references,
        VerifiedDeduplicationAuthorityDecision? causeDecision,
        VerifiedCorpusSnapshot? causeSnapshot,
        VerifiedDeduplicationAuthorityPolicy policy,
        IReadOnlyList<VerifiedDeduplicationAuthorityDecision>? knownDecisions,
        IReadOnlyList<VerifiedCorpusSnapshot>? knownSnapshots,
        bool requireCanonicalText)
    {
        if (references is null)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Invalidated record references are required.");
        }

        if (references.Any(reference => reference is null))
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Invalidated record references cannot contain null entries.");
        }

        if (causeDecision is null || causeSnapshot is null)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Invalidation cause decision and snapshot are required.");
        }

        if (knownDecisions is null || knownSnapshots is null)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Known verified records are required.");
        }

        var knownDecisionLookup = knownDecisions.ToDictionary(
            item => $"{item.DecisionId}\u001f{item.DecisionDigest}",
            item => item,
            StringComparer.Ordinal);
        var knownSnapshotLookup = knownSnapshots.ToDictionary(
            item => $"{item.SnapshotId}\u001f{item.RecordDigest}",
            item => item,
            StringComparer.Ordinal);

        var normalized = references.Select(reference =>
        {
            var recordKind = RequireCanonicalText(reference.RecordKind, nameof(reference.RecordKind), requireCanonicalText);
            if (!string.Equals(recordKind, CorpusSnapshotInvalidationConstants.InvalidationDecisionKind, StringComparison.Ordinal) &&
                !string.Equals(recordKind, CorpusSnapshotInvalidationConstants.InvalidationSnapshotKind, StringComparison.Ordinal))
            {
                throw Invalid(
                    CorpusSnapshotInvalidationErrorCodes.UnsupportedInvalidationKind,
                    "Invalidated record kinds are restricted to deduplication-decision and corpus-snapshot.");
            }

            var recordId = RequireCanonicalText(reference.RecordId, nameof(reference.RecordId), requireCanonicalText);
            var recordDigest = RequireValidDigest(reference.RecordDigest, nameof(reference.RecordDigest));
            var key = $"{recordId}\u001f{recordDigest}";

            if (string.Equals(recordKind, CorpusSnapshotInvalidationConstants.InvalidationDecisionKind, StringComparison.Ordinal))
            {
                if (!knownDecisionLookup.TryGetValue(key, out var referencedDecision))
                {
                    throw Invalid(
                        CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                        "Invalidated records must reference known verified records.");
                }

                if (!string.Equals(referencedDecision.AuthoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
                    referencedDecision.AuthoritySourceDigest != policy.PolicyDigest ||
                    !string.Equals(referencedDecision.SourceResultId, causeSnapshot.SourceResultId, StringComparison.Ordinal) ||
                    referencedDecision.SourceResultDigest != causeSnapshot.SourceResultDigest ||
                    !string.Equals(referencedDecision.SourceSnapshotId, causeSnapshot.SupersedesSnapshotId, StringComparison.Ordinal) ||
                    referencedDecision.SourceSnapshotRecordDigest != causeSnapshot.SupersedesSnapshotRecordDigest)
                {
                    throw Invalid(
                        CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                        "Invalidated records must be on the active local policy lineage.");
                }

                if (string.Equals(recordId, causeDecision.DecisionId, StringComparison.Ordinal) &&
                    recordDigest == causeDecision.DecisionDigest)
                {
                    throw Invalid(
                        CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                        "Invalidation cannot self-reference its cause decision.");
                }
            }
            else
            {
                if (!knownSnapshotLookup.TryGetValue(key, out var referencedSnapshot))
                {
                    throw Invalid(
                        CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                        "Invalidated records must reference known verified records.");
                }

                if (!string.Equals(referencedSnapshot.AuthoritySourceId, policy.PolicyId, StringComparison.Ordinal) ||
                    referencedSnapshot.AuthoritySourceDigest != policy.PolicyDigest ||
                    !string.Equals(referencedSnapshot.SourceResultId, causeSnapshot.SourceResultId, StringComparison.Ordinal) ||
                    referencedSnapshot.SourceResultDigest != causeSnapshot.SourceResultDigest ||
                    !string.Equals(referencedSnapshot.SnapshotId, causeSnapshot.SupersedesSnapshotId, StringComparison.Ordinal) ||
                    referencedSnapshot.RecordDigest != causeSnapshot.SupersedesSnapshotRecordDigest)
                {
                    throw Invalid(
                        CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                        "Invalidated records must be on the active local policy lineage.");
                }

                if (string.Equals(recordId, causeSnapshot.SnapshotId, StringComparison.Ordinal) &&
                    recordDigest == causeSnapshot.RecordDigest)
                {
                    throw Invalid(
                        CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                        "Invalidation cannot self-reference its cause snapshot.");
                }
            }

            return new CorpusSnapshotInvalidationInvalidatedRecordReference(
                recordKind,
                recordId,
                recordDigest);
        }).ToArray();

        if (normalized.Length == 0)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "At least one invalidated record reference is required.");
        }

        if (normalized.Select(item => $"{item.RecordKind}\u001f{item.RecordId}\u001f{item.RecordDigest}")
            .Distinct(StringComparer.Ordinal).Count() != normalized.Length)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.DuplicateInvalidationMaterial,
                "Invalidated record references must be unique.");
        }

        return Array.AsReadOnly(normalized);
    }

    private static CanonicalJsonObject BuildInvalidationContent(
        NormalizedCorpusSnapshotInvalidation normalized,
        bool canonicalizeCollections)
    {
        var invalidatedRecords = canonicalizeCollections
            ? normalized.InvalidatedRecordReferences
                .OrderBy(item => item.RecordKind, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                .ThenBy(item => item.RecordDigest.ToString(), StringComparer.Ordinal)
                .ToArray()
            : normalized.InvalidatedRecordReferences.ToArray();

        return new CanonicalJsonObject()
            .Add("invalidation_id", normalized.InvalidationId)
            .Add("schema_id", CorpusSnapshotInvalidationConstants.SchemaId)
            .Add("schema_version", CorpusSnapshotInvalidationConstants.SchemaVersion)
            .Add("cause_decision_id", normalized.CauseDecisionId)
            .Add("cause_decision_digest", normalized.CauseDecisionDigest.ToString())
            .Add("cause_snapshot_id", normalized.CauseSnapshotId)
            .Add("cause_snapshot_digest", normalized.CauseSnapshotDigest.ToString())
            .Add("invalidated_records", CanonicalJsonValue.Array(
                invalidatedRecords.Select(reference => (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("record_kind", reference.RecordKind)
                    .Add("record_id", reference.RecordId)
                    .Add("record_digest", reference.RecordDigest.ToString()))
                .ToArray()))
            .Add("actor_id", normalized.ActorId)
            .Add("actor_role", normalized.ActorRole)
            .Add("authority_source_id", normalized.AuthoritySourceId)
            .Add("authority_source_kind", normalized.AuthoritySourceKind)
            .Add("authority_source_digest", normalized.AuthoritySourceDigest.ToString())
            .AddTimestamp("invalidated_at", normalized.InvalidatedAt);
    }

    private static void EnsureKnownSchema(UnverifiedCorpusSnapshotInvalidation input)
    {
        if (!string.Equals(input.SchemaId, CorpusSnapshotInvalidationConstants.SchemaId, StringComparison.Ordinal) ||
            !string.Equals(input.SchemaVersion, CorpusSnapshotInvalidationConstants.SchemaVersion, StringComparison.Ordinal))
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                "Invalidation schema id or version is invalid.");
        }
    }

    private static void EnsureCanonicalInput(string label, CanonicalJsonValue provided, CanonicalJsonValue canonical)
    {
        if (!string.Equals(Canonicalize(provided), Canonicalize(canonical), StringComparison.Ordinal))
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.NonCanonicalInvalidationMaterial,
                $"{label} is not in canonical collection order.");
        }
    }

    private static string Canonicalize(CanonicalJsonValue value) => CanonicalJsonSerializer.Serialize(value);

    private static string RequireCanonicalText(string value, string name, bool enforceNormalized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, $"{name} is required.");
        }

        if (enforceNormalized && !value.IsNormalized(NormalizationForm.FormC))
        {
            throw Invalid(CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord, $"{name} must be NFC-normalized.");
        }

        return value;
    }

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name)
    {
        if (!CanonicalTimestamp.IsCanonicalUtc(value, rejectDefault: true))
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                $"{name} must be canonical UTC.");
        }

        return value;
    }

    private static ContentDigest RequireValidDigest(ContentDigest digest, string name)
    {
        if (!digest.IsValid)
        {
            throw Invalid(
                CorpusSnapshotInvalidationErrorCodes.InvalidInvalidationRecord,
                $"{name} must be a valid lowercase SHA-256 content digest.");
        }

        return digest;
    }

    private static CorpusSnapshotInvalidationException Invalid(string category, string message) =>
        new CorpusSnapshotInvalidationException(category, message);

    private sealed class NormalizedCorpusSnapshotInvalidation(
        string invalidationId,
        string causeDecisionId,
        ContentDigest causeDecisionDigest,
        string causeSnapshotId,
        ContentDigest causeSnapshotDigest,
        IReadOnlyList<CorpusSnapshotInvalidationInvalidatedRecordReference> invalidatedRecordReferences,
        string actorId,
        string actorRole,
        string authoritySourceId,
        string authoritySourceKind,
        ContentDigest authoritySourceDigest,
        DateTimeOffset invalidatedAt)
    {
        public string InvalidationId { get; } = invalidationId;
        public string CauseDecisionId { get; } = causeDecisionId;
        public ContentDigest CauseDecisionDigest { get; } = causeDecisionDigest;
        public string CauseSnapshotId { get; } = causeSnapshotId;
        public ContentDigest CauseSnapshotDigest { get; } = causeSnapshotDigest;
        public IReadOnlyList<CorpusSnapshotInvalidationInvalidatedRecordReference> InvalidatedRecordReferences { get; } =
            Array.AsReadOnly(invalidatedRecordReferences.ToArray());
        public string ActorId { get; } = actorId;
        public string ActorRole { get; } = actorRole;
        public string AuthoritySourceId { get; } = authoritySourceId;
        public string AuthoritySourceKind { get; } = authoritySourceKind;
        public ContentDigest AuthoritySourceDigest { get; } = authoritySourceDigest;
        public DateTimeOffset InvalidatedAt { get; } = invalidatedAt;

        public IReadOnlyList<CorpusSnapshotInvalidationInvalidatedRecordReference> CanonicalInvalidatedRecordReferences
        {
            get
            {
                return Array.AsReadOnly(InvalidatedRecordReferences
                    .OrderBy(item => item.RecordKind, StringComparer.Ordinal)
                    .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                    .ThenBy(item => item.RecordDigest.ToString(), StringComparer.Ordinal)
                    .ToArray());
            }
        }
    }
}
