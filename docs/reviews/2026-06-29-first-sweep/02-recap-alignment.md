# Recap Alignment

Status: comparison of supplied recaps against the current repo.

## Core Recap Alignment

The supplied Core recap says the project is an audit-grade C# research workflow kernel with strict deterministic records, protocol authority, workflow planning, provenance, bundles/artifacts, shared identity, Search/import evidence, Deduplication, Screening, and a Full Text contract.

The live repo supports that framing:

- Core pipeline projects exist under `src/` for Kernel, Protocol, Workflow, Artifacts, Provenance, Shared, Search, Deduplication, Screening, Bundles, Extensibility, AI, and CLI.
- Conformance fixtures exist for kernel, protocol, workflow, provenance, bundles, shared identity, search, deduplication, and screening.
- ADR 0014 exists for Full Text contract scope, while implementation remains future.
- The branch does not add persistence or full app integration.

The Core recap also says there is no persistence/database layer yet. The live repo matches that: `docs/persistence/` does not exist and `docs/adr/0015-local-workspace-persistence-boundary.md` does not exist.

## UI Recap Alignment

The supplied UI recap says:

1. Phase 0 produced UI planning docs.
2. Phase 1 added `NexusScholar.UiContracts`.
3. Phase 2 added contract-backed sample block plans.
4. Phase 3 added `NexusScholar.Avalonia.Blocks`.
5. Phase 3.5 added the sample host.

The live repo matches that structure:

- `docs/ui/` contains UI philosophy, product positioning, block framework, roadmap, portability, AI rules, research cockpit, beginner/audit mode, workspace concepts, and renderer/host docs.
- `src/NexusScholar.UiContracts/BlockContracts.cs` defines `WorkspacePlan`, `ResearchBlockDescriptor`, `EvidenceRef`, `ValidationRef`, `BlockActionDescriptor`, enums, known vocabularies, JSON options, and guards.
- `samples/block-plans/` contains:
  - `import-warning.sample.json`
  - `dedup-review.sample.json`
  - `bundle-verification.sample.json`
- `src/NexusScholar.Avalonia.Blocks/` contains renderer controls and view models.
- `samples/NexusScholar.Avalonia.Blocks.SampleHost/` contains the visual harness.
- Tests exist for UiContracts, sample plans, renderer view models, sample host loading, and architecture boundaries.

## Authority Boundary Alignment

The recaps repeatedly state that UI samples and renderer code are not Core authority. The repo agrees:

- `docs/ui/UI-CONTRACTS-v0.md` says no app service composes block plans from Core state and no persistence/app-state boundary exists (`docs/ui/UI-CONTRACTS-v0.md:90`, `docs/ui/UI-CONTRACTS-v0.md:92`).
- `docs/ui/AVALONIA-RENDERER-PROTOTYPE-v0.md` says action callbacks are not a Core command boundary (`docs/ui/AVALONIA-RENDERER-PROTOTYPE-v0.md:43`).
- `docs/ui/AVALONIA-SAMPLE-HOST-v0.md` says no Core state is loaded and no records are created, updated, approved, merged, screened, exported, or persisted (`docs/ui/AVALONIA-SAMPLE-HOST-v0.md:18`).
- Sample JSON descriptions label themselves as not Core authority, not ADRs, not scientific fixtures, and not PHP compatibility fixtures.

## Alignment Gaps

1. The UI recap says current status includes CI-validated PRs. I did not verify remote PR/CI state in this sweep. Locally, build/test/format passed.

2. `docs/ui/README.md` did not fully catch up with Phase 3/3.5 implementation. It still reads like all UI work is planning-only, while more specific docs correctly describe the implemented contract, renderer, and host.

3. The persistence planning base is not yet represented in repo docs. That is expected if this sweep is before the persistence documentation slice.

## Bottom Line

The reviewer recaps are broadly accurate against the current implementation. The main correction is wording precision: the branch has implemented UI contract/renderer/sample-host infrastructure, but it has not implemented app services, persistence, Core command execution, real workflow UI, or scientific mutation.
