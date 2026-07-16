using System.Text.Json;
using NexusScholar.Kernel;

namespace NexusScholar.Bundles;

public static class BundleV2Constants
{
    public const string SchemaId = "nexus.review-bundle.manifest";
    public const string SchemaVersion = "2.0.0";
    public const string ManifestPath = "manifest.json";
}

public static class BundleV2ErrorCodes
{
    public const string InvalidManifest = "invalid-bundle-v2-manifest";
    public const string NonCanonicalManifest = "non-canonical-bundle-v2-manifest";
    public const string MissingInventory = "missing-bundle-v2-inventory";
    public const string ExtraInventory = "extra-bundle-v2-inventory";
    public const string AlteredArtifact = "altered-bundle-v2-artifact";
    public const string MisScopedDigest = "mis-scoped-bundle-v2-digest";
    public const string InvalidPath = "invalid-bundle-v2-path";
    public const string DuplicateEntry = "duplicate-bundle-v2-entry";
    public const string ForeignGeneration = "foreign-bundle-v2-generation";
    public const string InvalidExternalReference = "invalid-bundle-v2-external-reference";
}

public sealed class BundleV2Exception : InvalidOperationException
{
    public BundleV2Exception(string category, string message) : base(message) => Category = category;
    public string Category { get; }
}

public sealed record BundleV2ScopedDigest(string Scope, ContentDigest Value);
public sealed record BundleV2SourceBinding(string Role, string GenerationId, BundleV2ScopedDigest ManifestDigest, string? CandidateId = null);

public abstract record BundleV2Entry(string EntryKind, string ArtifactRole, BundleV2SourceBinding SourceBinding);

public sealed record BundleV2EmbeddedEntry(
    int Ordinal,
    string LogicalPath,
    long SizeBytes,
    BundleV2ScopedDigest ContentDigest,
    string ArtifactRole,
    BundleV2SourceBinding SourceBinding) : BundleV2Entry("embedded", ArtifactRole, SourceBinding);

public sealed record BundleV2ExternalEntry(
    string ReferenceId,
    string ReferenceKind,
    string Locator,
    string AvailabilityNote,
    BundleV2ScopedDigest? ExpectedContentDigest,
    string ArtifactRole,
    BundleV2SourceBinding SourceBinding) : BundleV2Entry("external", ArtifactRole, SourceBinding);

public sealed class VerifiedReviewBundleV2
{
    internal VerifiedReviewBundleV2(string bundleId, BundleV2ScopedDigest reportDigest, string workspaceId, long projectRevision,
        BundleV2ScopedDigest workspaceCutDigest,
        IReadOnlyList<BundleV2SourceBinding> sources, IReadOnlyList<BundleV2Entry> entries, IReadOnlyList<string> nonClaims,
        DigestEnvelope envelope)
    {
        BundleId = bundleId; ReportDigest = reportDigest; WorkspaceId = workspaceId; ProjectRevision = projectRevision;
        WorkspaceCutDigest = workspaceCutDigest with { };
        SourceGenerations = Array.AsReadOnly(sources.Select(item => item with { }).ToArray());
        Entries = Array.AsReadOnly(entries.Select(Clone).ToArray());
        NonClaims = Array.AsReadOnly(nonClaims.ToArray()); DigestEnvelope = envelope;
    }
    public string BundleId { get; }
    public BundleV2ScopedDigest ReportDigest { get; }
    public string WorkspaceId { get; }
    public long ProjectRevision { get; }
    public BundleV2ScopedDigest WorkspaceCutDigest { get; }
    public IReadOnlyList<BundleV2SourceBinding> SourceGenerations { get; }
    public IReadOnlyList<BundleV2Entry> Entries { get; }
    public IReadOnlyList<string> NonClaims { get; }
    public bool IsSelfContained => Entries.All(item => item is BundleV2EmbeddedEntry);
    public DigestEnvelope DigestEnvelope { get; }
    public ContentDigest ManifestDigest => DigestEnvelope.ComputeDigest();
    public CanonicalJsonObject ToCanonicalJson() => DigestEnvelope.ToCanonicalJsonObject();
    private static BundleV2Entry Clone(BundleV2Entry item) => item switch
    {
        BundleV2EmbeddedEntry embedded => embedded with { SourceBinding = embedded.SourceBinding with { }, ContentDigest = embedded.ContentDigest with { } },
        BundleV2ExternalEntry external => external with { SourceBinding = external.SourceBinding with { }, ExpectedContentDigest = external.ExpectedContentDigest is null ? null : external.ExpectedContentDigest with { } },
        _ => throw new BundleV2Exception(BundleV2ErrorCodes.InvalidManifest, "Unknown Bundle v2 entry kind.")
    };
}

