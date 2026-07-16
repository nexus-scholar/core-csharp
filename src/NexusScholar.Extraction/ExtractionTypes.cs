using System.Globalization;

namespace NexusScholar.Extraction;

public static class ExtractionSchemas
{
    public const string FormSchemaId = "nexus.extraction.form";
    public const string RecordSchemaId = "nexus.extraction.record";
    public const string InvalidationSchemaId = "nexus.extraction.invalidation";
    public const string SchemaVersion = "1.0.0";
}

public static class ExtractionActorKinds
{
    public const string Human = "human";
    public const string Automation = "automation";
}

public static class ExtractionRecordKinds
{
    public const string Proposal = "proposal";
    public const string Review = "review";
    public const string Correction = "correction";
    public const string Resolution = "resolution";
}

public enum ExtractionRecordKind
{
    Proposal,
    Review,
    Correction,
    Resolution
}

public static class ExtractionErrorCodes
{
    public const string NonHumanApprover = "extraction-form-must-be-human-approved";
    public const string InvalidProtocolStatus = "extraction-form-protocol-invalid";
    public const string InvalidQuestionReference = "extraction-form-question-ref-invalid";
    public const string InvalidFieldDefinition = "extraction-field-definition-invalid";
    public const string DuplicateField = "extraction-duplicate-field";
    public const string InvalidFieldValue = "extraction-field-value-invalid";
    public const string MissingFieldEvidence = "extraction-field-evidence-missing";
    public const string MissingRequiredField = "extraction-required-field-missing";
    public const string InvalidActor = "extraction-record-actor-invalid";
    public const string AutomationCannotFinalize = "extraction-automation-cannot-finalize";
    public const string InvalidChain = "extraction-journal-chain-invalid";
    public const string InvalidRecordBinding = "extraction-record-binding-invalid";
    public const string MissingRecordKind = "extraction-record-kind-missing";
    public const string CorrectionTargetNotCurrent = "extraction-correction-target-not-current";
    public const string ResolutionTargetInvalid = "extraction-resolution-target-invalid";
    public const string RecordConflictResolutionNotFound = "extraction-resolution-conflict-not-found";
    public const string InvalidationScopeInvalid = "extraction-invalidation-scope-invalid";
    public const string MissingInvalidationTarget = "extraction-invalidation-target-missing";
}

public sealed class ExtractionRuleException : DomainRuleException
{
    public ExtractionRuleException(string category, string message)
        : base(message)
    {
        Category = Guard.NotBlank(category, nameof(category));
    }

    public string Category { get; }
}

public sealed record ExtractionActor(string ActorId, string Kind, string? Role = null)
{
    public static ExtractionActor Human(string actorId, string? role = null) =>
        new(Guard.NotBlank(actorId, nameof(actorId)), ExtractionActorKinds.Human, role);

    public static ExtractionActor Automation(string actorId) =>
        new(Guard.NotBlank(actorId, nameof(actorId)), ExtractionActorKinds.Automation, null);

    public bool IsHuman => KindNormalized == ExtractionActorKinds.Human;

    public string KindNormalized => NormalizeKind(Kind);

    public CanonicalJsonObject ToCanonicalJson()
    {
        var kind = KindNormalized;
        var result = new CanonicalJsonObject()
            .Add("actor_id", Guard.NotBlank(ActorId, nameof(ActorId)))
            .Add("kind", kind);
        if (!string.IsNullOrWhiteSpace(Role))
        {
            result.Add("role", Role.Trim());
        }

        return result;
    }

    public static string NormalizeKind(string kind)
    {
        var normalized = Guard.NotBlank(kind, nameof(kind)).ToLowerInvariant();
        return normalized is ExtractionActorKinds.Human or ExtractionActorKinds.Automation
            ? normalized
            : throw new ExtractionRuleException(ExtractionErrorCodes.InvalidActor, "Extraction actor kind is not recognized.");
    }
}

public sealed record ExtractionFieldDefinition(string FieldId, string QuestionRef, string ValueType, bool Required = true)
{
    public string CanonicalFieldId => Guard.NotBlank(FieldId, nameof(FieldId));
    public string CanonicalQuestionRef => Guard.NotBlank(QuestionRef, nameof(QuestionRef));
    public string CanonicalValueType => NormalizeValueType(Guard.NotBlank(ValueType, nameof(ValueType)));

    public CanonicalJsonObject ToCanonicalJson() =>
        new CanonicalJsonObject()
            .Add("field_id", CanonicalFieldId)
            .Add("question_ref", CanonicalQuestionRef)
            .Add("value_type", CanonicalValueType)
            .Add("required", Required);

    public static string NormalizeValueType(string valueType)
    {
        return Guard.NotBlank(valueType, nameof(valueType)).Trim().ToLowerInvariant() switch
        {
            "string" => "string",
            "integer" => "integer",
            "number" => "number",
            "boolean" => "boolean",
            "object" => "object",
            "array" => "array",
            _ => throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldDefinition, "Unsupported extraction field value type.")
        };
    }

    public bool IsValueCompatible(CanonicalJsonValue value)
    {
        return CanonicalValueType switch
        {
            "string" => value is CanonicalJsonString,
            "integer" => IsInteger(value),
            "number" => value is CanonicalJsonNumber,
            "boolean" => value is CanonicalJsonBoolean,
            "object" => value is CanonicalJsonObject,
            "array" => value is CanonicalJsonArray,
            _ => false
        };
    }

    private static bool IsInteger(CanonicalJsonValue value)
    {
        if (value is not CanonicalJsonNumber numberValue)
        {
            return false;
        }

        return decimal.TryParse(numberValue.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValueParsed) &&
            numberValueParsed >= long.MinValue &&
            numberValueParsed <= long.MaxValue &&
            numberValueParsed == decimal.Truncate(numberValueParsed);
    }
}

public sealed record ExtractionFieldValue(string FieldId, CanonicalJsonValue Value, FullTextEvidenceLocation EvidenceLocation)
{
    public CanonicalJsonObject ToCanonicalJson()
    {
        if (Value is null)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldValue, "Extraction value is required.");
        }

        if (EvidenceLocation is null)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.MissingFieldEvidence, "Extraction value evidence location is required.");
        }

        return new CanonicalJsonObject()
            .Add("field_id", Guard.NotBlank(FieldId, nameof(FieldId)))
            .Add("value", CanonicalJsonValue.DeepClone(Value))
            .Add("evidence_location", EvidenceLocation.ToCanonicalJson());
    }
}

public sealed record ExtractionConflict(
    string ConflictId,
    string CandidateId,
    string FieldId,
    IReadOnlyList<ContentDigest> SourceRecordDigests,
    bool Resolved);

public sealed record ExtractionProjection(
    ContentDigest HeadDigest,
    IReadOnlyDictionary<string, IReadOnlyList<ContentDigest>> CurrentRecordDigestsByCandidate,
    IReadOnlyDictionary<string, IReadOnlyList<ExtractionConflict>> DisagreementsByCandidate,
    IReadOnlyList<ContentDigest> InvalidatedRecordDigests);

public interface IExtractionJournalEntry
{
    int Ordinal { get; }
    ContentDigest PreviousDigest { get; }
    ContentDigest Digest { get; }
}
