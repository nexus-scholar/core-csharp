# FE-08: Local Product Desktop Shell - Slices 1 And 2

Status: complete under ADR 0035. Merged through PR 62 at `b40bba6`; branch and
post-merge validation passed.

## Progress Evidence

- `NexusScholar.ResearchWorkspace` exposes structured initialize/import results
  with duplicate, source-digest, expected-revision, active-authority, lock, and
  recovery handling; CLI init/import adapters preserve their existing output.
- `NexusScholar.Desktop.AppServices` owns immutable previews, deterministic
  confirmation tokens, exact effects/non-claims, stale checks, and normalized
  success/attention/failure/stale/recovery results.
- `NexusScholar.Desktop` is a non-packable Avalonia product host for open,
  initialize, import, verify, and analyze. Every write is previewed in an effect
  inspector and requires explicit confirmation.
- shared analysis identity and report wording are local-workspace-neutral rather
  than incorrectly claiming that desktop execution invoked the CLI.
- focused ResearchWorkspace, facade, desktop, CLI compatibility, and architecture
  tests cover the admitted behavior and negative cases.

## Researcher Outcome

A researcher can start the Nexus Scholar desktop product, open or initialize a
local workspace, import a researcher-selected Search export, verify it, analyze
it, and inspect refreshed structured workspace state without invoking the CLI or
creating a second scientific authority.

## Sources

- ADR 0016 and ADR 0035;
- FE-08 in `docs/plans/2026-07-14-feature-expansion-priority.md`;
- implemented ResearchWorkspace transactions, read models, and CLI behavior;
- existing UiContracts, Avalonia Blocks, and Desktop Preview tests.

## Dependency-Ordered Work

1. FE-08.0: accept productization and command-facade authority.
2. FE-08.1: add structured initialize/import operations and preserve CLI output.
3. FE-08.2: add deterministic preview and structured command-result contracts.
4. FE-08.3: create the product host for open/init/import/verify/analyze.
5. FE-08.4: add recovery, accessibility, component, architecture, and visual QA.
6. Closeout: independent review, local/hosted validation, roadmap, and evidence.

## Required Behavior

- product UI invokes only structured application services;
- initialization is all-or-recoverable and rejects an existing workspace;
- import accepts only a user-selected local file and supported source/format;
- import copies bytes, records their digest and trace, and rejects reimport;
- import/analyze reject stale project revision and active authority generation;
- changed source bytes invalidate an import preview;
- verify/analyze refresh the displayed read model after completion;
- completed, attention, failure, stale, and recovery-required are distinct;
- no scientific decision control is enabled.

## Required Negative Cases

- initialize target already contains a workspace;
- initialize fails during directory or project-file creation;
- import source is missing, changes after preview, or has unsupported format;
- import input id already exists;
- import or analyze preview targets a stale project revision;
- authority generation or workspace lock blocks a write;
- parser warning is displayed as attention rather than hidden or fatal;
- failed operation is not rendered as success;
- UI state, row id, selection id, or path is serialized as authority;
- a Core domain project references Avalonia or desktop application services.

## Allowed Paths

- `docs/adr/0035-*`, this gate, roadmap, UI docs, and completion evidence;
- focused ResearchWorkspace init/import services and CLI adapter refactor;
- `NexusScholar.Desktop.AppServices`, `NexusScholar.Desktop`, UiContracts, and
  focused Avalonia Blocks additions;
- affected tests, fixtures, solution, package topology, and release policy.

## Excluded Scope

- deduplication, Screening, Full Text, Protocol, reporting, or export decisions;
- durable UI settings, installer, updater, telemetry, or crash upload;
- providers, network, scraping, PDF/OCR, AI, plugins, database, API, cloud, or
  multi-user behavior;
- PHP, blueprint, visual-design, accessibility-certification, or production
  deployment claims.

## Verification

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Also run product-host component tests, architecture tests, import/init recovery
tests, and desktop screenshots at supported Windows viewport/scaling settings.

## Exit Criteria

- open/init/import/verify/analyze work through structured services;
- every write operation has preview, confirmation, stale, success, and failure
  coverage;
- CLI init/import behavior remains fixture-compatible;
- desktop visual QA proves the first-run, loaded, import, warning, and failure
  states fit supported viewports without overlap;
- independent review has no blocking or high-severity finding;
- clean hosted validation passes and the protected branch merges.
