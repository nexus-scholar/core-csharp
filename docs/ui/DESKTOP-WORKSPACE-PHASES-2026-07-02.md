# Desktop Workspace Implementation Phases

Status: Accepted planning baseline
Date: 2026-07-02
Depends on: `docs/adr/0016-desktop-shell-and-researchworkspace-boundary.md`

## Purpose

This document turns the desktop UI plan into execution phases. It is intended for small, safe PRs that preserve the current Nexus boundaries:

- local workspace only;
- researcher-supplied files only;
- deterministic local Search/Deduplication/AppServices evidence;
- read-only review and cluster inspection first;
- display-only APP-01 merge gates;
- no provider calls;
- no persistence/database/API/cloud;
- no PDF/OCR;
- no AI/model calls;
- no Core mutation;
- no executable merge decisions;
- no PHP compatibility claims.

## Phase UI-00: Persist Imported Specs

Status: complete on `cdx/ui-guides-specs-plan`.

### Goal

Preserve the imported UI guide/spec pack and create a planning baseline.

### Outputs

```text
docs/ui/imported/2026-07-02-ui-guides-and-specs/
docs/ui/DESKTOP-WORKSPACE-PLAN-2026-07-02.md
docs/ui/README.md
docs/ui/ROADMAP.md
```

### Exit Criteria

- Imported files are preserved.
- Original archive hash and persisted file hashes are recorded.
- Imported HTML/React/CSS are labeled as visual guidance, not production web implementation.
- No source or test behavior changes.

## Phase UI-ADR-01: Desktop Shell And ResearchWorkspace Boundary

Status: accepted.

### Goal

Accept or reject the desktop/shared-service architecture before code moves.

### Output

```text
docs/adr/0016-desktop-shell-and-researchworkspace-boundary.md
```

### Required Decisions

- Create `NexusScholar.ResearchWorkspace` as the shared non-UI workspace library.
- Keep CLI commands as thin adapters over that library.
- Start desktop as `samples/NexusScholar.Desktop.Preview`, not `src/NexusScholar.Desktop`.
- Make UI-01 read-only.
- Defer UI workflow writes to UI-02.
- Defer durable preferences to a later settings ADR.
- Keep APP-01 merge actions locked/display-only.

### Allowed Paths

```text
docs/adr/**
docs/ui/**
docs/ops/**        # routing notes only, if needed
```

### Validation

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

## Phase RW-01: Extract Shared ResearchWorkspace Services

Status: complete in `cdx/rw01-researchworkspace-services`.

### Goal

Move reusable Research Workspace behavior out of CLI-only code into a shared non-UI application-service package.

### New Projects

```text
src/NexusScholar.ResearchWorkspace/
tests/NexusScholar.ResearchWorkspace.Tests/
```

### Move Or Recreate As Shared Services

Candidate service and model groups:

```text
ResearchWorkspaceProject
ResearchWorkspaceInput
ResearchWorkspaceLocation
ResearchWorkspacePaths
ResearchWorkspaceStore
ResearchWorkspaceJson
ResearchWorkspaceVerifier
ResearchWorkspaceAnalyzer
ResearchWorkspaceAnalysisResult
SearchImportAliases
ResearchWorkspaceExitCodes or structured status result equivalent
WorkspacePlanReader
```

CLI command classes should remain in `NexusScholar.Cli`:

```text
ResearchWorkspaceInitCommand
ResearchWorkspaceStatusCommand
SearchImportWorkspaceCommand
ResearchWorkspaceVerifyCommand
ResearchWorkspaceAnalyzeCommand
ResearchWorkspaceReviewCommand
ResearchWorkspaceClustersCommand
```

Those command classes should call shared services and format console output.

### Tasks

1. Create `NexusScholar.ResearchWorkspace`.
2. Move reusable records and services.
3. Keep file and folder behavior unchanged.
4. Keep current CLI output unchanged.
5. Add architecture tests:
   - no Avalonia references;
   - no desktop UI references;
   - no provider SDK references;
   - no persistence/database/cloud references;
   - no AI/model-client references.
6. Add behavior tests for:
   - project discovery from current folder;
   - project discovery from child folder;
   - missing project;
   - missing input;
   - digest mismatch;
   - unsupported schema;
   - unsupported import format;
   - no absolute path leakage when relative paths are available.

### Forbidden

- Avalonia;
- desktop shell;
- provider calls;
- persistence;
- cloud/API;
- AI/model calls;
- PDF/OCR;
- merge decision execution;
- Core scientific record changes;
- PHP compatibility work.

### Validation

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test tests/NexusScholar.ResearchWorkspace.Tests/NexusScholar.ResearchWorkspace.Tests.csproj -c Release --no-build
dotnet test tests/NexusScholar.Cli.Tests/NexusScholar.Cli.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

## Phase RW-02: Add UI-Friendly Read Models

Status: complete in `cdx/rw02-ui-read-models`.

