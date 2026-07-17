# Desktop Workspace UI Plan - 2026-07-02

Status: historical planning baseline; later accepted ADRs and FE-08 evidence supersede it as a current-state description.

Use `docs/ui/README.md`, `docs/ui/ROADMAP.md`,
`docs/ops/BRANCH-BOARD.md`, and `site/status/index.html` for current routing.

This plan converts the imported UI guide/spec pack into dependency-ordered work that a smaller model can execute. It does not authorize implementation by itself. Start implementation only after the first ADR/task below is accepted.

## Goal

Build a local-first C# desktop workspace experience for the implemented Research Workspace CLI loop:

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

The UI should feel like a professional JetBrains-style desktop workspace for research evidence, while preserving Nexus scientific boundaries:

- local folder project model;
- researcher-supplied local files;
- deterministic local Search/Deduplication/AppServices evidence;
- read-only review and cluster inspection;
- display-only APP-01 merge gates.

## Source References

- Imported spec pack: `docs/ui/imported/2026-07-02-ui-guides-and-specs/`
- Main imported spec: `docs/ui/imported/2026-07-02-ui-guides-and-specs/nexus-csharp-core-jetbrains-ui-spec-v3.md`
- Current CLI contract: `docs/cli/RESEARCH-WORKSPACE-CLI-v0.md`
- Current ops state: `docs/ops/BRANCH-BOARD.md`
- UI contracts: `docs/ui/UI-CONTRACTS-v0.md`
- Avalonia renderer prototype: `docs/ui/AVALONIA-RENDERER-PROTOTYPE-v0.md`
- Sample host: `docs/ui/AVALONIA-SAMPLE-HOST-v0.md`
- APP-01 ADR: `docs/adr/0015-app-services-readonly-workspace-composition.md`
- Desktop boundary ADR draft: `docs/adr/0016-desktop-shell-and-researchworkspace-boundary.md`
- Desktop workspace phases: `docs/ui/DESKTOP-WORKSPACE-PHASES-2026-07-02.md`
- Current implementation: `src/NexusScholar.Cli/ResearchWorkspace/`, `src/NexusScholar.AppServices/`, `src/NexusScholar.UiContracts/`, `src/NexusScholar.Avalonia.Blocks/`

## Non-Negotiable Boundary

A Nexus research project is a local folder. `nexus.project.json` is a local project index, not a database and not canonical scientific authority.

The UI may verify local files, analyze imported Search/Deduplication evidence, and show records requiring human review. It must not query live providers or execute merge decisions.

APP-01 merge-gate actions are placeholders only. They must not mutate Core records, execute commands, write files, call services, or imply that the CLI/UI can finalize a scientific decision.

Do not add persistence/database/API/cloud, live providers, scraping, provider credentials, PDF/OCR, AI/model calls, Core mutation, executable merge decisions, or PHP compatibility claims.

## Proposed Work Packets

### UI-00 - Persist Imported Specs

Owner: docs/ui.

Status: complete in this planning branch.

Tasks:

1. Preserve the supplied UI guide/spec files under `docs/ui/imported/2026-07-02-ui-guides-and-specs/`.
2. Record archive and file SHA-256 hashes.
3. Label the imported HTML/React/CSS as visual guide material, not production web code.
4. Link this plan and import from `docs/ui/README.md` and `docs/ui/ROADMAP.md`.

Validation:

```powershell
git diff --check
```

### UI-ADR-01 - Desktop Shell And ResearchWorkspace Boundary ADR

Owner: docs/adr.

Status: accepted as `docs/adr/0016-desktop-shell-and-researchworkspace-boundary.md`.

Goal: decide whether and how to introduce a durable desktop UI and a shared ResearchWorkspace application-service layer.

Allowed paths:

- `docs/adr/**`
- `docs/ui/**`
- `docs/ops/**` only for routing updates

Required decisions:

