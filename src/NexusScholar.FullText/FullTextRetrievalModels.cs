using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using NexusScholar.Kernel;

namespace NexusScholar.FullText;

public static class FullTextRetrievalRights
{
    public const string OpenAccess = "open-access";
    public const string Licensed = "licensed";
    public const string PublicDomain = "public-domain";

    public static bool IsAdmitted(string status) => string.Equals(status, OpenAccess, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, Licensed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, PublicDomain, StringComparison.OrdinalIgnoreCase);
}

public static class FullTextRetrievalAccessRoutes
{
    public const string LandingPage = "landing-page";
    public const string Repository = "repository";
    public const string ProviderApi = "provider-api";
    public const string DoiLookup = "doi-lookup";

    public static bool IsKnown(string value) => value is LandingPage or Repository or ProviderApi or DoiLookup;
}

public static class FullTextRetrievalRetention
{
    public const string RetainedLocalFixture = "retained-local-retrieval-fixture";
}

public static class FullTextRetrievalErrorCodes
{
    public const string InvalidEvidence = "invalid-fulltext-retrieval-evidence";
    public const string RedirectChainViolation = "fulltext-retrieval-redirect-policy-violation";
    public const string InvalidUriPolicy = "fulltext-retrieval-uri-policy-violation";
    public const string RightsNotAdmitted = "fulltext-retrieval-rights-not-admitted";
    public const string InvalidClock = "fulltext-retrieval-timestamp-violation";
    public const string ByteLengthMismatch = "fulltext-retrieval-byte-length-mismatch";
    public const string DigestMismatch = "fulltext-retrieval-digest-mismatch";
    public const string IncompleteBody = "fulltext-retrieval-incomplete-body";
    public const string UnsupportedEncoding = "fulltext-retrieval-encoded-body-not-allowed";
    public const string OversizedBody = "fulltext-retrieval-body-exceeded-maximum";
    public const string AccessRouteMissing = "fulltext-retrieval-access-route-missing";
    public const string ConversionFailed = "fulltext-retrieval-conversion-failed";
    public const string TerminalFailure = "fulltext-retrieval-terminal-failure";
}

public sealed class FullTextRecordedRedirect
{
    public FullTextRecordedRedirect(string redirectUrl, int statusCode)
    {
        RedirectUrl = Guard.NotBlank(redirectUrl, nameof(redirectUrl));
        FullTextRetrievalUriPolicy.RejectCredentialBearingReference(RedirectUrl, nameof(redirectUrl));
        if (statusCode is < 300 or > 399)
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "Recorded redirect status must be an HTTP redirect status.");
        }

        StatusCode = statusCode;
    }

    public string RedirectUrl { get; }
    public int StatusCode { get; }
}

public sealed class FullTextRecordedRetrievalPolicy
{
    public const long DefaultMaxBytes = 8 * 1024 * 1024;

    public FullTextRecordedRetrievalPolicy(
        IReadOnlyCollection<string> admittedHosts,
        long maximumBytes = DefaultMaxBytes,
        string? retentionDisposition = null)
    {
        ArgumentNullException.ThrowIfNull(admittedHosts);
        if (maximumBytes <= 0)
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "Maximum accepted bytes must be positive.");
        }

        RetentionDisposition = retentionDisposition is null ? FullTextRetrievalRetention.RetainedLocalFixture : Guard.NotBlank(retentionDisposition, nameof(retentionDisposition));
        MaximumBytes = maximumBytes;
        AdmittedHosts = new ReadOnlyCollection<string>(
            admittedHosts.Select(host => Guard.NotBlank(host, nameof(host)).ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray());
        if (AdmittedHosts.Count == 0)
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "At least one admitted redirect host is required.");
        }
    }

    public long MaximumBytes { get; }
    public ReadOnlyCollection<string> AdmittedHosts { get; }
    public string RetentionDisposition { get; }
}

public sealed class FullTextRecordedRetrievalEvidence
{
    public const string SchemaId = "nexus.fulltext.recorded-retrieval-evidence";
    public const string SchemaVersion = "1.0.0";

