# Codex Branch Board

Source: post-merge branch probes from local `main` after Gate 4 merge.

## Main Baseline

- Current `main` commit: `f3266dfacd3c2d0042c87a11be7b294ac423ff03` (`Merge Gate 4 workflow compiler`).
- Gate 0 through Gate 4 are merged into `main`.
- Gate 4 hosted CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28270846959`.
- `cdx/gate-4-workflow` and `cdx/gate-4-workflow-planning` are included in the merge baseline.

## Branch Classes

- merged: `main`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/run-gate-zero-discovery`
- active: `main`
- blocked: none recorded
- stale: `cdx/run-gate-0-discovery`, `cdx/main-gate2-merge`
- review: none identified by current git state

Cleanup candidates above are confirmed by `git branch --merged main`. Remote merge state also confirms `origin/cdx/gate-4-workflow` and `origin/cdx/gate-4-workflow-planning` are merged into `origin/main`.

## Safe Cleanup Candidates

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

## Next Work

- Next main branch target is `cdx/gate-5-provenance`.
- Optional parallel branch: `cdx/gate-9-shared-identity` for docs and shared-identity fixture/conflict support.
- PHP Shared identity reconnaissance remains docs-only parallel work.

## Unresolved Ambiguity

- No unresolved cleanup ambiguity after Gate 4 merge evidence capture; `cdx/run-gate-0-discovery` is still retained for historical reference.
