using NexusScholar.FullText;

namespace NexusScholar.Extraction;

public sealed class ExtractionRecord : IExtractionJournalEntry
{
    private ExtractionRecord(
        string recordId,
        string formId,
        ContentDigest formDigest,
        string formCandidateId,
        ExtractionRecordKind kind,
        ExtractionActor actor,
        IReadOnlyList<ExtractionFieldValue> values,
        DateTimeOffset recordedAt,
        string? sourceRecordDigest,
        IReadOnlyList<ContentDigest> sourceRecordDigests,
        string? sourceConflictId)
    {
        RecordId = Guard.NotBlank(recordId, nameof(recordId));
        FormId = Guard.NotBlank(formId, nameof(formId));
        FormDigest = formDigest;
        FormCandidateId = Guard.NotBlank(formCandidateId, nameof(formCandidateId));
        Kind = kind;
        Actor = actor;
        Values = Array.AsReadOnly(values.ToArray());
        RecordedAt = recordedAt;
        SourceRecordDigest = string.IsNullOrWhiteSpace(sourceRecordDigest)
            ? null
            : ContentDigest.Parse(sourceRecordDigest);
        SourceRecordDigests = Array.AsReadOnly((sourceRecordDigests ?? Array.Empty<ContentDigest>()).Distinct().ToArray());
        SourceConflictId = string.IsNullOrWhiteSpace(sourceConflictId) ? null : Guard.NotBlank(sourceConflictId, nameof(sourceConflictId));
    }

    public string RecordId { get; }
    public string FormId { get; }
    public ContentDigest FormDigest { get; }
    public string FormCandidateId { get; }
    public ExtractionRecordKind Kind { get; }
    public ExtractionActor Actor { get; }
    public IReadOnlyList<ExtractionFieldValue> Values { get; }
    public DateTimeOffset RecordedAt { get; }
    public ContentDigest? SourceRecordDigest { get; }
    public IReadOnlyList<ContentDigest> SourceRecordDigests { get; }
    public string? SourceConflictId { get; }
    public int Ordinal { get; private set; }
    public ContentDigest PreviousDigest { get; private set; } = default!;
    public ContentDigest Digest { get; private set; }

    public static string KindToString(ExtractionRecordKind kind) =>
        kind switch
        {
            ExtractionRecordKind.Proposal => ExtractionRecordKinds.Proposal,
            ExtractionRecordKind.Review => ExtractionRecordKinds.Review,
            ExtractionRecordKind.Correction => ExtractionRecordKinds.Correction,
            ExtractionRecordKind.Resolution => ExtractionRecordKinds.Resolution,
            _ => throw new ExtractionRuleException(ExtractionErrorCodes.MissingRecordKind, "Unsupported extraction record kind.")
        };

