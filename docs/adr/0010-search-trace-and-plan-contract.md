# ADR 0010: Search Trace and Plan Contract

Status: Accepted

Date: 2026-06-27

## Context

Gate 9 Search reconnaissance mapped observable PHP Search behavior from the pinned PHP reference. PHP Search validates search requests, selects providers, executes provider adapters, normalizes provider responses into `ScholarlyWork`, records provider stats and partial failures, parses YAML search plans, and caches normalized aggregate payloads.

The reconnaissance also found three conflicts that must be resolved before C# Search implementation:

- `CF-013`: PHP Search cache identity excludes some fields, including `includeRawData`, and is provider-order-insensitive.
- `CF-016`: PHP `SearchAggregator` deduplicates during Search by converting raw provider results into `CorpusSlice`, invoking `DeduplicationPort`, returning a deduplicated corpus, and caching deduplicated works.
- `CF-017`: PHP YAML Search plans are permissive and ignore unknown fields, while local C# gates have preferred schema closure for authoritative artifacts.

The app reconnaissance added `CF-018`: CLI and Web consume PHP Core Search while adding app-local run files, database rows, display hashes, plan drafts, audit rows, and workflow projections. Those app behaviors are useful consumer evidence, but they do not override accepted C# Core ADRs.

`ADR 0001` requires intentional incompatibilities to be documented. `ADR 0002` requires deterministic canonical records and excludes projections, caches, local paths, and generated narratives from scientific authority. `ADR 0007` defines shared scientific identity through normalized stable identifiers, rejects title-only identity, and keeps no-id works as unresolved candidates. `ADR 0009` defines portable bundle/artifact rules and keeps Search, Deduplication, Screening, and general corpus snapshot equality out of Gate 6.

This ADR defines the local C# Search trace and plan contract needed before implementation. It does not implement Search.

## Decision

### 1. Search output is a raw Search trace

C# Search output is a raw Search trace, not a deduplicated `CorpusSlice` and not canonical corpus membership.

The local schema id for the first C# Search trace contract is:

```text
nexus.search.trace
```

The local schema version is:

```text
1.0.0
```

A Search trace records what Search attempted and observed. It is evidence for later stages. It is not the later Deduplication result, Screening corpus, locked corpus snapshot, bundle, or release snapshot.

### 2. Required Search trace shape

A Search trace must carry at least:

- `trace_id`
- `schema_id`
- `schema_version`
- `request`
- `cache_identity`
- `provider_attempts`
- `provider_stats`
- `sightings`
- `summary`
- `non_claims`

The `request` section must carry:

- query term
- optional year range
- optional language
- max results
- offset
- requested `include_raw_data`
- selected provider aliases
- active provider aliases
- optional plan binding when the request came from a Search plan

The `cache_identity` section must carry:

- cache identity algorithm
- material version
- included fields
- excluded fields
- provider-order-insensitive marker
- rendered cache key value

The `provider_attempts` section must carry one ordered attempt record for every selected active provider. Attempt records must include provider alias, attempt order, status, result count, and failure information when present.

The `provider_stats` section must carry display and audit-facing provider statistics such as provider alias, result count, partial failure status, and optional operational duration.

The `sightings` section must carry ordered raw provider sightings. Each sighting must include provider alias, provider-local rank or order, provider work id when available, normalized identifiers when available, normalized display fields, unresolved-candidate marker when no stable identifier exists, and raw payload material or digest when requested.

The `summary` section may include counts such as attempted providers, succeeded providers, failed providers, raw sighting count, and whether all providers failed. Summary values are derived from the trace and must not hide raw sightings.

### 3. Duplicate provider sightings are preserved

Search must preserve duplicate provider sightings.

Two sightings with overlapping stable identifiers remain two Search sightings until a later Deduplication stage consumes the Search trace and emits an explicit Deduplication result.

Search trace ordering is semantic:

- provider attempt order follows selected active provider order;
- sighting order follows provider result order within each provider;
- cross-provider sighting order follows provider attempt order unless a later Search implementation ADR defines a different deterministic ordering rule.

### 4. Provider attempts and partial failure behavior

