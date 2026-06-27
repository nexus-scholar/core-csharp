# Codex Branch Board

Source: live branch probes from local `main` after the Gate 9 Search local implementation merge.

## Main Baseline

- Current `main` head: `9431e4b56112f0c0a97e63a3665aec06497f3fce` (`Record Gate 9 Search hosted CI evidence`).
- Gate 0 through Gate 6 are merged into `main`.
- Gate 9 shared identity is merged into `main`.
- Gate 9 Search reconnaissance is merged into `main` as docs/planning only.
- ADR 0010 Search Trace and Plan Contract is merged into `main`.
- Gate 9 Search local stub-provider implementation is merged into `main`.
- ADR 0010 branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289131170`.
- ADR 0010 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289224733`.
- Gate 9 Search local branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290113371`.
- Gate 9 Search push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290167673`.
- Search local implementation is complete only for deterministic stub-provider Search traces.
- Imported-export parser readiness is `No`; a future Search import contract is required.

## Branch Classes

- merged: `main`, `cdx/gate-9-search-local`, `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: `cdx/gate-9-search-local`, `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- active: none in the local branch graph after this refresh
- review: none
- blocked: live provider/network behavior, Search import parsers, Scopus API, Web of Science API, Google Scholar scraping, PHP compatibility claims, generated PHP fixtures, Deduplication, Screening, persistence/API/UI/cloud, and app integration claims remain out of scope.

Cleanup candidates above should be rechecked with `git branch --merged main` before deletion.

## Safe Cleanup Candidates

- `cdx/gate-9-search-contract`
- `cdx/gate-9-search-local`
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

- Next branch: `cdx/gate-9-search-import-contract`.
- Goal: plan the imported-export Search source contract only.
- Allowed next planning scope: supported import evidence families, source file digest rules, source-specific identifier handling, parser warnings, comparator policy, and fixture catalog for future imported-export traces.
- Do not implement import parsers, live providers, provider/network calls, Scopus API, Web of Science API, Google Scholar scraping, PHP-generated fixtures, PHP compatibility, Deduplication, Screening, persistence/API/UI/cloud, or CLI/Web alignment in the import-contract branch.

## Unresolved Boundaries

- `CF-013`: implemented for local Search cache identity.
- `CF-016`: implemented for raw Search trace and no-Dedup boundary.
- `CF-017`: implemented for local schema-closed Search plans.
- `CF-018`: remains narrowed for app consumer boundary.
- `CF-019`: remains future/planning for imported-export source contracts and blocks import parser implementation until resolved.
