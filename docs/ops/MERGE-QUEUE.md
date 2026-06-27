# Merge Queue

Source: live status from branch probes after Gate 6 merge.

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
- `cdx/two-model-codex-workflow` (historical merged workflow setup branch)
- `cdx/shared-identity-adr-0007` (reconnaissance planning branch)

## Current Queue

- `main` includes Gate 6 at `7bb279e97a26c21b4400d0f38e245864931141f7`.
- Gate 6 final branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28275496330`.
- Gate 6 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28275617885`.
- Next primary branch: `cdx/gate-9-search-recon`.
- Search reconnaissance is docs/planning only; Search implementation, provider/network calls, PHP compatibility, and generated PHP fixtures are not queued yet.

## Cleanup Candidates

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

## Verification

- `git branch --merged main` currently confirms the cleanup candidates above.
- `git branch --all --no-merged main` returned no unmerged local/remote gate branches at the time of this refresh.
