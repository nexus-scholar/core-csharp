# Chat Roster

Branch-derived Codex lane roster from current git state after the Gate 6 bundle merge.

## Active Lanes

- Lane `main`: merged baseline containing Gate 0 through Gate 6 and Gate 9, current head `7bb279e`.
- Lane `gate-6-bundle-planning`: merged implementation branch `cdx/gate-6-bundle-planning`, head `7bb279e`.
- Lane `gate-9-shared-identity`: merged implementation branch `cdx/gate-9-shared-identity`, head `efde929`.
- Lane `gate-5-provenance`: merged closeout branch `cdx/gate-5-provenance`, head `360ed8b`.
- Lane `gate-4-workflow`: merged implementation branch `cdx/gate-4-workflow`, head `9ccc795`.
- Lane `gate-4-workflow-planning`: merged planning branch `cdx/gate-4-workflow-planning`, head `3cf28ce`.
- Lane `gate-3-protocol-lifecycle`: merged closeout branch `cdx/gate-3-protocol-lifecycle`, head `b513d6a`.
- Lane `gate-3-planning-decisions`: merged planning branch `cdx/gate-3-planning-decisions`, head `d925796`.
- Lane `gate-2-digest-kernel-cleanup`: merged kernel cleanup branch `cdx/gate-2-digest-kernel-cleanup`, head `5e5dde1`.
- Lane `gate-2`: merged evidence branch `cdx/run-gate-zero-discovery`, head `e17ec4f`.
- Lane `gate-0`: stale bootstrap branch `cdx/run-gate-0-discovery`, head `ee46eb4`.

## Branch Containment Relationships

- `main` contains Gate 0 through Gate 6 and Gate 9.
- `main` contains the two-model workflow setup branch and the Gate 9 shared-identity ADR/reconnaissance branch.
- `cdx/gate-6-bundle-planning` is now a merged historical lane rather than an active delivery branch.
- `cdx/gate-9-shared-identity` remains a merged historical lane.

## Status Notes

- Gate 6 local bundle/artifact scope is merged with green final-branch CI and green push-triggered `main` CI.
- Gate 6 closes local review-bundle manifest, raw artifact byte digest, staged verification, tamper categories, import-safety, and local bundle round-trip equality scope.
- Gate 6 does not claim blueprint conformance, PHP compatibility, persistence/API/UI/cloud, provider/network behavior, workflow execution, plugin runtime, AI governance, or general corpus snapshot equality.
- Next branch should be `cdx/gate-9-search-recon`, limited to PHP Search behavior mapping and fixture/comparator planning.
- Cleanup-safe merged lanes now include `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, and `cdx/run-gate-zero-discovery`.
