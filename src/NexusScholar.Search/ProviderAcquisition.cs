using System.Collections.ObjectModel;
using NexusScholar.Kernel;

namespace NexusScholar.Search;

public static class ProviderAcquisitionErrorCodes
{
    public const string InvalidProviderEvidence = "invalid-provider-evidence";
    public const string InvalidProviderPage = "invalid-provider-page";
    public const string FixtureDigestMismatch = "fixture-digest-mismatch";
    public const string SecretBearingDescriptor = "secret-bearing-descriptor";
    public const string ProviderSchemaDrift = "provider-schema-drift";
    public const string PaginationChainMismatch = "pagination-chain-mismatch";
    public const string CredentialUnavailable = "provider-credential-unavailable";
    public const string TransportPolicyViolation = "provider-transport-policy-violation";
    public const string ResponseTooLarge = "provider-response-too-large";
    public const string ProviderTimeout = "provider-timeout";
}

public sealed class ProviderAcquisitionRequest
{
    public const string SchemaId = "nexus.search.provider-acquisition-request";
    public const string SchemaVersion = "1.0.0";

    private ProviderAcquisitionRequest(
        string requestId,
        string providerAlias,
        string query,
        SearchYearRange? yearRange,
        string? language,
        int maxResults,
        int offset,
        bool includeRawData,
        DateTimeOffset requestedAt)
    {
        RequestId = Require(requestId, nameof(requestId));
        ProviderAlias = Require(providerAlias, nameof(providerAlias)).ToLowerInvariant();
        Query = SearchQueryTerm.From(query).Value;
        YearRange = yearRange is null
            ? null
            : SearchYearRange.Validate(yearRange.From, yearRange.To, requestedAt.Year);
        Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
        if (maxResults <= 0)
        {
            throw Rule(ProviderAcquisitionErrorCodes.InvalidProviderPage, "Provider max results must be positive.");
        }

        if (offset < 0)
        {
            throw Rule(ProviderAcquisitionErrorCodes.InvalidProviderPage, "Provider offset cannot be negative.");
        }

        RequireUtc(requestedAt, nameof(requestedAt));
        MaxResults = maxResults;
        Offset = offset;
        IncludeRawData = includeRawData;
        RequestedAt = requestedAt;
    }

    public string RequestId { get; }
    public string ProviderAlias { get; }
    public string Query { get; }
    public SearchYearRange? YearRange { get; }
    public string? Language { get; }
    public int MaxResults { get; }
    public int Offset { get; }
    public bool IncludeRawData { get; }
    public DateTimeOffset RequestedAt { get; }

    public static ProviderAcquisitionRequest Create(
        string requestId,
        string providerAlias,
        string query,
        SearchYearRange? yearRange,
        string? language,
        int maxResults,
        int offset,
        bool includeRawData,
        DateTimeOffset requestedAt) =>
        new(requestId, providerAlias, query, yearRange, language, maxResults, offset, includeRawData, requestedAt);

    public DigestEnvelope Envelope()
    {
        var content = new CanonicalJsonObject()
            .Add("include_raw_data", IncludeRawData)
            .Add("language", Language ?? string.Empty)
            .Add("max_results", MaxResults)
            .Add("offset", Offset)
            .Add("provider_alias", ProviderAlias)
            .Add("query", Query)
            .Add("request_id", RequestId)
            .AddTimestamp("requested_at", RequestedAt)
            .Add("year_from", YearRange?.From ?? 0)
            .Add("year_to", YearRange?.To ?? 0);
        return new DigestEnvelope(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion, content);
    }

    public ContentDigest Digest => Envelope().ComputeDigest();

    internal static SearchRuleException Rule(string category, string message) => new(category, message);

    internal static string Require(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, $"{name} must not be blank.")
            : value.Trim();

    internal static void RequireUtc(DateTimeOffset value, string name)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, $"{name} must be UTC.");
        }
    }
}

public sealed class ProviderPageRequest
{
    public const string SchemaId = "nexus.search.provider-page-request";
    public const string SchemaVersion = "1.0.0";

