using NexusScholar.Kernel;

namespace NexusScholar.Search;

public static class ProviderEvidenceCacheErrorCodes
{
    public const string CachePolicyDenied = "provider-evidence-cache-policy-denied";
    public const string InvalidCacheDescriptor = "provider-evidence-cache-invalid-descriptor";
    public const string IncompleteResponse = "provider-evidence-cache-incomplete-response";
    public const string IncompatiblePolicy = "provider-evidence-cache-incompatible-policy";
    public const string InvalidResponseEvidence = "provider-evidence-cache-invalid-response-evidence";
    public const string DigestMismatch = "provider-evidence-cache-digest-mismatch";
    public const string StoreIndexCorrupt = "provider-evidence-cache-index-corrupt";
}

public enum ProviderEvidenceCacheRetentionMode
{
    Denied = 0,
    DigestOnly = 1,
    RetainBody = 2
}

public sealed class ProviderEvidenceCachePolicy
{
    public const string PolicySchemaId = "nexus.search.provider-evidence-cache-policy";
    public const string PolicySchemaVersion = "1.0.0";
    public const string MatrixSchemaVersion = "1.0.0";

    private ProviderEvidenceCachePolicy(
        string providerAlias,
        string operation,
        ProviderEvidenceCacheRetentionMode retentionMode,
        TimeSpan retentionWindow,
        string policyIdentity)
    {
        ProviderAlias = Normalize(providerAlias);
        Operation = Normalize(operation);

        if (!Enum.IsDefined(typeof(ProviderEvidenceCacheRetentionMode), retentionMode))
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor,
                "Provider evidence cache retention mode is invalid.");
        }

        RetentionMode = retentionMode;

        if (retentionWindow <= TimeSpan.Zero)
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor,
                "Provider evidence cache retention window must be positive.");
        }

        RetentionWindow = retentionWindow;
        PolicyIdentity = ProviderAcquisitionRequest.Require(policyIdentity, nameof(policyIdentity)).ToLowerInvariant();
    }

    public string ProviderAlias { get; }
    public string Operation { get; }
    public ProviderEvidenceCacheRetentionMode RetentionMode { get; }
    public TimeSpan RetentionWindow { get; }
    public string PolicyIdentity { get; }
    public bool IsAllowed => RetentionMode != ProviderEvidenceCacheRetentionMode.Denied;
    public bool RetainsBody => RetentionMode == ProviderEvidenceCacheRetentionMode.RetainBody;
    public bool IsDigestOnly => RetentionMode == ProviderEvidenceCacheRetentionMode.DigestOnly;

    public static ProviderEvidenceCachePolicy Create(
        string providerAlias,
        string operation,
        ProviderEvidenceCacheRetentionMode retentionMode,
        TimeSpan retentionWindow,
        string policyIdentity) =>
        new(providerAlias, operation, retentionMode, retentionWindow, policyIdentity);

    public static ProviderEvidenceCachePolicy DenyAll(string providerAlias, string operation) =>
        new(providerAlias, operation, ProviderEvidenceCacheRetentionMode.Denied, TimeSpan.FromMinutes(1), "fe-09e-denied-v1");

    public static ProviderEvidenceCacheRetentionMode ParseRetentionMode(string value)
    {
        if (!Enum.TryParse(value, true, out ProviderEvidenceCacheRetentionMode parsed) ||
            !Enum.IsDefined(typeof(ProviderEvidenceCacheRetentionMode), parsed))
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor,
                "Provider evidence cache retention mode is invalid.");
        }

        return parsed;
    }

    public DigestEnvelope Envelope() =>
        new(
            DigestScope.CanonicalJsonRecord,
            PolicySchemaId,
            PolicySchemaVersion,
            new CanonicalJsonObject()
                .Add("operation", Operation)
                .Add("policy_id", PolicyIdentity)
                .Add("provider_alias", ProviderAlias)
                .Add("retention_mode", RetentionMode.ToString())
                .Add("retention_seconds", (long)RetentionWindow.TotalSeconds));

    public ContentDigest Digest => Envelope().ComputeDigest();

    private static string Normalize(string value) =>
        ProviderAcquisitionRequest.Require(value, nameof(value)).ToLowerInvariant();
}

