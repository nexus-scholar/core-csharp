using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Search;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class SearchImportServiceTests
{
    private const string ImportedBy = "import-operator-1";
    private const string ImportedAt = "2026-06-27T12:00:00Z";
    private const string ParserId = "local-slice-parser";
    private const string ParserVersion = "1.0.0";

    [TestMethod]
    public void Search_import_parse_requires_imported_by()
    {
        var service = new SearchImportService();
        var request = new SearchImportRequest("crossref", "ris", ParserId, ParserVersion, string.Empty, ImportedAt);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => service.Parse(
            "trace-import-required-by",
            request,
            Encoding.UTF8.GetBytes("TY  - JOUR\nTI  - Example\nER  -\n")));

        Assert.AreEqual("ImportedBy", exception.ParamName);
    }

    [TestMethod]
    public void Search_import_parse_requires_imported_at()
    {
        var service = new SearchImportService();
        var request = new SearchImportRequest("crossref", "ris", ParserId, ParserVersion, ImportedBy, string.Empty);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => service.Parse(
            "trace-import-required-at",
            request,
            Encoding.UTF8.GetBytes("TY  - JOUR\nTI  - Example\nER  -\n")));

        Assert.AreEqual("ImportedAt", exception.ParamName);
    }

    [TestMethod]
    public void Search_import_binds_raw_file_digest_from_exact_bytes()
    {
        var request = NewRequest("ris");
        var sourceText = "TY  - JOUR\r\nTI  - Digest Scope Test\r\nPY  - 2026\r\nER  -\r\n";
        var sourceBytes = Encoding.UTF8.GetBytes(sourceText);
        var expectedDigest = ContentDigest.Sha256(sourceBytes).ToString();

        var trace = new SearchImportService().Parse("trace-import-digest", request, sourceBytes);

        Assert.AreEqual(expectedDigest, trace.Metadata.SourceFileDigest);
        Assert.AreEqual(DigestScope.RawArtifactBytes.ToString(), trace.Metadata.SourceFileDigestScope);
        var record = trace.ImportedRecords[0];
        Assert.IsNotNull(record.RawRecordText);
        Assert.AreEqual(record.RawRecordDigest, ContentDigest.Sha256Utf8(record.RawRecordText!).ToString());
        Assert.IsNotNull(trace.ImportedRecords[0].RawRecordDigest);
    }

    [TestMethod]
    public void Search_import_preserves_parser_metadata()
    {
        var request = NewRequest("scopus-csv", "crossref", ImportedBy, ImportedAt);
        var sourceText = "title,year,source title\ntest title,2026,test journal";

        var trace = new SearchImportService().Parse("trace-import-metadata", request, Encoding.UTF8.GetBytes(sourceText));

        Assert.AreEqual("crossref", trace.Metadata.SourceDatabaseOrTool);
        Assert.AreEqual("scopus-csv", trace.Metadata.ExportFormat);
        Assert.AreEqual(ParserId, trace.Metadata.ParserId);
        Assert.AreEqual(ParserVersion, trace.Metadata.ParserVersion);
        Assert.AreEqual(DigestScope.RawArtifactBytes.ToString(), trace.Metadata.SourceFileDigestScope);
        Assert.AreEqual(ImportedBy, trace.Metadata.ImportedBy);
        Assert.AreEqual(ImportedAt, trace.Metadata.ImportedAt);
        Assert.AreEqual(1, trace.Metadata.RecordCount);
        Assert.AreEqual(0, trace.Metadata.ParserWarnings.Count);
    }

    [TestMethod]
    public void Search_import_returns_skipped_records_as_evidence_and_excludes_them_from_sightings()
    {
        var sourceText = string.Join('\n', new[]
        {
            "TY  - JOUR",
            "AU  - Author",
            "ER  -",
            "TY  - JOUR",
            "TI  - Valid",
            "ER  -"
        });
        var trace = new SearchImportService().Parse("trace-import-skipped", NewRequest("ris"), Encoding.UTF8.GetBytes(sourceText));

        Assert.AreEqual(2, trace.ImportedRecords.Count);
        Assert.AreEqual(1, trace.Sightings.Count);
        Assert.IsTrue(trace.ImportedRecords.Any(record => record.IsSkipped));
        Assert.IsTrue(trace.ImportedRecords.Any(record => record.Notices.Any(notice => notice.Category == SearchImportErrorCodes.MissingRequiredField)));
        Assert.IsTrue(trace.ParserWarnings.Any(notice => notice.Category == SearchImportErrorCodes.MissingRequiredField));
        Assert.IsTrue(trace.ParserWarnings.Any(notice => notice.Category == SearchImportErrorCodes.SkippedRecord));
    }

    [TestMethod]
    public void Search_import_preserves_parser_warning_from_invalid_year_data()
    {
        var sourceText = string.Join('\n', new[]
        {
            "TY  - JOUR",
            "TI  - Invalid year",
            "PY  - bad-year",
            "ER  -"
        });
        var trace = new SearchImportService().Parse("trace-import-warning", NewRequest("ris"), Encoding.UTF8.GetBytes(sourceText));
        var record = trace.ImportedRecords[0];

        Assert.IsTrue(trace.ParserWarnings.Any(notice => notice.Category == SearchImportErrorCodes.ParserWarning));
        Assert.IsTrue(record.RawRecordText?.Length > 0);
        Assert.IsNotNull(record.RawRecordText);
        Assert.IsNotNull(record.RawRecordDigest);
    }

    [TestMethod]
    public void Search_import_normalizes_doi_arxiv_and_pubmed_identifiers()
    {
        var sourceText = string.Join('\n', new[]
        {
            "@article{row1,",
            "  title = {Normalized IDs},",
            "  doi = {https://doi.org/10.1000/ABC},",
            "  eprint = {arXiv:2101.12345},",
            "  pmid = {123456},",
            "  year = {2024}",
            "}"
        });
        var trace = new SearchImportService().Parse("trace-import-bib", NewRequest("bibtex"), Encoding.UTF8.GetBytes(sourceText));
        var ids = trace.ImportedRecords[0].Work.WorkIds.Ids.Select(id => id.ToString()).ToArray();
        var normalizedIds = string.Join(",", ids);

        if (!ids.Contains("doi:10.1000/abc"))
        {
            Assert.Fail($"Work ids: {normalizedIds}");
        }

        if (!ids.Contains("arxiv:2101.12345"))
        {
            Assert.Fail($"Work ids: {normalizedIds}");
        }

        if (!ids.Any(id => id.StartsWith("pubmed:", StringComparison.Ordinal)))
        {
            Assert.Fail($"Work ids: {normalizedIds}");
        }
    }

    [TestMethod]
    public void Search_import_keeps_scopus_eid_as_source_identifier_not_work_id()
    {
        var sourceText = string.Join('\n', new[]
        {
            "eid,title,author names,year,source title",
            "2-s2.0-12345,Only Source Evidence,Alpha Beta,2022,Journal Name"
        });
        var trace = new SearchImportService().Parse("trace-import-eid", NewRequest("scopus-csv"), Encoding.UTF8.GetBytes(sourceText));
        var record = trace.ImportedRecords[0];

        Assert.IsFalse(record.Work.HasStableIdentifier);
        Assert.IsTrue(record.SourceIdentifiers.Contains("eid:2-s2.0-12345"));
        Assert.IsFalse(record.Work.WorkIds.Ids.Any(id => id.ToString().StartsWith("eid:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Search_import_title_only_records_are_not_deduplicated_by_search_trace()
    {
        var sourceText = string.Join('\n', new[]
        {
            "title,author names,year,source title",
            "Duplicate Title,Alpha,2020,Journal A",
            "Duplicate Title,Beta,2020,Journal B"
        });
        var trace = new SearchImportService().Parse("trace-import-dupes", NewRequest("scopus-csv"), Encoding.UTF8.GetBytes(sourceText));

        Assert.AreEqual(2, trace.ImportedRecords.Count);
        Assert.AreEqual(2, trace.Sightings.Count);
        Assert.IsTrue(trace.Sightings.All(sighting => !sighting.Work.HasStableIdentifier));
        Assert.AreNotEqual(trace.Sightings[0].Work.SourceContext, trace.Sightings[1].Work.SourceContext);
    }

    [TestMethod]
    public void Search_import_rejects_google_scholar_format_as_unsupported()
    {
        var exception = Assert.ThrowsExactly<SearchRuleException>(() => new SearchImportService().Parse(
            "trace-import-google",
            NewRequest("google-scholar-csv"),
            Encoding.UTF8.GetBytes("anything")));

        Assert.AreEqual(SearchImportErrorCodes.UnsupportedFormat, exception.Category);
    }

    [TestMethod]
    public void Search_import_rejects_malformed_ris_without_record_end_marker()
    {
        var sourceText = string.Join('\n', new[]
        {
            "TY  - JOUR",
            "TI  - Unclosed",
            "PY  - 2020",
            "AU  - Bad"
        });
        var trace = new SearchImportService().Parse("trace-import-malformed", NewRequest("ris"), Encoding.UTF8.GetBytes(sourceText));

        Assert.AreEqual(1, trace.ImportedRecords.Count);
        Assert.IsTrue(trace.ImportedRecords[0].IsSkipped);
        Assert.IsTrue(trace.ImportedRecords[0].SkipReason == SearchImportErrorCodes.MalformedRecord || trace.ImportedRecords[0].Notices.Any(note => note.Category == SearchImportErrorCodes.MalformedRecord));
    }

    [TestMethod]
    public void Ris_author_commas_do_not_split_one_person_into_multiple_authors()
    {
        var sourceText = "TY  - JOUR\nTI  - Author fidelity\nAU  - Smith, John\nAU  - Doe, Jane\nER  -\n";

        var trace = new SearchImportService().Parse("trace-ris-authors", NewRequest("ris"), Encoding.UTF8.GetBytes(sourceText));

        CollectionAssert.AreEqual(new[] { "Smith, John", "Doe, Jane" }, trace.ImportedRecords.Single().Authors.ToArray());
    }

    [TestMethod]
    public void Scopus_csv_supports_quoted_multiline_fields_and_escaped_quotes()
    {
        var sourceText = "eid,title,abstract,year\n2-s2.0-1,\"A \"\"quoted\"\" title\",\"First line\nSecond line\",2024\n";

        var trace = new SearchImportService().Parse("trace-csv-multiline", NewRequest("scopus-csv"), Encoding.UTF8.GetBytes(sourceText));

        Assert.AreEqual(1, trace.ImportedRecords.Count);
        Assert.AreEqual("A \"quoted\" title", trace.ImportedRecords[0].Work.Title);
        Assert.AreEqual("First line\nSecond line", trace.ImportedRecords[0].Abstract);
    }

    [TestMethod]
    public void Bibtex_supports_multiline_fields_and_nested_braces()
    {
        var sourceText = """
            @article{nested2026,
              title = {A {Nested} Title
                Across Lines},
              author = {Smith, John and Doe, Jane},
              abstract = {Evidence with {nested {braces}} preserved},
              year = {2026}
            }
            """;

        var trace = new SearchImportService().Parse("trace-bibtex-nested", NewRequest("bibtex"), Encoding.UTF8.GetBytes(sourceText));
        var record = trace.ImportedRecords.Single();

        Assert.AreEqual("A {Nested} Title Across Lines", record.Work.Title);
        CollectionAssert.AreEqual(new[] { "Smith, John", "Doe, Jane" }, record.Authors.ToArray());
        Assert.AreEqual("Evidence with {nested {braces}} preserved", record.Abstract);
    }

    [TestMethod]
    public void Scopus_csv_preserves_unterminated_quoted_rows_as_skipped_evidence()
    {
        var sourceText = "title,abstract\nBroken,\"unterminated\n";

        var trace = new SearchImportService().Parse("trace-csv-malformed", NewRequest("scopus-csv"), Encoding.UTF8.GetBytes(sourceText));

        Assert.AreEqual(1, trace.ImportedRecords.Count);
        Assert.IsTrue(trace.ImportedRecords[0].IsSkipped);
        Assert.AreEqual(SearchImportErrorCodes.MalformedRecord, trace.ImportedRecords[0].SkipReason);
    }

    [TestMethod]
    public void Deterministic_parser_mutation_corpus_never_crashes_or_loses_trace_identity()
    {
        var seeds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ris"] = "TY  - JOUR\nTI  - Seed title\nAU  - Doe, Jane\nER  -\n",
            ["scopus-csv"] = "title,author names,year\nSeed title,Jane Doe,2026\n",
            ["bibtex"] = "@article{seed,title={Seed title},author={Doe, Jane},year={2026}}"
        };
        foreach (var seed in seeds)
        {
            for (var mutation = 0; mutation < 150; mutation++)
            {
                var bytes = MutateUtf8(seed.Value, mutation);
                var traceId = $"fuzz-{seed.Key}-{mutation:000}";
                try
                {
                    var trace = new SearchImportService().Parse(traceId, NewRequest(seed.Key), bytes);
                    Assert.AreEqual(traceId, trace.TraceId);
                    Assert.AreEqual(ContentDigest.Sha256(bytes).ToString(), trace.Metadata.SourceFileDigest);
                    Assert.IsTrue(trace.ImportedRecords.All(record => record.IsSkipped || !string.IsNullOrWhiteSpace(record.RawRecordDigest)));
                }
                catch (SearchRuleException exception)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Category));
                }
            }
        }
    }

    private static byte[] MutateUtf8(string seed, int mutation)
    {
        var bytes = Encoding.UTF8.GetBytes(seed).ToList();
        var random = new Random(0x51A7 + mutation);
        switch (mutation % 5)
        {
            case 0 when bytes.Count > 0:
                var start = random.Next(bytes.Count);
                bytes.RemoveRange(start, random.Next(1, Math.Min(8, bytes.Count - start) + 1));
                break;
            case 1:
                bytes.Insert(random.Next(bytes.Count + 1), (byte)new[] { '\n', '\r', ',', '"', '{', '}', '\\', 0 }[mutation % 8]);
                break;
            case 2 when bytes.Count > 0:
                bytes[random.Next(bytes.Count)] ^= (byte)(1 << random.Next(8));
                break;
            case 3:
                bytes.AddRange(Encoding.UTF8.GetBytes(new string('x', mutation % 31)));
                break;
            case 4:
                bytes.InsertRange(0, new byte[] { 0xEF, 0xBB, 0xBF });
                break;
        }

        return bytes.ToArray();
    }

    private static SearchImportRequest NewRequest(string format, string sourceDatabase = "crossref", string importedBy = ImportedBy, string importedAt = ImportedAt) =>
        new(sourceDatabase, format, ParserId, ParserVersion, importedBy, importedAt);
}
