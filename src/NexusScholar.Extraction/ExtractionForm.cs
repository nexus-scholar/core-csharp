using NexusScholar.Protocol;

namespace NexusScholar.Extraction;

public sealed class ExtractionForm
{
    private ExtractionForm(
        string formId,
        string candidateId,
        VerifiedProtocolVersion protocol,
        IReadOnlyList<string> questionRefs,
        IReadOnlyList<ExtractionFieldDefinition> fields,
        ExtractionActor approvedBy,
        DateTimeOffset approvedAt)
    {
        FormId = Guard.NotBlank(formId, nameof(formId));
        CandidateId = Guard.NotBlank(candidateId, nameof(candidateId));
        ProtocolVersion = protocol;
        ProtocolVersionId = protocol.Version.Id;
        ProtocolContentDigest = protocol.Version.ContentDigest;
        QuestionRefs = Array.AsReadOnly(questionRefs.ToArray());
        Fields = Array.AsReadOnly(fields.ToArray());
        ApprovedBy = approvedBy;
        ApprovedAt = RequireUtc(approvedAt, nameof(approvedAt));
        DigestEnvelope = BuildDigestEnvelope();
    }

    public string FormId { get; }
    public string CandidateId { get; }
    public VerifiedProtocolVersion ProtocolVersion { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public IReadOnlyList<string> QuestionRefs { get; }
    public IReadOnlyList<ExtractionFieldDefinition> Fields { get; }
    public ExtractionActor ApprovedBy { get; }
    public DateTimeOffset ApprovedAt { get; }
    public DigestEnvelope DigestEnvelope { get; }
    public ContentDigest Digest => DigestEnvelope.ComputeDigest();

    public static string SchemaId => ExtractionSchemas.FormSchemaId;

    public static string SchemaVersion => ExtractionSchemas.SchemaVersion;

    public static ExtractionForm Create(
        string formId,
        string candidateId,
        VerifiedProtocolVersion protocol,
        IEnumerable<string> questionRefs,
        IEnumerable<ExtractionFieldDefinition> fields,
        ExtractionActor approvedBy,
        DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(questionRefs);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(approvedBy);
        if (!approvedBy.IsHuman)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.NonHumanApprover, "Extraction forms require a human approver.");
        }

        if (protocol.Version.Status != ProtocolStatus.Approved)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidProtocolStatus, "Extraction forms require an approved Protocol version.");
        }

        var protocolQuestionRefs = protocol.Version.RequiredDecisions
            .Select(item => item.DecisionKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (protocolQuestionRefs.Length == 0)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidProtocolStatus, "Approved Protocol version is missing required decisions.");
        }

        var normalizedQuestionRefs = questionRefs
            .Select(value => Guard.NotBlank(value, nameof(questionRefs)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (normalizedQuestionRefs.Length == 0)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidQuestionReference, "Extraction forms require at least one question reference.");
        }

        if (!normalizedQuestionRefs.All(questionRef => protocolQuestionRefs.Contains(questionRef, StringComparer.Ordinal)))
        {
            throw new ExtractionRuleException(
                ExtractionErrorCodes.InvalidQuestionReference,
                "Every extraction question reference must exist in the approved Protocol decision set.");
        }

        var normalizedFields = fields
            .Select(field => new ExtractionFieldDefinition(field.FieldId, field.QuestionRef, field.ValueType, field.Required))
            .ToArray();
        if (normalizedFields.Length == 0)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.InvalidFieldDefinition, "Extraction forms require at least one field definition.");
        }

        if (normalizedFields.Select(field => field.CanonicalFieldId).Distinct(StringComparer.Ordinal).Count() != normalizedFields.Length)
        {
            throw new ExtractionRuleException(ExtractionErrorCodes.DuplicateField, "Extraction field identifiers must be unique.");
        }

        foreach (var field in normalizedFields)
        {
            if (!normalizedQuestionRefs.Contains(field.CanonicalQuestionRef, StringComparer.Ordinal))
            {
                throw new ExtractionRuleException(ExtractionErrorCodes.InvalidQuestionReference, "Field references must be bound through the form question refs.");
            }
        }

        return new ExtractionForm(
            Guard.NotBlank(formId, nameof(formId)),
            Guard.NotBlank(candidateId, nameof(candidateId)),
            protocol,
            normalizedQuestionRefs,
            normalizedFields.OrderBy(field => field.FieldId, StringComparer.Ordinal).ToArray(),
            approvedBy,
            approvedAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => DigestEnvelope.ToCanonicalJsonObject();

    public byte[] ToCanonicalBytes() => DigestEnvelope.ToCanonicalJsonBytes();

    private DigestEnvelope BuildDigestEnvelope() => new(
        DigestScope.CanonicalJsonRecord,
        ExtractionSchemas.FormSchemaId,
        ExtractionSchemas.SchemaVersion,
        new CanonicalJsonObject()
            .Add("form_id", FormId)
            .Add("candidate_id", CandidateId)
            .Add("protocol_version_id", ProtocolVersionId)
            .Add("protocol_content_digest", ProtocolContentDigest.ToString())
            .Add("question_refs", CanonicalJsonValue.Array(QuestionRefs.Select(CanonicalJsonValue.From).ToArray()))
            .Add("fields", CanonicalJsonValue.Array(Fields.Select(field => field.ToCanonicalJson()).ToArray()))
            .Add("approved_by", ApprovedBy.ToCanonicalJson())
            .AddTimestamp("approved_at", ApprovedAt));

    private static DateTimeOffset RequireUtc(DateTimeOffset value, string name) =>
        value.Offset == TimeSpan.Zero
            ? value
            : throw new ExtractionRuleException(ExtractionErrorCodes.InvalidProtocolStatus, $"{name} must be UTC.");
}