    private ProviderPageRequest(
        ContentDigest acquisitionRequestDigest,
        int acquisitionOffset,
        int acquisitionMaxResults,
        int pageIndex,
        int pageSize,
        int offset,
        string? cursor,
        ContentDigest? previousPageResultDigest)
    {
        if (!acquisitionRequestDigest.IsValid)
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Acquisition request digest is required.");
        }

        if (pageIndex < 0 || pageSize <= 0 || offset < acquisitionOffset ||
            acquisitionMaxResults <= 0 || pageSize > acquisitionMaxResults ||
            offset - acquisitionOffset >= acquisitionMaxResults ||
            pageSize > acquisitionMaxResults - (offset - acquisitionOffset) ||
            (pageIndex == 0 && (offset != acquisitionOffset || previousPageResultDigest.HasValue)) ||
            (pageIndex > 0 && (!previousPageResultDigest.HasValue || !previousPageResultDigest.Value.IsValid)))
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderPage, "Provider page index, size, or offset is invalid.");
        }

        AcquisitionRequestDigest = acquisitionRequestDigest;
        AcquisitionOffset = acquisitionOffset;
        AcquisitionMaxResults = acquisitionMaxResults;
        PageIndex = pageIndex;
        PageSize = pageSize;
        Offset = offset;
        Cursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor.Trim();
        PreviousPageResultDigest = previousPageResultDigest;
    }

    public ContentDigest AcquisitionRequestDigest { get; }
    public int AcquisitionOffset { get; }
    public int AcquisitionMaxResults { get; }
    public int PageIndex { get; }
    public int PageSize { get; }
    public int Offset { get; }
    public string? Cursor { get; }
    public ContentDigest? PreviousPageResultDigest { get; }

    public static ProviderPageRequest Create(
        ProviderAcquisitionRequest acquisition,
        int pageIndex,
        int pageSize,
        int offset,
        string? cursor = null,
        ContentDigest? previousPageResultDigest = null)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        return new ProviderPageRequest(
            acquisition.Digest,
            acquisition.Offset,
            acquisition.MaxResults,
            pageIndex,
            pageSize,
            offset,
            cursor,
            previousPageResultDigest);
    }

    public DigestEnvelope Envelope() => new(
        DigestScope.CanonicalJsonRecord,
        SchemaId,
        SchemaVersion,
        new CanonicalJsonObject()
            .Add("acquisition_request_digest", AcquisitionRequestDigest.ToString())
            .Add("acquisition_max_results", AcquisitionMaxResults)
            .Add("acquisition_offset", AcquisitionOffset)
            .Add("cursor", Cursor ?? string.Empty)
            .Add("offset", Offset)
            .Add("page_index", PageIndex)
            .Add("page_size", PageSize)
            .Add("previous_page_result_digest", PreviousPageResultDigest?.ToString() ?? string.Empty));

    public ContentDigest Digest => Envelope().ComputeDigest();
}

public sealed class RecordedProviderFixtureEvidence
{
    public const string SchemaId = "nexus.search.recorded-provider-fixture";
    public const string SchemaVersion = "1.0.0";
    public const string RetentionDisposition = "retained-local-fixture";

    private RecordedProviderFixtureEvidence(
        string fixtureId,
        string fixtureRelativePath,
        string acceptedBy,
        string acceptingActorKind,
        DateTimeOffset acceptedAt,
        string sourceNote,
        string parserId,
        string parserVersion,
        ContentDigest rawResponseDigest,
        long byteLength)
    {
        FixtureId = ProviderAcquisitionRequest.Require(fixtureId, nameof(fixtureId));
        FixtureRelativePath = NormalizeFixturePath(fixtureRelativePath);
        AcceptedBy = ProviderAcquisitionRequest.Require(acceptedBy, nameof(acceptedBy));
        AcceptingActorKind = ProviderAcquisitionRequest.Require(acceptingActorKind, nameof(acceptingActorKind));
        if (AcceptingActorKind is not ("human" or "fixture-generator"))
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Fixture accepting actor kind is invalid.");
        }

