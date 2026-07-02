# First Public Feedback Plan - 2026-06-29

Status: accepted historical task trace for the first public-readiness push.

Current outcome after PR08:

- PF-01, PF-02, CLI-01, CLI-02, CLI-03, WEB-01, UI presentation, APP-01, PR02-PR08 CLI workflow, and PR07 public CLI walkthrough work are complete on `main`/`gh-pages`.
- `main` is now at `0d93bd9` (`polish(cli): improve workspace status and exit-code consistency`).
- `gh-pages` is now at `589fc2e` (`docs(site): add research workspace CLI walkthrough`).
- This file is now a historical task trace. Current active routing lives in `docs/ops/BRANCH-BOARD.md`, `docs/ops/MERGE-QUEUE.md`, `CODEX-START-HERE.md`, and `PLANS.md`.

Historical baseline at plan acceptance:

- `main` at `7cd63ae`
- `gh-pages` at `53d7aa4`
- remote branches: `main`, `gh-pages`
- latest watched hosted `gate-01` green on `main`: run `28381357879` for `e79f5cd`
- planning commit `7cd63ae` was pushed before `cdx/public-feedback-cli-onboarding` was created

## Decision

Prioritize **CLI plus public onboarding** this week.

Use UI only as presentation support.

Defer online scholarly providers to a later provider/network/legal planning gate.

## Argument

Online providers are high public value, but they are not the next bottleneck. They introduce network behavior, credentials, rate limits, provider terms, legal/access handling, retry semantics, and live-data reproducibility questions. Current ADRs explicitly keep live providers, HTTP downloads, scraping, paywall bypass, and provider SDKs out of scope.

The current bottleneck is distribution: a public visitor still needs a short path that answers:

- what can I run;
- what will I see;
- what feedback is wanted;
- what is explicitly not implemented.

The CLI is the best first bridge because it can expose deterministic local evidence using existing Core behavior without adding persistence, cloud, live providers, or UI mutation. The UI sample host should show the same story visually after the CLI path is clear.

## Goal

Create a first public feedback loop where a tester can clone the repo, run verified local commands, inspect one evidence-preserving workflow, and file structured feedback.

The target public claim after this plan:

> Nexus Scholar Core is a verified local research workflow kernel. You can run a deterministic local demo, inspect evidence-preserving import/dedup blocks, and give feedback on architecture, workflow clarity, and onboarding.

## Source Of Truth

- `AGENTS.md`
- `README.md`
- `docs/reviews/2026-06-29-main-public-readiness/`
- `docs/ops/BRANCH-BOARD.md`
- `docs/ops/MERGE-QUEUE.md`
- `docs/adr/0011-search-import-source-contract.md`
- `docs/adr/0014-fulltext-acquisition-artifact-and-extraction-contract.md`
- `docs/ui/ROADMAP.md`
- current C# implementation and tests

## Non-Claims To Preserve

All tasks must preserve these non-claims:

- no live scholarly provider calls;
- no HTTP download behavior;
- no provider SDKs or credentials;
- no Scopus API, Web of Science API, Google Scholar scraping, or publisher scraping;
- no paywall bypass or shadow-library source;
- no persistence, API, cloud sync, or production desktop shell;
- no PDF extraction or OCR;
- no PHP compatibility claim without generated fixtures and comparators;
- no AI authority over scientific decisions;
- no Core dependency on UI frameworks.

## This Week Work Order

1. Public feedback scaffolding.
2. CLI local deterministic demo.
3. Public getting-started tutorial.
4. UI sample-host presentation support.
5. Provider planning note only.

Do not start provider implementation this week.

## Task Packet PF-00: Create Fresh Work Branch

Owner: repo operations.

Purpose: keep `main` clean while the first public feedback work is built.

Historical suggested branch:

```powershell
# historical branch used for the completed public-feedback packet
git switch main
git pull origin main
git switch -c cdx/public-feedback-cli-onboarding
```

Allowed paths:

- no file edits required

Exit criteria:

- branch starts from current `origin/main`;
- `git status --short --branch` is clean before task work begins.

Validation:

```powershell
git status --short --branch
git log --oneline --decorate --max-count=3
```

## Task Packet PF-01: Refresh Maintainer Routing Docs

Owner: documentation.

Purpose: prevent future work from restarting at Gate 0 or reintroducing branch sprawl.

Allowed paths:

