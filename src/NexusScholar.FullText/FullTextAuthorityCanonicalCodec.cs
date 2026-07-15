using System.Globalization;
using System.Text.Json;
using NexusScholar.Kernel;

namespace NexusScholar.FullText;

public static class FullTextAuthorityCanonicalCodec
{
    public const string AcquisitionSchemaVersion = "1.1.0";
    public const string ArtifactSchemaVersion = "1.1.0";

    public static byte[] Serialize(FullTextInput input) => SerializeEnvelope(
        FullTextSchemas.InputSchemaId,
        FullTextSchemas.SchemaVersion,
        Input(input ?? throw new ArgumentNullException(nameof(input))));

    public static byte[] Serialize(FullTextAcquisitionRecord acquisition) => SerializeEnvelope(
        FullTextSchemas.AcquisitionRecordSchemaId,
        AcquisitionSchemaVersion,
        Acquisition(acquisition ?? throw new ArgumentNullException(nameof(acquisition))));

    public static byte[] Serialize(FullTextArtifactEvidence artifact) => SerializeEnvelope(
        FullTextSchemas.ArtifactEvidenceSchemaId,
        ArtifactSchemaVersion,
        Artifact(artifact ?? throw new ArgumentNullException(nameof(artifact))));

    public static VerifiedFullTextChain Rehydrate(
        byte[] inputBytes,
        ContentDigest expectedInputDigest,
        byte[] acquisitionBytes,
        ContentDigest expectedAcquisitionDigest,
        byte[] artifactBytes,
        ContentDigest expectedArtifactDigest,
        byte[] acceptedBytes,
        long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(acceptedBytes);

        try
        {
            var inputContent = ParseEnvelope(
                inputBytes, expectedInputDigest, FullTextSchemas.InputSchemaId, FullTextSchemas.SchemaVersion);
            var input = ParseInput(inputContent);
            RequireReproduction(inputBytes, Serialize(input), "Full Text input");

            var acquisitionContent = ParseEnvelope(
                acquisitionBytes,
                expectedAcquisitionDigest,
                FullTextSchemas.AcquisitionRecordSchemaId,
                AcquisitionSchemaVersion);
            var acquisition = ParseAcquisition(acquisitionContent);
            RequireReproduction(acquisitionBytes, Serialize(acquisition), "Full Text acquisition");

            var artifactContent = ParseEnvelope(
                artifactBytes,
                expectedArtifactDigest,
                FullTextSchemas.ArtifactEvidenceSchemaId,
                ArtifactSchemaVersion);
            var artifact = ParseArtifact(artifactContent, acceptedBytes);
            RequireReproduction(artifactBytes, Serialize(artifact), "Full Text artifact evidence");

            return FullTextRehydrator.Rehydrate(
                new UnverifiedFullTextChain(input, acquisition, artifact, acceptedBytes, maximumBytes));
        }
        catch (FullTextRuleException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        {
            throw Rule($"Full Text canonical authority rehydration failed: {exception.Message}");
        }
    }

    private static CanonicalJsonObject Input(FullTextInput input)
    {
        var content = new CanonicalJsonObject()
            .Add("candidate_id", input.CandidateId)
            .Add("candidate_set_id", input.CandidateSetId)
            .Add("eligibility", input.Eligibility)
            .Add("input_id", input.InputId)
            .Add("non_claims", Strings(input.NonClaims))
            .Add("source_kind", input.SourceKind)
            .Add("source_refs", new CanonicalJsonArray(input.SourceRefs.Select(SourceRef)));
        AddOptional(content, "dedup_cluster_id", input.DedupClusterId);
        AddOptional(content, "dedup_result_id", input.DedupResultId);
        AddOptional(content, "screening_decision_id", input.ScreeningDecisionId);
        AddOptional(content, "screening_stage", input.ScreeningStage);
        AddOptional(content, "work_id", input.WorkId);
        return content;
    }

    private static CanonicalJsonObject Acquisition(FullTextAcquisitionRecord acquisition)
    {
        var content = new CanonicalJsonObject()
            .Add("acquired_at", CanonicalTimestamp.FormatUtc(acquisition.AcquiredAt))
            .Add("acquisition_id", acquisition.AcquisitionId)
            .Add("acquisition_kind", acquisition.AcquisitionKind)
            .Add("errors", Strings(acquisition.Errors))
            .Add("input_ref", Input(acquisition.InputRef))
            .Add("non_claims", Strings(acquisition.NonClaims))
            .Add("source_alias", acquisition.SourceAlias)
            .Add("source_attempts", new CanonicalJsonArray(acquisition.SourceAttempts.Select(SourceAttempt)))
            .Add("source_metadata", StringMap(acquisition.SourceMetadata))
            .Add("source_reference", acquisition.SourceReference)
            .Add("status", acquisition.Status)
            .Add("warnings", Strings(acquisition.Warnings));
        if (acquisition.AcquiredBy is not null) content.Add("acquired_by", Actor(acquisition.AcquiredBy));
        AddOptional(content, "artifact_evidence_id", acquisition.ArtifactEvidenceId);
        AddOptional(content, "doi_or_landing_page", acquisition.DoiOrLandingPage);
        AddOptional(content, "source_url", acquisition.SourceUrl);
        return content;
    }

    private static CanonicalJsonObject Artifact(FullTextArtifactEvidence artifact)
    {
        var content = new CanonicalJsonObject()
            .Add("acquisition_id", artifact.AcquisitionId)
            .Add("acquisition_kind", artifact.AcquisitionKind)
            .Add("artifact_id", artifact.ArtifactId)
            .Add("artifact_kind", artifact.ArtifactKind)
            .Add("candidate_id", artifact.CandidateId)
            .Add("errors", Strings(artifact.Errors))
            .Add("input_ref", Input(artifact.InputRef))
            .Add("media_type", artifact.MediaType)
            .Add("non_claims", Strings(artifact.NonClaims))
            .Add("raw_byte_digest", artifact.RawByteDigest)
            .Add("raw_byte_digest_scope", artifact.RawByteDigestScope)
            .Add("size_bytes", artifact.SizeBytes)
            .Add("source_alias", artifact.SourceAlias)
            .Add("source_metadata", StringMap(artifact.SourceMetadata))
            .Add("validation_status", artifact.ValidationStatus)
            .Add("warnings", Strings(artifact.Warnings));
        AddOptional(content, "candidate_set_id", artifact.CandidateSetId);
        AddOptional(content, "dedup_cluster_id", artifact.DedupClusterId);
        AddOptional(content, "logical_path", artifact.LogicalPath);
        AddOptional(content, "original_file_name", artifact.OriginalFileName);
        AddOptional(content, "screening_decision_id", artifact.ScreeningDecisionId);
        AddOptional(content, "source_reference", artifact.SourceReference);
        AddOptional(content, "work_id", artifact.WorkId);
        return content;
    }

    private static CanonicalJsonObject SourceRef(FullTextSourceRef value) => new CanonicalJsonObject()
        .Add("ref_id", value.RefId)
        .Add("ref_kind", value.RefKind);

    private static CanonicalJsonObject Actor(FullTextActor value) => new CanonicalJsonObject()
        .Add("actor_id", value.ActorId)
        .Add("actor_kind", value.ActorKind);

    private static CanonicalJsonObject SourceAttempt(FullTextSourceAttempt attempt)
    {
        var content = new CanonicalJsonObject()
            .Add("acquisition_kind", attempt.AcquisitionKind)
            .Add("attempt_id", attempt.AttemptId)
            .Add("attempt_order", attempt.AttemptOrder)
            .Add("source_alias", attempt.SourceAlias)
            .Add("source_metadata", StringMap(attempt.SourceMetadata))
            .Add("status", attempt.Status);
        AddOptional(content, "artifact_evidence_id", attempt.ArtifactEvidenceId);
        AddOptional(content, "artifact_kind", attempt.ArtifactKind);
        AddOptional(content, "error_category", attempt.ErrorCategory);
        AddOptional(content, "error_message", attempt.ErrorMessage);
        if (attempt.HttpStatus is not null) content.Add("http_status", attempt.HttpStatus.Value);
        AddOptional(content, "media_type", attempt.MediaType);
        AddOptional(content, "source_reference", attempt.SourceReference);
        AddOptional(content, "source_url", attempt.SourceUrl);
        return content;
    }

    private static FullTextInput ParseInput(CanonicalJsonObject content)
    {
        RequireExact(content,
            ["candidate_id", "candidate_set_id", "eligibility", "input_id", "non_claims", "source_kind", "source_refs"],
            ["dedup_cluster_id", "dedup_result_id", "screening_decision_id", "screening_stage", "work_id"]);
        return new FullTextInput(
            Text(content, "input_id"),
            Text(content, "source_kind"),
            Text(content, "candidate_set_id"),
            Text(content, "candidate_id"),
            Text(content, "eligibility"),
            Array(content, "source_refs").Select(ParseSourceRef).ToArray(),
            OptionalText(content, "screening_decision_id"),
            OptionalText(content, "screening_stage"),
            OptionalText(content, "dedup_result_id"),
            OptionalText(content, "dedup_cluster_id"),
            OptionalText(content, "work_id"),
            ParseStrings(content, "non_claims"));
    }

    private static FullTextAcquisitionRecord ParseAcquisition(CanonicalJsonObject content)
    {
        RequireExact(content,
            ["acquired_at", "acquisition_id", "acquisition_kind", "errors", "input_ref", "non_claims", "source_alias", "source_attempts", "source_metadata", "source_reference", "status", "warnings"],
            ["acquired_by", "artifact_evidence_id", "doi_or_landing_page", "source_url"]);
        var attempts = Array(content, "source_attempts").Select(ParseSourceAttempt).ToArray();
        if (!attempts.Select((attempt, index) => attempt.AttemptOrder == index + 1).All(value => value))
            throw Rule("Full Text source attempts must remain in contiguous persisted order.");
        return new FullTextAcquisitionRecord(
            Text(content, "acquisition_id"),
            ParseInput(Object(content, "input_ref")),
            Text(content, "acquisition_kind"),
            Text(content, "source_alias"),
            Text(content, "source_reference"),
            content.Properties.ContainsKey("acquired_by") ? ParseActor(Object(content, "acquired_by")) : null,
            Timestamp(content, "acquired_at"),
            Text(content, "status"),
            attempts,
            OptionalText(content, "source_url"),
            OptionalText(content, "doi_or_landing_page"),
            ParseStringMap(content, "source_metadata"),
            OptionalText(content, "artifact_evidence_id"),
            ParseStrings(content, "warnings"),
            ParseStrings(content, "errors"),
            ParseStrings(content, "non_claims"));
    }

    private static FullTextArtifactEvidence ParseArtifact(CanonicalJsonObject content, byte[] acceptedBytes)
    {
        RequireExact(content,
            ["acquisition_id", "acquisition_kind", "artifact_id", "artifact_kind", "candidate_id", "errors", "input_ref", "media_type", "non_claims", "raw_byte_digest", "raw_byte_digest_scope", "size_bytes", "source_alias", "source_metadata", "validation_status", "warnings"],
            ["candidate_set_id", "dedup_cluster_id", "logical_path", "original_file_name", "screening_decision_id", "source_reference", "work_id"]);
        return new FullTextArtifactEvidence(
            Text(content, "artifact_id"),
            ParseInput(Object(content, "input_ref")),
            Text(content, "candidate_id"),
            Text(content, "acquisition_id"),
            Text(content, "acquisition_kind"),
            Text(content, "source_alias"),
            Text(content, "artifact_kind"),
            Text(content, "media_type"),
            Long(content, "size_bytes"),
            Text(content, "raw_byte_digest"),
            Text(content, "raw_byte_digest_scope"),
            Text(content, "validation_status"),
            acceptedBytes,
            OptionalText(content, "candidate_set_id"),
            OptionalText(content, "screening_decision_id"),
            OptionalText(content, "work_id"),
            OptionalText(content, "dedup_cluster_id"),
            OptionalText(content, "source_reference"),
            ParseStringMap(content, "source_metadata"),
            OptionalText(content, "logical_path"),
            OptionalText(content, "original_file_name"),
            ParseStrings(content, "warnings"),
            ParseStrings(content, "errors"),
            ParseStrings(content, "non_claims"));
    }

    private static FullTextSourceRef ParseSourceRef(CanonicalJsonValue value)
    {
        var content = AsObject(value);
        RequireExact(content, ["ref_id", "ref_kind"]);
        return new FullTextSourceRef(Text(content, "ref_kind"), Text(content, "ref_id"));
    }

    private static FullTextActor ParseActor(CanonicalJsonObject content)
    {
        RequireExact(content, ["actor_id", "actor_kind"]);
        return new FullTextActor(Text(content, "actor_id"), Text(content, "actor_kind"));
    }

    private static FullTextSourceAttempt ParseSourceAttempt(CanonicalJsonValue value)
    {
        var content = AsObject(value);
        RequireExact(content,
            ["acquisition_kind", "attempt_id", "attempt_order", "source_alias", "source_metadata", "status"],
            ["artifact_evidence_id", "artifact_kind", "error_category", "error_message", "http_status", "media_type", "source_reference", "source_url"]);
        return new FullTextSourceAttempt(
            Text(content, "attempt_id"),
            Text(content, "source_alias"),
            Integer(content, "attempt_order"),
            Text(content, "acquisition_kind"),
            Text(content, "status"),
            OptionalText(content, "source_url"),
            OptionalText(content, "source_reference"),
            OptionalText(content, "artifact_kind"),
            OptionalText(content, "media_type"),
            content.Properties.ContainsKey("http_status") ? Integer(content, "http_status") : null,
            OptionalText(content, "error_category"),
            OptionalText(content, "error_message"),
            ParseStringMap(content, "source_metadata"),
            OptionalText(content, "artifact_evidence_id"));
    }

    private static CanonicalJsonObject ParseEnvelope(byte[] bytes, ContentDigest expectedDigest, string schemaId, string schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var document = JsonDocument.Parse(bytes);
        if (CanonicalJsonValue.FromJsonElement(document.RootElement) is not CanonicalJsonObject root ||
            !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(root)))
            throw Rule("Full Text authority bytes must be exact canonical JSON.");
        return DigestEnvelope.RehydrateAndVerify(
            document.RootElement, expectedDigest, DigestScope.CanonicalJsonRecord, schemaId, schemaVersion).Envelope.Content;
    }