public static class ReviewBundleV2Authority
{
    public static VerifiedReviewBundleV2 Create(string bundleId, BundleV2ScopedDigest reportDigest, string workspaceId, long projectRevision,
        BundleV2ScopedDigest workspaceCutDigest,
        IEnumerable<BundleV2SourceBinding> sourceGenerations, IEnumerable<BundleV2Entry> entries, IEnumerable<string> nonClaims)
    {
        if (!CanonicalRecord(reportDigest) || !CanonicalRecord(workspaceCutDigest) || projectRevision < 0)
            throw Invalid(BundleV2ErrorCodes.MisScopedDigest, "Bundle report and workspace cut require canonical-json-record digests.");
        var sources = (sourceGenerations ?? throw new ArgumentNullException(nameof(sourceGenerations))).Select(item => item with
        {
            Role = Required(item.Role),
            GenerationId = Required(item.GenerationId),
            CandidateId = item.CandidateId is null ? null : Required(item.CandidateId)
        }).OrderBy(item => item.Role, StringComparer.Ordinal).ThenBy(item => item.CandidateId, StringComparer.Ordinal).ThenBy(item => item.GenerationId, StringComparer.Ordinal).ToArray();
        if (sources.Length == 0 || sources.Any(item => !CanonicalRecord(item.ManifestDigest)) || sources.Select(item => (item.Role, item.CandidateId)).Distinct().Count() != sources.Length)
            throw Invalid(BundleV2ErrorCodes.InvalidManifest, "Bundle source generations must be unique and digest-bound.");
        var sourceSet = sources.Select(item => (item.Role, item.GenerationId, item.CandidateId, item.ManifestDigest.Scope, item.ManifestDigest.Value)).ToHashSet();
        var normalized = (entries ?? throw new ArgumentNullException(nameof(entries))).Select(item => Normalize(item, sourceSet)).ToArray();
        var embedded = normalized.OfType<BundleV2EmbeddedEntry>().OrderBy(item => item.Ordinal).ToArray();
        var external = normalized.OfType<BundleV2ExternalEntry>().OrderBy(item => item.ReferenceId, StringComparer.Ordinal).ToArray();
        if (embedded.Select(item => item.Ordinal).SequenceEqual(Enumerable.Range(1, embedded.Length)) is false ||
            embedded.Select(item => item.LogicalPath).Distinct(StringComparer.Ordinal).Count() != embedded.Length ||
            external.Select(item => item.ReferenceId).Distinct(StringComparer.Ordinal).Count() != external.Length)
            throw Invalid(BundleV2ErrorCodes.DuplicateEntry, "Bundle entries require unique ordinal paths and external reference ids.");
        if (!embedded.Any(item => item.ArtifactRole == "canonical-report"))
            throw Invalid(BundleV2ErrorCodes.InvalidManifest, "Bundle v2 must embed its canonical report artifact.");
        var claims = (nonClaims ?? throw new ArgumentNullException(nameof(nonClaims))).Select(Required).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (claims.Length == 0) throw Invalid(BundleV2ErrorCodes.InvalidManifest, "Bundle v2 requires explicit non-claims.");
        var orderedEntries = embedded.Cast<BundleV2Entry>().Concat(external).ToArray();
        var content = BuildContent(Required(bundleId), reportDigest, Required(workspaceId), projectRevision, workspaceCutDigest, sources, orderedEntries, claims);
        var envelope = new DigestEnvelope(DigestScope.BundleManifest, BundleV2Constants.SchemaId, BundleV2Constants.SchemaVersion, content);
        return new VerifiedReviewBundleV2(bundleId.Trim(), reportDigest, workspaceId.Trim(), projectRevision, workspaceCutDigest, sources, orderedEntries, claims, envelope);
    }

