# Feature Expansion Priority Plan - 2026-07-14

Status: active successor roadmap after Hardening 30.

Current state: FE-01 through FE-08 are complete locally. FE-09A's retained-local-
fixture contract and Crossref normalization adapter are complete locally under
ADR 0039; hosted CI and merge remain pending on a stacked branch. FE-09D host,
credential, and runtime-evidence policy is accepted under ADR 0040. FE-09F
OpenAlex/Semantic Scholar transport is complete locally; OpenAlex live smoke
passed, while authenticated S2 bulk/batch smoke remains credential-blocked.
Remaining FE-09 slices and FE-10 through FE-12 stay dependency-ordered.

## Operating Decision

Hardening Phases 1-7 and Hardening 30 are complete. The next product objective is
to turn the current evidence-inspection alpha into a local, human-authorized,
reconstructable review workflow.

The architecture rule for this expansion is:

> Strict Core invariants, flexible workflow profiles.

Scientific identity, protocol binding, human authority, provenance, immutable
snapshots, invalidation, and reproducibility remain strict. A workflow profile
may choose a systematic review, rapid review, scoping review, evidence map, or
lighter personal workflow without weakening those underlying records.

The first expansion must close the human decision loop. Live providers, a
product desktop shell, plugin execution, AI acceptance, and cloud persistence
remain later work because none of them can safely create scientific authority
before actor-bound decisions and corpus snapshots exist.

## Authority And Inputs

Use this source order while planning and implementing every feature:

1. approved files in `specs/`;
2. accepted ADRs in `docs/adr/`;
3. golden fixtures in `fixtures/`;
4. observable behavior of the pinned PHP source;
5. current C# behavior;
6. discovery notes, blueprint material, app behavior, and this roadmap.

Key current inputs are:

- [Hardening completion](../reviews/2026-07-11-hardening-plan/README.md)
- [Open conflict register](../port/OPEN-CONFLICTS.md)
- [Deduplication contract](../adr/0012-deduplication-evidence-and-cluster-contract.md)
- [Screening contract](../adr/0013-screening-decision-and-conflict-contract.md)
- [Full Text contract](../adr/0014-fulltext-acquisition-artifact-and-extraction-contract.md)
- [Read-only AppServices boundary](../adr/0015-app-services-readonly-workspace-composition.md)
- [Desktop and ResearchWorkspace boundary](../adr/0016-desktop-shell-and-researchworkspace-boundary.md)
- [Corpus snapshot compatibility boundary](../adr/0026-phase-7-corpus-lock-snapshot-compatibility-boundary.md)
- [Network and reporting evidence boundary](../adr/0027-phase-7-citation-network-dissemination-evidence-boundary.md)

`CF-005` remains blocking for broad blueprint adoption. A feature gate may adopt
the minimum schema and behavior it needs through an accepted ADR, but it must not
promote the whole blueprint, its draft method packs, or its examples into Core
authority.

## Priority Summary

| Priority | Feature | Minimum dependency | Primary product result |
| --- | --- | --- | --- |
| FE-01 | Decision and snapshot authority | Hardening complete | Trustworthy local scientific mutation foundation |
| FE-02 | Executable Deduplication review | FE-01 | Human-resolved duplicate candidates and locked membership |
| FE-03 | Workflow execution journal | FE-01 and one concrete FE-02 decision path | Replayable protocol-bound work state |
| FE-04 | Title and abstract Screening | FE-01 through FE-03 | Human screening, conflicts, and adjudication |
| FE-05 | Local Full Text workflow | FE-04 handoff | Digest-bound local artifacts and full-text decisions |
| FE-06 | Reporting, audit bundle, and Rapid Review profile | FE-01 through FE-05 | Verifiable end-to-end local review output |
| FE-07 | Extraction, appraisal, and synthesis | FE-03 through FE-06 | Structured evidence analysis records |
| FE-08 | Local product desktop shell | Stable FE-01 through FE-06 commands | Usable local operator workflow |
| FE-09 | Live providers and citation networks | Stable local workflow and legal/network ADRs | Reproducible external acquisition and graph evidence |
| FE-10 | Plugin runtime | Staged outputs, validation, and capability ADR | Controlled extension execution |
| FE-11 | Governed AI | FE-01 human authority plus task-governance ADR | Evidence-bound proposals with human acceptance |
| FE-12 | Database, API, cloud, and multi-user operation | Stable local authority and application contracts | Deployable collaborative infrastructure |

