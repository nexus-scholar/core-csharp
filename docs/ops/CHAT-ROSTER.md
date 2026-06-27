# Chat Roster

Branch-derived Codex lane roster from current git state after the ADR 0013 Screening contract merge.

## Active Lanes

- Lane `main`: merged baseline containing Gate 0 through Gate 6, Gate 9 shared identity, Gate 9 Search reconnaissance, app consumer reconnaissance, ADR 0010 Search Trace and Plan Contract, Gate 9 local stub-provider Search implementation, ADR 0011 Search Import Source Contract, Gate 9 Search import local first-slice implementation, Gate 9 Deduplication reconnaissance, ADR 0012 Deduplication Evidence and Cluster Contract, Gate 9 local Dedup implementation, Gate 9 Screening reconnaissance, and ADR 0013 Screening Decision and Conflict Contract; latest merge head `49d068c`, with later ops-only commits allowed on top.
- Lane `gate-9-screening-contract`: merged ADR/contract branch `cdx/gate-9-screening-contract`, head `49d068c`.
- Lane `gate-9-screening-recon`: merged docs-only reconnaissance branch `cdx/gate-9-screening-recon`, head `095a275`.
- Lane `gate-9-dedup-local`: merged local implementation branch `cdx/gate-9-dedup-local`, head `8fa573d`.
- Lane `gate-9-dedup-contract`: merged ADR/contract branch `cdx/gate-9-dedup-contract`, head `0249f67`.
- Lane `gate-9-dedup-recon`: merged docs-only reconnaissance branch `cdx/gate-9-dedup-recon`, head `76933e3`.
- Lane `gate-9-search-import-local`: merged local implementation branch `cdx/gate-9-search-import-local`, head `970eef2`.
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

- `main` contains Gate 0 through Gate 6, Gate 9 shared identity, Gate 9 Search reconnaissance, app consumer reconnaissance, ADR 0010, local stub-provider Search, ADR 0011, local first-slice Search import parsers, Gate 9 Deduplication reconnaissance, ADR 0012, local Dedup implementation, Gate 9 Screening reconnaissance, and ADR 0013.
- `main` contains the two-model workflow setup branch and the Gate 9 shared-identity ADR/reconnaissance branch.
- `cdx/gate-9-dedup-local` is now a merged historical lane rather than an active implementation branch.
- `cdx/gate-9-dedup-contract` is now a merged historical lane rather than an active contract branch.
- `cdx/gate-9-dedup-recon` is now a merged historical lane rather than an active reconnaissance branch.
- `cdx/gate-9-screening-recon` is now a merged historical lane rather than an active reconnaissance branch.
- `cdx/gate-9-screening-contract` is now a merged historical lane rather than an active contract branch.
- No merged GitHub remote branch cleanup candidates remain after the post-merge remote cleanup.

## Status Notes

- ADR 0013 Screening contract is merged with green branch CI and green push-triggered `main` CI.
- Gate 9 Dedup local implementation is merged with green branch CI and green push-triggered `main` CI.
- Dedup consumes raw Search/import sightings, not PHP's pre-deduplicated Search corpus.
- Exact ADR 0007 identifier overlap forms automatic clusters namespace-sensitively.
- Fuzzy title matching is review-required candidate evidence with local threshold `95` / `0.95`.
- No-id, title-only, source-specific-id-only, and runtime-object identity never auto-merge.
- Representative records are deterministic projections over preserved evidence.
- Imported-export source-file digest, digest scope, raw-record digest, parser warnings, and record notices are preserved through Dedup raw candidates, source evidence, and representative projections where clustered.
- Web hashes, representative snapshots, persisted runs, stale-run checks, and app scoring remain app projections, not Core authority.
- Screening recon mapped PHP Core, CLI, and Web screening behavior and opened `CF-021` through `CF-024`.
- ADR 0013 resolves `CF-021`, `CF-022`, and `CF-023` for the local Screening contract, and narrows `CF-024` for Core/app projection boundaries.
- Next branch should be `cdx/gate-9-screening-local`.

## Explicit Non-Claims For Next Lane

- no PHP compatibility
- no PHP-generated fixtures
- no persistence/API/UI/cloud
- no live provider/network behavior
- no Scopus API
- no Web of Science API
- no Google Scholar scraping
- no AI governance implementation
- no app behavior made authoritative
