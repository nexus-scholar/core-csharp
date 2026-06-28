# Merge Queue

Source: live status after Gate 9 Full Text reconnaissance was applied to `main`.

## Completed Merges

- `cdx/run-gate-zero-discovery` (merged to `main`)
- `cdx/gate-2-digest-kernel-cleanup` (merged to `main`)
- `cdx/gate-3-planning-decisions` (merged to `main`)
- `cdx/gate-3-protocol-lifecycle` (merged to `main`)
- `cdx/gate-4-workflow-planning` (merged to `main`)
- `cdx/gate-4-workflow` (merged to `main`)
- `cdx/gate-5-provenance` (merged to `main`)
- `cdx/gate-6-bundle-planning` (merged to `main`)
- `cdx/gate-9-shared-identity` (merged to `main`)
- `cdx/gate-9-search-recon` (merged to `main`)
- `cdx/app-recon-cli-web-core-usage` (merged to `main`)
- `cdx/gate-9-search-contract` (merged to `main`)
- `cdx/gate-9-search-local` (merged to `main`)
- `cdx/gate-9-search-import-contract` (merged to `main`)
- `cdx/gate-9-search-import-local` (merged to `main`)
- `cdx/gate-9-dedup-recon` (merged to `main`)
- `cdx/gate-9-dedup-contract` (merged to `main`)
- `cdx/gate-9-dedup-local` (merged to `main`)
- `cdx/gate-9-screening-recon` (merged to `main`)
- `cdx/gate-9-screening-contract` (merged to `main`)
- `cdx/gate-9-screening-local` (merged to `main`; remote branch deleted after cleanup)
- `cdx/ui-contract-block-plan-samples` (merged to `main`; remote branch deleted after cleanup)
- `cdx/gate-9-fulltext-recon` content applied to `main` by patch-equivalent cherry-pick because `origin/main` advanced before fast-forward merge.

## Current Queue

- `main` head: `37a2881` (`docs: map PHP full text behavior`).
- Full Text recon branch CI is green: https://github.com/nexus-scholar/core-csharp/actions/runs/28318711028
- Final push-triggered `main` CI is green: https://github.com/nexus-scholar/core-csharp/actions/runs/28318771878
- GitHub remote branch cleanup candidates by ancestry: none.
- Patch-equivalent stale branch: `origin/cdx/gate-9-fulltext-recon`.
- Next primary branch: `cdx/gate-9-fulltext-contract`.
- Full Text work should continue with ADR 0014 before implementation.

## Not Queued Yet

- C# Full Text implementation
- full-text artifact storage
- live provider/network calls
- Unpaywall, PMC, Europe PMC, arXiv, OpenAlex, Semantic Scholar, or publisher adapters
- Scopus API
- Web of Science API
- Google Scholar scraping
- paywall bypass or shadow-library sources
- PHP compatibility claims
- generated PHP fixtures
- persistence/API/UI/cloud behavior
- CLI/Web app alignment
- AI governance

## Cleanup Candidates

- none on GitHub by ancestry.

## Not Safe To Delete

- `main`
- `origin/cdx/gate-9-fulltext-recon` until patch-equivalent cleanup is explicitly accepted.

## Verification

- `git branch -r --merged main` returns only `origin/main`.
- `git branch -r` returns `origin/main` and `origin/cdx/gate-9-fulltext-recon`.
- `cdx/gate-9-fulltext-recon` commit `85d2e17` and `main` commit `37a2881` have the same stable patch id for the Full Text recon changes.
