# PHP Screening Behavior Map

Status: reconnaissance and planning only. No C# Screening behavior is implemented by this document.

Pinned PHP source:

- Repository: `../core`
- Commit: `b24d0d71ec7b64003465182477e7edb7f49994f4`
- Source lock: `specs/SOURCE.lock.json`

Local note: the pinned PHP checkout was inspected at the locked commit. The checkout contains unrelated local `composer.json` and `composer.lock` modifications; those files were not edited for this reconnaissance.

## Sources Read

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0010-search-trace-and-plan-contract.md`
- `docs/adr/0011-search-import-source-contract.md`
- `docs/adr/0012-deduplication-evidence-and-cluster-contract.md`
- `docs/recon/apps/**`
- `specs/SOURCE.lock.json`
- `../core/src/Screening/**`
- `../core/tests/Unit/Screening/**`
- `../core/tests/Feature/Persistence/*Screening*`
- `../core/tests/Feature/Laravel/NexusScreenCommandTest.php`
- `../core/docs/v1.0/modules/05-core-screening-and-adjudication.md`
- `../core/docs/v1.0/tutorials/advanced-screening-adjudication-and-comparison.md`
- `../nexus-cli/app/Console/Commands/NexusScreen*.php`
- `../nexus-cli/tests/Feature/Commands/*Screen*`
- `../nexus-cli/docs/commands/nexus-screen*/README.md`
- `../nexus-web/app/Actions/Projects/*Screening*.php`
- `../nexus-web/app/Models/ProjectScreening*.php`
- `../nexus-web/tests/Feature/ProjectScreeningWorkflowTest.php`
- `../nexus-web/docs/workflow-6-title-abstract-screening.md`
- `../nexus-web/docs/workflow-8-full-text-screening.md`

## Behavior Summary

PHP Screening is a decision-recording and adjudication pipeline over project works. It can run LLM-based title/abstract screening, record model votes, aggregate council votes, append human adjudication verdicts, compare screening runs, and persist run/vote/decision rows through Laravel ports.

The PHP and app behavior cannot be ported mechanically into C# Core because Nexus product laws say:

- human screening decisions are scientific decisions;
- LLM output remains proposal evidence until an authorized human action accepts it;
- app assignment rows, conflict rows, audit rows, local files, and database rows are projections unless a Core contract admits them.

Implementation readiness is therefore **no** until a Screening ADR defines the Core decision record, authority boundary, criteria digest, input snapshot, and app projection boundary.

## 1. Screening Input Shape

PHP Core screens `ScreeningWork` values:

- `id`
- `title`
- `abstract`
- `year`
- `venue`
- `sourceProvider`
- `identifiers`
- `metadata`

`ScreeningWork` rejects blank `id` and blank `title`. `hasAbstract()` indicates whether abstract text is present.

Project-level screening uses `ScreenCorpusCommand`:

- `projectId`
- `criteria`
- optional `screeningRunId`
- `stage`, default `title_abstract`
- `mode`, default `llm_single`
- `model`
- `councilModels`
- `limit`
- `workIds`
- `queryIds`
- `name`
- `context`
- `temperature`
- `maxTokens`
- `storePrompt`
- `storeRawResponse`
- `continueOnFailure`

`ScreenCorpusHandler` loads works from `ScreeningWorkSourcePort::forProject(projectId, limit, workIds, queryIds)`, starts a run, screens each work, completes counts, and records per-work failures when continuation is allowed.

## 2. Relationship To Dedup Output And Locked Corpus

PHP Core can enforce a `CorpusLockPolicy`. When present, corpus screening asserts the project is locked for `CorpusOperation::SCREEN` and verifies explicit work ids belong to the locked project corpus.

Nexus Web is stricter than PHP Core:

- title/abstract screening starts from a locked corpus snapshot;
- the snapshot must be representative-aware;
- assignments are created from snapshot work ids;
- full-text screening starts only after title/abstract screening and full-text retrieval produce screenable artifacts.

C# Screening should not consume raw Search traces directly. The safe expected direction is:

- Search emits raw traces.
- Dedup consumes Search/import evidence and emits reviewable candidates.
- A later snapshot/lock rule freezes a reviewable candidate set.
- Screening consumes that locked/reviewable candidate set.

The exact C# input shape is unresolved and tracked as `CF-021`.

## 3. Screening Run Shape

PHP `ScreeningRun` carries:

- `id`
- `projectId`
- `stage`
- `mode`
- `status`
- `criteria`
- optional `name`
- `config`
- `source`
- `counts`
- `startedAt`
- `completedAt`
- optional explicit `criteriaHash`

Stages:

- `title_abstract`
- `full_text`
- `human_adjudication`

Modes:

- `rules`
- `llm_single`
- `llm_council`
- `human`

Statuses:

- `running`
- `completed`
- `failed`
- `cancelled`

`ScreeningRun::start(...)` sets status to `running` and stamps `startedAt` from PHP runtime time. C# fixtures must use injected clocks and stable ids.

## 4. Criteria Shape And Criteria Hash Behavior

PHP `ScreeningCriteria` stores arbitrary `array<string,mixed>` criteria. `fromArray()` recursively sorts associative object keys and preserves list order. `hash()` computes:

```text
sha256(json_encode(normalized_criteria))
```

This hash is stable for semantically equivalent associative key ordering, but it is not an `ADR 0002` digest envelope and has no explicit digest scope or schema id.

C# must define a Screening criteria digest contract before implementation. Expected decisions include:

- whether criteria are protocol-bound;
- whether criteria use `canonical-json-record` or a new `screening-criteria` digest scope;
- whether title/abstract and full-text stages have separate criteria schemas;
- which fields are required for schema closure.

This is tracked as `CF-023`.

## 5. Title/Abstract Screening Behavior

PHP Core title/abstract screening uses `ScreenWorkHandler`:

1. render a prompt from work, criteria, stage, and context;
2. call an `LlmClientPort` once per model;
3. convert each LLM response into a `ScreeningVote`;
4. aggregate council votes or convert the single vote to a verdict;
5. record the verdict;
6. record votes linked to the verdict.

The default prompt renderer tells the model to use only title and abstract, return JSON only, choose `needs_review` when title/abstract is not decisive, and choose `exclude` when clearly out of scope.

The required response schema is:

- `decision`: `include`, `needs_review`, or `exclude`
- `confidence`: number from 0 to 1
- `reason`
- `evidence`
- `uncertainty`
- `exclusion_basis`

Invalid model response content becomes a failed vote. A single-model failure becomes a `needs_review` verdict with confidence `0`.

## 6. Full-Text Screening Behavior

PHP Core defines a `full_text` stage, but the Core prompt renderer still instructs the model to use title and abstract. Full-text screening behavior is mainly expressed in Nexus Web:

- full-text screening candidates are derived from completed title/abstract screening and full-text retrieval results;
- only successful full-text artifacts with artifact paths can be assigned;
- reviewer decisions require confirmation that the artifact was inspected;
- exclude decisions require at least one exclusion basis;
- verdict metadata records source full-text batch and item ids plus artifact-inspected evidence.

This is app workflow evidence, not yet a C# Core full-text screening contract. Full text itself remains a future reconnaissance track.

## 7. Vote And Decision Shape

PHP decisions are tri-state:

- `include`
- `needs_review`
- `exclude`

`ScreeningDecision::included()` is true only for `include`.

PHP `ScreeningVerdict` carries:

- `id`
- optional `screeningRunId`
- `projectId`
- `workId`
- `stage`
- `decision`
- optional `confidence`
- `source`
- `rationale`
- optional `decidedBy`
- optional `decidedAt`
- optional `criteriaHash`
- `votes`
- `metadata`

`ScreeningRationale` carries:

- `reason`
- `evidence`
- `uncertainty`
- `exclusionBasis`

PHP `ScreeningVote` carries:

- `id`
- `screeningRunId`
- optional `screeningDecisionId`
- `projectId`
- `workId`
- `stage`
- `provider`
- `model`
- `attempt`
- optional `decision`
- optional `confidence`
- `rationale`
- `usage`
- optional `latencyMs`
- optional `error`
- optional `promptHash`
- optional `responseHash`
- optional stored `prompt`
- optional `rawResponse`

Vote confidence must be between `0` and `1` when present. Attempt must be at least `1`.

## 8. Council Aggregation

PHP `CouncilDecisionAggregator`:

- keeps successful and failed votes in the audit path;
- returns `needs_review` with confidence `0` if no model produced a valid vote;
- returns `needs_review` when successful votes include both `include` and `exclude`;
- returns `needs_review` for split votes without at least two votes for the winner;
- otherwise chooses the majority decision;
- averages winner confidence for successful majority decisions;
- multiplies confidence by `0.95` when any model failures occurred;
- records uncertainty such as `council_include_exclude_conflict`, `council_split_vote`, and `model_failure:{model}`.

This is useful proposal aggregation evidence. It is not final C# scientific authority unless a later ADR explicitly defines how AI suggestions are reviewed and accepted.

## 9. Human Adjudication Behavior

PHP `AdjudicateScreeningDecisionsCommand` requires:

- `projectId`
- `actorId`
- `stage`
- `criteriaHash`
- non-empty adjudication decisions

Each `HumanAdjudicationInput` requires:

- `workId`
- decision
- non-empty `reason`
- optional evidence, uncertainty, exclusion basis
- optional source decision ids
- confidence from `0` to `1`, default `1.0`

The handler requires a locked project, verifies work membership, starts or reuses a human run, records human verdicts, preserves source decision ids in metadata, and completes counts. Tests verify adjudication appends decisions instead of overwriting prior LLM decisions.

Human adjudication is the closest PHP behavior to the C# authority model, but C# still needs an ADR to define decision digest, actor identity, provenance binding, conflict semantics, and how earlier AI/model votes remain proposal evidence.

## 10. Run Comparison Behavior

PHP `CompareScreeningRunsHandler` compares two runs:

- requires both run ids;
- rejects same run id;
- requires both runs to belong to the requested project;
- optionally requires matching stage;
- indexes the first verdict per work id in each run;
- sorts combined work ids;
- reports agreement, disagreement, transition counts, missing-in-baseline, missing-in-candidate, and optional per-work rows;
- returns a reference run id when one compared run is human.

Comparison is a reporting/review aid. It does not mutate decisions or resolve conflicts.

## 11. CLI Behavior

`nexus-cli` has three screening command surfaces:

- `nexus:screen`
- `nexus:screen-adjudicate`
- `nexus:screen-compare`

`nexus:screen` has two modes:

- database-backed project mode delegates to PHP Core `ScreenCorpusHandler`;
- file-based mode reads run and criteria files, performs deterministic keyword/year screening, optionally calls an app-bound LLM callable, and writes `storage/screens/{run_id}.json`.

The file-based mode uses fields such as `included`, `deterministic_decision`, `llm_include`, `llm_confidence`, `final_decision_source`, `llm_prompt`, and `llm_response`. This is app-local behavior and must not become C# Core authority without a later contract.

`nexus:screen-adjudicate` records human adjudication from YAML or JSON files through PHP Core. `nexus:screen-compare` compares persisted runs and can output JSON.

## 12. Web Behavior

Nexus Web adds workflow behavior around PHP Core verdicts:

- starts title/abstract screening batches from representative-aware locked snapshots;
- requires reviewer membership and reviewer count;
- creates round-robin assignments;
- records human reviewer decisions with rationale;
- marks assignments resolved when reviewers agree;
- creates conflict rows when reviewer decisions differ;
- resolves conflicts through an adjudicator action that records a human-adjudication verdict;
- records audit events for batch start, decision recording, conflict creation, and conflict resolution;
- derives full-text screening from completed title/abstract screening and successful full-text artifacts.

These rows are product workflow and app integration evidence. They are not automatically C# Core authority. This boundary is tracked as `CF-024`.

## 13. Persistence Behavior

PHP Laravel migrations and repositories persist:

- screening runs;
- screening decisions;
- screening votes;
- LLM prompt/response hashes and optional raw content;
- Web screening batches;
- Web assignments;
- Web conflicts.

C# Screening reconnaissance does not admit persistence, EF Core, API, UI, cloud, or job behavior. Persistence remains future app/infrastructure work.

## Behaviors To Port Locally After An ADR

Likely local C# Screening behavior should include:

- explicit Screening run and decision records;
- title/abstract and full-text stage identifiers;
- decision values `include`, `needs_review`, and `exclude`;
- required actor/rationale for human decisions;
- confidence validation when present;
- criteria digest binding;
- immutable append-only decisions rather than silent overwrites;
- source decision links for adjudication;
- conflict/review evidence without mutating prior decisions;
- comparison output between runs;
- linkage to locked/reviewable post-Dedup candidate sets;
- provenance-ready actor and evidence references.

## Intentional Incompatibilities To Consider

Likely C# differences:

- LLM verdicts should be suggestions or proposal evidence, not final scientific decisions.
- Criteria digest should use an explicit canonical digest scope rather than PHP's unsuffixed SHA-256 hash.
- Runtime timestamps and generated ids must be fixture-injected.
- CLI file-based screening should remain app-local unless a later ADR admits it.
- Web batch/assignment/conflict rows should remain app workflow projections unless a later ADR admits them.
- Full-text artifact paths must not become scientific identity; artifact evidence must use accepted artifact/digest rules.
- No PHP compatibility claim can be made without generated fixtures and comparators.

## Required C# Decisions Before Implementation

Implementation readiness is **no** until an ADR resolves:

- Screening input shape and locked/reviewable candidate set boundary;
- decision authority and AI proposal handling;
- criteria schema and digest scope;
- run, vote, decision, conflict, and adjudication record shape;
- actor/rationale/confidence requirements;
- title/abstract versus full-text stage boundaries;
- app assignment/conflict projection boundary;
- provenance and protocol/workflow binding expectations;
- fixture and comparator policy.

## Explicit Non-Claims

- no C# Screening implementation
- no generated PHP fixtures
- no PHP compatibility
- no Screening ADR or contract accepted
- no persistence/API/UI/cloud behavior
- no CLI/Web behavior made authoritative
- no full-text retrieval implementation
- no AI governance behavior
- no live provider/network behavior
- no Search or Deduplication behavior change
- no bundle behavior change
- no blueprint conformance
