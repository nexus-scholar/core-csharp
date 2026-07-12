using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Search;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class SearchFixtureTests
{
    private static readonly string FixtureDirectory =
        Path.Combine(AppContext.BaseDirectory, "fixtures", "search");

    private static readonly string[] ExpectedFixtureFiles =
    {
        "search-query-validation.json",
        "search-cache-key-provider-order.json",
        "search-cache-key-field-inclusion.json",
        "search-cache-key-field-exclusion.json",
        "search-cache-key-active-provider-set.json",
        "search-cache-key-include-raw-data-included.json",
        "search-provider-selection-all.json",
        "search-provider-selection-subset.json",
        "search-provider-selection-unknown-alias.json",
        "search-provider-partial-failure.json",
        "search-provider-all-failed-empty.json",
        "search-trace-schema-closed-plan.json",
        "search-trace-php-legacy-plan-import.json",
        "search-trace-raw-provider-results.json",
        "search-trace-duplicate-provider-sightings.json",
        "search-trace-no-id-candidates.json",
        "search-trace-raw-data-preserved.json",
        "search-trace-raw-data-not-requested.json",
        "search-trace-dedup-not-applied.json",
        "search-import-ris-trace.json",
        "search-import-bibtex-trace.json",
        "search-import-scopus-csv-trace.json",
        "search-import-realistic-parser-forms.json",
        "search-import-source-file-digest.json",
        "search-import-parser-warning.json",
        "search-import-no-id-candidates.json",
        "search-import-dedup-not-applied.json",
        "search-import-source-specific-id-not-workid.json",
        "search-import-google-scholar-scraping-rejected.json"
    };

    [TestMethod]
    public void Search_fixtures_are_present()
    {
        Directory.CreateDirectory(FixtureDirectory);
        var files = Directory.EnumerateFiles(FixtureDirectory, "*.json")
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedFile in ExpectedFixtureFiles)
        {
            Assert.IsTrue(files.Contains(expectedFile), $"Missing fixture '{expectedFile}'.");
        }
    }

    [TestMethod]
    public void Search_import_source_file_digest_fixture_matches_exact_bytes_and_scope()
    {
        using var document = LoadFixture("search-import-source-file-digest.json");
        var @case = document.RootElement.GetProperty("case");
        var request = ReadImportRequest(@case.GetProperty("request"));
        var sourceBytes = Encoding.UTF8.GetBytes(@case.GetProperty("sourceFileText").GetString() ?? string.Empty);
        var expected = @case.GetProperty("expected");

        var trace = new SearchImportService().Parse("trace-import-digest-fixture", request, sourceBytes);

        Assert.AreEqual(expected.GetProperty("expectedSourceFileDigest").GetString(), trace.Metadata.SourceFileDigest);
        Assert.AreEqual(expected.GetProperty("sourceFileDigestScope").GetString(), trace.Metadata.SourceFileDigestScope);
        Assert.AreEqual(DigestScope.RawArtifactBytes.ToString(), trace.Metadata.SourceFileDigestScope);
    }

    [TestMethod]
    public void Search_import_realistic_parser_forms_replay_without_field_loss()
    {
        using var document = LoadFixture("search-import-realistic-parser-forms.json");
        foreach (var entry in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var request = ReadImportRequest(entry.GetProperty("request"));
            var trace = new SearchImportService().Parse(
                $"trace-{entry.GetProperty("caseId").GetString()}",
                request,
                Encoding.UTF8.GetBytes(entry.GetProperty("sourceFileText").GetString()!));
            var record = trace.ImportedRecords.Single();
            var expected = entry.GetProperty("expected");

            Assert.IsFalse(record.IsSkipped);
            Assert.AreEqual(expected.GetProperty("title").GetString(), record.Work.Title);
            Assert.AreEqual(expected.GetProperty("authorCount").GetInt32(), record.Authors.Count);
            if (expected.TryGetProperty("abstract", out var abstractText))
            {
                Assert.AreEqual(abstractText.GetString(), record.Abstract);
            }
        }
    }

    [TestMethod]
    public void Search_query_validation_fixture_rejects_invalid_terms_and_year_ranges()
    {
        using var document = LoadFixture("search-query-validation.json");
        var root = document.RootElement;
        var @case = root.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var service = NewService();

        foreach (var entry in @case.GetProperty("invalidCases").EnumerateArray())
        {
            var errorCategory = entry.GetProperty("errorCategory").GetString();
            var input = ReadInput(entry, Array.Empty<string>());
            var exception = Assert.ThrowsExactly<SearchRuleException>(() => service.Execute($"trace-invalid-{entry.GetProperty("query").GetString()}", input, validationYear));
            Assert.AreEqual(errorCategory, exception.Category, $"Mismatch for query '{entry.GetProperty("query").GetString()}'.");
        }
    }

    [TestMethod]
    public void Search_cache_key_is_provider_order_insensitive()
    {
        using var document = LoadFixture("search-cache-key-provider-order.json");
        var root = document.RootElement;
        var @case = root.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();

        var first = SearchCacheIdentity.Compute(ReadInput(@case.GetProperty("requestA"), Array.Empty<string>()), validationYear, ReadAliases(@case.GetProperty("requestA"), "selectedAliases"));
        var second = SearchCacheIdentity.Compute(ReadInput(@case.GetProperty("requestB"), Array.Empty<string>()), validationYear, ReadAliases(@case.GetProperty("requestB"), "selectedAliases"));
        Assert.AreEqual(first.CacheKey, second.CacheKey);
    }

    [TestMethod]
    public void Search_cache_identity_includes_required_fields_and_excludes_projection_fields()
    {
        using var document = LoadFixture("search-cache-key-field-inclusion.json");
        var root = document.RootElement;
        var @case = root.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());
        var activeAliases = ReadAliases(@case.GetProperty("input"), "activeProviderAliases");
        var cacheIdentity = SearchCacheIdentity.Compute(input, validationYear, activeAliases);

        foreach (var expected in @case.GetProperty("expectedIncluded").EnumerateArray())
        {
            var value = expected.GetString();
            Assert.IsTrue(cacheIdentity.IncludedFields.Contains(value));
        }

        foreach (var expected in @case.GetProperty("expectedExcluded").EnumerateArray())
        {
            var value = expected.GetString();
            Assert.IsTrue(cacheIdentity.ExcludedFields.Contains(value));
        }
    }

    [TestMethod]
    public void Search_cache_key_uses_sorted_active_provider_aliases()
    {
        using var document = LoadFixture("search-cache-key-active-provider-set.json");
        var root = document.RootElement;
        var @case = root.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());

        var aliasOrderA = ReadStringArray(@case.GetProperty("activeAliasesA"));
        var aliasOrderB = ReadStringArray(@case.GetProperty("activeAliasesB"));

        var first = SearchCacheIdentity.Compute(input, validationYear, aliasOrderA);
        var second = SearchCacheIdentity.Compute(input, validationYear, aliasOrderB);
        var expectedMaterial = new[] { "crossref", "openalex", "semantic_scholar" };

        Assert.AreEqual(first.CacheKey, second.CacheKey);
        var parsedTraceMaterial = JsonDocument.Parse(first.TraceMaterial).RootElement;
        var serializedAliases = parsedTraceMaterial.GetProperty("active_provider_aliases").EnumerateArray().Select(item => item.GetString()).ToArray();
        CollectionAssert.AreEqual(expectedMaterial, serializedAliases);
    }

    [TestMethod]
    public void Search_cache_key_includes_raw_data_flag()
    {
        using var document = LoadFixture("search-cache-key-include-raw-data-included.json");
        var @case = document.RootElement.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();

        var withRaw = ReadInput(@case.GetProperty("withRawData"), Array.Empty<string>());
        var withoutRaw = ReadInput(@case.GetProperty("withoutRawData"), Array.Empty<string>());
        var activeAliases = ReadAliases(@case.GetProperty("withRawData"), "selectedAliases");

        var withRawIdentity = SearchCacheIdentity.Compute(withRaw, validationYear, activeAliases);
        var withoutRawIdentity = SearchCacheIdentity.Compute(withoutRaw, validationYear, activeAliases);

        Assert.AreNotEqual(withRawIdentity.CacheKey, withoutRawIdentity.CacheKey);
        Assert.AreNotEqual(withRawIdentity.TraceMaterial, withoutRawIdentity.TraceMaterial);
    }

    [TestMethod]
    public void Search_all_active_provider_selection_runs_default_registration_order()
    {
        using var document = LoadFixture("search-provider-selection-all.json");
        var @case = document.RootElement.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var service = NewService();

        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());
        var trace = service.Execute("trace-all", input, validationYear);

        var expectedAliases = ReadStringArray(@case.GetProperty("expectedAliases"));
        CollectionAssert.AreEqual(expectedAliases, trace.ProviderAttempts.Select(attempt => attempt.ProviderAlias).ToArray());
    }

    [TestMethod]
    public void Search_subset_provider_selection_preserves_registration_order()
    {
        using var document = LoadFixture("search-provider-selection-subset.json");
        var @case = document.RootElement.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var service = NewService();

        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());
        var trace = service.Execute("trace-subset", input, validationYear);

        var expectedAliases = ReadStringArray(@case.GetProperty("expectedAliases"));
        CollectionAssert.AreEqual(expectedAliases, trace.ProviderAttempts.Select(attempt => attempt.ProviderAlias).ToArray());
    }

    [TestMethod]
    public void Search_rejects_unknown_provider_alias_before_execution()
    {
        using var document = LoadFixture("search-provider-selection-unknown-alias.json");
        var @case = document.RootElement.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var service = NewService();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());

        var exception = Assert.ThrowsExactly<SearchRuleException>(() => service.Execute("trace-unknown", input, validationYear));
        Assert.AreEqual(SearchErrorCodes.UnknownProviderAlias, exception.Category);
    }

    [TestMethod]
    public void Search_partial_provider_failure_is_preserved_as_attempt_failure()
    {
        using var document = LoadFixture("search-provider-partial-failure.json");
        var @case = document.RootElement.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var service = NewService();

        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());
        var trace = service.Execute("trace-partial", input, validationYear);

        Assert.AreEqual(2, trace.ProviderAttempts.Count);
        Assert.AreEqual(1, trace.ProviderAttempts.Count(attempt => attempt.Status == "success"));
        Assert.AreEqual(1, trace.ProviderAttempts.Count(attempt => attempt.Status == "failure"));
        Assert.IsFalse(trace.Summary.AllFailed);
        Assert.IsTrue(trace.ProviderAttempts.Any(attempt => attempt.SkipReason is null && attempt.ResultCount > 0));
    }

    [TestMethod]
    public void Search_all_selected_providers_can_fail_with_empty_trace()
    {
        using var document = LoadFixture("search-provider-all-failed-empty.json");
        var @case = document.RootElement.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var service = NewService();

        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());
        var trace = service.Execute("trace-all-failed", input, validationYear);

        Assert.AreEqual(@case.GetProperty("expectedAttemptCount").GetInt32(), trace.ProviderAttempts.Count);
        Assert.IsTrue(trace.Summary.AllFailed);
        Assert.AreEqual(0, trace.Sightings.Count);
    }

    [TestMethod]
    public void Search_trace_parser_for_schema_closed_plan()
    {
        using var document = LoadFixture("search-trace-schema-closed-plan.json");
        var @case = document.RootElement.GetProperty("case");
        var plan = @case.GetProperty("plan").GetRawText();
        var parsed = SearchPlanParser.ParseSchemaClosed(plan);

        Assert.AreEqual(SearchPlanSource.SchemaClosed, parsed.Source);
        Assert.AreEqual(SearchErrorCodes.KnownPlanSchemaId, parsed.SchemaId);

        var service = NewService();
        var item = parsed.Items[0];
        var input = new SearchQueryInput(
            item.Query,
            item.YearFrom,
            item.YearTo,
            parsed.Language,
            item.MaxResults,
            0,
            item.IncludeRawData,
            item.Providers,
            new SearchPlanBinding("plan-1", item.SourceIndex.ToString(), parsed.SchemaId, parsed.SchemaVersion, parsed.ProjectId));
        var trace = service.Execute("trace-closed-plan", input, @case.GetProperty("validationYear").GetInt32());

        Assert.AreEqual(trace.Request.Query, item.Query);
        Assert.IsTrue(trace.ProviderAttempts.Count > 0);
        Assert.IsTrue(trace.CacheIdentity.Algorithm == SearchCacheIdentity.AlgorithmId);
    }

    [TestMethod]
    public void Search_trace_parser_for_legacy_php_plan_import()
    {
        using var document = LoadFixture("search-trace-php-legacy-plan-import.json");
        var @case = document.RootElement.GetProperty("case");
        var plan = @case.GetProperty("plan").GetRawText();
        var parsed = SearchPlanParser.ParseLegacyImport(plan);

        Assert.AreEqual(SearchPlanSource.PhpLegacyImport, parsed.Source);
        Assert.IsTrue(parsed.Items.Count > 0);

        var service = NewService();
        var item = parsed.Items[0];
        var input = new SearchQueryInput(
            item.Query,
            item.YearFrom,
            item.YearTo,
            parsed.Language,
            item.MaxResults,
            0,
            item.IncludeRawData,
            item.Providers,
            new SearchPlanBinding("plan-legacy", item.SourceIndex.ToString(), parsed.SchemaId, parsed.SchemaVersion, parsed.ProjectId));

        var trace = service.Execute("trace-legacy-plan", input, @case.GetProperty("validationYear").GetInt32());
        Assert.AreEqual(item.Query, trace.Request.Query);
        Assert.IsTrue(trace.ProviderAttempts.Count > 0);
        Assert.IsNotNull(trace.Request.PlanBinding);
    }

    [TestMethod]
    public void Search_trace_preserves_raw_provider_payload_when_requested()
    {
        using var document = LoadFixture("search-trace-raw-provider-results.json");
        var @case = document.RootElement.GetProperty("case");
        var service = NewService();
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());

        var trace = service.Execute("trace-raw-requested", input, validationYear);
        Assert.IsTrue(trace.Sightings.Count > 0);
        Assert.IsTrue(trace.Sightings.All(sighting => sighting.Work.RawData.ContainsKey("raw_provider_payload")));
    }

    [TestMethod]
    public void Search_trace_preserves_duplicate_provider_sightings()
    {
        using var document = LoadFixture("search-trace-duplicate-provider-sightings.json");
        var @case = document.RootElement.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var service = NewService();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());
        var trace = service.Execute("trace-dup", input, validationYear);

        var duplicateWorkId = @case.GetProperty("expectedDuplicateWorkId").GetString();
        var duplicateCount = @case.GetProperty("expectedDuplicateCount").GetInt32();
        Assert.AreEqual(duplicateCount, trace.Sightings.Count(sighting => sighting.ProviderWorkId == duplicateWorkId));
        Assert.IsTrue(trace.Sightings.Count >= @case.GetProperty("expectedMinSightings").GetInt32());
    }

    [TestMethod]
    public void Search_trace_keeps_no_id_candidates_without_primary_identity()
    {
        using var document = LoadFixture("search-trace-no-id-candidates.json");
        var @case = document.RootElement.GetProperty("case");
        var service = NewService();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());
        var trace = service.Execute("trace-noid", input, @case.GetProperty("validationYear").GetInt32());

        Assert.AreEqual(1, trace.Sightings.Count);
        Assert.IsNull(trace.Sightings[0].ProviderWorkId);
        Assert.AreEqual(@case.GetProperty("expectedSourceContext").GetString(), trace.Sightings[0].Work.SourceContext);
    }

    [TestMethod]
    public void Search_trace_keeps_raw_payload_when_requested_for_provider()
    {
        using var document = LoadFixture("search-trace-raw-data-preserved.json");
        var @case = document.RootElement.GetProperty("case");
        var service = NewService();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());

        var trace = service.Execute("trace-raw-data-preserved", input, @case.GetProperty("validationYear").GetInt32());
        Assert.IsTrue(trace.Sightings.All(sighting => sighting.Work.RawData.Count > 0));
    }

    [TestMethod]
    public void Search_trace_omits_raw_payload_when_not_requested()
    {
        using var document = LoadFixture("search-trace-raw-data-not-requested.json");
        var @case = document.RootElement.GetProperty("case");
        var service = NewService();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());

        var trace = service.Execute("trace-raw-data-not-requested", input, @case.GetProperty("validationYear").GetInt32());
        Assert.IsTrue(trace.Sightings.All(sighting => sighting.Work.RawData.Count == 0));
    }

    [TestMethod]
    public void Search_trace_does_not_apply_deduplication()
    {
        using var document = LoadFixture("search-trace-dedup-not-applied.json");
        var @case = document.RootElement.GetProperty("case");
        var validationYear = @case.GetProperty("validationYear").GetInt32();
        var service = NewService();
        var input = ReadInput(@case.GetProperty("input"), Array.Empty<string>());
        var trace = service.Execute("trace-no-dedup", input, validationYear);

        var expectedWorkId = @case.GetProperty("expectedDuplicateWorkId").GetString();
        var duplicateCount = trace.Sightings.Count(sighting => sighting.ProviderWorkId == expectedWorkId);
        Assert.IsTrue(duplicateCount >= 2, $"Expected duplicate sightings for {expectedWorkId}.");
        Assert.AreEqual(@case.GetProperty("expectedMinSightings").GetInt32(), trace.Sightings.Count);
    }

    private static SearchQueryInput ReadInput(JsonElement root, IEnumerable<string> defaultAliases)
    {
        var selected = root.TryGetProperty("selectedAliases", out var aliasesElement)
            ? ReadAliases(root, "selectedAliases")
            : defaultAliases;

        return new SearchQueryInput(
            root.GetProperty("query").GetString() ?? string.Empty,
            ReadNullableInt(root, "yearFrom"),
            ReadNullableInt(root, "yearTo"),
            root.TryGetProperty("language", out var language) ? language.GetString() : null,
            root.GetProperty("maxResults").GetInt32(),
            root.GetProperty("offset").GetInt32(),
            root.GetProperty("includeRawData").GetBoolean(),
            selected.ToArray());
    }

    private static SearchImportRequest ReadImportRequest(JsonElement root)
    {
        return new SearchImportRequest(
            root.GetProperty("sourceDatabaseOrTool").GetString() ?? string.Empty,
            root.GetProperty("exportFormat").GetString() ?? string.Empty,
            root.GetProperty("parserId").GetString() ?? string.Empty,
            root.GetProperty("parserVersion").GetString() ?? string.Empty,
            root.GetProperty("importedBy").GetString() ?? string.Empty,
            root.GetProperty("importedAt").GetString() ?? string.Empty,
            root.TryGetProperty("originalQueryText", out var originalQueryText) ? originalQueryText.GetString() : null,
            root.TryGetProperty("exportedAt", out var exportedAt) ? exportedAt.GetString() : null);
    }

    private static int? ReadNullableInt(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetInt32()
            : null;
    }

    private static string[] ReadAliases(JsonElement parent, string propertyName)
    {
        return parent.GetProperty(propertyName).EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
    }

    private static string[] ReadStringArray(JsonElement root)
    {
        return root.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();
    }

    private static JsonDocument LoadFixture(string fileName)
    {
        var path = Path.Combine(FixtureDirectory, fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static SearchService NewService() => new SearchService(SearchProviderCatalog.DefaultProviders());
}
