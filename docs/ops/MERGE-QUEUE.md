# Merge Queue

Source: live status from branch probes after the ADR 0010 Search contract merge.

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
- `cdx/app-recon-cli-web-core-usage` (merged to `main`)
- `cdx/gate-9-search-contract` (merged to `main`)
- `cdx/two-model-codex-workflow` (historical merged workflow setup branch)
- `cdx/shared-identity-adr-0007` (reconnaissance planning branch)

## Current Queue

- `main` includes ADR 0010 at `49d2a6073a04dc21bac5097987022d8747c86a24`.
- ADR 0010 branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289131170`.
- ADR 0010 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289224733`.
- Next primary branch: `cdx/gate-9-search-local`.
- Gate 9 Search local implementation is allowed only for deterministic stub-provider Search trace behavior.

## Not Queued Yet

- imported-export parser contract or implementation
- live provider/network calls
- Scopus API
- Web of Science API
- Google Scholar scraping
- PHP compatibility claims
- generated PHP fixtures
- Deduplication implementation
- Screening behavior
- Search persistence/API/UI/cloud behavior
- CLI/Web app alignment

## Cleanup Candidates

- `cdx/gate-9-search-contract`
- `cdx/app-recon-cli-web-core-usage`
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
- `cdx/shared-identity-adr-0007`
- `cdx/run-gate-zero-discovery`
- `cdx/run-gate-0-discovery`

## Not Safe To Delete

- `main`

## Verification

- `git branch --merged main` confirms all local branch names listed in cleanup candidates above are merged.
- `git branch --all --no-merged main` returned no unmerged local or remote branches at the time of this refresh.
