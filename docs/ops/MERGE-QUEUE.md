# Merge Queue

Source: live status after ADR 0014 Full Text contract was merged to `main`.

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
- `cdx/gate-9-fulltext-contract` (merged to `main` at `c3ced65`)

## Current Queue

- `main` head: `c3ced65` (`docs: define full text contract`).
- Full Text contract branch CI is green: https://github.com/nexus-scholar/core-csharp/actions/runs/28320117650
- Final push-triggered `main` CI is green: https://github.com/nexus-scholar/core-csharp/actions/runs/28320176602
- GitHub remote branch cleanup candidates by ancestry: `origin/cdx/gate-9-fulltext-contract`.
- Patch-equivalent stale branch: `origin/cdx/gate-9-fulltext-recon`.
- Active unrelated branch: `origin/cdx/ui-phase-3-avalonia-renderer`.
- Next primary branch: `cdx/gate-9-fulltext-local`.
- Full Text work should continue with the local no-network implementation slice against `ADR 0014`.

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
- AI governance

## Cleanup Candidates

- `origin/cdx/gate-9-fulltext-contract` is safe by ancestry after merge.

## Not Safe To Delete

- `main`
- `origin/cdx/gate-9-fulltext-recon` until patch-equivalent cleanup is explicitly accepted.
- `origin/cdx/ui-phase-3-avalonia-renderer` because it is an active unrelated UI prototype lane.

## Verification

- `git branch -r --merged main` returns `origin/HEAD -> origin/main`, `origin/main`, and `origin/cdx/gate-9-fulltext-contract`.
- `git branch -r` returns `origin/main`, `origin/cdx/gate-9-fulltext-contract`, `origin/cdx/gate-9-fulltext-recon`, and `origin/cdx/ui-phase-3-avalonia-renderer`.
- `cdx/gate-9-fulltext-contract` commit `c3ced65` is ancestry-merged into `main`.
- `cdx/gate-9-fulltext-recon` commit `85d2e17` and `main` commit `37a2881` have the same stable patch id for the Full Text recon changes.
