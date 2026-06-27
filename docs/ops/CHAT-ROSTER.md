# Chat Roster

Branch-derived Codex lane roster from current git state after the Gate 9 Search import contract merge and GitHub branch cleanup.

## Active Lanes

- Lane `main`: merged baseline containing Gate 0 through Gate 6, Gate 9 shared identity, Gate 9 Search reconnaissance, app consumer reconnaissance, ADR 0010 Search Trace and Plan Contract, Gate 9 local stub-provider Search implementation, and ADR 0011 Search Import Source Contract; current head `89f065b`.
- Lane `gate-9-search-import-contract`: merged ADR/contract branch `cdx/gate-9-search-import-contract`, head `89f065b`.
- Lane `gate-9-search-local`: merged local implementation branch `cdx/gate-9-search-local`, head `9431e4b`.
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

- `main` contains Gate 0 through Gate 6, Gate 9 shared identity, Gate 9 Search reconnaissance, app consumer reconnaissance, ADR 0010, local stub-provider Search, and ADR 0011.
- `main` contains the two-model workflow setup branch and the Gate 9 shared-identity ADR/reconnaissance branch.
- `cdx/gate-9-search-contract` is now a merged historical lane rather than an active planning branch.
- `cdx/gate-9-search-local` is now a merged historical lane rather than an active implementation branch.
- `cdx/gate-9-search-import-contract` is now a merged historical lane rather than an active planning branch.
- GitHub remote branch cleanup is complete; only `origin/main` remains.
- No local or remote branches were unmerged from `main` at the time of this refresh.

## Status Notes

- Gate 9 Search local implementation is merged with green branch CI and green push-triggered `main` CI.
- ADR 0011 Search Import Source Contract is merged with green branch CI and green push-triggered `main` CI.
- ADR 0010 defines Search output as a raw trace, not a deduplicated corpus.
- First Search implementation is stub-provider-only.
- Imported-export traces are admitted only as user-supplied acquisition evidence; parser implementation is now ready for a local-only implementation gate.
- Search implementation readiness is complete only for local deterministic stub-provider Search.
- Next branch should be `cdx/gate-9-search-import-local`.
- GitHub has no cleanup-safe merged remote branches left after this refresh.

## Explicit Non-Claims For Next Lane

- no live provider/network behavior
- no Scopus API
- no Web of Science API
- no Google Scholar scraping
- no PHP compatibility
- no generated PHP fixtures
- no Deduplication
- no Screening
- no persistence/API/UI/cloud
- no app behavior made authoritative
