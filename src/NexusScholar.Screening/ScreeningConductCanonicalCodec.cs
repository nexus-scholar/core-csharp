using System.Globalization;
using System.Text.Json;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Screening;

public static class ScreeningConductCanonicalCodec
{
    public static byte[] Serialize(ScreeningConductPolicy policy) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes((policy ?? throw new ArgumentNullException(nameof(policy))).ToCanonicalJson());

    public static byte[] Serialize(ScreeningConductHeader header) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes((header ?? throw new ArgumentNullException(nameof(header))).ToCanonicalJson());

    public static byte[] Serialize(ScreeningConductDecision decision) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes((decision ?? throw new ArgumentNullException(nameof(decision))).ToCanonicalJson());

    public static byte[] Serialize(ScreeningConductInvalidation invalidation) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes((invalidation ?? throw new ArgumentNullException(nameof(invalidation))).ToCanonicalJson());

    public static byte[] Serialize(ScreeningConductHandoff handoff) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes((handoff ?? throw new ArgumentNullException(nameof(handoff))).ToCanonicalJson());

    public static ScreeningConductPolicy RehydratePolicy(
        byte[] bytes,
        ContentDigest expectedDigest,
        VerifiedDeduplicationResult deduplication,
        VerifiedProtocolVersion protocol,
        ScreeningCriteria criteria)
    {
        var content = ParseEnvelope(bytes, expectedDigest, ScreeningConductPolicy.SchemaId);
        RequireExact(content,
        [
            "adjudicator_roles", "approved_at", "approved_by", "assignments", "candidate_set_digest", "candidate_set_id",
            "criteria_digest", "criteria_id", "exclusion_reasons", "policy_id", "protocol_content_digest",
            "protocol_version_id", "required_review_count"
        ]);
        var policy = ScreeningConductPolicy.Create(
            Text(content, "policy_id"), Text(content, "candidate_set_id"), deduplication, protocol, criteria,
            Integer(content, "required_review_count"),
            Array(content, "assignments").Select(ParseAssignment),
            Array(content, "adjudicator_roles").Select(Text),
            Array(content, "exclusion_reasons").Select(ParseReason),
            ParseActor(Object(content, "approved_by")), Timestamp(content, "approved_at"));
        RequireReproduction(bytes, policy.Digest, expectedDigest, Serialize(policy), "Screening conduct policy");
        return policy;
    }

    public static ScreeningConductHeader RehydrateHeader(
        byte[] bytes,
        ContentDigest expectedDigest,
        ScreeningConductPolicy policy)
    {
        var content = ParseEnvelope(bytes, expectedDigest, ScreeningConductHeader.SchemaId);
        RequireExact(content,
        [
            "candidate_ids", "candidate_set_digest", "candidate_set_id", "conduct_id", "created_at", "created_by",
            "criteria_digest", "criteria_id", "policy_digest", "policy_id", "protocol_content_digest", "protocol_version_id"
        ]);
        var header = ScreeningConductHeader.Create(
            Text(content, "conduct_id"), policy, ParseActor(Object(content, "created_by")), Timestamp(content, "created_at"));
        RequireReproduction(bytes, header.Digest, expectedDigest, Serialize(header), "Screening conduct header");
        return header;
    }

    public static ScreeningConductDecision RehydrateDecision(
        byte[] bytes,
        ContentDigest expectedDigest,
        ScreeningConductHeader header)
    {
        var content = ParseEnvelope(bytes, expectedDigest, ScreeningConductDecision.SchemaId);
        RequireExact(content,
        [
            "actor", "candidate_id", "candidate_set_digest", "candidate_set_id", "conduct_id", "criteria_digest",
            "criteria_id", "decided_at", "decision_id", "evidence", "kind", "ordinal", "policy_digest", "policy_id",
            "previous_digest", "protocol_content_digest", "protocol_version_id", "rationale", "request_digest", "request_id",
            "source_decision_ids", "verdict"
        ], ["exclusion_reason_code", "resolved_conflict_id", "supersedes_decision_id"]);
        var decision = ScreeningConductDecision.Create(
            header, Integer(content, "ordinal"), Digest(content, "previous_digest"), Text(content, "request_id"),
            Text(content, "candidate_id"), ParseKind(Text(content, "kind")), Text(content, "verdict"),
            ParseActor(Object(content, "actor")), Text(content, "rationale"), Timestamp(content, "decided_at"),
            OptionalText(content, "exclusion_reason_code"), OptionalText(content, "supersedes_decision_id"),
            OptionalText(content, "resolved_conflict_id"), Array(content, "source_decision_ids").Select(Text),
            Array(content, "evidence").Select(ParseEvidence));
        RequireReproduction(bytes, decision.Digest, expectedDigest, Serialize(decision), "Screening conduct decision");
        return decision;
    }

    public static ScreeningConductInvalidation RehydrateInvalidation(
        byte[] bytes,
        ContentDigest expectedDigest,
        ScreeningConductHeader header)
    {
        var content = ParseEnvelope(bytes, expectedDigest, ScreeningConductInvalidation.SchemaId);
        RequireExact(content,
        [
            "actor", "affected_decision_ids", "conduct_id", "invalidated_at", "invalidation_id", "ordinal",
            "policy_digest", "policy_id", "previous_digest", "reason", "source"
        ]);
        var invalidation = ScreeningConductInvalidation.Create(
            header, Integer(content, "ordinal"), Digest(content, "previous_digest"), Text(content, "invalidation_id"),
            ParseEvidence(Object(content, "source")), Array(content, "affected_decision_ids").Select(Text),
            ParseActor(Object(content, "actor")), Text(content, "reason"), Timestamp(content, "invalidated_at"));
        RequireReproduction(bytes, invalidation.Digest, expectedDigest, Serialize(invalidation), "Screening conduct invalidation");
        return invalidation;
    }

