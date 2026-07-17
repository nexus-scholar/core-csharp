using System.Text.Json;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Screening;

public static class ScreeningCriteriaCanonicalCodec
{
    public static byte[] Serialize(ScreeningCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(criteria.ToCanonicalJson());
    }

    public static ScreeningCriteria Rehydrate(
        byte[] bytes,
        ContentDigest expectedCriteriaDigest,
        VerifiedProtocolVersion protocol)
    {
        if (!expectedCriteriaDigest.IsValid)
        {
            throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, "Expected criteria digest must be a canonical content digest.");
        }

        try
        {
            ArgumentNullException.ThrowIfNull(protocol);
            var content = ParseCanonicalRecord(bytes);
            RequireExact(content,
                [
                    "approved_protocol_binding", "approved_protocol_digest", "approved_protocol_digest_scope",
                    "approved_protocol_status", "criteria_id", "criteria_version", "exclude", "include",
                    "require_protocol_binding", "schema_id", "schema_version", "stage"],
                [
                    "current_protocol_content_digest", "workflow_binding", "review_guidance", "full_text_requirements"
                ]);

            var criteria = new ScreeningCriteria(
                Text(content, "criteria_id"),
                Text(content, "criteria_version"),
                Text(content, "stage"),
                Value(content, "include"),
                Value(content, "exclude"),
                Boolean(content, "require_protocol_binding"),
                OptionalText(content, "approved_protocol_binding"),
                OptionalText(content, "approved_protocol_digest"),
                OptionalText(content, "workflow_binding"),
                OptionalText(content, "review_guidance"),
                content.Properties.TryGetValue("full_text_requirements", out var fullTextRequirementsValue)
                    ? fullTextRequirementsValue
                    : null,
                approvedProtocolDigestScope: OptionalText(content, "approved_protocol_digest_scope"),
                approvedProtocolStatus: OptionalText(content, "approved_protocol_status"),
                currentProtocolContentDigest: OptionalText(content, "current_protocol_content_digest"));

            if (!string.Equals(criteria.Stage, ScreeningStages.TitleAbstract, StringComparison.Ordinal))
            {
                throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, "Screening criteria stage must be 'title_abstract'.");
            }

            EnsureProtocolBinding(criteria, protocol);

            if (criteria.ComputeDigest() != expectedCriteriaDigest)
            {
                throw Rule(ScreeningErrorCodes.CriteriaDigestMismatch, "Screening criteria digest does not match expected digest.");
            }

            var replayed = Serialize(criteria);
            if (!replayed.SequenceEqual(bytes))
            {
                throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, "Screening criteria canonical bytes are not reproducible.");
            }

            return criteria;
        }
        catch (ScreeningRuleException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        {
            throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, $"Screening criteria canonical record failed to rehydrate: {exception.Message}");
        }
    }

    private static void EnsureProtocolBinding(ScreeningCriteria criteria, VerifiedProtocolVersion protocol)
    {
        if (!criteria.RequiresProtocolBinding ||
            string.IsNullOrWhiteSpace(criteria.ApprovedProtocolBinding) ||
            string.IsNullOrWhiteSpace(criteria.ApprovedProtocolDigest) ||
            string.IsNullOrWhiteSpace(criteria.ApprovedProtocolDigestScope) ||
            string.IsNullOrWhiteSpace(criteria.ApprovedProtocolStatus))
        {
            throw Rule(ScreeningErrorCodes.InvalidProtocolBinding, "Final Screening criteria must be protocol-bound.");
        }

        if (!string.Equals(criteria.ApprovedProtocolBinding, protocol.Version.Id, StringComparison.Ordinal) ||
            !string.Equals(criteria.ApprovedProtocolStatus, ScreeningProtocolBindingStatus.Approved, StringComparison.Ordinal))
        {
            throw Rule(ScreeningErrorCodes.InvalidProtocolBinding, "Screening criteria protocol binding is not approved for the supplied protocol.");
        }

        var protocolDigest = ParseDigest(criteria.ApprovedProtocolDigest, "approved_protocol_digest");
        if (protocolDigest != protocol.Version.ContentDigest)
        {
            throw Rule(ScreeningErrorCodes.InvalidProtocolBinding, "Screening criteria approved protocol digest does not match verified protocol.");
        }

        if (!string.Equals(criteria.ApprovedProtocolDigestScope, DigestScope.ProtocolContent.ToString(), StringComparison.Ordinal))
        {
            throw Rule(ScreeningErrorCodes.InvalidCriteriaDigestScope, "Screening criteria protocol digest scope must be protocol-content.");
        }

        if (criteria.CurrentProtocolContentDigest is not null)
        {
            var currentDigest = ParseDigest(criteria.CurrentProtocolContentDigest, "current_protocol_content_digest");
            if (currentDigest != protocol.Version.ContentDigest)
            {
                throw Rule(ScreeningErrorCodes.InvalidProtocolBinding, "Screening criteria current protocol content digest does not match verified protocol.");
            }
        }
    }

    private static CanonicalJsonObject ParseCanonicalRecord(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var document = JsonDocument.Parse(bytes);
        var parsed = CanonicalJsonValue.FromJsonElement(document.RootElement);
        if (parsed is not CanonicalJsonObject record ||
            !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(record)))
        {
            throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, "Screening criteria canonical bytes must be exact canonical JSON.");
        }

        if (!string.Equals(Text(record, "schema_id"), ScreeningSchema.CriteriaSchemaId, StringComparison.Ordinal) ||
            !string.Equals(Text(record, "schema_version"), ScreeningSchema.CriteriaSchemaVersion, StringComparison.Ordinal))
        {
            throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, "Screening criteria schema must match canonical criteria schema.");
        }

        return record;
    }

    private static void RequireExact(CanonicalJsonObject value, IEnumerable<string> required, IEnumerable<string>? optional = null)
    {
        var requiredSet = required.ToHashSet(StringComparer.Ordinal);
        var allowedSet = requiredSet.Concat(optional ?? System.Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        if (!requiredSet.IsSubsetOf(value.Properties.Keys) || value.Properties.Keys.Any(key => !allowedSet.Contains(key)))
        {
            throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, "Screening criteria canonical record has missing or unknown fields.");
        }
    }

    private static CanonicalJsonValue Value(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value)
        ? value : throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, $"Screening criteria canonical field '{name}' is required.");

    private static string Text(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonString value
        ? value.Value : throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, $"Screening criteria canonical field '{name}' must be a string.");

    private static string? OptionalText(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value)
        ? value is CanonicalJsonString textValue ? textValue.Value : throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord,
            $"Screening criteria canonical field '{name}' must be a string.")
        : null;

    private static bool Boolean(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonBoolean value
        ? value.Value : throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, $"Screening criteria canonical field '{name}' must be a boolean.");

    private static ContentDigest ParseDigest(string value, string field)
    {
        try
        {
            return ContentDigest.Parse(value);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw Rule(ScreeningErrorCodes.InvalidCriteriaCanonicalRecord, $"Screening criteria canonical field '{field}' is not a canonical digest: {exception.Message}");
        }
    }

    private static ScreeningRuleException Rule(string category, string message) => new(category, message);
}
