# PHP Search Behavior Map

Status: reconnaissance and planning only. No C# Search behavior is implemented by this document.

Pinned PHP source:

- Repository: `../core`
- Locked commit: `b24d0d71ec7b64003465182477e7edb7f49994f4`
- Lock file: `specs/SOURCE.lock.json`
- Local PHP checkout note: the inspected checkout matched the locked commit, but had pre-existing dirty `composer.json` and `composer.lock` changes. This reconnaissance did not edit the PHP repo.

## Scope Boundary

This file maps observable PHP Search behavior from the pinned PHP reference. It does not claim PHP compatibility, generate PHP fixtures, introduce providers or network behavior in C#, or implement Search.

Search in PHP currently spans:

- request modeling: `SearchQuery`, `SearchTerm`, `YearRange`
- provider selection and provider execution
- provider response normalization into shared `ScholarlyWork`
- cache identity and versioned cache storage
- YAML search plans
- persistent search-run recording
- provider VCR cassette tests

## 1. Search Query And Request Shape

PHP single-search entry point:

- `SearchAcrossProviders`
- fields: `query`, `projectId`, `maxResults`, `yearFrom`, `yearTo`, `providerAliases`, `includeRawData`
- constructs a `SearchQuery`

`SearchQuery` fields:

- `id`
- `term`
- `projectId`
- `yearRange`
- `language`
- `maxResults`
- `offset`
- `includeRawData`
- `providerAliases`

`SearchQuery` generates `id` when absent as `Q` plus 10 hexadecimal characters from `random_bytes(5)`. Tests explicitly reject `uniqid()` shape.

`SearchTerm` rejects values with fewer than two non-whitespace characters after trimming, but stores the original input value. Direct query construction can therefore preserve surrounding whitespace in cache-key material. The YAML plan parser trims query strings before constructing commands.

`YearRange` accepts unbounded ranges, one-sided ranges, and bounded ranges. It rejects years below `1000`, years later than current year plus five, and inverted ranges. The PHP upper bound depends on runtime `date('Y')`; C# implementation must avoid nondeterministic fixture behavior by pinning the comparison clock or fixture generation date.

## 2. Search Plan Shape

PHP search plans are YAML documents parsed by `YamlSearchPlanParser`.

Root fields:

- `project` or `project_id`; default `default-project`
- `searches` or legacy `queries`
- `providers`; root default list, array or comma-separated string
- `include_raw_data`; root default
- `version`; present in fixtures but not semantically enforced by the parser

Item fields:

- `id`
- `label`; default item id
- `query` or legacy `text`
- `project` or `project_id`; overrides root
- `limit` or `max_results`; default `50`
- `year_from` or `year_min`
- `year_to` or `year_max`
- `providers`; overrides root providers
- `include_raw_data`; overrides root flag
- `metadata`; must be a mapping
- `priority`; item field or `metadata.priority`
- `include_title_abstract`
- `exclude_title_abstract`
- `sourceIndex`; derived from item order

Plan selection behavior:

- `onlyIds` filters by explicit item ids and throws on missing requested ids.
- `priority` filters after id selection.
- runtime overrides can replace project id, max results, and provider aliases.
- `continueOnFailure` defaults to true for plan runs.

Unknown root or item fields are currently ignored unless they are routed through `metadata`. This is permissive PHP behavior and needs a C# decision before implementation because prior local gates prefer schema closure for audit artifacts.

## 3. Provider Aliases And Selection

Default provider aliases from `ProviderConfigRegistry`:

- `openalex`
- `crossref`
- `semantic_scholar`
- `arxiv`
- `pubmed`
- `ieee`
- `doaj`

Default provider rates and enablement:

| Alias | Base URL | Default rate | Enabled by default |
| --- | --- | ---: | --- |
| `openalex` | `https://api.openalex.org` | 10/sec | yes |
| `crossref` | `https://api.crossref.org` | 15/sec | yes |
| `semantic_scholar` | `https://api.semanticscholar.org` | 1/sec without key, 10/sec with key | yes |
| `arxiv` | `https://export.arxiv.org/api` | 3/sec | yes |
| `pubmed` | `https://eutils.ncbi.nlm.nih.gov/entrez/eutils` | 3/sec without key, 10/sec with key | yes |
| `ieee` | `https://ieeexploreapi.ieee.org/api/v1` | 1/sec | only with key |
| `doaj` | `https://doaj.org/api` | 5/sec | yes |

