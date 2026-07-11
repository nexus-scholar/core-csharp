using System.Security.Cryptography;
using System.Text;

namespace NexusScholar.Kernel;

public readonly record struct ContentDigest
{
    private const int Sha256HexLength = 64;
    private readonly DigestAlgorithm _algorithm;
    private readonly string? _value;

    private ContentDigest(DigestAlgorithm algorithm, string value)
    {
        _algorithm = algorithm;
        _value = value;
    }

    public bool IsValid => _algorithm.IsValid &&
        _value is { Length: Sha256HexLength } &&
        _value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

    public DigestAlgorithm Algorithm => IsValid
        ? _algorithm
        : throw new InvalidOperationException("Default content digests are invalid.");

    public string Value => IsValid
        ? _value!
        : throw new InvalidOperationException("Default content digests are invalid.");

    public static ContentDigest Create(DigestAlgorithm algorithm, string value)
    {
        value = Guard.NotBlank(value, nameof(value));

        if (algorithm != DigestAlgorithm.Sha256)
        {
            throw new ArgumentException("Only the canonical digest algorithm 'sha256' is supported.", nameof(algorithm));
        }

        if (value.Length != Sha256HexLength)
        {
            throw new FormatException("SHA-256 digests must contain exactly 64 lowercase hexadecimal characters.");
        }

        foreach (var character in value)
        {
            if (character is >= 'A' and <= 'F')
            {
                throw new FormatException("SHA-256 digests must use lowercase hexadecimal characters.");
            }

            if (!char.IsAsciiDigit(character) && (character < 'a' || character > 'f'))
            {
                throw new FormatException("SHA-256 digests must use hexadecimal characters only.");
            }
        }

        return new ContentDigest(algorithm, value);
    }

    public static ContentDigest Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var separatorIndex = value.IndexOf(':');
        if (separatorIndex < 0)
        {
            throw new FormatException("Canonical digests must include an algorithm prefix such as 'sha256:'.");
        }

        var algorithm = DigestAlgorithm.Parse(value[..separatorIndex]);
        return Create(algorithm, value[(separatorIndex + 1)..]);
    }

    public static bool TryParse(string? value, out ContentDigest digest)
    {
        try
        {
            if (value is null)
            {
                digest = default;
                return false;
            }

            digest = Parse(value);
            return true;
        }
        catch (ArgumentException)
        {
        }
        catch (FormatException)
        {
        }

        digest = default;
        return false;
    }

    public static ContentDigest Sha256(ReadOnlySpan<byte> content)
    {
        var bytes = SHA256.HashData(content);
        return Create(DigestAlgorithm.Sha256, Convert.ToHexStringLower(bytes));
    }

    public static ContentDigest Sha256Utf8(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Sha256(Encoding.UTF8.GetBytes(content));
    }

    public static ContentDigest Sha256CanonicalJson(
        CanonicalJsonValue value,
        CanonicalJsonSerializerOptions? options = null)
    {
        return Sha256(CanonicalJsonSerializer.SerializeToUtf8Bytes(value, options));
    }

    public static ContentDigest Sha256NdjsonUtf8(
        string content,
        NdjsonCanonicalizerOptions? options = null)
    {
        return Sha256(NdjsonCanonicalizer.CanonicalizeToUtf8Bytes(content, options));
    }

    public override string ToString() => $"{Algorithm}:{Value}";
}
