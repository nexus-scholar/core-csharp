using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using NexusScholar.Kernel;

namespace NexusScholar.FullText;

public static class FullTextExtractionAttemptStatuses
{
    public const string Success = "success";
    public const string Partial = "partial";
    public const string Failure = "failure";
    public const string Unsupported = "unsupported";

    public static bool IsKnown(string value) => value is Success or Partial or Failure or Unsupported;
}

public sealed class FullTextExtractionConfiguration
{
    private FullTextExtractionConfiguration(
        string extractorId,
        string extractorVersion,
        string representationKind,
        CanonicalJsonObject options)
    {
        ExtractorId = Guard.NotBlank(extractorId, nameof(extractorId));
        ExtractorVersion = Guard.NotBlank(extractorVersion, nameof(extractorVersion));
        if (!FullTextExtractionRepresentations.IsAllowed(representationKind))
            throw Rule(FullTextErrorCodes.InvalidExtractionRepresentation, "Extraction configuration representation is unsupported.");
        RepresentationKind = representationKind;
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Digest = ContentDigest.Sha256CanonicalJson(ToCanonicalJson());
    }

    public string ExtractorId { get; }
    public string ExtractorVersion { get; }
    public string RepresentationKind { get; }
    public CanonicalJsonObject Options { get; }
    public ContentDigest Digest { get; }

    public static FullTextExtractionConfiguration Create(
        string extractorId,
        string extractorVersion,
        string representationKind,
        CanonicalJsonObject? options = null) => new(
            extractorId,
            extractorVersion,
            representationKind,
            options ?? new CanonicalJsonObject());

    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("extractor_id", ExtractorId)
        .Add("extractor_version", ExtractorVersion)
        .Add("representation_kind", RepresentationKind)
        .Add("options", Options);

    private static FullTextRuleException Rule(string category, string message) => new(category, message);
}

public sealed class FullTextExtractionAttempt
{
    public const string SchemaId = "nexus.fulltext.extraction-attempt";
    public const string SchemaVersion = "1.0.0";

    private FullTextExtractionAttempt(
        string attemptId,
        VerifiedFullTextChain source,
        FullTextExtractionConfiguration configuration,
        DateTimeOffset attemptedAt,
        string status,
        IReadOnlyList<string> values,
        IReadOnlyList<string> warnings,
        string? failureCategory,
        string? failureSummary)
    {
        AttemptId = Guard.NotBlank(attemptId, nameof(attemptId));
        SourceArtifactId = source.Artifact.ArtifactId;
        SourceRawByteDigest = ContentDigest.Parse(source.Artifact.RawByteDigest);
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        AttemptedAt = attemptedAt;
        Status = Guard.NotBlank(status, nameof(status));
        Values = Array.AsReadOnly(values.ToArray());
        Warnings = Array.AsReadOnly(warnings.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray());
        FailureCategory = string.IsNullOrWhiteSpace(failureCategory) ? null : failureCategory.Trim();
        FailureSummary = string.IsNullOrWhiteSpace(failureSummary) ? null : failureSummary.Trim();
        Validate();
        OutputDigest = HasOutput
            ? FullTextExtractionRecord.ComputeRepresentationDigest(Configuration.RepresentationKind, Values)
            : null;
        Digest = Envelope().ComputeDigest();
    }

    public string AttemptId { get; }
    public string SourceArtifactId { get; }
    public ContentDigest SourceRawByteDigest { get; }
    public FullTextExtractionConfiguration Configuration { get; }
    public DateTimeOffset AttemptedAt { get; }
    public string Status { get; }
    public IReadOnlyList<string> Values { get; }
    public IReadOnlyList<string> Warnings { get; }
    public string? FailureCategory { get; }
    public string? FailureSummary { get; }
    public ContentDigest? OutputDigest { get; }
    public ContentDigest Digest { get; }
    public bool HasOutput => Status is FullTextExtractionAttemptStatuses.Success or FullTextExtractionAttemptStatuses.Partial;