public static class ProviderEvidenceCachePolicies
{
    public const string OpenAlexAlias = "openalex";
    public const string SemanticScholarAlias = "semantic_scholar";
    public const string CrossrefAlias = "crossref";

    public const string OpenAlexWorks = "openalex.works";
    public const string SemanticScholarBulkSearch = "semantic_scholar.bulk-search";
    public const string SemanticScholarPaperBatch = "semantic_scholar.paper-batch";
    public const string CrossrefWorks = "crossref.works";

    public static readonly ProviderEvidenceCachePolicy OpenAlexWorksPolicy = ProviderEvidenceCachePolicy.Create(
        OpenAlexAlias,
        OpenAlexWorks,
        ProviderEvidenceCacheRetentionMode.RetainBody,
        TimeSpan.FromDays(14),
        "fe-09e-openalex-works-retain-body-v1");

    public static readonly ProviderEvidenceCachePolicy SemanticScholarBulkSearchPolicy = ProviderEvidenceCachePolicy.Create(
        SemanticScholarAlias,
        SemanticScholarBulkSearch,
        ProviderEvidenceCacheRetentionMode.DigestOnly,
        TimeSpan.FromDays(14),
        "fe-09e-semantic-scholar-bulk-search-digest-only-v1");

    public static readonly ProviderEvidenceCachePolicy SemanticScholarPaperBatchPolicy = ProviderEvidenceCachePolicy.Create(
        SemanticScholarAlias,
        SemanticScholarPaperBatch,
        ProviderEvidenceCacheRetentionMode.DigestOnly,
        TimeSpan.FromDays(14),
        "fe-09e-semantic-scholar-paper-batch-digest-only-v1");

    public static readonly ProviderEvidenceCachePolicy CrossrefRecordedOnlyPolicy = ProviderEvidenceCachePolicy.Create(
        CrossrefAlias,
        CrossrefWorks,
        ProviderEvidenceCacheRetentionMode.Denied,
        TimeSpan.FromHours(1),
        "fe-09e-crossref-runtime-cache-denied-v1");

    public static bool TryGet(string providerAlias, string operation, out ProviderEvidenceCachePolicy policy)
    {
        var key = PolicyKey(providerAlias, operation);
        return PolicyIndex.TryGetValue(key, out policy!);
    }

    public static ProviderEvidenceCachePolicy Resolve(string providerAlias, string operation) =>
        TryGet(providerAlias, operation, out var policy)
            ? policy
            : ProviderEvidenceCachePolicy.DenyAll(providerAlias, operation);

    private static readonly Dictionary<string, ProviderEvidenceCachePolicy> PolicyIndex = new(StringComparer.Ordinal)
    {
        [PolicyKey(OpenAlexAlias, OpenAlexWorks)] = OpenAlexWorksPolicy,
        [PolicyKey(SemanticScholarAlias, SemanticScholarBulkSearch)] = SemanticScholarBulkSearchPolicy,
        [PolicyKey(SemanticScholarAlias, SemanticScholarPaperBatch)] = SemanticScholarPaperBatchPolicy,
        [PolicyKey(CrossrefAlias, CrossrefWorks)] = CrossrefRecordedOnlyPolicy
    };

    private static string PolicyKey(string providerAlias, string operation) =>
        $"{ProviderAcquisitionRequest.Require(providerAlias, nameof(providerAlias)).ToLowerInvariant()}::{ProviderAcquisitionRequest.Require(operation, nameof(operation)).ToLowerInvariant()}";
}

public sealed class ProviderEvidenceCacheKey
{
    public const string SchemaId = "nexus.search.provider-evidence-cache-key";
    public const string SchemaVersion = "1.0.0";

