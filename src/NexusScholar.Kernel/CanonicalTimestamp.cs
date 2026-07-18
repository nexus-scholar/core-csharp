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
        return IsCanonicalUtc(value, rejectDefault: false);
    }

    public static bool IsCanonicalUtc(string? value, bool rejectDefault)
    {
        if (!TryParseCanonical(value, out var valueUtc, rejectDefault))
        {
            return false;
        }

        return valueUtc.Offset == TimeSpan.Zero;
    }

    public static bool IsCanonicalUtc(DateTimeOffset value, bool rejectDefault)
    {
        return value.Offset == TimeSpan.Zero && (!rejectDefault || value != default);
    }

    public static void ValidateCanonicalUtc(string value)
    {
        ValidateCanonicalUtc(value, rejectDefault: true);
    }

    public static void ValidateCanonicalUtc(string value, bool rejectDefault)
    {
        if (!IsCanonicalUtc(value, rejectDefault))
        {
            throw new FormatException("Canonical UTC timestamps must use the format yyyy-MM-ddTHH:mm:ss.fffffffZ.");
        }
    }

    private static bool TryParseCanonical(string? value, out DateTimeOffset parsed, bool rejectDefault)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                value,
                DefaultUtcFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed) || !value.EndsWith('Z'))
        {
            return false;
        }

        return !rejectDefault || parsed != default;
    }
}
