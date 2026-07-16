using System.Globalization;
using System.Text.Json;
using NexusScholar.Deduplication;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Screening;

namespace NexusScholar.Screening.FullText;

public static class FullTextScreeningConductCanonicalCodec
{
    public static byte[] Serialize(FullTextScreeningConductPolicy value) => Bytes(value.ToCanonicalJson());
    public static byte[] Serialize(FullTextScreeningConductHeader value) => Bytes(value.ToCanonicalJson());
    public static byte[] Serialize(FullTextScreeningConductDecision value) => Bytes(value.ToCanonicalJson());
    public static byte[] Serialize(FullTextScreeningConductInvalidation value) => Bytes(value.ToCanonicalJson());
    public static byte[] Serialize(FullTextScreeningConductHandoff value) => Bytes(value.ToCanonicalJson());

    public static FullTextScreeningConductPolicy RehydratePolicy(
        byte[] bytes, ContentDigest expectedDigest, VerifiedDeduplicationResult deduplication,
        VerifiedProtocolVersion protocol, ScreeningCriteria criteria, VerifiedFullTextAdmission admission,
        ContentDigest expectedArtifactDigest, ContentDigest? expectedExtractionDigest = null)
    {
        var content = Content(bytes, expectedDigest, FullTextScreeningConductSchema.PolicySchemaId);
        Exact(content,
            ["adjudicator_roles", "admission_candidate_id", "admission_candidate_set_id", "admission_conduct_id", "admission_digest", "admission_handoff_id", "approved_at", "approved_by", "assignments", "candidate_set_digest", "candidate_set_id", "criteria_digest", "criteria_id", "exclusion_reasons", "full_text_artifact_digest", "policy_id", "protocol_content_digest", "protocol_version_id", "required_review_count"],
            ["full_text_extraction_attempt_digest"]);
        if (Digest(content, "full_text_artifact_digest") != expectedArtifactDigest ||
            OptionalDigest(content, "full_text_extraction_attempt_digest") != expectedExtractionDigest)
            throw Invalid("Full Text policy artifact or extraction authority does not match expected inputs.");
        var policy = FullTextScreeningConductPolicy.Create(
            Text(content, "policy_id"), Text(content, "candidate_set_id"), deduplication, protocol, criteria, admission,
            Integer(content, "required_review_count"), Array(content, "assignments").Select(Assignment),
            Strings(content, "adjudicator_roles"), Array(content, "exclusion_reasons").Select(Reason),
            Actor(Object(content, "approved_by")), Timestamp(content, "approved_at"), expectedArtifactDigest, expectedExtractionDigest);
        RequireReproduction(bytes, Serialize(policy), policy.Digest, expectedDigest, "Full Text policy");
        return policy;
    }

    public static FullTextScreeningConductPolicy RehydratePolicy(
        byte[] bytes, ContentDigest expectedDigest, FullTextScreeningConductPolicy expectedPolicy)
    {
        ArgumentNullException.ThrowIfNull(expectedPolicy);
        _ = Content(bytes, expectedDigest, FullTextScreeningConductSchema.PolicySchemaId);
        RequireReproduction(bytes, Serialize(expectedPolicy), expectedPolicy.Digest, expectedDigest, "Full Text policy");
        return expectedPolicy;
    }

    public static FullTextScreeningConductHeader RehydrateHeader(
        byte[] bytes, ContentDigest expectedDigest, FullTextScreeningConductPolicy policy)
    {
        var content = Content(bytes, expectedDigest, FullTextScreeningConductSchema.HeaderSchemaId);
        Exact(content,
            ["admission_conduct_id", "admission_digest", "admission_handoff_id", "candidate_ids", "candidate_set_digest", "candidate_set_id", "conduct_id", "created_at", "created_by", "full_text_artifact_digest", "policy_digest", "policy_id"],
            ["full_text_extraction_attempt_digest"]);
        var header = FullTextScreeningConductHeader.Create(Text(content, "conduct_id"), policy,
            Actor(Object(content, "created_by")), Timestamp(content, "created_at"));
        RequireReproduction(bytes, Serialize(header), header.Digest, expectedDigest, "Full Text header");
        return header;
    }