1. Whether reusable Research Workspace logic should move from `NexusScholar.Cli` into a shared non-UI library.
2. Whether the desktop app starts as `samples/NexusScholar.Desktop.Preview` or `src/NexusScholar.Desktop`.
3. Which commands may be invoked from UI v0: status-only, read-only browse, or import/verify/analyze.
4. Where local UI preferences may be stored, if any.
5. Whether UI app settings require a later persistence ADR.
6. How to keep merge actions display-only and locked.

Exit checklist:

- ADR explicitly accepts or rejects the shared library.
- ADR names allowed project references.
- ADR states that `NexusScholar.Core` remains UI-free.
- ADR preserves local-only/no-provider/no-decision-execution boundaries.
- No source code changed.

Validation:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

### RW-01 - Extract Shared ResearchWorkspace Services

Owner: application-services.

Prerequisite: UI-ADR-01 accepted.

Status: complete in `cdx/rw01-researchworkspace-services`.

Goal: make CLI and future desktop UI share the same workspace logic without shelling out to CLI text.

Allowed paths:

- `src/NexusScholar.ResearchWorkspace/**` if ADR accepts a new project
- `src/NexusScholar.Cli/**`
- `tests/NexusScholar.Cli.Tests/**`
- new `tests/NexusScholar.ResearchWorkspace.Tests/**` if a new project is added
- `NexusScholar.Core.slnx`
- project files needed for references

Tasks:

1. Move reusable workspace records and services out of `src/NexusScholar.Cli/ResearchWorkspace/`.
2. Keep CLI command classes as thin adapters.
3. Preserve existing CLI output exactly unless a test is intentionally updated.
4. Add architecture tests so the shared library has no UI framework, provider SDK, persistence, cloud, or model-client dependency.
5. Add tests proving existing CLI workflow behavior is unchanged.

Do not:

- add Avalonia;
- add persistence;
- add providers;
- add merge decision execution;
- change Core scientific records.

Negative cases:

- missing project remains exit `2`;
- digest mismatch remains exit `3`;
- unsupported format/schema remains exit `4`;
- absolute paths are not printed by default;
- no live provider symbols in the new shared project.

Validation:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test tests/NexusScholar.Cli.Tests/NexusScholar.Cli.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

### RW-02 - Add UI-Friendly Read Models

Owner: application-services.

Prerequisite: RW-01.

Status: complete in `cdx/rw02-ui-read-models`.

Goal: expose structured read models that match the imported UI spec without parsing CLI console text.

Allowed paths:

- shared ResearchWorkspace project from RW-01
- tests for that project
- `docs/ui/**` for read-model notes

Suggested read models:

- `WorkspaceState`
- `WorkspaceOverviewReadModel`
- `WorkspaceAttentionItem`
- `WorkflowStepReadModel`
- `EvidenceRecordRow`
- `ReviewQueueItem`
- `DuplicateClusterSummary`
- `DuplicateCandidateDetail`
- `LockedDecisionAction`

Tasks:

1. Build read models from `nexus.project.json`, import traces, dedup result, workspace plan, and review report presence.
2. Use project-relative paths by default.
3. Surface parser warnings, skipped records, digest mismatches, missing files, exact clusters, review candidates, and blocking merge gates.
4. Represent APP-01 actions as locked/display-only.
5. Add deterministic JSON or object-level tests.

Negative cases:

- missing workspace;
- child folder workspace discovery;
- changed input digest;
- missing generated output;
- malformed workspace plan;
- locked merge action is never executable.

Validation:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

### UI-01 - Desktop Preview Shell, Read-Only First

Owner: desktop-ui.

Prerequisite: UI-ADR-01 and RW-02.

Status: complete in `cdx/ui01-desktop-preview-shell`.

Goal: create a first desktop preview that opens a local workspace and displays state without executing workflow mutations.

Allowed paths:

- desktop project accepted by UI-ADR-01
- desktop tests
- docs/ui screenshots or notes
- `NexusScholar.Core.slnx`

