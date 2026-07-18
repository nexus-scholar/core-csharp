using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NexusScholar.Kernel;
using NexusScholar.Shared;

namespace NexusScholar.Search;

public sealed class SearchImportService
{
    private readonly HashSet<string> _supportedFormats = new(
        new[] { "ris", "bibtex", "scopus-csv" },
        StringComparer.Ordinal);

    public SearchImportTrace Parse(string traceId, SearchImportRequest request, byte[] sourceFileBytes)
    {
        ArgumentNullException.ThrowIfNull(traceId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sourceFileBytes);

        var sourceDatabaseOrTool = Guard.NotBlank(request.SourceDatabaseOrTool, nameof(request.SourceDatabaseOrTool));
        var exportFormat = Guard.NotBlank(request.ExportFormat, nameof(request.ExportFormat));
        var parserId = Guard.NotBlank(request.ParserId, nameof(request.ParserId));
        var parserVersion = Guard.NotBlank(request.ParserVersion, nameof(request.ParserVersion));
        var importedBy = Guard.NotBlank(request.ImportedBy, nameof(request.ImportedBy));
        var importedAt = Guard.NotBlank(request.ImportedAt, nameof(request.ImportedAt));

        if (sourceFileBytes.Length == 0)
        {
            throw new SearchRuleException(
                SearchImportErrorCodes.MalformedRecord,
                "Source import bytes are empty.");
        }

        var normalizedFormat = NormalizeFormat(exportFormat);
        if (normalizedFormat.Contains("google", StringComparison.OrdinalIgnoreCase) &&
            normalizedFormat.Contains("scholar", StringComparison.OrdinalIgnoreCase))
        {
            throw new SearchRuleException(
                SearchImportErrorCodes.UnsupportedFormat,
                "Google Scholar scraping is not supported for imported-export parsing.");
        }

        if (!_supportedFormats.Contains(normalizedFormat))
        {
            throw new SearchRuleException(
                SearchImportErrorCodes.UnsupportedFormat,
                $"Unsupported import format '{exportFormat}'.");
        }

        var parserWarnings = new List<SearchImportParserNotice>();
        var records = normalizedFormat switch
        {
            "ris" => ParseRis(sourceFileBytes, sourceDatabaseOrTool, parserWarnings),
            "bibtex" => ParseBibTex(sourceFileBytes, sourceDatabaseOrTool, parserWarnings),
            "scopus-csv" => ParseScopusCsv(sourceFileBytes, sourceDatabaseOrTool, parserWarnings),
            _ => throw new SearchRuleException(SearchImportErrorCodes.UnsupportedFormat, $"Unsupported import format '{exportFormat}'.")
        };

        var sourceFileDigest = ContentDigest.Sha256(sourceFileBytes).ToString();
        var metadata = new SearchImportMetadata(
            SearchImportMetadata.AcquisitionKindImportedExport,
            sourceDatabaseOrTool,
            exportFormat,
            parserId,
            parserVersion,
            sourceFileDigest,
            DigestScope.RawArtifactBytes.ToString(),
            importedBy,
            importedAt,
            request.OriginalQueryText,
            request.ExportedAt,
            records.Count,
            new ReadOnlyCollection<SearchImportParserNotice>(parserWarnings.ToArray()));

        var sightings = new List<SearchSighting>();
        var sightingOrder = 1;
        foreach (var record in records)
        {
            if (record.IsSkipped)
            {
                continue;
            }

            sightings.Add(new SearchSighting(
                record.SourceDatabaseOrTool,
                1,
                sightingOrder,
                record.Work));
            sightingOrder++;
        }

        return new SearchImportTrace(
            Guard.NotBlank(traceId, nameof(traceId)),
            SearchTrace.TraceSchemaId,
            SearchTrace.TraceSchemaVersion,
            metadata,
            new ReadOnlyCollection<SearchImportRecord>(records),
            new ReadOnlyCollection<SearchSighting>(sightings),
            new ReadOnlyCollection<SearchImportParserNotice>(parserWarnings),
            SearchImportTrace.DefaultNonClaims);
    }

