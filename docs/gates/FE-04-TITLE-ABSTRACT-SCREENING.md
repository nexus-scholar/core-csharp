# FE-04: Title And Abstract Screening

Status: complete under accepted ADR 0031. Completion evidence:
`FE-04-TITLE-ABSTRACT-SCREENING-EVIDENCE.md`.

## Goal

Deliver replayable title/abstract conduct over a locked candidate set with
protocol-bound criteria, independent human review, deterministic conflict and
adjudication, explicit invalidation, and verified downstream handoff.

## Sources

- approved Protocol and verified Deduplication authority;
- ADRs 0013, 0021, 0028, 0029, 0030, and 0031;
- the FE-04 section of the feature expansion priority plan;
- existing Screening fixtures as historical semantic evidence.

## Dependency-Ordered Work

1. FE-04.1: canonical policy, header, decision, invalidation, and handoff records;
   strict rehydration; deterministic replay and conflict projection.
2. FE-04.2: FE-03 human-task reference bridge and actor equality enforcement.
3. FE-04.3: atomic ResearchWorkspace Screening plus workflow generations.
4. FE-04.4: AppServices preview/commit and read projections.
5. FE-04.5: CLI manifest and artifact-integrity status; authority replay and
   mutation remain behind verified ports until process-entry authority resolution exists.
6. FE-04.6: conformance fixtures, evidence, architecture review, and closeout.

## Required Behavior

- only a locked candidate set derived from verified Deduplication is admitted;
- criteria and every decision bind the approved Protocol content digest;
- independent reviewers are authorized by a digest-bound conduct policy;
- one actor cannot satisfy two independent review slots for one generation;
- exclude decisions require a stage-valid protocol-defined reason code;
- corrections supersede by digest and never overwrite history;
- conflict and adjudication generations are deterministic projections;
- unresolved conflict and `needs_review` block handoff;
- invalidation is explicit, complete, append-only, and replayable;
- canonical reopen reproduces the same current projection and head digest;
- automation and suggestions cannot become final decisions.

## Allowed Scope

- `src/NexusScholar.Screening/**`;
- a separate Screening-to-WorkflowExecution bridge project;
- focused AppServices, ResearchWorkspace, and CLI adapters;
- focused unit, architecture, conformance, and workspace tests;
- `fixtures/conformance/screening-conduct/**`;
- FE-04 ADR, gate, evidence, and feature-plan status updates.

## Excluded Scope

- Full Text conduct or artifact extraction;
- database, API, cloud, UI shell, scheduler, queue, or background service;
- live provider, plugin, or model execution;
- mutable app rows as Core authority;
- PHP, blueprint, production, scale, or institutional compatibility claims.

## Required Negative Cases

- unlocked, stale, or unresolved candidate authority;
- draft, stale, or mismatched Protocol or criteria authority;
- same actor attempts both independent reviews;
- actor role text is not present in the conduct policy;
- missing rationale, unknown reason code, or full-text-only reason at this stage;
- correction omits or misidentifies its superseded decision;
- adjudication omits conflict sources or uses an unauthorized actor;
- AI or automation attempts a final decision;
- ordinal gap, prior-digest mismatch, record reorder, removal, or splice;
- incomplete invalidation set;
- unresolved conflict, `needs_review`, or insufficient reviews passed to handoff;
- stale expected head, concurrent append, or partial workspace generation.

## Verification

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Also run focused Screening, WorkflowExecution bridge, ResearchWorkspace,
AppServices, CLI, architecture, and conformance tests.

## Exit Criteria

- accepted ADR 0031 remains satisfied after scientific and architecture review;
- local canonical records reconstruct complete title/abstract conduct;
- conflict, correction, adjudication, and invalidation preserve history;
- verified handoff cannot bypass unresolved or insufficient review;
- Screening and matching FE-03 completion commit atomically;
- app projections and paths never become scientific identity;
- all focused and solution-wide validation passes.
