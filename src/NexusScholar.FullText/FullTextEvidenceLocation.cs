using System.Text.Json;
using NexusScholar.Kernel;

namespace NexusScholar.FullText;

public static class FullTextEvidenceLocationKinds
{
    public const string Page = "page";
    public const string Section = "section";
    public const string Table = "table";
    public const string Text = "text";

    public static bool IsAllowed(string value) => value is Page or Section or Table or Text;
}

public static class FullTextEvidenceLocationCodec
{
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal)
    {
        "location_id", "source_artifact_id", "source_raw_byte_digest", "extraction_id", "extraction_digest",
        "representation_kind", "location_kind", "element_ordinal", "locator", "excerpt", "source_element_digest",
        "excerpt_digest"
    };

    public static byte[] Serialize(FullTextEvidenceLocation location) =>
        (location ?? throw new ArgumentNullException(nameof(location))).ToCanonicalBytes();

    public static FullTextEvidenceLocation Rehydrate(
        byte[] bytes,
        ContentDigest expectedDigest,
        VerifiedFullTextExtraction source)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            var canonical = CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(root));
            if (!bytes.SequenceEqual(canonical)) throw Invalid("Evidence location bytes must be canonical JSON.");
            var verified = DigestEnvelope.RehydrateAndVerify(
                root, expectedDigest, DigestScope.CanonicalJsonRecord,
                FullTextSchemas.EvidenceLocationSchemaId, FullTextSchemas.SchemaVersion);
            var content = root.GetProperty("content");
            if (content.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal).SetEquals(Fields) is false)
                throw Invalid("Evidence location contains missing or unknown fields.");

            var result = FullTextEvidenceLocation.Create(
                Text(content, "location_id"), source, Text(content, "location_kind"),
                Integer(content, "element_ordinal"), Text(content, "locator"), Text(content, "excerpt"));
            if (!result.ToCanonicalBytes().SequenceEqual(bytes) || result.Digest != verified.Digest)
                throw Invalid("Evidence location does not match the verified Full Text source.");
            return result;
        }
        catch (FullTextRuleException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw Invalid("Evidence location canonical record is invalid.");
        }
    }

    private static string Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw Invalid($"Evidence location field '{name}' is invalid.");

    private static int Integer(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
            ? result
            : throw Invalid($"Evidence location field '{name}' is invalid.");

    private static FullTextRuleException Invalid(string message) =>
        new(FullTextEvidenceLocationErrorCodes.InvalidLocation, message);
}

public static class FullTextEvidenceLocationErrorCodes
{
    public const string InvalidLocation = "invalid-fulltext-evidence-location";
    public const string SourceMismatch = "fulltext-evidence-source-mismatch";
}

public sealed class FullTextEvidenceLocation
{
    private FullTextEvidenceLocation(
        string locationId,
        VerifiedFullTextExtraction source,
        string locationKind,
        int elementOrdinal,
        string locator,
        string excerpt,
        ContentDigest sourceElementDigest,
        ContentDigest excerptDigest)
    {
        LocationId = locationId;
        SourceArtifactId = source.Source.Artifact.ArtifactId;
        SourceRawByteDigest = ContentDigest.Parse(source.Source.Artifact.RawByteDigest);
        ExtractionId = source.Record.ExtractionId;
        ExtractionDigest = ContentDigest.Parse(source.Record.ExtractedTextDigest!);
        RepresentationKind = source.Record.RepresentationKind!;
        LocationKind = locationKind;
        ElementOrdinal = elementOrdinal;
        Locator = locator;
        Excerpt = excerpt;
        SourceElementDigest = sourceElementDigest;
        ExcerptDigest = excerptDigest;
        DigestEnvelope = BuildEnvelope();
    }

    public string LocationId { get; }
    public string SourceArtifactId { get; }
    public ContentDigest SourceRawByteDigest { get; }
    public string ExtractionId { get; }
    public ContentDigest ExtractionDigest { get; }
    public string RepresentationKind { get; }
    public string LocationKind { get; }
    public int ElementOrdinal { get; }
    public string Locator { get; }
    public string Excerpt { get; }
    public ContentDigest SourceElementDigest { get; }
    public ContentDigest ExcerptDigest { get; }
    public DigestEnvelope DigestEnvelope { get; }
    public ContentDigest Digest => DigestEnvelope.ComputeDigest();

    public static FullTextEvidenceLocation Create(
        string locationId,
        VerifiedFullTextExtraction source,
        string locationKind,
        int elementOrdinal,
        string locator,
        string excerpt)
    {
        ArgumentNullException.ThrowIfNull(source);
        var id = Require(locationId, nameof(locationId));
        var kind = Require(locationKind, nameof(locationKind)).ToLowerInvariant();
        var normalizedLocator = Require(locator, nameof(locator));
        var exactExcerpt = Require(excerpt, nameof(excerpt));
        if (!FullTextEvidenceLocationKinds.IsAllowed(kind) || elementOrdinal < 1)
            throw Rule("Evidence location kind and one-based element ordinal are required.");

        var record = source.Record;
        if (record.ExtractedTextDigest is null || record.RepresentationKind is null)
            throw Rule("Evidence locations require a successful derived-text representation.");
        if (kind == FullTextEvidenceLocationKinds.Page && record.RepresentationKind != FullTextExtractionRepresentations.PageText)
            throw Rule("Page locations require a page-text representation.");
        if (kind == FullTextEvidenceLocationKinds.Section && record.RepresentationKind != FullTextExtractionRepresentations.Sections)
            throw Rule("Section locations require a sections representation.");

        IReadOnlyList<string> values = record.RepresentationKind == FullTextExtractionRepresentations.PageText
            ? record.PageText
            : record.Sections;
        if (elementOrdinal > values.Count)
            throw Rule("Evidence location element does not exist in the verified representation.");
        var element = values[elementOrdinal - 1];
        if (!element.Contains(exactExcerpt, StringComparison.Ordinal))
            throw Rule("Evidence excerpt does not occur in the selected source element.");

        return new FullTextEvidenceLocation(
            id, source, kind, elementOrdinal, normalizedLocator, exactExcerpt,
            ContentDigest.Sha256Utf8(element), ContentDigest.Sha256Utf8(exactExcerpt));
    }

    public CanonicalJsonObject ToCanonicalJson() => DigestEnvelope.ToCanonicalJsonObject();
    public byte[] ToCanonicalBytes() => DigestEnvelope.ToCanonicalJsonBytes();

    private DigestEnvelope BuildEnvelope() => new(
        DigestScope.CanonicalJsonRecord,
        FullTextSchemas.EvidenceLocationSchemaId,
        FullTextSchemas.SchemaVersion,
        new CanonicalJsonObject()
            .Add("location_id", LocationId)
            .Add("source_artifact_id", SourceArtifactId)
            .Add("source_raw_byte_digest", SourceRawByteDigest.ToString())
            .Add("extraction_id", ExtractionId)
            .Add("extraction_digest", ExtractionDigest.ToString())
            .Add("representation_kind", RepresentationKind)
            .Add("location_kind", LocationKind)
            .Add("element_ordinal", ElementOrdinal)
            .Add("locator", Locator)
            .Add("excerpt", Excerpt)
            .Add("source_element_digest", SourceElementDigest.ToString())
            .Add("excerpt_digest", ExcerptDigest.ToString()));

    private static string Require(string value, string name) =>
        !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ArgumentException("Value is required.", name);

    private static FullTextRuleException Rule(string message) =>
        new(FullTextEvidenceLocationErrorCodes.InvalidLocation, message);
}
