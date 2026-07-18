using System.Text.RegularExpressions;

namespace NexusScholar.Search;

public static class ProviderSecretPolicy
{
    private static readonly Regex EmailAddressPattern = new(
        @"[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly string[] ForbiddenNameFragments =
    [
        "://",
        "mailto",
        "authorization",
        "api_key",
        "api-key",
        "apikey",
        "x-api-key",
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
        if (!tokenIsAllowed &&
            (ContainsAny(rawName, ForbiddenNameFragments) ||
             ContainsAny(name, ForbiddenNameFragments) ||
             ForbiddenExactNames.Contains(rawName)))
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
            decoded = Uri.UnescapeDataString(value);
            return true;
        }
        catch (UriFormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }
}
