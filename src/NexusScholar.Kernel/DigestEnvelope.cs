using System.Text.Json;

namespace NexusScholar.Kernel;

public sealed class DigestEnvelope
{
    private static readonly HashSet<string> RequiredFields = new(StringComparer.Ordinal)
    {
        "algorithm",
        "canonicalizationProfile",
        "content",
        "schema",
        "schemaVersion",
        "scope"
    };

    public DigestAlgorithm Algorithm => DigestAlgorithm.Sha256;

    public string CanonicalizationProfile => CanonicalJsonSerializer.ProfileId;

    public DigestEnvelope(
        DigestScope scope,
        string schemaId,
        string schemaVersion,
        CanonicalJsonObject content)
    {
        if (!scope.IsValid)
        {
            throw new ArgumentException("Digest envelope scope must be an approved non-default value.", nameof(scope));
        }

        Scope = scope;
        SchemaId = Guard.NotBlank(schemaId, nameof(schemaId));
        SchemaVersion = Guard.NotBlank(schemaVersion, nameof(schemaVersion));
        Content = ((CanonicalJsonObject)CanonicalJsonValue.DeepClone(content ?? throw new ArgumentNullException(nameof(content)))).Freeze();
    }

    public DigestScope Scope { get; }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public CanonicalJsonObject Content { get; }

    public CanonicalJsonObject ToCanonicalJsonObject()
    {
        return new CanonicalJsonObject()
            .Add("algorithm", Algorithm.Value)
            .Add("canonicalizationProfile", CanonicalizationProfile)
            .Add("content", Content)
            .Add("schema", SchemaId)
            .Add("schemaVersion", SchemaVersion)
            .Add("scope", Scope.Value);
    }

    public byte[] ToCanonicalJsonBytes(CanonicalJsonSerializerOptions? options = null)
    {
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(ToCanonicalJsonObject(), options);
    }

    public string ToCanonicalJson(CanonicalJsonSerializerOptions? options = null)
    {
        return CanonicalJsonSerializer.Serialize(ToCanonicalJsonObject(), options);
    }

    public ContentDigest ComputeDigest(CanonicalJsonSerializerOptions? options = null)
    {
        return ContentDigest.Sha256(ToCanonicalJsonBytes(options));
    }

    public static VerifiedDigestEnvelope RehydrateAndVerify(
        JsonElement root,
        ContentDigest expectedDigest,
        DigestScope expectedScope,
        string expectedSchemaId,
        string expectedSchemaVersion)
    {
        if (!expectedDigest.IsValid)
        {
            throw new ArgumentException("Expected digest must be a valid non-default content digest.", nameof(expectedDigest));
        }

        if (!expectedScope.IsValid)
        {
            throw new ArgumentException("Expected scope must be an approved non-default digest scope.", nameof(expectedScope));
        }

        expectedSchemaId = Guard.NotBlank(expectedSchemaId, nameof(expectedSchemaId));
        expectedSchemaVersion = Guard.NotBlank(expectedSchemaVersion, nameof(expectedSchemaVersion));

        ValidateCanonicalShape(root);

        var actualScope = ParseScope(RequireString(root, "scope"));
        var actualSchemaId = RequireString(root, "schema");
        var actualSchemaVersion = RequireString(root, "schemaVersion");

        RequireExpectedValue("scope", expectedScope.Value, actualScope.Value);
        RequireExpectedValue("schema", expectedSchemaId, actualSchemaId);
        RequireExpectedValue("schemaVersion", expectedSchemaVersion, actualSchemaVersion);

        var content = (CanonicalJsonObject)CanonicalJsonValue.FromJsonElement(root.GetProperty("content"));
        var envelope = new DigestEnvelope(actualScope, actualSchemaId, actualSchemaVersion, content);
        var actualDigest = envelope.ComputeDigest();

        if (actualDigest != expectedDigest)
        {
            throw new InvalidOperationException("Digest envelope content does not reproduce the expected digest.");
        }

        return new VerifiedDigestEnvelope(envelope, actualDigest);
    }

    public static void ValidateCanonicalShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Digest envelope fixtures must be JSON objects.");
        }

        var observedFields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!RequiredFields.Contains(property.Name))
            {
                throw new InvalidOperationException($"Digest envelope contains unknown field '{property.Name}'.");
            }

            if (!observedFields.Add(property.Name))
            {
                throw new InvalidOperationException($"Digest envelope contains duplicate field '{property.Name}'.");
            }
        }

        RequireString(root, "algorithm", DigestAlgorithm.Sha256.Value);
        RequireString(root, "canonicalizationProfile", CanonicalJsonSerializer.ProfileId);
        RequireString(root, "schema");
        RequireString(root, "schemaVersion");
        _ = ParseScope(RequireString(root, "scope"));

        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Digest envelope must include an object-valued 'content' field.");
        }
    }

    private static string RequireString(JsonElement root, string propertyName, string? expectedValue = null)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Digest envelope must include string field '{propertyName}'.");
        }

        if (expectedValue is not null && !string.Equals(value.GetString(), expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Digest envelope field '{propertyName}' must equal '{expectedValue}'.");
        }

        return value.GetString()!;
    }

    private static DigestScope ParseScope(string value)
    {
        if (!DigestScope.TryParse(value, out var scope))
        {
            throw new InvalidOperationException("Digest envelope scope must be an approved value.");
        }

        return scope;
    }

    private static void RequireExpectedValue(string field, string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Digest envelope field '{field}' does not match the expected contract.");
        }
    }
}

public sealed class VerifiedDigestEnvelope
{
    internal VerifiedDigestEnvelope(DigestEnvelope envelope, ContentDigest digest)
    {
        Envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
        if (!digest.IsValid || envelope.ComputeDigest() != digest)
        {
            throw new ArgumentException("Verified digest envelopes require the recomputed envelope digest.", nameof(digest));
        }

        Digest = digest;
    }

    public DigestEnvelope Envelope { get; }

    public ContentDigest Digest { get; }
}
