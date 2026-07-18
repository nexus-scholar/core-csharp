using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Search.Providers.Live;
using NexusScholar.Shared;

namespace NexusScholar.Search.Providers.SemanticScholar;

public sealed record SemanticScholarRequestDescriptor(
    string Method,
    string EndpointPathAndQuery,
    string ProviderAlias,
    string PageRequestDigest);

public sealed class SemanticScholarRecordedResponseAdapter
{
    public const string ProviderAlias = "semantic_scholar";
    public const string ParserId = "nexus.semantic-scholar.works-json";
    public const string ParserVersion = "1.0.0";

    private const int MaxPageSize = 1000;
    private const string FixedSearchFields = "paperId,externalIds,title,abstract,year,venue,authors,citationCount";

    public static SemanticScholarRequestDescriptor Describe(ProviderAcquisitionRequest acquisition, ProviderPageRequest page)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        ArgumentNullException.ThrowIfNull(page);
        if (!string.Equals(acquisition.ProviderAlias, ProviderAlias, StringComparison.Ordinal))
        {
            throw Rule(SearchErrorCodes.UnknownProviderAlias, $"Unknown Semantic Scholar adapter provider alias '{acquisition.ProviderAlias}'.");
        }

        if (page.AcquisitionRequestDigest != acquisition.Digest)
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                "Semantic Scholar response page is not bound to its acquisition request.");
        }

        if (page.PageSize > MaxPageSize)
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderPage,
                "Semantic Scholar per-page limit cannot exceed 1000.");
        }

        if (page.PageIndex == 0)
        {
            if (!string.IsNullOrWhiteSpace(page.Cursor) && page.Cursor is not "*")
            {
                throw Rule(
                    ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    "First Semantic Scholar page cursor must be omitted.");
            }
        }
        else if (string.IsNullOrWhiteSpace(page.Cursor))
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                "Semantic Scholar continuation page requires a cursor.");
        }

        ValidateQueryValue(acquisition.Query);
        var translated = TranslateQuery(acquisition.Query);
        var query = new List<string>
        {
            $"query={Uri.EscapeDataString(translated)}",
            $"fields={Uri.EscapeDataString(FixedSearchFields)}",
            "sort=paperId"
        };

        if (acquisition.YearRange is { From: int from, To: int to })
        {
            query.Add($"year={Uri.EscapeDataString(from == to ? from.ToString(CultureInfo.InvariantCulture) : $"{from}-{to}")}");
        }
        else if (acquisition.YearRange?.From is int onlyFrom)
        {
            query.Add($"year={Uri.EscapeDataString(onlyFrom.ToString(CultureInfo.InvariantCulture))}");
        }
        else if (acquisition.YearRange?.To is int onlyTo)
        {
            query.Add($"year={Uri.EscapeDataString(onlyTo.ToString(CultureInfo.InvariantCulture))}");
        }

        if (page.PageIndex > 0 && !string.IsNullOrWhiteSpace(page.Cursor) && page.Cursor is not "*")
        {
            query.Add($"token={Uri.EscapeDataString(page.Cursor)}");
        }

        var descriptor = new SemanticScholarRequestDescriptor(
            "GET",
            $"/graph/v1/paper/search/bulk?{string.Join("&", query)}",
            ProviderAlias,
            page.Digest.ToString());
        ValidateSanitizedDescriptor(
            descriptor.EndpointPathAndQuery,
            allowPaginationToken: page.PageIndex > 0 && !string.IsNullOrWhiteSpace(page.Cursor));
        return descriptor;
    }

    public static ProviderLiveRequest DescribeLiveRequest(ProviderAcquisitionRequest acquisition, ProviderPageRequest page) =>
        ProviderLiveRequest.Get("semantic-scholar.bulk-search", ProviderAlias, Describe(acquisition, page).EndpointPathAndQuery);

    public static ProviderLiveRequest DescribePaperBatchRequest(IReadOnlyList<string> paperIds)
    {
        var uniqueIds = ValidateBatchIdentifiers(paperIds);
        if (uniqueIds.Count is < 1 or > 500)
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderPage,
                "Semantic Scholar batch request requires between 1 and 500 unique paper ids.");
        }

        var body = BuildBatchRequestBody(uniqueIds);
        var encodedFields = Uri.EscapeDataString(FixedSearchFields);
        return ProviderLiveRequest.Post(
            "semantic-scholar.paper-batch",
            ProviderAlias,
            $"/graph/v1/paper/batch?fields={encodedFields}",
            body);
    }

    internal static RuntimeProviderResponseEvidence CaptureResponse(
        ProviderAcquisitionRequest acquisition,
        ProviderPageRequest page,
        byte[] exactResponseBytes,
        int statusCode,
        string mediaType,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        IReadOnlyDictionary<string, string>? observedHeaders = null,
        string? parserId = null,
        string? parserVersion = null) =>
        RuntimeProviderResponseEvidence.Capture(
            ProviderAlias,
            DescribeLiveRequest(acquisition, page).Envelope().ComputeDigest(),
            parserId ?? ParserId,
            parserVersion ?? ParserVersion,
            exactResponseBytes,
            statusCode,
            mediaType,
            requestedAt,
            receivedAt,
            observedHeaders);

    public static RuntimeProviderResponseEvidence CaptureResponse(
        ProviderLiveHttpResponse response,
        string? parserId = null,
        string? parserVersion = null)
    {
        var body = response.CopyBody();
        response.VerifyReceipt(body);
        return RuntimeProviderResponseEvidence.Capture(
            response.Request.ProviderAlias,
            response.Request.Envelope().ComputeDigest(),
            parserId ?? ParserId,
            parserVersion ?? ParserVersion,
            body,
            response.StatusCode,
            response.MediaType,
            response.RequestedAt,
            response.ReceivedAt,
            response.ObservedHeaders);
    }

    internal static RuntimeProviderResponseEvidence CaptureResponse(
        ProviderLiveRequest request,
        byte[] exactResponseBytes,
        int statusCode,
        string mediaType,
        DateTimeOffset requestedAt,
        DateTimeOffset receivedAt,
        IReadOnlyDictionary<string, string>? observedHeaders = null,
        string? parserId = null,
        string? parserVersion = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RuntimeProviderResponseEvidence.Capture(
            request.ProviderAlias,
            request.Envelope().ComputeDigest(),
            parserId ?? ParserId,
            parserVersion ?? ParserVersion,
            exactResponseBytes,
            statusCode,
            mediaType,
            requestedAt,
            receivedAt,
            observedHeaders);
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
            throw Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                $"Unknown Semantic Scholar adapter provider alias '{acquisition.ProviderAlias}'.");
        }

        if (page.AcquisitionRequestDigest != acquisition.Digest)
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                "Semantic Scholar response page is not bound to its acquisition request.");
        }

        var expectedRequest = DescribeLiveRequest(acquisition, page);
        if (response.ProviderAlias != ProviderAlias ||
            response.SanitizedRequestDigest != expectedRequest.Digest)
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "Semantic Scholar response evidence is not bound to the sanitized bulk request.");
        }

        if (page.PageIndex == 0)
        {
            if (previousPageResult is not null)
            {
                throw Rule(
                    ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    "The first Semantic Scholar page cannot declare a previous page result.");
            }
        }
        else
        {
            if (previousPageResult is null)
            {
                throw Rule(
                    ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    "A later Semantic Scholar page requires its preceding page result.");
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
        if (!string.Equals(response.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            !CanonicalTimestamp.IsCanonicalUtc(response.ReceivedAt, rejectDefault: true))
        {
            return FailureResult(
                acquisition,
                page,
                response,
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "invalid-response-envelope");
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
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                    "unsupported-response-envelope");
            }

            var sightings = new List<SearchSighting>();
            var warnings = new List<string>();
            var rank = 1;
            string? previousPaperId = null;
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    return FailureResult(
                        acquisition,
                        page,
                        response,
                        ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                        "non-object-bulk-item");
                }

                var parseWarnings = new List<string>();
                var sighting = BuildSightingFromPayload(
                    acquisition,
                    item,
                    page.Offset,
                    rank,
                    parseWarnings,
                    response.RawResponseDigest.ToString(),
                    includeIndexFromOutput: true);
                warnings.AddRange(parseWarnings);
                var paperId = OptionalString(item, "paperId")?.Trim();
                if (!string.IsNullOrWhiteSpace(previousPaperId) &&
                    !string.IsNullOrWhiteSpace(paperId) &&
                    string.CompareOrdinal(previousPaperId, paperId) > 0)
                {
                    return FailureResult(
                        acquisition,
                        page,
                        response,
                        ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                        "bulk-paper-id-order-drift");
                }

                previousPaperId = paperId ?? previousPaperId;
                sightings.Add(sighting);
                rank++;
            }

            if (data.GetArrayLength() > page.PageSize)
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    "response-page-window-mismatch");
            }

            var observedEnd = checked(page.Offset + sightings.Count);
            var acquisitionEnd = checked(page.AcquisitionOffset + page.AcquisitionMaxResults);
            var nextCursor = OptionalString(root, "token");
            var hasMore = !string.IsNullOrWhiteSpace(nextCursor);
            var isComplete = !hasMore || observedEnd >= acquisitionEnd;
            var isPartial = !isComplete && sightings.Count == 0;
            int? nextOffset = isComplete || isPartial ? null : observedEnd;
            var nextContinuation = isComplete || isPartial ? null : nextCursor;
            var partialReason = isPartial ? "short-page-before-total-results" : null;

            if (isComplete && observedEnd > acquisitionEnd)
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    "response-page-window-mismatch");
            }

            return new RuntimeProviderPageResult(
                page,
                response,
                sightings,
                warnings,
                nextContinuation,
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
                    partialReason));
        }
    }

    public RuntimeProviderPageResult ParseRecordedBatchResponse(
        ProviderAcquisitionRequest acquisition,
        ProviderPageRequest page,
        IReadOnlyList<string> requestedPaperIds,
        byte[] exactResponseBytes,
        RuntimeProviderResponseEvidence response)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(requestedPaperIds);
        ArgumentNullException.ThrowIfNull(exactResponseBytes);
        ArgumentNullException.ThrowIfNull(response);

        if (!string.Equals(acquisition.ProviderAlias, ProviderAlias, StringComparison.Ordinal))
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                $"Unknown Semantic Scholar adapter provider alias '{acquisition.ProviderAlias}'.");
        }

        if (page.PageIndex != 0 ||
            page.Offset != page.AcquisitionOffset ||
            page.PreviousPageResultDigest is not null)
        {
            return FailureResult(
                acquisition,
                page,
                response,
                ProviderAcquisitionErrorCodes.InvalidProviderPage,
                "batch-page-not-supported");
        }

        if (string.IsNullOrWhiteSpace(response.ParserId) ||
            !string.Equals(response.ParserId, ParserId, StringComparison.Ordinal) ||
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
        if (!string.Equals(response.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            !CanonicalTimestamp.IsCanonicalUtc(response.ReceivedAt, rejectDefault: true) ||
            response.StatusCode is < 100 or > 599)
        {
            return FailureResult(
                acquisition,
                page,
                response,
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "invalid-response-envelope");
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

        var normalizedIds = ValidateBatchIdentifiers(requestedPaperIds);
        if (normalizedIds.Count == 0 || normalizedIds.Count > 500)
        {
            return FailureResult(
                acquisition,
                page,
                response,
                ProviderAcquisitionErrorCodes.InvalidProviderPage,
                "invalid-batch-size");
        }

        if (normalizedIds.Count > page.PageSize)
        {
            return FailureResult(
                acquisition,
                page,
                response,
                ProviderAcquisitionErrorCodes.InvalidProviderPage,
                "batch-size-mismatch");
        }

        var expectedBatchRequest = DescribePaperBatchRequest(normalizedIds);
        if (response.ProviderAlias != ProviderAlias ||
            response.SanitizedRequestDigest != expectedBatchRequest.Digest)
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.InvalidProviderEvidence,
                "Semantic Scholar response evidence is not bound to the sanitized batch request.");
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
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                    "unsupported-response-envelope");
            }

            var mapped = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            var identifierCollision = false;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var identifier in ResponseIdentifiers(item))
                {
                    if (mapped.TryGetValue(identifier, out var existing) &&
                        !string.Equals(existing.GetRawText(), item.GetRawText(), StringComparison.Ordinal))
                    {
                        identifierCollision = true;
                        break;
                    }

                    mapped.TryAdd(identifier, item);
                }
            }

            if (identifierCollision)
            {
                return FailureResult(
                    acquisition,
                    page,
                    response,
                    ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                    "batch-identifier-collision");
            }

            var warnings = new List<string>();
            var sightings = new List<SearchSighting>();
            for (var rank = 0; rank < normalizedIds.Count; rank++)
            {
                var requestedId = normalizedIds[rank];
                if (mapped.TryGetValue(requestedId, out var item))
                {
                    var indexWarnings = new List<string>();
                    var sighting = BuildSightingFromPayload(
                        acquisition,
                        item,
                        page.Offset,
                        rank + 1,
                        indexWarnings,
                        response.RawResponseDigest.ToString(),
                        includeIndexFromOutput: true);
                    sightings.Add(sighting);
                    warnings.AddRange(indexWarnings);
                    continue;
                }

                warnings.Add($"item-{rank + 1}-missing-batch-result");
                var unresolved = ScholarlyWork.UnresolvedCandidate(
                    "Unknown Title",
                    $"{ProviderAlias}:unresolved:{response.RawResponseDigest}:{page.Offset + rank}:{requestedId}",
                    rawData: acquisition.IncludeRawData
                        ? new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["raw_provider_response_digest"] = response.RawResponseDigest.ToString(),
                            ["raw_provider_item_index"] = (page.Offset + rank).ToString(CultureInfo.InvariantCulture)
                        }
                        : null);
                sightings.Add(new SearchSighting(ProviderAlias, 1, page.Offset + rank + 1, unresolved));
            }

            return new RuntimeProviderPageResult(
                page,
                response,
                sightings,
                warnings,
                null,
                null,
                isComplete: true,
                isPartial: false,
                null,
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
                    "success",
                    "complete",
                    response.ObservedHeaders.TryGetValue("retry-after", out var retryAfter) ? retryAfter : null,
                    response.ObservedHeaders.TryGetValue("x-rate-limit-limit", out var rateLimitLimit) ? rateLimitLimit : null,
                    response.ObservedHeaders.TryGetValue("x-rate-limit-interval", out var rateLimitInterval) ? rateLimitInterval : null,
                    null));
        }
    }

    public static void VerifyNextPage(RuntimeProviderPageResult current, ProviderPageRequest next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);
        if (string.IsNullOrWhiteSpace(next.Cursor) ||
            !string.Equals(next.Cursor, current.NextCursor, StringComparison.Ordinal))
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "Semantic Scholar next cursor is not chained.");
        }

        if (next.AcquisitionRequestDigest != current.Request.AcquisitionRequestDigest ||
            next.PageIndex != current.Request.PageIndex + 1 ||
            next.Offset != current.Request.Offset + current.Sightings.Count ||
            !next.PreviousPageResultDigest.HasValue ||
            next.PreviousPageResultDigest.Value != current.Digest ||
            current.NextCursor is null ||
            current.IsComplete ||
            current.IsPartial ||
            !current.NextOffset.HasValue)
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                "Semantic Scholar next page does not continue the accepted page chain.");
        }
    }

    public static void ValidateSanitizedDescriptor(string descriptor) =>
        ValidateSanitizedDescriptor(descriptor, allowPaginationToken: false);

    private static void ValidateSanitizedDescriptor(
        string descriptor,
        bool allowPaginationToken)
    {
        if (string.IsNullOrWhiteSpace(descriptor) || descriptor.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "Semantic Scholar request descriptor contains a URL, contact, credential, or secret-shaped field.");
        }

        var queryIndex = descriptor.IndexOf('?');
        var descriptorPath = queryIndex < 0 ? descriptor : descriptor[..queryIndex];
        if (queryIndex < 0)
        {
            return;
        }

        foreach (var pair in descriptor[(queryIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = separator < 0 ? pair : pair[..separator];
            var value = separator < 0 ? string.Empty : pair[(separator + 1)..];
            if (ProviderSecretPolicy.ContainsForbiddenDescriptorValue(
                    name,
                    value,
                    allowPaginationToken: allowPaginationToken &&
                        string.Equals(
                            descriptorPath,
                            "/graph/v1/paper/search/bulk",
                            StringComparison.Ordinal)))
            {
                throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "Semantic Scholar request descriptor contains a contact, credential, or secret-shaped parameter.");
            }
        }
    }

    private static SearchSighting BuildSightingFromPayload(
        ProviderAcquisitionRequest acquisition,
        JsonElement item,
        int pageOffset,
        int rank,
        List<string> warnings,
        string rawResponseDigest,
        bool includeIndexFromOutput)
    {
        var outputRank = rank;
        var title = OptionalString(item, "title")?.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Unknown Title";
            warnings.Add($"item-{outputRank}-missing-title");
        }

        var paperId = OptionalString(item, "paperId")?.Trim();
        if (string.IsNullOrWhiteSpace(paperId))
        {
            warnings.Add($"item-{outputRank}-missing-paper-id");
        }

        var resolvedIds = new List<WorkId>();
        if (!string.IsNullOrWhiteSpace(paperId))
        {
            resolvedIds.Add(WorkId.From("s2", paperId));
        }

        if (!item.TryGetProperty("year", out var yearElement))
        {
            warnings.Add($"item-{outputRank}-missing-year");
        }
        else if (yearElement.ValueKind == JsonValueKind.Number)
        {
            if (!yearElement.TryGetInt32(out var year) || year <= 0)
            {
                warnings.Add($"item-{outputRank}-invalid-year");
            }
        }
        else if (yearElement.ValueKind != JsonValueKind.Null)
        {
            warnings.Add($"item-{outputRank}-invalid-year");
        }

        if (!item.TryGetProperty("externalIds", out var externalIds) || externalIds.ValueKind != JsonValueKind.Object)
        {
            warnings.Add($"item-{outputRank}-missing-external-ids");
        }
        else
        {
            var doi = OptionalString(externalIds, "doi") ?? OptionalString(externalIds, "DOI");
            var arxiv = OptionalString(externalIds, "arxiv") ?? OptionalString(externalIds, "ArXiv");
            var pubmed = OptionalString(externalIds, "pubmed") ?? OptionalString(externalIds, "PubMed");

            if (!string.IsNullOrWhiteSpace(doi))
            {
                resolvedIds.Add(WorkId.From("doi", doi));
            }

            if (!string.IsNullOrWhiteSpace(arxiv))
            {
                resolvedIds.Add(WorkId.From("arxiv", arxiv));
            }

            if (!string.IsNullOrWhiteSpace(pubmed))
            {
                resolvedIds.Add(WorkId.From("pubmed", pubmed));
            }
        }

        if (resolvedIds.Count == 0)
        {
            warnings.Add($"item-{outputRank}-invalid-ids");
        }

        Dictionary<string, string>? rawData = null;
        if (acquisition.IncludeRawData)
        {
            rawData = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["raw_provider_response_digest"] = rawResponseDigest,
                ["raw_provider_item_index"] = checked(pageOffset + (includeIndexFromOutput ? outputRank - 1 : 0)).ToString(CultureInfo.InvariantCulture)
            };
        }

        var sourceContext = !string.IsNullOrWhiteSpace(paperId)
            ? $"{ProviderAlias}:{paperId}"
            : $"{ProviderAlias}:unresolved:{rawResponseDigest}:{pageOffset + outputRank - 1}";

        var work = resolvedIds.Count == 0
            ? ScholarlyWork.UnresolvedCandidate(
                title,
                sourceContext,
                rawData: rawData)
            : ScholarlyWork.Identified(
                title,
                WorkIdSet.From(resolvedIds.ToArray()),
                sourceContext: sourceContext,
                rawData: rawData);

        return new SearchSighting(ProviderAlias, 1, outputRank, work);
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

    private static List<string> ValidateBatchIdentifiers(IReadOnlyList<string> ids)
    {
        var normalized = new List<string>(ids.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in ids)
        {
            var trimmed = (id ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw Rule(ProviderAcquisitionErrorCodes.InvalidProviderPage, "Semantic Scholar paper id must not be blank.");
            }

            if (!seen.Add(trimmed))
            {
                throw Rule(ProviderAcquisitionErrorCodes.InvalidProviderPage, "Semantic Scholar batch paper ids must be unique.");
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    private static IEnumerable<string> ResponseIdentifiers(JsonElement item)
    {
        var paperId = OptionalString(item, "paperId")?.Trim();
        if (!string.IsNullOrWhiteSpace(paperId))
        {
            yield return paperId;
            yield return $"S2:{paperId}";
        }

        if (!item.TryGetProperty("externalIds", out var externalIds) ||
            externalIds.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in externalIds.EnumerateObject())
        {
            if (property.Value.ValueKind is not JsonValueKind.String and not JsonValueKind.Number)
            {
                continue;
            }

            var value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()?.Trim()
                : property.Value.GetRawText();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            yield return value;
            var prefix = property.Name.ToUpperInvariant() switch
            {
                "ARXIV" => "ARXIV",
                "DOI" => "DOI",
                "PUBMED" => "PMID",
                "PUBMEDCENTRAL" => "PMCID",
                "CORPUSID" => "CorpusId",
                "MAG" => "MAG",
                "ACL" => "ACL",
                "DBLP" => "DBLP",
                _ => property.Name
            };
            var unprefixed = value.StartsWith($"{prefix}:", StringComparison.OrdinalIgnoreCase)
                ? value[(prefix.Length + 1)..]
                : value;
            yield return $"{prefix}:{unprefixed}";
        }
    }

    private static byte[] BuildBatchRequestBody(IReadOnlyList<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var serializer = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        var json = JsonSerializer.Serialize(new { ids }, serializer);
        return Encoding.UTF8.GetBytes(json);
    }

    private static string TranslateQuery(string value)
    {
        var translated = Regex.Replace(
            value ?? string.Empty,
            @"\bNOT\b\s+",
            "-",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        translated = Regex.Replace(
            translated,
            @"\bAND\b",
            "+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        translated = Regex.Replace(
            translated,
            @"\bOR\b",
            "|",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        translated = Regex.Replace(
            translated,
            @"\s+",
            " ",
            RegexOptions.CultureInvariant);
        return translated.Trim();
    }

    private static string? OptionalString(JsonElement parent, string propertyName)
    {
        if (parent.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static void ValidateQueryValue(string query)
    {
        if (ProviderSecretPolicy.ContainsForbiddenQueryValue(query))
        {
            throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "Provider query contains URL, contact, credential, or secret-shaped material.");
        }
    }

    private static SearchRuleException Rule(string category, string message) =>
        new(category, message);
}
