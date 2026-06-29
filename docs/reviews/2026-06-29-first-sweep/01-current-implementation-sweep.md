# Current Implementation Sweep

Status: read-only implementation sweep, with report files added afterward.

## Verdict

The current implementation is acceptable for its stated narrow scope:

- `NexusScholar.UiContracts` is a platform-neutral workspace/block contract layer.
- `NexusScholar.Avalonia.Blocks` is a renderer-only library.
- `samples/NexusScholar.Avalonia.Blocks.SampleHost` is a visual inspection harness.
- The sample host loads sample `WorkspacePlan` JSON and renders it.
- No Core domain project dependency, persistence dependency, live provider dependency, AI SDK dependency, or Core mutation path was found in the renderer/host slice.

Safe label: renderer/sample-host prototype.

Unsafe labels: product desktop app, app services, persistence, Core command execution, scientific decision engine.

## Evidence Checked

- Solution includes `NexusScholar.UiContracts`, `NexusScholar.Avalonia.Blocks`, the sample host, and matching tests in `NexusScholar.Core.slnx`.
- Avalonia package references are limited to the renderer and sample host project surface. Central package versions live in `Directory.Packages.props`.
- Architecture tests assert:
  - Core domain projects do not reference `NexusScholar.UiContracts` (`tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs:248`).
  - `NexusScholar.Avalonia.Blocks` references UiContracts without Core domain projects (`tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs:278`).
  - Sample host references renderer and UiContracts without Core domain projects (`tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs:311`).
  - Repository package policy only permits Avalonia on the renderer and host surfaces (`tests/NexusScholar.Architecture.Tests/RepositoryPolicyTests.cs:45`).
- UiContracts tests assert no Avalonia, mobile, web, terminal, or network dependency (`tests/NexusScholar.UiContracts.Tests/UiContractTests.cs:216`).
- Sample plan tests assert object-root payload JSON and renderer-neutral sample JSON (`tests/NexusScholar.UiContracts.Tests/SampleBlockPlanTests.cs:99`, `tests/NexusScholar.UiContracts.Tests/SampleBlockPlanTests.cs:116`).
- Sample host loads exactly the three expected sample files (`samples/NexusScholar.Avalonia.Blocks.SampleHost/SampleWorkspaceLoader.cs:10`).

## Renderer And Host Behavior

The renderer converts `WorkspacePlan` into view models and marks sample/non-authoritative input through authority-status detection (`src/NexusScholar.Avalonia.Blocks/WorkspacePlanViewModels.cs:56`, `src/NexusScholar.Avalonia.Blocks/WorkspacePlanViewModels.cs:61`).

Actions are UI callbacks, not Core commands. `BlockActionViewModel.Invoke()` only invokes a supplied callback with an invocation object (`src/NexusScholar.Avalonia.Blocks/WorkspacePlanViewModels.cs:198`). The host status text explicitly says no Core command was called (`samples/NexusScholar.Avalonia.Blocks.SampleHost/MainWindow.cs:95`).

The host also initializes with an explicit no-Core/no-persistence/no-AI status message (`samples/NexusScholar.Avalonia.Blocks.SampleHost/MainWindow.cs:72`).

## Findings

### Blocking

None found for the current stated scope.

### Important

None found for the current stated scope.

### Minor / Follow-Up

1. `docs/ui/README.md` has stale Phase 0 wording. It still says `OPEN-QUESTIONS.md` should be resolved before contract implementation and that creating planning documents does not add Avalonia (`docs/ui/README.md:21`, `docs/ui/README.md:25`). That was true when the docs were only planning material, but this branch now includes implemented UiContracts, Avalonia renderer, and sample host. The more specific Phase 3/3.5 docs are accurate, so this is documentation drift rather than an implementation problem.

2. Manual visual inspection was not performed in this sweep. Build/tests/format passed, and the host is present, but the actual Avalonia window was not launched and visually checked. That should happen before deciding on visual polish.

3. Renderer actions display and propagate `RequiresHumanConfirmation`, but the renderer does not enforce confirmation. This is acceptable while callbacks are placeholders. A future app-service boundary must enforce human confirmation before any real command executes.

## Verification

```text
dotnet build NexusScholar.Core.slnx -c Release /nr:false /p:UseSharedCompilation=false
Passed: 0 warnings, 0 errors

dotnet test NexusScholar.Core.slnx -c Release --no-build
Passed: 297 tests

dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
Passed
```
