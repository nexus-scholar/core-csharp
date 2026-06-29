# Merge Queue

Source: live status after main consolidation, remote push, remote branch cleanup, and hosted CI.

## Current Queue

No implementation branch is queued.

Current baseline:

- `main` head: `ebb7bba` (`docs: refresh readmes after main consolidation`).
- `origin/main`: `ebb7bba`.
- Hosted `main` CI: `gate-01` run `28380516236`, passed on Ubuntu and Windows.
- Remote branches: `main`, `gh-pages`.
- Local branches: `main`, `gh-pages`.

## Completed Consolidation

- `origin/main` was advanced from `16cabc3` to `ebb7bba`.
- Full Text implementation was ported from old local commit `a520616` into `main` as `5a13abc`.
- Review and README refresh landed as `ebb7bba`.
- Remote `cdx/*` branches were deleted.
- Local `cdx/*` branches and clean obsolete worktrees were deleted.

## Completed Merges And Ports

- Gate 0 through Gate 6 local foundations.
- Gate 9 Shared Identity.
- Gate 9 Search reconnaissance, contract, and local Search.
- Gate 9 Search Import contract and local import.
- Gate 9 Deduplication reconnaissance, contract, and local Deduplication.
- Gate 9 Screening reconnaissance, contract, and local Screening.
- Gate 9 Full Text reconnaissance and ADR 0014 contract.
- Gate 9 local no-network Full Text implementation.
- UI contract/block-plan samples.
- Avalonia block renderer prototype.
- Avalonia sample host.

## Not Queued Yet

- live provider/network calls
- Unpaywall, PMC, Europe PMC, arXiv, OpenAlex, Semantic Scholar, or publisher adapters
- Scopus API
- Web of Science API
- Google Scholar scraping
- paywall bypass or shadow-library sources
- actual PDF text extraction
- OCR
- full-text artifact storage
- PHP compatibility claims
- generated PHP fixtures
- persistence/API/UI/cloud behavior
- CLI/Web app alignment
- AI governance beyond current proposal contracts

## Recommended Next Branch

Create a fresh branch from `main` for one of:

1. public onboarding and first-tester walkthrough;
2. issue/PR feedback templates;
3. maintainer routing docs refresh;
4. AppServices/read-only block composition planning.

## Verification

Local consolidation verification passed:

- `dotnet build NexusScholar.Core.slnx -c Release`
- `dotnet test NexusScholar.Core.slnx -c Release --no-build` with 318 tests
- `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`
- `powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1`

Hosted verification passed:

- https://github.com/nexus-scholar/core-csharp/actions/runs/28380516236
