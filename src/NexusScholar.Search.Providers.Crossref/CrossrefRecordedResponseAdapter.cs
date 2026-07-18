using System.Collections.ObjectModel;
using System.Text.Json;
using NexusScholar.Kernel;
using NexusScholar.Search;
using NexusScholar.Shared;

namespace NexusScholar.Search.Providers.Crossref;

public sealed record CrossrefRequestDescriptor(
    string Method,
    string EndpointPathAndQuery,
    string ProviderAlias,
    string PageRequestDigest);

public sealed class CrossrefRecordedResponseAdapter
{
    public const string ProviderAlias = "crossref";
    public const string ParserId = "nexus.crossref.works-json";
    public const string ParserVersion = "1.0.0";

    public CrossrefRequestDescriptor DescribeRequest(
        ProviderAcquisitionRequest acquisition,
        ProviderPageRequest page)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        ArgumentNullException.ThrowIfNull(page);
        if (!string.Equals(acquisition.ProviderAlias, ProviderAlias, StringComparison.Ordinal))
        {
            throw Rule(SearchErrorCodes.UnknownProviderAlias, $"Unknown Crossref adapter provider alias '{acquisition.ProviderAlias}'.");
        }

        if (page.AcquisitionRequestDigest != acquisition.Digest)
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "Crossref page request is not bound to its acquisition request.");
        }

        ValidateQueryValue(acquisition.Query);
        var query = new List<string>
        {
            $"query.bibliographic={Uri.EscapeDataString(acquisition.Query)}",
            $"rows={page.PageSize}",
            $"offset={page.Offset}"
        };
        var filters = new List<string>();
        if (acquisition.YearRange?.From is int from)
        {
            filters.Add($"from-pub-date:{from}");
        }

        if (acquisition.YearRange?.To is int to)
        {
            filters.Add($"until-pub-date:{to}");
        }

        if (filters.Count > 0)
        {
            query.Add($"filter={Uri.EscapeDataString(string.Join(",", filters))}");
        }

        var descriptor = $"/works?{string.Join("&", query)}";
        ValidateSanitizedDescriptor(descriptor);
        return new CrossrefRequestDescriptor("GET", descriptor, ProviderAlias, page.Digest.ToString());
    }

    public ProviderPageResult ParseRecordedResponse(
        ProviderAcquisitionRequest acquisition,
        ProviderPageRequest page,
        RecordedProviderFixtureEvidence fixture,
        byte[] exactResponseBytes,
        int statusCode,
        string mediaType,
        DateTimeOffset receivedAt,
        IReadOnlyDictionary<string, string>? observedHeaders = null,
        ProviderPageResult? previousPageResult = null)
    {
        ArgumentNullException.ThrowIfNull(acquisition);
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(exactResponseBytes);
        if (!string.Equals(acquisition.ProviderAlias, ProviderAlias, StringComparison.Ordinal))
        {
            throw Rule(SearchErrorCodes.UnknownProviderAlias, $"Unknown Crossref adapter provider alias '{acquisition.ProviderAlias}'.");
        }

        if (page.AcquisitionRequestDigest != acquisition.Digest)
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "Crossref response page is not bound to its acquisition request.");
        }

        if (page.PageIndex == 0)
        {
            if (previousPageResult is not null)
            {
                throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "The first Crossref page cannot declare a previous page result.");
            }
        }
        else
        {
            if (previousPageResult is null)
            {
                throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "A later Crossref page requires its preceding page result.");
            }

            VerifyNextPage(previousPageResult, page);
        }

        fixture.Verify(exactResponseBytes);
        if (!CanonicalTimestamp.IsCanonicalUtc(receivedAt, rejectDefault: true) ||
            statusCode is < 100 or > 599 ||
            !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw Rule(ProviderAcquisitionErrorCodes.InvalidProviderEvidence, "Crossref response metadata is invalid.");
        }

        var response = new ProviderRawResponseEvidence(
            ProviderAlias,
            fixture.FixtureId,
            fixture.FixtureRelativePath,
            fixture.Digest,
            fixture.RawResponseDigest,
            fixture.ByteLength,
            statusCode,
            mediaType.ToLowerInvariant(),
            receivedAt,
            RecordedProviderFixtureEvidence.RetentionDisposition);

        var headers = observedHeaders ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        ValidateObservedHeaders(headers);
        var retryAfter = Header(headers, "retry-after");
        var rateLimit = Header(headers, "x-rate-limit-limit");
        var rateInterval = Header(headers, "x-rate-limit-interval");

        if (!string.Equals(fixture.ParserId, ParserId, StringComparison.Ordinal) ||
            !string.Equals(fixture.ParserVersion, ParserVersion, StringComparison.Ordinal))
        {
            return FailureResult(
                acquisition, page, fixture, response, "unsupported-parser-version",
                retryAfter, rateLimit, rateInterval);
        }

        if (statusCode != 200)
        {
            var category = $"http-{statusCode}";
            return FailureResult(
                acquisition, page, fixture, response, category,
                retryAfter, rateLimit, rateInterval);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(exactResponseBytes);
        }
        catch (JsonException exception)
        {
            return FailureResult(
                acquisition, page, fixture, response, ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                retryAfter, rateLimit, rateInterval, $"invalid-json:{exception.GetType().Name}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (!HasString(root, "status", "ok") ||
                !HasString(root, "message-type", "work-list") ||
                !HasString(root, "message-version", "1.0.0"))
            {
                return FailureResult(
                    acquisition, page, fixture, response, ProviderAcquisitionErrorCodes.ProviderSchemaDrift,
                    retryAfter, rateLimit, rateInterval, "unsupported-response-envelope");
            }

            if (!root.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("total-results", out var totalElement) || !totalElement.TryGetInt32(out var totalResults) ||
                !message.TryGetProperty("items-per-page", out var itemsPerPageElement) || !itemsPerPageElement.TryGetInt32(out var itemsPerPage) ||
                !message.TryGetProperty("query", out var responseQuery) || responseQuery.ValueKind != JsonValueKind.Object ||
                !responseQuery.TryGetProperty("start-index", out var startIndexElement) || !startIndexElement.TryGetInt32(out var startIndex) ||
                !message.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array ||
                totalResults < 0 || itemsPerPage != page.PageSize || startIndex != page.Offset)
            {
                return FailureResult(
                    acquisition, page, fixture, response, ProviderAcquisitionErrorCodes.PaginationChainMismatch,
                    retryAfter, rateLimit, rateInterval, "response-page-window-mismatch");
            }

            var warnings = new List<string>();
            var sightings = new List<SearchSighting>();
            var rank = 1;
            foreach (var item in items.EnumerateArray())
            {
                var title = FirstString(item, "title");
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "Unknown Title";
                    warnings.Add($"item-{rank}-missing-title");
                }

                var doi = OptionalString(item, "DOI");
                ScholarlyWork work;
                var rawData = acquisition.IncludeRawData
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["raw_provider_response_digest"] = fixture.RawResponseDigest.ToString(),
                        ["raw_provider_item_index"] = (page.Offset + rank - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    }
                    : null;
                if (string.IsNullOrWhiteSpace(doi))
                {
                    warnings.Add($"item-{rank}-missing-doi");
                    work = ScholarlyWork.UnresolvedCandidate(
                        title,
                        $"crossref:{fixture.FixtureId}:{page.Offset + rank - 1}",
                        rawData: rawData);
                }
                else
                {
                    work = ScholarlyWork.Identified(
                        title,
                        WorkIdSet.From(WorkId.From("doi", doi)),
                        sourceContext: $"crossref:{doi.Trim().ToLowerInvariant()}",
                        rawData: rawData);
                }

                sightings.Add(new SearchSighting(ProviderAlias, 1, rank++, work));
            }

            var count = sightings.Count;
            var observedEnd = checked(page.Offset + count);
            var acquisitionEnd = checked(page.AcquisitionOffset + page.AcquisitionMaxResults);
            var isComplete = observedEnd >= totalResults || observedEnd >= acquisitionEnd;
            var isPartial = !isComplete && count < page.PageSize;
            var partialReason = isPartial ? "short-page-before-total-results" : null;
            int? nextOffset = isComplete || isPartial ? null : observedEnd;
            var outcome = isPartial ? "partial" : "success";

            return new ProviderPageResult(
                page,
                fixture,
                response,
                ParserId,
                ParserVersion,
                sightings,
                warnings,
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
                    acquisition.RequestedAt,
                    response.ReceivedAt,
                    outcome,
                    isComplete ? "complete" : isPartial ? "partial" : "continuable",
                    retryAfter,
                    rateLimit,
                    rateInterval,
                    partialReason));
        }
    }

    public static void ValidateSanitizedDescriptor(string descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor) || descriptor.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "Provider request descriptor contains a URL, contact, credential, or secret-bearing field.");
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
                throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "Provider request descriptor contains a contact, credential, or secret-bearing parameter.");
            }
        }
    }

    public static void VerifyNextPage(ProviderPageResult current, ProviderPageRequest next)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(next);
        if (!current.NextOffset.HasValue ||
            next.AcquisitionRequestDigest != current.Request.AcquisitionRequestDigest ||
            next.PageIndex != current.Request.PageIndex + 1 ||
            next.Offset != current.NextOffset.Value ||
            next.PreviousPageResultDigest != current.Digest)
        {
            throw Rule(ProviderAcquisitionErrorCodes.PaginationChainMismatch, "Crossref next page does not continue the accepted page chain.");
        }
    }

    private static string? Header(IReadOnlyDictionary<string, string> headers, string name) =>
        headers.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    private static void ValidateObservedHeaders(IReadOnlyDictionary<string, string> headers)
    {
        var admittedHeaders = new[]
        {
            "retry-after",
            "x-rate-limit-limit",
            "x-rate-limit-interval"
        };

        if (headers.Keys.Any(name =>
                !admittedHeaders.Contains(name, StringComparer.OrdinalIgnoreCase)))
        {
            throw Rule(
                ProviderAcquisitionErrorCodes.SecretBearingDescriptor,
                "Only admitted retry and rate-limit observation headers may cross the recorded provider boundary.");
        }
    }

    private static string? OptionalString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FirstString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                return item.GetString();
            }
        }

        return null;
    }

    private static bool HasString(JsonElement parent, string propertyName, string expected) =>
        parent.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static ProviderPageResult FailureResult(
        ProviderAcquisitionRequest acquisition,
        ProviderPageRequest page,
        RecordedProviderFixtureEvidence fixture,
        ProviderRawResponseEvidence response,
        string category,
        string? retryAfter,
        string? rateLimit,
        string? rateInterval,
        string? stopReason = null) =>
        new(
            page,
            fixture,
            response,
            ParserId,
            ParserVersion,
            Array.Empty<SearchSighting>(),
            new[] { category },
            null,
            isComplete: false,
            isPartial: true,
            partialReason: category,
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
                acquisition.RequestedAt,
                response.ReceivedAt,
                category,
                "failed",
                retryAfter,
                rateLimit,
                rateInterval,
                stopReason ?? category));

    private static void ValidateQueryValue(string query)
    {
        if (ProviderSecretPolicy.ContainsForbiddenQueryValue(query))
        {
            throw Rule(ProviderAcquisitionErrorCodes.SecretBearingDescriptor, "Provider query contains URL, contact, credential, or secret-shaped material.");
        }
    }

    private static SearchRuleException Rule(string category, string message) => new(category, message);
}