    private static BundleV2Entry Normalize(BundleV2Entry item, HashSet<(string Role, string GenerationId, string? CandidateId, string Scope, ContentDigest Value)> sources)
    {
        ArgumentNullException.ThrowIfNull(item);
        var source = item.SourceBinding with
        {
            Role = Required(item.SourceBinding.Role),
            GenerationId = Required(item.SourceBinding.GenerationId),
            CandidateId = item.SourceBinding.CandidateId is null ? null : Required(item.SourceBinding.CandidateId)
        };
        if (!ValidScoped(source.ManifestDigest) || !sources.Contains((source.Role, source.GenerationId, source.CandidateId, source.ManifestDigest.Scope, source.ManifestDigest.Value)))
            throw Invalid(BundleV2ErrorCodes.ForeignGeneration, "Bundle entry source generation is foreign.");
        var role = Required(item.ArtifactRole);
        if (item is BundleV2EmbeddedEntry embedded)
        {
            var path = NormalizePath(embedded.LogicalPath);
            if (embedded.ContentDigest.Scope != DigestScope.RawArtifactBytes.ToString() || !embedded.ContentDigest.Value.IsValid)
                throw Invalid(BundleV2ErrorCodes.MisScopedDigest, "Embedded Bundle entry requires a raw-artifact-bytes digest.");
            if (path == BundleV2Constants.ManifestPath || embedded.Ordinal <= 0 || embedded.SizeBytes < 0)
                throw Invalid(BundleV2ErrorCodes.InvalidManifest, "Embedded Bundle entry path, ordinal, size, or digest is invalid.");
            return embedded with { LogicalPath = path, ArtifactRole = role, SourceBinding = source, ContentDigest = embedded.ContentDigest with { } };
        }
        if (item is BundleV2ExternalEntry external)
        {
            var locator = Required(external.Locator);
            var hasUri = Uri.TryCreate(locator, UriKind.Absolute, out var uri);
            if (Path.IsPathRooted(locator) || locator.Contains("..", StringComparison.Ordinal) || locator.Contains('@', StringComparison.Ordinal) ||
                hasUri && (!AllowedLocatorScheme(uri!) || !string.IsNullOrEmpty(uri!.UserInfo) || !string.IsNullOrEmpty(uri.Query)) ||
                external.ExpectedContentDigest is not null && !ValidScoped(external.ExpectedContentDigest) ||
                ContainsCredential(locator))
                throw Invalid(BundleV2ErrorCodes.InvalidExternalReference, "External reference locator or expected digest is unsafe.");
            return external with
            {
                ReferenceId = Required(external.ReferenceId),
                ReferenceKind = Required(external.ReferenceKind),
                Locator = locator,
                AvailabilityNote = Required(external.AvailabilityNote),
                ArtifactRole = role,
                SourceBinding = source,
                ExpectedContentDigest = external.ExpectedContentDigest is null ? null : external.ExpectedContentDigest with { }
            };
        }
        throw Invalid(BundleV2ErrorCodes.InvalidManifest, "Unknown Bundle v2 entry kind.");
    }

