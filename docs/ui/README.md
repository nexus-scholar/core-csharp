# Nexus Scholar UI Notes

This folder records the UI architecture and product evolution without moving
scientific authority into the desktop.

The UI lane is no longer preview-only. FE-08 slices 1 through 9 are complete:

- slices 1-2: local product host and admitted open, initialize, local Search
  import, verify, and analyze operations;
- Slice 3: first desktop scientific action, an authority-checked FE-02
  Deduplication decision;
- Slice 4: durable, fail-closed Screening authority resolution and read-only
  readiness projection;
- slices 5-9: title/abstract Screening conduct and resolution, local Full Text
  review, reporting, Bundle v2/export-ledger verification, and desktop
  recovery/accessibility closeout.

## Authority Model

```text
Avalonia view
    -> renderer-neutral UiContracts
    -> Desktop.AppServices command/readiness facade
    -> ResearchWorkspace durable transaction
    -> owning domain authority
```

The desktop may invoke a dedicated, accepted application command. It does not
own the actor, Protocol, criteria, decision, snapshot, invalidation, or
provenance rules implemented by that command.

This is the precise boundary:

- permitted: the desktop gathers explicit inputs, requests a preview, displays
  authority and effects, confirms a sealed request, invokes an admitted command,
  and refreshes from verified durable state;
- forbidden: a button, action descriptor, view model, selected row, local path,
  or cached UI value becoming scientific authority or bypassing the owning
  domain command.

## Current Product Projects

- `src/NexusScholar.UiContracts`: UI-framework-free workspace plans, research
  blocks, evidence references, validation references, and action descriptors.
- `src/NexusScholar.Avalonia.Blocks`: reusable Avalonia controls and view models
  that render `UiContracts`.
- `src/NexusScholar.ResearchWorkspace`: shared non-UI project discovery,
  durable generations, authority verification, manifest-verified backup and
  restore, projections, and local orchestration.
- `src/NexusScholar.AppServices`: framework-neutral use-case composition and
  admitted domain commands.
- `src/NexusScholar.Desktop.AppServices`: desktop-safe facade for open,
  initialize, import, verify, analyze, FE-02 Deduplication review, and
  Screening/Full Text/reporting workflows plus operational backup/restore.
- `src/NexusScholar.Desktop`: Windows-first Avalonia product host and composition
  root.

Historical and diagnostic surfaces:

- `samples/NexusScholar.Avalonia.Blocks.SampleHost`: non-authoritative block
  renderer inspection harness.
- `samples/NexusScholar.Desktop.Preview`: historical read-only preview over
  ResearchWorkspace read models.
- `samples/block-plans`: illustrative contract plans, not scientific fixtures.

Tests:

- `tests/NexusScholar.UiContracts.Tests`
- `tests/NexusScholar.Avalonia.Blocks.Tests`
- `tests/NexusScholar.Avalonia.Blocks.SampleHost.Tests`
- `tests/NexusScholar.ResearchWorkspace.Tests`
- `tests/NexusScholar.Desktop.Preview.Tests`
- `tests/NexusScholar.Desktop.AppServices.Tests`
- `tests/NexusScholar.Desktop.Tests`
- `tests/NexusScholar.Desktop.Acceptance.Tests`
- `tests/NexusScholar.Architecture.Tests`

## FE-08 Slice Boundaries

### Slices 1-2: product foundation

The host can:

- open and inspect an existing local Research Workspace;
- initialize a workspace;
- import researcher-supplied local Search exports;
- verify local inputs and generated state;
- run deterministic local analysis;
- surface success, attention, failure, stale, and recovery states.

It does not contact live providers, scrape, parse PDFs, call models, or create
database/cloud state.

### Slice 3: Deduplication review

The desktop can perform the three admitted FE-02 review actions through a
preview/confirmation command facade. The command binds actor, role, target,
source result, policy, predecessor snapshot, expected effects, request digest,
and stale-state material. Durable commit and recovery remain owned by
ResearchWorkspace and the Deduplication authority boundary.

This is a real scientific mutation, but it is not UI-owned authority.

### Slice 4: Screening authority readiness

The workspace can persist and rehydrate:

