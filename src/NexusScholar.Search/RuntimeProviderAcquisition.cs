using System.Collections.ObjectModel;
using NexusScholar.Kernel;

namespace NexusScholar.Search;

public sealed class RuntimeProviderResponseEvidence
{
    public const string SchemaId = "nexus.search.runtime-provider-response";
    public const string SchemaVersion = "1.0.0";
    public const string RetentionDisposition = "runtime-transient-digest-only";

    private static readonly string[] AllowedHeaders =
    [
        "retry-after",
        "content-length",
        "content-type",
        "x-rate-limit-limit",
        "x-rate-limit-interval",
        "x-rate-limit-remaining",
        "x-rate-limit-credits-used",
        "x-rate-limit-reset"
    ];

    private RuntimeProviderResponseEvidence(
        string providerAlias,
        ContentDigest sanitizedRequestDigest,
        string parserId,
        string parserVersion,
        ContentDigest rawResponseDigest,
        long byteLength,
        int statusCode,
        string mediaType,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        IReadOnlyDictionary<string, string> observedHeaders)
    {
        if (!sanitizedRequestDigest.IsValid || !rawResponseDigest.IsValid ||
            byteLength < 0 || statusCode is < 100 or > 599)
        {
            throw ProviderAcquisitionRequest.Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "Runtime provider response identity is invalid.");
        }

        ProviderAcquisitionRequest.RequireUtc(requestedAt, nameof(requestedAt));
        ProviderAcquisitionRequest.RequireUtc(receivedAt, nameof(receivedAt));
        if (receivedAt < requestedAt)
        {
            throw ProviderAcquisitionRequest.Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "Runtime provider response cannot predate its request.");
        }

        var normalizedHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in observedHeaders)
        {
            var normalizedName = ProviderAcquisitionRequest.Require(name, "header name").ToLowerInvariant();
            if (!AllowedHeaders.Contains(normalizedName, StringComparer.Ordinal))
            {
                throw ProviderAcquisitionRequest.Rule(
                    ProviderAcquisitionErrorCodes.SecretBearingDescriptor,
                    "Runtime provider evidence contains an unadmitted response header.");
            }

            normalizedHeaders.Add(
                normalizedName,
                ProviderAcquisitionRequest.Require(value, $"header {normalizedName}"));
        }

        ProviderAlias = ProviderAcquisitionRequest.Require(providerAlias, nameof(providerAlias)).ToLowerInvariant();
        SanitizedRequestDigest = sanitizedRequestDigest;
        ParserId = ProviderAcquisitionRequest.Require(parserId, nameof(parserId));
        ParserVersion = ProviderAcquisitionRequest.Require(parserVersion, nameof(parserVersion));
        RawResponseDigest = rawResponseDigest;
        ByteLength = byteLength;
        StatusCode = statusCode;
        MediaType = ProviderAcquisitionRequest.Require(mediaType, nameof(mediaType)).ToLowerInvariant();
        RequestedAt = requestedAt;
        ReceivedAt = receivedAt;
        ObservedHeaders = new ReadOnlyDictionary<string, string>(normalizedHeaders);
    }

    public string ProviderAlias { get; }
    public ContentDigest SanitizedRequestDigest { get; }
    public string ParserId { get; }
    public string ParserVersion { get; }
    public ContentDigest RawResponseDigest { get; }
    public long ByteLength { get; }
    public int StatusCode { get; }
    public string MediaType { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public IReadOnlyDictionary<string, string> ObservedHeaders { get; }
    public bool BodyComplete => true;
    public string Retention => RetentionDisposition;

    internal static RuntimeProviderResponseEvidence Capture(
        string providerAlias,
        ContentDigest sanitizedRequestDigest,
        string parserId,
        string parserVersion,
        byte[] exactIdentityEncodedBodyBytes,
        int statusCode,
        string mediaType,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        IReadOnlyDictionary<string, string>? observedHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(exactIdentityEncodedBodyBytes);
        return new RuntimeProviderResponseEvidence(
            providerAlias,
            sanitizedRequestDigest,
            parserId,
            parserVersion,
            ContentDigest.Sha256(exactIdentityEncodedBodyBytes),
            exactIdentityEncodedBodyBytes.LongLength,
            statusCode,
            mediaType,
            requestedAt,
            receivedAt,
            observedHeaders ?? new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)));
    }

    public void Verify(byte[] exactIdentityEncodedBodyBytes)
    {
        ArgumentNullException.ThrowIfNull(exactIdentityEncodedBodyBytes);
        if (exactIdentityEncodedBodyBytes.LongLength != ByteLength ||
            ContentDigest.Sha256(exactIdentityEncodedBodyBytes) != RawResponseDigest)
        {
            throw ProviderAcquisitionRequest.Rule(
                ProviderAcquisitionErrorCodes.FixtureDigestMismatch,
                "Runtime provider bytes do not match their response evidence.");
        }
    }

    public DigestEnvelope Envelope()
    {
        var headers = ObservedHeaders
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
                (CanonicalJsonValue)new CanonicalJsonObject()
                    .Add("name", pair.Key)
                    .Add("value", pair.Value))
            .ToArray();
        return new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            SchemaId,
            SchemaVersion,
            new CanonicalJsonObject()
                .Add("body_complete", BodyComplete)
                .Add("byte_length", ByteLength)
                .Add("media_type", MediaType)
                .Add("observed_headers", CanonicalJsonValue.Array(headers))
                .Add("parser_id", ParserId)
                .Add("parser_version", ParserVersion)
                .Add("provider_alias", ProviderAlias)
                .Add("raw_response_digest", RawResponseDigest.ToString())
                .Add("raw_response_scope", DigestScope.RawArtifactBytes.Value)
                .AddTimestamp("received_at", ReceivedAt)
                .AddTimestamp("requested_at", RequestedAt)
                .Add("retention_disposition", Retention)
                .Add("sanitized_request_digest", SanitizedRequestDigest.ToString())
                .Add("status_code", StatusCode));
    }

    public ContentDigest Digest => Envelope().ComputeDigest();
}