Query provider aliases are normalized by lowercasing, trimming, dropping empty aliases, and deduplicating exact aliases while preserving first-seen order on the query object.

`AdapterCollection::matching([])` returns all registered adapters in registration order. A non-empty alias selection validates unknown aliases before execution and returns matching adapters in registration order, not query alias order.

Unknown selected aliases throw `UnknownProviderAlias` before cache lookup and before provider execution.

## 4. Search Result Record Shape

PHP provider adapters normalize raw provider responses into `ScholarlyWork` records.

Common normalized fields:

- stable identifiers as `WorkIdSet`
- title, defaulting to `Unknown Title` in most adapters when provider title is absent
- source provider alias
- year
- authors
- venue
- abstract where available
- cited-by count where available
- retraction flag where available
- raw provider data only when `includeRawData` is true

Aggregation returns `AggregatedResult`:

- `corpus`
- `providerStats`
- `totalRaw`
- `fromCache`
- `durationMs`

Provider stats use:

- `alias`
- `resultCount`
- `latencyMs`
- `skipReason`

Provider execution returns one `ProviderSearchResult` per provider, success or failure. `ProviderSearchExecutionResult::works()` concatenates successful provider works in provider execution result order.

## 5. Identifier Extraction And Shared Identity

Provider identifier extraction:

| Provider | Extracted namespaces |
| --- | --- |
| `openalex` | `doi`, `openalex`, `pubmed`, `arxiv` |
| `semantic_scholar` | `s2`, `doi`, `arxiv`, `pubmed` |
| `crossref` | `doi` |
| `arxiv` | `arxiv` |
| `pubmed` | `pubmed`, `doi` |
| `ieee` | `doi`, `ieee` |
| `doaj` | `doi`, `doaj` |

PHP Search uses the shared `ScholarlyWork`, `WorkId`, `WorkIdSet`, and `CorpusSlice` model. In PHP aggregation, raw provider works are converted to `CorpusSlice::fromWorks(...)`, then passed through `DeduplicationPort`, and the cached payload stores the deduplicated corpus.

C# Gate 9 shared identity deliberately does not resolve Search, Deduplication, or Screening. C# Search must not silently reproduce PHP's Search-time deduplication unless a later accepted Search/Deduplication decision explicitly authorizes that boundary.

## 6. Pagination And Cursor Behavior

Provider pagination behavior:

| Provider | Pagination behavior |
| --- | --- |
| `openalex` | `per-page = min(maxResults, 200)`, `page = floor(offset / perPage) + 1` |
| `crossref` | `rows = maxResults`, `offset = offset` |
| `semantic_scholar` | bulk endpoint with continuation `token`; offset is not used directly |
| `arxiv` | `start = offset`, `max_results = maxResults` |
| `pubmed` | `esearch` with `retmax = min(maxResults, 10000)`, then `efetch` batches of 200 |
| `ieee` | `start_record = offset + 1`, `max_records = min(maxResults, 200)` |
| `doaj` | `pageSize = min(maxResults, 100)`, `page = floor(offset / pageSize) + 1` |

`SearchQuery::nextPage()` increments offset by `maxResults`.

Semantic Scholar can return partial collected results if a later cursor page fails with `ProviderUnavailable`.

## 7. Search Cache Key Behavior

`SearchQuery::cacheKey()` is the authoritative PHP cache key function.

Cache identity material is:

1. raw stored search term value
2. year range `from`, empty when null
3. year range `to`, empty when null
4. language value, empty when null
5. `maxResults`
6. `offset`
7. comma-joined sorted provider aliases

The material is joined with `|` and hashed with SHA-256.

When aggregation executes, it first resolves active adapters, sorts the active adapter aliases, and passes that sorted list to `cacheKey(...)`. This means an empty requested-provider list is not cached as empty providers; it is cached against the sorted set of active adapters.

## 8. Provider-Order Sensitivity

PHP cache keys are provider-order-insensitive.

Examples verified by unit tests:

- `['crossref', 'openalex']` and `['openalex', 'crossref']` produce the same key.
- normalized query aliases such as `[' OpenAlex ', 'arxiv', 'openalex', '']` become `['openalex', 'arxiv']`, and default cache-key calculation sorts those aliases before hashing.

Execution order remains provider-registration order. Cache identity and execution order are separate concepts.