- canonical approved-Protocol authority;
- canonical title/abstract Screening criteria;
- exact FE-01 generation, result, decision-set, and snapshot bindings;
- immutable Screening authority packages;
- ready, unavailable, stale, invalid, and recovery-required states.

Desktop.AppServices receives readiness projections only. It cannot create a
Screening decision.

### Slices 5-9: desktop conduct and closeout

Slices 5 through 9 are complete:

- title/abstract Screening conduct, correction, adjudication, and handoff;
- local Full Text review and evidence handling;
- reporting, Bundle v2, export verification, and export-ledger publication;
- recovery and attention-state closure with accessibility updates.

### Alpha.2 release readiness

ADR 0046 adds an unsigned self-contained Windows x64 portable artifact,
sanitized local crash diagnostics, manifest-verified backup and new-directory
restore, and real Avalonia headless acceptance. These are product-operation
capabilities, not new scientific authority.

## Documents

- `UI-PHILOSOPHY.md`: strict internals, simple workflows, AI assistance, and
  human-authorized science.
- `PRODUCT-POSITIONING.md`: audit-grade workflow positioning rather than another
  paper summarizer.
- `BLOCK-FRAMEWORK-BLUEPRINT.md`: typed research interaction units.
- `BLOCK-CATALOG-v0.md`: early Import and Deduplication block families.
- `PORTABILITY-STRATEGY.md`: shared plans across desktop, CLI, web, and mobile
  without Core becoming UI-aware.
- `AI-ASSISTED-UI-RULES.md`: safe and unsafe AI roles.
- `RESEARCH-COCKPIT-CONCEPT.md`: workflow navigation, adaptive workspace,
  assistant, and evidence/provenance inspection.
- `BEGINNER-VS-AUDIT-MODE.md`: simplified and audit-rich views over the same
  underlying records.
- `UI-CONTRACTS-v0.md`: initial renderer-neutral contract summary.
- `DEDUP-REVIEW-WORKSPACE-v0.md`: early Deduplication review concept.
- `SCREENING-WORKSPACE-v0.md`: early Screening workspace concept.
- `AVALONIA-RENDERER-PROTOTYPE-v0.md`: renderer-only prototype.
- `AVALONIA-SAMPLE-HOST-v0.md`: sample inspection host.
- `DESKTOP-WORKSPACE-PLAN-2026-07-02.md`: staged desktop architecture plan.
- `DESKTOP-WORKSPACE-PHASES-2026-07-02.md`: dependency-ordered historical
  desktop phases.
- `ROADMAP.md`: current UI delivery history and next boundary.
- `OPEN-QUESTIONS.md`: unresolved product and technical questions.
- `imported/2026-07-02-ui-guides-and-specs/`: preserved imported UI guide/spec
  pack.

Accepted architecture and gate anchors:

- `../adr/0016-desktop-shell-and-researchworkspace-boundary.md`
- `../adr/0035-local-product-desktop-shell-and-command-facade.md`
- `../adr/0036-desktop-human-authorized-deduplication-review.md`
- `../adr/0037-durable-screening-authority-resolution.md`
- `../gates/FE-08-LOCAL-PRODUCT-DESKTOP-SLICES-1-2.md`
- `../gates/FE-08-DESKTOP-DEDUPLICATION-REVIEW-SLICE-3.md`
- `../gates/FE-08-SCREENING-AUTHORITY-RESOLUTION-SLICE-4.md`

## Dependency Boundary

Core domain projects must not reference `NexusScholar.UiContracts`, Avalonia,
desktop projects, application services, persistence frameworks, or model
clients.

`NexusScholar.UiContracts` has no UI-framework dependency. The product host
reaches workspace behavior through `NexusScholar.Desktop.AppServices`; it does
not reference Core domain modules or `NexusScholar.ResearchWorkspace` directly.

Any UI work that changes scientific authority, record schemas, digest material,
provenance, invalidation, persistence, AI acceptance, or compatibility requires
the normal ADR, fixture, gate, and verification path.

## Run the Product Host

```powershell
dotnet run --project src/NexusScholar.Desktop/NexusScholar.Desktop.csproj -c Release
```

The current product uses durable local files. It does not provide database,
server API, cloud sync, authentication, tenancy, or multi-user collaboration.
Crash reports stay local and sanitized. Workspace restore never overwrites or
merges an existing workspace.
