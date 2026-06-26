using System.Text.Json;

namespace NexusScholar.Kernel;

public sealed class DigestEnvelope
{
    public DigestAlgorithm Algorithm => DigestAlgorithm.Sha256;

    public string CanonicalizationProfile => CanonicalJsonSerializer.ProfileId;

    public DigestEnvelope(
        DigestScope scope,
        string schemaId,
        string schemaVersion,
        CanonicalJsonObject content)
    {
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

    public static void ValidateCanonicalShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Digest envelope fixtures must be JSON objects.");
        }

        RequireString(root, "algorithm", DigestAlgorithm.Sha256.Value);
        RequireString(root, "canonicalizationProfile", CanonicalJsonSerializer.ProfileId);
        RequireString(root, "schema");
        RequireString(root, "schemaVersion");
        RequireString(root, "scope");

        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Digest envelope must include an object-valued 'content' field.");
        }
    }

    private static void RequireString(JsonElement root, string propertyName, string? expectedValue = null)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Digest envelope must include string field '{propertyName}'.");
        }

        if (expectedValue is not null && !string.Equals(value.GetString(), expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Digest envelope field '{propertyName}' must equal '{expectedValue}'.");
        }
    }
}