    private FullTextRecordedRetrievalEvidence(
        string evidenceId,
        FullTextInput input,
        string sourceAlias,
        string sourceReference,
        string accessRoute,
        string rightsStatus,
        string rightsReference,
        string artifactKind,
        string mediaType,
        int httpStatus,
        string? contentEncoding,
        long byteLength,
        string rawByteDigest,
        string? retentionDisposition,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        bool responseComplete,
        IReadOnlyList<FullTextRecordedRedirect> redirectChain,
        string? terminalFailureCategory,
        string? terminalFailureSummary)
    {
        EvidenceId = Guard.NotBlank(evidenceId, nameof(evidenceId));
        Input = input ?? throw new ArgumentNullException(nameof(input));
        InputDigest = ContentDigest.Sha256(FullTextAuthorityCanonicalCodec.Serialize(Input));
        SourceAlias = Guard.NotBlank(sourceAlias, nameof(sourceAlias));
        SourceReference = Guard.NotBlank(sourceReference, nameof(sourceReference));
        FullTextRetrievalUriPolicy.RejectCredentialBearingReference(SourceReference, nameof(sourceReference));
        AccessRoute = Guard.NotBlank(accessRoute, nameof(accessRoute)).ToLowerInvariant();
        RightsStatus = Guard.NotBlank(rightsStatus, nameof(rightsStatus));
        RightsReference = Guard.NotBlank(rightsReference, nameof(rightsReference));
        FullTextRetrievalUriPolicy.RejectCredentialBearingReference(RightsReference, nameof(rightsReference));
        ArtifactKind = Guard.NotBlank(artifactKind, nameof(artifactKind));
        MediaType = Guard.NotBlank(mediaType, nameof(mediaType));
        if (httpStatus is < 100 or > 599)
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "Recorded HTTP status must be a valid response status code.");
        }

        HttpStatus = httpStatus;
        ContentEncoding = contentEncoding?.Trim();
        if (byteLength < 0)
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "Recorded byte length cannot be negative.");
        }

        if (!long.TryParse(byteLength.ToString(CultureInfo.InvariantCulture), out _))
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "Recorded byte length must be numeric.");
        }

        ResponseComplete = responseComplete;
        RedirectChain = new ReadOnlyCollection<FullTextRecordedRedirect>((redirectChain ?? Array.Empty<FullTextRecordedRedirect>()).ToArray());
        if (RedirectChain.Any(item => item is null))
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "Recorded redirect chain must not contain null entries.");
        }

        ByteLength = byteLength;
        RawByteDigest = Guard.NotBlank(rawByteDigest, nameof(rawByteDigest));
        if (!ContentDigest.TryParse(RawByteDigest, out _))
        {
            throw new FullTextRuleException(FullTextErrorCodes.MissingRawArtifactDigest, "Recorded byte digest must be a valid canonical SHA-256 digest.");
        }

        RawByteDigestScope = DigestScope.RawArtifactBytes.ToString();
        RetentionDisposition = Guard.NotBlank(retentionDisposition ?? FullTextRetrievalRetention.RetainedLocalFixture, nameof(retentionDisposition));
        RequestedAt = requestedAt;
        ReceivedAt = receivedAt;
        if (!CanonicalTimestamp.IsCanonicalUtc(requestedAt, rejectDefault: true) ||
            !CanonicalTimestamp.IsCanonicalUtc(receivedAt, rejectDefault: true) || receivedAt < requestedAt)
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidClock, "Recorded retrieval timestamps must be UTC and request before or equal receipt.");
        }

        TerminalFailureCategory = string.IsNullOrWhiteSpace(terminalFailureCategory)
            ? null : terminalFailureCategory.Trim();
        TerminalFailureSummary = terminalFailureSummary is null ? null : terminalFailureSummary.Trim();
        if ((TerminalFailureCategory is null) != (TerminalFailureSummary is null))
        {
            throw new FullTextRuleException(FullTextRetrievalErrorCodes.InvalidEvidence, "Terminal failure summary and category must be supplied together.");
        }
    }

    public string EvidenceId { get; }
    public FullTextInput Input { get; }
    public ContentDigest InputDigest { get; }
    public string SourceAlias { get; }
    public string SourceReference { get; }
    public string AccessRoute { get; }
    public string RightsStatus { get; }
    public string RightsReference { get; }
    public string ArtifactKind { get; }
    public string MediaType { get; }
    public int HttpStatus { get; }
    public string? ContentEncoding { get; }
    public long ByteLength { get; }
    public string RawByteDigest { get; }
    public string RawByteDigestScope { get; }
    public bool ResponseComplete { get; }
    public string RetentionDisposition { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public ReadOnlyCollection<FullTextRecordedRedirect> RedirectChain { get; }
    public string? TerminalFailureCategory { get; }
    public string? TerminalFailureSummary { get; }
    public ContentDigest Digest => Envelope().ComputeDigest();

    public static FullTextRecordedRetrievalEvidence Record(
        string evidenceId,
        FullTextInput input,
        string sourceAlias,
        string sourceReference,
        string accessRoute,
        string rightsStatus,
        string rightsReference,
        string artifactKind,
        string mediaType,
        int httpStatus,
        byte[] exactBytes,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        bool responseComplete = true,
        string? contentEncoding = null,
        IReadOnlyList<FullTextRecordedRedirect>? redirectChain = null,
        string? terminalFailureCategory = null,
        string? terminalFailureSummary = null,
        string retentionDisposition = FullTextRetrievalRetention.RetainedLocalFixture)
    {
        ArgumentNullException.ThrowIfNull(exactBytes);
        return new FullTextRecordedRetrievalEvidence(
            evidenceId,
            input,
            sourceAlias,
            sourceReference,
            accessRoute,
            rightsStatus,
            rightsReference,
            artifactKind,
            mediaType,
            httpStatus,
            contentEncoding,
            exactBytes.LongLength,
            ContentDigest.Sha256(exactBytes).ToString(),
            retentionDisposition,
            requestedAt,
            receivedAt,
            responseComplete,
            redirectChain ?? Array.Empty<FullTextRecordedRedirect>(),
            terminalFailureCategory,
            terminalFailureSummary);
    }

    public CanonicalJsonObject ToCanonicalJson() => Envelope().ToCanonicalJsonObject();

    public byte[] ToCanonicalBytes() => Envelope().ToCanonicalJsonBytes();

    private DigestEnvelope Envelope()
    {
        var content = new CanonicalJsonObject()
            .Add("access_route", AccessRoute)
            .Add("artifact_kind", ArtifactKind)
            .Add("byte_length", ByteLength)
            .Add("evidence_id", EvidenceId)
            .Add("http_status", HttpStatus)
            .Add("input_digest", InputDigest.ToString())
            .Add("media_type", MediaType)
            .Add("raw_byte_digest", RawByteDigest)
            .Add("raw_byte_digest_scope", RawByteDigestScope)
            .AddTimestamp("received_at", ReceivedAt)
            .Add("redirect_chain", new CanonicalJsonArray(RedirectChain.Select(item =>
                new CanonicalJsonObject()
                    .Add("status_code", item.StatusCode)
                    .Add("url", item.RedirectUrl))))
            .Add("response_complete", ResponseComplete)
            .Add("retention_disposition", RetentionDisposition)
            .Add("rights_reference", RightsReference)
            .Add("rights_status", RightsStatus)
            .Add("source_alias", SourceAlias)
            .Add("source_reference", SourceReference)
            .AddTimestamp("requested_at", RequestedAt);

        if (ContentEncoding is not null)
        {
            content.Add("content_encoding", ContentEncoding);
        }

        if (TerminalFailureCategory is not null)
        {
            content
                .Add("terminal_failure_category", TerminalFailureCategory)
                .Add("terminal_failure_summary", TerminalFailureSummary!);
        }

        return new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            SchemaId,
            SchemaVersion,
            content);
    }
}