Recommended delivery is sequential. Later design work may run in parallel, but a
later implementation gate must not bypass an unfinished minimum dependency.

## Rules For Every Feature Gate

Every FE item must be opened as one or more coherent gates. Each gate must have:

1. an accepted ADR or an explicit statement that existing ADRs fully authorize
   the behavior;
2. a single scientific behavior and one primary module owner per work package;
3. schema identifiers, versions, digest scopes, and rehydration rules for every
   authority-bearing record;
4. fixtures for valid behavior, malformed input, stale authority, tampering,
   invalid transitions, and automation attempting to exceed its authority;
5. architecture tests proving domain projects remain free of UI, storage,
   provider, host, and concrete model dependencies;
6. application and persistence adapters that depend inward on domain contracts;
7. explicit non-claims for deferred providers, PHP parity, AI, cloud, security,
   and production readiness;
8. a completion evidence report bound to the tested commit;
9. green Windows and Linux CI before protected merge;
10. an updated conflict register and operating plan.

No feature may use an editable file path, database row, UI object, provider
payload, or current projection as scientific identity. Stable identifiers and
content digests remain the identity boundary.

## FE-01 - Decision And Snapshot Authority

### Outcome

Create the minimum trustworthy foundation for a human action to change
scientific state. The feature must define an actor-bound, append-only decision
record and an immutable corpus or candidate snapshot derived from exact evidence.

### Why It Is First

The current system can display review-required evidence but cannot durably record
an authorized final action. `ADR 0012` deliberately leaves Deduplication review
execution unresolved, and `ADR 0026` deliberately leaves general corpus snapshot
identity and equality unresolved. Every later feature needs both concepts.

### Required Contract

The decision record must bind at least:

- decision id and schema version;
- identified human actor, role, and authority source;
- action type and policy identifier;
- target kind, stable target id, and target content digest;
- source protocol version and exact protocol-content digest where applicable;
- source result or snapshot id and digest;
- ordered evidence references and their digest scopes;
- rationale and structured reason code where required;
- UTC timestamp supplied through an injected clock;
- superseded decision id when correcting an earlier action;
- invalidation effects and affected downstream record references;
- canonical decision digest.

The snapshot contract must define:

- schema id, schema version, snapshot id, and content digest;
- deterministic membership representation and ordering rules;
- stable work or candidate identity;
- treatment of no-id and unresolved candidates;
- representative and membership relationships without deleting raw sightings;
- exact source Deduplication result and decision-set bindings;
- creating human actor and creation timestamp;
- immutability, equality, supersession, and invalidation semantics;
- persistence-independent verification and rehydration.

### Work Packages And Owners

- FE-01.1 authority ADR and threat model: architecture and governance docs owner.
- FE-01.2 first decision record shape: `NexusScholar.Deduplication` owner.
- FE-01.3 snapshot domain location and contract: architecture owner; the ADR must
  choose either a focused new corpus module or an existing domain owner before
  code is created.
- FE-01.4 provenance event binding: `NexusScholar.Provenance` owner.
- FE-01.5 atomic local append and generation storage:
  `NexusScholar.ResearchWorkspace` owner.
- FE-01.6 read-only command projection: `NexusScholar.AppServices` owner.

### Fixtures And Negative Cases

- valid human decision over an exact target and source generation;
- missing, blank, unknown, or automation actor rejection;
- stale source generation and stale snapshot rejection;
- target id matches but target digest differs;
- duplicate snapshot membership and conflicting representative relation;
- unresolved no-id candidate is silently omitted;
- correction attempts to mutate rather than supersede a decision;
- tampered decision, membership, evidence, or provenance digest;
- crash between staging and promotion;
- two writers attempt to append against the same expected generation.

### Exit Criteria

- accepted ADR resolves the named scope of `CF-014` and any affected app boundary;
- schemas and digest scopes are explicit and versioned;
- verified rehydration rejects all malformed authority-bearing data;
- append-only decisions and immutable snapshots survive save, reopen, and verify;
- local writes use expected-generation CAS and atomic promotion;
- downstream invalidation is deterministic and tested;
- no database, API, cloud, provider, UI command, or AI acceptance is introduced.

## FE-02 - Executable Deduplication Review

### Outcome

Turn the current read-only Deduplication queue into the first complete human
decision slice. A reviewer can accept a proposed merge, keep records separate,
or mark the case unresolved, and the system produces a new immutable candidate
snapshot without destroying source evidence.

### Why It Is Second