    public static FullTextScreeningConductDecision RehydrateDecision(
        byte[] bytes, ContentDigest expectedDigest, FullTextScreeningConductHeader header,
        FullTextExtractionAttempt? extractionAttempt = null)
    {
        var content = Content(bytes, expectedDigest, FullTextScreeningConductSchema.DecisionSchemaId);
        Exact(content,
            ["actor", "candidate_id", "conduct_id", "decided_at", "decision_id", "evidence", "kind", "ordinal", "policy_digest", "policy_id", "previous_digest", "rationale", "request_id", "source_decision_digests", "verdict"],
            ["exclusion_reason_code", "full_text_extraction_attempt_digest", "resolved_conflict_id", "supersedes_decision_digest"]);
        var decision = FullTextScreeningConductDecision.Create(
            header, Integer(content, "ordinal"), Digest(content, "previous_digest"), Text(content, "request_id"),
            Text(content, "candidate_id"), DecisionKind(Text(content, "kind")), Text(content, "verdict"),
            Actor(Object(content, "actor")), Text(content, "rationale"), Timestamp(content, "decided_at"),
            OptionalText(content, "exclusion_reason_code"), OptionalText(content, "supersedes_decision_digest"),
            OptionalText(content, "resolved_conflict_id"), Digests(content, "source_decision_digests"),
            Array(content, "evidence").Select(Evidence), extractionAttempt: extractionAttempt,
            extractionAttemptDigest: OptionalDigest(content, "full_text_extraction_attempt_digest"));
        RequireReproduction(bytes, Serialize(decision), decision.Digest, expectedDigest, "Full Text decision");
        return decision;
    }

    public static FullTextScreeningConductInvalidation RehydrateInvalidation(
        byte[] bytes, ContentDigest expectedDigest, FullTextScreeningConductHeader header)
    {
        var content = Content(bytes, expectedDigest, FullTextScreeningConductSchema.InvalidationSchemaId);
        Exact(content, ["actor", "affected_decision_digests", "conduct_id", "invalidated_at", "invalidation_id", "ordinal", "previous_digest", "reason", "source"]);
        var invalidation = FullTextScreeningConductInvalidation.Create(
            header, Integer(content, "ordinal"), Digest(content, "previous_digest"), Text(content, "invalidation_id"),
            Evidence(Object(content, "source")), Digests(content, "affected_decision_digests"), Actor(Object(content, "actor")),
            Text(content, "reason"), Timestamp(content, "invalidated_at"));
        RequireReproduction(bytes, Serialize(invalidation), invalidation.Digest, expectedDigest, "Full Text invalidation");
        return invalidation;
    }

    public static FullTextScreeningConductHandoff RehydrateHandoff(
        byte[] bytes, ContentDigest expectedDigest, FullTextScreeningConductJournal journal)
    {
        var content = Content(bytes, expectedDigest, FullTextScreeningConductSchema.HandoffSchemaId);
        Exact(content,
            ["admission_conduct_id", "admission_digest", "admission_handoff_id", "conduct_id", "created_at", "full_text_artifact_digest", "handoff_id", "journal_head_digest", "outcomes", "policy_digest"],
            ["full_text_extraction_attempt_digest"]);
        var handoff = journal.CreateHandoff(Text(content, "handoff_id"), Timestamp(content, "created_at"));
        RequireReproduction(bytes, Serialize(handoff), handoff.Digest, expectedDigest, "Full Text handoff");
        return handoff;
    }