    public static ExtractionRecord Create(
        string recordId,
        string formId,
        ContentDigest formDigest,
        string candidateId,
        ExtractionRecordKind kind,
        ExtractionActor actor,
        IEnumerable<ExtractionFieldValue> values,
        DateTimeOffset recordedAt,
        string? sourceRecordDigest = null,
        IEnumerable<ContentDigest>? sourceRecordDigests = null,
        string? sourceConflictId = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(values);

        if (actor.KindNormalized is not (ExtractionActorKinds.Automation or ExtractionActorKinds.Human))
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidActor, "Invalid extraction actor.");
        }

        if (kind is not (ExtractionRecordKind.Proposal or ExtractionRecordKind.Review or ExtractionRecordKind.Correction or ExtractionRecordKind.Resolution))
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.MissingRecordKind, "Unsupported extraction record kind.");
        }

        if (actor.IsHuman is false && kind != ExtractionRecordKind.Proposal)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.AutomationCannotFinalize, "Automation can only create extraction proposals.");
        }

        var valueList = values.ToArray();
        if (valueList.Length == 0)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldValue, "At least one field value is required.");
        }

        if (kind == ExtractionRecordKind.Correction)
        {
            if (string.IsNullOrWhiteSpace(sourceRecordDigest))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.CorrectionTargetNotCurrent, "Correction requires exactly one source record.");
            }

            if (sourceConflictId is not null)
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.CorrectionTargetNotCurrent, "Correction records cannot carry a source conflict identifier.");
            }

            return new ExtractionRecord(
                Guard.NotBlank(recordId, nameof(recordId)),
                Guard.NotBlank(formId, nameof(formId)),
                formDigest,
                Guard.NotBlank(candidateId, nameof(candidateId)),
                kind,
                actor,
                valueList.OrderBy(item => Guard.NotBlank(item.FieldId, nameof(item.FieldId)), StringComparer.Ordinal).ToArray(),
                RequireUtc(recordedAt, nameof(recordedAt)),
                sourceRecordDigest,
                Array.Empty<ContentDigest>(),
                null);
        }

        if (kind == ExtractionRecordKind.Resolution)
        {
            if (string.IsNullOrWhiteSpace(sourceConflictId))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.RecordConflictResolutionNotFound, "Resolution requires source conflict id.");
            }

            var sources = (sourceRecordDigests ?? throw new ArgumentNullException(nameof(sourceRecordDigests)))
                .Distinct()
                .OrderBy(item => item.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (sources.Length < 2)
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.ResolutionTargetInvalid, "Resolution must target two or more source records.");
            }

            if (sources.Any(digest => !digest.IsValid))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.InvalidRecordBinding, "Resolution source records must be valid digests.");
            }

            return new ExtractionRecord(
                Guard.NotBlank(recordId, nameof(recordId)),
                Guard.NotBlank(formId, nameof(formId)),
                formDigest,
                Guard.NotBlank(candidateId, nameof(candidateId)),
                kind,
                actor,
                valueList.OrderBy(item => Guard.NotBlank(item.FieldId, nameof(item.FieldId)), StringComparer.Ordinal).ToArray(),
                RequireUtc(recordedAt, nameof(recordedAt)),
                null,
                sources,
                Guard.NotBlank(sourceConflictId, nameof(sourceConflictId)));
        }

        if (sourceConflictId is not null || sourceRecordDigest is not null)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidRecordBinding, "Only correction and resolution records may include source bindings.");
        }

        return new ExtractionRecord(
            Guard.NotBlank(recordId, nameof(recordId)),
            Guard.NotBlank(formId, nameof(formId)),
            formDigest,
            Guard.NotBlank(candidateId, nameof(candidateId)),
            kind,
            actor,
            valueList.OrderBy(item => Guard.NotBlank(item.FieldId, nameof(item.FieldId)), StringComparer.Ordinal).ToArray(),
            RequireUtc(recordedAt, nameof(recordedAt)),
            null,
            Array.Empty<ContentDigest>(),
            null);
    }

    public CanonicalJsonObject ToCanonicalJson()
    {
        var result = new CanonicalJsonObject()
            .Add("record_id", RecordId)
            .Add("form_id", FormId)
            .Add("form_digest", FormDigest.ToString())
            .Add("candidate_id", FormCandidateId)
            .Add("kind", KindToString(Kind))
            .Add("actor", Actor.ToCanonicalJson())
            .AddTimestamp("recorded_at", RecordedAt)
            .Add("ordinal", Ordinal)
            .Add("previous_digest", PreviousDigest.ToString())
            .Add("values", CanonicalJsonValue.Array(Values.Select(item => item.ToCanonicalJson()).ToArray()));

        if (SourceRecordDigest.HasValue)
        {
            result.Add("source_record_digest", SourceRecordDigest.Value.ToString());
        }

        if (SourceRecordDigests.Count > 0)
        {
            result.Add(
                "source_record_digests",
                CanonicalJsonValue.Array(SourceRecordDigests.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()));
        }

        if (SourceConflictId is not null)
        {
            result.Add("source_conflict_id", SourceConflictId);
        }

        return result;
    }

    public byte[] ToCanonicalBytes() => ToDigestEnvelope().ToCanonicalJsonBytes();

    internal void AttachChain(
        int ordinal,
        ContentDigest previousDigest,
        string formId,
        ContentDigest formDigest,
        IReadOnlyDictionary<string, ExtractionFieldDefinition> formFields,
        bool requireFinalValues)
    {
        if (Ordinal != 0 || Digest.IsValid)
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidChain, "An appended extraction record is immutable and cannot be attached again.");
        if (!string.Equals(formId, FormId, StringComparison.Ordinal) || formDigest != FormDigest)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidRecordBinding, "Record does not bind to the current form.");
        }

        if (ordinal < 1)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidChain, "Extraction record ordinal must be one or larger.");
        }

        if (!previousDigest.IsValid)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidChain, "Extraction record previous digest must be a valid digest.");
        }

        if (Kind == ExtractionRecordKind.Resolution && SourceRecordDigests.Count < 2)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.ResolutionTargetInvalid, "Resolution must target two or more source records.");
        }

        if (Kind != ExtractionRecordKind.Resolution && SourceRecordDigests.Count != 0)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.CorrectionTargetNotCurrent, "Source record digests are only allowed on resolution.");
        }

        if (Kind != ExtractionRecordKind.Correction && SourceRecordDigest.HasValue)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.CorrectionTargetNotCurrent, "Single source record digest is only allowed on correction.");
        }

        if (Kind == ExtractionRecordKind.Correction && !SourceRecordDigest.HasValue)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.CorrectionTargetNotCurrent, "Correction requires a source record.");
        }

        ValidateValues(formFields, requireFinalValues);
        Ordinal = ordinal;
        PreviousDigest = previousDigest;
        Digest = ToDigestEnvelope().ComputeDigest();
    }

    private void ValidateValues(IReadOnlyDictionary<string, ExtractionFieldDefinition> formFields, bool requireFinalValues)
    {
        if (Values.Count == 0)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldValue, "At least one value is required.");
        }

        if (Values.Select(item => item.FieldId).Distinct(StringComparer.Ordinal).Count() != Values.Count)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.DuplicateField, "Field values must target unique field identifiers.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in Values)
        {
            var fieldId = Guard.NotBlank(value.FieldId, nameof(value.FieldId));
            if (!formFields.TryGetValue(fieldId, out var fieldDefinition))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldDefinition, "Unknown extraction field.");
            }

            if (value.Value is null)
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldValue, "Extraction values may not be null.");
            }

            if (value.EvidenceLocation is null || !value.EvidenceLocation.Digest.IsValid)
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.MissingFieldEvidence, "Extraction values require exact Full Text evidence location.");
            }

            if (!fieldDefinition.IsValueCompatible(value.Value))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldValue, "Extraction value does not match field type.");
            }

            seen.Add(fieldId);
        }

        if (requireFinalValues)
        {
            var requiredFields = formFields.Values.Where(field => field.Required).Select(field => field.CanonicalFieldId).ToArray();
            foreach (var required in requiredFields)
            {
                if (!seen.Contains(required))
                {
                    throw new ExtractionRuleException(ExtractionErrorCodes.MissingRequiredField, "Final extraction records must include all required fields.");
                }
            }
        }
    }

    private DigestEnvelope ToDigestEnvelope() => new(
        DigestScope.CanonicalJsonRecord,
        ExtractionSchemas.RecordSchemaId,
        ExtractionSchemas.SchemaVersion,
        ToCanonicalJson());

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ExtractionRuleException(ExtractionErrorCodes.InvalidProtocolStatus, $"{name} must be UTC.");
}