This is the narrowest user-visible workflow already prepared by APP-01. It tests
FE-01 against a real scientific mutation before a generic execution framework or
desktop shell is built.

### Required Behavior

- decisions operate on the exact review candidate and Deduplication result digest;
- accepted merges preserve every source sighting and explain membership;
- keep-separate decisions prevent the same evidence from being auto-merged later
  unless a newer human decision supersedes them;
- unresolved decisions remain visible and block a falsely final corpus lock;
- fuzzy, no-id, source-specific, and policy-override candidates require review;
- title-only equality never becomes automatic merge authority;
- representative selection is deterministic or explicitly human-overridden under
  a recorded policy;
- every accepted action appends provenance and creates a successor snapshot;
- Screening, Full Text, report, and bundle records are not admitted FE-02
  invalidation kinds and must not be claimed current against a successor snapshot;
  explicit stale records move to their later domain gates.

### Work Packages And Owners

- FE-02.1 domain commands and validation: `NexusScholar.Deduplication` owner.
- FE-02.2 decision-to-snapshot reducer: the FE-01 snapshot module owner.
- FE-02.3 transactional command handler: `NexusScholar.ResearchWorkspace` owner.
- FE-02.4 CLI interaction: `NexusScholar.Cli` owner.
- FE-02.5 UI-neutral command and result contracts:
  `NexusScholar.AppServices` owner.

### Fixtures And Negative Cases

- exact-id duplicate accepted;
- fuzzy candidate kept separate;
- no-id candidate marked unresolved;
- repeated identical command is idempotent;
- conflicting second decision requires supersession rather than overwrite;
- candidate belongs to a stale Deduplication result;
- evidence digest is missing or mismatched;
- automatic or AI actor attempts a final merge;
- partial write leaves the prior generation authoritative;
- a downstream artifact is incorrectly claimed current against the successor.

### Exit Criteria

- APP-01 placeholders are replaced only for the admitted commands;
- the CLI can preview effects before confirmation;
- reopen and verify reconstructs the decision and resulting snapshot;
- raw Search/import evidence remains unchanged and addressable;
- every mutation has actor, rationale, evidence, provenance, and generation links;
- no generic UI shell, database, live provider, or AI behavior is added.

## FE-03 - Workflow Execution Journal

### Outcome

Represent actual protocol-bound work as replayable execution records rather than
as CLI control flow or current-state flags. The journal records work state,
attempts, human tasks, approvals, evidence, outputs, failures, and invalidation.

### Why It Is Third

FE-02 provides a concrete decision path from which the minimum reusable execution
semantics can be extracted. Building a generic runtime before that evidence would
risk speculative infrastructure.

### Required Behavior

- execution instances bind an approved protocol and compiled workflow digest;
- node states include at least pending, ready, active, blocked, completed, failed,
  invalidated, and superseded;
- transitions are append-only records with expected prior state;
- attempts bind inputs, outputs, responsible agent, timestamps, and errors;
- human tasks identify the required role and cannot be completed by automation;
- approval gates reuse accepted human-authority rules;
- invalidation propagates from changed protocol, snapshot, decision, or artifact;
- replay produces the same current projection from the same journal;
- retries are explicit attempts, not overwritten status fields.

### Work Packages And Owners

- FE-03.1 execution ADR and state machine: `NexusScholar.Workflow` owner.
- FE-03.2 execution record assembly boundary: architecture owner; a separate
  `NexusScholar.WorkflowExecution` project is preferred if it keeps compiler
  contracts independent from mutable execution history.
- FE-03.3 provenance projection: `NexusScholar.Provenance` owner.
- FE-03.4 local journal persistence: `NexusScholar.ResearchWorkspace` owner.
- FE-03.5 command orchestration: `NexusScholar.AppServices` owner.

### Fixtures And Negative Cases

- valid multi-node execution with a human approval gate;
- execution against a draft or stale protocol;
- duplicate completion, completion before start, and transition after invalidation;
- output digest does not resolve;
- retry overwrites the prior attempt;
- automation completes a human task;
- replay order is changed or an event is removed;
- concurrent writers advance the same node;
- crash during a transition append.

### Exit Criteria

- state transition table and invalidation rules are accepted and exhaustive;
- replay and current projection are deterministic;
- no runtime state is hidden only in CLI memory or UI state;
- execution records remain domain and persistence independent;
- no background scheduler, distributed queue, plugin host, or AI runner is added.

## FE-04 - Title And Abstract Screening

### Outcome