    public static FullTextExtractionAttempt Create(
        string attemptId,
        VerifiedFullTextChain source,
        FullTextExtractionConfiguration configuration,
        DateTimeOffset attemptedAt,
        string status,
        IEnumerable<string>? values = null,
        IEnumerable<string>? warnings = null,
        string? failureCategory = null,
        string? failureSummary = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new FullTextExtractionAttempt(
            attemptId,
            source,
            configuration,
            attemptedAt,
            status,
            (values ?? Array.Empty<string>()).ToArray(),
            (warnings ?? Array.Empty<string>()).ToArray(),
            failureCategory,
            failureSummary);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    private DigestEnvelope Envelope()
    {
        var content = new CanonicalJsonObject()
            .Add("attempt_id", AttemptId)
            .Add("source_artifact_id", SourceArtifactId)
            .Add("source_raw_byte_digest", SourceRawByteDigest.ToString())
            .Add("configuration", Configuration.ToCanonicalJson())
            .Add("configuration_digest", Configuration.Digest.ToString())
            .AddTimestamp("attempted_at", AttemptedAt)
            .Add("status", Status)
            .Add("values", CanonicalJsonValue.Array(Values.Select(CanonicalJsonValue.From).ToArray()))
            .Add("warnings", CanonicalJsonValue.Array(Warnings.Select(CanonicalJsonValue.From).ToArray()));
        if (OutputDigest is not null) content.Add("output_digest", OutputDigest.Value.ToString());
        if (FailureCategory is not null) content.Add("failure_category", FailureCategory);
        if (FailureSummary is not null) content.Add("failure_summary", FailureSummary);
        return new DigestEnvelope(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion, content);
    }

    private void Validate()
    {
        if (!FullTextExtractionAttemptStatuses.IsKnown(Status))
            throw Rule(FullTextErrorCodes.ExtractionFailure, "Extraction attempt status is unsupported.");
        if (HasOutput)
        {
            if (Values.Count == 0 || FailureCategory is not null || FailureSummary is not null)
                throw Rule(FullTextErrorCodes.InvalidExtractionRepresentation, "Successful or partial extraction requires output and cannot carry failure fields.");
            if (Status == FullTextExtractionAttemptStatuses.Partial && Warnings.Count == 0)
                throw Rule(FullTextErrorCodes.PartialExtraction, "Partial extraction requires at least one warning.");
        }
        else
        {
            if (Values.Count != 0 || Warnings.Count != 0 || FailureCategory is null || FailureSummary is null)
                throw Rule(FullTextErrorCodes.ExtractionFailure, "Failed or unsupported extraction requires failure fields and cannot carry output or warnings.");
        }
    }

    private static FullTextRuleException Rule(string category, string message) => new(category, message);
}

public static class FullTextDeterministicExtractor
{
    public const string ExtractorId = "nexus.local-structured-text";
    public const string ExtractorVersion = "1.0.0";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static FullTextExtractionAttempt Extract(
        string attemptId,
        VerifiedFullTextChain source,
        byte[] acceptedBytes,
        DateTimeOffset attemptedAt)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(acceptedBytes);
        if (ContentDigest.Sha256(acceptedBytes).ToString() != source.Artifact.RawByteDigest)
            throw new FullTextRuleException(FullTextErrorCodes.ExtractionSourceMismatch, "Extraction bytes do not match the verified raw artifact.");
        var configuration = FullTextExtractionConfiguration.Create(
            ExtractorId,
            ExtractorVersion,
            FullTextExtractionRepresentations.PageText,
            new CanonicalJsonObject().Add("network", false).Add("xml_dtd", "prohibit"));

        return source.Artifact.ArtifactKind switch
        {
            FullTextArtifactKinds.Text => ExtractText(attemptId, source, configuration, acceptedBytes, attemptedAt),
            FullTextArtifactKinds.Xml => ExtractXml(attemptId, source, configuration, acceptedBytes, attemptedAt),
            FullTextArtifactKinds.Pdf => FullTextExtractionAttempt.Create(
                attemptId, source, configuration, attemptedAt, FullTextExtractionAttemptStatuses.Unsupported,
                failureCategory: FullTextErrorCodes.UnsupportedFileType,
                failureSummary: "Deterministic PDF parsing is not admitted by ADR 0032."),
            _ => FullTextExtractionAttempt.Create(
                attemptId, source, configuration, attemptedAt, FullTextExtractionAttemptStatuses.Unsupported,
                failureCategory: FullTextErrorCodes.UnsupportedFileType,
                failureSummary: "Artifact kind is not supported by the local extractor.")
        };
    }

    private static FullTextExtractionAttempt ExtractText(
        string attemptId,
        VerifiedFullTextChain source,
        FullTextExtractionConfiguration configuration,
        byte[] bytes,
        DateTimeOffset attemptedAt)
    {
        try
        {
            var text = StrictUtf8.GetString(bytes);
            return FullTextExtractionAttempt.Create(
                attemptId, source, configuration, attemptedAt, FullTextExtractionAttemptStatuses.Success, [text]);
        }
        catch (DecoderFallbackException)
        {
            return FullTextExtractionAttempt.Create(
                attemptId, source, configuration, attemptedAt, FullTextExtractionAttemptStatuses.Failure,
                failureCategory: FullTextErrorCodes.ExtractionFailure,
                failureSummary: "Text artifact is not valid UTF-8.");
        }
    }

    private static FullTextExtractionAttempt ExtractXml(
        string attemptId,
        VerifiedFullTextChain source,
        FullTextExtractionConfiguration configuration,
        byte[] bytes,
        DateTimeOffset attemptedAt)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
            var text = string.Concat(document.DescendantNodes().OfType<XText>().Select(node => node.Value));
            if (string.IsNullOrWhiteSpace(text))
                throw new XmlException("XML contains no text content.");
            return FullTextExtractionAttempt.Create(
                attemptId, source, configuration, attemptedAt, FullTextExtractionAttemptStatuses.Success, [text]);
        }
        catch (Exception exception) when (exception is XmlException or DecoderFallbackException)
        {
            return FullTextExtractionAttempt.Create(
                attemptId, source, configuration, attemptedAt, FullTextExtractionAttemptStatuses.Failure,
                failureCategory: FullTextErrorCodes.ExtractionFailure,
                failureSummary: "XML extraction failed deterministic validation.");
        }
    }
}

