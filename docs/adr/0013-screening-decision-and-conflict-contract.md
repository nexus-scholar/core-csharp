# ADR 0013: Screening Decision and Conflict Contract

Status: Accepted

Date: 2026-06-27

## Context

Gate 9 Screening reconnaissance mapped pinned PHP Screening behavior and CLI/Web screening behavior before any C# implementation.

The pinned PHP reference records Screening runs, criteria hashes, model votes, council aggregates, verdicts, human adjudication, and run comparisons. Nexus Web adds reviewer assignments, screening batches, conflict rows, audit rows, representative-aware locked snapshots, and full-text screening workflows. Nexus CLI adds a file-based deterministic/LLM screening path in addition to project-backed Core calls.

These sources conflict with C# Core laws and prior ADRs:

- `AGENTS.md` says LLM outputs remain proposals until an authorized human action accepts them.
- `ADR 0002` requires canonical digest scope and excludes projections, local paths, and runtime artifacts from scientific identity.
- `ADR 0007` rejects title-only and runtime-object identity.
- `ADR 0010` says Search emits raw traces and does not call Deduplication.
- `ADR 0012` says Deduplication consumes raw Search/import sightings and emits evidence-backed clusters and representatives.
- `ADR 0008` defines local provenance events but does not require every domain record to synchronously generate a provenance event.

The conflict register records the exact unresolved Screening conflicts:

- `CF-021`: PHP Screening can read project works through `ScreeningWorkSourcePort` and optional `CorpusLockPolicy`; Nexus Web requires representative-aware locked snapshots and full-text handoff artifacts. C# must decide whether Screening consumes a Deduplication result, locked corpus snapshot, reviewable candidate set, or another stage-specific record, and must not consume raw Search traces directly.
- `CF-022`: PHP records LLM single/council outputs as screening verdicts, while C# product laws say LLM outputs remain proposals until authorized human action accepts them. C# must define model vote, suggestion, human decision, adjudication, actor, rationale, confidence, and source-decision boundaries.
- `CF-023`: PHP criteria hash recursively sorts associative keys and hashes JSON with SHA-256, but it has no `ADR 0002` digest scope, schema id, protocol binding, or stage-specific schema. C# must define criteria schema, canonical digest scope, stage binding, and comparator policy.
- `CF-024`: CLI file screening outputs and Nexus Web batches, assignments, conflicts, full-text item links, and audit rows are user-visible workflow evidence, but they are app projections unless a C# Screening ADR admits them as Core records.

This ADR defines the local C# Screening contract. It does not implement Screening, generate fixtures, claim PHP compatibility, add persistence, or change CLI/Web behavior.

## Decision

### 1. Screening Input Shape

C# Screening consumes a locked or reviewable candidate set, not raw Search traces.

The local Screening input record is conceptually:

```text
nexus.screening.candidate-set
```

version:

```text
1.0.0
```

A candidate set carries:

- `candidate_set_id`
- `schema_id`
- `schema_version`
- `source_kind`
- `source_refs`
- `locked`
- `created_from_dedup_result_id`, when applicable
- `created_from_dedup_result_digest`, when available
- `candidates`
- `unresolved_candidates`, when present
- `non_claims`

Supported source kinds for the local contract are:

- `deduplication-result`
- `locked-reviewable-candidate-set`

Raw Search traces and raw Search import outputs are not valid Screening inputs. They must first pass through Deduplication or another later accepted candidate-set construction rule.

Candidate records carry enough identity and evidence to bind back to prior stages:

- `candidate_id`
- optional `work_id` or primary ADR 0007 identifier
- optional `dedup_cluster_id`
- optional `dedup_representative_id`
- optional `dedup_member_ids`
- display fields needed for review, such as title and abstract
- source evidence references back to Search/import/Dedup records
- unresolved-candidate marker when stable identity is absent

Local Screening implementation may begin with in-memory candidate sets. General corpus snapshot equality remains outside this ADR.

### 2. Screening Stage Model

The local stage vocabulary is:

- `title_abstract`
- `full_text`
- `human_adjudication`

`title_abstract` is the first review stage over candidate title and abstract evidence.

`full_text` is a later review stage over full-text artifact evidence. This ADR recognizes the stage and decision shape but does not implement full-text retrieval or artifact inspection. Full-text artifact evidence must later bind through accepted artifact and raw-byte digest rules, not local paths.