    internal static CanonicalJsonObject BuildContent(string bundleId, BundleV2ScopedDigest reportDigest, string workspaceId, long projectRevision,
        BundleV2ScopedDigest workspaceCutDigest,
        IEnumerable<BundleV2SourceBinding> sources, IEnumerable<BundleV2Entry> entries, IEnumerable<string> nonClaims) =>
        new CanonicalJsonObject().Add("bundle_id", bundleId).Add("report_digest", DigestJson(reportDigest)).Add("workspace_id", workspaceId)
            .Add("project_revision", projectRevision).Add("workspace_cut_digest", DigestJson(workspaceCutDigest))
            .Add("source_generations", CanonicalJsonValue.Array(sources.Select(SourceJson).ToArray()))
            .Add("entries", CanonicalJsonValue.Array(entries.Select(EntryJson).ToArray()))
            .Add("non_claims", CanonicalJsonValue.Array(nonClaims.Select(CanonicalJsonValue.From).ToArray()));

    private static CanonicalJsonObject EntryJson(BundleV2Entry item)
    {
        var value = new CanonicalJsonObject().Add("entry_kind", item.EntryKind).Add("artifact_role", item.ArtifactRole)
            .Add("source_binding", SourceJson(item.SourceBinding));
        if (item is BundleV2EmbeddedEntry embedded)
            return value.Add("ordinal", embedded.Ordinal).Add("logical_path", embedded.LogicalPath).Add("size_bytes", embedded.SizeBytes)
                .Add("content_digest", DigestJson(embedded.ContentDigest));
        var external = (BundleV2ExternalEntry)item;
        value.Add("reference_id", external.ReferenceId).Add("reference_kind", external.ReferenceKind).Add("locator", external.Locator)
            .Add("availability_note", external.AvailabilityNote);
        if (external.ExpectedContentDigest is not null) value.Add("expected_content_digest", DigestJson(external.ExpectedContentDigest));
        return value;
    }
    private static CanonicalJsonObject DigestJson(BundleV2ScopedDigest digest) => new CanonicalJsonObject().Add("scope", digest.Scope).Add("value", digest.Value.ToString());
    private static CanonicalJsonObject SourceJson(BundleV2SourceBinding item)
    {
        var value = new CanonicalJsonObject().Add("role", item.Role).Add("generation_id", item.GenerationId).Add("manifest_digest", DigestJson(item.ManifestDigest));
        if (item.CandidateId is not null) value.Add("candidate_id", item.CandidateId);
        return value;
    }
    private static bool ValidScoped(BundleV2ScopedDigest digest) => digest is not null && DigestScope.TryParse(digest.Scope, out _) && digest.Value.IsValid;
    private static bool CanonicalRecord(BundleV2ScopedDigest digest) => ValidScoped(digest) && digest.Scope == DigestScope.CanonicalJsonRecord.ToString();
    private static bool ContainsCredential(string value) => new[] { "token=", "key=", "secret=", "password=" }
        .Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    private static bool AllowedLocatorScheme(Uri uri) => uri.Scheme is "https" or "http" or "doi" or "urn" or "arxiv" or "repository";
    private static string NormalizePath(string value)
    {
        try { return BundleArtifactPath.Normalize(value); }
        catch (ArgumentException error) { throw Invalid(BundleV2ErrorCodes.InvalidPath, error.Message); }
    }
    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw Invalid(BundleV2ErrorCodes.InvalidManifest, "Bundle v2 value is required.");
    private static BundleV2Exception Invalid(string category, string message) => new(category, message);
}

