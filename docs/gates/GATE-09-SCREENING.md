# Gate 9 Screening Reconnaissance

Status: ADR 0013 contract decisions accepted locally. C# Screening implementation is ready to start only after this ADR branch is merged.

## Goal

Map pinned PHP Screening behavior and CLI/Web screening behavior before any C# Screening implementation.

This document covers the Screening part of Gate 9 PHP behavior porting. It now includes the local contract decisions from `ADR 0013`. It does not implement Screening, generate fixtures, or claim PHP compatibility.

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
- `docs/adr/0013-screening-decision-and-conflict-contract.md`
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

## Contract Decisions

`ADR 0013` decides:

- Screening consumes a Deduplication result or locked/reviewable candidate set, not raw Search traces directly.
- Local stages are `title_abstract`, `full_text`, and `human_adjudication`.
- Screening criteria use `nexus.screening.criteria` version `1.0.0` and an `ADR 0002` canonical JSON record digest.
- Final Screening decisions use `nexus.screening.decision` version `1.0.0`.
- Canonical verdicts are `include`, `exclude`, and `needs_review`; app labels such as `maybe` map explicitly to `needs_review`.
- Final decisions require an identified human actor and rationale.
- AI/model/rule outputs are suggestion evidence only.
- Conflicts are detected from differing final human verdicts for the same candidate, stage, criteria digest, and candidate set.
- Adjudication is append-only, preserves source decision ids, and resolves rather than mutates prior decisions.
- Web assignments, batches, conflict rows, full-text item links, CLI screen files, and app audit rows remain app projections.

## Conflict Status

`CF-021`: Screening input and locked candidate boundary.

Resolved for the local contract by `ADR 0013`. Screening consumes a Deduplication result or locked/reviewable candidate set and must not consume raw Search traces directly.

`CF-022`: Screening decision authority and AI proposal boundary.

Resolved for the local contract by `ADR 0013`. Final Screening decisions require an identified human actor; AI/model/rule outputs are proposal evidence only.

`CF-023`: Screening criteria schema and digest contract.

Resolved for the local contract by `ADR 0013`. Criteria are schema-identified, versioned, stage-bound, and digest-bound using `ADR 0002` canonical JSON record rules.

`CF-024`: Screening app workflow projection boundary.

Narrowed for Core by `ADR 0013`. CLI file outputs and Web batch, assignment, conflict, audit, and full-text item rows remain app workflow evidence unless a later ADR admits them.

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

Implementation readiness: **yes for local C# Screening implementation after ADR 0013 is merged**.

Still not ready:

- PHP compatibility
- generated PHP fixtures
- persistence/API/UI/cloud
- app workflow authority
- live LLM/provider/network behavior
- AI governance implementation
- full-text retrieval or artifact storage behavior

## Explicit Claims Not Made

- no C# Screening implementation
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
