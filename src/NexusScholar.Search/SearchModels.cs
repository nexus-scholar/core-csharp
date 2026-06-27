using System.Collections.ObjectModel;
using System.Globalization;
using NexusScholar.Kernel;
using NexusScholar.Shared;

namespace NexusScholar.Search;

public sealed record SearchQueryInput(
    string Query,
    int? YearFrom,
    int? YearTo,
    string? Language,
    int MaxResults,
    int Offset,
    bool IncludeRawData,
    IReadOnlyList<string> SelectedProviderAliases,
    SearchPlanBinding? PlanBinding = null);

public sealed record SearchPlanBinding(
    string PlanId,
    string ItemId,
    string SchemaId,
    string SchemaVersion,
    string? ProjectId = null);

public sealed record SearchYearRange(int? From, int? To)
{
    public static SearchYearRange Validate(int? from, int? to, int validationYear)
    {
        var maxYear = validationYear + 5;

        if (from.HasValue && from.Value < 1000)
        {
            throw new SearchRuleException(
                SearchErrorCodes.YearFromBelowMinimum,
                "Search year_from must be 1000 or greater.");
        }

        if (to.HasValue && to.Value > maxYear)
        {
            throw new SearchRuleException(
                SearchErrorCodes.YearToExceedsValidationYear,
                "Search year_to exceeds validationYear + 5.");
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new SearchRuleException(
                SearchErrorCodes.YearRangeInverted,
                "Search year range is inverted.");
        }

        return new SearchYearRange(from, to);
    }
}

public sealed record SearchQueryTerm(string Value)
{
    public static SearchQueryTerm From(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length < 2)
        {
            throw new SearchRuleException(
                SearchErrorCodes.QueryLengthBelowMinimum,
                "Search term requires at least two non-whitespace characters.");
        }

        return new SearchQueryTerm(trimmed);
    }
}

public sealed record SearchTraceRequest(
    string Query,
    SearchYearRange? YearRange,
    string? Language,
    int MaxResults,
    int Offset,
    bool IncludeRawData,
    IReadOnlyList<string> SelectedProviderAliases,
    IReadOnlyList<string> ActiveProviderAliases,
    SearchPlanBinding? PlanBinding = null);

public sealed record SearchCacheIdentity(
    string Algorithm,
    string MaterialVersion,
    IReadOnlyList<string> IncludedFields,
    IReadOnlyList<string> ExcludedFields,
    bool ProviderOrderInsensitive,
    string CacheKey,
    string TraceMaterial)
{
    public const string AlgorithmId = "sha256";
    public const string MaterialVersionId = "1.0.0";

    public static readonly IReadOnlyList<string> IncludedFieldNames =
        new ReadOnlyCollection<string>(
            [
                "query",
                "year_from",
                "year_to",
                "language",
                "max_results",
                "offset",
                "active_provider_aliases",
                "include_raw_data"
            ]);

    public static readonly IReadOnlyList<string> ExcludedFieldNames =
        new ReadOnlyCollection<string>(
            [
                "query_id",
                "trace_id",
                "project_id",
                "runtime_duration_ms",
                "provider_stats",
                "provider_failures",
                "raw_payload_bytes",
                "app_id",
                "app_hash",
                "local_paths",
                "provider_credentials"
            ]);

    public static SearchCacheIdentity Compute(
        SearchQueryInput input,
        int validationYear,
        IReadOnlyList<string> activeAliases)
    {
        ArgumentNullException.ThrowIfNull(input);
        _ = validationYear;

        var query = SearchQueryTerm.From(input.Query);
        var yearRange = SearchYearRange.Validate(input.YearFrom, input.YearTo, validationYear);

        var normalizedAliases = SearchService.NormalizeProviderAliases(activeAliases).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var material = new CanonicalJsonObject()
            .Add("query", query.Value)
            .Add("year_from", yearRange.From?.ToString() ?? string.Empty)
            .Add("year_to", yearRange.To?.ToString() ?? string.Empty)
            .Add("language", input.Language ?? string.Empty)
            .Add("max_results", input.MaxResults)
            .Add("offset", input.Offset)
            .Add(
                "active_provider_aliases",
                CanonicalJsonValue.Array(normalizedAliases.Select(CanonicalJsonValue.From).ToArray()))
            .Add("include_raw_data", input.IncludeRawData);

        var key = ContentDigest.Sha256CanonicalJson(material);
        return new SearchCacheIdentity(
            AlgorithmId,
            MaterialVersionId,
            IncludedFieldNames,
            ExcludedFieldNames,
            ProviderOrderInsensitive: true,
            key.ToString(),
            CanonicalJsonSerializer.Serialize(material));
    }
}

public sealed record SearchProviderAttempt(int AttemptOrder, string ProviderAlias, string Status, int ResultCount, string? SkipReason = null);

