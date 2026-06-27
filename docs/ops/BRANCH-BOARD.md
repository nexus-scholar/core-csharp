# Codex Branch Board

Source: live branch probes from local `main` after the ADR 0010 Search contract merge.

## Main Baseline

- ADR 0010 merge head: `49d2a6073a04dc21bac5097987022d8747c86a24` (`docs: admit imported search exports`).
- Gate 0 through Gate 6 are merged into `main`.
- Gate 9 shared identity is merged into `main`.
- Gate 9 Search reconnaissance is merged into `main` as docs/planning only.
- ADR 0010 Search Trace and Plan Contract is merged into `main`.
- ADR 0010 branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289131170`.
- ADR 0010 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289224733`.
- Search implementation readiness is `Yes` only for local deterministic stub-provider Search.
- Imported-export parser readiness is `No`; a future Search import contract is required.

## Branch Classes

- merged: `main`, `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- active: none in the local branch graph after this refresh
- review: none
- blocked: live provider/network behavior, Search import parsers, Scopus API, Web of Science API, Google Scholar scraping, PHP compatibility claims, generated PHP fixtures, Deduplication, Screening, persistence/API/UI/cloud, and app integration claims remain out of scope.

Cleanup candidates above should be rechecked with `git branch --merged main` before deletion.

## Safe Cleanup Candidates

- `cdx/gate-9-search-contract`
- `cdx/app-recon-cli-web-core-usage`
- `cdx/gate-9-search-recon`
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
- `cdx/shared-identity-adr-0007`
- `cdx/run-gate-zero-discovery`
- `cdx/run-gate-0-discovery`

## Not Safe To Delete

- `main`

## Next Work

- Next branch: `cdx/gate-9-search-local`.
- Goal: implement local C# Search trace behavior with deterministic stub providers only.
- Allowed next implementation scope: raw Search trace model, request validation, provider alias normalization, provider-order-insensitive cache identity, deterministic stub providers, schema-closed local plans, local conformance fixtures, and Gate 9 Search evidence.
- Do not implement live providers, provider/network calls, import parsers, Scopus API, Web of Science API, Google Scholar scraping, PHP-generated fixtures, PHP compatibility, Deduplication, Screening, persistence/API/UI/cloud, or CLI/Web alignment in the local Search branch.

## Unresolved Boundaries

- `CF-013`: implemented next only for local Search cache identity.
- `CF-016`: implemented next only for raw Search trace and no-Dedup boundary.
- `CF-017`: implemented next only for local schema-closed Search plans.
- `CF-018`: remains narrowed for app consumer boundary.
- `CF-019`: remains future/planning for imported-export source contracts and does not block local stub-provider Search.
