# Chat Roster

Branch-derived Codex lane roster from current git state after Gate 9 Full Text reconnaissance.

## Active Lanes

- Lane `main`: merged baseline containing Gate 0 through Gate 6, Gate 9 Shared Identity, Gate 9 Search, Search Import, Deduplication, Screening, UI contract/block-plan samples, and Full Text reconnaissance; current head `37a2881`.
- Lane `gate-9-fulltext-recon`: closed docs-only reconnaissance lane, original branch head `85d2e17`, content applied to `main` at `37a2881`; remote branch remains because it is patch-equivalent but not ancestry-merged.

## Branch Containment Relationships

- `main` contains the implemented local review pipeline through Search, Import, Deduplication, and Screening.
- `main` contains Gate 9 Full Text reconnaissance docs and conflict/fixture planning.
- `main` contains UI contract/block-plan sample work from merge commit `13f343e`.
- `cdx/gate-9-fulltext-recon` is not ancestry-contained by `main`; it is patch-equivalent to `37a2881` and should not be treated as a normal merged branch without explicit cleanup approval.

## Status Notes

- Full Text recon branch CI is green on Ubuntu and Windows: https://github.com/nexus-scholar/core-csharp/actions/runs/28318711028
- Final `main` CI for `37a2881` is green on Ubuntu and Windows: https://github.com/nexus-scholar/core-csharp/actions/runs/28318771878
- Full Text reconnaissance mapped PHP Core, CLI, and Web behavior.
- PHP Full Text combines source resolution, HTTP downloads, storage, retries/cooldowns, audit rows, and app projections.
- PHP records paths and metadata but does not record raw artifact byte digests.
- C# Full Text must use digest-bound raw artifact evidence, not local file paths.
- Live providers, scraping, paywall bypass, shadow libraries, artifact storage, and app behavior as Core authority remain unclaimed.
- Open Full Text blockers: `CF-025`, `CF-026`, and `CF-027`.
- Next branch should be `cdx/gate-9-fulltext-contract`.

## Explicit Non-Claims For Next Lane

- no C# Full Text implementation
- no PHP compatibility
- no PHP-generated fixtures
- no persistence/API/UI/cloud
- no live provider/network behavior
- no provider SDKs or credentials
- no paywall bypass
- no shadow-library source
- no Google Scholar scraping
- no artifact storage implementation
- no Screening behavior change
- no CLI/Web behavior changes
- no app behavior made authoritative