Search must preserve provider attempts, provider stats, partial failures, and all-failed results.

A provider failure is not a Search trace failure if other providers produce sightings. It is an attempt with failure status and failure details.

An all-failed Search run is still a valid Search trace when the request was valid and provider attempts were made. It has zero sightings and failed attempt records.

Unknown provider aliases are request validation failures and must occur before provider execution and before cache lookup.

### 5. Binding to shared scientific identity

Search binds normalized identifiers to `ADR 0007` namespaces:

- `doi`
- `arxiv`
- `openalex`
- `s2`
- `pubmed`
- `pmcid`
- `ieee`
- `doaj`
- `internal`

Search may normalize provider identifiers into these namespaces, but identifier overlap inside the Search trace is not Deduplication output.

Search must not use title-only identity, title/year/provider display keys, runtime object identity, object hash codes, local row ids, app database ids, or app display hashes as scientific identity.

### 6. No-id works are unresolved Search candidates

Provider sightings without stable identifiers are unresolved Search candidates.

No-id Search candidates:

- may appear in a Search trace;
- must carry source context, provider alias, provider order, and non-empty display title when available;
- cannot satisfy canonical corpus membership identity;
- cannot deduplicate by title, runtime identity, insertion order, app row id, or display hash;
- must remain staged evidence until a later identifier-resolution or Deduplication decision resolves them.

### 7. Search does not call Deduplication

C# Search must not call Deduplication.

The Search implementation must not return a deduplicated corpus as its primary output, must not collapse sightings into `CorpusSlice` membership, and must not elect representatives.

This is an intentional incompatibility with PHP Search aggregation. It is required so the C# audit trail can preserve raw provider evidence before any duplicate decision.

### 8. Deduplication receives Search traces later

Future Deduplication receives Search traces as input.

The Deduplication gate must define:

- which Search trace sections it consumes;
- how duplicate evidence is grouped;
- how representative records are elected or preserved;
- how human-reviewed duplicate decisions are represented;
- how unresolved no-id candidates can or cannot enter deduplicated outputs;
- how Deduplication output binds back to the source Search trace.

ADR 0010 does not implement or resolve Deduplication semantics beyond the Search handoff boundary.

### 9. Search cache identity

C# Search cache identity is provider-order-insensitive.

C# cache identity must include:

- query term value used for Search;
- year range from, if present;
- year range to, if present;
- language, if present;
- max results;
- offset;
- sorted active provider aliases;
- `include_raw_data`.

C# cache identity must exclude:

- generated query id;
- trace id;
- project id;
- runtime durations;
- provider stats;
- provider failure messages;
- raw payload bytes;
- app run ids;
- app database ids;
- app display hashes;
- local file paths;
- provider API keys, base URLs, rate-limit settings, and credentials.

Including `include_raw_data` is an intentional incompatibility with PHP cache behavior. PHP excludes `includeRawData`, which can allow a non-raw cached response to satisfy a raw request or a raw cached response to satisfy a non-raw request. C# rejects that ambiguity because raw payload preservation changes the Search trace evidence surface.

PHP compatibility remains unclaimed until generated fixtures and comparators classify this incompatibility explicitly.

### 10. Deterministic year-range clock policy

Year-range validation must use an explicit deterministic clock policy.

C# Search must not call wall-clock `now` from inside canonical Search validation or fixture replay. The implementation must receive an explicit clock or validation year from the caller, test harness, or fixture harness.

The local Search rule keeps the PHP-compatible year bounds unless later changed:

- minimum year: `1000`;
- maximum year: validation year plus five;
- inverted ranges are invalid.

Fixtures must pin the validation year.

### 11. Search plan parser policy

Local C# Search plan artifacts are schema-closed.

The local Search plan schema id is:

```text
nexus.search.plan
```

The local schema version is:

```text
1.0.0
```

Authoritative C# Search plan artifacts must reject unknown root fields, unknown item fields, missing schema id, missing schema version, and unsupported schema version.

