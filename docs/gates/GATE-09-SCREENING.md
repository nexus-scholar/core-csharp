# Gate 9 Screening Reconnaissance

Status: reconnaissance and planning only. C# Screening implementation is not ready.

## Goal

Map pinned PHP Screening behavior and CLI/Web screening behavior before any C# Screening implementation.

This document covers the Screening part of Gate 9 PHP behavior porting. It does not implement Screening, generate fixtures, or claim PHP compatibility.

## Sources Read

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0010-search-trace-and-plan-contract.md`
- `docs/adr/0011-search-import-source-contract.md`
- `docs/adr/0012-deduplication-evidence-and-cluster-contract.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `docs/recon/apps/**`
- `specs/SOURCE.lock.json`
- pinned PHP Screening module under `../core`
- PHP Screening unit and feature tests
- CLI screening command behavior under `../nexus-cli`
- Web screening workflow, batch, assignment, conflict, and adjudication behavior under `../nexus-web`

## Branch Scope

Allowed paths:

- `docs/port/php-screening-behavior.md`
- `docs/port/php-screening-fixture-plan.md`
- `docs/gates/GATE-09-SCREENING.md`
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
- C# Screening implementation

## Reconnaissance Summary

Pinned PHP Screening behavior includes:

- `ScreeningWork` records with title, abstract, year, venue, provider, identifiers, and metadata;
- `ScreeningCriteria` with recursively sorted associative keys and SHA-256 hash over JSON;
- screening stages `title_abstract`, `full_text`, and `human_adjudication`;
- run modes `rules`, `llm_single`, `llm_council`, and `human`;
- decision values `include`, `needs_review`, and `exclude`;
- LLM prompt rendering and strict response schema;
- single-model vote handling;
- council aggregation with conservative `needs_review` behavior for severe disagreement or model failure;
- `ScreeningVerdict` records with rationale, confidence, criteria hash, votes, actor/model source, and metadata;
- human adjudication that appends decisions and preserves source decision ids;
- screening run comparison with transitions, agreement/disagreement, missing works, and human reference run detection;
- optional project lock enforcement through `CorpusLockPolicy`.

CLI behavior adds a file-based deterministic/LLM screening path that writes local screen files. That path is app-local evidence, not Core authority.

Web behavior adds reviewer assignment, batches, conflict rows, adjudication UI, audit rows, representative-aware locked snapshot requirements, and full-text screening workflows. These are app workflow evidence, not Core authority unless a later ADR admits them.

## Open Conflicts

`CF-021`: Screening input and locked candidate boundary.

C# Screening must decide whether input is a Deduplication result, a locked representative-aware corpus snapshot, a reviewable candidate set, or another stage-specific record. Screening must not consume raw Search traces directly.

`CF-022`: Screening decision authority and AI proposal boundary.

PHP records LLM-produced verdicts. C# must define how model votes and council aggregates remain proposal evidence and when an authorized human action creates a scientific screening decision.

`CF-023`: Screening criteria schema and digest contract.

PHP criteria hash is raw SHA-256 over normalized JSON. C# must define criteria schema, criteria digest scope, stage binding, and protocol/workflow binding before implementation.

`CF-024`: Screening app workflow projection boundary.

CLI file outputs and Web batch, assignment, conflict, audit, and full-text item rows are app workflow evidence. C# Core must decide which, if any, become domain records.

## Fixture Plan

Planned fixture families are recorded in `docs/port/php-screening-fixture-plan.md` and `docs/port/GOLDEN-FIXTURE-PLAN.md`.

Required future fixture groups:

- Screening input and lock boundary
- Criteria digest and schema closure
- Human decision records
- LLM suggestion and council evidence
- Human adjudication and conflict resolution
- Run lifecycle and comparison
- Full-text screening planning
- App projection boundary

Required negative categories:

- raw Search trace screened directly;
- unlocked or mutable corpus screened;
- missing actor;
- missing rationale;
- invalid confidence;
- wrong project or stage;
- LLM suggestion treated as final decision;
- silent overwrite of prior decision;
- conflict/adjudication source links lost;
- full-text screening without artifact evidence;
- app rows treated as Core authority;
- PHP compatibility claimed without generated fixtures.

## Comparator Plan

Comparators must preserve:

- decision value;
- stage;
- actor/model authority marker;
- rationale;
- evidence;
- uncertainty;
- exclusion basis;
- confidence;
- criteria digest;
- source decision ids;
- vote-to-verdict links;
- candidate/snapshot binding;
- append-only decision history.

Comparators may ignore generated ids, timestamps, and durations only when a fixture explicitly marks them as generated and non-semantic. They must not ignore human actor, criteria digest, source decision links, or authority boundary fields.

## Implementation Readiness

Implementation readiness: **no**.

Next required branch should be an ADR/contract, not implementation:

```text
ADR 0013: Screening Decision and Conflict Contract
```

ADR 0013 should resolve:

- `CF-021`
- `CF-022`
- `CF-023`
- `CF-024`

Expected local stance unless evidence contradicts it:

- Screening consumes a locked/reviewable post-Dedup candidate set.
- Human decisions require actor and rationale.
- AI/model output is suggestion evidence only.
- Decisions append and never overwrite.
- Criteria digest uses explicit C# canonical digest semantics.
- Web assignments/conflicts are app workflow projections unless admitted by the ADR.

## Explicit Claims Not Made

- no C# Screening implementation
- no Screening ADR accepted
- no generated PHP fixtures
- no PHP compatibility
- no Screening fixtures generated
- no full-text retrieval implementation
- no persistence/API/UI/cloud behavior
- no CLI/Web behavior made authoritative
- no AI governance behavior
- no Search or Deduplication behavior change
- no bundle behavior change
- no blueprint conformance