    private static CanonicalJsonObject Content(byte[] bytes, ContentDigest digest, string schemaId)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (CanonicalJsonValue.FromJsonElement(document.RootElement) is not CanonicalJsonObject root ||
                !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(root)))
                throw Invalid("Full Text conduct record must use exact canonical JSON bytes.");
            return DigestEnvelope.RehydrateAndVerify(document.RootElement, digest, DigestScope.CanonicalJsonRecord,
                schemaId, FullTextScreeningConductSchema.SchemaVersion).Envelope.Content;
        }
        catch (ScreeningRuleException) { throw; }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw Invalid($"Full Text conduct rehydration failed: {exception.Message}");
        }
    }

    private static void RequireReproduction(byte[] original, byte[] reproduced, ContentDigest actual, ContentDigest expected, string label)
    {
        if (actual != expected || !original.SequenceEqual(reproduced))
            throw Invalid($"{label} digest or canonical bytes are not reproducible.");
    }

    private static void Exact(CanonicalJsonObject value, IEnumerable<string> required, IEnumerable<string>? optional = null)
    {
        var requiredSet = required.ToHashSet(StringComparer.Ordinal);
        var allowed = requiredSet.Concat(optional ?? []).ToHashSet(StringComparer.Ordinal);
        if (!requiredSet.IsSubsetOf(value.Properties.Keys) || value.Properties.Keys.Any(key => !allowed.Contains(key)))
            throw Invalid("Full Text conduct record has missing or unknown fields.");
    }

    private static ScreeningConductRoleAssignment Assignment(CanonicalJsonValue value)
    {
        var item = AsObject(value); Exact(item, ["actor_id", "role"]);
        return new ScreeningConductRoleAssignment(Text(item, "actor_id"), Text(item, "role"));
    }
    private static ScreeningExclusionReason Reason(CanonicalJsonValue value)
    {
        var item = AsObject(value); Exact(item, ["code", "stage"]);
        return new ScreeningExclusionReason(Text(item, "code"), Text(item, "stage"));
    }
    private static ScreeningConductActor Actor(CanonicalJsonObject item)
    {
        Exact(item, ["actor_id", "kind", "role"]);
        return new ScreeningConductActor(Text(item, "actor_id"), Text(item, "kind"), Text(item, "role"));
    }
    private static ScreeningConductEvidenceRef Evidence(CanonicalJsonValue value) => Evidence(AsObject(value));
    private static ScreeningConductEvidenceRef Evidence(CanonicalJsonObject item)
    {
        Exact(item, ["digest", "id", "kind"]);
        return new ScreeningConductEvidenceRef(Text(item, "kind"), Text(item, "id"), Digest(item, "digest"));
    }
    private static ScreeningConductDecisionKind DecisionKind(string value) => value switch
    {
        "review" => ScreeningConductDecisionKind.Review,
        "correction" => ScreeningConductDecisionKind.Correction,
        "adjudication" => ScreeningConductDecisionKind.Adjudication,
        _ => throw Invalid("Unknown Full Text conduct decision kind.")
    };
    private static byte[] Bytes(CanonicalJsonObject value) => CanonicalJsonSerializer.SerializeToUtf8Bytes(value);
    private static CanonicalJsonObject Object(CanonicalJsonObject root, string name) => AsObject(Value(root, name));
    private static CanonicalJsonObject AsObject(CanonicalJsonValue value) => value as CanonicalJsonObject ?? throw Invalid("Full Text conduct field must be an object.");
    private static IReadOnlyList<CanonicalJsonValue> Array(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonArray array ? array.Items : throw Invalid($"Full Text conduct field '{name}' must be an array.");
    private static string Text(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonString text ? text.Value : throw Invalid($"Full Text conduct field '{name}' must be text.");
    private static string? OptionalText(CanonicalJsonObject root, string name) => root.Properties.ContainsKey(name) ? Text(root, name) : null;
    private static int Integer(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonNumber number && int.TryParse(number.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result) ? result : throw Invalid($"Full Text conduct field '{name}' must be an integer.");
    private static ContentDigest Digest(CanonicalJsonObject root, string name) => ContentDigest.Parse(Text(root, name));
    private static ContentDigest? OptionalDigest(CanonicalJsonObject root, string name) => root.Properties.ContainsKey(name) ? Digest(root, name) : null;
    private static IReadOnlyList<string> Strings(CanonicalJsonObject root, string name) => Array(root, name).Select(value => value is CanonicalJsonString text ? text.Value : throw Invalid($"Full Text conduct array '{name}' must contain text.")).ToArray();
    private static IReadOnlyList<ContentDigest> Digests(CanonicalJsonObject root, string name) => Strings(root, name).Select(ContentDigest.Parse).ToArray();
    private static DateTimeOffset Timestamp(CanonicalJsonObject root, string name)
    {
        var value = Text(root, name); CanonicalTimestamp.ValidateCanonicalUtc(value);
        return DateTimeOffset.ParseExact(value, CanonicalTimestamp.DefaultUtcFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
    private static CanonicalJsonValue Value(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value) ? value : throw Invalid($"Full Text conduct field '{name}' is required.");
    private static ScreeningRuleException Invalid(string message) => new(FullTextScreeningConductErrorCodes.InvalidAuthorityChain, message);
}
