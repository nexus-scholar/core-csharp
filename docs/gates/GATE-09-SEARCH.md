# Gate 9 Search Trace And Plan Contract

Status: ADR/contract accepted for local stub-provider Search implementation. No Search source code is implemented by this document.

## Goal

Record the local Search trace and plan contract before C# Search implementation.

This gate document builds on PHP Search reconnaissance and `ADR 0010`. It covers the Search portion of Gate 9 porting work and does not change the accepted Gate 9 shared identity implementation.

## Sources Read

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0009-portable-bundle-and-artifact-contract.md`
- `docs/adr/0010-search-trace-and-plan-contract.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `docs/recon/apps/**`
- `specs/SOURCE.lock.json`
- pinned PHP Search module under `../core`
- PHP Search unit and integration tests
- PHP search plan fixtures
- PHP VCR cassette catalog

## Branch Scope

Allowed paths:

- `docs/adr/0010-search-trace-and-plan-contract.md`
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
- `nexus-cli`
- `nexus-web`

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

## ADR 0010 Decisions Accepted For Future Task Contract

`ADR 0010` defines the local Search Trace and Plan Contract. Future C# Search implementation should:

1. start with local/stub provider traces, not live provider/network behavior
2. preserve PHP provider alias normalization and unknown-provider rejection
3. preserve provider-order-insensitive cache identity
4. use deterministic year-range validation for fixtures instead of a runtime-current-year dependency
5. use schema-closed local Search plans, with PHP permissive plans admitted only through an explicit legacy import/comparator profile
6. preserve raw provider sightings and duplicate provider sightings before Deduplication
7. bind normalized identifiers to ADR 0007 shared identity namespaces without using title-only identity
8. keep no-id works as unresolved candidates, not canonical membership identity
9. expose provider stats and partial failures as audit evidence
10. include `include_raw_data` in C# cache identity as an intentional incompatibility with PHP `includeRawData` cache exclusion
11. keep provider/network adapters, persistence, jobs, API, UI, and cloud behavior outside the first local Search implementation

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

`CF-013`: resolved for the local Search contract by `ADR 0010`. C# cache identity remains provider-order-insensitive, includes term, year range, language, max results, offset, sorted active provider aliases, and `include_raw_data`, and excludes generated query id, trace id, project id, runtime data, provider stats/failures, raw bytes, app ids/hashes, local paths, and provider credentials. PHP compatibility remains pending because PHP excludes `includeRawData`.

`CF-016`: resolved for the local Search contract by `ADR 0010`. C# Search output is a raw Search trace, preserves duplicate provider sightings, and does not call Deduplication. Deduplication remains a later gate and will consume Search traces as input.

`CF-017`: resolved for the local Search contract by `ADR 0010`. Authoritative local C# Search plan artifacts are schema-closed. PHP-permissive plan parsing is allowed only as an explicit legacy import/comparator profile.

`CF-018`: narrowed for the Search consumer boundary by `ADR 0010`. CLI/Web may consume Search traces and display projections, but app display hashes, run files, database rows, job lifecycle rows, audit rows, latest pointers, and app manifests are not Core authority.

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
- `search-cache-key-include-raw-data-included.json`
- `search-provider-selection-all.json`
- `search-provider-selection-subset.json`
- `search-provider-selection-unknown-alias.json`
- `search-provider-partial-failure.json`
- `search-provider-all-failed-empty.json`
- `search-plan-parse-nexus-cli-v4.json`
- `search-plan-parse-legacy-queries.json`
- `search-trace-schema-closed-plan.json`
- `search-trace-php-legacy-plan-import.json`
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
- raw-data request must not be satisfied by a non-raw cache entry under the local C# cache contract
- PHP raw-data cache ambiguity must be classified as an intentional incompatibility unless a later ADR reverses `ADR 0010`

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

Yes, for a local stub-provider C# Search implementation only.

Implementation is still blocked for:

- live provider/network behavior
- PHP compatibility claims
- generated PHP fixtures
- Search persistence/API/UI/job/cloud behavior
- Deduplication and Screening behavior
- bundle behavior changes
- app behavior authority beyond Search trace consumption

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
