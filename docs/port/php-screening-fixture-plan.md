# PHP Screening Fixture And Comparator Plan

Status: H28 generated subset implemented; broader catalog remains planned.

Pinned PHP source:

- Repository: `../core`
- Commit: `b24d0d71ec7b64003465182477e7edb7f49994f4`
- Source lock: `specs/SOURCE.lock.json`

## Scope

This plan defines the Screening fixture and comparator catalog. H28 implements the deterministic domain subset in `fixtures/php-golden/screening-fulltext/v1/`; PHP tests, CLI files, Web workflow rows, and app docs remain source evidence only.

C# Screening may be implemented locally after `ADR 0013` is merged. PHP compatibility still requires generated fixtures and comparators.

## Fixture Families

### Screening Input And Lock Boundary

Planned fixtures:

- `screening-input-dedup-result-candidates.json`
- `screening-input-locked-candidate-set.json`
- `screening-input-raw-search-trace-rejected.json`
- `screening-unlocked-corpus-rejected.json`
- `screening-work-not-in-candidate-set-rejected.json`

Coverage:

- Screening consumes a post-Dedup reviewable candidate set or locked snapshot, not raw Search traces directly.
- Explicit work ids must belong to the accepted candidate set.
- Unlocked or mutable corpus inputs are rejected when the accepted policy requires a lock.
- No-id candidates can be represented only under `ADR 0013` as unresolved candidates with source evidence; Screening them does not create stable scientific identity.

### Criteria And Digest

Planned fixtures:

- `screening-criteria-canonical-digest.json`
- `screening-criteria-key-order-stable.json`
- `screening-criteria-stage-specific.json`
- `screening-criteria-digest-scope-negative.json`

Coverage:

- Criteria digest is deterministic.
- Associative key order does not change digest.
- List order remains semantic.
- Title/abstract and full-text criteria are stage-bound.
- PHP raw SHA-256 criteria hash is classified before compatibility is claimed.

### Human Decision Records

Planned fixtures:

- `screening-human-include-decision.json`
- `screening-human-exclude-decision.json`
- `screening-human-needs-review-decision.json`
- `screening-human-missing-actor-negative.json`
- `screening-human-missing-rationale-negative.json`
- `screening-confidence-bounds-negative.json`
- `screening-append-only-decision-history.json`

Coverage:

- Decision values are `include`, `needs_review`, and `exclude`.
- Human decisions require actor and rationale.
- Confidence is either omitted under policy or between `0` and `1`.
- New decisions append and do not silently overwrite earlier decisions.

### LLM Suggestion And Council Evidence

Planned fixtures:

- `screening-ai-single-suggestion.json`
- `screening-ai-single-invalid-needs-review.json`
- `screening-council-majority-suggestion.json`
- `screening-council-conflict-needs-review.json`
- `screening-council-all-failed-needs-review.json`
- `screening-ai-suggestion-not-final.json`

Coverage:

- PHP single-model and council outputs are preserved as proposal evidence.
- Model votes preserve provider, model, attempt, prompt/response hash where present, confidence, rationale, usage, latency, and errors.
- Include/exclude council conflict routes to `needs_review` proposal evidence.
- AI/model suggestions do not become final C# scientific decisions without authorized human action.

### Human Adjudication And Conflicts

Planned fixtures:

- `screening-human-adjudication-verdict.json`
- `screening-adjudication-source-decision-links.json`
- `screening-conflict-created-from-disagreement.json`
- `screening-conflict-resolved-by-human.json`
- `screening-unresolved-conflict-blocks-handoff.json`
- `screening-conflict-resolution-missing-reason-negative.json`
- `screening-adjudication-wrong-project-or-stage-negative.json`

Coverage:

- Conflicting decisions preserve source decision ids.
- Human adjudication records actor, rationale, stage, criteria digest, and source decision links.
- Prior decisions remain reconstructable after adjudication.
- Wrong project or wrong stage comparisons are rejected.

### Run Lifecycle And Comparison

Planned fixtures:

- `screening-run-lifecycle-counts.json`
- `screening-run-failure-counts.json`
- `screening-run-comparison-agreement.json`
- `screening-run-comparison-disagreement.json`
- `screening-run-comparison-missing-work.json`
- `screening-run-comparison-human-reference.json`

Coverage:

- Runs track total, included, needs-review, excluded, and failed counts.
- Comparison reports transitions, missing works, agreement/disagreement rates, and human reference run id when applicable.
- Runtime duration and generated ids are fixture-pinned or ignored by comparator policy.

### Full-Text Screening Planning

Planned fixtures:

- `screening-full-text-artifact-required.json`
- `screening-full-text-artifact-inspected.json`
- `screening-full-text-exclude-requires-basis.json`
- `screening-full-text-stage-deferred.json`

