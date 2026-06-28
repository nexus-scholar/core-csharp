# Codex Branch Board

Source: live branch probes after ADR 0014 Full Text contract was merged to `main`.

## Main Baseline

- Current `main`: `c3ced65` (`docs: define full text contract`).
- `main` also includes UI contract/block-plan samples from merge commit `13f343e`.
- Gate 0 through Gate 6 are merged into `main`.
- Gate 9 Shared Identity is merged into `main`.
- Gate 9 Search, Search Import, Deduplication, and Screening local C# scopes are merged into `main`.
- Gate 9 Full Text reconnaissance is applied to `main` at `37a2881`.
- ADR 0014 Full Text acquisition, artifact, and extraction contract is merged into `main` at `c3ced65`.
- Full Text contract branch CI: https://github.com/nexus-scholar/core-csharp/actions/runs/28320117650
- Final push-triggered `main` CI for `c3ced65`: https://github.com/nexus-scholar/core-csharp/actions/runs/28320176602
- Both CI runs passed Ubuntu and Windows restore, build, test, and format.
- PHP compatibility, generated PHP fixtures, persistence/API/UI/cloud, live provider/network behavior, full-text artifact storage, actual PDF parsing, OCR, AI governance, and app behavior as Core authority remain unclaimed.

## Branch Classes

- merged: `main`, `cdx/gate-9-fulltext-contract`, `cdx/ui-contract-block-plan-samples`, `cdx/gate-9-screening-local`, `cdx/gate-9-screening-contract`, `cdx/gate-9-screening-recon`, `cdx/gate-9-dedup-local`, `cdx/gate-9-dedup-contract`, `cdx/gate-9-dedup-recon`, `cdx/gate-9-search-import-local`, `cdx/gate-9-search-import-contract`, `cdx/gate-9-search-local`, `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: `origin/cdx/gate-9-fulltext-contract` is safe by ancestry after merge to `main`.
- active: `origin/cdx/ui-phase-3-avalonia-renderer` (`4efd341`) is an unrelated UI renderer prototype lane; do not fold it into Full Text work.
- review: none.
- blocked: PHP compatibility claims, generated PHP fixtures, persistence/API/UI/cloud, live provider/network behavior, Scopus API, Web of Science API, Google Scholar scraping, paywall bypass, shadow-library sources, AI governance, full-text artifact storage, actual PDF parsing, OCR, and app integration claims remain out of scope.
- stale: `origin/cdx/gate-9-fulltext-recon` is patch-equivalent to `37a2881` but not ancestry-merged because `origin/main` advanced before the merge; do not classify it as an ancestry-merged cleanup branch without explicit human approval.

Remote cleanup state from `git branch -r --merged main`:

- `origin/HEAD -> origin/main`
- `origin/cdx/gate-9-fulltext-contract`
- `origin/main`

Remote branches currently visible:

- `origin/main`
- `origin/cdx/gate-9-fulltext-contract`
- `origin/cdx/gate-9-fulltext-recon`
- `origin/cdx/ui-phase-3-avalonia-renderer`

## Safe Cleanup Candidates

- `origin/cdx/gate-9-fulltext-contract`

## Not Safe To Delete

- `main`
- `origin/cdx/gate-9-fulltext-recon` until patch-equivalent branch cleanup is explicitly accepted.
- `origin/cdx/ui-phase-3-avalonia-renderer` because it is an active unrelated UI prototype lane.

## Next Work

- Next branch: `cdx/gate-9-fulltext-local`.
- Goal: implement local C# Full Text no-network slice against `ADR 0014`.
- Allowed first slice: Full Text input records, acquisition records, source attempts, artifact evidence records, raw byte digest validation, duplicate artifact digest detection, PDF/XML/text validators, manual/user-supplied acquisition, deterministic stub artifacts, structured extraction records, local conformance fixtures, and evidence docs.
- Do not add live providers, HTTP clients, credentials, scraping, provider SDKs, artifact storage, persistence/API/UI/cloud, actual PDF text extraction, OCR, PHP-generated fixtures, PHP compatibility claims, CLI/Web changes, or Screening behavior changes.

## Unresolved Boundaries

- `CF-025`: resolved for the local Full Text contract by `ADR 0014`; implementation must preserve exact raw bytes plus `raw-artifact-bytes` digest identity.
- `CF-026`: narrowed by `ADR 0014`; live provider/network and legal-access behavior remain future.
- `CF-027`: narrowed for Core by `ADR 0014`; app rows and paths remain projections unless transformed into Core Full Text records.
- `CF-024`: Screening app workflow rows remain projections.
- `CF-019`: Search import remaining parser families and live provider/API integration remain future.
