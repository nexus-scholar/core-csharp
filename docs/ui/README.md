# Nexus Scholar UI Notes

This folder tracks UI and UX work for Nexus Scholar without changing Core scientific authority. Authoritative scientific behavior remains in `specs/`, accepted ADRs, fixtures, observable pinned PHP behavior, and the current C# implementation.

The current UI lane has moved past preview-only work. FE-08 slices 1 and 2 add a
local product host, structured desktop command facade, and safe open, initialize,
local Search import, verify, and analyze workflows. FE-08 Slice 3 adds the first
human-authorized desktop scientific action: verified FE-02 deduplication review.
The earlier preview remains a sample. The host is not scientific authority;
policy-assigned human actors remain the authority.

## Documents

- `UI-PHILOSOPHY.md`: product philosophy for strict internals, simple workflows, AI assistance, and human-authorized science.
- `PRODUCT-POSITIONING.md`: market and wedge positioning for an audit-grade research workflow system rather than another paper summarizer.
- `BLOCK-FRAMEWORK-BLUEPRINT.md`: early architecture for Nexus Scholar Blocks as typed research interaction units.
- `BLOCK-CATALOG-v0.md`: candidate block families and first prototype candidates around Import and Deduplication.
- `PORTABILITY-STRATEGY.md`: how shared block plans can render across desktop, CLI, web, and mobile without making Core UI-aware.
- `AI-ASSISTED-UI-RULES.md`: safe and unsafe AI roles in the user experience.
- `RESEARCH-COCKPIT-CONCEPT.md`: desktop shell concept with workflow navigation, adaptive workspace, assistant, and evidence/provenance inspector.
- `BEGINNER-VS-AUDIT-MODE.md`: how the same block can be rendered differently for beginner and audit users.
- `UI-CONTRACTS-v0.md`: Phase 1 contract-layer summary for `NexusScholar.UiContracts`.
- `DEDUP-REVIEW-WORKSPACE-v0.md`: first serious workflow prototype concept for review-required duplicate candidates.
- `SCREENING-WORKSPACE-v0.md`: early screening workspace concept aligned with human decision authority.
- `AVALONIA-RENDERER-PROTOTYPE-v0.md`: Phase 3 renderer-only Avalonia block prototype.
- `AVALONIA-SAMPLE-HOST-v0.md`: Phase 3.5 sample host for manually inspecting the renderer.
- `imported/2026-07-02-ui-guides-and-specs/`: preserved imported desktop UI guide/spec pack, including the JetBrains-style C# UI spec and HTML/React visual guides.
- `DESKTOP-WORKSPACE-PLAN-2026-07-02.md`: staged plan for turning the imported UI specs into ADR, shared ResearchWorkspace services, read models, and a desktop preview.
- `DESKTOP-WORKSPACE-PHASES-2026-07-02.md`: dependency-ordered desktop workspace phases from ADR review through shared services, read models, read-only preview, and later local workflow actions.
- `../adr/0016-desktop-shell-and-researchworkspace-boundary.md`: accepted ADR for the shared ResearchWorkspace service boundary and first desktop preview scope.
- `ROADMAP.md`: staged path from documentation to UI contracts, sample block plans, renderers, and later AI proposal support.
- `OPEN-QUESTIONS.md`: product and technical questions that remain open.

## Implemented UI Packages

- `src/NexusScholar.UiContracts`: renderer-neutral `WorkspacePlan`, `ResearchBlockDescriptor`, evidence refs, validation refs, action descriptors, and display mode vocabulary.
- `src/NexusScholar.Avalonia.Blocks`: Avalonia controls and view models that render `WorkspacePlan` data.
- `src/NexusScholar.ResearchWorkspace`: shared non-UI local Research Workspace services and read models for CLI and desktop preview use.
- `samples/block-plans`: sample-only workspace plan JSON files.
- `samples/NexusScholar.Avalonia.Blocks.SampleHost`: local visual inspection host for those samples.
- `samples/NexusScholar.Desktop.Preview`: Avalonia preview over local Research Workspace outputs, with UI-02A verify/analyze actions only.
- `src/NexusScholar.Desktop.AppServices`: deterministic preview/confirmation and
  stale-safe command facade for admitted desktop operations and FE-02
  deduplication review.
- `src/NexusScholar.Desktop`: Windows-first local product host for FE-08 slices 1
  and 2.
- `tests/NexusScholar.UiContracts.Tests`, `tests/NexusScholar.Avalonia.Blocks.Tests`,
  `tests/NexusScholar.Avalonia.Blocks.SampleHost.Tests`,
  `tests/NexusScholar.ResearchWorkspace.Tests`,
  `tests/NexusScholar.Desktop.Preview.Tests`,
  `tests/NexusScholar.Desktop.AppServices.Tests`, and
  `tests/NexusScholar.Desktop.Tests`: serialization, view-model, architecture,
  loader, read-model, command-facade, and product-host coverage.

## Boundary

Core projects must not reference `NexusScholar.UiContracts`, Avalonia, renderer packages, app services, persistence, or AI/model clients.

`NexusScholar.UiContracts` remains UI-framework-free. The product host reaches
workspace behavior only through `NexusScholar.Desktop.AppServices`; it must not
reference ResearchWorkspace or Core domain projects directly. No desktop path may
execute scientific decisions, call providers, or store UI state as authority.

Run the product host locally with:

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe run --project src/NexusScholar.Desktop/NexusScholar.Desktop.csproj -c Release
```

Any future UI work that affects scientific authority, record schemas, digest material, provenance, AI acceptance, app persistence, or PHP compatibility requires the normal ADR, fixture, and verification path.