    private static byte[] SerializeEnvelope(string schemaId, string schemaVersion, CanonicalJsonObject content) =>
        new DigestEnvelope(DigestScope.CanonicalJsonRecord, schemaId, schemaVersion, content).ToCanonicalJsonBytes();

    private static void RequireReproduction(byte[] original, byte[] reproduced, string kind)
    {
        if (!original.SequenceEqual(reproduced)) throw Rule($"{kind} did not reproduce its canonical bytes.");
    }

    private static void RequireExact(CanonicalJsonObject content, IEnumerable<string> required, IEnumerable<string>? optional = null)
    {
        var requiredSet = required.ToHashSet(StringComparer.Ordinal);
        var allowed = requiredSet.Concat(optional ?? System.Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        if (!requiredSet.IsSubsetOf(content.Properties.Keys) || content.Properties.Keys.Any(key => !allowed.Contains(key)))
            throw Rule("Full Text canonical record has missing or unknown fields.");
    }

    private static void AddOptional(CanonicalJsonObject content, string name, string? value)
    {
        if (value is not null) content.Add(name, value);
    }

    private static CanonicalJsonArray Strings(IEnumerable<string> values) =>
        new(values.Select(CanonicalJsonValue.From));

    private static CanonicalJsonObject StringMap(IEnumerable<KeyValuePair<string, string>> values)
    {
        var result = new CanonicalJsonObject();
        foreach (var pair in values) result.Add(pair.Key, pair.Value);
        return result;
    }

    private static CanonicalJsonObject Object(CanonicalJsonObject root, string name) => AsObject(Value(root, name));
    private static CanonicalJsonObject AsObject(CanonicalJsonValue value) => value as CanonicalJsonObject
        ?? throw Rule("Full Text canonical field must be an object.");
    private static IReadOnlyList<CanonicalJsonValue> Array(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonArray array
        ? array.Items : throw Rule($"Full Text canonical field '{name}' must be an array.");
    private static string Text(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonString text
        ? text.Value : throw Rule($"Full Text canonical field '{name}' must be text.");
    private static string? OptionalText(CanonicalJsonObject root, string name) => root.Properties.ContainsKey(name) ? Text(root, name) : null;
    private static IReadOnlyList<string> ParseStrings(CanonicalJsonObject root, string name) => Array(root, name).Select(value => value is CanonicalJsonString text
        ? text.Value : throw Rule($"Full Text canonical array '{name}' must contain text.")).ToArray();
    private static IReadOnlyDictionary<string, string> ParseStringMap(CanonicalJsonObject root, string name) => Object(root, name).Properties.ToDictionary(
        pair => pair.Key,
        pair => pair.Value is CanonicalJsonString text ? text.Value : throw Rule($"Full Text canonical map '{name}' must contain text values."),
        StringComparer.Ordinal);
    private static int Integer(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonNumber number &&
        int.TryParse(number.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
            ? result : throw Rule($"Full Text canonical field '{name}' must be an integer.");
    private static long Long(CanonicalJsonObject root, string name) => Value(root, name) is CanonicalJsonNumber number &&
        long.TryParse(number.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result)
            ? result : throw Rule($"Full Text canonical field '{name}' must be an integer.");
    private static DateTimeOffset Timestamp(CanonicalJsonObject root, string name)
    {
        var value = Text(root, name);
        CanonicalTimestamp.ValidateCanonicalUtc(value);
        return DateTimeOffset.ParseExact(value, CanonicalTimestamp.DefaultUtcFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
    private static CanonicalJsonValue Value(CanonicalJsonObject root, string name) => root.Properties.TryGetValue(name, out var value)
        ? value : throw Rule($"Full Text canonical field '{name}' is required.");
    private static FullTextRuleException Rule(string message) => new(FullTextErrorCodes.InvalidAuthorityChain, message);
}