public sealed class ExtractionAmendmentInvalidation : IExtractionJournalEntry
{
    private ExtractionAmendmentInvalidation(
        string invalidationId,
        string formId,
        ContentDigest formDigest,
        VerifiedProtocolAmendment amendment,
        IReadOnlyList<ContentDigest> affectedRecordDigests,
        string reason,
        ExtractionActor actor,
        DateTimeOffset invalidatedAt)
    {
        InvalidationId = Guard.NotBlank(invalidationId, nameof(invalidationId));
        FormId = Guard.NotBlank(formId, nameof(formId));
        FormDigest = formDigest;
        Amendment = amendment;
        AffectedRecordDigests = Array.AsReadOnly(affectedRecordDigests.ToArray());
        Reason = Guard.NotBlank(reason, nameof(reason));
        Actor = actor;
        InvalidatedAt = RequireUtc(invalidatedAt, nameof(invalidatedAt));
    }

    public string InvalidationId { get; }
    public string FormId { get; }
    public ContentDigest FormDigest { get; }
    public VerifiedProtocolAmendment Amendment { get; }
    public IReadOnlyList<ContentDigest> AffectedRecordDigests { get; }
    public string Reason { get; }
    public ExtractionActor Actor { get; }
    public DateTimeOffset InvalidatedAt { get; }
    public int Ordinal { get; private set; }
    public ContentDigest PreviousDigest { get; private set; } = default!;
    public ContentDigest Digest { get; private set; }

