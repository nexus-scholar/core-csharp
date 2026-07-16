using System.Text.Json;
using NexusScholar.Kernel;

namespace NexusScholar.Reporting;

public sealed record PersistedReviewReportVerification(ContentDigest ReportDigest, ContentDigest SliceDigest);

public static class PersistedReportingVerifier
{
    private static readonly string[] SliceFields =
    [
        "amendment_digests", "deduplication_result_digest", "deduplication_result_id", "deviation_digests",
        "full_text_cases", "project_revision", "protocol_content_digest", "protocol_version_id",
        "provenance_event_digests", "rapid_review_profile_digest", "screening_binding_digest",
        "screening_handoff_digest", "screening_policy_digest", "snapshot_id", "snapshot_record_digest",
        "waiver_digests", "workflow_digest", "workflow_id", "workspace_cut_digest", "workspace_generations", "workspace_id"
    ];
    private static readonly string[] ReportFields =
    [
        "audit_counts", "bindings", "counts", "disclosures", "full_text_exclusion_reasons", "non_claims",
        "title_abstract_exclusion_reasons"
    ];

    public static ContentDigest VerifySlice(byte[] bytes, ContentDigest expectedDigest)
    {
        using var document = VerifyEnvelope(bytes, expectedDigest, ReportingSchemas.SliceBindingId);
        var content = document.RootElement.GetProperty("content");
        Exact(content, SliceFields, optional: ["rapid_review_profile_digest"]);
        foreach (var name in new[]
                 {
                     "protocol_content_digest", "workflow_digest", "deduplication_result_digest", "snapshot_record_digest",
                     "screening_binding_digest", "screening_policy_digest", "screening_handoff_digest", "workspace_cut_digest"
                 }) Digest(content, name);
        if (content.TryGetProperty("rapid_review_profile_digest", out var profile)) _ = ContentDigest.Parse(profile.GetString()!);
        foreach (var name in new[] { "waiver_digests", "amendment_digests", "deviation_digests", "provenance_event_digests" })
            DigestArray(content.GetProperty(name));
        RequiredText(content, "protocol_version_id"); RequiredText(content, "workflow_id");
        RequiredText(content, "deduplication_result_id"); RequiredText(content, "snapshot_id"); RequiredText(content, "workspace_id");
        if (content.GetProperty("project_revision").GetInt64() < 0) throw Invalid("Slice project revision cannot be negative.");
        ValidateWorkspaceGenerations(content.GetProperty("workspace_generations"));
        ValidateFullTextBindings(content.GetProperty("full_text_cases"));
        return expectedDigest;
    }

    public static PersistedReviewReportVerification VerifyReport(byte[] bytes, ContentDigest expectedDigest)
    {
        using var document = VerifyEnvelope(bytes, expectedDigest, ReportingSchemas.ReportId);
        var content = document.RootElement.GetProperty("content");
        Exact(content, ReportFields);
        var bindings = content.GetProperty("bindings");
        Exact(bindings,
        [
            "amendment_digests", "deduplication_result_digest", "deviation_digests", "full_text_cases",
            "protocol_content_digest", "provenance_event_digests", "rapid_review_profile_digest", "screening_binding_digest",
            "screening_handoff_digest", "slice_digest", "snapshot_record_digest", "waiver_digests", "workflow_digest",
            "workspace_cut_digest"
        ], optional: ["rapid_review_profile_digest"]);
        var sliceDigest = Digest(bindings, "slice_digest");
        foreach (var name in new[]
                 {
                     "protocol_content_digest", "workflow_digest", "deduplication_result_digest", "snapshot_record_digest",
                     "screening_binding_digest", "screening_handoff_digest", "workspace_cut_digest"
                 }) Digest(bindings, name);
        if (bindings.TryGetProperty("rapid_review_profile_digest", out var profile)) _ = ContentDigest.Parse(profile.GetString()!);
        foreach (var name in new[] { "waiver_digests", "amendment_digests", "deviation_digests", "provenance_event_digests" })
            DigestArray(bindings.GetProperty(name));
        ValidateFullTextBindings(bindings.GetProperty("full_text_cases"));

        var counts = content.GetProperty("counts");
        Exact(counts, ["duplicates_consolidated", "full_text_excluded", "full_text_included", "identified", "included", "post_dedup", "title_abstract_excluded", "title_abstract_included"]);
        var identified = NonNegative(counts, "identified");
        var duplicates = NonNegative(counts, "duplicates_consolidated");
        var postDedup = NonNegative(counts, "post_dedup");
        var titleIncluded = NonNegative(counts, "title_abstract_included");
        var titleExcluded = NonNegative(counts, "title_abstract_excluded");
        var fullIncluded = NonNegative(counts, "full_text_included");
        var fullExcluded = NonNegative(counts, "full_text_excluded");
        var included = NonNegative(counts, "included");
        if (identified - duplicates != postDedup || titleIncluded + titleExcluded != postDedup ||
            fullIncluded + fullExcluded != titleIncluded || included != fullIncluded)
            throw Invalid("Persisted report counts do not conserve.");
        if (ReasonTotal(content.GetProperty("title_abstract_exclusion_reasons")) != titleExcluded ||
            ReasonTotal(content.GetProperty("full_text_exclusion_reasons")) != fullExcluded)
            throw Invalid("Persisted report exclusion reason totals do not conserve.");
        ValidateAudit(content.GetProperty("audit_counts"));
        TextArray(content.GetProperty("disclosures"), requireNonEmpty: false);
        TextArray(content.GetProperty("non_claims"), requireNonEmpty: true);
        return new PersistedReviewReportVerification(expectedDigest, sliceDigest);
    }