Conduct title and abstract screening over an immutable candidate snapshot with
identified human decisions, protocol-bound criteria, independent review,
conflict detection, and adjudication.

### Why It Is Fourth

The Screening domain already defines strong local authority rules. FE-04 supplies
the missing durable conduct path after Deduplication membership and workflow
execution have stable identities.

### Required Behavior

- Screening consumes a locked candidate snapshot, never raw Search traces;
- criteria bind an approved protocol and exact protocol-content digest;
- decisions bind candidate, stage, criterion, actor, rationale, and evidence;
- include, exclude, and needs-review are distinct outcomes;
- exclusion reasons use protocol-defined reason codes plus optional explanation;
- independent reviewer assignments cannot be satisfied by one actor twice;
- conflicting decisions create a conflict record and block downstream handoff;
- adjudication is a new authorized decision, not destructive conflict editing;
- changes to criteria, protocol, candidate snapshot, or source evidence invalidate
  affected decisions and handoffs;
- AI or rules may later propose, but cannot finalize, a verdict.

### Work Packages And Owners

- FE-04.1 durable decision and conflict application service:
  `NexusScholar.Screening` owner.
- FE-04.2 workflow tasks and assignment projection: FE-03 execution owner.
- FE-04.3 atomic workspace storage: `NexusScholar.ResearchWorkspace` owner.
- FE-04.4 CLI review flow: `NexusScholar.Cli` owner.
- FE-04.5 read models: `NexusScholar.AppServices` owner.

### Fixtures And Negative Cases

- single-review include and exclude;
- dual-independent agreement;
- dual-review conflict and authorized adjudication;
- same actor attempts both independent reviews;
- missing rationale or unknown reason code;
- decision references stale criteria or candidate snapshot;
- full-text-only reason is used at title/abstract stage;
- unresolved conflict is passed to Full Text;
- an AI proposal is submitted as a final human decision;
- reopening the workspace changes decision order or current projection.

### Exit Criteria

- complete title/abstract conduct can be reconstructed from local records;
- unresolved conflicts visibly block handoff;
- decision history remains append-only through correction and adjudication;
- snapshot and protocol changes invalidate deterministically;
- app batch, assignment, and row projections are not promoted to Core identity.

## FE-05 - Local Full Text Workflow

### Outcome

Accept user-supplied local full-text evidence, preserve exact artifact identity,
derive auditable extraction records, and conduct human full-text screening. The
initial slice remains local and no-network.

### Why It Is Fifth

Full Text must consume an authorized Screening handoff. Retrieval automation or
parsing before stable candidate and decision authority would create derived data
without a trustworthy scientific target.

### Required Behavior

- intake is allowed only for candidates admitted by the Screening handoff policy;
- a path is an input reference, never artifact identity;
- exact bytes are retained or resolvable by a `raw-artifact-bytes` digest;
- acquisition records capture actor, timestamp, source type, access notes, and
  legal or availability status;
- every extraction attempt records parser id/version/configuration, input digest,
  output digest, warnings, failure category, and completeness status;
- raw artifact evidence remains available when extraction fails or is partial;
- extracted text or structure is derived evidence and cannot replace raw bytes;
- full-text decisions bind the exact artifact or extraction evidence used;
- changes to artifact, extraction, criteria, or protocol invalidate decisions;
- manual text/XML intake may precede PDF parsing; deterministic PDF parsing needs
  a dedicated ADR and justified library dependency; OCR remains later work.

### Work Packages And Owners

- FE-05.1 intake and evidence rules: `NexusScholar.FullText` owner.
- FE-05.2 local source adapter: `NexusScholar.ResearchWorkspace` owner.
- FE-05.3 parser adapter boundary: `NexusScholar.FullText` owner, with concrete
  parsers isolated in an outward infrastructure project.
- FE-05.4 full-text Screening decisions: `NexusScholar.Screening` owner.
- FE-05.5 operator commands: `NexusScholar.AppServices` owner.

### Fixtures And Negative Cases

- valid local PDF, XML, and text intake;
- same path contains different bytes;
- same bytes arrive through different paths;
- artifact is assigned to the wrong candidate or acquisition attempt;
- parser output digest does not match reconstructed output;
- parser returns partial text with warnings;
- encrypted, malformed, or unsupported artifact;
- extraction failure is silently treated as an exclusion;
- artifact changes after a full-text decision;
- network URL or paywall-bypass source is submitted to the local gate.

### Exit Criteria

