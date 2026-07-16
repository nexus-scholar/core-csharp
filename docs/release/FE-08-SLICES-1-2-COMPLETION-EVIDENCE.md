# FE-08 Slices 1 And 2 Completion Evidence

Date: 2026-07-16

Authority: ADR 0035 and
`docs/gates/FE-08-LOCAL-PRODUCT-DESKTOP-SLICES-1-2.md`.

## Delivered Scope

- Windows-first local Avalonia product host;
- structured desktop application-service facade;
- open and verify structured projections;
- preview/confirm initialize, local Search import, and analyze commands;
- source-digest and project-revision stale rejection;
- explicit success, attention, failure, stale, and recovery-required states;
- shared init/import behavior with unchanged CLI rendering;
- local-workspace-neutral analysis provenance and report wording.

## Invariants Enforced

- UI state, row ids, selection ids, and paths never become scientific identity;
- every write command binds exact preview material and a deterministic token;
- changed preview material, source bytes, target state, or project revision fails
  closed;
- workspace lock, active authority, and I/O faults never render as success;
- import remains user-selected local evidence with digest and parser trace;
- no scientific decision, actor role, provider, network, AI, cloud, API, database,
  plugin, PDF/OCR, PHP, or blueprint behavior is admitted.

## Local Validation

Commands:

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Measured result after final review refresh:

- Release build: passed with 0 warnings and 0 errors;
- tests: 859 passed in the complete serial Release matrix, including 37
  ResearchWorkspace, 20 Desktop.AppServices, and 6 Desktop product-host tests;
- format verification: passed after import-order correction;
- release policy, deterministic package comparison, and clean local-source smoke:
  23 approved packages passed;
- native first-run and loaded-warning visual QA at 1360 x 840 and 100% scaling
  exposed and closed a missing control theme; the corrected states showed
  visible fields, stable three-column layout, actionable warning metrics, and no
  overlap. See `docs/release/FE-08-VISUAL-QA.md`.

## Review Closure

Independent manager, scientific-invariant, and test reviews found no blocking or
high-severity code, architecture, test, or authority finding.

## Hosted Validation

- implementation commit: `d95cf3b`;
- protected PR: `#62`;
- merge commit on `main`: `b40bba64807b15bb088dcffbb197a5462babc73e`;
- branch Gate 01: run `29537423473`, Ubuntu and Windows passed;
- branch CodeQL: run `29537423478`, passed;
- dependency review: run `29537423524`, passed;
- post-merge Gate 01: run `29537771256`, Ubuntu and Windows passed;
- post-merge CodeQL: run `29537771198`, passed.

## Claim Boundary

This evidence establishes FE-08 slices 1 and 2 local behavior only. It makes no
accessibility certification, installer, updater, deployment, cross-platform,
scientific-decision, provider, AI, database/API/cloud, PHP, or blueprint claim.
