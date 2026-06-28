# UI Roadmap

This roadmap stages future UI/UX work without changing Core scientific behavior.

## Phase 0: Docs And UI Philosophy

Phase 0 is the planning layer for product philosophy, block concepts, cockpit layout, AI boundaries, portability, and initial workflow prototypes.

## Phase 1: UiContracts Only

Status: implemented as the first contract layer.

Created package:

- `src/NexusScholar.UiContracts`

Created tests:

- `tests/NexusScholar.UiContracts.Tests`

Scope:

- workspace plan id, title, mode, block list, optional description, and context references;
- research block id, extensible string kind, title, mode, severity, source kind, evidence refs, validation refs, actions, optional summary, and optional object-root JSON payload;
- lightweight evidence refs;
- lightweight validation refs;
- action descriptors with human-confirmation and destructive-action flags;
- beginner, audit, review, and repair mode vocabulary;
- severity, source-kind, and action-kind vocabulary;
- JSON serialization round-trip tests;
- architecture guard that Core assemblies do not reference `NexusScholar.UiContracts`.

Phase 1 deliberately did not add Avalonia, app services, renderers, AI execution, persistence, or Core behavior changes. `NexusScholar.UiContracts` has no project references and no UI-framework dependency.

## Phase 2: Sample Block Plans

Status: implemented as contract-backed samples.

Created sample plans under `samples/block-plans` using `NexusScholar.UiContracts` JSON shape for:

- import warning;
- dedup review;
- bundle verification.

The samples deserialize into `WorkspacePlan`, round-trip through the contract test layer, preserve block order, validate object-root `PayloadJson`, and avoid renderer-specific fields. They remain illustrative. They are not Core authority, not ADRs, not scientific fixtures, and not PHP compatibility fixtures.

No Screening sample was added in Phase 2 because Screening Core is still outside this UI sample task.

## Phase 3: Avalonia Block Renderer Prototype

Create an isolated Avalonia renderer package after Phase 2 proves sample plans:

- `NexusScholar.Avalonia.Blocks`

Renderer actions must route through application services later. They must not call Core mutation directly.

## Later Phases

- Import/Dedup workspace prototype.
- Screening workspace after Core Screening implementation and fixtures are ready.
- CLI renderer for the same contracts.
- Persistence and app state separation.
- AI proposal layer.
- Web/mobile exploration after desktop/CLI contract loops are proven.

Do not build mobile first. Do not connect AI mutation directly to Core.