    public static ScreeningConductHandoff RehydrateHandoff(
        byte[] bytes,
        ContentDigest expectedDigest,
        ScreeningConductJournal journal)
    {
        var content = ParseEnvelope(bytes, expectedDigest, ScreeningConductHandoff.SchemaId);
        RequireExact(content, ["conduct_id", "created_at", "handoff_id", "journal_head_digest", "outcomes", "policy_digest"]);
        var handoff = ScreeningConductHandoff.Create(Text(content, "handoff_id"), journal, Timestamp(content, "created_at"));
        RequireReproduction(bytes, handoff.Digest, expectedDigest, Serialize(handoff), "Screening conduct handoff");
        return handoff;
    }

    private static CanonicalJsonObject ParseEnvelope(byte[] bytes, ContentDigest expectedDigest, string schemaId)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var parsed = CanonicalJsonValue.FromJsonElement(document.RootElement);
            if (parsed is not CanonicalJsonObject root || !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(root)))
                throw Rule("Canonical Screening conduct record bytes are required.");
            return DigestEnvelope.RehydrateAndVerify(
                document.RootElement, expectedDigest, DigestScope.CanonicalJsonRecord, schemaId, "1.0.0").Envelope.Content;
        }
        catch (ScreeningRuleException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or FormatException)
        {
            throw Rule($"Screening conduct envelope verification failed: {exception.Message}");
        }
    }

    private static ScreeningConductActor ParseActor(CanonicalJsonObject value)
    {
        RequireExact(value, ["actor_id", "kind", "role"]);
        return new ScreeningConductActor(Text(value, "actor_id"), Text(value, "kind"), Text(value, "role"));
    }

    private static ScreeningConductRoleAssignment ParseAssignment(CanonicalJsonValue value)
    {
        var item = AsObject(value);
        RequireExact(item, ["actor_id", "role"]);
        return new ScreeningConductRoleAssignment(Text(item, "actor_id"), Text(item, "role"));
    }

    private static ScreeningExclusionReason ParseReason(CanonicalJsonValue value)
    {
        var item = AsObject(value);
        RequireExact(item, ["code", "stage"]);
        return new ScreeningExclusionReason(Text(item, "code"), Text(item, "stage"));
    }

    private static ScreeningConductEvidenceRef ParseEvidence(CanonicalJsonValue value)
    {
        var item = AsObject(value);
        RequireExact(item, ["digest", "id", "kind"]);
        return new ScreeningConductEvidenceRef(Text(item, "kind"), Text(item, "id"), Digest(item, "digest"));
    }

    private static ScreeningConductDecisionKind ParseKind(string value) => value switch
    {
        "review" => ScreeningConductDecisionKind.Review,
        "correction" => ScreeningConductDecisionKind.Correction,
        "adjudication" => ScreeningConductDecisionKind.Adjudication,
        _ => throw Rule("Unknown persisted Screening conduct decision kind.")
    };

    private static void RequireReproduction(byte[] original, ContentDigest actual, ContentDigest expected, byte[] reproduced, string kind)
    {
        if (actual != expected || !original.SequenceEqual(reproduced))
            throw Rule($"{kind} did not reproduce the expected digest and canonical bytes.");
    }

    private static void RequireExact(CanonicalJsonObject value, IEnumerable<string> required, IEnumerable<string>? optional = null)
    {
        var requiredSet = required.ToHashSet(StringComparer.Ordinal);
        var allowed = requiredSet.Concat(optional ?? System.Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        if (!requiredSet.IsSubsetOf(value.Properties.Keys) || value.Properties.Keys.Any(key => !allowed.Contains(key)))
            throw Rule("Screening conduct canonical record has missing or unknown fields.");
    }

    private static CanonicalJsonObject Object(CanonicalJsonObject root, string name) => AsObject(Value(root, name));
    private static CanonicalJsonObject AsObject(CanonicalJsonValue value) => value as CanonicalJsonObject
        ?? throw Rule("Screening conduct canonical field must be an object.");
    private static IReadOnlyList<CanonicalJsonValue> Array(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonArray array
        ? array.Items : throw Rule($"Screening conduct canonical field '{name}' must be an array.");
    private static string Text(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonString text
        ? text.Value : throw Rule($"Screening conduct canonical field '{name}' must be a string.");
    private static string Text(CanonicalJsonValue value) => value is CanonicalJsonString text
        ? text.Value : throw Rule("Screening conduct canonical array entry must be a string.");
    private static string? OptionalText(CanonicalJsonObject root, string name) => root.Properties.ContainsKey(name) ? Text(root, name) : null;
    private static int Integer(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonNumber number &&
        int.TryParse(number.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result : throw Rule($"Screening conduct canonical field '{name}' must be an integer.");
    private static ContentDigest Digest(CanonicalJsonObject root, string name)
    {
        try { return ContentDigest.Parse(Text(root, name)); }
        catch (Exception exception) when (exception is ArgumentException or FormatException) { throw Rule($"Screening conduct digest is invalid: {exception.Message}"); }
    }
    private static DateTimeOffset Timestamp(CanonicalJsonObject root, string name)
    {
        var value = Text(root, name);
        CanonicalTimestamp.ValidateCanonicalUtc(value);
        return DateTimeOffset.ParseExact(value, CanonicalTimestamp.DefaultUtcFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
    private static CanonicalJsonValue Value(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value)
        ? value : throw Rule($"Screening conduct canonical field '{name}' is required.");
    private static ScreeningRuleException Rule(string message) => new(ScreeningErrorCodes.UnverifiedConductAuthority, message);
}