    public static ExtractionAmendmentInvalidation Create(
        string invalidationId,
        ExtractionForm form,
        VerifiedProtocolAmendment amendment,
        IEnumerable<ContentDigest> affectedRecordDigests,
        string reason,
        ExtractionActor actor,
        DateTimeOffset invalidatedAt)
    {
        ArgumentNullException.ThrowIfNull(amendment);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(affectedRecordDigests);
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.IsHuman)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidActor, "Amendment invalidation requires a human actor.");
        }

        if (amendment.Amendment.AmendsVersionId != form.ProtocolVersionId ||
            amendment.Amendment.PreviousContentDigest != form.ProtocolContentDigest)
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidRecordBinding, "Invalidation amendment does not match the extraction form Protocol authority.");

        var targets = affectedRecordDigests
            .Where(digest => digest.IsValid)
            .Distinct()
            .OrderBy(digest => digest.ToString(), StringComparer.Ordinal)
            .ToArray();

        if (targets.Length == 0)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.MissingInvalidationTarget, "Amendment invalidation requires at least one affected record digest.");
        }

        return new ExtractionAmendmentInvalidation(
            Guard.NotBlank(invalidationId, nameof(invalidationId)),
            form.FormId,
            form.Digest,
            amendment,
            targets,
            Guard.NotBlank(reason, nameof(reason)),
            actor,
            invalidatedAt);
    }

    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("invalidation_id", InvalidationId)
            .Add("form_id", FormId)
            .Add("form_digest", FormDigest.ToString())
            .Add("protocol_amendment_id", Amendment.Amendment.AmendmentId)
            .Add("protocol_amendment_digest", Amendment.AmendmentDigest.ToString())
            .Add("reason", Reason)
            .Add("invalidated_by", Actor.ToCanonicalJson())
            .AddTimestamp("invalidated_at", InvalidatedAt)
            .Add("affected_record_digests", CanonicalJsonValue.Array(AffectedRecordDigests.Select(item => CanonicalJsonValue.From(item.ToString())).ToArray()));
    }

    internal void AttachChain(
        int ordinal,
        ContentDigest previousDigest,
        string formId,
        ContentDigest formDigest)
    {
        if (Ordinal != 0 || Digest.IsValid)
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidChain, "An appended extraction invalidation is immutable and cannot be attached again.");
        if (!string.Equals(formId, FormId, StringComparison.Ordinal) || formDigest != FormDigest)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidRecordBinding, "Invalidation does not bind the current extraction form.");
        }

        if (ordinal < 1)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidChain, "Invalidation ordinal must be one or larger.");
        }

        if (!previousDigest.IsValid)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidChain, "Invalidation previous digest must be a valid digest.");
        }

        if (AffectedRecordDigests.Count == 0)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.MissingInvalidationTarget, "Invalidation must target at least one record.");
        }

        Ordinal = ordinal;
        PreviousDigest = previousDigest;
        Digest = ToDigestEnvelope().ComputeDigest();
    }

    public byte[] ToCanonicalBytes() => ToDigestEnvelope().ToCanonicalJsonBytes();

    private DigestEnvelope ToDigestEnvelope() => new(
        DigestScope.CanonicalJsonRecord,
        ExtractionSchemas.InvalidationSchemaId,
        ExtractionSchemas.SchemaVersion,
        ToCanonicalJson());

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ExtractionRuleException(ExtractionErrorCodes.InvalidProtocolStatus, $"{name} must be UTC.");
}