Coverage:

- Full-text screening requires successful full-text artifact evidence when in scope.
- Artifact paths are app references and not scientific identity.
- Full-text artifact bytes must later bind through artifact digest rules.
- Full-text screening remains deferred until a full-text contract and artifact evidence policy exist.

### App Projection Boundary

Planned fixtures:

- `screening-app-assignment-projection-not-authority.json`
- `screening-app-conflict-row-projection-not-authority.json`
- `screening-cli-file-output-not-core-authority.json`
- `screening-web-audit-row-not-provenance-event.json`

Coverage:

- Web assignment rows, batch rows, conflict rows, and audit rows are app workflow evidence.
- CLI file-based screening output is app-local unless a later ADR admits it.
- App audit rows are not Gate 5 provenance events.
- App display labels such as "maybe" must map explicitly to Core decision values before implementation.

## Negative Fixture Catalog

Required negative cases:

- raw Search trace passed directly to Screening when locked candidate set is required
- unlocked corpus
- work not in accepted candidate set
- missing criteria digest
- wrong criteria digest scope
- unknown stage
- unknown decision value
- missing human actor
- missing human rationale
- confidence below `0` or above `1`
- LLM suggestion treated as final authority
- decision overwritten silently
- adjudication without source decision links when policy requires them
- conflict resolution without reason
- wrong project or stage in run comparison
- full-text screening without successful full-text artifact
- full-text exclusion without exclusion basis
- local file path used as artifact identity
- Web assignment/conflict row treated as Core authority
- CLI screen file treated as Core authority
- PHP compatibility claimed without generated PHP fixtures

## Comparator Plan

### General

- Compare semantic fields, not PHP object identity.
- Ignore generated ids only when fixture policy says they are not semantic.
- Ignore runtime timestamps and durations unless fixtures inject fixed values.
- Do not ignore actor ids, stage, decision, criteria digest, rationale, evidence, source decision ids, candidate bindings, or authority markers.

### Criteria Comparator

Compare:

- normalized criteria content;
- stage;
- digest algorithm and scope;
- key order stability;
- list order preservation.

Classify PHP's raw `sha256(json_encode(...))` criteria hash separately from C# canonical digest behavior until an ADR resolves it.

### Decision Comparator

Compare:

- work/candidate id;
- stage;
- decision;
- source;
- actor or model;
- confidence;
- rationale reason;
- evidence;
- uncertainty;
- exclusion basis;
- criteria digest;
- source decision ids;
- append-only history.

### LLM Vote Comparator

Compare:

- provider;
- model;
- attempt;
- succeeded/failed status;
- decision and confidence when present;
- prompt hash;
- response hash;
- usage;
- error category;
- vote-to-verdict link.

Do not treat model votes as final human decisions.

### Conflict And Adjudication Comparator

Compare:

- conflicting decision ids;
- conflict status;
- human adjudicator actor;
- resolution decision;
- resolution rationale;
- source decision id preservation;
- prior decision preservation.

### App Projection Comparator

Compare app workflow fixtures separately:

- batch id;
- assignment id;
- conflict id;
- snapshot id;
- reviewer ids;
- audit event type;
- full-text item id.

These must be marked app projection unless a later ADR makes them Core authority.

## Generator Requirements

The PHP fixture generator must:

1. run against commit `b24d0d71ec7b64003465182477e7edb7f49994f4`;
2. record exact command and environment;
3. inject fixed ids, clocks, and durations where equality matters;
4. avoid live LLM calls and live providers;
5. use fake LLM clients or recorded deterministic payloads;
6. record input digest and output digest;
7. preserve raw votes, verdicts, source decisions, and criteria hash material;
8. classify every difference as equivalent serialization, intentional incompatibility, PHP defect, C# defect, or unresolved conflict.

## Implementation Readiness

Implementation readiness: **yes for local C# Screening implementation after ADR 0013 is merged**.

Resolved local decisions:

- Screening input shape and lock boundary (`CF-021`);
- human decision authority and AI proposal handling (`CF-022`);
- Screening criteria schema and digest contract (`CF-023`);
- app assignment/conflict/adjudication boundary (`CF-024`).

Implemented H28 evidence:

- generated verdict/stage vocabulary, criteria ordering, confidence-bound, council, and authority-boundary observations;
- semantic C# comparators for the shared subset;
- explicit classification of PHP `NAN` confidence acceptance as a PHP defect;
- no broad PHP Screening or PHP screening-authority compatibility claim.

## Explicit Non-Claims

- no broad PHP Screening compatibility beyond H28
- no PHP council verdict as C# final authority
- no Search or Deduplication behavior change
- no full-text retrieval implementation
- no persistence schema
- no API/UI/cloud behavior
- no AI governance behavior
- no app behavior made authoritative
