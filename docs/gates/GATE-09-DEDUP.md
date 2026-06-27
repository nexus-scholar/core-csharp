# Gate 9 Deduplication Contract

Status: contract accepted for local C# Deduplication implementation. C# Deduplication is not implemented in this branch.

## Goal

Record the PHP Deduplication reconnaissance outcome, ADR 0012 local contract decisions, and fixture/comparator plan before C# Deduplication implementation.

This document extends Gate 9 porting work after Shared Identity, Search trace, and Search import local slices. It does not alter those accepted scopes.

## Sources Read

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0010-search-trace-and-plan-contract.md`
- `docs/adr/0011-search-import-source-contract.md`
- `docs/adr/0012-deduplication-evidence-and-cluster-contract.md`
- `docs/gates/GATE-09-SEARCH.md`
- `docs/gates/GATE-09-SEARCH-IMPORT.md`
- `docs/port/php-search-behavior.md`
- `docs/port/php-search-fixture-plan.md`
- `docs/recon/apps/**`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `specs/SOURCE.lock.json`
- pinned PHP Deduplication module under `../core`
- PHP Deduplication tests
- PHP corpus/snapshot/lock tests affecting Deduplication
- `nexus-cli` Search/Dedup related behavior
- `nexus-web` Deduplication and representative lock behavior

## Branch Scope

Allowed paths:

- `docs/adr/0012-deduplication-evidence-and-cluster-contract.md`
- `docs/port/php-deduplication-fixture-plan.md`
- `docs/gates/GATE-09-DEDUP.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

Forbidden paths:

- `src/**`
- `tests/**`
- `fixtures/**`
- `specs/**`
- PHP reference repo changes
- `nexus-cli` changes
- `nexus-web` changes
- generated PHP fixtures
- C# Deduplication implementation

## Behavior Summary

Pinned PHP Deduplication:

- receives a `CorpusSlice`
- uses ordered duplicate policies
- exact policies cover DOI, arXiv, OpenAlex, Semantic Scholar, and PubMed
- title fuzzy policy exists with a threshold conflict
- fingerprint policy exists but is not in the documented default Laravel binding
- uses union-find for transitive clustering
- preserves direct duplicate pair evidence
- elects a representative by completeness plus provider priority
- merges duplicate member fields into the representative when exporting representative corpus output
- can fall back to runtime object identity for no-primary-id PHP objects
- can enforce project lock state before deduplication

PHP app behavior adds important projections:

- CLI Search writes a global deduplicated `all_*.json` through `CorpusSlice` merge behavior.
- Web rebuilds draft corpus with `fromWorksUnsafe()` so Deduplication can inspect every draft member.
- Web persists dedup runs, membership hashes, clusters, cluster members, policy stats, and representative snapshots.
- Web blocks corpus lock until dedup evidence is fresh and complete.
- Web Screening requires a representative-aware locked snapshot.

## Contract Decisions

ADR 0012 decides:

- C# Deduplication consumes Search trace/import sighting evidence.
- PHP's pre-deduplicated Search corpus is not authoritative C# Deduplication input.
- Exact identifier overlap uses ADR 0007 normalized stable identifiers and namespace-sensitive matching.
- Title-only identity remains forbidden.
- Fuzzy title matching creates candidate evidence and requires review by default.
- The local default fuzzy title threshold is `95` / `0.95`; PHP `92` is treated as documentation/configuration drift unless an explicit custom policy records otherwise.
- Exact identifier clusters are transitive, but candidate-only evidence does not create automatic clusters.
- Representative election is deterministic, evidence-backed, and independent of runtime object identity.
- Representative projection fills missing fields without erasing raw sightings or evidence links.
- No-id works remain unresolved candidates unless future human review accepts a duplicate decision.
- Web membership hashes, representative snapshots, persisted runs, and app clustering are app projections, not Core authority.

## Conflict Status

`CF-011`: resolved for local C# Deduplication contract.

Raw Dedup input is Search trace/import sighting evidence with source bindings. PHP `CorpusSlice` and PHP Search-time deduplicated corpus output are not authoritative C# Dedup input.

`CF-012`: resolved for local C# Deduplication contract.

The local title fuzzy default is `95` / `0.95`. PHP `92` remains drift evidence and can be represented only by an explicit custom policy.

`CF-016`: implemented for Search; narrowed for Dedup handoff.

Search already emits raw traces and does not call Deduplication. Deduplication consumes those traces later without changing Search behavior.

`CF-020`: narrowed for Core.

CLI/Web projections are useful evidence but not Core authority. Web membership hashes, persisted runs, representative snapshots, stale-run checks, and app scoring remain projections unless a later ADR adopts them.

## Fixture Plan

Detailed fixture planning lives in `docs/port/php-deduplication-fixture-plan.md`.

Required planned fixture families:

- input shape from Search traces and imported sightings
- exact identifier clustering
- title fuzzy threshold, review-required, and year-gap behavior
- transitive cluster assembly
- representative election and merge behavior
- no-id unresolved candidates
- raw duplicate evidence preservation
- app projection catalog

Key planned fixture IDs:

- `dedup-input-search-trace-to-candidates.json`
- `dedup-input-imported-sightings-to-candidates.json`
- `dedup-input-raw-sightings-preserved.json`
- `dedup-exact-doi-cluster.json`
- `dedup-exact-openalex-cluster.json`
- `dedup-exact-s2-cluster.json`
- `dedup-exact-arxiv-cluster.json`
- `dedup-exact-pubmed-cluster.json`
- `dedup-source-specific-id-not-workid.json`
- `dedup-title-fuzzy-threshold-decision.json`
- `dedup-title-fuzzy-threshold-conflict-92-vs-95.json`
- `dedup-title-fuzzy-review-required.json`
- `dedup-transitive-cluster.json`
- `dedup-transitive-evidence-preserved.json`
- `dedup-representative-election-completeness.json`
- `dedup-representative-election-provider-priority.json`
- `dedup-representative-election-doi-tiebreak.json`
- `dedup-representative-election-stable-tiebreak.json`
- `dedup-merge-field-preservation.json`
- `dedup-merge-identifier-union.json`
- `dedup-no-id-candidate-not-auto-merged.json`
- `dedup-raw-duplicate-evidence-preserved.json`
- `dedup-app-membership-hash-projection.json`
- `dedup-representative-snapshot-app-projection.json`

## Comparator Plan

Comparators must be built before PHP compatibility claims.

Comparator groups:

- input comparator: Search/import sightings to Dedup candidates
- identifier comparator: normalized namespace/value overlap
- title fuzzy comparator: normalization, threshold `95` / `0.95`, year gap, confidence rounding, and review-required flag
- cluster comparator: unordered member sets plus ordered/direct evidence edges
- representative comparator: deterministic election and tie-breakers
- merge comparator: representative field preservation and fill behavior
- app projection comparator: membership hash, persisted run summaries, representative snapshot membership, stale run detection

Generated ids, runtime durations, PHP object ids, wall-clock retrieval times, app row ids, and local file paths must not be semantic comparator anchors unless fixture generators pin them.

## Implementation Readiness

Implementation readiness: **yes for local C# Deduplication implementation against ADR 0012**.

Still not ready:

- PHP compatibility
- generated PHP fixtures
- Screening behavior
- corpus snapshot implementation
- Deduplication persistence schema
- API/UI/cloud behavior
- `nexus-cli` or `nexus-web` behavior as Core authority

## Explicit Claims Not Made

- no C# Deduplication implementation
- no generated PHP fixtures
- no PHP compatibility
- no live provider/network behavior
- no Search implementation change
- no Search import implementation change
- no Screening behavior
- no corpus snapshot implementation
- no Deduplication persistence schema
- no API/UI/cloud behavior
- no `nexus-cli` or `nexus-web` behavior made authoritative
- no blueprint conformance
