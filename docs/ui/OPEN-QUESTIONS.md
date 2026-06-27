# Open Questions

These questions should be resolved before implementing durable renderers or app services.

## Resolved For v0

- Minimal contract shape: `WorkspacePlan`, `ResearchBlockDescriptor`, `EvidenceRef`, `ValidationRef`, and `BlockActionDescriptor`.
- Block kind is an extensible string.
- Mode, severity, source kind, and action kind use enums.
- Payloads use optional `PayloadJson`.
- `PayloadJson`, when supplied, must be valid JSON with an object root.
- Workspace/context references use lightweight refs rather than Core types.
- `NexusScholar.UiContracts` is a standalone project with no project references.
- Phase 2 samples exist for import warnings, dedup review, and bundle verification.
- Phase 2 samples are tested as `WorkspacePlan` JSON and remain non-authoritative.

## Still Open

- Should block ids be stable across a session, a workspace, or an exported bundle?
- How should block contract versions be represented beyond assembly version and kind strings?
- What constitutes a breaking block change?
- Should renderer compatibility be tested through golden JSON examples?
- Should sample plans become conformance fixtures after contracts stabilize? Current answer: not yet.
- How should AI proposals be stored before acceptance?
- How should prompt and response digests be exposed in UI blocks?
- How should accepted AI suggestions be recorded in provenance or domain records?
- Which UI actions create Core records and which create app-local projections only?
- When should a provenance preview become an actual provenance event?
- How should human actor identity be captured in a local desktop workflow?
- When should app persistence be introduced?
- Should Avalonia renderer code live in this repo during prototyping?
- Should web/mobile renderers live in separate repos?
- When should sample payloads become typed payload records instead of object-root JSON strings?
