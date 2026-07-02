# 0016: Desktop Shell And ResearchWorkspace Service Boundary

Status: Accepted
Date: 2026-07-02
Owner: docs/adr, application-services, desktop-ui
Related plan: `docs/ui/DESKTOP-WORKSPACE-PLAN-2026-07-02.md`
Related phases: `docs/ui/DESKTOP-WORKSPACE-PHASES-2026-07-02.md`
Related UI import: `docs/ui/imported/2026-07-02-ui-guides-and-specs/`
Supersedes: none
Superseded by: none

## Context

Nexus Scholar already has a local Research Workspace CLI loop:

```bash
nexus init
nexus status
nexus import search
nexus verify
nexus analyze
nexus review
nexus clusters
nexus clusters exact
nexus clusters review
nexus clusters show <id>
```

The desktop UI direction is to provide a local-first, JetBrains-style research evidence workspace over that workflow. The desktop should make the workflow easier to inspect, but it must not become a separate scientific authority or a hidden mutation layer.

The current UI lane already contains renderer-neutral contracts, sample block plans, an Avalonia renderer prototype, and a sample visual host. Those pieces are non-authoritative and must remain separate from Core scientific behavior.

The current Research Workspace implementation lives under `src/NexusScholar.Cli/ResearchWorkspace/`. That is acceptable for the CLI milestone, but a desktop UI should not permanently parse CLI console text or duplicate CLI-only workspace logic. A shared non-UI boundary is needed before a durable desktop shell.

Motivating constraints:

- `AGENTS.md` requires scientific mutations to identify an actor and append provenance, while domain projects must not reference UI frameworks, provider SDKs, storage SDKs, or concrete model clients.
- `docs/adr/0015-app-services-readonly-workspace-composition.md` defines APP-01 as a read-only projection and keeps merge actions as placeholders only.
- `docs/cli/RESEARCH-WORKSPACE-CLI-v0.md` defines the implemented local Research Workspace loop as local-folder workflow behavior, not live provider behavior.
- Existing CLI, AppServices, UiContracts, and Avalonia sample-host tests are the current executable guardrails for this lane.
- There is no PHP compatibility claim for the desktop or ResearchWorkspace service boundary.

## Decision

Accept the following boundary for desktop workspace work.

### 1. Add a shared non-UI ResearchWorkspace library before desktop implementation

Create a shared application-service package:

```text
src/NexusScholar.ResearchWorkspace/
tests/NexusScholar.ResearchWorkspace.Tests/
```

This package will own reusable local workspace behavior that is currently CLI-specific, including:

- workspace discovery from a folder or child folder;
- `nexus.project.json` read/write helpers;
- project-relative path resolution;
- local input digest verification;
- import trace and generated-output discovery;
- analysis orchestration over imported local Search evidence;
- structured command/workflow result objects;
- read-model construction for the future desktop UI.

The package is not a Core domain package and is not scientific authority. It is a local application-service layer over existing Search, Deduplication, AppServices, and UiContracts projections.

### 2. Keep CLI behavior stable

Refactor `NexusScholar.Cli` so command classes become thin adapters over `NexusScholar.ResearchWorkspace`.

CLI output, exit codes, path-display behavior, and current tests must remain stable unless an intentional CLI polish task explicitly changes them.

Required preserved behaviors include:

```text
0  success
1  validation or command usage failure
2  missing project or missing input
3  digest mismatch
4  unsupported schema or import format
5  unexpected runtime failure
```

The CLI must continue to avoid printing machine-local absolute workspace paths when a project-relative path is available.

### 3. Start desktop as a preview sample, not a product shell

The first desktop project should be:

```text
samples/NexusScholar.Desktop.Preview/
tests/NexusScholar.Desktop.Preview.Tests/
```

Do not start with:

```text
src/NexusScholar.Desktop/
```

Rationale: the current accepted product boundary does not yet authorize a full desktop product shell, durable app settings, installer behavior, or product persistence. A sample preview is enough to validate the workflow, visual model, and read models while keeping expectations clear.

A later ADR may graduate the preview into `src/NexusScholar.Desktop`.

### 4. UI-01 is read-only