        ProviderAcquisitionRequest.RequireUtc(acceptedAt, nameof(acceptedAt));
        SourceNote = ProviderAcquisitionRequest.Require(sourceNote, nameof(sourceNote));
        ParserId = ProviderAcquisitionRequest.Require(parserId, nameof(parserId));
        ParserVersion = ProviderAcquisitionRequest.Require(parserVersion, nameof(parserVersion));
        if (!rawResponseDigest.IsValid || byteLength < 0)
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Fixture digest or byte length is invalid.");
        }

        AcceptedAt = acceptedAt;
        RawResponseDigest = rawResponseDigest;
        ByteLength = byteLength;
    }

    public string FixtureId { get; }
    public string FixtureRelativePath { get; }
    public string AcceptedBy { get; }
    public string AcceptingActorKind { get; }
    public DateTimeOffset AcceptedAt { get; }
    public string SourceNote { get; }
    public string ParserId { get; }
    public string ParserVersion { get; }
    public ContentDigest RawResponseDigest { get; }
    public long ByteLength { get; }

    public static RecordedProviderFixtureEvidence AcceptRetainedLocal(
        string fixtureId,
        string fixtureRelativePath,
        string acceptedBy,
        string acceptingActorKind,
        DateTimeOffset acceptedAt,
        string sourceNote,
        string parserId,
        string parserVersion,
        byte[] exactBytes)
    {
        ArgumentNullException.ThrowIfNull(exactBytes);
        return new RecordedProviderFixtureEvidence(
            fixtureId,
            fixtureRelativePath,
            acceptedBy,
            acceptingActorKind,
            acceptedAt,
            sourceNote,
            parserId,
            parserVersion,
            ContentDigest.Sha256(exactBytes),
            exactBytes.LongLength);
    }

    public void Verify(byte[] exactBytes)
    {
        ArgumentNullException.ThrowIfNull(exactBytes);
        if (exactBytes.LongLength != ByteLength || ContentDigest.Sha256(exactBytes) != RawResponseDigest)
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.FixtureDigestMismatch, "Recorded provider fixture bytes do not match their accepted evidence.");
        }
    }

    public DigestEnvelope Envelope() => new(
        DigestScope.CanonicalJsonRecord,
        SchemaId,
        SchemaVersion,
        new CanonicalJsonObject()
            .Add("accepted_by", AcceptedBy)
            .Add("accepting_actor_kind", AcceptingActorKind)
            .AddTimestamp("accepted_at", AcceptedAt)
            .Add("byte_length", ByteLength)
            .Add("fixture_id", FixtureId)
            .Add("fixture_relative_path", FixtureRelativePath)
            .Add("parser_id", ParserId)
            .Add("parser_version", ParserVersion)
            .Add("raw_response_digest", RawResponseDigest.ToString())
            .Add("raw_response_scope", DigestScope.RawArtifactBytes.Value)
            .Add("retention_disposition", RetentionDisposition)
            .Add("source_note", SourceNote));

    public ContentDigest Digest => Envelope().ComputeDigest();

    internal static string NormalizeFixturePath(string value)
    {
        var normalized = ProviderAcquisitionRequest.Require(value, nameof(value)).Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) ||
            normalized.Contains(':', StringComparison.Ordinal) ||
            normalized.Split('/').Any(segment => segment is "." or ".."))
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Fixture path must be a repository-relative path without traversal.");
        }

        return normalized;
    }
}

public sealed class ProviderRawResponseEvidence
{
    public const string SchemaId = "nexus.search.provider-raw-response";
    public const string SchemaVersion = "1.0.0";

    public ProviderRawResponseEvidence(
        string providerAlias,
        string fixtureId,
        string fixtureRelativePath,
        ContentDigest fixtureEvidenceDigest,
        ContentDigest rawResponseDigest,
        long byteLength,
        int statusCode,
        string mediaType,
        DateTimeOffset receivedAt,
        string retentionDisposition)
    {
        if (!fixtureEvidenceDigest.IsValid || !rawResponseDigest.IsValid || byteLength < 0 ||
            statusCode is < 100 or > 599)
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Raw provider response identity is invalid.");
        }

