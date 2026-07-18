using System.Globalization;
using System.Text.Json;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Search.Providers.Live;
using NexusScholar.Shared;

namespace NexusScholar.Search.Providers.OpenAlex;

public sealed record OpenAlexRequestDescriptor(
    string Method,
    string EndpointPathAndQuery,
    string ProviderAlias,
    string PageRequestDigest);

public sealed class OpenAlexRecordedResponseAdapter
{
    public const string ProviderAlias = "openalex";
    public const string ParserId = "nexus.openalex.works-json";
    public const string ParserVersion = "1.0.0";

    private const int MaxPerPage = 100;
    private const string FixedSelect = "id,doi,display_name,publication_year,cited_by_count,authorships,primary_location";

    public static OpenAlexRequestDescriptor Describe(ProviderAcquisitionRequest acquisition, ProviderPageRequest page)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        ArgumentNullException.ThrowIfNull(page);

        if (!string.Equals(acquisition.ProviderAlias, ProviderAlias, StringComparison.Ordinal))
        {
            throw Rule(SearchErrorCodes.UnknownProviderAlias, $"Unknown OpenAlex adapter provider alias '{acquisition.ProviderAlias}'.");
        }

        if (page.AcquisitionRequestDigest != acquisition.Digest)
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "OpenAlex page request is not bound to its acquisition request.");
        }

        if (page.PageIndex == 0)
        {
            if (!string.IsNullOrWhiteSpace(page.Cursor) && page.Cursor != "*")
            {
                throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "First OpenAlex page cursor must be wildcard '*'.");
            }
        }
        else if (string.IsNullOrWhiteSpace(page.Cursor))
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "OpenAlex continuation page requires a cursor.");
        }

        if (page.PageSize > MaxPerPage)
        {
            throw Rule(ProviderAcquisitionErrorCodes.InvalidProviderPage, "OpenAlex per_page cannot exceed 100.");
        }

        ValidateQueryValue(acquisition.Query);

        var query = new List<string>
        {
            $"search={Uri.EscapeDataString(acquisition.Query)}",
            $"select={Uri.EscapeDataString(FixedSelect)}"
        };

        var filters = new List<string>();
        if (acquisition.YearRange?.From is int from && acquisition.YearRange?.To is int to)
        {
            filters.Add($"publication_year:{from}-{to}");
        }
        else if (acquisition.YearRange?.From is int onlyFrom)
        {
            filters.Add($"publication_year:{onlyFrom}");
        }
        else if (acquisition.YearRange?.To is int onlyTo)
        {
            filters.Add($"publication_year:{onlyTo}");
        }

        if (!string.IsNullOrWhiteSpace(acquisition.Language))
        {
            filters.Add($"language:{acquisition.Language.Trim()}");
        }

        if (filters.Count > 0)
        {
            query.Add($"filter={Uri.EscapeDataString(string.Join(',', filters))}");
        }

        query.Add($"per_page={page.PageSize}");
        query.Add($"cursor={Uri.EscapeDataString(page.PageIndex == 0 ? "*" : page.Cursor!)}");

        var descriptor = $"/works?{string.Join('&', query)}";
        ValidateSanitizedDescriptor(descriptor);
        return new OpenAlexRequestDescriptor("GET", descriptor, ProviderAlias, page.Digest.ToString());
    }

    public OpenAlexRequestDescriptor DescribeRequest(ProviderAcquisitionRequest acquisition, ProviderPageRequest page) =>
        Describe(acquisition, page);

    public static ProviderLiveRequest DescribeLiveRequest(ProviderAcquisitionRequest acquisition, ProviderPageRequest page) =>
        ProviderLiveRequest.Get("openalex.works", ProviderAlias, Describe(acquisition, page).EndpointPathAndQuery);

    internal static RuntimeProviderResponseEvidence CaptureResponse(
        ProviderAcquisitionRequest acquisition,
        ProviderPageRequest page,
        byte[] exactResponseBytes,
        int statusCode,
        string mediaType,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        IReadOnlyDictionary<string, string>? observedHeaders = null)
    {
        ArgumentNullException.ThrowIfNull(exactResponseBytes);
        var request = DescribeLiveRequest(acquisition, page);
        return RuntimeProviderResponseEvidence.Capture(
            request.ProviderAlias,
            request.Envelope().ComputeDigest(),
            ParserId,
            ParserVersion,
            exactResponseBytes,
            statusCode,
            mediaType,
            requestedAt,
            receivedAt,
            observedHeaders);
    }

    public static RuntimeProviderResponseEvidence CaptureResponse(ProviderLiveHttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        var body = response.CopyBody();
        response.VerifyReceipt(body);
        return RuntimeProviderResponseEvidence.Capture(
            response.Request.ProviderAlias,
            response.Request.Envelope().ComputeDigest(),
            ParserId,
            ParserVersion,
            body,
            response.StatusCode,
            response.MediaType,
            response.RequestedAt,
            response.ReceivedAt,
            response.ObservedHeaders);
    }

    public RuntimeProviderPageResult ParseRecordedResponse(
        ProviderAcquisitionRequest acquisition,
        ProviderPageRequest page,
        byte[] exactResponseBytes,
        RuntimeProviderResponseEvidence response,
        RuntimeProviderPageResult? previousPageResult = null)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(exactResponseBytes);
        ArgumentNullException.ThrowIfNull(response);

        if (!string.Equals(acquisition.ProviderAlias, ProviderAlias, StringComparison.Ordinal))
        {
            throw Rule(SearchErrorCodes.UnknownProviderAlias, $"Unknown OpenAlex adapter provider alias '{acquisition.ProviderAlias}'.");
        }

        if (page.AcquisitionRequestDigest != acquisition.Digest)
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "OpenAlex response page is not bound to its acquisition request.");
        }

        var expectedRequest = DescribeLiveRequest(acquisition, page);
        if (response.ProviderAlias != ProviderAlias ||
            response.SanitizedRequestDigest != expectedRequest.Digest)
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "OpenAlex response evidence is not bound to the sanitized page request.");
        }

        if (page.PageIndex == 0)
        {
            if (previousPageResult is not null)
            {
                throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "The first OpenAlex page cannot declare a previous page result.");
            }
        }
        else
        {
            if (previousPageResult is null)
            {
                throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "A later OpenAlex page requires its preceding page result.");
            }

            VerifyNextPage(previousPageResult, page);
        }

        if (!string.Equals(response.ParserId, ParserId, StringComparison.Ordinal) ||
            !string.Equals(response.ParserVersion, ParserVersion, StringComparison.Ordinal))
        {
            return FailureResult(
                acquisition,
                page,
                response,
                "unsupported-parser-version",
                "unsupported-parser-version");
        }

        response.Verify(exactResponseBytes);

        if (!CanonicalTimestamp.IsCanonicalUtc(response.ReceivedAt, rejectDefault: true))
        {
            return FailureResult(
                acquisition,
                page,
                response,
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "invalid-response-timezone");
        }

        if (response.StatusCode != 200)
        {
            return FailureResult(
                acquisition,
                page,
                response,
                $"http-{response.StatusCode}",
                $"http-{response.StatusCode}");
        }

        if (!string.Equals(response.MediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return FailureResult(
                acquisition,
                page,
                response,
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "invalid-response-media-type");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(exactResponseBytes);
        }
        catch (JsonException exception)
        {
            return FailureResult(
                acquisition,
                page,
                response,
                ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                $"invalid-json:{exception.GetType().Name}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object ||
                !meta.TryGetProperty("count", out var totalElement) || !totalElement.TryGetInt32(out var totalResults) ||
                !meta.TryGetProperty("per_page", out var perPageElement) || !perPageElement.TryGetInt32(out var perPage) ||
                !meta.TryGetProperty("next_cursor", out var nextCursorElement) ||
                !root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                    "unsupported-response-envelope");
            }

            if (totalResults < 0 ||
                perPage != page.PageSize ||
                results.GetArrayLength() > perPage ||
                totalResults < results.GetArrayLength())
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    "response-page-window-mismatch");
            }

            if (nextCursorElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                    "unsupported-next-cursor-format");
            }

            var cursor = nextCursorElement.ValueKind == JsonValueKind.String
                ? nextCursorElement.GetString()
                : null;
            var nextCursor = string.IsNullOrWhiteSpace(cursor) ? null : cursor;

            var warnings = new List<string>();
            var sightings = new List<SearchSighting>();
            var rank = 1;
            foreach (var item in results.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return FailureResult(
                        acquisition,
                        page,
                        response,
                        ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                        "non-object-result-item");
                }

                var title = OptionalString(item, "display_name")?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "Unknown Title";
                    warnings.Add($"item-{rank}-missing-title");
                }

                var openAlexId = NormalizeOpenAlexId(OptionalString(item, "id"));
                var doi = OptionalString(item, "doi");
                if (string.IsNullOrWhiteSpace(openAlexId))
                {
                    warnings.Add($"item-{rank}-missing-openalex-id");
                }

                if (string.IsNullOrWhiteSpace(doi))
                {
                    warnings.Add($"item-{rank}-missing-doi");
                }

                var resolvedIds = new List<WorkId>();
                if (!string.IsNullOrWhiteSpace(openAlexId))
                {
                    resolvedIds.Add(WorkId.From("openalex", openAlexId));
                }

                if (!string.IsNullOrWhiteSpace(doi))
                {
                    resolvedIds.Add(WorkId.From("doi", doi));
                }

                Dictionary<string, string>? rawData = null;
                if (acquisition.IncludeRawData)
                {
                    rawData = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["raw_provider_response_digest"] = response.RawResponseDigest.ToString(),
                        ["raw_provider_item_index"] = (page.Offset + rank - 1).ToString(CultureInfo.InvariantCulture)
                    };
                }

                var work = resolvedIds.Count == 0
                    ? ScholarlyWork.UnresolvedCandidate(
                        title,
                        $"openalex:unresolved:{response.RawResponseDigest}:{page.Offset + rank - 1}",
                        rawData: rawData)
                    : ScholarlyWork.Identified(
                        title,
                        WorkIdSet.From(resolvedIds.ToArray()),
                        sourceContext: openAlexId is null ? null : $"openalex:{openAlexId}",
                        rawData: rawData);

                sightings.Add(new SearchSighting(ProviderAlias, 1, rank++, work));
            }

            var observedEnd = checked(page.Offset + sightings.Count);
            var acquisitionEnd = checked(page.AcquisitionOffset + page.AcquisitionMaxResults);
            if (previousPageResult?.ObservedTotalResults is int previousTotal &&
                previousTotal != totalResults)
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    "total-results-chain-drift");
            }

            var hasMoreCursor = !string.IsNullOrWhiteSpace(nextCursor);
            var isComplete = !hasMoreCursor || observedEnd >= totalResults || observedEnd >= acquisitionEnd;
            if (isComplete && totalResults < observedEnd)
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    "response-page-window-mismatch");
            }

            var isPartial = !isComplete && sightings.Count < page.PageSize;
            var partialReason = isPartial ? "short-page-before-total-results" : null;
            int? nextOffset = isComplete || isPartial ? null : observedEnd;
            var nextContinuationCursor = isComplete || isPartial ? null : nextCursor;

            return new RuntimeProviderPageResult(
                page,
                response,
                sightings,
                warnings,
                nextContinuationCursor,
                nextOffset,
                isComplete,
                isPartial,
                partialReason,
                new ProviderAttemptEvidence(
                    1,
                    ProviderAlias,
                    ParserId,
                    ParserVersion,
                    acquisition.Digest,
                    page.Digest,
                    response.Digest,
                    page.PageIndex,
                    page.PageSize,
                    page.Offset,
                    response.StatusCode,
                    response.RequestedAt,
                    response.ReceivedAt,
                    isComplete ? "success" : isPartial ? "partial" : "continuable",
                    isComplete ? "complete" : isPartial ? "partial" : "continuable",
                    response.ObservedHeaders.TryGetValue("retry-after", out var retryAfter) ? retryAfter : null,
                    response.ObservedHeaders.TryGetValue("x-rate-limit-limit", out var rateLimitLimit) ? rateLimitLimit : null,
                    response.ObservedHeaders.TryGetValue("x-rate-limit-interval", out var rateLimitInterval) ? rateLimitInterval : null,
                    partialReason),
                totalResults);
        }
    }

    public static void VerifyNextPage(RuntimeProviderPageResult current, ProviderPageRequest next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);

        if (string.IsNullOrWhiteSpace(next.Cursor) ||
            !string.Equals(next.Cursor, current.NextCursor, StringComparison.Ordinal))
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "OpenAlex next cursor is not chained to the previous page.");
        }

        if (next.AcquisitionRequestDigest != current.Request.AcquisitionRequestDigest ||
            next.PageIndex != current.Request.PageIndex + 1 ||
            next.Offset != current.Request.Offset + current.Sightings.Count ||
            !next.PreviousPageResultDigest.HasValue ||
            next.PreviousPageResultDigest.Value != current.Digest)
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "OpenAlex next page does not continue the accepted page chain.");
        }

        if (current.NextCursor is null || current.IsComplete || current.IsPartial || !current.NextOffset.HasValue)
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "OpenAlex next page does not continue the accepted page chain.");
        }
    }

    private static RuntimeProviderPageResult FailureResult(
        ProviderAcquisitionRequest acquisition,
        ProviderPageRequest page,
        RuntimeProviderResponseEvidence response,
        string category,
        string? stopReason)
    {
        return new RuntimeProviderPageResult(
            page,
            response,
            Array.Empty<SearchSighting>(),
            new[] { category },
            null,
            null,
            isComplete: false,
            isPartial: true,
            partialReason: category,
            new ProviderAttemptEvidence(
                1,
                ProviderAlias,
                response.ParserId,
                response.ParserVersion,
                acquisition.Digest,
                page.Digest,
                response.Digest,
                page.PageIndex,
                page.PageSize,
                page.Offset,
                response.StatusCode,
                response.RequestedAt,
                response.ReceivedAt,
                category,
                "failed",
                response.ObservedHeaders.TryGetValue("retry-after", out var retryAfter) ? retryAfter : null,
                response.ObservedHeaders.TryGetValue("x-rate-limit-limit", out var rateLimitLimit) ? rateLimitLimit : null,
                response.ObservedHeaders.TryGetValue("x-rate-limit-interval", out var rateLimitInterval) ? rateLimitInterval : null,
                stopReason ?? category));
    }

    public static void ValidateSanitizedDescriptor(string descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor) || descriptor.Contains(":/", StringComparison.OrdinalIgnoreCase))
        {
            throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "OpenAlex request descriptor contains a URL, contact, credential, or secret-shaped field.");
        }

        var queryIndex = descriptor.IndexOf('?');
        if (queryIndex < 0)
        {
            return;
        }

        foreach (var pair in descriptor[(queryIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            if (ProviderSecretPolicy.ContainsForbiddenDescriptorValue(name, value))
            {
                throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "OpenAlex request descriptor contains a contact, credential, or secret-shaped parameter.");
            }
        }
    }

    private static string? NormalizeOpenAlexId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        const string Prefix = "https://openalex.org/";
        if (normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[Prefix.Length..];
        }

        return normalized.ToLowerInvariant();
    }

    private static string? OptionalString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void ValidateQueryValue(string query)
    {
        if (ProviderSecretPolicy.ContainsForbiddenQueryValue(query))
        {
            throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "Provider query contains URL, contact, credential, or secret-shaped material.");
        }
    }

    private static SearchRuleException Rule(string category, string message) =>
        new SearchRuleException(category, message);
}
