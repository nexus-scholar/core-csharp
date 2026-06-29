# Main Baseline Audit

Baseline: `origin/main` at `16cabc3`.

## Bottom Line

`origin/main` is a coherent technical foundation. It is not just UI planning. It has real C# modules, conformance fixtures, architecture guards, and a public site. The weakness is presentation and product readiness: the top-level repo docs still read like an earlier scaffold, the ops docs lag the current merge state, and there is no complete runnable first-tester path.

## What Is Actually Present

Measured from the clean `origin/main` worktree:

- 15 source/sample projects in `NexusScholar.Core.slnx`.
- 6 test projects.
- 137 C# files under `src/`.
- 62 C# files under `tests/`.
- 141 JSON conformance fixtures.
- 14 ADR files.
- 25 gate/evidence docs.

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

3. Search, Deduplication, and Screening are separated correctly.

   Search preserves observations and imports. Deduplication structures duplicate evidence without deleting raw sightings. Screening requires human decision authority and blocks unresolved conflicts.

4. The UI direction is safe so far.

   `UiContracts` is framework-free. `Avalonia.Blocks` renders contract data without Core references. The sample host is explicitly non-authoritative.

5. Public Pages already exist.

   `gh-pages` has a homepage, narrative posts, architecture page, module pages, contributing guide, and developer docs. Internal static links passed a local link check.

## Blocking Public-Readiness Findings

### 1. The top-level README undersells and misstates current scope.

`README.md` still says Search, corpus, screening, extraction, persistence, API, desktop, and web modules are added only when their gates begin. But `origin/main` already includes Search, Search import, Deduplication, Screening, UiContracts, Avalonia Blocks, and a sample host.

Impact: a first visitor gets a stale picture from the repo landing page.

Fix: rewrite `README.md` around the current evidence-backed state:

- what works now;
- what is contract-only;
- what is sample-only;
- what is explicitly not claimed;
- how to run verification;
- how to run the CLI smoke path and sample host.

### 2. `CODEX-START-HERE.md` is stale.

It tells Codex to read AGENTS and run Gate 0 discovery. That was right at project start; it is wrong for the current public baseline.

Impact: future work may restart from old discovery instead of current `main`.

Fix: make it route to a current maintainer path:

- read `README.md`;
- read `docs/ops/BRANCH-BOARD.md` after refresh;
- read current ADR/gate docs for the target lane;
- use `scripts/verify.ps1`;
- do not broaden non-claims.

### 3. Ops docs are stale after PR #4.

`docs/ops/BRANCH-BOARD.md`, `MERGE-QUEUE.md`, and `CHAT-ROSTER.md` still describe `main` at `c3ced65`, not `16cabc3`.

Impact: branch cleanup and next-lane planning can be wrong.

Fix: refresh ops docs from live branch state after the branch cleanup decision.

### 4. The public tutorial is still a placeholder.

`gh-pages/tutorials/getting-started/index.html` says it is a placeholder for the first complete public walkthrough.

Impact: this is the main blocker for first testers. A tester needs an exact path, not just a story.

Fix: create one verified tutorial:

1. clone repo;
2. install .NET SDK per `global.json`;
3. run build/test/format;
4. run `dotnet run --project src/NexusScholar.Cli -- doctor`;
5. run `dotnet run --project src/NexusScholar.Cli -- sample`;
6. run the Avalonia sample host;
7. inspect `samples/block-plans/dedup-review.sample.json`;
8. report feedback through a GitHub issue template.

## Important Findings

### 1. The CLI is not a product CLI.

`src/NexusScholar.Cli` supports `doctor` and `sample`. It is useful for smoke testing, but it does not expose Search, Import, Deduplication, Screening, bundles, or Full Text.

Implication: do not invite researchers to "use Nexus" yet. Invite them to review architecture, run the sample, and critique first workflows.

### 2. Full Text implementation exists only on an unmerged local branch.

`cdx/gate-9-fulltext-local` adds a serious implementation slice, fixtures, and tests, but it is based before current UI merges. A direct merge would delete current UI renderer/host files.

Implication: Full Text local work is likely the next valuable implementation lane, but it must be rebased or cherry-picked onto `origin/main` carefully.

### 3. No GitHub issue templates exist.

Only `.github/workflows/gate-01.yml` exists. There are no public feedback issue templates.

Implication: if you ask people for feedback now, their feedback will be noisy and hard to sort.

Fix: add issue templates for:

- architecture critique;
- first-tester walkthrough result;
- research workflow use case;
- bug/report validation failure;
- documentation confusion.

## Verification Evidence

```text
dotnet build NexusScholar.Core.slnx -c Release /nr:false /p:UseSharedCompilation=false
Passed

dotnet test NexusScholar.Core.slnx -c Release --no-build
Passed: 297 tests

dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
Passed
```
