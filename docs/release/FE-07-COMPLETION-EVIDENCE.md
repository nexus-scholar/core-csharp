# FE-07 Completion Evidence

Date: 2026-07-16

Authority: ADR 0034 and
`docs/gates/FE-07-EXTRACTION-APPRAISAL-SYNTHESIS.md`.

## Delivered Scope

- exact canonical Full Text evidence locations with strict rehydration;
- packable structured Extraction owner with append-only corrections,
  disagreements, resolutions, proposals, and amendment invalidation;
- packable versioned Appraisal owner with complete evidence-bound human
  judgments, corrections, proposals, and amendment invalidation;
- packable plan-only Synthesis owner with verified eligible-record factories,
  outcomes, measures, units, transformations, assumptions, missing-data policy,
  sensitivity analyses, and calculation library/version/configuration records;
- packable WorkflowExecution scientific-record invalidation bridge;
- local FE-07 conformance catalog and explicit non-claims.

## Invariants Enforced

- evidence locations can only be created and reopened against one verified Full
  Text extraction and exact representation element/excerpt;
- automation output remains proposal-only and never becomes extracted fact,
  appraisal judgment, or synthesis authority;
- corrections and resolutions supersede exact current records without deleting
  history; proposals are excluded from current authority;
- appraisal instruments require an explicit supported version, complete answer
  set, exact evidence, allowed judgment, rationale, actor, and time;
- synthesis rejects stale, superseded, invalidated, foreign-Protocol, or
  caller-fabricated source records;
- effect-measure or unit mismatch requires an explicit transformation;
- calculation declarations bind library id, version, canonical configuration,
  and configuration digest but do not execute or certify statistics;
- amendment invalidations append exact target digests and the Workflow bridge
  rejects mixed-amendment or duplicate target sets.

## Review Closure

Independent scientific, architecture, and test review required ADR
authorization, FE-07D-first ordering, explicit human acceptance, append-only
invalidation, plan-only synthesis, strict evidence rehydration, package-policy
updates, and a focused outward Workflow bridge. The implementation incorporates
those constraints. During integration, full tests exposed and closed one real
authority bug: proposal records were initially projected as current Extraction
records. Final manager, scientific-invariant, and test review found no remaining
blocking or high-severity findings.

## Local Validation

Commands:

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Result:

- Release build: passed, 0 warnings, 0 errors;
- tests: 819 passed, 0 failed, 0 skipped;
- Core: 483;
- Conformance: 141;
- Architecture: 37;
- CLI: 71;
- ResearchWorkspace: 26;
- AppServices: 23;
- UI contracts: 18;
- Desktop Preview: 9;
- Avalonia Blocks: 7;
- Avalonia sample host: 4;
- format verification: passed;
- package policy and deterministic package comparison: passed;
- clean local-source package smoke: 23 assemblies loaded at `0.1.0-alpha.2`.

## Claim Boundary

FE-07 makes no PHP or blueprint compatibility, named appraisal-instrument
endorsement, statistical execution/correctness, clinical, causal, certainty,
provider, database, UI, production, or regulatory claim. Synthesis records are
plans, not results or conclusions.

## Hosted Validation

Pull request: `#60`.

Branch validation for implementation commit `64b69fb` passed:

- gate-01 run `29524787914`: Ubuntu job `87710394666` and Windows job
  `87710394658` passed;
- dependency-review run `29524787975`, job `87710394673`, passed;
- codeql run `29524787944`, analyze job `87710394462`, passed.

Protected merge and post-merge hosted validation remain pending.