    private ProviderEvidenceCacheKey(
        string providerAlias,
        string operation,
        ContentDigest sanitizedRequestDigest,
        ContentDigest pageRequestDigest,
        string parserId,
        string parserVersion)
    {
        ProviderAlias = ProviderAcquisitionRequest.Require(providerAlias, nameof(providerAlias)).ToLowerInvariant();
        Operation = ProviderAcquisitionRequest.Require(operation, nameof(operation)).ToLowerInvariant();

        if (!sanitizedRequestDigest.IsValid)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor, "Sanitized request digest is invalid.");
        }

        if (!pageRequestDigest.IsValid)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor, "Page request digest is invalid.");
        }

        SanitizedRequestDigest = sanitizedRequestDigest;
        PageRequestDigest = pageRequestDigest;
        ParserId = ProviderAcquisitionRequest.Require(parserId, nameof(parserId));
        ParserVersion = ProviderAcquisitionRequest.Require(parserVersion, nameof(parserVersion));
        Identity = ComputeIdentity(this);
    }

    public string ProviderAlias { get; }
    public string Operation { get; }
    public ContentDigest SanitizedRequestDigest { get; }
    public ContentDigest PageRequestDigest { get; }
    public string ParserId { get; }
    public string ParserVersion { get; }
    public ContentDigest Identity { get; }

    public static ProviderEvidenceCacheKey Create(
        string providerAlias,
        string operation,
        ContentDigest sanitizedRequestDigest,
        ContentDigest pageRequestDigest,
        string parserId,
        string parserVersion) => new(providerAlias, operation, sanitizedRequestDigest, pageRequestDigest, parserId, parserVersion);

    public static ProviderEvidenceCacheKey Restore(
        string providerAlias,
        string operation,
        ContentDigest sanitizedRequestDigest,
        ContentDigest pageRequestDigest,
        string parserId,
        string parserVersion) => new(providerAlias, operation, sanitizedRequestDigest, pageRequestDigest, parserId, parserVersion);

    public bool HasParser(string parserId, string parserVersion) =>
        string.Equals(ParserId, parserId, StringComparison.Ordinal) &&
        string.Equals(ParserVersion, parserVersion, StringComparison.Ordinal);

    public DigestEnvelope Envelope() =>
        new(
            DigestScope.CanonicalJsonRecord,
            SchemaId,
            SchemaVersion,
            new CanonicalJsonObject()
                .Add("operation", Operation)
                .Add("page_request_digest", PageRequestDigest.ToString())
                .Add("parser_id", ParserId)
                .Add("parser_version", ParserVersion)
                .Add("provider_alias", ProviderAlias)
                .Add("sanitized_request_digest", SanitizedRequestDigest.ToString()));

    public ContentDigest Digest => Envelope().ComputeDigest();
    public ContentDigest IdentityDigest => Digest;

    public override string ToString() => Identity.ToString();

    private static ContentDigest ComputeIdentity(ProviderEvidenceCacheKey key) => key.Envelope().ComputeDigest();
}

public sealed class ProviderEvidenceCacheEntry
{
    public const string SchemaId = "nexus.search.provider-evidence-cache-entry";
    public const string SchemaVersion = "1.0.0";

    private ProviderEvidenceCacheEntry(
        ProviderEvidenceCacheKey key,
        string policyIdentity,
        ProviderEvidenceCacheRetentionMode retentionMode,
        TimeSpan retentionWindow,
        DateTimeOffset storedAt,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        int statusCode,
        string mediaType,
        long byteLength,
        ContentDigest responseDigest,
        ContentDigest responseEvidenceDigest,
        bool bodyRetained)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        PolicyIdentity = ProviderAcquisitionRequest.Require(policyIdentity, nameof(policyIdentity));