public sealed class IncompleteRuntimeProviderResponseEvidence
{
    public const string SchemaId = "nexus.search.incomplete-runtime-provider-response";
    public const string SchemaVersion = "1.0.0";
    public const string RetentionDisposition = "runtime-transient-digest-only";

    public IncompleteRuntimeProviderResponseEvidence(
        string providerAlias,
        ContentDigest sanitizedRequestDigest,
        byte[] observedPrefixBytes,
        int statusCode,
        DateTimeOffset requestedAt,
        DateTimeOffset observedAt,
        string stopReason)
    {
        ArgumentNullException.ThrowIfNull(observedPrefixBytes);
        ProviderAcquisitionRequest.RequireUtc(requestedAt, nameof(requestedAt));
        ProviderAcquisitionRequest.RequireUtc(observedAt, nameof(observedAt));
        if (!sanitizedRequestDigest.IsValid ||
            statusCode is < 100 or > 599 ||
            observedAt < requestedAt)
        {
            throw ProviderAcquisitionRequest.Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "Incomplete runtime response identity is invalid.");
        }

        ProviderAlias = ProviderAcquisitionRequest.Require(providerAlias, nameof(providerAlias)).ToLowerInvariant();
        SanitizedRequestDigest = sanitizedRequestDigest;
        ObservedPrefixDigest = ContentDigest.Sha256(observedPrefixBytes);
        ObservedPrefixLength = observedPrefixBytes.LongLength;
        StatusCode = statusCode;
        RequestedAt = requestedAt;
        ObservedAt = observedAt;
        StopReason = ProviderAcquisitionRequest.Require(stopReason, nameof(stopReason));
    }

    public string ProviderAlias { get; }
    public ContentDigest SanitizedRequestDigest { get; }
    public ContentDigest ObservedPrefixDigest { get; }
    public long ObservedPrefixLength { get; }
    public int StatusCode { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset ObservedAt { get; }
    public string StopReason { get; }
    public bool BodyComplete => false;
    public string Retention => RetentionDisposition;

    public DigestEnvelope Envelope() => new(
        DigestScope.CanonicalJsonRecord,
        SchemaId,
        SchemaVersion,
        new CanonicalJsonObject()
            .Add("body_complete", BodyComplete)
            .Add("observed_prefix_digest", ObservedPrefixDigest.ToString())
            .Add("observed_prefix_length", ObservedPrefixLength)
            .AddTimestamp("observed_at", ObservedAt)
            .Add("provider_alias", ProviderAlias)
            .AddTimestamp("requested_at", RequestedAt)
            .Add("retention_disposition", Retention)
            .Add("sanitized_request_digest", SanitizedRequestDigest.ToString())
            .Add("status_code", StatusCode)
            .Add("stop_reason", StopReason));

    public ContentDigest Digest => Envelope().ComputeDigest();
}

public sealed class RuntimeProviderPageResult
{
    public const string SchemaId = "nexus.search.runtime-provider-page-result";
    public const string SchemaVersion = "1.0.0";