### Goal

Expose structured read models for the desktop preview without parsing CLI console text.

### Read Models

```csharp
public enum WorkspaceState
{
    Missing,
    Initialized,
    Imported,
    ImportedWithWarnings,
    Analyzed,
    ReviewReady,
    NeedsAttention
}

public sealed record WorkspaceOverviewReadModel(...);
public sealed record WorkspaceAttentionItem(...);
public sealed record WorkflowStepReadModel(...);
public sealed record EvidenceRecordRow(...);
public sealed record EvidenceRecordDetail(...);
public sealed record ImportSourceSummary(...);
public sealed record VerificationHealthReadModel(...);
public sealed record AnalysisSummaryReadModel(...);
public sealed record ReviewQueueItem(...);
public sealed record DuplicateClusterSummary(...);
public sealed record DuplicateCandidateSummary(...);
public sealed record DuplicateCandidateDetail(...);
public sealed record LockedDecisionAction(...);
public sealed record EvidenceRefReadModel(...);
```

### Required Mapping

Build read models from:

```text
nexus.project.json
inputs/search/**
nexus-output/imports/*.import-trace.json
nexus-output/dedup/current.deduplication-result.json
nexus-output/workspace/current.workspace-plan.json
nexus-output/reports/review.md presence
```

### Required UI Support

The read models must support these UI screens:

1. Welcome/open workspace status summary.
2. Project Overview.
3. Evidence Records table.
4. Imports summary.
5. Verification health.
6. Analysis summary.
7. Review Queue.
8. Duplicate Clusters.
9. Duplicate Detail / Record Comparison.
10. Locked Decision panel.
11. Advanced evidence/digest inspector.

### Evidence Records Table Columns

The table should support a Zotero-style record grid:

```text
Title
Creators
Year
Venue
Source
Identifier
Warnings
Duplicate State
Import ID
```

Optional advanced columns:

```text
Source Record ID
Source Trace ID
Source File Digest
Raw Record Digest
Candidate ID
Cluster ID
```

### Tasks

1. Add read-model builders to `NexusScholar.ResearchWorkspace`.
2. Use project-relative paths by default.
3. Never expose full machine-local absolute paths unless explicitly requested for an OS shell action.
4. Represent APP-01 merge actions as `LockedDecisionAction`, never executable delegates.
5. Add deterministic object-level tests.
6. Add fixture coverage for:
   - initialized workspace;
   - imported workspace;
   - imported-with-warnings workspace;
   - analyzed audit workspace;
   - review-ready workspace;
   - digest mismatch;
   - missing generated workspace plan;
   - missing deduplication result;
   - malformed workspace plan;
   - locked APP-01 actions.

### Validation

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test tests/NexusScholar.ResearchWorkspace.Tests/NexusScholar.ResearchWorkspace.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

## Phase UI-01: Read-Only Desktop Preview Shell

Status: complete in `cdx/ui01-desktop-preview-shell`.

### Goal

Create a first Avalonia desktop preview that opens a real local workspace and displays state without executing workflow mutations.

### New Projects

```text
samples/NexusScholar.Desktop.Preview/
tests/NexusScholar.Desktop.Preview.Tests/
```

### Outputs

- Read-only Avalonia desktop preview sample.
- Workspace folder picker and manual path load.
- Stable left sidebar over RW-02 read models.
- Top boundary/status area for local-only and locked-decision constraints.
- Evidence records, imports, verification, analysis, review queue, duplicate clusters, duplicate detail, reports, and diagnostics screens.
- Locked APP-01 merge actions rendered disabled.
- View-model and layout smoke tests.
- Architecture guardrails for desktop preview dependencies and forbidden source symbols.

### Screens

1. Welcome / Open Workspace.
2. Project Overview.
3. Evidence Records.
4. Imports.
5. Verification.
6. Analysis.
7. Review Queue.
8. Duplicate Clusters.
9. Duplicate Detail / Record Comparison.
10. Reports / generated-output links.
11. Diagnostics placeholder.

### Avalonia Component Direction

```text
MainWindow
WorkspaceShellView
WelcomeView
ProjectOverviewView
EvidenceRecordsView
ImportsView
VerificationView
AnalysisView
ReviewQueueView
DuplicateClustersView
DuplicateDetailView
LockedDecisionPanel
EvidenceRefsPanel
AdvancedDetailsDrawer
CliEquivalentDrawer
```

### Tasks

1. Load an existing workspace folder.
2. Bind to RW-02 read models.
3. Render a stable left sidebar.
4. Render a top status bar:
   - workspace title;
   - state;
   - local-only;
   - no providers;
   - no executable decisions.
5. Render Evidence Records as a read-only table.
6. Render Review Queue as task inbox cards.
7. Render Duplicate Detail as a split comparison view.
8. Render merge actions as locked/disabled.
9. Add view-model tests for:
   - missing workspace;
   - review-ready workspace;
   - locked decision actions;
   - no-op action behavior.
