# Codex Branch Board

Source: live branch probes after Gate 9 Full Text reconnaissance was applied to `main`.

## Main Baseline

- Current `main`: `37a2881` (`docs: map PHP full text behavior`).
- `main` also includes UI contract/block-plan samples from merge commit `13f343e`.
- Gate 0 through Gate 6 are merged into `main`.
- Gate 9 Shared Identity is merged into `main`.
- Gate 9 Search, Search Import, Deduplication, and Screening local C# scopes are merged into `main`.
- Gate 9 Full Text reconnaissance is applied to `main` at `37a2881`.
- Original Full Text recon branch commit: `85d2e17`.
- Full Text recon branch CI: https://github.com/nexus-scholar/core-csharp/actions/runs/28318711028
- Final push-triggered `main` CI for `37a2881`: https://github.com/nexus-scholar/core-csharp/actions/runs/28318771878
- Both CI runs passed Ubuntu and Windows restore, build, test, and format.
- PHP compatibility, generated PHP fixtures, persistence/API/UI/cloud, live provider/network behavior, full-text artifact storage, AI governance, and app behavior as Core authority remain unclaimed.

## Branch Classes

- merged: `main`, `cdx/ui-contract-block-plan-samples`, `cdx/gate-9-screening-local`, `cdx/gate-9-screening-contract`, `cdx/gate-9-screening-recon`, `cdx/gate-9-dedup-local`, `cdx/gate-9-dedup-contract`, `cdx/gate-9-dedup-recon`, `cdx/gate-9-search-import-local`, `cdx/gate-9-search-import-contract`, `cdx/gate-9-search-local`, `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: none currently safe by ancestry on GitHub; `git branch -r --merged main` returns only `origin/main`.
- active: none.
- review: none.
- blocked: PHP compatibility claims, generated PHP fixtures, persistence/API/UI/cloud, live provider/network behavior, Scopus API, Web of Science API, Google Scholar scraping, paywall bypass, shadow-library sources, AI governance, full-text artifact storage, and app integration claims remain out of scope.
- stale: `origin/cdx/gate-9-fulltext-recon` is patch-equivalent to `37a2881` but not ancestry-merged because `origin/main` advanced before the merge; do not classify it as an ancestry-merged cleanup branch without explicit human approval.

Remote cleanup state from `git branch -r`:

- `origin/main`
- `origin/cdx/gate-9-fulltext-recon`

## Safe Cleanup Candidates

- none on GitHub by ancestry.

## Not Safe To Delete

- `main`
- `origin/cdx/gate-9-fulltext-recon` until patch-equivalent branch cleanup is explicitly accepted.

## Next Work

- Next branch: `cdx/gate-9-fulltext-contract`.
- Goal: write `ADR 0014: Full Text Artifact Evidence Contract`.
- Focus areas: Full Text input boundary, raw artifact byte identity, artifact evidence record shape, no-network first implementation boundary, source attempt model, legal/access boundary, PDF/XML/text validation, XML/text sidecar status, Screening handoff, app projection boundary, and fixture/comparator consequences.
- Do not add C# Full Text implementation, live providers, HTTP clients, credentials, scraping, artifact storage, persistence/API/UI/cloud, PHP-generated fixtures, PHP compatibility claims, CLI/Web changes, or Screening behavior changes.

## Unresolved Boundaries

- `CF-025`: Full Text artifact evidence and raw-byte identity; blocks C# Full Text implementation.
- `CF-026`: Full Text provider/network and legal-access boundary; blocks live source adapters.
- `CF-027`: Full Text app projection and Screening handoff boundary; blocks treating CLI/Web rows or paths as Core authority.
- `CF-024`: Screening app workflow rows remain projections.
- `CF-019`: Search import remaining parser families and live provider/API integration remain future.