        ProviderAcquisitionRequest.RequireUtc(receivedAt, nameof(receivedAt));
        ProviderAlias = ProviderAcquisitionRequest.Require(providerAlias, nameof(providerAlias)).ToLowerInvariant();
        FixtureId = ProviderAcquisitionRequest.Require(fixtureId, nameof(fixtureId));
        FixtureRelativePath = RecordedProviderFixtureEvidence.NormalizeFixturePath(fixtureRelativePath);
        FixtureEvidenceDigest = fixtureEvidenceDigest;
        RawResponseDigest = rawResponseDigest;
        ByteLength = byteLength;
        StatusCode = statusCode;
        MediaType = ProviderAcquisitionRequest.Require(mediaType, nameof(mediaType)).ToLowerInvariant();
        ReceivedAt = receivedAt;
        RetentionDisposition = ProviderAcquisitionRequest.Require(retentionDisposition, nameof(retentionDisposition));
        if (!string.Equals(RetentionDisposition, RecordedProviderFixtureEvidence.RetentionDisposition, StringComparison.Ordinal))
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "FE-09A accepts retained local fixture responses only.");
        }
    }

    public string ProviderAlias { get; }
    public string FixtureId { get; }
    public string FixtureRelativePath { get; }
    public ContentDigest FixtureEvidenceDigest { get; }
    public ContentDigest RawResponseDigest { get; }
    public long ByteLength { get; }
    public int StatusCode { get; }
    public string MediaType { get; }
    public DateTimeOffset ReceivedAt { get; }
    public string RetentionDisposition { get; }

    public DigestEnvelope Envelope() => new(
        DigestScope.CanonicalJsonRecord,
        SchemaId,
        SchemaVersion,
        new CanonicalJsonObject()
            .Add("byte_length", ByteLength)
            .Add("fixture_evidence_digest", FixtureEvidenceDigest.ToString())
            .Add("fixture_id", FixtureId)
            .Add("fixture_relative_path", FixtureRelativePath)
            .Add("media_type", MediaType)
            .Add("provider_alias", ProviderAlias)
            .Add("raw_response_digest", RawResponseDigest.ToString())
            .Add("raw_response_scope", DigestScope.RawArtifactBytes.Value)
            .AddTimestamp("received_at", ReceivedAt)
            .Add("retention_disposition", RetentionDisposition)
            .Add("status_code", StatusCode));

    public ContentDigest Digest => Envelope().ComputeDigest();
}

public sealed class ProviderAttemptEvidence
{
    public const string SchemaId = "nexus.search.provider-attempt-evidence";
    public const string SchemaVersion = "1.0.0";

    public ProviderAttemptEvidence(
        int attemptOrdinal,
        string providerAlias,
        string parserId,
        string parserVersion,
        ContentDigest acquisitionRequestDigest,
        ContentDigest pageRequestDigest,
        ContentDigest rawResponseEvidenceDigest,
        int pageIndex,
        int pageSize,
        int offset,
        int statusCode,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        string outcomeCategory,
        string completionState,
        string? retryAfter,
        string? rateLimitLimit,
        string? rateLimitInterval,
        string? stopReason)
    {
        if (attemptOrdinal <= 0 || !acquisitionRequestDigest.IsValid ||
            !pageRequestDigest.IsValid || !rawResponseEvidenceDigest.IsValid ||
            pageIndex < 0 || pageSize <= 0 || offset < 0 || statusCode is < 100 or > 599)
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Provider attempt identity is invalid.");
        }