`human_adjudication` records a human resolution of conflicting screening decisions. When it resolves a conflict from another stage, it must record the target stage and source decision ids.

### 3. Screening Criteria Model

Screening criteria are explicit, versioned, and digest-bound.

The local criteria record uses:

```text
schema_id = nexus.screening.criteria
schema_version = 1.0.0
```

Criteria records carry:

- `criteria_id`
- `criteria_version`
- `stage`
- `include`
- `exclude`
- optional `review_guidance`
- optional `full_text_requirements`
- optional `protocol_binding`
- optional `workflow_binding`
- `criteria_digest`

The criteria digest uses `ADR 0002` canonical JSON digest rules:

- digest scope: `canonical-json-record`
- schema id: `nexus.screening.criteria`
- schema version: `1.0.0`
- content: criteria fields that affect screening decisions

The digest value itself is excluded from its digest input. Criteria key order must not affect digest equality. List order remains semantic.

PHP's raw `sha256(json_encode(normalized_criteria))` hash is source evidence for PHP behavior only. It is not the local C# criteria digest rule.

### 4. Screening Decision Shape

A local final Screening decision record uses:

```text
schema_id = nexus.screening.decision
schema_version = 1.0.0
```

It carries:

- `decision_id`
- `candidate_set_id`
- `candidate_id`
- optional `work_id`
- optional `dedup_cluster_id`
- `stage`
- `verdict`
- `actor`
- `decided_at`
- `rationale`
- optional `confidence`
- `criteria_ref`
- `criteria_digest`
- `evidence_refs`
- optional `source_decision_ids`
- optional `source_suggestion_ids`
- optional `conflict_id`
- `decision_kind`
- `non_claims`

`actor` must identify a human actor for final scientific decisions. `decided_at` must be supplied by an injected clock or fixture harness in tests.

`rationale` carries:

- `reason`
- `evidence`
- `uncertainty`
- `exclusion_basis`

Confidence, when present, must be in `[0, 1]`. Confidence is not a substitute for rationale or human authority.

Decision records are append-only. A later decision does not mutate or delete earlier decisions.

### 5. Verdict Vocabulary

The local canonical verdict vocabulary is:

- `include`
- `exclude`
- `needs_review`

PHP and app display labels such as `maybe`, `unsure`, or `needs review` must map explicitly to `needs_review` before entering Core records. The local Core contract does not use `maybe` or `unsure` as canonical verdicts.

Conflict status and adjudication status are not verdicts. They are workflow or resolution state derived from decisions and conflict records.

### 6. Human Authority Boundary

Final Screening decisions require an identified human actor.

Automation, rules, LLMs, model councils, importers, plugins, and systems may create suggestions, validation evidence, or proposal records. They cannot create final Screening decisions.

AI/LLM suggestion records may carry:

- `suggestion_id`
- `candidate_set_id`
- `candidate_id`
- `stage`
- suggested verdict
- model/provider
- attempt
- confidence
- rationale
- prompt digest
- response digest
- usage and latency evidence
- error information
- criteria digest

AI suggestions may be cited by a later human decision through `source_suggestion_ids`, but they do not become final authority by themselves.

### 7. Conflict Detection

A Screening conflict exists when all of the following match:

- same `candidate_set_id`
- same `candidate_id`
- same target stage
- same `criteria_digest`
- at least two final human decisions
- different canonical verdict values
- no later adjudication decision has resolved the conflict

Different rationales, evidence, uncertainty, or confidence values do not create a conflict when verdict values match, but they must remain preserved.

Unresolved conflicts block downstream stage advancement for the affected candidate. A candidate with unresolved `title_abstract` conflict cannot become a final included/excluded title/abstract outcome, and cannot advance to full-text handoff except under a later explicit waiver or workflow policy.

### 8. Adjudication

Adjudication is an append-only human decision that resolves a conflict.

An adjudication record carries the normal Screening decision fields plus:

- `decision_kind = adjudication`
- `adjudicator_actor`
- `source_decision_ids`
- `resolved_conflict_id`
- target stage
- resolution rationale

Adjudication preserves prior reviewer decisions and AI suggestions. It does not overwrite them. For downstream consumption, the adjudication decision supersedes the unresolved conflict outcome by linking to the source decisions and conflict id.

