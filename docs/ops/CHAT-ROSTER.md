# Chat Roster

Branch-derived Codex lane roster from current git state after the ADR 0010 Search contract merge.

## Active Lanes

- Lane `main`: merged baseline containing Gate 0 through Gate 6, Gate 9 shared identity, Gate 9 Search reconnaissance, app consumer reconnaissance, and ADR 0010 Search Trace and Plan Contract; ADR 0010 merge head `49d2a60`.
- Lane `gate-9-search-contract`: merged ADR/contract branch `cdx/gate-9-search-contract`, head `49d2a60`.
- Lane `app-recon-cli-web-core-usage`: merged docs-only app consumer reconnaissance branch, head `c783d55`.
- Lane `gate-9-search-recon`: merged docs-only PHP Search reconnaissance branch, head `3688ca1`.
- Lane `gate-6-bundle-planning`: merged implementation branch `cdx/gate-6-bundle-planning`, head `7bb279e`.
- Lane `gate-9-shared-identity`: merged implementation branch `cdx/gate-9-shared-identity`, head `efde929`.
- Lane `gate-5-provenance`: merged closeout branch `cdx/gate-5-provenance`, head `360ed8b`.
- Lane `gate-4-workflow`: merged implementation branch `cdx/gate-4-workflow`, head `9ccc795`.
- Lane `gate-4-workflow-planning`: merged planning branch `cdx/gate-4-workflow-planning`, head `3cf28ce`.
- Lane `gate-3-protocol-lifecycle`: merged closeout branch `cdx/gate-3-protocol-lifecycle`, head `b513d6a`.
- Lane `gate-3-planning-decisions`: merged planning branch `cdx/gate-3-planning-decisions`, head `d925796`.
- Lane `gate-2-digest-kernel-cleanup`: merged kernel cleanup branch `cdx/gate-2-digest-kernel-cleanup`, head `5e5dde1`.
- Lane `gate-2`: merged evidence branch `cdx/run-gate-zero-discovery`, head `e17ec4f`.
- Lane `gate-0`: merged historical bootstrap branch `cdx/run-gate-0-discovery`, head `ee46eb4`.

## Branch Containment Relationships

- `main` contains Gate 0 through Gate 6, Gate 9 shared identity, Gate 9 Search reconnaissance, app consumer reconnaissance, and ADR 0010.
- `main` contains the two-model workflow setup branch and the Gate 9 shared-identity ADR/reconnaissance branch.
- `cdx/gate-9-search-contract` is now a merged historical lane rather than an active planning branch.
- No local or remote branches were unmerged from `main` at the time of this refresh.

## Status Notes

- ADR 0010 is merged with green branch CI and green push-triggered `main` CI.
- ADR 0010 defines Search output as a raw trace, not a deduplicated corpus.
- First Search implementation remains stub-provider-only.
- Imported-export traces are admitted only as future acquisition evidence; import parser implementation remains blocked by `CF-019`.
- Search implementation readiness is `Yes` only for local deterministic stub-provider Search.
- Next branch should be `cdx/gate-9-search-local`.
- Cleanup-safe merged lanes now include `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, and `cdx/run-gate-0-discovery`.

## Explicit Non-Claims For Next Lane

- no live provider/network behavior
- no import parser implementation
- no Scopus API
- no Web of Science API
- no Google Scholar scraping
- no PHP compatibility
- no generated PHP fixtures
- no Deduplication
- no Screening
- no persistence/API/UI/cloud
- no app behavior made authoritative