    public RuntimeProviderPageResult(
        ProviderPageRequest request,
        RuntimeProviderResponseEvidence response,
        IReadOnlyList<SearchSighting> sightings,
        IReadOnlyList<string> warnings,
        string? nextCursor,
        int? nextOffset,
        bool isComplete,
        bool isPartial,
        string? partialReason,
        ProviderAttemptEvidence attempt,
        int? observedTotalResults = null)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Response = response ?? throw new ArgumentNullException(nameof(response));
        Sightings = new ReadOnlyCollection<SearchSighting>(
            (sightings ?? throw new ArgumentNullException(nameof(sightings))).ToArray());
        Warnings = new ReadOnlyCollection<string>(
            (warnings ?? throw new ArgumentNullException(nameof(warnings)))
                .Select(value => ProviderAcquisitionRequest.Require(value, "warning"))
                .ToArray());
        NextCursor = string.IsNullOrWhiteSpace(nextCursor) ? null : nextCursor.Trim();
        if (nextOffset is < 0 ||
            observedTotalResults is < 0 ||
            (isComplete && (NextCursor is not null || nextOffset.HasValue)) ||
            (isPartial && string.IsNullOrWhiteSpace(partialReason)))
        {
            throw ProviderAcquisitionRequest.Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderPage,
                "Runtime provider completion or pagination evidence is inconsistent.");
        }

        NextOffset = nextOffset;
        IsComplete = isComplete;
        IsPartial = isPartial;
        PartialReason = string.IsNullOrWhiteSpace(partialReason) ? null : partialReason.Trim();
        ObservedTotalResults = observedTotalResults;
        Attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
        if (Attempt.PageRequestDigest != Request.Digest ||
            Attempt.AcquisitionRequestDigest != Request.AcquisitionRequestDigest ||
            Attempt.RawResponseEvidenceDigest != Response.Digest ||
            Attempt.ProviderAlias != Response.ProviderAlias ||
            Attempt.ParserId != Response.ParserId ||
            Attempt.ParserVersion != Response.ParserVersion ||
            Attempt.PageIndex != Request.PageIndex ||
            Attempt.PageSize != Request.PageSize ||
            Attempt.Offset != Request.Offset ||
            Attempt.StatusCode != Response.StatusCode ||
            Attempt.RequestedAt != Response.RequestedAt ||
            Attempt.ReceivedAt != Response.ReceivedAt ||
            (IsComplete && Attempt.CompletionState != "complete") ||
            (IsPartial && Attempt.CompletionState is not ("partial" or "failed")) ||
            (!IsComplete && !IsPartial && Attempt.CompletionState != "continuable"))
        {
            throw ProviderAcquisitionRequest.Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "Runtime provider page evidence bindings do not reproduce.");
        }
    }

    public ProviderPageRequest Request { get; }
    public RuntimeProviderResponseEvidence Response { get; }
    public IReadOnlyList<SearchSighting> Sightings { get; }
    public IReadOnlyList<string> Warnings { get; }
    public string? NextCursor { get; }
    public int? NextOffset { get; }
    public bool IsComplete { get; }
    public bool IsPartial { get; }
    public string? PartialReason { get; }
    public int? ObservedTotalResults { get; }
    public ProviderAttemptEvidence Attempt { get; }

    public DigestEnvelope Envelope()
    {
        var sightings = Sightings.Select(item =>
            (CanonicalJsonValue)new CanonicalJsonObject()
                .Add("provider_alias", item.ProviderAlias)
                .Add("provider_local_rank", item.ProviderLocalRank)
                .Add("provider_order", item.ProviderOrder)
                .Add("source_context", item.Work.SourceContext ?? string.Empty)
                .Add("title", item.Work.Title)
                .Add("work_ids", CanonicalJsonValue.Array(
                    item.WorkIds.Select(CanonicalJsonValue.From).ToArray()))).ToArray();
        return new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            SchemaId,
            SchemaVersion,
            new CanonicalJsonObject()
                .Add("attempt_digest", Attempt.Digest.ToString())
                .Add("is_complete", IsComplete)
                .Add("is_partial", IsPartial)
                .Add("next_cursor", NextCursor ?? string.Empty)
                .Add("next_offset", NextOffset ?? 0)
                .Add("observed_total_results", ObservedTotalResults ?? 0)
                .Add("partial_reason", PartialReason ?? string.Empty)
                .Add("page_request_digest", Request.Digest.ToString())
                .Add("response_evidence_digest", Response.Digest.ToString())
                .Add("sightings", CanonicalJsonValue.Array(sightings))
                .Add("warnings", CanonicalJsonValue.Array(
                    Warnings.Select(CanonicalJsonValue.From).ToArray())));
    }

    public ContentDigest Digest => Envelope().ComputeDigest();
}
