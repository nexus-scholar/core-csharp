# Main Baseline Audit

Baseline: `origin/main` at `ebb7bba`.

## Bottom Line

`origin/main` is now a coherent technical foundation with the main implementation and branch cleanup consolidated. It is not just UI planning and it is not merely a scaffold. It contains real C# modules, conformance fixtures, architecture guards, a local no-network Full Text slice, UI contracts, a renderer prototype, and a sample visual host.

The remaining weakness is public product readiness: the repo can be reviewed by developers and methodologists, but it still lacks a complete first-tester walkthrough, issue templates, screenshots/GIFs, and a current public getting-started page.

## Measured Current State

Measured from the current clean `main` worktree:

- 16 source projects under `src/`.
- 2 sample folders under `samples/`.
- 6 test projects.
- 144 C# files under `src/`.
- 64 C# files under `tests/`.
- 163 JSON conformance fixtures.
- 14 ADR files.
- 26 gate/evidence docs.

Implemented or scaffolded module surface:

- `NexusScholar.Kernel`
- `NexusScholar.Protocol`
- `NexusScholar.Workflow`
- `NexusScholar.Artifacts`
- `NexusScholar.Provenance`
- `NexusScholar.Shared`
- `NexusScholar.Search`
- `NexusScholar.Deduplication`
- `NexusScholar.Screening`
- `NexusScholar.FullText`
- `NexusScholar.Bundles`
- `NexusScholar.Extensibility`
- `NexusScholar.AI`
- `NexusScholar.UiContracts`
- `NexusScholar.Avalonia.Blocks`
- `NexusScholar.Cli`
- `samples/NexusScholar.Avalonia.Blocks.SampleHost`

## What Is Strong

1. The architecture boundary is unusually clear.

   Domain projects are guarded against EF Core, ASP.NET Core, Avalonia, provider SDKs, storage SDKs, AI SDKs, and live-call primitives. Avalonia is allowed only for renderer/host surfaces.

2. The scientific invariants are explicit.

   The repo preserves product laws such as suggestion-not-decision, draft-not-approved-protocol, automation-not-authority, current-state-not-history, and projection-not-canonical-evidence.

3. Search, Deduplication, Screening, and Full Text are separated correctly.

   Search preserves observations and imports. Deduplication structures duplicate evidence without deleting raw sightings. Screening requires human decision authority and blocks unresolved conflicts. Full Text records local artifact evidence by exact raw bytes plus `raw-artifact-bytes` digest and keeps extraction derived.

4. The UI direction is safe so far.

   `UiContracts` is framework-free. `Avalonia.Blocks` renders contract data without Core references. The sample host is explicitly non-authoritative.

5. Public Pages already exist.

   `gh-pages` has a homepage, narrative posts, architecture page, module pages, contributing guide, and developer docs. Internal static links passed in the earlier review.

6. Branch state is clean.

   Remote branches are now only `main` and `gh-pages`. Local branches are `main` and `gh-pages`. Old `cdx/*` branches were deleted after the useful Full Text implementation was ported.

## Remaining Public-Readiness Findings

### 1. The top-level README drift is resolved.

The README now reflects the current implementation surface, quick start, CLI smoke path, sample host, authority rules, and non-claims.

Residual risk: the README is suitable for developer review, but it still does not replace a guided public tutorial.

### 2. `CODEX-START-HERE.md` is stale.

It still tells Codex to read `AGENTS.md` and run Gate 0 discovery. That was right at project start; it is wrong for the current public baseline.

Impact: future work may restart from old discovery instead of current `main`.

Fix: route to the current maintainer path:

- read `README.md`;
- read current ADR/gate docs for the target lane;
- use `scripts/verify.ps1`;
- preserve non-claims;
- treat old gate plans as history unless the target lane needs them.

### 3. Ops docs were stale and have now been refreshed.

`docs/ops/BRANCH-BOARD.md`, `MERGE-QUEUE.md`, and `CHAT-ROSTER.md` now describe `main` at `ebb7bba`, remote branches `main`/`gh-pages`, and no active `cdx/*` branch queue.

Residual risk: these docs should be refreshed again after the next real branch/PR.

### 4. The public tutorial is still a placeholder.

`gh-pages/tutorials/getting-started/index.html` says it is a placeholder for the first complete public walkthrough.

Impact: this is the main blocker for first testers. A tester needs an exact path, not just a story.

Fix: create one verified tutorial:

1. clone repo;
2. install the .NET SDK used by the repo;
3. run build/test/format or `scripts/verify`;
4. run `dotnet run --project src/NexusScholar.Cli -- doctor`;
5. run `dotnet run --project src/NexusScholar.Cli -- sample`;
6. run the Avalonia sample host;
7. inspect `samples/block-plans/dedup-review.sample.json`;
8. report feedback through a GitHub issue template.

### 5. No GitHub issue templates exist.

Only `.github/workflows/gate-01.yml` exists. There are no public feedback issue templates.

Impact: if you ask people for feedback now, their feedback will be noisy and hard to sort.

Fix: add issue templates for:

- architecture critique;
- first-tester walkthrough result;
- research workflow use case;
- bug/report validation failure;
- documentation confusion.

## Important Product Findings

### 1. The CLI is still not a product CLI.

`src/NexusScholar.Cli` supports `doctor` and `sample`. It is useful for smoke testing, but it does not expose Search, Import, Deduplication, Screening, bundles, or Full Text workflows.

Implication: do not invite researchers to "use Nexus" yet. Invite them to review architecture, run the sample, and critique first workflows.

### 2. Full Text is implemented only as a local no-network evidence slice.

The implementation is valuable and now merged to `main`, but it deliberately excludes live providers, HTTP downloads, provider SDKs, credentials, scraping, paywall bypass, shadow-library sources, actual PDF parsing, OCR, persistence/API/UI/cloud behavior, and PHP compatibility claims.

Implication: Full Text can support architecture review and local evidence-shape testing, not live paper retrieval.

## Verification Evidence

```text
dotnet build NexusScholar.Core.slnx -c Release
Passed

dotnet test NexusScholar.Core.slnx -c Release --no-build
Passed: 318 tests

dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
Passed

powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
Passed: 318 tests

Hosted gate-01 run 28380516236
Passed: ubuntu-latest and windows-latest
```