public static class FullTextExtractionAttemptCodec
{
    public static byte[] Serialize(FullTextExtractionAttempt attempt) =>
        CanonicalJsonSerializer.SerializeToUtf8Bytes(attempt.ToCanonicalJson());

    public static FullTextExtractionAttempt Rehydrate(
        byte[] bytes,
        ContentDigest expectedDigest,
        VerifiedFullTextChain source)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var document = JsonDocument.Parse(bytes);
        if (CanonicalJsonValue.FromJsonElement(document.RootElement) is not CanonicalJsonObject root ||
            !bytes.SequenceEqual(CanonicalJsonSerializer.SerializeToUtf8Bytes(root)))
            throw Rule("Extraction attempt bytes must be canonical JSON.");
        var content = DigestEnvelope.RehydrateAndVerify(
            document.RootElement,
            expectedDigest,
            DigestScope.CanonicalJsonRecord,
            FullTextExtractionAttempt.SchemaId,
            FullTextExtractionAttempt.SchemaVersion).Envelope.Content;
        var required = new[]
        {
            "attempt_id", "attempted_at", "configuration", "configuration_digest", "source_artifact_id",
            "source_raw_byte_digest", "status", "values", "warnings"
        };
        var allowed = required.Concat(new[] { "failure_category", "failure_summary", "output_digest" }).ToHashSet(StringComparer.Ordinal);
        if (!required.All(content.Properties.ContainsKey) || content.Properties.Keys.Any(key => !allowed.Contains(key)))
            throw Rule("Extraction attempt has missing or unknown fields.");
        var configurationValue = Object(content, "configuration");
        RequireExact(configurationValue, ["extractor_id", "extractor_version", "options", "representation_kind"]);
        var configuration = FullTextExtractionConfiguration.Create(
            Text(configurationValue, "extractor_id"),
            Text(configurationValue, "extractor_version"),
            Text(configurationValue, "representation_kind"),
            Object(configurationValue, "options"));
        if (configuration.Digest != Digest(content, "configuration_digest") ||
            source.Artifact.ArtifactId != Text(content, "source_artifact_id") ||
            source.Artifact.RawByteDigest != Text(content, "source_raw_byte_digest"))
            throw Rule("Extraction attempt authority binding is stale or mismatched.");
        var attempt = FullTextExtractionAttempt.Create(
            Text(content, "attempt_id"),
            source,
            configuration,
            Timestamp(content, "attempted_at"),
            Text(content, "status"),
            Array(content, "values").Select(Text),
            Array(content, "warnings").Select(Text),
            OptionalText(content, "failure_category"),
            OptionalText(content, "failure_summary"));
        if (content.Properties.ContainsKey("output_digest") && attempt.OutputDigest != Digest(content, "output_digest"))
            throw Rule("Extraction attempt output digest does not reproduce.");
        var reproduced = Serialize(attempt);
        if (attempt.Digest != expectedDigest || !bytes.SequenceEqual(reproduced))
            throw Rule("Extraction attempt did not reproduce its digest and canonical bytes.");
        return attempt;
    }

    private static void RequireExact(CanonicalJsonObject value, IEnumerable<string> names)
    {
        var expected = names.ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(value.Properties.Keys)) throw Rule("Extraction configuration has missing or unknown fields.");
    }

    private static CanonicalJsonObject Object(CanonicalJsonObject root, string name) =>
        root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonObject result
            ? result : throw Rule($"Extraction attempt field '{name}' must be an object.");
    private static IReadOnlyList<CanonicalJsonValue> Array(CanonicalJsonObject root, string name) =>
        root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonArray result
            ? result.Items : throw Rule($"Extraction attempt field '{name}' must be an array.");
    private static string Text(CanonicalJsonObject root, string name) =>
        root.Properties.TryGetValue(name, out var value) && value is CanonicalJsonString result
            ? result.Value : throw Rule($"Extraction attempt field '{name}' must be text.");
    private static string Text(CanonicalJsonValue value) => value is CanonicalJsonString result
        ? result.Value : throw Rule("Extraction attempt array values must be text.");
    private static string? OptionalText(CanonicalJsonObject root, string name) => root.Properties.ContainsKey(name) ? Text(root, name) : null;
    private static ContentDigest Digest(CanonicalJsonObject root, string name) => ContentDigest.Parse(Text(root, name));
    private static DateTimeOffset Timestamp(CanonicalJsonObject root, string name)
    {
        var value = Text(root, name);
        CanonicalTimestamp.ValidateCanonicalUtc(value);
        return DateTimeOffset.ParseExact(value, CanonicalTimestamp.DefaultUtcFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
    private static FullTextRuleException Rule(string message) => new(FullTextErrorCodes.InvalidAuthorityChain, message);
}