- local intake, verify, reopen, and decision replay pass on Windows and Linux;
- parser failures and partial results remain explicit evidence;
- raw bytes and every derived representation have distinct identities;
- no live retrieval, scraping, paywall bypass, OCR, cloud storage, or provider SDK
  is introduced under this gate.

## FE-06 - Reporting, Audit Bundle, And Rapid Review Profile

Status: complete under ADR 0033. The accepted implementation order and evidence
are recorded in `docs/gates/FE-06-REPORTING-AUDIT-BUNDLE-RAPID-REVIEW.md`.

### Outcome

Produce a portable, verifiable account of the local review slice from protocol
through Full Text. Add the first flexible workflow profile only after all counts,
decisions, artifacts, and deviations can be reconstructed.

### Why It Is Sixth

Reports and bundles are trustworthy only when they reference immutable snapshots
and actor-bound decisions. A method profile should configure proven behavior, not
define authority through an editable template.

### Required Behavior

- deterministic flow counts are computed from immutable snapshots and decisions;
- exclusion reasons, conflicts, adjudications, amendments, waivers, and deviations
  are represented from canonical records;
- reports cite protocol, workflow, snapshot, artifact, decision, and provenance
  identifiers and digests;
- export history is append-only and records exact source generation;
- bundle verification detects missing, extra, altered, or mis-scoped artifacts;
- the Rapid Review profile declares each shortcut, scientific consequence,
  mitigation, required approval, and reporting disclosure;
- the profile cannot disable actor identity, provenance, snapshot immutability,
  evidence identity, or invalidation;
- generated narrative is a presentation, not canonical scientific state.

### Work Packages And Owners

ADR 0033 and `docs/gates/FE-06-REPORTING-AUDIT-BUNDLE-RAPID-REVIEW.md`
supersede the original numbering below. Execution now begins with FE-06.0
snapshot-to-Screening binding and follows the dependency order in that gate;
the list below is retained as historical planning context.

- FE-06.1 reporting contract ADR: new `NexusScholar.Reporting` domain owner.
- FE-06.2 bundle extension and verification: `NexusScholar.Bundles` owner.
- FE-06.3 deterministic count projection: `NexusScholar.Reporting` owner.
- FE-06.4 Rapid Review profile: `NexusScholar.Workflow` owner.
- FE-06.5 export commands: `NexusScholar.Cli` owner.

### Fixtures And Negative Cases

- reproducible report and bundle from the same generation;
- unresolved Deduplication or Screening case is hidden from counts;
- report uses current projection instead of bound snapshot;
- altered exclusion reason or decision after export;
- missing raw artifact or intentionally external artifact reference;
- profile shortcut lacks consequence, mitigation, or approval;
- generated narrative asserts a conclusion absent from canonical records;
- bundle round-trip changes semantic identity.

### Exit Criteria

- a clean workspace can conduct and export one complete local Rapid Review slice;
- independent verification reproduces counts and detects tampering;
- profile flexibility never weakens Core authority invariants;
- Citation Network metrics, live dissemination, journal submission, and AI-written
  conclusions remain separate gates.

## FE-07 - Extraction, Appraisal, And Synthesis

Status: complete under ADR 0034 and
`docs/gates/FE-07-EXTRACTION-APPRAISAL-SYNTHESIS.md`; PR `#60` merged as
`a8f89dc`, and post-merge gate-01 and CodeQL validation passed.

### Outcome

Add structured records for evidence extraction, methodological appraisal, and
synthesis planning. Execute this priority as three focused vertical gates, not as
one broad scaffold of empty projects.

### Why It Is Seventh

These records depend on stable Full Text evidence and reporting identities. They
also expand scientific semantics substantially, so each method-specific behavior
needs an accepted contract and evidence mapping.

### Required Behavior

- extraction forms bind protocol questions, fields, source locations, actor, and
  exact Full Text evidence;
- corrections supersede extraction records and preserve history;
- appraisal records identify the instrument, version, domain answers, evidence,
  judgments, actor, and rationale;
- synthesis plans define eligible records, outcomes, effect measures, assumptions,
  transformations, missing-data policy, and planned sensitivity analyses;
- protocol amendments invalidate affected extraction, appraisal, and synthesis
  records rather than silently rewriting them;
- calculations use proven libraries where appropriate and record version and
  configuration;
- automation can prefill proposals but cannot authorize extracted facts,
  appraisal judgments, or scientific conclusions.

### Work Packages And Owners

