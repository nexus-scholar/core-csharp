using System.Text;

namespace NexusScholar.Kernel;

public sealed record NdjsonCanonicalizerOptions
{
    public bool NormalizeCrLfToLf { get; init; }
}

public static class NdjsonCanonicalizer
{
    public static string Canonicalize(string content, NdjsonCanonicalizerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        options ??= new NdjsonCanonicalizerOptions();

        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            throw new InvalidOperationException("NDJSON digest input must not include a UTF-8 BOM.");
        }

        if (options.NormalizeCrLfToLf)
        {
            content = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        else if (content.Contains('\r'))
        {
            throw new InvalidOperationException("NDJSON digest input must use LF-only line endings unless CRLF normalization is explicitly enabled.");
        }

        if (content.Contains('\r'))
        {
            throw new InvalidOperationException("NDJSON digest input contains unsupported carriage-return characters.");
        }

        return content;
    }

    public static byte[] CanonicalizeToUtf8Bytes(string content, NdjsonCanonicalizerOptions? options = null)
    {
        return Encoding.UTF8.GetBytes(Canonicalize(content, options));
    }
}
