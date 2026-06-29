# Merge Queue

Source: live status after main consolidation, remote branch cleanup, first public-feedback planning, and branch creation.

## Current Queue

Active docs/planning branch: `cdx/public-feedback-cli-onboarding`.

Current baseline:

- `main` head: `7cd63ae` (`docs: plan first public feedback loop`).
- `origin/main`: `7cd63ae`.
- Hosted `main` CI: `gate-01` run `28381357879`, passed on Ubuntu and Windows for the review-refresh baseline. The first public-feedback plan commit was then pushed to `main`.
- Remote branches: `main`, `gh-pages`.
- Local branches include `main`, `gh-pages`, and active branch `cdx/public-feedback-cli-onboarding`.

## Completed Consolidation

- `origin/main` was advanced from `16cabc3` to `ebb7bba`.
- Full Text implementation was ported from old local commit `a520616` into `main` as `5a13abc`.
- Review and README refresh landed as `ebb7bba`.
- Review/ops state refresh landed as `e79f5cd`.
- First public-feedback plan landed as `7cd63ae`.
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

## Active Work

Current branch:

- `cdx/public-feedback-cli-onboarding`

Current packets:

1. PF-01 maintainer routing docs.
2. PF-02 issue/PR feedback templates.
3. CLI-01 local deterministic demo contract.

Stop after CLI-01 review. Do not implement CLI demo code yet.

Current detailed plan:

- `docs/ops/FIRST-PUBLIC-FEEDBACK-PLAN-2026-06-29.md`

## Verification

Local consolidation verification passed:

- `dotnet build NexusScholar.Core.slnx -c Release`
- `dotnet test NexusScholar.Core.slnx -c Release --no-build` with 318 tests
- `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`
- `powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1`

Hosted verification passed:

- https://github.com/nexus-scholar/core-csharp/actions/runs/28380516236