The first desktop preview may:

- open an existing local workspace folder;
- discover a parent workspace from a child folder;
- display workspace status;
- display workflow steps;
- display imported evidence records;
- display parser warnings and skipped records;
- display verification health;
- display analysis outputs;
- display review queue items;
- display duplicate clusters;
- display duplicate candidate details;
- show locked/display-only APP-01 merge actions.

The first desktop preview must not:

- run `init`;
- run `import`;
- run `verify`;
- run `analyze`;
- accept merge;
- reject merge;
- mark unresolved;
- write project files;
- write scientific decisions;
- call live providers;
- create persistence/database/API/cloud behavior;
- call AI/model clients;
- add PDF/OCR behavior;
- claim PHP compatibility.

### 5. UI-02 may add safe local workflow execution only after UI-01 feedback

After UI-01 is reviewed, a later accepted task may allow the desktop to execute these local non-decision workflow actions through `NexusScholar.ResearchWorkspace` structured services:

- initialize local workspace;
- import local Search export;
- verify local workspace;
- analyze local evidence.

UI-02 still must not execute merge decisions, query providers, call AI/model clients, add PDF/OCR, add cloud/API/database behavior, or mutate Core scientific records outside the existing local workflow-output rules.

### 6. Do not store UI preferences in `nexus.project.json`

`nexus.project.json` remains a local project index, not an app preferences store and not canonical scientific authority.

UI-01 should use no durable preferences. Recent-workspace UI may be placeholder, in-memory, fixture-backed, or disabled.

If durable recent-workspace settings, window layout, theme settings, or UI preferences are needed, create a later lightweight settings ADR. Any settings file must live outside the research workspace by default, for example under an OS app-data location, and must not affect scientific verification.

### 7. Keep merge actions display-only and locked

APP-01 merge actions may be rendered as affordances, but they must be visibly disabled and labeled as unavailable.

Allowed labels include:

```text
Accept merge - locked
Reject merge - locked
Mark unresolved - locked
Decision execution is not available in this version.
```

They must not execute commands, call services, write files, mutate Core records, or imply that the UI can finalize a scientific decision.

## Allowed References

### `NexusScholar.ResearchWorkspace`

May reference:

- `NexusScholar.Kernel`
- `NexusScholar.Shared`
- `NexusScholar.Search`
- `NexusScholar.Deduplication`
- `NexusScholar.AppServices`
- `NexusScholar.UiContracts`

Must not reference:

- Avalonia;
- desktop UI projects;
- provider SDKs;
- persistence/database packages;
- cloud/API/server packages;
- AI/model-client packages;
- PHP compatibility code.

### `NexusScholar.Cli`

May reference:

- `NexusScholar.ResearchWorkspace`
- existing packages needed for CLI formatting and compatibility.

CLI command classes should become adapters, not duplicate workflow engines.

### `samples/NexusScholar.Desktop.Preview`

May reference:

- `NexusScholar.ResearchWorkspace`
- `NexusScholar.UiContracts`
- `NexusScholar.Avalonia.Blocks`
- Avalonia packages needed for the preview shell.

Must not reference Core domain packages directly for mutation.

### Core projects

Must remain UI-free.

Core projects must not reference:

- `NexusScholar.UiContracts`;
- `NexusScholar.Avalonia.Blocks`;
- desktop UI projects;
- app persistence;
- AI/model clients;
- provider SDKs.

## Consequences

### Positive

- CLI and desktop use one workspace implementation.
- Desktop UI can bind to structured read models instead of parsing console text.
- The preview can validate real local workspace outputs without implying product persistence.
- Merge-decision boundaries remain explicit.
- Core stays UI-free.

### Negative

- Desktop implementation waits until shared services and read models exist.
- A small refactor is required before UI value is visible.
- Some code currently internal to `NexusScholar.Cli` must be made public or moved.
- App preferences and recent workspaces remain out of scope until a later ADR.

## Alternatives Considered

### Keep all workspace behavior inside `NexusScholar.Cli`

Rejected. A desktop UI would either parse console text or duplicate CLI-only behavior. Both options would make UI state drift from CLI behavior and weaken testing.