- `CODEX-START-HERE.md`
- `PLANS.md`
- `docs/ops/BRANCH-BOARD.md`
- `docs/ops/MERGE-QUEUE.md`
- `docs/ops/CHAT-ROSTER.md`

Inputs:

- `README.md`
- `docs/reviews/2026-06-29-main-public-readiness/`
- `docs/ops/FIRST-PUBLIC-FEEDBACK-PLAN-2026-06-29.md`

Steps:

1. Replace `CODEX-START-HERE.md` gate-zero wording with current maintainer routing.
2. Make `PLANS.md` separate historical gates from the current roadmap.
3. Add this plan as the current recommended next work in `docs/ops/MERGE-QUEUE.md`.
4. Keep branch-board wording exact: current remote heads are `main` and `gh-pages`.
5. Do not change ADRs, specs, fixtures, source code, or tests.

Acceptance criteria:

- `CODEX-START-HERE.md` no longer says the next action is Gate 0 discovery.
- `PLANS.md` still preserves the historical gate sequence but points to the current public feedback plan.
- `docs/ops/*` name this plan as the current active work.

Validation:

```powershell
rg -n "Gate 0 discovery|current recommended next work|FIRST-PUBLIC-FEEDBACK" CODEX-START-HERE.md PLANS.md docs/ops
git diff --check
```

## Task Packet PF-02: Add Feedback Templates

Owner: public feedback.

Purpose: make early feedback structured enough to act on.

Allowed paths:

- `.github/ISSUE_TEMPLATE/first-tester-run.yml`
- `.github/ISSUE_TEMPLATE/architecture-boundary-review.yml`
- `.github/ISSUE_TEMPLATE/research-workflow-use-case.yml`
- `.github/ISSUE_TEMPLATE/documentation-confusion.yml`
- `.github/ISSUE_TEMPLATE/bug-report.yml`
- `.github/PULL_REQUEST_TEMPLATE.md`
- `docs/ops/FIRST-PUBLIC-FEEDBACK-PLAN-2026-06-29.md` only if the task discovers a template gap

Steps:

1. Create issue templates with short required fields.
2. Keep templates focused on public feedback, not support promises.
3. Add a PR template that asks for behavior changed, authority source, tests run, non-claims preserved, and docs/ADR/fixture impact.
4. Do not add GitHub labels through the API in this task. If label suggestions are needed, document them in the plan or template comments.

Acceptance criteria:

- Each issue template can be opened by GitHub.
- Each template asks for reproducible commands or concrete examples where applicable.
- PR template explicitly asks whether provider/network/persistence/API/UI/cloud claims changed.

Validation:

```powershell
git diff --check
rg -n "provider|network|non-claims|tests run|expected|actual" .github
```

## Task Packet CLI-01: Define The CLI Demo Contract

Owner: CLI.

Purpose: define a deterministic local `demo` command before code changes.

Allowed paths:

- `docs/ops/FIRST-PUBLIC-FEEDBACK-PLAN-2026-06-29.md`
- optional new file: `docs/cli/LOCAL-DEMO-CONTRACT-v0.md`

Forbidden paths:

- `src/**`
- `tests/**`
- `fixtures/**`

Steps:

1. Inspect current `src/NexusScholar.Cli/Program.cs`.
2. Inspect current Search Import and Deduplication APIs.
3. Define the demo input shape and expected output.
4. Define exact non-claims: no file persistence, no network, no provider calls, no live search.
5. Decide whether the demo prints only to stdout or also writes an optional output file. Prefer stdout only for first slice.

Recommended demo behavior:

- command name: `demo`
- local deterministic input: embedded or sample user-supplied export data;
- behavior: parse or project imported evidence, run local deduplication, print summary;
- output should include:
  - source/export digest or sample id;
  - imported record count;
  - parser warning count;
  - dedup cluster count;
  - review-required candidate count;
  - explicit "no live providers were called" line.

Acceptance criteria:

- A smaller model can implement the command without inventing product behavior.
- The contract lists exact expected stdout lines or stable substrings.
- The contract identifies which tests must be added.

Validation:

```powershell
git diff --check
```

## Task Packet CLI-02: Implement CLI Demo Command

Owner: CLI.

Dependencies:

- CLI-01 complete.

Allowed paths:

- `src/NexusScholar.Cli/**`
- `tests/NexusScholar.Cli.Tests/**` if a new CLI test project is needed
- `NexusScholar.Core.slnx` if a new test project is added
- existing test project files only when required for references