public static class ReviewBundleV2CanonicalCodec
{
    public static byte[] Serialize(VerifiedReviewBundleV2 manifest) => CanonicalJsonSerializer.SerializeToUtf8Bytes(manifest.ToCanonicalJson());
    public static VerifiedReviewBundleV2 Rehydrate(byte[] bytes, ContentDigest expectedDigest)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes ?? throw new ArgumentNullException(nameof(bytes)));
            var verifiedEnvelope = DigestEnvelope.RehydrateAndVerify(document.RootElement, expectedDigest, DigestScope.BundleManifest,
                BundleV2Constants.SchemaId, BundleV2Constants.SchemaVersion);
            var content = document.RootElement.GetProperty("content");
            Exact(content, ["bundle_id", "entries", "non_claims", "project_revision", "report_digest", "source_generations", "workspace_cut_digest", "workspace_id"]);
            var sources = Array(content, "source_generations").Select(ParseSource).ToArray();
            var entries = Array(content, "entries").Select(ParseEntry).ToArray();
            var nonClaims = Array(content, "non_claims").Select(item => String(item)).ToArray();
            var manifest = ReviewBundleV2Authority.Create(String(content, "bundle_id"), ParseDigest(content.GetProperty("report_digest")),
                String(content, "workspace_id"), Int64(content, "project_revision"), ParseDigest(content.GetProperty("workspace_cut_digest")),
                sources, entries, nonClaims);
            if (manifest.ManifestDigest != verifiedEnvelope.Digest || !bytes.SequenceEqual(Serialize(manifest)))
                throw Invalid("Bundle v2 bytes do not exactly reproduce canonical authority.");
            return manifest;
        }
        catch (BundleV2Exception) { throw; }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            throw Invalid($"Bundle v2 canonical record is invalid: {error.Message}");
        }
    }

    private static BundleV2Entry ParseEntry(JsonElement value)
    {
        var kind = String(value, "entry_kind");
        if (kind == "embedded")
        {
            Exact(value, ["artifact_role", "content_digest", "entry_kind", "logical_path", "ordinal", "size_bytes", "source_binding"]);
            return new BundleV2EmbeddedEntry(Int32(value, "ordinal"), String(value, "logical_path"), Int64(value, "size_bytes"),
                ParseDigest(value.GetProperty("content_digest")), String(value, "artifact_role"), ParseSource(value.GetProperty("source_binding")));
        }
        if (kind == "external")
        {
            Exact(value, ["artifact_role", "availability_note", "entry_kind", "locator", "reference_id", "reference_kind", "source_binding"], ["expected_content_digest"]);
            return new BundleV2ExternalEntry(String(value, "reference_id"), String(value, "reference_kind"), String(value, "locator"),
                String(value, "availability_note"), value.TryGetProperty("expected_content_digest", out var digest) ? ParseDigest(digest) : null,
                String(value, "artifact_role"), ParseSource(value.GetProperty("source_binding")));
        }
        throw Invalid("Bundle v2 entry kind is unknown.");
    }

    private static BundleV2SourceBinding ParseSource(JsonElement value)
    {
        Exact(value, ["generation_id", "manifest_digest", "role"], ["candidate_id"]);
        return new BundleV2SourceBinding(String(value, "role"), String(value, "generation_id"), ParseDigest(value.GetProperty("manifest_digest")),
            value.TryGetProperty("candidate_id", out var candidate) ? String(candidate) : null);
    }

    private static BundleV2ScopedDigest ParseDigest(JsonElement value)
    {
        Exact(value, ["scope", "value"]);
        if (!ContentDigest.TryParse(String(value, "value"), out var digest) || !digest.IsValid) throw Invalid("Bundle v2 digest value is invalid.");
        return new BundleV2ScopedDigest(String(value, "scope"), digest);
    }

    private static JsonElement[] Array(JsonElement value, string name) => value.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
        ? array.EnumerateArray().ToArray() : throw Invalid($"Bundle v2 field '{name}' must be an array.");
    private static string String(JsonElement value, string name) => value.TryGetProperty(name, out var property) ? String(property) : throw Invalid($"Bundle v2 field '{name}' is required.");
    private static string String(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid("Bundle v2 string value is invalid.");
    private static int Int32(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.TryGetInt32(out var result) ? result : throw Invalid($"Bundle v2 field '{name}' must be an integer.");
    private static long Int64(JsonElement value, string name) => value.TryGetProperty(name, out var property) && property.TryGetInt64(out var result) ? result : throw Invalid($"Bundle v2 field '{name}' must be an integer.");
    private static void Exact(JsonElement value, IReadOnlyCollection<string> required, IReadOnlyCollection<string>? optional = null)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid("Bundle v2 object value is required.");
        var allowed = required.Concat(optional ?? System.Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!allowed.Contains(property.Name) || !observed.Add(property.Name)) throw Invalid($"Bundle v2 field '{property.Name}' is unknown or duplicated.");
        if (required.Any(name => !observed.Contains(name))) throw Invalid("Bundle v2 record is missing required fields.");
    }
    private static BundleV2Exception Invalid(string message) => new(BundleV2ErrorCodes.NonCanonicalManifest, message);
}

