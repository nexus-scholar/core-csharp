using System.Globalization;

namespace NexusScholar.Kernel;

public static class CanonicalTimestamp
{
    public const string DefaultUtcFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public static string FormatUtc(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString(DefaultUtcFormat, CultureInfo.InvariantCulture);
    }

    public static bool IsCanonicalUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return DateTimeOffset.TryParseExact(
                value,
                DefaultUtcFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _)
            && value.EndsWith('Z');
    }

    public static void ValidateCanonicalUtc(string value)
    {
        if (!IsCanonicalUtc(value))
        {
            throw new FormatException("Canonical UTC timestamps must use the format yyyy-MM-ddTHH:mm:ss.fffffffZ.");
        }
    }
}
