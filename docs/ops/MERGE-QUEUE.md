# Merge Queue

Source: live status from branch probes after Gate 4 merge.

## Completed Merges

- `cdx/run-gate-zero-discovery` (merged to `main`)
- `cdx/gate-2-digest-kernel-cleanup` (merged to `main`)
- `cdx/gate-3-planning-decisions` (merged to `main`)
- `cdx/gate-3-protocol-lifecycle` (merged to `main`)
- `cdx/gate-4-workflow-planning` (merged to `main`)
- `cdx/gate-4-workflow` (merged to `main`)
- `cdx/two-model-codex-workflow` (historical merged workflow setup branch)
- `cdx/shared-identity-adr-0007` (reconnaissance planning branch)

## Current Queue

- `main` is current at `f3266dfacd3c2d0042c87a11be7b294ac423ff03`.
- Gate 4 is merged and CI green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28270846959`.
- Next primary branch: `cdx/gate-5-provenance`.
- Optional parallel branch: `cdx/gate-9-shared-identity`.

## Cleanup Candidates

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

- `git branch --merged main` currently confirms the above cleanup candidates.