Screens:

1. Welcome / recent workspace placeholder.
2. Open existing local workspace folder.
3. Project overview mapped to `nexus status`.
4. Evidence records table from read models.
5. Review queue.
6. Duplicate clusters.
7. Duplicate detail / record comparison with locked decision actions.

Tasks:

1. Use Avalonia and the existing visual language from the imported guide.
2. Keep UI dense, professional, and workspace-oriented.
3. Do not show marketing hero content.
4. Keep status bars and side navigation stable under resizing.
5. Add smoke tests for view models and no-op/locked action behavior.

Do not:

- execute `init`, `import`, `verify`, `analyze`, `accept merge`, `reject merge`, or `mark unresolved`;
- write project files;
- store scientific decisions;
- call providers;
- add database/cloud/API behavior.

Manual validation:

```powershell
dotnet run --project <desktop-preview-project>
```

Check:

- opens normally;
- can select a local workspace folder;
- displays state and generated outputs;
- review/cluster screens are reachable;
- locked decision actions are visibly disabled;
- no horizontal overflow or clipped lower content at short window heights.

### UI-02 - Import/Verify/Analyze Workflow Actions

Owner: desktop-ui plus application-services.

Prerequisite: explicit accepted task after UI-01 feedback.

Goal: add safe local workflow execution for non-decision commands only.

Allowed actions:

- initialize local workspace;
- import local Search export;
- verify local workspace;
- analyze local evidence.

Still forbidden:

- executable merge decisions;
- provider queries;
- database/cloud/API;
- AI/model calls;
- PDF/OCR;
- Core mutation outside existing local workflow outputs.

Exit checklist:

- UI and CLI use the same shared services.
- Command results are structured, not parsed from console text.
- Failed operations show clear recovery copy.
- File writes are covered by tests.
- No decision action writes state.

## Fixtures

Use existing generated local APP-01 bundle fixtures:

```text
tests/NexusScholar.AppServices.Tests/Fixtures/App01GeneratedLocalBundles/bundles/FB07-combined-app01-demo/
```

Add UI/read-model fixtures only when they are clearly app-projection fixtures, not scientific Core fixtures and not PHP compatibility evidence.

Required fixture scenarios:

- clean initialized workspace;
- imported workspace;
- imported-with-warnings workspace;
- analyzed audit workspace;
- review-ready workspace with blocking merge gates;
- needs-attention workspace with digest mismatch;
- missing generated workspace plan;
- missing deduplication result;
- malformed workspace plan;
- locked APP-01 action descriptors.

## Risks And ADR Needs

- Moving CLI workspace logic into a shared library changes project boundaries and needs UI-ADR-01 first.
- A product desktop shell can imply persistence or scientific authority unless copy and architecture keep it local/read-only.
- UI preferences must not be written into `nexus.project.json`.
- Import/verify/analyze from UI write local files and need transaction/error-copy decisions.
- Merge decision execution needs a later ADR for actor identity, decision persistence, provenance semantics, and mutation rules.
- Provider integration needs a separate provider/network/legal ADR.

## Global Validation Commands

For every packet:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

Add affected project tests when a packet creates a new project.

Search boundary text before merge:

```powershell
rg -n "provider|scrape|cloud|database|PDF|OCR|AI/model|accept merge|reject merge|mark unresolved|PHP compatibility" docs src tests samples
```

Inspect any hits. Boundary-preserving non-claims are expected; new capability claims are not.

## Measurable Exit Checklist

- Imported specs are preserved with hashes.
- ADR exists before any desktop shell or shared service extraction.
- CLI behavior remains unchanged after shared service extraction.
- Read models use project-relative paths by default.
- Desktop preview can display a real generated APP-01 workspace.
- Review and cluster screens display locked merge gates only.
- No provider, persistence, API/cloud, PDF/OCR, AI/model, Core mutation, or PHP compatibility claim is added.
- Documentation tells future agents to collect feedback before expanding scope.