10. Add architecture tests:
   - desktop preview does not reference Core domain packages directly for mutation;
   - desktop preview does not reference providers, persistence, cloud/API, or AI/model clients.

### Explicitly Forbidden In UI-01

- `init`;
- `import`;
- `verify`;
- `analyze`;
- accept merge;
- reject merge;
- mark unresolved;
- write project files;
- store scientific decisions;
- durable settings;
- providers;
- database/cloud/API;
- PDF/OCR;
- AI/model calls;
- PHP compatibility claims.

### Manual Validation

```powershell
dotnet run --project samples/NexusScholar.Desktop.Preview
```

Check:

- app opens;
- workspace folder can be selected;
- generated local APP-01 workspace displays;
- review/cluster/detail screens are reachable;
- locked merge actions are visibly disabled;
- no clipped lower content at short heights;
- no horizontal overflow in normal desktop widths.

### Automated Validation

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test tests/NexusScholar.Desktop.Preview.Tests/NexusScholar.Desktop.Preview.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

## Phase UI-02A: Safe Local Verify/Analyze Actions

Status: implemented after UI-01 feedback and explicit accepted task.

### Goal

Allow the UI to run non-decision local workflow actions through structured shared services.

### Allowed Actions

```text
verify local workspace
analyze local evidence
```

### Why Verify/Analyze First

`verify` and `analyze` write or inspect only existing local workspace state and generated outputs. They are safer than import because they do not introduce an external user-selected file copy/import wizard yet.

### Requirements

- Use `NexusScholar.ResearchWorkspace`, not CLI shell-out.
- Return structured success/failure results.
- Show recovery copy for missing files and digest mismatches.
- Refresh read models after success.
- Add file-write tests for generated outputs.
- Keep merge actions locked.

### Implemented Surface

- Desktop preview header buttons for `Verify` and `Analyze`.
- Shared `ResearchWorkspaceWorkflowActions` result boundary.
- `Verify` reads local files and refreshes verification state without writing generated outputs.
- `Analyze` writes only existing generated output files and updates project output references.
- Verification view shows recovery guidance from project-relative attention items.

### Forbidden

- import wizard;
- provider queries;
- merge decisions;
- AI/model calls;
- PDF/OCR;
- persistence/database/cloud/API;
- Core mutation outside existing generated-output behavior.

## Phase UI-02B: Safe Local Init/Import Actions

Status: after UI-02A or explicit accepted task.

### Goal

Add local workspace creation and local Search export import from the desktop UI.

### Allowed Actions

```text
initialize local workspace
import local Search export
```

### Requirements

- Create local workspace scaffolding through shared services.
- Import only user-selected local files.
- Support current aliases:
  - sources: Scopus, Web of Science, Google Scholar, OpenAlex, Semantic Scholar, Other;
  - formats: CSV, RIS, BibTeX.
- Compute and record digests.
- Write import traces.
- Show parser warnings as warnings, not fatal errors.
- Add overwrite/reimport as still unsupported unless a separate task accepts it.
- Use transaction-like safety where possible:
  - no partial project update if file copy fails;
  - clear recovery message if parse fails;
  - no hidden network behavior.

### Forbidden

- live providers;
- scraping;
- provider credentials;
- PDF/OCR;
- AI/model calls;
- executable merge decisions;
- database/cloud/API;
- PHP compatibility claims.

## Phase UI-03: Productization Decision ADR

Status: later.

### Goal

Decide whether the preview graduates from sample to product desktop project.

### Possible Decisions

- Keep preview sample only.
- Create `src/NexusScholar.Desktop`.
- Add durable app settings under OS app-data.
- Package installer.
- Add crash/logging policy.
- Add screenshot/golden visual tests.
- Add UX accessibility acceptance criteria.

### Still Separate ADRs

- merge decision persistence;
- actor identity;
- decision provenance;
- provider integration;
- PDF/OCR;
- AI proposal layer;
- cloud/API/sync.

## Global Boundary Search Before Merge

For every implementation packet, inspect capability claims:

```powershell
rg -n "provider|scrape|cloud|database|PDF|OCR|AI/model|accept merge|reject merge|mark unresolved|PHP compatibility" docs src tests samples
```

Expected hits are boundary-preserving non-claims or locked action labels. New capability claims require a separate accepted ADR.

## Global Exit Checklist

- ADR exists before source implementation.
- CLI behavior remains unchanged after RW-01.
- Shared ResearchWorkspace services have architecture tests.
- Read models avoid CLI console parsing.
- Read models use project-relative paths by default.
- Desktop preview opens a real generated local APP-01 workspace.
- Evidence table, review queue, duplicate clusters, and duplicate detail render from read models.
- APP-01 merge actions are locked/display-only.
- No provider, persistence, cloud/API, PDF/OCR, AI/model, Core mutation, executable merge decision, or PHP compatibility claim is added.
