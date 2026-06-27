# Codex Branch Board

Source: live branch probes from local `main` after the Gate 6 merge.

## Main Baseline

- Current `main` head: `7bb279e97a26c21b4400d0f38e245864931141f7` (`docs: record Gate 6 closeout evidence`).
- Gate 0 through Gate 6 are merged into `main`.
- Gate 9 shared identity is also merged into `main`; Gate 9 was intentionally completed before Gate 6.
- Gate 6 final branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28275496330`.
- Gate 6 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28275617885`.
- Gate 6 reviewer closeout returned no blockers.

## Branch Classes

- merged: `main`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/run-gate-zero-discovery`
- active: `main`
- blocked: Search implementation, provider/network behavior, PHP compatibility claims, and generated PHP fixtures remain blocked until Search reconnaissance freezes behavior and fixture/comparator plans.
- stale: `cdx/run-gate-0-discovery`, `cdx/main-gate2-merge`
- review: none identified by current git state

Cleanup candidates above are confirmed by `git branch --merged main`.

## Safe Cleanup Candidates

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

## Next Work

- Next active branch target: `cdx/gate-9-search-recon`.
- Scope: PHP Search behavior mapping only, including provider/cache/plan behavior, fixture plan, and comparator plan.
- Do not implement providers, network behavior, Search execution, persistence, API/UI/cloud, PHP compatibility, or blueprint conformance in the reconnaissance branch.

## Unresolved Ambiguity

- `cdx/run-gate-0-discovery` is still retained for historical reference.