    private static List<SearchImportRecord> ParseRis(
        byte[] sourceFileBytes,
        string sourceDatabaseOrTool,
        List<SearchImportParserNotice> parserWarnings)
    {
        var text = Encoding.UTF8.GetString(sourceFileBytes).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = text.Split('\n');
        var recordLines = new List<string>();
        var currentBlockOpen = false;
        var discardCurrentBlock = false;
        var seenRecordIds = new HashSet<string>(StringComparer.Ordinal);
        var records = new List<SearchImportRecord>();
        var recordIndex = 0;

        void finalizeRecord()
        {
            if (recordLines.Count == 0)
            {
                return;
            }

            recordIndex++;
            var parsedRecord = ParseRisRecord(
                recordLines,
                sourceDatabaseOrTool,
                recordIndex,
                seenRecordIds,
                parserWarnings);
            records.Add(parsedRecord);
            recordLines.Clear();
        }

        foreach (var line in lines)
        {
            var normalized = line.TrimEnd();
            if (normalized.Length == 0 && !currentBlockOpen)
            {
                continue;
            }

            if (normalized.StartsWith("TY  -", StringComparison.Ordinal))
            {
                if (currentBlockOpen)
                {
                    if (!discardCurrentBlock)
                    {
                        recordIndex++;
                        records.Add(CreateSkippedRecord(
                            sourceDatabaseOrTool,
                            $"row-{recordIndex}",
                            SearchImportErrorCodes.MalformedRecord,
                            "Nested TY header found before ER block.",
                            parserWarnings));
                    }

                    recordLines.Clear();
                    discardCurrentBlock = true;
                    continue;
                }

                currentBlockOpen = true;
                discardCurrentBlock = false;
                recordLines.Add(normalized);
                continue;
            }

            if (!currentBlockOpen)
            {
                continue;
            }

            if (!discardCurrentBlock)
            {
                recordLines.Add(normalized);
            }

            if (normalized.StartsWith("ER  -", StringComparison.Ordinal))
            {
                if (!discardCurrentBlock)
                {
                    finalizeRecord();
                }

                recordLines.Clear();
                currentBlockOpen = false;
                discardCurrentBlock = false;
            }
        }

        if (currentBlockOpen && !discardCurrentBlock)
        {
            recordIndex++;
            records.Add(CreateSkippedRecord(
                sourceDatabaseOrTool,
                $"row-{recordIndex}",
                SearchImportErrorCodes.MalformedRecord,
                "RIS record missing ER end marker.",
                parserWarnings));
        }

        if (records.Count == 0)
        {
            parserWarnings.Add(new SearchImportParserNotice(SearchImportErrorCodes.MalformedRecord, "No parseable RIS records."));
        }

        return records;
    }

    private static SearchImportRecord ParseRisRecord(
        IReadOnlyList<string> rawLines,
        string sourceDatabaseOrTool,
        int recordIndex,
        HashSet<string> seenSourceIds,
        List<SearchImportParserNotice> parserWarnings)
    {
        var recordText = string.Join('\n', rawLines);
        var recordDigest = ContentDigest.Sha256Utf8(recordText).ToString();
        var lineParser = new Regex(@"^(?<tag>[A-Za-z0-9]{2})\s+-\s*(?<value>.*)$");

        var fields = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in rawLines)
        {
            var match = lineParser.Match(line.TrimEnd());
            if (!match.Success)
            {
                continue;
            }

            var tag = match.Groups["tag"].Value;
            var value = match.Groups["value"].Value.Trim();
            if (!fields.TryGetValue(tag, out var values))
            {
                values = new List<string>();
                fields.Add(tag, values);
            }

            values.Add(value);
        }

        if (fields.Count == 0)
        {
            return CreateSkippedRecord(
                sourceDatabaseOrTool,
                $"row-{recordIndex}",
                SearchImportErrorCodes.MalformedRecord,
                "Malformed RIS record body.",
                parserWarnings);
        }

        var sourceRecordId = FirstOrDefault(fields, "ID") ?? $"row-{recordIndex}";
        var notices = new List<SearchImportParserNotice>();

        if (!TryEnsureUniqueSourceRecord(sourceRecordId, seenSourceIds, notices, recordIndex, sourceDatabaseOrTool, parserWarnings))
        {
            return CreateSkippedRecord(
                sourceDatabaseOrTool,
                sourceRecordId,
                SearchImportErrorCodes.DuplicateSourceRecordId,
                notices,
                parserWarnings);
        }

        var title = FirstOrDefault(fields, "TI") ?? FirstOrDefault(fields, "T1") ?? FirstOrDefault(fields, "CT");
        if (string.IsNullOrWhiteSpace(title))
        {
            notices.Add(new SearchImportParserNotice(
                SearchImportErrorCodes.MissingRequiredField,
                "RIS record missing title.",
                recordIndex,
                sourceRecordId));
            parserWarnings.Add(new SearchImportParserNotice(
                SearchImportErrorCodes.MissingRequiredField,
                "RIS record missing title.",
                recordIndex,
                sourceRecordId));
            return CreateSkippedRecord(sourceDatabaseOrTool, sourceRecordId, SearchImportErrorCodes.MissingRequiredField, notices, parserWarnings);
        }