- FE-07A structured extraction gate: new `NexusScholar.Extraction` owner.
- FE-07B appraisal gate: new `NexusScholar.Appraisal` owner.
- FE-07C synthesis-plan gate: new `NexusScholar.Synthesis` owner.
- FE-07D shared evidence location vocabulary: `NexusScholar.FullText` owner.
- FE-07E workflow integration: FE-03 execution owner.

New projects are created only when their accepted gate contains real behavior,
fixtures, and package-boundary justification.

### Fixtures And Negative Cases

- extraction from an exact page, section, table, or text location;
- source evidence changes after extraction;
- two extractors disagree and require resolution;
- appraisal instrument version is missing or unknown;
- appraisal judgment lacks supporting evidence;
- synthesis includes an ineligible or stale record;
- unit or effect-measure mismatch;
- protocol amendment does not invalidate affected analysis;
- model-generated value is silently treated as human-verified data.

### Exit Criteria

- each subgate has its own accepted schema, fixtures, and completion evidence;
- records remain method-aware without hard-coding one review type globally;
- calculations and judgments are reconstructable and distinguish human from
  automated contributions;
- no unsupported statistical, clinical, causal, or certainty claim is made.

## FE-08 - Local Product Desktop Shell

Status: slices 1 through 5 complete locally. Slices 1 and 2 are governed by ADR 0035,
Slice 3 desktop deduplication review by ADR 0036, and Slice 4 durable Screening
authority resolution by ADR 0037 and
`docs/gates/FE-08-SCREENING-AUTHORITY-RESOLUTION-SLICE-4.md`. Slice 5 desktop
title/abstract Screening is implemented under ADR 0038 and
`docs/gates/FE-08-REMAINING-SLICES-5-9.md`.

### Outcome

Turn the existing preview and UI contracts into a usable local operator shell for
the proven FE-01 through FE-06 commands. The shell simplifies conduct but does not
become scientific authority.

### Why It Is Eighth

A product shell is valuable only after commands, effects, persistence, recovery,
and human authority work without it. Building it earlier would move unresolved
domain behavior into UI state.

### Required Behavior

- UI reads projections through `NexusScholar.AppServices`;
- every mutation routes through an admitted command service;
- actor and active role are explicit before a scientific action;
- destructive or invalidating effects are previewed before confirmation;
- evidence, policy, source generation, and expected outputs are inspectable;
- stale views cannot submit against a newer generation without refresh;
- workspace health, lock contention, quarantine, recovery, and verification are
  visible operator states;
- keyboard and accessibility workflows cover repeated review work;
- UI state, row ids, selection state, and local paths never become scientific ids.

### Work Packages And Owners

- FE-08.1 command facade: `NexusScholar.AppServices` owner.
- FE-08.2 framework-neutral contracts: `NexusScholar.UiContracts` owner.
- FE-08.3 reusable Avalonia controls: `NexusScholar.Avalonia.Blocks` owner.
- FE-08.4 product host and composition root: desktop host owner outside Core.
- FE-08.5 workspace recovery integration: `NexusScholar.ResearchWorkspace` owner.

### Fixtures And Negative Cases

- complete local review flow from open workspace to verified export;
- stale view submits a decision;
- user changes actor midway through a pending confirmation;
- failed command is shown as success;
- app restarts during transaction or lock contention;
- unresolved conflict is hidden by filtering;
- renderer serializes UI state as authority;
- domain project gains an Avalonia or app-service reference.

### Exit Criteria

- all admitted commands have preview, confirmation, success, failure, and stale
  states;
- headless/component tests cover behavior and architecture tests preserve
  dependency direction;
- desktop visual QA covers supported Windows viewport and scaling configurations;
- no API, cloud sync, provider, or AI requirement is hidden inside the shell.

## FE-09 - Live Providers And Citation Networks

### Outcome

Add reproducible external acquisition and citation-graph evidence after the local
workflow can already operate from imports. Split this priority into provider,
Full Text retrieval, and Network gates with separate legal and contract reviews.

### Why It Is Ninth

Provider access improves convenience and coverage, but imports already permit
local scientific work. Network integration introduces changing APIs, credentials,
rate limits, licensing, and legal-access risks that must not define Core authority.

### Required Behavior

- provider clients live in outward infrastructure adapters;
- domain records capture provider, query/request identity, timestamps, response
  evidence, pagination, parser version, warnings, and completeness;