    private static JsonDocument VerifyEnvelope(byte[] bytes, ContentDigest digest, string schema)
    {
        var document = JsonDocument.Parse(bytes);
        try
        {
            var verified = DigestEnvelope.RehydrateAndVerify(document.RootElement, digest,
                DigestScope.CanonicalJsonRecord, schema, ReportingSchemas.Version);
            if (!bytes.SequenceEqual(verified.Envelope.ToCanonicalJsonBytes())) throw Invalid("Persisted Reporting record is not canonical.");
            return document;
        }
        catch { document.Dispose(); throw; }
    }

    private static void ValidateAudit(JsonElement audit)
    {
        Exact(audit, ["adjudications", "conflicts", "corrections", "invalidations"]);
        foreach (var name in new[] { "adjudications", "conflicts", "corrections", "invalidations" }) NonNegative(audit, name);
    }

    private static int ReasonTotal(JsonElement reasons)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var total = 0;
        foreach (var reason in reasons.EnumerateArray())
        {
            Exact(reason, ["code", "count"]);
            if (!codes.Add(RequiredText(reason, "code"))) throw Invalid("Persisted report reason codes are duplicated.");
            total = checked(total + NonNegative(reason, "count"));
        }
        return total;
    }

    private static void ValidateWorkspaceGenerations(JsonElement values)
    {
        var roles = new HashSet<(string Role, string? CandidateId)>();
        foreach (var item in values.EnumerateArray())
        {
            Exact(item, ["candidate_id", "generation_id", "manifest_digest", "role"], optional: ["candidate_id"]);
            var candidate = item.TryGetProperty("candidate_id", out var value) ? value.GetString() : null;
            if (!roles.Add((RequiredText(item, "role"), candidate))) throw Invalid("Workspace generation roles are duplicated.");
            RequiredText(item, "generation_id"); Digest(item, "manifest_digest");
        }
        if (roles.Count == 0) throw Invalid("Workspace generations are required.");
    }

    private static void ValidateFullTextBindings(JsonElement values)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in values.EnumerateArray())
        {
            Exact(item,
            [
                "admission_digest", "artifact_digest", "candidate_id", "conduct_policy_digest",
                "extraction_attempt_digest", "handoff_digest"
            ], optional: ["extraction_attempt_digest"]);
            if (!candidates.Add(RequiredText(item, "candidate_id"))) throw Invalid("Full Text report candidates are duplicated.");
            foreach (var name in new[] { "admission_digest", "artifact_digest", "conduct_policy_digest", "handoff_digest" })
                Digest(item, name);
            if (item.TryGetProperty("extraction_attempt_digest", out var extraction)) _ = ContentDigest.Parse(extraction.GetString()!);
        }
    }

    private static void DigestArray(JsonElement values)
    {
        var observed = values.EnumerateArray().Select(value => ContentDigest.Parse(value.GetString()!)).ToArray();
        if (observed.Distinct().Count() != observed.Length) throw Invalid("Digest array contains duplicates.");
    }

    private static void TextArray(JsonElement values, bool requireNonEmpty)
    {
        var observed = values.EnumerateArray().Select(value => value.GetString()).ToArray();
        if (requireNonEmpty && observed.Length == 0 || observed.Any(string.IsNullOrWhiteSpace) ||
            observed.Distinct(StringComparer.Ordinal).Count() != observed.Length)
            throw Invalid("Text array is empty, blank, or duplicated.");
    }

    private static int NonNegative(JsonElement root, string name)
    {
        var value = root.GetProperty(name).GetInt32();
        return value >= 0 ? value : throw Invalid($"{name} cannot be negative.");
    }

    private static ContentDigest Digest(JsonElement root, string name) => ContentDigest.Parse(RequiredText(root, name));
    private static string RequiredText(JsonElement root, string name) => root.GetProperty(name).GetString() is { Length: > 0 } value
        ? value : throw Invalid($"{name} is required.");

    private static void Exact(JsonElement root, IEnumerable<string> allowed, IEnumerable<string>? optional = null)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        var optionalSet = (optional ?? []).ToHashSet(StringComparer.Ordinal);
        var names = root.EnumerateObject().Select(property => property.Name).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() != names.Length || names.Any(name => !allowedSet.Contains(name)) ||
            allowedSet.Except(optionalSet).Any(required => !names.Contains(required, StringComparer.Ordinal)))
            throw Invalid("Persisted Reporting record fields are missing, duplicated, or unknown.");
    }

    private static ReportingRuleException Invalid(string message) => new(ReportingErrorCodes.NonCanonicalRecord, message);
}