## 9. Fields Included Or Excluded From Cache Identity

Included:

- search term value
- year range from/to
- language
- max results
- offset
- sorted active provider aliases

Excluded:

- generated query id
- project id
- include-raw-data flag
- provider execution mode
- cache version prefix
- rate-limit settings
- provider API keys
- provider base URLs
- persistence status
- run duration
- provider stats

The exclusion of `includeRawData` is a risk: two requests that differ only by raw-data preservation map to the same cache identity in PHP.

## 10. Error Handling And Partial Provider Failure

Provider-level failures are captured in `ProviderStat.skipReason` and do not stop remaining providers. If all providers fail, aggregation returns an empty corpus with provider stats.

Unknown selected provider aliases fail before provider execution and before cache lookup.

Project locks are checked in both `SearchAcrossProvidersHandler` and `PersistentSearchRunner`; locked projects reject search mutation before execution or recording.

Search plan runs default to continuing after item failure. When `continueOnFailure` is false, the first item failure is rethrown.

## 11. Rate-Limit And Retry Behavior

`BaseProviderAdapter::request(...)`:

- calls `RateLimiterPort::waitForToken()` before each HTTP request
- retries provider-unavailable exceptions
- retries HTTP rate-limit and server errors
- does not retry HTTP 401, 403, or 404
- uses exponential backoff 1s, 2s, 4s with random 0-1s jitter
- throws `ProviderUnavailable` after configured retries are exhausted

Default `ProviderConfig.maxRetries` is 3 and must be at least 1.

This is provider/network behavior and is not part of the current C# reconnaissance implementation.

## 12. Provider Response Normalization

Observed provider normalization rules:

- OpenAlex reconstructs abstracts from `abstract_inverted_index`, parses OpenAlex author names as first-token given and remainder family, and maps venue from `primary_location.source`.
- Semantic Scholar translates boolean terms for bulk search (`AND` to `+`, `OR` to `|`, `NOT` to `-`) and extracts `paperId`, `externalIds`, title, abstract, year, venue, authors, and citation count.
- Crossref reads the first title entry, publication year from date-parts, first ISSN/container-title venue, DOI, authors, and cited-by count.
- arXiv parses Atom XML, normalizes versioned arXiv URL ids by dropping version suffixes, filters normalized works by year after XML parsing, and uses venue `arXiv`.
- PubMed uses `esearch` then `efetch`, extracts PMID and DOI from XML, and skips articles without titles.
- IEEE requires an API key, sorts by publication year descending in the request, extracts DOI and article number, authors from nested `authors.authors`, and cited count.
- DOAJ escapes Lucene special characters in path search text, extracts DOI from `bibjson.identifier`, record id from root `id`, and journal venue from `bibjson.journal`.

Provider adapters preserve raw provider payload only when `SearchQuery.includeRawData` is true.

## 13. Sort And Order Stability

PHP does not apply a cross-provider sort after provider execution.

Order sources:

- adapter selection order follows registration order
- sequential execution returns provider results in selected adapter order
- concurrent execution appends async results when flushed, then synchronous fallback results; tests assert this ordering
- within each provider, result order follows provider response order, except provider request parameters can influence order, such as IEEE sorting by publication year descending
- aggregation concatenates provider works in provider-result order before deduplication
- plan items preserve YAML item order after filters and overrides

C# comparators should not invent a global relevance sort unless a later Search ADR defines one.

## 14. Duplicate Result Handling Before Deduplication

PHP Search aggregation currently deduplicates before returning and before caching by calling `DeduplicationPort`.

This conflicts with the C# porting boundary requested for Search:

- Search must not perform Deduplication.
- Search must preserve raw provider result traces before any later deduplication stage.
- Deduplication compatibility remains a separate future track.

The C# Search implementation therefore needs a Search result/trace representation that can preserve duplicates and provider sightings without collapsing them through `CorpusSlice` identity rules.

## 15. Raw Result Preservation

PHP has two raw-preservation layers:

- adapter-level `rawData` on normalized `ScholarlyWork`, included only when `includeRawData` is true
- VCR cassettes under `tests/Fixture/vcr_cassettes`, which preserve HTTP request/response bodies for provider tests

PHP cache payload stores `ScholarlyWorkDto` arrays. If `includeRawData` was true, raw data may be present in cached work DTOs. Because `includeRawData` is excluded from cache identity, a cached non-raw response can satisfy a later raw request, or a cached raw response can satisfy a later non-raw request.