### 9. Reviewer Assignment Boundary

Core decision semantics include:

- candidate set identity;
- final human decisions;
- AI/rule suggestions as proposal evidence;
- conflict detection over final human decisions;
- adjudication decision records;
- source decision links;
- criteria digest binding.

Core does not own reviewer assignment workflow in this ADR.

Reviewer pools, round-robin assignment, required reviewer counts, queue state, assignment statuses, batch statuses, reviewer availability, UI ordering, and notification behavior remain app workflow projections unless a later ADR admits them as Core records.

### 10. CLI and Web Boundary

CLI file-based screening and CLI LLM screening are app-local or future AI-governance evidence unless a later ADR explicitly adopts them.

Web batch rows, assignment rows, conflict rows, full-text item links, status badges, read models, and app audit rows are integration evidence, not Core authority.

App rows may reference or display Core Screening decision records later, but app row identity, app display labels, app local paths, and app audit rows do not become Core Screening identity or Gate 5 provenance events by default.

### 11. Locked Corpus and Candidate Set Boundary

Local Screening requires a locked or reviewable candidate set for final decisions.

For the first local implementation, accepted input should bind to one of:

- a Deduplication result id and digest;
- a locked reviewable candidate set id and digest;
- a later accepted snapshot/candidate contract.

Screening records must preserve candidate ids and source evidence bindings. They must not use raw Search trace ids alone as the screened candidate identity.

Unresolved no-id candidates may be screened only as unresolved candidates with source evidence. Screening them does not create stable scientific identity.

### 12. Provenance and Audit Relation

Final human Screening decisions and adjudication decisions are scientific decisions.

This ADR defines the domain record shape for those decisions. It does not require the Screening implementation to synchronously create Gate 5 provenance events.

A later provenance or application workflow may record `provenance-event` records that reference Screening decisions as subjects or outputs. When that happens, app audit rows remain projections unless transformed into `ADR 0008` provenance event records with event digests.

### 13. Error Categories and Validation Failures

Local Screening validation must expose stable categories. Required categories include:

- `invalid-screening-input`
- `raw-search-trace-not-screenable`
- `candidate-set-not-locked`
- `candidate-not-in-set`
- `unknown-screening-stage`
- `unknown-screening-verdict`
- `missing-human-actor`
- `automation-cannot-finalize`
- `missing-rationale`
- `invalid-confidence`
- `missing-criteria-digest`
- `invalid-criteria-digest-scope`
- `criteria-digest-mismatch`
- `duplicate-decision-id`
- `decision-not-append-only`
- `unresolved-conflict`
- `missing-source-decision`
- `adjudication-source-mismatch`
- `app-projection-not-core-authority`
- `full-text-artifact-required`
- `local-path-not-artifact-identity`

Tests and fixtures should assert categories instead of relying only on free-form exception text.

## Fixture and Comparator Consequences

Gate 9 local Screening fixtures should cover:

- candidate-set input from Dedup output;
- locked/reviewable candidate set input;
- raw Search trace rejection;
- criteria canonical digest;
- criteria key-order stability;
- stage-specific criteria;
- human include/exclude/needs-review decisions;
- missing actor and missing rationale rejection;
- confidence bounds;
- append-only decision history;
- AI suggestion not final;
- council conflict preserved as proposal evidence;
- conflict creation from reviewer disagreement;
- adjudication preserving source decision links;
- unresolved conflict blocking downstream handoff;
- full-text artifact-required planning;
- app assignment/conflict/audit projection non-authority.

Comparators must preserve:

- candidate set id;
- candidate id;
- stage;
- verdict;
- human actor;
- decided timestamp shape or fixed value;
- rationale;
- evidence refs;
- uncertainty;
- exclusion basis;
- criteria digest;
- source decision ids;
- source suggestion ids;
- conflict id;
- decision kind;
- append-only history.

Comparators may ignore generated ids, timestamps, and durations only when the fixture explicitly marks them as generated and non-semantic. They must not ignore actor, authority kind, criteria digest, conflict resolution links, candidate-set binding, or source decision links.

Generated PHP fixtures are still required before PHP compatibility can be claimed.

## Conflict Effect

`CF-021` is resolved for the local C# Screening contract: Screening consumes a Deduplication result or locked/reviewable candidate set and must not consume raw Search traces directly.

