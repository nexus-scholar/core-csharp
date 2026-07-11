namespace NexusScholar.Kernel;

public readonly record struct DigestAlgorithm
{
    private readonly string? _value;

    private DigestAlgorithm(string value)
    {
        _value = value;
    }

    public bool IsValid => string.Equals(_value, "sha256", StringComparison.Ordinal);

    public string Value => IsValid
        ? _value!
        : throw new InvalidOperationException("Default digest algorithms are invalid.");

    public static DigestAlgorithm Sha256 { get; } = new("sha256");

    public static DigestAlgorithm Parse(string value)
    {
        if (!TryParse(value, out var algorithm))
        {
            throw new ArgumentException("Only the canonical digest algorithm 'sha256' is supported.", nameof(value));
        }

        return algorithm;
    }

    public static bool TryParse(string? value, out DigestAlgorithm algorithm)
    {
        if (string.Equals(value, Sha256.Value, StringComparison.Ordinal))
        {
            algorithm = Sha256;
            return true;
        }

        algorithm = default;
        return false;
    }

    public override string ToString() => Value;
}

public readonly record struct DigestScope
{
    private static readonly HashSet<string> ApprovedScopes = new(StringComparer.Ordinal)
    {
        "raw-artifact-bytes",
        "canonical-json-record",
        "protocol-content",
        "approval-record",
        "provenance-event",
        "bundle-manifest",
        "ndjson-stream"
    };

    private readonly string? _value;

    private DigestScope(string value)
    {
        _value = value;
    }

    public bool IsValid => _value is not null && ApprovedScopes.Contains(_value);

    public string Value => IsValid
        ? _value!
        : throw new InvalidOperationException("Default digest scopes are invalid.");

    public static DigestScope RawArtifactBytes { get; } = new("raw-artifact-bytes");

    public static DigestScope CanonicalJsonRecord { get; } = new("canonical-json-record");

    public static DigestScope ProtocolContent { get; } = new("protocol-content");

    public static DigestScope ApprovalRecord { get; } = new("approval-record");

    public static DigestScope ProvenanceEvent { get; } = new("provenance-event");

    public static DigestScope BundleManifest { get; } = new("bundle-manifest");

    public static DigestScope NdjsonStream { get; } = new("ndjson-stream");

    public static DigestScope Parse(string value)
    {
        if (!TryParse(value, out var scope))
        {
            throw new ArgumentException("Digest scope must be one of the approved Gate 2 local scope identifiers.", nameof(value));
        }

        return scope;
    }

    public static bool TryParse(string? value, out DigestScope scope)
    {
        if (value is not null && ApprovedScopes.Contains(value))
        {
            scope = new DigestScope(value);
            return true;
        }

        scope = default;
        return false;
    }

    public override string ToString() => Value;
}
