# Chat Roster

Branch-derived Codex lane roster from current git state after ADR 0014 Full Text contract merge.

## Active Lanes

- Lane `main`: merged baseline containing Gate 0 through Gate 6, Gate 9 Shared Identity, Gate 9 Search, Search Import, Deduplication, Screening, UI contract/block-plan samples, Full Text reconnaissance, and ADR 0014 Full Text contract; current head `c3ced65`.
- Lane `gate-9-fulltext-recon`: closed docs-only reconnaissance lane, original branch head `85d2e17`, content applied to `main` at `37a2881`; remote branch remains because it is patch-equivalent but not ancestry-merged.
- Lane `ui-phase-3-avalonia-renderer`: unrelated UI renderer prototype lane at `4efd341`; keep separate from Full Text implementation.

## Branch Containment Relationships

- `main` contains the implemented local review pipeline through Search, Import, Deduplication, and Screening.
- `main` contains Gate 9 Full Text reconnaissance docs and conflict/fixture planning.
- `main` contains ADR 0014 Full Text acquisition, artifact, and extraction contract.
- `main` contains UI contract/block-plan sample work from merge commit `13f343e`.
- `cdx/gate-9-fulltext-contract` is ancestry-contained by `main`.
- `cdx/gate-9-fulltext-recon` is not ancestry-contained by `main`; it is patch-equivalent to `37a2881` and should not be treated as a normal merged branch without explicit cleanup approval.
- `cdx/ui-phase-3-avalonia-renderer` is not contained by `main` and should not be touched by Full Text work.

## Status Notes

- Full Text contract branch CI is green on Ubuntu and Windows: https://github.com/nexus-scholar/core-csharp/actions/runs/28320117650
- Final `main` CI for `c3ced65` is green on Ubuntu and Windows: https://github.com/nexus-scholar/core-csharp/actions/runs/28320176602
- ADR 0014 defines Full Text input boundary, acquisition records, source attempts, artifact evidence records, raw byte digest identity, extraction records, failure categories, legal/access boundary, app projection boundary, and Screening handoff.
- Local C# Full Text implementation is ready only for a no-network slice.
- Raw artifact identity is exact bytes plus `raw-artifact-bytes` digest.
- Derived extraction evidence must bind back to source artifact id and raw digest, and must not replace raw artifact evidence.
- PHP `pdf_fetches`, CLI manifests, Web batches/items, app audit rows, storage paths, and download routes are projections unless transformed into ADR 0014 records.
- Live providers, scraping, paywall bypass, shadow libraries, artifact storage, actual PDF parsing, OCR, and app behavior as Core authority remain unclaimed.
- Next branch should be `cdx/gate-9-fulltext-local`.

## Explicit Non-Claims For Next Lane

- no PHP compatibility
- no PHP-generated fixtures
- no persistence/API/UI/cloud
- no live provider/network behavior
- no provider SDKs or credentials
- no paywall bypass
- no shadow-library source
- no Google Scholar scraping
- no actual PDF text extraction
- no OCR
- no artifact storage implementation
- no Screening behavior change
- no CLI/Web behavior changes
- no app behavior made authoritative