        AttemptOrdinal = attemptOrdinal;
        ProviderAlias = ProviderAcquisitionRequest.Require(providerAlias, nameof(providerAlias)).ToLowerInvariant();
        ParserId = ProviderAcquisitionRequest.Require(parserId, nameof(parserId));
        ParserVersion = ProviderAcquisitionRequest.Require(parserVersion, nameof(parserVersion));
        AcquisitionRequestDigest = acquisitionRequestDigest;
        PageRequestDigest = pageRequestDigest;
        RawResponseEvidenceDigest = rawResponseEvidenceDigest;
        PageIndex = pageIndex;
        PageSize = pageSize;
        Offset = offset;
        StatusCode = statusCode;
        ProviderAcquisitionRequest.RequireUtc(requestedAt, nameof(requestedAt));
        ProviderAcquisitionRequest.RequireUtc(receivedAt, nameof(receivedAt));
        if (receivedAt < requestedAt)
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Provider response cannot predate its request.");
        }

        RequestedAt = requestedAt;
        ReceivedAt = receivedAt;
        OutcomeCategory = ProviderAcquisitionRequest.Require(outcomeCategory, nameof(outcomeCategory));
        CompletionState = ProviderAcquisitionRequest.Require(completionState, nameof(completionState));
        RetryAfter = Optional(retryAfter);
        RateLimitLimit = Optional(rateLimitLimit);
        RateLimitInterval = Optional(rateLimitInterval);
        StopReason = Optional(stopReason);
    }

    public int AttemptOrdinal { get; }
    public string ProviderAlias { get; }
    public string ParserId { get; }
    public string ParserVersion { get; }
    public ContentDigest AcquisitionRequestDigest { get; }
    public ContentDigest PageRequestDigest { get; }
    public ContentDigest RawResponseEvidenceDigest { get; }
    public int PageIndex { get; }
    public int PageSize { get; }
    public int Offset { get; }
    public int StatusCode { get; }
    public DateTimeOffset RequestedAt { get; }
    public DateTimeOffset ReceivedAt { get; }
    public string OutcomeCategory { get; }
    public string CompletionState { get; }
    public string? RetryAfter { get; }
    public string? RateLimitLimit { get; }
    public string? RateLimitInterval { get; }
    public string? StopReason { get; }

    public DigestEnvelope Envelope() => new(
        DigestScope.CanonicalJsonRecord,
        SchemaId,
        SchemaVersion,
        new CanonicalJsonObject()
            .Add("acquisition_request_digest", AcquisitionRequestDigest.ToString())
            .Add("attempt_ordinal", AttemptOrdinal)
            .Add("completion_state", CompletionState)
            .Add("offset", Offset)
            .Add("outcome_category", OutcomeCategory)
            .Add("page_index", PageIndex)
            .Add("page_request_digest", PageRequestDigest.ToString())
            .Add("page_size", PageSize)
            .Add("parser_id", ParserId)
            .Add("parser_version", ParserVersion)
            .Add("provider_alias", ProviderAlias)
            .Add("rate_limit_interval", RateLimitInterval ?? string.Empty)
            .Add("rate_limit_limit", RateLimitLimit ?? string.Empty)
            .Add("raw_response_evidence_digest", RawResponseEvidenceDigest.ToString())
            .AddTimestamp("received_at", ReceivedAt)
            .AddTimestamp("requested_at", RequestedAt)
            .Add("retry_after", RetryAfter ?? string.Empty)
            .Add("status_code", StatusCode)
            .Add("stop_reason", StopReason ?? string.Empty));

    public ContentDigest Digest => Envelope().ComputeDigest();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class ProviderPageResult
{
    public const string SchemaId = "nexus.search.provider-page-result";
    public const string SchemaVersion = "1.0.0";

    public ProviderPageResult(
        ProviderPageRequest request,
        RecordedProviderFixtureEvidence fixture,
        ProviderRawResponseEvidence rawResponse,
        string parserId,
        string parserVersion,
        IReadOnlyList<SearchSighting> sightings,
        IReadOnlyList<string> warnings,
        int? nextOffset,
        bool isComplete,
        bool isPartial,
        string? partialReason,
        ProviderAttemptEvidence attempt)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        RawResponse = rawResponse ?? throw new ArgumentNullException(nameof(rawResponse));
        ParserId = ProviderAcquisitionRequest.Require(parserId, nameof(parserId));
        ParserVersion = ProviderAcquisitionRequest.Require(parserVersion, nameof(parserVersion));
        Sightings = new ReadOnlyCollection<SearchSighting>((sightings ?? throw new ArgumentNullException(nameof(sightings))).ToArray());
        Warnings = new ReadOnlyCollection<string>((warnings ?? throw new ArgumentNullException(nameof(warnings))).Select(value => ProviderAcquisitionRequest.Require(value, "warning")).ToArray());
        if (nextOffset is < 0 || (isComplete && nextOffset.HasValue) || (isPartial && string.IsNullOrWhiteSpace(partialReason)))
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderPage, "Provider page completion or pagination evidence is inconsistent.");
        }

        NextOffset = nextOffset;
        IsComplete = isComplete;
        IsPartial = isPartial;
        PartialReason = string.IsNullOrWhiteSpace(partialReason) ? null : partialReason.Trim();
        Attempt = attempt ?? throw new ArgumentNullException(nameof(attempt));
        if (RawResponse.FixtureEvidenceDigest != Fixture.Digest ||
            RawResponse.FixtureId != Fixture.FixtureId ||
            RawResponse.FixtureRelativePath != Fixture.FixtureRelativePath ||
            RawResponse.RawResponseDigest != Fixture.RawResponseDigest ||
            RawResponse.ByteLength != Fixture.ByteLength ||
            Attempt.PageRequestDigest != Request.Digest ||
            Attempt.AcquisitionRequestDigest != Request.AcquisitionRequestDigest ||
            Attempt.RawResponseEvidenceDigest != RawResponse.Digest ||
            Attempt.ProviderAlias != RawResponse.ProviderAlias ||
            Attempt.ParserId != ParserId ||
            Attempt.ParserVersion != ParserVersion ||
            Attempt.PageIndex != Request.PageIndex ||
            Attempt.PageSize != Request.PageSize ||
            Attempt.Offset != Request.Offset ||
            Attempt.StatusCode != RawResponse.StatusCode ||
            (IsComplete && Attempt.CompletionState != "complete") ||
            (IsPartial && Attempt.CompletionState is not ("partial" or "failed")) ||
            (!IsComplete && !IsPartial && Attempt.CompletionState != "continuable"))
        {
            throw ProviderAcquisitionRequest.Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Provider page evidence bindings do not reproduce.");
        }
    }

    public ProviderPageRequest Request { get; }
    public RecordedProviderFixtureEvidence Fixture { get; }
    public ProviderRawResponseEvidence RawResponse { get; }
    public string ParserId { get; }
    public string ParserVersion { get; }
    public IReadOnlyList<SearchSighting> Sightings { get; }
    public IReadOnlyList<string> Warnings { get; }
    public int? NextOffset { get; }
    public bool IsComplete { get; }
    public bool IsPartial { get; }
    public string? PartialReason { get; }
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
                .Add(
                    "work_ids",
                    CanonicalJsonValue.Array(item.WorkIds.Select(CanonicalJsonValue.From).ToArray()))).ToArray();
        return new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            SchemaId,
            SchemaVersion,
            new CanonicalJsonObject()
                .Add("attempt_digest", Attempt.Digest.ToString())
                .Add("fixture_evidence_digest", Fixture.Digest.ToString())
                .Add("is_complete", IsComplete)
                .Add("is_partial", IsPartial)
                .Add("next_offset", NextOffset ?? 0)
                .Add("parser_id", ParserId)
                .Add("parser_version", ParserVersion)
                .Add("partial_reason", PartialReason ?? string.Empty)
                .Add("page_request_digest", Request.Digest.ToString())
                .Add("raw_response_evidence_digest", RawResponse.Digest.ToString())
                .Add("sightings", CanonicalJsonValue.Array(sightings))
                .Add(
                    "warnings",
                    CanonicalJsonValue.Array(Warnings.Select(CanonicalJsonValue.From).ToArray())));
    }

    public ContentDigest Digest => Envelope().ComputeDigest();
}
