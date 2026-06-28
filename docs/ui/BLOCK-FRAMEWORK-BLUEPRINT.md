# Block Framework Blueprint

Nexus Scholar Blocks are typed research interaction units. They are not merely visual widgets. A block represents a research situation that needs explanation, evidence inspection, user judgment, or a valid future application command.

Blocks should make Core strictness renderable without making Core depend on any UI framework.

## Flow

```text
Core state / validation report / workflow step
  -> situation detector
  -> block composer
  -> typed block plan
  -> renderer
  -> human action
  -> Core command
  -> validation + provenance
```

The situation detector and block composer belong outside Core, likely in future application services. Core should expose scientific records, validation results, and commands. It should not know whether the caller is Avalonia, CLI, web, or mobile.

## Phase 1 Contract Layer

Phase 1 introduced `NexusScholar.UiContracts` as a UI-framework-free contract library. It currently defines:

- `WorkspacePlan`;
- `ResearchBlockDescriptor`;
- `EvidenceRef`;
- `ValidationRef`;
- `BlockActionDescriptor`;
- block mode, severity, source-kind, and action-kind vocabularies;
- known block, action, command, and evidence-reference constants.

The contract keeps block kind extensible as a string so future blocks can be added without changing a central enum. The optional `PayloadJson` field is intentionally conservative: when present, it must be valid JSON with an object root. This supports typed payload evolution later without accepting arbitrary object dictionaries.

## Contract Responsibilities

Block contracts may define:

- ids and titles;
- block kind;
- display mode;
- severity;
- source kind;
- evidence references;
- validation references;
- action descriptors;
- human-confirmation requirements;
- optional summary;
- optional object-root JSON payload.

Block contracts must not define:

- Avalonia controls;
- CSS classes;
- web routes;
- mobile views;
- terminal rendering;
- database row identities;
- provider SDK clients;
- model prompts that mutate Core;
- arbitrary generated C# or XAML;
- scientific decisions not accepted by Core.

## Architecture Boundary

- `NexusScholar.Core`: strict scientific logic and records.
- `NexusScholar.UiContracts`: platform-neutral block descriptors and workspace plans. Implemented in Phase 1.
- `NexusScholar.AppServices`: future mapper from Core state and validation to block plans.
- `NexusScholar.Avalonia.Blocks`: future Avalonia renderer.
- Future CLI, web, and mobile renderers.

Core must remain UI-free. It must not reference Avalonia, web UI frameworks, mobile frameworks, terminal renderers, or `NexusScholar.UiContracts`.

Architecture tests assert that Core assemblies do not reference `NexusScholar.UiContracts`. Contract tests assert that `NexusScholar.UiContracts` has no Avalonia, MAUI, web, or terminal-network dependency.