internal static class FullTextRetrievalUriPolicy
{
    private static readonly string[] ForbiddenValueAssignments =
    [
        "authorization=",
        "api_key=",
        "api-key=",
        "apikey=",
        "x-api-key=",
        "password=",
        "secret=",
        "token=",
        "credential=",
        "signature=",
        "sig=",
        "key=",
        "x-amz-signature=",
        "x-amz-credential=",
        "x-amz-security-token=",
        "x-goog-signature=",
        "x-goog-credential=",
        "awsaccesskeyid=",
        "access_key=",
        "access-key="
    ];

    internal static void RejectCredentialBearingReference(string value, string parameterName)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (uri.UserInfo.Length > 0 || ContainsSensitiveQueryParameter(uri.Query))
            {
                throw new FullTextRuleException(
                    FullTextRetrievalErrorCodes.InvalidUriPolicy,
                    $"Recorded URI '{parameterName}' contains credential-shaped material.");
            }

            return;
        }

        if (ContainsSensitiveQueryParameter(ExtractQuery(value)))
        {
            throw new FullTextRuleException(
                FullTextRetrievalErrorCodes.InvalidUriPolicy,
                $"Recorded URI '{parameterName}' contains credential-shaped material.");
        }
    }

    private static bool ContainsSensitiveQueryParameter(string query)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var rawName = separator < 0 ? pair : pair[..separator];
            var rawValue = separator < 0 ? string.Empty : pair[(separator + 1)..];
            if (IsCredentialBearingQueryName(rawName) || IsCredentialBearingQueryValue(rawValue))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCredentialBearingQueryName(string rawName)
    {
        var current = rawName;
        for (var decodePass = 0; decodePass < 8; decodePass++)
        {
            if (LooksCredentialName(current))
            {
                return true;
            }

            if (!TryDecodePercent(current, out var decoded))
            {
                return true;
            }

            if (string.Equals(current, decoded, StringComparison.Ordinal))
            {
                return false;
            }

            current = decoded;
        }

        return true;
    }

    private static bool IsCredentialBearingQueryValue(string rawValue)
    {
        var current = rawValue;
        for (var decodePass = 0; decodePass < 8; decodePass++)
        {
            if (LooksCredentialValue(current))
            {
                return true;
            }

            if (!TryDecodePercent(current, out var decoded))
            {
                return true;
            }

            if (string.Equals(current, decoded, StringComparison.Ordinal))
            {
                return false;
            }

            current = decoded;
        }

        return true;
    }

    private static bool LooksCredentialName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.Trim()
            .Replace('-', '_')
            .Replace('+', '_')
            .Replace(" ", "_")
            .ToLowerInvariant();

        if (normalized is "key" or "sig" or "signature" or "authorization" or "credential" or "secret")
        {
            return true;
        }

        if (normalized.Contains("api_key", StringComparison.Ordinal) ||
            normalized.Contains("apikey", StringComparison.Ordinal) ||
            normalized.Contains("token", StringComparison.Ordinal) ||
            normalized.Contains("credential", StringComparison.Ordinal) ||
            normalized.Contains("signature", StringComparison.Ordinal) ||
            normalized.EndsWith("_key", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool LooksCredentialValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Replace('+', ' ').ToLowerInvariant();
        if (ForbiddenValueAssignments.Any(fragment => normalized.Contains(fragment, StringComparison.Ordinal)) ||
            normalized.Contains("bearer ", StringComparison.Ordinal))
        {
            return true;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var nestedUri) &&
            (nestedUri.UserInfo.Length > 0 || ContainsSensitiveQueryParameter(nestedUri.Query)))
        {
            return true;
        }

        return ContainsSensitiveQueryParameter(ExtractQuery(value));
    }

    private static string ExtractQuery(string value)
    {
        var queryStart = value.IndexOf('?');
        if (queryStart < 0)
        {
            return string.Empty;
        }

        var fragmentStart = value.IndexOf('#', queryStart + 1);
        if (fragmentStart >= 0)
        {
            return value[(queryStart + 1)..fragmentStart];
        }

        return value[(queryStart + 1)..];
    }

    private static bool TryDecodePercent(string value, out string decoded)
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
                decoded = string.Empty;
                return false;
            }

            index += 2;
        }

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

public sealed class FullTextRecordedRetrievalOutcome
{
    internal FullTextRecordedRetrievalOutcome(
        bool isSuccess,
        FullTextAcquisitionRecord acquisition,
        FullTextSourceAttempt sourceAttempt,
        FullTextArtifactEvidence? artifact,
        VerifiedFullTextChain? chain,
        string? failureCategory,
        string? failureSummary)
    {
        IsSuccess = isSuccess;
        Acquisition = acquisition ?? throw new ArgumentNullException(nameof(acquisition));
        SourceAttempt = sourceAttempt ?? throw new ArgumentNullException(nameof(sourceAttempt));
        Artifact = artifact;
        Chain = chain;
        FailureCategory = failureCategory;
        FailureSummary = failureSummary;
    }

    public bool IsSuccess { get; }
    public FullTextAcquisitionRecord Acquisition { get; }
    public FullTextSourceAttempt SourceAttempt { get; }
    public FullTextArtifactEvidence? Artifact { get; }
    public VerifiedFullTextChain? Chain { get; }
    public string? FailureCategory { get; }
    public string? FailureSummary { get; }
}
