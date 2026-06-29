# Main Public-Readiness Review - 2026-06-29

Status: upgraded review after the first sweep was found to be too branch-directed.

Baseline reviewed: `origin/main` at `16cabc3` (`Merge pull request #4 from nexus-scholar/cdx/ui-phase-3-5-avalonia-sample-host`).

Public site branch reviewed: `origin/gh-pages` at `53d7aa4` (`docs: expand architecture guide`).

Clean review worktrees:

- `C:/tmp/core-csharp-main-review`
- `C:/tmp/core-csharp-gh-pages-review`

## Reports

- `01-main-baseline-audit.md` - what is actually present on `origin/main`, with risks.
- `02-branch-cleanup-plan.md` - what can be deleted, what should not be merged blindly, and what needs rebasing.
- `03-public-readiness-and-feedback-plan.md` - what is ready to show publicly and what blocks first testers.
- `04-continuation-roadmap.md` - practical next path from kernel to first useful tester loop.

## Corrected Verdict

The project is stronger than the first sweep implied. The current `origin/main` is not only UI docs. It contains a substantial audit-grade Core foundation:

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
- UiContracts;
- Avalonia block renderer;
- Avalonia sample host;
- Full Text contract and reconnaissance docs.

But it is not yet a researcher-usable product. The current public posture should be:

> Nexus Scholar Core is a verified research workflow kernel and public architecture foundation, with a sample UI-rendering harness. It is ready for architecture feedback, developer critique, and early design/tester conversations, not for real review execution by non-developers.

## Validation

```text
dotnet build NexusScholar.Core.slnx -c Release /nr:false /p:UseSharedCompilation=false
Result: passed, 0 warnings, 0 errors

dotnet test NexusScholar.Core.slnx -c Release --no-build
Result: passed, 297 tests

dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
Result: passed
```

Static `gh-pages` internal link check: passed.
