# Avalonia Sample Host v0

Phase 3.5 adds `samples/NexusScholar.Avalonia.Blocks.SampleHost` as a visual inspection harness for the Phase 3 renderer.

This is not a desktop product shell. It is not Core authority, not an application-service layer, not persistence, not AI execution, and not scientific mutation. Its only purpose is to load the Phase 2 sample `WorkspacePlan` JSON files and render them through `NexusScholar.Avalonia.Blocks`.

## Input Path

The only supported Phase 3.5 path is:

1. `samples/block-plans/import-warning.sample.json`;
2. `samples/block-plans/dedup-review.sample.json`;
3. `samples/block-plans/bundle-verification.sample.json`;
4. `System.Text.Json` deserialization through `NexusScholar.UiContracts`;
5. rendering through `NexusScholar.Avalonia.Blocks`;
6. placeholder action callbacks that update host status text only.

No Core state is loaded. No Core commands are called. No records are created, updated, approved, merged, screened, exported, or persisted.

## Dependency Boundary

The sample host may reference:

- `NexusScholar.UiContracts`;
- `NexusScholar.Avalonia.Blocks`;
- `Avalonia`;
- `Avalonia.Desktop`.

It must not reference Core domain projects, Search, Deduplication, Screening, app services, database packages, AI SDKs, web renderers, mobile renderers, or CLI renderers.

## Host Surface

The host provides one main window with:

- a sample selector;
- a rendered workspace area;
- status text stating that the host is sample-only;
- placeholder action feedback.

The sample/non-authoritative status remains visible through the rendered `WorkspacePlan` authority status and host copy.

## Run Command

```powershell
dotnet run --project samples/NexusScholar.Avalonia.Blocks.SampleHost
```

This command launches a local desktop window for manual inspection. It is not part of CI and should not be treated as product behavior.