public sealed record SearchProviderStat(string ProviderAlias, int ResultCount, long DurationMs, string? SkipReason = null);

public sealed record SearchSummary(
    int AttemptedProviders,
    int SucceededProviders,
    int FailedProviders,
    int RawSightingCount,
    bool AllFailed);

public sealed record SearchSighting(
    string ProviderAlias,
    int ProviderOrder,
    int ProviderLocalRank,
    ScholarlyWork Work)
{
    public string? ProviderWorkId => Work.PrimaryWorkId?.ToString();
    public IReadOnlyList<string> WorkIds => Work.WorkIds.Ids.Select(identifier => identifier.ToString()).ToArray();
}

public sealed record SearchTrace(
    string TraceId,
    string SchemaId,
    string SchemaVersion,
    SearchTraceRequest Request,
    SearchCacheIdentity CacheIdentity,
    IReadOnlyList<SearchProviderAttempt> ProviderAttempts,
    IReadOnlyList<SearchProviderStat> ProviderStats,
    IReadOnlyList<SearchSighting> Sightings,
    SearchSummary Summary,
    IReadOnlyList<string> NonClaims)
{
    public const string TraceSchemaId = "nexus.search.trace";
    public const string TraceSchemaVersion = "1.0.0";

    public static readonly IReadOnlyList<string> DefaultNonClaims = new[]
    {
        "no-php-compatibility-claim",
        "no-live-provider-network",
        "no-dedup-at-search-time"
    };
}

public static class SearchImportErrorCodes
{
    public const string UnsupportedFormat = "unsupported-format";
    public const string MalformedRecord = "malformed-record";
    public const string MissingRequiredField = "missing-required-field";
    public const string UnknownIdentifierType = "unknown-identifier-type";
    public const string DuplicateSourceRecordId = "duplicate-source-record-id";
    public const string SkippedRecord = "skipped-record";
    public const string ParserWarning = "parser-warning";
}

public sealed record SearchImportRequest(
    string SourceDatabaseOrTool,
    string ExportFormat,
    string ParserId,
    string ParserVersion,
    string ImportedBy,
    string ImportedAt,
    string? OriginalQueryText = null,
    string? ExportedAt = null);

public sealed record SearchImportParserNotice(
    string Category,
    string Message,
    int? RecordIndex = null,
    string? SourceRecordId = null);

public sealed record SearchImportMetadata(
    string AcquisitionKind,
    string SourceDatabaseOrTool,
    string ExportFormat,
    string ParserId,
    string ParserVersion,
    string SourceFileDigest,
    string ImportedBy,
    string ImportedAt,
    string? OriginalQueryText,
    string? ExportedAt,
    int RecordCount,
    IReadOnlyList<SearchImportParserNotice> ParserWarnings)
{
    public const string AcquisitionKindImportedExport = "imported-export";
}

public sealed record SearchImportRecord(
    string SourceDatabaseOrTool,
    string SourceRecordId,
    string? SourceIdentifier,
    IReadOnlyList<string> SourceIdentifiers,
    ScholarlyWork Work,
    IReadOnlyList<string> Authors,
    int? Year,
    string? Venue,
    string? Abstract,
    IReadOnlyList<string> Keywords,
    string? RawRecordDigest,
    string? RawRecordText,
    bool IsSkipped,
    string? SkipReason,
    IReadOnlyList<SearchImportParserNotice> Notices)
{
    public static SearchImportRecord Skipped(
        string sourceDatabaseOrTool,
        string sourceRecordId,
        string message,
        IReadOnlyList<SearchImportParserNotice> notices)
    {
        var work = ScholarlyWork.UnresolvedCandidate("Unknown title", sourceRecordId);
        return new SearchImportRecord(
            sourceDatabaseOrTool,
            sourceRecordId,
            null,
            Array.Empty<string>(),
            work,
            Array.Empty<string>(),
            null,
            null,
            null,
            Array.Empty<string>(),
            null,
            null,
            true,
            message,
            notices);
    }

    public bool IsResolved => Work.HasStableIdentifier;
}

public sealed record SearchImportTrace(
    string TraceId,
    string SchemaId,
    string SchemaVersion,
    SearchImportMetadata Metadata,
    IReadOnlyList<SearchImportRecord> ImportedRecords,
    IReadOnlyList<SearchSighting> Sightings,
    IReadOnlyList<SearchImportParserNotice> ParserWarnings,
    IReadOnlyList<string> NonClaims)
{
    public static readonly IReadOnlyList<string> DefaultNonClaims = new[]
    {
        "no-php-compatibility-claim",
        "no-live-provider-network",
        "no-network-requests",
        "no-google-scholar-scraping",
        "no-import-parser"
    };

    public string TraceSummary()
    {
        var importer = CultureInfo.InvariantCulture;
        return string.Format(importer, "imported {0}/{1} records", Sightings.Count, Metadata.RecordCount);
    }
}
