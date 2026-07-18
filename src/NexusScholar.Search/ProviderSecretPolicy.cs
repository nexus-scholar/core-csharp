using System.Text.RegularExpressions;

namespace NexusScholar.Search;

public static class ProviderSecretPolicy
{
    private const int MaxDecodePasses = 2;
    private static readonly Regex EmailAddressPattern = new(
        @"[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        Regex.InfiniteMatchTimeout);

    private static readonly string[] ForbiddenNameFragments =
    [
        "://",
        "mailto",
        "authorization",
        "api_key",
        "api-key",
        "apikey",
        "x-api-key",
        "access_key",
        "access-key",
        "password",
        "secret",
        "token",
        "credential",
        "signature",
        "contact",
        "email",
        "phone",
        "telephone"
    ];

    private static readonly HashSet<string> ForbiddenExactNames = new(
        [
            "sig",
            "awsaccesskeyid",
            "access_key",
            "access-key",
            "x-amz-signature",
            "x-amz-credential",
            "x-amz-security-token",
            "x-goog-signature",
            "x-goog-credential"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ForbiddenSignedValueFragments =
    [
        "credential=",
        "signature=",
        "x-amz-signature=",
        "x-amz-credential=",
        "x-goog-signature=",
        "x-goog-credential=",
        "awsaccesskeyid=",
        "access_key=",
        "access-key="
    ];

    private static readonly string[] ForbiddenQueryValueFragments =
    [
        "://",
        "mailto:",
        "authorization=",
        "authorization:",
        "api_key=",
        "api-key=",
        "apikey=",
        "x-api-key=",
        "password=",
        "secret=",
        "token=",
        "contact=",
        "email=",
        "phone=",
        "telephone=",
        "tel:",
        "bearer ",
        "sk-"
    ];

    private static readonly string[] ForbiddenPayloadFragments =
    [
        "://",
        "mailto:",
        "authorization",
        "api_key",
        "api-key",
        "apikey",
        "x-api-key",
        "password",
        "secret",
        "token=",
        "contact",
        "email",
        "bearer ",
        "sk-"
    ];

    public static bool ContainsForbiddenValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!TryDecode(value, out var decoded))
        {
            return true;
        }

        return ForbiddenPayloadFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase)) ||
            ForbiddenPayloadFragments.Any(fragment => decoded.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    public static bool ContainsForbiddenQueryValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!TryDecode(value, out var decoded))
        {
            return true;
        }

        return ContainsAny(value, ForbiddenQueryValueFragments) ||
            ContainsAny(decoded, ForbiddenQueryValueFragments) ||
            ContainsAny(value, ForbiddenSignedValueFragments) ||
            ContainsAny(decoded, ForbiddenSignedValueFragments) ||
            EmailAddressPattern.IsMatch(decoded);
    }

    public static bool ContainsForbiddenDescriptorValue(
        string name,
        string? value = null,
        bool allowPaginationToken = false)
    {
        if (!TryDecode(name, out var rawName))
        {
            return true;
        }

        var tokenIsAllowed = allowPaginationToken &&
            string.Equals(rawName, "token", StringComparison.OrdinalIgnoreCase);
        var normalizedName = NormalizeDescriptorName(rawName);
        if (!tokenIsAllowed &&
            (ContainsAny(rawName, ForbiddenNameFragments) ||
             ContainsAny(normalizedName, ForbiddenNameFragments) ||
             ContainsAny(name, ForbiddenNameFragments) ||
             ForbiddenExactNames.Contains(rawName) ||
             ForbiddenExactNames.Contains(normalizedName)))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return ContainsForbiddenQueryValue(value);
    }

    private static bool ContainsAny(string value, IEnumerable<string> fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static bool TryDecode(string value, out string decoded)
    {
        try
        {
            var normalized = NormalizeForSecretPolicy(value);
            for (var pass = 0; ; pass++)
            {
                if (!HasValidPercentEncoding(normalized))
                {
                    decoded = string.Empty;
                    return false;
                }

                var unescaped = Uri.UnescapeDataString(normalized);
                if (string.Equals(unescaped, normalized, StringComparison.Ordinal))
                {
                    decoded = unescaped;
                    return true;
                }

                if (pass + 1 >= MaxDecodePasses)
                {
                    decoded = unescaped;
                    return false;
                }

                normalized = NormalizeForSecretPolicy(unescaped);
                if (!HasValidPercentEncoding(normalized))
                {
                    decoded = unescaped;
                    return true;
                }
            }
        }
        catch (UriFormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    private static string NormalizeForSecretPolicy(string value) =>
        value.Replace('+', ' ');

    private static string NormalizeDescriptorName(string value) =>
        new(value.Select(character =>
            char.IsWhiteSpace(character) || character is '+' or '-'
                ? '_'
                : character).ToArray());

    private static bool HasValidPercentEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
            {
                continue;
            }

            if (index + 2 >= value.Length ||
                !Uri.IsHexDigit(value[index + 1]) ||
                !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        return true;
    }
}