### Start directly with `src/NexusScholar.Desktop`

Rejected for now. A durable product desktop project implies product commitments around settings, packaging, persistence expectations, and support boundaries that are not yet accepted.

### Let the desktop call Core and AppServices directly

Rejected for UI-01. The desktop should bind to application-service read models. Direct calls from the preview into domain or mutation pathways would blur scientific authority and make locked merge actions harder to enforce.

### Add providers, persistence, or merge execution before the desktop preview

Rejected. Those capabilities require separate ADRs because they affect network/legal behavior, actor identity, decision persistence, provenance semantics, and scientific mutation rules.

## Work Authorized By This ADR

This ADR authorizes the next planning and implementation packets only:

1. `RW-01`: extract shared ResearchWorkspace services.
2. `RW-02`: add UI-friendly read models.
3. `UI-01`: build read-only desktop preview shell after read models exist.

It does not authorize:

- full product desktop shell;
- durable app settings;
- installer/distribution work;
- provider integrations;
- AI/model calls;
- PDF/OCR;
- executable merge decisions;
- Core mutation;
- PHP compatibility work.

## Migration Effect

If accepted, later RW-01 work may move reusable types and services from `src/NexusScholar.Cli/ResearchWorkspace/` into `src/NexusScholar.ResearchWorkspace/`.

This migration must preserve:

- existing CLI commands;
- existing CLI output unless a separate CLI task intentionally changes it;
- existing CLI exit codes;
- project-relative path display behavior;
- local-folder workspace semantics.

No existing Core record schema, Search import contract, Deduplication contract, AppServices APP-01 block contract, UiContracts schema, or fixture authority changes as part of this ADR.

## Fixture Effect

This ADR does not create or modify scientific fixtures.

Later RW-01, RW-02, and UI-01 work may add deterministic local application-service or UI/read-model fixtures. Those fixtures must be labeled as app-projection or UI-preview fixtures. They must not become:

- Core scientific authority;
- PHP compatibility evidence;
- real Scopus exports;
- real Web of Science exports;
- Google Scholar scrapes;
- live provider evidence.

The generated local APP-01 bundle fixtures may be reused for preview smoke coverage when they remain clearly non-conformance and non-authoritative.

## Acceptance Criteria

- `NexusScholar.ResearchWorkspace` exists and has architecture tests preventing UI-framework, provider, persistence, cloud, and model-client dependencies.
- Existing CLI tests continue to pass.
- CLI output and exit codes remain stable unless intentionally changed in a separate CLI task.
- `WorkspaceOverviewReadModel`, `EvidenceRecordRow`, `ReviewQueueItem`, `DuplicateClusterSummary`, `DuplicateCandidateDetail`, and `LockedDecisionAction` exist before desktop UI.
- UI-01 can open and display a generated local APP-01 workspace without writing files.
- APP-01 merge actions are visibly locked and never executable.
- No absolute workspace path is displayed when a project-relative path is available.
- No provider, persistence, cloud/API, PDF/OCR, AI/model, Core mutation, executable merge decision, or PHP compatibility claim is introduced.

## Validation

For ADR-only changes:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

For later source changes, also run the affected project tests added by each work packet.

## Reversal Conditions

Revisit this ADR if any of the following becomes true:

1. CLI behavior cannot be preserved while extracting shared ResearchWorkspace services.
2. UI-01 needs durable settings, local app databases, or recent-workspace persistence before a preview can be evaluated.
3. Real users need executable merge decisions before read-only review inspection is valuable.
4. Provider/network/legal ADRs admit live evidence acquisition that changes the workspace service boundary.
5. A product desktop shell is accepted and the preview should graduate from `samples/` to `src/`.
6. APP-01 semantics change so merge-gate actions are no longer placeholder-only.

## Status Notes

This ADR is accepted as of 2026-07-02 by maintainer direction in the UI planning session.

Accepted points:

- the shared ResearchWorkspace service boundary is the correct architecture;
- the first desktop project should start as a preview sample;
- UI-01 remains read-only;
- UI-02 requires a separate accepted task;
- durable UI preferences require a later settings ADR.
