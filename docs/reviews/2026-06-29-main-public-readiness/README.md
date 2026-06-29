# Main Public-Readiness Review - 2026-06-29

Status: refreshed after local consolidation, remote push, remote branch cleanup, and hosted CI.

Current repository baseline: `origin/main` at `ebb7bba` (`docs: refresh readmes after main consolidation`).

Public site branch: `origin/gh-pages` at `53d7aa4` (`docs: expand architecture guide`).

Remote branch state after cleanup:

- `origin/main`
- `origin/gh-pages`

The older first-sweep package remains historical context. This folder is the current review package.

## Reports

- `01-main-baseline-audit.md` - current `origin/main` baseline, strengths, findings, and validation.
- `02-branch-cleanup-plan.md` - completed branch cleanup record and remaining branch state.
- `03-public-readiness-and-feedback-plan.md` - what is ready to show publicly and what still blocks first testers.
- `04-continuation-roadmap.md` - practical next path after consolidation.
- `05-state-refresh-after-remote-cleanup.md` - exact post-push state snapshot.

## Current Verdict

Nexus Scholar Core is now in a cleaner public baseline than the earlier review found. `main` contains the UI renderer/sample-host work and the local no-network Full Text implementation. Remote branch noise has been removed.

The project is still not a researcher-usable systematic-review product. The honest public posture is:

> Nexus Scholar Core is a verified research workflow kernel and public architecture foundation, with a sample UI-rendering harness and local no-network Full Text evidence slice. It is ready for architecture feedback, developer critique, and carefully framed first-tester conversations, not for real review execution by non-developers.

## Current Implementation Surface

- deterministic kernel;
- protocol lifecycle;
- workflow compiler;
- artifact identity;
- provenance ledger;
- bundle verifier;
- shared scholarly identity;
- Search trace and local search-import behavior;
- Deduplication;
- Screening;
- local no-network Full Text;
- UiContracts;
- Avalonia block renderer;
- Avalonia sample host;
- CLI doctor/sample commands.

## Validation

Local consolidation verification:

```text
dotnet build NexusScholar.Core.slnx -c Release
Result: passed, 0 warnings, 0 errors

dotnet test NexusScholar.Core.slnx -c Release --no-build
Result: passed, 318 tests

dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
Result: passed

powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
Result: passed, 318 tests
```

Hosted verification after push:

```text
gate-01 run 28380516236
Commit: ebb7bba
Result: passed on ubuntu-latest and windows-latest
URL: https://github.com/nexus-scholar/core-csharp/actions/runs/28380516236
```

Static `gh-pages` internal link check from the earlier review remains passed, but the public tutorial is still a placeholder and should be refreshed before inviting broad testers.
