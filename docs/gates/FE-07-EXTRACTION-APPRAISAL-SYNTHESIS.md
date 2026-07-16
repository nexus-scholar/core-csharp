# FE-07: Extraction, Appraisal, And Synthesis

Status: implementation, independent local review, and hosted branch validation
accepted under ADR 0034; protected merge validation remains open.

## Goal

Add reconstructable structured extraction, methodological appraisal, and
synthesis-plan authority bound to exact Full Text and approved Protocol state.

## Sources

- accepted ADRs 0002, 0004, 0014, 0028, 0030-0034;
- FE-05 and FE-06 completion evidence;
- the FE-07 section of the feature expansion priority plan;
- current verified Full Text, Protocol amendment, and Workflow invalidation
  behavior.

## Dependency-Ordered Work

1. FE-07D: shared exact Full Text evidence-location vocabulary.
2. FE-07A: structured extraction form, append-only journal, corrections,
   disagreement resolution, and proposal boundary.
3. FE-07B: versioned methodological appraisal instrument and records.
4. FE-07C: immutable synthesis plans over current eligible records.
5. FE-07E: explicit amendment invalidation and Workflow-facing effect bindings.
6. Phase closeout: fixtures, architecture/package checks, independent review,
   local and hosted validation, roadmap and completion evidence.

## Required Behavior

- every scientific value or judgment binds exact verified Full Text evidence;
- extraction fields bind approved Protocol questions and preserve corrections;
- disagreements remain unresolved until a human resolution binds both sources;
- appraisal records bind a known instrument version, complete domain answers,
  evidence, judgment, and rationale;
- synthesis plans bind eligible current records, outcomes, effect measures,
  units, assumptions, transformations, missing-data policy, sensitivities, and
  calculation library/version/configuration declarations;
- amendment effects append invalidations and never mutate historical records;
- automation is proposal-only and cannot authorize facts, judgments, plans, or
  conclusions.

## Allowed Scope

- focused `NexusScholar.FullText` evidence-location additions;
- new packable Extraction, Appraisal, and Synthesis domain projects;
- focused Workflow-facing invalidation binding;
- unit, conformance, architecture, package-smoke, fixture, ADR, gate, roadmap,
  and completion-evidence changes.

## Excluded Scope

- OCR, PDF parsing, network retrieval, provider calls, persistence, UI, API,
  database, model runtime, statistical execution, certainty grading, clinical
  or causal conclusions;
- endorsement or full implementation of any named appraisal instrument;
- PHP or blueprint compatibility claims.

## Required Negative Cases

- evidence source bytes or derived representation changes;
- location ordinal, kind, locator, or excerpt does not match the source;
- correction targets a non-current record or resolution omits a disagreement;
- automation finalizes an extraction, appraisal, or synthesis plan;
- appraisal instrument version is missing/unknown or judgment lacks evidence;
- synthesis includes an ineligible, stale, superseded, or invalidated record;
- unit/effect-measure mismatch or calculation library version/config is absent;
- amendment omits an affected downstream record.

## Verification

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Also run focused Core, conformance, architecture, deterministic-package-policy,
and package-smoke checks for all three new packages.

## Exit Criteria

- each subgate has accepted schemas, deterministic fixtures, and evidence;
- all required negative cases fail with stable categories;
- records are method-aware and human/automation contributions are distinct;
- amendment replay identifies every invalidated downstream record;
- clean local and hosted validation passes and the branch merges;
- no unsupported statistical, clinical, causal, or certainty claim is made.

## Progress Evidence

- FE-07D: `nexus.fulltext.evidence-location / 1.0.0` binds verified raw artifact
  and derived-text identities, exact representation element, locator, source
  element digest, and exact excerpt; strict canonical rehydration rejects
  altered, noncanonical, unknown-field, or source-mismatched bytes.
- FE-07A: the packable Extraction owner provides approved Protocol-bound forms,
  typed fields, exact evidence values, proposal/review/correction/resolution
  records, append-only journal replay, disagreement projection, and amendment
  invalidation. Proposals are excluded from current scientific authority.
- FE-07B: the packable Appraisal owner provides versioned method-domain
  instruments, complete evidence-bound answers, human judgments and rationale,
  corrections, and amendment invalidation. Unknown versions, incomplete
  answers, missing evidence, and automation finalization fail closed.
- FE-07C: the packable Synthesis owner provides plan-only authority over current
  Extraction/Appraisal records, outcomes, effect measures, units, explicit
  transformations, assumptions, missing-data policy, sensitivity analyses, and
  calculation library/version/configuration declarations. It executes no
  statistics and emits fixed non-claims.
- FE-07E: the packable WorkflowExecution.ScientificRecords bridge admits only
  package-owned invalidations from one exact Protocol amendment and exposes one
  digest-bound Workflow execution source reference.
- local fixtures identify all seven schemas, positive paths, required negative
  cases, and explicit PHP/blueprint/statistical/clinical/causal non-claims.
- independent manager, scientific-invariant, and test review found no remaining
  blocking or high-severity findings.
- Release build passed with zero warnings/errors; 819 tests passed; format was
  clean; package policy, deterministic package comparison, and clean local
  package smoke passed for all 23 approved packages.