C# may also provide an explicit PHP legacy Search plan import profile for fixture generation and compatibility comparison. That profile may accept PHP forms such as `searches`, legacy `queries`, `query`, legacy `text`, `project`, `project_id`, `limit`, `max_results`, `year_from`, `year_min`, `year_to`, `year_max`, and root/item provider defaults. Any unknown PHP fields accepted by that import profile must be recorded as ignored or extension fields and must not silently enter canonical local Search plan digest material.

This resolves the local contract without pretending that PHP permissive parsing is a safe authoritative artifact policy.

### 12. Stub-provider-only first implementation

The first C# Search implementation must be local and stub-provider only.

Stub providers must be deterministic and fixture-friendly. They may simulate:

- successful provider attempts;
- partial provider failure;
- all-failed provider attempts;
- duplicate sightings;
- no-id unresolved candidates;
- raw payload preservation and omission;
- provider-order-insensitive cache identity.

### 13. Live provider and network behavior is deferred

Live scholarly provider adapters, HTTP clients, retries, rate limits, API keys, credentials, VCR cassette replay, provider SDKs, background jobs, provider configuration, and network behavior are deferred.

No C# CI path may call live scholarly providers.

Provider-specific normalization may be planned from PHP evidence and local stub fixtures, but live provider parity requires a later provider/network gate or explicit Search extension ADR.

### 14. Runtime duration and generated query id comparator policy

Generated query ids and runtime durations are not semantic compatibility anchors unless a fixture pins them.

Comparators must:

- ignore generated query id equality by default;
- compare generated query id shape only when the fixture is about id shape;
- ignore exact runtime durations by default;
- assert duration presence, numeric type, and non-negative value when duration is present;
- compare exact duration only in fixed-duration stub fixtures;
- compare provider alias, attempt status, result count, failure category, sighting order, normalized identifiers, raw-data presence, and cache identity exactly.

Runtime duration and generated ids must not be used as scientific identity.

### 15. CLI and Web consumer boundary

CLI and Web app evidence informs the Search trace shape, but app projections are not Core authority.

C# Search trace must be consumable by CLI/Web later without forcing Search-time Deduplication. It must preserve:

- raw provider sightings;
- duplicate provider sightings;
- provider attempts;
- provider stats;
- partial failure evidence;
- no-id unresolved candidates;
- enough stable request and provider-order data for app display and later handoff.

CLI/Web display hashes, fallback keys, local run files, latest pointers, database rows, job lifecycle rows, audit rows, wiki pages, app protocol snapshots, app corpus snapshots, and app manifests are projections or app persistence records. They are not Search trace identity, Core protocol authority, Core provenance, Core bundle identity, or scientific work identity.

Apps may store or display Search trace projections, but those projections must not mutate the Search trace or imply Deduplication.

### 16. Search trace and bundles

Search traces may later be exported as `ADR 0009` artifacts.

When exported as artifacts, their artifact identity is their `raw-artifact-bytes` digest over exact bytes. ADR 0010 does not add a bundle manifest section, bundle equality rule, or general corpus snapshot equality rule.

### 17. Future work

Future gates must still decide:

- C# Search source implementation;
- local Search fixtures;
- PHP-generated Search fixtures;
- provider-specific normalization implementation;
- live provider/network adapters;
- Search persistence;
- API/UI/cloud integration;
- Deduplication behavior;
- Screening behavior;
- corpus lock and snapshot equality;
- app alignment beyond Search trace consumption.

## Alternatives Considered

### Preserve PHP Search-time deduplication

Rejected.

PHP behavior is observable evidence, but returning a deduplicated corpus from Search would erase raw duplicate sightings before the C# audit trail can record explicit Deduplication evidence. This would conflict with `ADR 0007` no-id candidate handling and with the app consumer need to see raw provider sightings.

### Preserve PHP cache exclusion of `includeRawData`

Rejected as a local C# cache rule.

`includeRawData` changes whether raw provider payload evidence is included in the Search trace. Excluding it from C# cache identity risks serving a trace with the wrong raw-evidence surface.

### Make PHP permissive YAML parsing the local artifact rule

Rejected.

Permissive parsing is useful for PHP fixture import, but authoritative local artifacts must be schema-closed so unknown fields cannot silently influence or disappear from scientific records.