C# must decide whether to preserve this PHP cache quirk for compatibility or intentionally reject it for audit-grade behavior.

## 16. Search Plan Fixture Structure

PHP plan fixtures:

- `tests/Fixture/search_plans/nexus_cli_v4_searches.yml`
- `tests/Fixture/search_plans/legacy_queries.yml`

`nexus_cli_v4_searches.yml` uses root `version`, root project, root providers, `searches`, item metadata, priority, title/abstract guidance, and item provider overrides.

`legacy_queries.yml` uses `project_id`, `queries`, `text`, `year_min`, `year_max`, `max_results`, and metadata priority.

Planned C# fixtures should replay both forms and explicitly record which fields are normalized, ignored, overridden, or preserved.

## 17. VCR Cassette Fixture Relevance

PHP VCR cassette files present:

- `arxiv_fetch_by_id.yml`
- `arxiv_search.yml`
- `crossref_fetch_by_id.yml`
- `crossref_search.yml`
- `doaj_fetch_by_id.yml`
- `doaj_search.yml`
- `ieee_fetch_by_id.yml`
- `ieee_no_key.yml`
- `ieee_with_key.yml`
- `openalex_fetch_by_id.yml`
- `openalex_search.yml`
- `pubmed_fetch_by_id.yml`
- `pubmed_search.yml`
- `s2_bulk_search.yml`
- `s2_fetch_by_id.yml`
- `s2_pagination.yml`

These are relevant as PHP-side evidence for request URL construction, response normalization, provider pagination, and provider-specific edge cases. They are not PHP-generated golden fixtures yet and should not be copied into C# compatibility claims without a generator manifest and comparator policy.

## 18. PHP Behaviors That Should Be Ported

Recommended C# Search implementation should preserve:

- strict search term validation with two-character minimum after trimming
- year-range validation with an explicit deterministic clock policy
- provider alias normalization and unknown-alias rejection before execution
- provider-order-insensitive cache identity
- cache key field inclusion/exclusion, unless an ADR records a deliberate incompatibility
- plan parser support for `searches` and legacy `queries`
- root and item provider/default override behavior
- plan id and priority selection behavior
- partial provider failure reporting
- provider stats as explicit execution evidence
- raw provider payload preservation when explicitly requested
- stable provider-specific identifier extraction into ADR 0007 namespaces
- no live provider/network calls in CI

## 19. PHP Behaviors That Should Become Intentional Incompatibilities

Recommended intentional incompatibilities or deferred behaviors:

- PHP Search-time deduplication should not be ported into C# Search; Deduplication must remain a separate stage.
- PHP runtime clock use in `YearRange` validation should be replaced with deterministic clock policy for fixture-safe behavior.
- PHP cache identity excluding `includeRawData` is unsafe for audit-grade raw preservation and needs an explicit decision before implementation.
- PHP permissive search-plan unknown-field handling may conflict with local schema-closure policy and needs a C# decision.
- PHP provider/network adapters, API keys, retries, and live HTTP behavior should not enter the first local C# Search implementation.
- PHP persistence side effects, Laravel auto-project creation, jobs, and package commands should not be ported into local domain Search.
- PHP compatibility must remain unclaimed until PHP-generated fixtures and comparators exist.

## 20. Required C# Decisions Before Search Implementation

Required decisions:

1. Define a C# Search trace/result record that preserves raw provider sightings and duplicates without Deduplication.
2. Decide whether Search cache identity exactly preserves PHP exclusions, especially `includeRawData`.
3. Decide deterministic year-range clock behavior.
4. Decide whether Search plan parsing remains PHP-permissive or becomes schema-closed.
5. Decide whether the first C# Search implementation is stub-provider only, with provider/network adapters deferred.
6. Define comparator rules for provider-result ordering, provider stats, raw payloads, generated ids, and runtime durations.
7. Define how Search traces bind to Shared Identity without creating title-only identity or no-id dedupe.
8. Define how later Deduplication receives Search raw result traces.

## Explicit Non-Claims

- no C# Search implementation
- no provider/network behavior
- no PHP compatibility claim
- no generated PHP fixtures
- no Deduplication behavior resolution
- no Screening behavior resolution
- no Search persistence schema
- no API, UI, cloud, provider SDK, or credential behavior
- no blueprint conformance claim