- credentials are opaque references and never enter Core records or logs;
- retries, throttling, cache use, and partial failure are explicit;
- raw responses are retained or digest-addressed subject to provider terms;
- Full Text retrieval records access route, rights status, redirects, bytes, and
  failure category without paywall bypass;
- citation nodes use stable scholarly identity and unresolved states;
- edges retain source evidence and graph snapshots are immutable;
- metrics state the exact graph snapshot and algorithm version.

### Work Packages And Owners

- FE-09A Search provider ADR and adapters: `NexusScholar.Search` contract owner,
  outward provider-infrastructure owner for clients.
- FE-09B legal Full Text retrieval gate: `NexusScholar.FullText` contract owner.
- FE-09C citation graph contract: new `NexusScholar.Network` owner.
- FE-09D credentials and host policy: application infrastructure owner.
- FE-09E provider evidence cache: outward local-store owner.

### Fixtures And Negative Cases

- recorded paginated provider response with deterministic parsing;
- rate limit, timeout, retry-after, partial page, and schema drift;
- credentials appear in logs, records, or bundles;
- cached response is used without source timestamp or digest;
- provider result changes between pages;
- DOI or source-specific ids are conflated across namespaces;
- inaccessible or legally unavailable Full Text;
- citation edge lacks evidence;
- graph metric is reported without snapshot or algorithm identity.

### Exit Criteria

- CI uses recorded fixtures and stubs, never live scholarly providers;
- provider-specific behavior remains outside domain projects;
- legal-access and data-retention policies are accepted;
- PHP or provider parity is claimed only for fixture-backed cases;
- scraping, paywall bypass, and shadow-library acquisition remain forbidden.

## FE-10 - Plugin Runtime

### Outcome

Execute approved extensions through capability-scoped, auditable boundaries.
Official compiled adapters may precede a generic host, but third-party execution
must be out of process and its outputs must remain staged until validated.

### Why It Is Tenth

The runtime depends on stable command, evidence, staging, validation, and
provenance contracts. A plugin host built earlier would expose unresolved internal
state and turn dependency isolation into a false security claim.

### Required Behavior

- manifests declare identity, version, capabilities, input/output schemas,
  resource needs, data classifications, network needs, and credential handles;
- grants are explicit per project or invocation and deny undeclared capability;
- plugins receive scoped data and handles, never database credentials;
- execution occurs in a separate process for third-party code;
- time, memory, filesystem roots, network destinations, and cancellation are
  constrained where the host platform permits;
- output is staged, schema-validated, digest-verified, and human-reviewed before
  adoption into scientific state;
- process logs, exit status, versions, inputs, outputs, and validation are audited;
- process isolation is described accurately and never called a complete sandbox.

### Work Packages And Owners

- FE-10.1 capability and manifest ADR: `NexusScholar.Extensibility` owner.
- FE-10.2 invocation and staged-output contracts:
  `NexusScholar.Extensibility` owner.
- FE-10.3 out-of-process host: plugin-host infrastructure owner.
- FE-10.4 adoption command: relevant domain module owner per output type.
- FE-10.5 security review and abuse fixtures: plugin security owner.

### Fixtures And Negative Cases

- valid capability-scoped invocation;
- undeclared filesystem, network, data, or credential request;
- path traversal and symlink escape;
- malformed, oversized, or digest-mismatched output;
- process crash, hang, cancellation, and resource exhaustion;
- plugin attempts direct database or workspace mutation;
- plugin version changes without output provenance change;
- staged output is adopted without schema validation or human authorization.

### Exit Criteria

- threat model and capability semantics are accepted;
- malicious and failure fixtures cannot mutate authoritative workspace state;
- every adopted output has plugin and human-action provenance;
- no claim of complete sandboxing or safe arbitrary-code execution is made.

## FE-11 - Governed AI

### Outcome

Run narrowly scoped model tasks that produce immutable, evidence-bound proposals.
Human acceptance remains a separate authorized decision using FE-01, and no model
output directly changes protocol, screening, extraction, appraisal, or synthesis.

### Why It Is Eleventh

AI governance depends on the human decision journal, workflow tasks, evidence
records, provider boundaries, privacy policy, and staged-output validation. Adding
model calls before those controls would create authority without provenance.

### Required Behavior

- each task type has an accepted schema, purpose, allowed inputs, prohibited data,
  evidence requirements, validation rules, and human action;
- context manifests enumerate exact record ids, digests, excerpts, and selection
  rationale supplied to the model;
- records capture provider, model, version, parameters, prompt/template digest,
  timestamps, data-transfer boundary, retention policy, cost, and output digest;