        if (!Enum.IsDefined(typeof(ProviderEvidenceCacheRetentionMode), retentionMode))
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor, "Provider evidence cache retention mode is invalid.");
        }

        if (retentionMode == ProviderEvidenceCacheRetentionMode.DigestOnly && bodyRetained)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, "Digest-only cache mode cannot retain body bytes.");
        }

        if (retentionWindow <= TimeSpan.Zero)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor, "Provider evidence cache retention window must be positive.");
        }

        RetentionMode = retentionMode;
        RetentionWindow = retentionWindow;
        ProviderAcquisitionRequest.RequireUtc(storedAt, nameof(storedAt));
        ProviderAcquisitionRequest.RequireUtc(requestedAt, nameof(requestedAt));
        ProviderAcquisitionRequest.RequireUtc(receivedAt, nameof(receivedAt));

        if (receivedAt < requestedAt || storedAt < receivedAt)
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor,
                "Cache entry timestamps must be ordered request, receipt, then storage.");
        }

        if (byteLength < 0)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor, "Cache entry byte length must be non-negative.");
        }

        if (!responseDigest.IsValid || !responseEvidenceDigest.IsValid)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor, "Cache entry response digest is invalid.");
        }

        if (statusCode != 200)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompleteResponse, "Cache entries require complete 200 responses.");
        }

        StatusCode = statusCode;
        MediaType = ProviderAcquisitionRequest.Require(mediaType, nameof(mediaType)).ToLowerInvariant();
        ResponseDigest = responseDigest;
        ResponseEvidenceDigest = responseEvidenceDigest;
        ByteLength = byteLength;
        RequestedAt = requestedAt;
        ReceivedAt = receivedAt;
        StoredAt = storedAt;
        IsBodyRetained = bodyRetained;
        ExpiresAt = storedAt + retentionWindow;
    }

    public ProviderEvidenceCacheKey Key { get; }
    public string PolicyIdentity { get; }
    public ProviderEvidenceCacheRetentionMode RetentionMode { get; }
    public TimeSpan RetentionWindow { get; }
    public DateTimeOffset StoredAt { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public int StatusCode { get; }
    public string MediaType { get; }
    public long ByteLength { get; }
    public ContentDigest ResponseDigest { get; }
    public ContentDigest ResponseEvidenceDigest { get; }
    public bool IsBodyRetained { get; }

    public bool IsFresh(DateTimeOffset at)
    {
        ProviderAcquisitionRequest.RequireUtc(at, nameof(at));
        return at <= ExpiresAt;
    }

    public bool IsExpired(DateTimeOffset at) => !IsFresh(at);

    public static ProviderEvidenceCacheEntry Create(
        ProviderEvidenceCacheKey key,
        ProviderEvidenceCachePolicy policy,
        RuntimeProviderResponseEvidence response,
        DateTimeOffset storedAt)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(response);

        if (!policy.IsAllowed)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.CachePolicyDenied, "Provider evidence cache policy denied this operation.");
        }

        if (!string.Equals(policy.ProviderAlias, key.ProviderAlias, StringComparison.Ordinal) ||
            !string.Equals(policy.Operation, key.Operation, StringComparison.Ordinal))
        {
            throw new SearchRuleException(
                ProviderEvidenceCacheErrorCodes.IncompatiblePolicy,
                "Provider evidence cache policy does not match cache key.");
        }

        if (!string.Equals(response.ParserId, key.ParserId, StringComparison.Ordinal) ||
            !string.Equals(response.ParserVersion, key.ParserVersion, StringComparison.Ordinal))
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, "Response parser does not match cache key parser.");
        }

        if (!response.BodyComplete)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompleteResponse, "Only complete responses can be cached.");
        }

        if (response.StatusCode != 200)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompleteResponse, "Only complete 200 responses can be cached.");
        }

        if (response.SanitizedRequestDigest != key.SanitizedRequestDigest)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.IncompatiblePolicy, "Response request digest does not match cache key.");
        }

        return new ProviderEvidenceCacheEntry(
            key,
            policy.PolicyIdentity,
            policy.RetentionMode,
            policy.RetentionWindow,
            storedAt,
            response.RequestedAt,
            response.ReceivedAt,
            response.StatusCode,
            response.MediaType,
            response.ByteLength,
            response.RawResponseDigest,
            response.Digest,
            policy.RetainsBody);
    }

    public static ProviderEvidenceCacheEntry Restore(
        ProviderEvidenceCacheKey key,
        string policyIdentity,
        ProviderEvidenceCacheRetentionMode retentionMode,
        TimeSpan retentionWindow,
        DateTimeOffset storedAt,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        DateTimeOffset expiresAt,
        int statusCode,
        string mediaType,
        long byteLength,
        ContentDigest responseDigest,
        ContentDigest responseEvidenceDigest,
        bool isBodyRetained)
    {
        var entry = new ProviderEvidenceCacheEntry(
            key,
            policyIdentity,
            retentionMode,
            retentionWindow,
            storedAt,
            requestedAt,
            receivedAt,
            statusCode,
            mediaType,
            byteLength,
            responseDigest,
            responseEvidenceDigest,
            isBodyRetained);

        if (entry.ExpiresAt != expiresAt)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidCacheDescriptor, "Invalid cache expiry timestamps.");
        }

        return entry;
    }

    public void VerifyResponseEvidence(RuntimeProviderResponseEvidence response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!string.Equals(response.ProviderAlias, Key.ProviderAlias, StringComparison.Ordinal) ||
            !string.Equals(response.ParserId, Key.ParserId, StringComparison.Ordinal) ||
            !string.Equals(response.ParserVersion, Key.ParserVersion, StringComparison.Ordinal) ||
            response.SanitizedRequestDigest != Key.SanitizedRequestDigest ||
            response.RawResponseDigest != ResponseDigest ||
            response.ByteLength != ByteLength ||
            !string.Equals(response.MediaType, MediaType, StringComparison.Ordinal) ||
            response.StatusCode != StatusCode ||
            response.RequestedAt != RequestedAt ||
            response.ReceivedAt != ReceivedAt ||
            response.Digest != ResponseEvidenceDigest)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.InvalidResponseEvidence, "Stored response evidence no longer reproduces this cache entry.");
        }
    }

    public void VerifyBody(byte[] exactIdentityEncodedBodyBytes)
    {
        ArgumentNullException.ThrowIfNull(exactIdentityEncodedBodyBytes);

        if (exactIdentityEncodedBodyBytes.LongLength != ByteLength || ContentDigest.Sha256(exactIdentityEncodedBodyBytes) != ResponseDigest)
        {
            throw new SearchRuleException(ProviderEvidenceCacheErrorCodes.DigestMismatch, "Body digest does not match cached response digest.");
        }
    }

    public DigestEnvelope Envelope() =>
        new(
            DigestScope.CanonicalJsonRecord,
            SchemaId,
            SchemaVersion,
            new CanonicalJsonObject()
                .Add("body_length", ByteLength)
                .Add("body_retained", IsBodyRetained)
                .Add("operation", Key.Operation)
                .Add("page_request_digest", Key.PageRequestDigest.ToString())
                .Add("parser_id", Key.ParserId)
                .Add("parser_version", Key.ParserVersion)
                .Add("policy_id", PolicyIdentity)
                .Add("provider_alias", Key.ProviderAlias)
                .Add("raw_response_digest", ResponseDigest.ToString())
                .Add("raw_response_evidence_digest", ResponseEvidenceDigest.ToString())
                .Add("request_digest", Key.SanitizedRequestDigest.ToString())
                .Add("retention_mode", RetentionMode.ToString())
                .Add("retention_seconds", (long)RetentionWindow.TotalSeconds)
                .AddTimestamp("request_time", RequestedAt)
                .AddTimestamp("received_time", ReceivedAt)
                .AddTimestamp("stored_time", StoredAt)
                .Add("status_code", StatusCode)
                .AddTimestamp("expiration_utc", ExpiresAt));

    public ContentDigest Digest => Envelope().ComputeDigest();
}
