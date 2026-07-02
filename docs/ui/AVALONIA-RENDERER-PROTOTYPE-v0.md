# Avalonia Renderer Prototype v0

Phase 3 introduces `NexusScholar.Avalonia.Blocks` as a renderer-only proof for `WorkspacePlan` JSON.

This is not a desktop application shell. It does not load Core state, compose workspace plans from domain records, execute Core commands, persist app state, or run AI. It consumes already-created `WorkspacePlan` data and renders it with Avalonia controls.

## Input Path

The only supported Phase 3 path is:

1. sample `WorkspacePlan` JSON;
2. `System.Text.Json` deserialization through `NexusScholar.UiContracts`;
3. renderer view models;
4. Avalonia controls;
5. no-op or caller-provided action callbacks.

The renderer path remains display-only. AppServices may compose `WorkspacePlan` data elsewhere, but this renderer does not load Core state, compose plans, persist decisions, execute commands, or invoke Core mutation.

## Project Boundary

`NexusScholar.Avalonia.Blocks` may reference:

- `NexusScholar.UiContracts`;
- Avalonia rendering packages.

It must not reference Core domain projects such as Kernel, Protocol, Workflow, Artifacts, Provenance, Shared, Search, Deduplication, Screening, Bundles, Extensibility, or AI.

Core projects must not reference `NexusScholar.UiContracts`, Avalonia, or `NexusScholar.Avalonia.Blocks`. `NexusScholar.UiContracts` must remain Avalonia-free.

## Renderer Surface

The prototype exposes:

- `WorkspacePlanView`;
- `ResearchBlockView`;
- `EvidenceRefListView`;
- `ValidationRefListView`;
- `BlockActionListView`;
- `PayloadJsonView`.

The renderer shows workspace title, mode, description, sample/non-authoritative status, block title, summary, mode, severity, source kind, evidence refs, validation refs, actions, human-confirmation flags, destructive-action flags, and payload JSON in an audit/details area.

Action buttons are placeholders. They call a supplied callback when present and otherwise do nothing. The callback receives ids and flags; it is not a Core command boundary.

## Authority Boundary

Sample plans remain non-authoritative. They are not Core records, ADRs, scientific fixtures, conformance fixtures, PHP compatibility fixtures, or renderer conformance evidence.

The renderer displays authority status when the plan identifies itself as a sample or non-authoritative input. It must not reinterpret scientific authority, approve protocol decisions, accept merge decisions, record provenance, or mutate records.