- output is parsed as untrusted data and schema-validated;
- claims or recommendations cite supplied evidence and expose uncertainty;
- proposals are immutable and cannot masquerade as human decisions;
- acceptance records the human actor, reviewed evidence, accepted portions,
  rationale, and resulting authoritative command;
- rejection and partial acceptance remain auditable;
- start with protocol clarification proposals before higher-risk scientific tasks.

### Work Packages And Owners

- FE-11.1 AI governance ADR: `NexusScholar.AI` owner.
- FE-11.2 context and task records: `NexusScholar.AI` owner.
- FE-11.3 concrete model adapter: outward model-provider owner.
- FE-11.4 proposal review command: relevant domain module owner.
- FE-11.5 privacy, evaluation, and retention review: governance owner.

### Fixtures And Negative Cases

- valid protocol clarification proposal with evidence;
- missing or stale context digest;
- private or prohibited data selected for external transfer;
- malformed JSON, prompt injection, unsupported citation, and fabricated evidence;
- provider or model version omitted;
- proposal is directly persisted as a Screening verdict;
- human acceptance lacks reviewed evidence or rationale;
- task replay incorrectly claims deterministic model output;
- live model call is attempted in CI.

### Exit Criteria

- one low-risk task is complete end to end with proposal, validation, review,
  acceptance or rejection, decision, provenance, and invalidation;
- privacy and provider policies are explicit;
- evaluation measures schema compliance and evidence support, not only fluency;
- CI remains model-free and uses recorded fixtures;
- AI remains advisory and no autonomous scientific authority is claimed.

## FE-12 - Database, API, Cloud, And Multi-User Operation

### Outcome

Deploy stable local semantics through durable application infrastructure without
moving scientific authority into database rows, HTTP resources, or cloud state.

### Why It Is Last

Persistence and distribution amplify every semantic mistake. Local records,
commands, snapshots, invalidation, and operator workflows must be stable before
schema migrations, concurrency, authorization, synchronization, and deployment
become compatibility obligations.

### Required Behavior

- an ADR selects storage technology and maps each domain record without adding
  storage dependencies to domain projects;
- migrations are versioned, reversible where possible, and verified against
  preserved authority-bearing data;
- API resources expose domain ids and digests without making URLs or row ids
  scientific identity;
- authorization distinguishes roles, project membership, and scientific action
  authority;
- optimistic concurrency rejects stale writes and preserves conflicting actions;
- sync has explicit ownership, conflict, tombstone, and offline behavior;
- encryption, secret management, backup, restore, deletion, retention, and audit
  policies are tested;
- tenant and project boundaries prevent cross-project data access;
- cloud storage does not transfer data ownership or silently become canonical;
- package publication remains separately release-gated.

### Work Packages And Owners

- FE-12.1 persistence ADR and migration policy: local-store infrastructure owner.
- FE-12.2 API contract and authorization: API host owner.
- FE-12.3 multi-user command concurrency: `NexusScholar.AppServices` owner.
- FE-12.4 synchronization and offline policy: sync infrastructure owner.
- FE-12.5 deployment, backup, security, and operations: platform owner.

### Fixtures And Negative Cases

- migration from every supported stored version;
- rollback or recovery after interrupted migration;
- stale concurrent scientific decisions;
- conflicting offline edits and invalidation propagation;
- unauthorized actor invokes an allowed UI/API action;
- cross-tenant record or artifact access;
- backup restore changes digests or loses history;
- cloud deletion leaves undisclosed retained evidence;
- API or database row identity replaces domain identity.

### Exit Criteria

- local and infrastructure-backed representations are semantically equivalent;
- migration, concurrency, authorization, backup, restore, and isolation tests pass;
- hosted deployment has an explicit threat model and operating runbook;
- Core domain projects retain inward-only dependencies;
- production, scale, security, or compliance claims are made only from evidence.

## Immediate Next Gate

FE-09B, FE-09C, and FE-09E are complete locally under ADRs 0042, 0043, and
0041. Close the stacked FE-09 branch through hosted CI and protected merge.
After merge, begin FE-10 plugin-runtime design; do not widen provider retention,
live Full Text transport, citation metrics, exports, or PHP compatibility
without their successor gates.

## Verification Baseline

Every implementation gate must run:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

It must also run affected architecture, conformance, mutation, package, and
workspace crash/concurrency checks. Hosted CI must remain free of live scholarly
provider and live model calls.
