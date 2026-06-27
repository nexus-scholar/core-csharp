# Merge Queue

Source: live status from branch probes after Gate 9 Search reconnaissance merge.

## Completed Merges

- `cdx/run-gate-zero-discovery` (merged to `main`)
- `cdx/gate-2-digest-kernel-cleanup` (merged to `main`)
- `cdx/gate-3-planning-decisions` (merged to `main`)
- `cdx/gate-3-protocol-lifecycle` (merged to `main`)
- `cdx/gate-4-workflow-planning` (merged to `main`)
- `cdx/gate-4-workflow` (merged to `main`)
- `cdx/gate-5-provenance` (merged to `main`)
- `cdx/gate-9-shared-identity` (merged to `main`)
- `cdx/gate-6-bundle-planning` (merged to `main`)
- `cdx/gate-9-search-recon` (merged to `main`)
- `cdx/two-model-codex-workflow` (historical merged workflow setup branch)
- `cdx/shared-identity-adr-0007` (reconnaissance planning branch)

## Current Queue

- `main` includes Gate 9 Search reconnaissance at `3688ca16bc03f1fe5f86096e810ffebf97d0f2dd`.
- Gate 9 Search recon branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28285488732`.
- Gate 9 Search recon push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28285547851`.
- Next primary branch: `cdx/gate-9-search-contract`.
- ADR 0010 Search Trace and Plan Contract is queued before any C# Search implementation.

## Not Queued Yet

- C# Search implementation
- provider/network calls
- PHP compatibility claims
- generated PHP fixtures
- Deduplication implementation
- Screening behavior
- Search persistence/API/UI/cloud behavior

## Cleanup Candidates

- `cdx/gate-9-search-recon`
- `cdx/gate-6-bundle-planning`
- `cdx/gate-9-shared-identity`
- `cdx/gate-5-provenance`
- `cdx/two-model-codex-workflow`
- `cdx/main-gate2-merge`
- `cdx/gate-4-workflow`
- `cdx/gate-4-workflow-planning`
- `cdx/gate-3-protocol-lifecycle`
- `cdx/gate-3-planning-decisions`
- `cdx/gate-2-digest-kernel-cleanup`
- `cdx/run-gate-zero-discovery`

## Not Safe To Delete

- `main`
- `cdx/run-gate-0-discovery`
- `cdx/app-recon-cli-web-core-usage` until its owner/purpose is classified

## Verification

- `git branch --merged main` currently confirms the cleanup candidates above.
- `git branch --all --no-merged main` returned no unmerged local/remote gate branches at the time of this refresh.
