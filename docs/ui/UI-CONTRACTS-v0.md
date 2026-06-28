# UI Contracts v0

Phase 1 created `NexusScholar.UiContracts`, a platform-neutral contract layer for future Nexus Scholar workspaces and research blocks.

This is an implementation boundary document, not an ADR and not Core scientific authority.

## What It Is

`NexusScholar.UiContracts` defines the shared language that future renderers and application services can use to describe research workspaces:

- `WorkspacePlan`;
- `ResearchBlockDescriptor`;
- `EvidenceRef`;
- `ValidationRef`;
- `BlockActionDescriptor`.

It also provides early vocabularies for block mode, severity, source kind, and action kind, plus known constants for initial block kinds, action names, command kinds, and evidence reference kinds.

## What It Is Not

`NexusScholar.UiContracts` is not:

- a renderer;
- an Avalonia project;
- a web, mobile, or CLI renderer;
- an app service layer;
- Core scientific authority;
- a persistence model;
- an AI execution layer;
- a PHP compatibility claim;
- a schema for arbitrary generated UI code.

It does not mutate Core records. It does not approve protocols, merge duplicates, make screening decisions, verify bundles, or record provenance.

## Dependency Boundary

`NexusScholar.UiContracts` has no project references. It uses `System.Text.Json` for serialization and does not depend on Avalonia, MAUI, ASP.NET Core, terminal rendering frameworks, provider SDKs, or AI SDKs.

Core remains UI-free. Architecture tests assert that Core assemblies do not reference `NexusScholar.UiContracts`.

## Rendering Strategy

The same `WorkspacePlan` can be rendered differently:

- Desktop can show dense side-by-side comparison, inspectors, and action panels.
- CLI can render numbered textual choices with evidence and action ids.
- Web can render browser-native layouts while preserving action authority.
- Mobile can render stacked cards and drill-down audit details later.

Renderer differences must not change the authority model. If a block action requires human confirmation, every renderer must preserve that requirement.

## AI Boundary

AI may propose typed block plans, explanations, draft rationales, or suggested commands. Those outputs remain proposals until a trusted application layer validates them and a human performs any required acceptance action.

AI must not generate arbitrary XAML/C# mutation, connect directly to Core mutation, approve protocols, finalize merge decisions, finalize screening decisions, overwrite evidence, or claim compatibility.

## Payload Strategy

The v0 payload strategy is intentionally conservative:

- `PayloadJson` is nullable.
- If supplied, it must be valid JSON.
- The root must be a JSON object.
- The contract does not accept arbitrary `object` dictionaries.
- No complex polymorphic payload schema exists yet.

## Phase 2 Samples

Phase 2 added contract-backed sample plans under `samples/block-plans`:

- `import-warning.sample.json`;
- `dedup-review.sample.json`;
- `bundle-verification.sample.json`.

These samples deserialize into `WorkspacePlan` and are tested through `NexusScholar.UiContracts.Tests`. They are intended to help future desktop and CLI renderer prototypes consume realistic block plans without inventing contracts in renderer code.

The samples remain non-authoritative:

- not Core records;
- not ADRs;
- not scientific fixtures;
- not conformance fixtures;
- not PHP compatibility fixtures;
- not renderer implementations.

## Current Limitations

- No renderer exists yet.
- No app service composes block plans from Core state.
- No Core commands are invoked from block actions.
- No persistence or app state boundary is implemented.
- No AI proposal storage or provenance acceptance path is implemented.
- No PHP compatibility claim is made.
- Sample payloads are simple JSON strings, not typed payload records.

Phase 3 can use these samples to build an isolated renderer prototype without changing Core.