### Implement live providers first

Rejected.

Live provider behavior introduces network, credentials, retries, rate limits, provider availability, and moving external APIs before the local trace contract is tested.

### Treat CLI/Web run files or display hashes as Core authority

Rejected.

App files, rows, display hashes, and status projections are integration evidence and UI state. They are not stable scientific identity or accepted Core records.

## Consequences

Positive:

- Search implementation can proceed with a narrow local stub-provider slice.
- Raw provider evidence survives for future Deduplication.
- App consumers can later display Search runs without forcing Search-time dedupe.
- The `include_raw_data` cache ambiguity is removed locally.
- Local Search plans have schema-closed artifact behavior.

Negative:

- C# Search will intentionally differ from PHP in Search-time deduplication and `includeRawData` cache identity.
- PHP compatibility remains unclaimed until generated fixtures and comparators classify differences.
- Live provider behavior remains unavailable in C# Core.
- App behavior still needs later alignment beyond Search trace consumption.

## Migration Effect

No persisted C# data is migrated by this ADR.

Future C# Search implementation must treat any existing scaffold or app-derived Search output as non-authoritative until transformed into the ADR 0010 Search trace shape.

PHP aggregate results that already deduplicated Search output cannot be treated as raw Search traces unless the raw provider sightings can be reconstructed from source evidence.

## Fixture Effect

Search fixtures must be updated or interpreted under ADR 0010:

- add `search-trace-schema-closed-plan.json`;
- add `search-trace-php-legacy-plan-import.json`;
- add `search-cache-key-include-raw-data-included.json`;
- keep `search-cache-key-provider-order.json`;
- keep `search-cache-key-field-inclusion.json`;
- keep `search-cache-key-field-exclusion.json`, but do not list `include_raw_data` as excluded for C# local behavior;
- keep `search-trace-duplicate-provider-sightings.json`;
- keep `search-trace-dedup-not-applied.json`;
- add or keep all-failed provider fixture coverage;
- add app consumer projection checks that display hashes and app run ids are outside Search trace identity.

Comparators must classify the PHP `includeRawData` cache exclusion and PHP Search-time deduplication as intentional incompatibilities for local C# Search unless a later ADR reverses this decision.

## Conflict Effect

`CF-013` is resolved for the local C# Search contract. C# preserves provider-order-insensitive cache identity, but intentionally includes `include_raw_data` in cache identity. PHP compatibility remains pending.

`CF-016` is resolved for the local C# Search contract. Search output is a raw trace, preserves duplicate provider sightings, and does not call Deduplication. Deduplication remains a later gate.

`CF-017` is resolved for the local C# Search contract. Local Search plan artifacts are schema-closed; PHP permissive parsing is allowed only through an explicit legacy import/comparator profile.

`CF-018` is narrowed for the Search consumer boundary. CLI/Web can consume Search traces and display projections, but app hashes, files, rows, and UI status are not Core authority. Broader app alignment for Protocol, Provenance, Bundle, Deduplication, Screening, Full Text, Snapshot, API/UI/cloud, and AI governance remains future work.

## Reversal Conditions

Revise this ADR only if:

1. generated PHP fixtures prove that preserving PHP Search-time deduplication is required and the repository explicitly chooses PHP compatibility over the raw-trace boundary;
2. a later accepted Deduplication ADR defines a different Search handoff shape with equivalent raw-evidence preservation;
3. provider/network implementation evidence requires a versioned Search trace schema change;
4. a later app-alignment ADR promotes specific CLI/Web fields into Core records with digest and migration rules;
5. a later schema policy replaces the schema-closed local Search plan rule.

## Explicit Claims Not Made

- no C# Search implementation
- no source code changes
- no generated fixtures
- no PHP compatibility claim
- no provider/network behavior
- no live provider calls
- no Search persistence schema
- no API, UI, job, command, or cloud behavior
- no Deduplication implementation
- no Screening behavior
- no bundle behavior change
- no corpus snapshot equality
- no app behavior made authoritative
- no AI governance behavior
- no blueprint conformance