        var rawIdentifiers = new List<string>();
        var sourceIdentifiers = new List<string>();
        var workIds = new HashSet<WorkId>(new WorkIdEqualityComparer());
        AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "doi", FirstOrDefault(fields, "DO"), notices, sourceRecordId, recordIndex, parserWarnings);
        AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "arxiv", FirstOrDefault(fields, "AR"), notices, sourceRecordId, recordIndex, parserWarnings);
        AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "pubmed", FirstOrDefault(fields, "PMID"), notices, sourceRecordId, recordIndex, parserWarnings);
        AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "pmcid", FirstOrDefault(fields, "PMCID"), notices, sourceRecordId, recordIndex, parserWarnings);

        AddSourceSpecificIdentifier("EID", FirstOrDefault(fields, "EID"), sourceIdentifiers, notices, sourceRecordId, recordIndex, parserWarnings);
        AddSourceSpecificIdentifier("ISBN", FirstOrDefault(fields, "ISBN"), sourceIdentifiers, notices, sourceRecordId, recordIndex, parserWarnings);
        AddSourceSpecificIdentifier("SCI", FirstOrDefault(fields, "SCI"), sourceIdentifiers, notices, sourceRecordId, recordIndex, parserWarnings);

        var authors = fields.TryGetValue("AU", out var rawAuthors)
            ? rawAuthors.Where(author => !string.IsNullOrWhiteSpace(author)).Select(author => author.Trim()).Distinct(StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        var year = ParseYear(FirstOrDefault(fields, "PY"), sourceRecordId, recordIndex, parserWarnings);
        var venue = FirstOrDefault(fields, "JO") ?? FirstOrDefault(fields, "JF");
        var abstractText = FirstOrDefault(fields, "AB");
        var keywords = fields.TryGetValue("KW", out var rawKeywords)
            ? rawKeywords.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
            : Array.Empty<string>();

        var rawData = CreateSearchImportRawData(sourceDatabaseOrTool, sourceRecordId, sourceIdentifiers, rawIdentifiers, recordDigest, recordText);
        var work = workIds.Count == 0
            ? ScholarlyWork.UnresolvedCandidate(title, BuildSourceContext(sourceDatabaseOrTool, sourceRecordId), rawData: rawData)
            : ScholarlyWork.Identified(
                title,
                WorkIdSet.From(workIds.ToArray()),
                BuildSourceContext(sourceDatabaseOrTool, sourceRecordId),
                rawData: rawData);

        return new SearchImportRecord(
            sourceDatabaseOrTool,
            sourceRecordId,
            null,
            new ReadOnlyCollection<string>(sourceIdentifiers.Distinct(StringComparer.Ordinal).ToArray()),
            work,
            new ReadOnlyCollection<string>(authors),
            year,
            venue,
            abstractText,
            new ReadOnlyCollection<string>(keywords),
            recordDigest,
            recordText,
            false,
            null,
            new ReadOnlyCollection<SearchImportParserNotice>(notices));
    }

    private static List<SearchImportRecord> ParseBibTex(
        byte[] sourceFileBytes,
        string sourceDatabaseOrTool,
        List<SearchImportParserNotice> parserWarnings)
    {
        var text = Encoding.UTF8.GetString(sourceFileBytes).Replace("\r\n", "\n", StringComparison.Ordinal);
        var entries = ParseBibtexEntries(text);
        var parsed = new List<SearchImportRecord>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var recordIndex = 0;

        foreach (var entry in entries)
        {
            recordIndex++;
            var sourceRecordId = entry.Key;
            if (string.IsNullOrWhiteSpace(sourceRecordId))
            {
                sourceRecordId = $"row-{recordIndex}";
            }

            var notices = new List<SearchImportParserNotice>();
            if (!TryEnsureUniqueSourceRecord(sourceRecordId, seenIds, notices, recordIndex, sourceDatabaseOrTool, parserWarnings))
            {
                parsed.Add(CreateSkippedRecord(
                    sourceDatabaseOrTool,
                    sourceRecordId,
                    SearchImportErrorCodes.DuplicateSourceRecordId,
                    notices,
                    parserWarnings));
                continue;
            }

            var fields = ParseBibtexFields(entry.Body);
            if (fields.Count == 0)
            {
                notices.Add(new SearchImportParserNotice(
                    SearchImportErrorCodes.MalformedRecord,
                    "Malformed BibTeX record body.",
                    recordIndex,
                    sourceRecordId));
                parserWarnings.Add(new SearchImportParserNotice(
                    SearchImportErrorCodes.MalformedRecord,
                    "Malformed BibTeX record body.",
                    recordIndex,
                    sourceRecordId));
                parsed.Add(CreateSkippedRecord(
                    sourceDatabaseOrTool,
                    sourceRecordId,
                    SearchImportErrorCodes.MalformedRecord,
                    notices,
                    parserWarnings));
                continue;
            }

            var title = GetSingle(fields, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                notices.Add(new SearchImportParserNotice(
                    SearchImportErrorCodes.MissingRequiredField,
                    "BibTeX record missing title.",
                    recordIndex,
                    sourceRecordId));
                parserWarnings.Add(new SearchImportParserNotice(
                    SearchImportErrorCodes.MissingRequiredField,
                    "BibTeX record missing title.",
                    recordIndex,
                    sourceRecordId));
                parsed.Add(CreateSkippedRecord(
                    sourceDatabaseOrTool,
                    sourceRecordId,
                    SearchImportErrorCodes.MissingRequiredField,
                    notices,
                    parserWarnings));
                continue;
            }

            var recordText = entry.RawText.Trim();
            var recordDigest = ContentDigest.Sha256Utf8(recordText).ToString();
            var rawIdentifiers = new List<string>();
            var sourceIdentifiers = new List<string>();
            var workIds = new HashSet<WorkId>(new WorkIdEqualityComparer());

            AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "doi", GetSingle(fields, "doi"), notices, sourceRecordId, recordIndex, parserWarnings);
            AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "arxiv", GetSingle(fields, "eprint"), notices, sourceRecordId, recordIndex, parserWarnings);
            AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "pubmed", GetSingle(fields, "pmid"), notices, sourceRecordId, recordIndex, parserWarnings);
            AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "pmcid", GetSingle(fields, "pmcid"), notices, sourceRecordId, recordIndex, parserWarnings);

            AddSourceSpecificIdentifier("isbn", GetSingle(fields, "isbn"), sourceIdentifiers, notices, sourceRecordId, recordIndex, parserWarnings);

            var authors = ParseBibtexAuthorList(GetSingle(fields, "author"));
            var year = ParseYear(GetSingle(fields, "year"), sourceRecordId, recordIndex, parserWarnings);
            var venue = GetSingle(fields, "journal");
            if (string.IsNullOrWhiteSpace(venue))
            {
                venue = GetSingle(fields, "booktitle");
            }

            var abstractText = GetSingle(fields, "abstract");
            if (string.IsNullOrWhiteSpace(abstractText))
            {
                abstractText = GetSingle(fields, "summary");
            }

            var keywords = ParseBibtexKeywords(GetSingle(fields, "keywords"));
            var rawData = CreateSearchImportRawData(
                sourceDatabaseOrTool,
                sourceRecordId,
                sourceIdentifiers,
                rawIdentifiers,
                recordDigest,
                recordText);

            var work = workIds.Count == 0
                ? ScholarlyWork.UnresolvedCandidate(title, BuildSourceContext(sourceDatabaseOrTool, sourceRecordId), rawData: rawData)
                : ScholarlyWork.Identified(
                    title,
                    WorkIdSet.From(workIds.ToArray()),
                    BuildSourceContext(sourceDatabaseOrTool, sourceRecordId),
                    rawData: rawData);

            parsed.Add(new SearchImportRecord(
                sourceDatabaseOrTool,
                sourceRecordId,
                null,
                new ReadOnlyCollection<string>(sourceIdentifiers.Distinct(StringComparer.Ordinal).ToArray()),
                work,
                new ReadOnlyCollection<string>(authors.ToArray()),
                year,
                venue,
                abstractText,
                new ReadOnlyCollection<string>(keywords.ToArray()),
                recordDigest,
                recordText,
                false,
                null,
                new ReadOnlyCollection<SearchImportParserNotice>(notices)));
        }

        if (parsed.Count == 0)
        {
            parserWarnings.Add(new SearchImportParserNotice(SearchImportErrorCodes.MalformedRecord, "No parseable BibTeX records."));
        }

        return parsed;
    }

    private static List<SearchImportRecord> ParseScopusCsv(
        byte[] sourceFileBytes,
        string sourceDatabaseOrTool,
        List<SearchImportParserNotice> parserWarnings)
    {
        var text = Encoding.UTF8.GetString(sourceFileBytes).Replace("\r\n", "\n", StringComparison.Ordinal);
        var csvRecords = ParseCsvRecords(text);
        if (csvRecords.Count == 0 || !csvRecords[0].IsComplete)
        {
            parserWarnings.Add(new SearchImportParserNotice(SearchImportErrorCodes.MalformedRecord, "Scopus CSV contains no header line."));
            return new List<SearchImportRecord>();
        }

        var headers = csvRecords[0].Fields;
        var records = new List<SearchImportRecord>();
        var normalizedHeaders = headers.Select(header => header.Trim().ToLowerInvariant()).ToArray();
        var duplicateHeader = normalizedHeaders
            .GroupBy(header => header, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateHeader is not null)
        {
            var headerLabel = string.IsNullOrWhiteSpace(duplicateHeader) ? "<blank>" : duplicateHeader;
            records.Add(CreateSkippedRecord(
                sourceDatabaseOrTool,
                "row-1",
                SearchImportErrorCodes.MalformedRecord,
                $"Scopus header contains duplicate normalized field '{headerLabel}'.",
                parserWarnings));
            return records;
        }

        var seenSourceIds = new HashSet<string>(StringComparer.Ordinal);
        var row = 0;

        foreach (var csvRecord in csvRecords.Skip(1))
        {
            if (csvRecord.Fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            row++;
            if (!csvRecord.IsComplete)
            {
                records.Add(CreateSkippedRecord(
                    sourceDatabaseOrTool,
                    $"row-{row}",
                    SearchImportErrorCodes.MalformedRecord,
                    "Scopus CSV record contains an unterminated quoted field.",
                    parserWarnings));
                continue;
            }

            var fields = csvRecord.Fields;
            var values = normalizedHeaders.Zip(fields, (header, value) => new { Header = header, Value = value })
                .ToDictionary(item => item.Header, item => item.Value, StringComparer.OrdinalIgnoreCase);

            var sourceRecordId = values.TryGetValue("eid", out var eidValue) && !string.IsNullOrWhiteSpace(eidValue)
                ? eidValue.Trim()
                : $"row-{row}";

            var notices = new List<SearchImportParserNotice>();
            if (!TryEnsureUniqueSourceRecord(sourceRecordId, seenSourceIds, notices, row, sourceDatabaseOrTool, parserWarnings))
            {
                records.Add(CreateSkippedRecord(
                    sourceDatabaseOrTool,
                    sourceRecordId,
                    SearchImportErrorCodes.DuplicateSourceRecordId,
                    notices,
                    parserWarnings));
                continue;
            }

            var title = GetValue(values, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                notices.Add(new SearchImportParserNotice(
                    SearchImportErrorCodes.MissingRequiredField,
                    "Scopus CSV record missing title.",
                    row,
                    sourceRecordId));
                parserWarnings.Add(new SearchImportParserNotice(
                    SearchImportErrorCodes.MissingRequiredField,
                    "Scopus CSV record missing title.",
                    row,
                    sourceRecordId));
                records.Add(CreateSkippedRecord(
                    sourceDatabaseOrTool,
                    sourceRecordId,
                    SearchImportErrorCodes.MissingRequiredField,
                    notices,
                    parserWarnings));
                continue;
            }

            var rawIdentifiers = new List<string>();
            var sourceIdentifiers = new List<string>();
            var workIds = new HashSet<WorkId>(new WorkIdEqualityComparer());
            AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "doi", GetValue(values, "doi"), notices, sourceRecordId, row, parserWarnings);
            AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "pubmed", GetValue(values, "pubmed id"), notices, sourceRecordId, row, parserWarnings);
            AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "pmcid", GetValue(values, "pmcid"), notices, sourceRecordId, row, parserWarnings);
            AddIdentifier(workIds, sourceIdentifiers, rawIdentifiers, "arxiv", GetValue(values, "arxiv id"), notices, sourceRecordId, row, parserWarnings);

            AddSourceSpecificIdentifier("eid", GetValue(values, "eid"), sourceIdentifiers, notices, sourceRecordId, row, parserWarnings);
            AddSourceSpecificIdentifier("wos", GetValue(values, "wos", false), sourceIdentifiers, notices, sourceRecordId, row, parserWarnings);

            var authors = ParseDelimitedAuthorList(GetValue(values, "author names"));
            if (authors.Count == 0)
            {
                authors = ParseDelimitedAuthorList(GetValue(values, "authors"));
            }

            var venue = GetValue(values, "source title");
            if (string.IsNullOrWhiteSpace(venue))
            {
                venue = GetValue(values, "journal");
            }

            var rawText = csvRecord.RawText;
            var recordDigest = ContentDigest.Sha256Utf8(rawText).ToString();
            var year = ParseYear(GetValue(values, "year"), sourceRecordId, row, parserWarnings);
            var abstractText = GetValue(values, "abstract");
            var keywords = ParseKeywords(GetValue(values, "keywords"));

            var rawData = CreateSearchImportRawData(
                sourceDatabaseOrTool,
                sourceRecordId,
                sourceIdentifiers,
                rawIdentifiers,
                recordDigest,
                rawText);

            var work = workIds.Count == 0
                ? ScholarlyWork.UnresolvedCandidate(title, BuildSourceContext(sourceDatabaseOrTool, sourceRecordId), rawData: rawData)
                : ScholarlyWork.Identified(
                    title,
                    WorkIdSet.From(workIds.ToArray()),
                    BuildSourceContext(sourceDatabaseOrTool, sourceRecordId),
                    rawData: rawData);

            records.Add(new SearchImportRecord(
                sourceDatabaseOrTool,
                sourceRecordId,
                null,
                new ReadOnlyCollection<string>(sourceIdentifiers.Distinct(StringComparer.Ordinal).ToArray()),
                work,
                new ReadOnlyCollection<string>(authors.ToArray()),
                year,
                venue,
                abstractText,
                new ReadOnlyCollection<string>(keywords.ToArray()),
                recordDigest,
                rawText,
                false,
                null,
                new ReadOnlyCollection<SearchImportParserNotice>(notices)));
        }

        return records;
    }

    private static SearchImportRecord CreateSkippedRecord(
        string sourceDatabaseOrTool,
        string sourceRecordId,
        string reasonCategory,
        string reasonMessage,
        List<SearchImportParserNotice>? sharedWarnings = null)
    {
        return CreateSkippedRecord(sourceDatabaseOrTool, sourceRecordId, reasonCategory, new[] { new SearchImportParserNotice(reasonCategory, reasonMessage, null, sourceRecordId) }, sharedWarnings);
    }

    private static SearchImportRecord CreateSkippedRecord(
        string sourceDatabaseOrTool,
        string sourceRecordId,
        string reasonCategory,
        IEnumerable<SearchImportParserNotice> notices,
        List<SearchImportParserNotice>? sharedWarnings = null)
    {
        var merged = notices.ToArray();
        if (merged.Length == 0)
        {
            merged = new[] { new SearchImportParserNotice(reasonCategory, "Record skipped.", null, sourceRecordId) };
        }

        var finalNotices = merged
            .Append(new SearchImportParserNotice(SearchImportErrorCodes.SkippedRecord, "Record skipped.", null, sourceRecordId))
            .ToArray();

        if (sharedWarnings is not null)
        {
            sharedWarnings.AddRange(finalNotices);
        }

        return new SearchImportRecord(
            sourceDatabaseOrTool,
            sourceRecordId,
            null,
            Array.Empty<string>(),
            ScholarlyWork.UnresolvedCandidate("Unknown title", BuildSourceContext(sourceDatabaseOrTool, sourceRecordId)),
            Array.Empty<string>(),
            null,
            null,
            null,
            Array.Empty<string>(),
            null,
            null,
            true,
            reasonCategory,
            new ReadOnlyCollection<SearchImportParserNotice>(finalNotices));
    }

    private static bool TryEnsureUniqueSourceRecord(
        string sourceRecordId,
        HashSet<string> seenSourceIds,
        List<SearchImportParserNotice> notices,
        int recordIndex,
        string sourceDatabaseOrTool,
        List<SearchImportParserNotice>? sharedWarnings)
    {
        if (string.IsNullOrWhiteSpace(sourceRecordId))
        {
            sourceRecordId = $"row-{recordIndex}";
        }

        if (seenSourceIds.Contains(sourceRecordId))
        {
            notices.Add(new SearchImportParserNotice(
                SearchImportErrorCodes.DuplicateSourceRecordId,
                "Duplicate source record id.",
                recordIndex,
                sourceRecordId));

            sharedWarnings?.Add(new SearchImportParserNotice(
                SearchImportErrorCodes.DuplicateSourceRecordId,
                "Duplicate source record id.",
                recordIndex,
                sourceRecordId));
            return false;
        }

        seenSourceIds.Add(sourceRecordId);
        return true;
    }

    private static void AddSourceSpecificIdentifier(
        string namespaceName,
        string? value,
        List<string> sourceIdentifiers,
        List<SearchImportParserNotice> notices,
        string sourceRecordId,
        int recordIndex,
        List<SearchImportParserNotice> parserWarnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = $"{namespaceName}:{value.Trim()}";
        sourceIdentifiers.Add(normalized);
        notices.Add(new SearchImportParserNotice(
            SearchImportErrorCodes.UnknownIdentifierType,
            $"Source-specific identifier {normalized} preserved as source evidence.",
            recordIndex,
            sourceRecordId));
        parserWarnings.Add(new SearchImportParserNotice(
            SearchImportErrorCodes.UnknownIdentifierType,
            $"Source-specific identifier {normalized} preserved as source evidence.",
            recordIndex,
            sourceRecordId));
    }

    private static void AddIdentifier(
        HashSet<WorkId> workIds,
        List<string> sourceIdentifiers,
        List<string> rawIdentifiers,
        string namespaceName,
        string? value,
        List<SearchImportParserNotice> notices,
        string sourceRecordId,
        int recordIndex,
        List<SearchImportParserNotice> parserWarnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmedValue = value.Trim();
        rawIdentifiers.Add($"{namespaceName}:{trimmedValue}");
        if (!IsApprovedNamespace(namespaceName))
        {
            notices.Add(new SearchImportParserNotice(SearchImportErrorCodes.UnknownIdentifierType, $"Identifier namespace '{namespaceName}' is not approved.", recordIndex, sourceRecordId));
            parserWarnings.Add(new SearchImportParserNotice(SearchImportErrorCodes.UnknownIdentifierType, $"Identifier namespace '{namespaceName}' is not approved.", recordIndex, sourceRecordId));
            sourceIdentifiers.Add($"{namespaceName}:{trimmedValue}");
            return;
        }

        try
        {
            workIds.Add(WorkId.From(namespaceName, trimmedValue));
        }
        catch (SharedIdentityRuleException)
        {
            notices.Add(new SearchImportParserNotice(SearchImportErrorCodes.UnknownIdentifierType, $"Identifier '{namespaceName}:{trimmedValue}' is not valid.", recordIndex, sourceRecordId));
            parserWarnings.Add(new SearchImportParserNotice(SearchImportErrorCodes.UnknownIdentifierType, $"Identifier '{namespaceName}:{trimmedValue}' is not valid.", recordIndex, sourceRecordId));
            sourceIdentifiers.Add($"{namespaceName}:{trimmedValue}");
        }
    }

    private static bool IsApprovedNamespace(string namespaceName) =>
        WorkIdNamespace.ApprovedNamespaces.Contains(namespaceName, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseBibtexFields(string body)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        while (index < body.Length)
        {
            SkipWhitespaceAndCommas(body, ref index);
            var keyStart = index;
            while (index < body.Length && (char.IsLetterOrDigit(body[index]) || body[index] is '_' or '-'))
            {
                index++;
            }

            if (keyStart == index)
            {
                index++;
                continue;
            }

            var key = body[keyStart..index].ToLowerInvariant();
            while (index < body.Length && char.IsWhiteSpace(body[index]))
            {
                index++;
            }

            if (index >= body.Length || body[index] != '=')
            {
                SkipToNextTopLevelComma(body, ref index);
                continue;
            }

            index++;
            while (index < body.Length && char.IsWhiteSpace(body[index]))
            {
                index++;
            }

            var value = ReadBibtexValue(body, ref index);
            if (!string.IsNullOrWhiteSpace(value))
            {
                fields[key] = NormalizeBibtexValue(value);
            }
        }

        return fields;
    }

    private static IReadOnlyList<BibtexEntry> ParseBibtexEntries(string text)
    {
        var entries = new List<BibtexEntry>();
        var index = 0;
        while (index < text.Length)
        {
            var entryStart = text.IndexOf('@', index);
            if (entryStart < 0)
            {
                break;
            }

            index = entryStart + 1;
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] is '_' or '-'))
            {
                index++;
            }

            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            if (index >= text.Length || text[index] is not ('{' or '('))
            {
                continue;
            }

            var open = text[index];
            var close = open == '{' ? '}' : ')';
            var contentStart = ++index;
            var depth = 1;
            var inQuotes = false;
            var escaped = false;
            while (index < text.Length && depth > 0)
            {
                var character = text[index];
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && character == open)
                {
                    depth++;
                }
                else if (!inQuotes && character == close)
                {
                    depth--;
                }

                index++;
            }

            if (depth != 0)
            {
                break;
            }

            var content = text[contentStart..(index - 1)];
            var separator = FindTopLevelComma(content);
            if (separator < 0)
            {
                continue;
            }

            var key = content[..separator].Trim();
            if (key.Length == 0)
            {
                key = $"row-{entries.Count + 1}";
            }

            entries.Add(new BibtexEntry(key, content[(separator + 1)..], text[entryStart..index]));
        }

        return entries;
    }

    private static int FindTopLevelComma(string value)
    {
        var braceDepth = 0;
        var inQuotes = false;
        var escaped = false;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (escaped)
            {
                escaped = false;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else if (character == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && character == '{')
            {
                braceDepth++;
            }
            else if (!inQuotes && character == '}')
            {
                braceDepth--;
            }
            else if (!inQuotes && braceDepth == 0 && character == ',')
            {
                return index;
            }
        }

        return -1;
    }

    private static string ReadBibtexValue(string body, ref int index)
    {
        if (index >= body.Length)
        {
            return string.Empty;
        }

        if (body[index] == '{')
        {
            return ReadBalancedValue(body, ref index, '{', '}');
        }

        if (body[index] == '"')
        {
            return ReadQuotedValue(body, ref index);
        }

        var start = index;
        while (index < body.Length && body[index] != ',')
        {
            index++;
        }

        return body[start..index].Trim();
    }

    private static string ReadBalancedValue(string body, ref int index, char open, char close)
    {
        var start = ++index;
        var depth = 1;
        while (index < body.Length && depth > 0)
        {
            if (body[index] == open)
            {
                depth++;
            }
            else if (body[index] == close)
            {
                depth--;
            }

            index++;
        }

        return depth == 0 ? body[start..(index - 1)] : body[start..];
    }

    private static string ReadQuotedValue(string body, ref int index)
    {
        var builder = new StringBuilder();
        index++;
        var escaped = false;
        while (index < body.Length)
        {
            var character = body[index++];
            if (escaped)
            {
                builder.Append(character);
                escaped = false;
            }
            else if (character == '\\')
            {
                builder.Append(character);
                escaped = true;
            }
            else if (character == '"')
            {
                break;
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static void SkipWhitespaceAndCommas(string value, ref int index)
    {
        while (index < value.Length && (char.IsWhiteSpace(value[index]) || value[index] == ','))
        {
            index++;
        }
    }

    private static void SkipToNextTopLevelComma(string value, ref int index)
    {
        var depth = 0;
        while (index < value.Length)
        {
            if (value[index] == '{')
            {
                depth++;
            }
            else if (value[index] == '}')
            {
                depth--;
            }
            else if (value[index] == ',' && depth == 0)
            {
                index++;
                return;
            }

            index++;
        }
    }

    private static string NormalizeBibtexValue(string value) =>
        Regex.Replace(value, @"\s+", " ").Trim();

    private static string GetSingle(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) ? value : string.Empty;

    private static string GetValue(Dictionary<string, string> values, string key, bool trim = true)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return string.Empty;
        }

        return trim ? value.Trim() : value;
    }

    private static IReadOnlyList<string> ParseBibtexKeywords(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> ParseKeywords(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<string> ParseBibtexAuthorList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var authors = new List<string>();
        var start = 0;
        var braceDepth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            braceDepth += value[index] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0
            };

            if (braceDepth == 0 && IsBibtexAndSeparator(value, index))
            {
                AddBibtexAuthor(authors, value[start..index]);
                index += 2;
                start = index + 1;
            }
        }

        AddBibtexAuthor(authors, value[start..]);
        return authors;
    }

    private static bool IsBibtexAndSeparator(string value, int index) =>
        index > 0 &&
        index + 3 < value.Length &&
        char.IsWhiteSpace(value[index - 1]) &&
        value.AsSpan(index, 3).Equals("and", StringComparison.OrdinalIgnoreCase) &&
        char.IsWhiteSpace(value[index + 3]);

    private static void AddBibtexAuthor(List<string> authors, string value)
    {
        var author = value.Trim();
        if (author.Length > 0)
        {
            authors.Add(author);
        }
    }

    private static IReadOnlyList<string> ParseDelimitedAuthorList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        if (value.Contains(',', StringComparison.Ordinal))
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int? ParseYear(
        string? value,
        string sourceRecordId,
        int recordIndex,
        List<SearchImportParserNotice> parserWarnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value.AsSpan().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        parserWarnings.Add(new SearchImportParserNotice(
            SearchImportErrorCodes.ParserWarning,
            $"Year '{value}' is not an integer; parsed as null.",
            recordIndex,
            sourceRecordId));
        return null;
    }

    private static IReadOnlyList<CsvRecord> ParseCsvRecords(string text)
    {
        var records = new List<CsvRecord>();
        var values = new List<string>();
        var field = new StringBuilder();
        var rawRecord = new StringBuilder();
        var inQuotes = false;

        void finishField()
        {
            values.Add(field.ToString().Trim());
            field.Clear();
        }

        void finishRecord()
        {
            finishField();
            records.Add(new CsvRecord(values.ToArray(), rawRecord.ToString().TrimEnd('\n'), !inQuotes));
            values.Clear();
            rawRecord.Clear();
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            rawRecord.Append(character);
            if (character == '"')
            {
                if (inQuotes && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    rawRecord.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == ',' && !inQuotes)
            {
                finishField();
                continue;
            }

            if (character == '\n' && !inQuotes)
            {
                finishRecord();
                continue;
            }

            field.Append(character);
        }

        if (field.Length > 0 || values.Count > 0 || rawRecord.Length > 0)
        {
            finishRecord();
        }

        return records;
    }

    private static Dictionary<string, string> CreateSearchImportRawData(
        string sourceDatabaseOrTool,
        string sourceRecordId,
        IReadOnlyList<string> sourceIdentifiers,
        IReadOnlyList<string> rawIdentifiers,
        string digest,
        string rawText)
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["imported_by_type"] = SearchImportMetadata.AcquisitionKindImportedExport,
            ["source_database_or_tool"] = sourceDatabaseOrTool,
            ["source_record_id"] = sourceRecordId,
            ["source_identifiers"] = string.Join("; ", sourceIdentifiers),
            ["raw_identifiers"] = string.Join("; ", rawIdentifiers),
            ["raw_record_digest"] = digest,
            ["raw_record_text"] = rawText,
            ["raw_record_digest_scope"] = "raw-artifact-bytes"
        };
    }

    private static string NormalizeFormat(string format) =>
        format.Trim().ToLowerInvariant().Replace("_", "-", StringComparison.Ordinal);

    private static string BuildSourceContext(string sourceDatabaseOrTool, string sourceRecordId) =>
        $"{sourceDatabaseOrTool}:{sourceRecordId}";

    private static string? FirstOrDefault(Dictionary<string, List<string>> fields, string key) =>
        fields.TryGetValue(key, out var values)
            ? values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            : null;

    private static string StripBibtexEnvelope(string value)
    {
        if ((value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal)) ||
            (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)))
        {
            return value[1..^1];
        }

        return value;
    }

    private sealed class WorkIdEqualityComparer : IEqualityComparer<WorkId>
    {
        public bool Equals(WorkId x, WorkId y) => string.Equals(x.ToString(), y.ToString(), StringComparison.Ordinal);

        public int GetHashCode(WorkId obj) => obj.ToString().GetHashCode(StringComparison.Ordinal);
    }

    private sealed record BibtexEntry(string Key, string Body, string RawText);

    private sealed record CsvRecord(IReadOnlyList<string> Fields, string RawText, bool IsComplete);
}
