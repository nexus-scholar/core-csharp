# Gate 9 Search Reconnaissance

Status: reconnaissance and planning only.

## Goal

Map pinned PHP Search behavior and prepare fixture/comparator planning before any C# Search implementation.

This gate document covers the Search portion of Gate 9 porting work. It does not change the accepted Gate 9 shared identity implementation.

## Sources Read

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0009-portable-bundle-and-artifact-contract.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `specs/SOURCE.lock.json`
- pinned PHP Search module under `../core`
- PHP Search unit and integration tests
- PHP search plan fixtures
- PHP VCR cassette catalog

## Branch Scope

Allowed paths:

- `docs/port/php-search-behavior.md`
- `docs/port/php-search-fixture-plan.md`
- `docs/gates/GATE-09-SEARCH.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

Forbidden paths:

- `src/**`
- `tests/**`
- `fixtures/**`
- `specs/**`
- PHP reference repo changes
- provider/network implementation
- generated PHP fixtures
- C# Search implementation

## Reconnaissance Summary

Pinned PHP Search behavior includes:

- validated `SearchQuery`, `SearchTerm`, and `YearRange`
- provider aliases: `openalex`, `crossref`, `semantic_scholar`, `arxiv`, `pubmed`, `ieee`, and `doaj`
- provider-order-insensitive cache identity
- selected-provider validation before cache lookup and execution
- partial provider failure reporting through provider stats
- provider-specific pagination, retry, rate-limit, and normalization behavior
- YAML plan parsing for current `searches` and legacy `queries`
- optional raw provider data preservation
- persistent search-run recording in Laravel-facing paths
- PHP Search-time deduplication before return/cache

The PHP Search-time deduplication boundary is not safe to port directly into C# Search because Gate 9 shared identity explicitly did not resolve Search, Deduplication, Screening, or corpus snapshot behavior.

## Planning Decisions Accepted For Future Task Contract

Future C# Search implementation should:

1. start with local/stub provider traces, not live provider/network behavior
2. preserve PHP provider alias normalization and unknown-provider rejection
3. preserve provider-order-insensitive cache identity unless an ADR records an intentional incompatibility
4. use deterministic year-range validation for fixtures instead of a runtime-current-year dependency
5. parse both PHP plan forms: `searches` and legacy `queries`
6. preserve raw provider sightings before Deduplication
7. bind normalized identifiers to ADR 0007 shared identity namespaces without using title-only identity
8. keep no-id works as unresolved candidates, not canonical membership identity
9. expose provider stats and partial failures as audit evidence
10. keep provider/network adapters, persistence, jobs, API, UI, and cloud behavior outside the first local Search implementation

## Required Future Implementation Shape

The next Search implementation contract should define a local Search trace with:

- request identity
- normalized request fields
- selected provider aliases
- active provider aliases
- cache identity material
- ordered provider attempts
- ordered raw provider sightings
- normalized work identity fields
- optional raw payload digest or raw payload field
- provider stats
- partial failure details
- explicit non-deduplication boundary

It must not return a deduplicated corpus as the Search output unless a separate accepted Deduplication decision creates that stage.

## Conflict Status

`CF-013`: resolved for Search reconnaissance planning. PHP cache identity is provider-order-insensitive; it includes term, year range, language, max results, offset, and sorted active provider aliases, and excludes query id, project id, and `includeRawData`. C# implementation and compatibility evidence remain pending.

`CF-016`: opened. PHP Search aggregates through `CorpusSlice` and `DeduplicationPort`, but C# Search must preserve raw provider sightings and duplicates before Deduplication.

`CF-017`: opened. PHP YAML Search plans ignore unknown fields, while local audit-grade gate behavior has preferred schema closure. C# must decide permissive PHP plan parsing versus stricter schema-closed Search artifacts before implementation.

## Fixture Plan

Required planned fixture families are recorded in `docs/port/php-search-fixture-plan.md` and summarized here:

- query and cache identity fixtures
- provider selection and execution fixtures
- search plan parsing fixtures
- provider normalization fixtures
- raw Search trace and Deduplication-boundary fixtures
- locked-project and persistence-shape fixtures only if admitted by a later scope

Representative fixture IDs:

- `search-query-validation.json`
- `search-cache-key-provider-order.json`
- `search-cache-key-field-inclusion.json`
- `search-cache-key-field-exclusion.json`
- `search-cache-key-active-provider-set.json`
- `search-provider-selection-all.json`
- `search-provider-selection-subset.json`
- `search-provider-selection-unknown-alias.json`
- `search-provider-partial-failure.json`
- `search-plan-parse-nexus-cli-v4.json`
- `search-plan-parse-legacy-queries.json`
- `search-plan-item-overrides.json`
- `search-normalize-openalex-stub.json`
- `search-normalize-semantic-scholar-stub.json`
- `search-normalize-crossref-stub.json`
- `search-normalize-arxiv-stub.json`
- `search-normalize-pubmed-stub.json`
- `search-normalize-ieee-stub.json`
- `search-normalize-doaj-stub.json`
- `search-trace-raw-provider-results.json`
- `search-trace-duplicate-provider-sightings.json`
- `search-trace-dedup-not-applied.json`

## Negative Cases

Required negative cases:

- invalid search term
- invalid year range
- unknown provider alias
- non-positive max results or plan limit
- invalid YAML
- non-list `searches` or `queries`
- non-mapping plan item
- missing item id
- missing query/text
- non-mapping metadata
- missing selected plan id
- partial provider failure
- all providers failed
- duplicate provider sightings must not be deduped by Search
- title-only overlap must not become Search identity
- no-id candidate must not become canonical membership identity
- raw-data cache ambiguity must be tested if PHP cache exclusions are preserved

## Comparator Plan

Comparators must be built before compatibility claims.

Comparator groups:

- cache comparator: exact included/excluded fields and hash equality/inequality
- plan parser comparator: normalized item shape, ordering, overrides, filters, and stable errors
- provider selection comparator: active aliases, validation order, stats order, partial failure shape
- provider normalization comparator: identifiers, title, year, authors, venue, raw payload presence/digest
- Search trace comparator: raw provider sightings, duplicates, order, provider stats, and no Deduplication output

Generated ids, runtime durations, and live HTTP timing must not be semantic comparator anchors unless the fixture generator pins them.

## Implementation Readiness

No.

Search implementation is blocked until:

- `CF-016` raw Search trace versus Deduplication boundary is resolved
- `CF-017` Search plan schema-closure policy is resolved
- cache handling for `includeRawData` exclusion is explicitly accepted or rejected
- a stub-provider trace contract is accepted
- generated PHP fixture/comparator harness exists, or the implementation is explicitly local-only with no PHP compatibility claim

## Explicit Claims Not Made

- no C# Search behavior implemented
- no provider/network behavior
- no PHP compatibility
- no generated PHP fixtures
- no Deduplication behavior
- no Screening behavior
- no Search persistence schema
- no API, UI, job, command, cloud, or provider SDK behavior
- no bundle behavior change
- no AI governance behavior
- no blueprint conformance