Forbidden paths:

- `src/NexusScholar.Kernel/**` unless a compile error proves a missing public helper is required
- `src/NexusScholar.Search/**`
- `src/NexusScholar.Deduplication/**`
- `src/NexusScholar.Screening/**`
- `src/NexusScholar.FullText/**`
- `docs/adr/**`
- `fixtures/**`

Steps:

1. Add `demo` to the CLI command switch.
2. Keep demo behavior deterministic and local-only.
3. Prefer extracting testable code from `Program.cs` into a small CLI-local class.
4. Add tests that assert stable output substrings and no network/provider wording.
5. Do not add production dependencies.
6. Do not make the CLI mutate Core records beyond in-memory demo construction.

Acceptance criteria:

- `dotnet run --project src/NexusScholar.Cli -- demo` exits 0.
- Output is deterministic across repeated runs.
- Output states that the demo is local/no-network.
- Output gives a concrete import/dedup style summary.
- Tests cover unknown command behavior and demo output.

Validation:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
dotnet run --project src/NexusScholar.Cli -- doctor
dotnet run --project src/NexusScholar.Cli -- sample
dotnet run --project src/NexusScholar.Cli -- demo
```

## Task Packet CLI-03: Record Expected CLI Output

Owner: documentation.

Dependencies:

- CLI-02 complete.

Allowed paths:

- `README.md`
- `docs/cli/LOCAL-DEMO-CONTRACT-v0.md`
- `docs/reviews/2026-06-29-main-public-readiness/03-public-readiness-and-feedback-plan.md`

Steps:

1. Add the `demo` command to README quick start.
2. Add a short expected-output block or stable output description.
3. Keep output docs concise and do not overclaim product readiness.

Acceptance criteria:

- README shows `doctor`, `sample`, and `demo`.
- README says the demo is local-only and non-authoritative.

Validation:

```powershell
dotnet run --project src/NexusScholar.Cli -- demo
git diff --check
```

## Task Packet WEB-01: Replace Public Getting-Started Placeholder

Owner: public site.

Purpose: give first testers a runnable 10-15 minute path.

Branch:

- use `gh-pages`, not `main`.
- recommended worktree:

```powershell
git worktree add C:\tmp\core-csharp-gh-pages-first-tester gh-pages
```

Allowed paths on `gh-pages`:

- `tutorials/getting-started/index.html`
- related CSS/assets only if the existing page requires them
- optional public roadmap or feedback page if already linked by the site

Steps:

1. Historical WEB-01 action, now complete: replace the old placeholder tutorial with exact commands:
   - clone;
   - verify;
   - `doctor`;
   - `sample`;
   - `demo` if CLI-02 is merged first;
   - sample host.
2. Include "what this proves" and "what this does not prove".
3. Include feedback links to issue templates after PF-02 is merged.
4. Keep public claims aligned with README non-claims.

Acceptance criteria:

- Tutorial is no longer a placeholder.
- A tester can follow it without reading the full repo first.
- It clearly says there are no live providers, no PDF extraction/OCR, no production app, and no cloud behavior.

Validation:

```powershell
git diff --check
```

If the site has a local static link checker, run it. If not, manually inspect internal links touched by this task.

## Task Packet UI-01: Add Sample Host Visual Asset

Owner: UI presentation.

Purpose: support public understanding without turning the sample host into a product shell.

Dependencies:

- CLI-02 preferred but not required.
- WEB-01 can link the asset once available.

Allowed paths:

- `docs/ui/**`
- `docs/reviews/2026-06-29-main-public-readiness/**`
- `gh-pages` image/static asset path if working on public site branch

Steps:

1. Run the sample host locally.
2. Capture one screenshot or short GIF that shows the sample selector and rendered block plan.
3. Add a caption that says this is a sample-only visual inspection host.
4. Do not add Core calls, persistence, app services, or real command execution to the sample host.

Acceptance criteria:

- Visual asset exists and is referenced by the public tutorial or public site.
- Caption preserves non-authority boundary.
- No UI code changes are required for this task.

Validation:

```powershell
dotnet run --project samples/NexusScholar.Avalonia.Blocks.SampleHost
git diff --check
```

Manual visual verification is acceptable for this task because the host is a local desktop window.

## Task Packet APP-01: Plan Read-Only AppServices Composition

Owner: application boundary.

Purpose: prepare the first product slice without letting UI mutate Core directly.

Allowed paths:

- new ADR draft under `docs/adr/` only if explicitly accepted as a draft naming convention;
- otherwise new planning doc under `docs/ui/` or `docs/ops/`;
- no source code in this task.

Recommended path:

- `docs/adr/0015-app-services-readonly-workspace-composition.md` as Draft, if ADR numbering is accepted.

Steps:

1. Define read-only composition from existing Core records into `WorkspacePlan`.
2. Define which Core modules may be read for the first slice: Search Import and Deduplication first.
3. Define what AppServices must not do:
   - no persistence;
   - no command execution;
   - no Core mutation;
   - no UI framework dependency;
   - no AI/model calls;
   - no provider/network calls.
4. Define required architecture tests before implementation.
5. Define the first useful workflow block: import warning plus dedup review.

Acceptance criteria:

- Plan explains how Core remains UI-free.
- Plan gives a narrow implementation path for a future branch.
- No code is added.

Validation:

```powershell
git diff --check
```

## Task Packet PROV-01: Provider Value Planning Only

Owner: provider/network planning.

Purpose: capture why online providers matter without implementing them this week.

Allowed paths:

- `docs/ops/FIRST-PUBLIC-FEEDBACK-PLAN-2026-06-29.md`
- optional new planning doc under `docs/port/` or `docs/adr/` as Draft

Forbidden paths:

- `src/**`
- `tests/**`
- package dependency files

Steps:

1. List provider families that would be valuable later:
   - OpenAlex metadata/search;
   - Crossref metadata;
   - Semantic Scholar metadata;
   - PubMed/Europe PMC where legally and technically appropriate;
   - user-supplied Scopus/Web of Science exports, not APIs, until a later gate.
2. For each, classify:
   - metadata search;
   - full-text source reference;
   - live download;
   - user-supplied export.
3. Identify ADR questions:
   - credentials;
   - rate limits;
   - caching and reproducibility;
   - provider terms;
   - audit evidence;
   - failure model;
   - no Google Scholar scraping.
4. Do not implement providers.

Acceptance criteria:

- Provider plan exists only as future planning.
- It reinforces that this week is CLI/onboarding first.
- It does not authorize live calls.

Validation:

```powershell
git diff --check
rg -n "no live|provider|Google Scholar|credentials|rate limits" <provider-planning-file>
```

## Task Packet REL-01: Release Review And Merge

Owner: release.

Dependencies:

- PF-01
- PF-02
- CLI-01
- CLI-02
- CLI-03
- optional WEB-01 and UI-01 if done on separate `gh-pages` branch

Steps:

1. Run local verification:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

2. Run CLI smoke commands:

```powershell
dotnet run --project src/NexusScholar.Cli -- doctor
dotnet run --project src/NexusScholar.Cli -- sample
dotnet run --project src/NexusScholar.Cli -- demo
```

3. Review changed files for non-claim drift:

```powershell
rg -n "live provider|download|scrape|production|cloud|PDF extraction|OCR|PHP compatibility" README.md docs src tests
```

4. Commit with a narrow message.
5. Push branch and open PR, or merge to `main` only after review.
6. Confirm hosted CI.

Acceptance criteria:

- local build/test/format pass;
- CLI commands pass;
- hosted CI passes;
- docs explain first-tester path;
- issue templates exist;
- no provider/network/persistence/API/UI/cloud/PHP compatibility claims were added.

## Today Checklist

Today should finish:

- PF-01 maintainer routing docs;
- PF-02 feedback templates;
- CLI-01 demo contract;
- no provider implementation.

Stretch today:

- start CLI-02 if the demo contract is clear.

## This Week Checklist

This week should finish:

- CLI-02 implementation;
- CLI-03 README output docs;
- WEB-01 public getting-started tutorial;
- UI-01 sample host screenshot/GIF;
- APP-01 AppServices planning doc;
- PROV-01 provider future-value planning note;
- REL-01 verification and merge.

## Definition Of Done For The Week

A first tester can:

1. open the repo and understand the current status from README;
2. run `scripts/verify`;
3. run CLI `doctor`, `sample`, and `demo`;
4. open the public getting-started tutorial;
5. view a sample host screenshot/GIF;
6. file structured feedback through an issue template.

The project still explicitly does not claim:

- production use;
- live providers;
- PDF extraction/OCR;
- persistence/API/cloud;
- PHP compatibility.
