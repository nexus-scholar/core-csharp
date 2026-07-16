# ADR 0034: Extraction, Appraisal, And Synthesis Authority

- Status: Accepted
- Date: 2026-07-16
- Decision owner: Nexus Scholar maintainer/manager

## Context

FE-05 supplies verified raw Full Text and one explicit derived-text
representation. FE-06 supplies immutable reporting identities. FE-07 must add
method-aware extraction, appraisal, and synthesis planning without turning a
derived text view, automation proposal, mutable projection, or calculation
result into scientific authority.

The feature priority plan requires three focused vertical gates and a shared
evidence-location vocabulary. No approved specification or pinned PHP fixture
defines these new C# semantics, so this ADR defines a local contract and makes
no compatibility claim.

## Decision

### Package boundaries

Extend `NexusScholar.FullText` with a canonical evidence-location record that
can only be created from `VerifiedFullTextExtraction`. Create three packable,
persistence-independent domain projects:

- `NexusScholar.Extraction`, depending on Kernel, Protocol, and FullText;
- `NexusScholar.Appraisal`, depending on Kernel, Protocol, and FullText;
- `NexusScholar.Synthesis`, depending on Kernel, Protocol, Extraction, and
  Appraisal.

The packages do not depend on persistence, UI, provider, model, or statistical
runtime libraries. Workflow execution may bind their canonical records and
invalidation effects, but the domain packages do not depend outward on
Workflow.

Create packable `NexusScholar.WorkflowExecution.ScientificRecords` as the
outward FE-07E bridge. It depends on WorkflowExecution and the three FE-07
owners, verifies that package-owned invalidations share one exact Protocol
amendment, and emits one digest-bound `WorkflowExecutionRecordRef`. It owns no
scientific record and cannot create or infer invalidations.

### Exact evidence locations

The schema is:

```text
nexus.fulltext.evidence-location / 1.0.0
```

A location binds the verified raw artifact id and digest, extraction id and
canonical representation digest, representation kind, one-based element
ordinal, location kind (`page`, `section`, `table`, or `text`), a
method-specific locator, the exact source-element digest, and an exact excerpt
digest. Creation verifies that the element exists and the excerpt occurs in it.
Page and section locations must match the extraction representation. Table and
text locations remain explicit locators and make no parsing or semantic claim.

### Structured extraction

The schemas are:

```text
nexus.extraction.form / 1.0.0
nexus.extraction.record / 1.0.0
nexus.extraction.invalidation / 1.0.0
```

A form binds an approved Protocol version and content digest, protocol question
references, typed field definitions, and a human approver. A record binds the
form, candidate, exact field values and evidence locations, actor, time, entry
kind, and optional proposal/supersession/conflict sources. Automation may create
only a proposal. A final review, correction, or conflict resolution requires a
human actor. Corrections supersede exactly one current record; resolutions bind
the disagreeing current records. Journal replay preserves every entry and
derives current records and unresolved conflicts.

### Methodological appraisal

The schemas are:

```text
nexus.appraisal.instrument / 1.0.0
nexus.appraisal.record / 1.0.0
nexus.appraisal.invalidation / 1.0.0
```

An instrument has a stable id, non-empty version, method domain, ordered
questions, allowed answers, and judgment vocabulary. An appraisal record binds
the approved Protocol, instrument digest, candidate, every domain answer,
supporting Full Text evidence, overall judgment, rationale, human actor, and
time. Unknown or missing instrument versions, incomplete answers, evidence-free
judgments, and automation-finalized judgments fail closed. Automation output is
proposal-only.

### Synthesis planning

The schemas are:

```text
nexus.synthesis.plan / 1.0.0
nexus.synthesis.invalidation / 1.0.0
```

A synthesis plan binds the approved Protocol, an immutable eligible-record set
of current extraction and appraisal digests, outcomes, effect measures and
units, assumptions, transformations, missing-data policy, sensitivity analyses,
human author, and time. Each calculation declaration records library id,
library version, and canonical configuration; the plan does not execute or
certify calculations. Unit/effect-measure mismatches and stale, invalidated, or
ineligible source records are rejected.

### Amendment invalidation

Each package owns an append-only invalidation record binding an exact verified
Protocol amendment, affected record digests, reason, human actor, and time.
Invalidation never rewrites or deletes a source record. Synthesis validation
requires current, non-invalidated source records. Workflow integration consumes
the three invalidation records as explicit effects; a Protocol version change
alone never silently rewrites downstream scientific state.

## Alternatives Rejected

- Free-text citations or page labels without extraction identity: cannot prove
  the exact evidence view.
- One generic scientific-record package: erases method-specific invariants and
  creates a broad dependency hub.
- Mutable `IsInvalid` flags: destroy the historical transition.
- Automation and human records sharing one final status: obscures authority.
- Executing statistical methods in this gate: requires method-specific ADRs,
  proven libraries, and validation evidence not yet present.

## Consequences

FE-07 gains reconstructable, method-aware domain authorities while remaining
local and persistence-independent. The cost is three new packages, strict
canonical records, explicit replay/invalidation, and no automatic statistical
conclusion generation.

## Migration Effect

No existing record is reinterpreted. Existing `FullTextExtractionRecord` remains
derived-text evidence, not a scientific extraction form. Callers must create new
evidence locations and FE-07 records explicitly.

## Fixture Effect

Add deterministic fixtures for every schema, exact page/section/table/text
location, changed source evidence, correction and disagreement resolution,
unknown instrument version, missing evidence, stale synthesis inputs,
unit/effect mismatch, amendment invalidation, and automation authority failure.

## Compatibility And Claims

This is a local C# contract. It makes no PHP, blueprint, appraisal-instrument
endorsement, statistical correctness, clinical, causal, certainty, provider,
database, UI, production, or regulatory compatibility claim.

## Reversal Conditions

Revise this ADR if an approved specification defines conflicting schemas, the
pinned reference gains observable compatible behavior, a method-specific
instrument requires materially different authority semantics, or synthesis
execution is authorized with a validated calculation library.