public sealed record BundleV2Verification(bool IsValid, bool IsSelfContained, ContentDigest ManifestDigest, ContentDigest InventoryDigest,
    IReadOnlyList<BundleVerificationFinding> Findings);

public sealed record BundleV2ObservedEntry(string Path, byte[] Bytes);

public static class ReviewBundleV2Verifier
{
    public static BundleV2Verification Verify(VerifiedReviewBundleV2 manifest, byte[] manifestBytes,
        IEnumerable<BundleV2ObservedEntry> observedInventory)
    {
        var findings = new List<BundleVerificationFinding>();
        var observed = (observedInventory ?? throw new ArgumentNullException(nameof(observedInventory))).ToArray();
        foreach (var item in observed)
            if (!BundleArtifactPath.TryValidate(item.Path, out _)) findings.Add(new(BundleV2ErrorCodes.InvalidPath, "Observed inventory path is invalid.", item.Path));
        var duplicatePaths = observed.GroupBy(item => item.Path, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        foreach (var duplicate in duplicatePaths) findings.Add(new(BundleV2ErrorCodes.DuplicateEntry, "Observed inventory path is duplicated.", duplicate));
        var observedByPath = observed.GroupBy(item => item.Path, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Bytes, StringComparer.Ordinal);
        if (!manifestBytes.SequenceEqual(ReviewBundleV2CanonicalCodec.Serialize(manifest)))
            findings.Add(new(BundleV2ErrorCodes.NonCanonicalManifest, "Manifest bytes are altered.", BundleV2Constants.ManifestPath));
        if (observedByPath.TryGetValue(BundleV2Constants.ManifestPath, out var observedManifest) && !observedManifest.SequenceEqual(manifestBytes))
            findings.Add(new(BundleV2ErrorCodes.AlteredArtifact, "Observed manifest inventory bytes differ from verified manifest bytes.", BundleV2Constants.ManifestPath));
        var expectedPaths = manifest.Entries.OfType<BundleV2EmbeddedEntry>().Select(item => item.LogicalPath)
            .Append(BundleV2Constants.ManifestPath).ToHashSet(StringComparer.Ordinal);
        var observedPaths = observedByPath.Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var missing in expectedPaths.Except(observedPaths).OrderBy(item => item, StringComparer.Ordinal))
            findings.Add(new(BundleV2ErrorCodes.MissingInventory, "Declared inventory path is missing.", missing));
        foreach (var extra in observedPaths.Except(expectedPaths).OrderBy(item => item, StringComparer.Ordinal))
            findings.Add(new(BundleV2ErrorCodes.ExtraInventory, "Observed inventory path is undeclared.", extra));
        foreach (var item in manifest.Entries.OfType<BundleV2EmbeddedEntry>())
            if (observedByPath.TryGetValue(item.LogicalPath, out var bytes) &&
                (bytes.LongLength != item.SizeBytes || ContentDigest.Sha256(bytes) != item.ContentDigest.Value))
                findings.Add(new(BundleV2ErrorCodes.AlteredArtifact, "Embedded artifact bytes or size are altered.", item.LogicalPath));
        var inventory = new CanonicalJsonObject().Add("paths", CanonicalJsonValue.Array(observed.OrderBy(item => item.Path, StringComparer.Ordinal)
            .Select(item => new CanonicalJsonObject().Add("path", item.Path).Add("size_bytes", item.Bytes.LongLength)
                .Add("digest", ContentDigest.Sha256(item.Bytes).ToString())).ToArray()));
        var inventoryDigest = ContentDigest.Sha256CanonicalJson(inventory);
        return new BundleV2Verification(findings.Count == 0, manifest.IsSelfContained, manifest.ManifestDigest, inventoryDigest,
            Array.AsReadOnly(findings.ToArray()));
    }
}