`CF-022` is resolved for the local C# Screening contract: final Screening decisions require an identified human actor; AI/model/rule outputs are proposal evidence only.

`CF-023` is resolved for the local C# Screening contract: criteria are schema-identified, versioned, stage-bound, and digest-bound using `ADR 0002` canonical JSON record rules rather than PHP's raw criteria hash.

`CF-024` is narrowed for Core: Web assignment/batch/conflict rows, CLI file screening output, full-text item links, and app audit rows remain app projections unless a later ADR admits them.

## Consequences

Positive consequences:

- Local C# Screening implementation can start from a clear human-authority contract.
- Screening no longer risks consuming raw Search traces before Deduplication.
- AI suggestions can be preserved without becoming scientific authority.
- Conflict and adjudication behavior is append-only and reconstructable.
- Criteria digest behavior is aligned with the deterministic kernel.

Negative consequences:

- The local contract intentionally diverges from PHP places that record LLM verdicts as verdicts.
- PHP compatibility remains unclaimed until generator-backed fixtures and comparators classify the authority and digest differences.
- App workflows still need later alignment before Web/CLI rows can be treated as Core records.
- Full-text artifact handling remains incomplete until full-text and artifact evidence gates define the required bindings.

## Alternatives Considered

### Consume Raw Search Traces Directly

Rejected. ADR 0010 and ADR 0012 deliberately separate Search from Deduplication. Screening raw traces would bypass duplicate evidence handling and representative/candidate-set formation.

### Preserve PHP LLM Verdicts As Final Decisions

Rejected. That would violate the product law that LLM outputs remain proposals until authorized human action accepts them.

### Use PHP Criteria Hash As The Local Digest

Rejected. The PHP hash is useful compatibility evidence, but it lacks explicit digest scope, schema id, schema version, and protocol/workflow binding.

### Adopt Web Assignment And Conflict Rows As Core Records

Rejected for this ADR. Web rows are valuable workflow evidence, but they carry app persistence and UI workflow concerns. Core admits decision and conflict semantics, not app row authority.

## Migration Effect

No persisted C# data is migrated by this ADR.

Future Screening implementation must treat any pre-ADR CLI file outputs, Web assignment rows, Web conflict rows, PHP LLM verdicts, or app audit rows as non-authoritative Core records until transformed into this contract.

Any imported PHP screening evidence must preserve whether a row was human, AI/model, council, rules, app workflow, or persistence projection.

## Fixture Effect

`docs/port/GOLDEN-FIXTURE-PLAN.md` and `docs/port/php-screening-fixture-plan.md` should list fixture families under this ADR.

Local fixtures may be hand-authored for the C# contract, but PHP compatibility fixtures must be generated from the pinned PHP source and include source commit, command, generator version, input digest, output digest, and comparator rules.

No fixture may imply live LLM calls, provider/network behavior, persistence/API/UI/cloud behavior, app authority, or PHP compatibility.

## Implementation Readiness

Local C# Screening implementation is ready to start after this ADR is merged.

Implementation is not ready for:

- PHP compatibility claims;
- generated PHP fixture comparison;
- persistence/API/UI/cloud behavior;
- app workflow authority;
- live LLM/provider/network behavior;
- AI governance implementation;
- full-text retrieval or artifact storage behavior.

## Reversal Conditions

Revise this ADR only if:

1. generated PHP fixtures prove a compatibility requirement that the project explicitly chooses over the human-authority boundary;
2. a later AI governance ADR defines a reviewed automation pathway that changes proposal acceptance rules;
3. a later snapshot/corpus ADR defines a stronger candidate-set identity or lock rule;
4. a later full-text ADR defines artifact-inspection records that require a versioned Screening decision schema change;
5. app-alignment work promotes specific CLI/Web fields into Core records with digest and migration rules.

## Explicit Claims Not Made

- no C# Screening implementation
- no generated PHP fixtures
- no PHP compatibility
- no persistence, API, UI, cloud, or app behavior
- no CLI/Web behavior changes
- no app behavior made authoritative
- no live LLM/provider/network behavior
- no AI governance implementation
- no full-text retrieval implementation
- no artifact storage behavior
- no Search or Deduplication behavior changes
- no bundle behavior change
- no blueprint conformance
