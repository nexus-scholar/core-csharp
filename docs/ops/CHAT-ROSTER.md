# Chat Roster

Branch-derived Codex lane roster from current git state after the Gate 9 Search reconnaissance merge.

## Active Lanes

- Lane `main`: merged baseline containing Gate 0 through Gate 6, Gate 9 shared identity, and Gate 9 Search reconnaissance, current head `3688ca1`.
- Lane `gate-9-search-recon`: merged docs-only reconnaissance branch `cdx/gate-9-search-recon`, head `3688ca1`.
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
- Lane `app-recon-cli-web-core-usage`: local merged branch at `3688ca1`, owner/purpose not classified in this refresh.

## Branch Containment Relationships

- `main` contains Gate 0 through Gate 6, Gate 9 shared identity, and Gate 9 Search reconnaissance.
- `main` contains the two-model workflow setup branch and the Gate 9 shared-identity ADR/reconnaissance branch.
- `cdx/gate-9-search-recon` is now a merged historical lane rather than an active planning branch.
- `cdx/gate-6-bundle-planning` is a merged historical lane rather than an active delivery branch.

## Status Notes

- Gate 9 Search reconnaissance is merged with green branch CI and green push-triggered `main` CI.
- Gate 9 Search reconnaissance maps PHP Search behavior and fixture/comparator planning only.
- Gate 9 Search reconnaissance does not claim C# Search behavior, provider/network behavior, PHP compatibility, generated PHP fixtures, Deduplication, Screening, persistence, API/UI/job/cloud, bundle behavior change, AI governance, or blueprint conformance.
- Search implementation remains blocked by `CF-016`, `CF-017`, `includeRawData` cache ambiguity, stub-provider Search trace contract, and fixture/comparator strategy.
- Next branch should be `cdx/gate-9-search-contract`, focused on ADR 0010 Search Trace and Plan Contract.
- Cleanup-safe merged lanes now include `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, and `cdx/run-gate-zero-discovery`.
