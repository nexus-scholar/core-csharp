# Codex Branch Board

Source: live branch probes from local `main` after the ADR 0013 Screening contract merge.

## Main Baseline

- Latest ADR 0013 Screening contract merge on `main`: `49d068c` (`docs: close screening ADR review findings`); later ops-only commits may sit on top.
- Gate 0 through Gate 6 are merged into `main`.
- Gate 9 shared identity is merged into `main`.
- Gate 9 Search reconnaissance is merged into `main` as docs/planning only.
- ADR 0010 Search Trace and Plan Contract is merged into `main`.
- Gate 9 Search local stub-provider implementation is merged into `main`.
- ADR 0011 Search Import Source Contract is merged into `main`.
- Gate 9 Search import local first-slice parser implementation is merged into `main`.
- Gate 9 Deduplication reconnaissance is merged into `main` as docs/planning only.
- ADR 0012 Deduplication Evidence and Cluster Contract is merged into `main`.
- Gate 9 Dedup local implementation is merged into `main`.
- Gate 9 Screening reconnaissance is merged into `main` as docs/planning only.
- ADR 0013 Screening Decision and Conflict Contract is merged into `main`.
- Gate 9 Screening contract branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28299739622`.
- Gate 9 Screening contract push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28299837271`.
- Local C# Dedup behavior is implemented for ADR 0012 scope only.
- Local C# Screening implementation is ready to start from ADR 0013, but is not implemented yet.
- PHP compatibility, generated PHP fixtures, persistence/API/UI/cloud, live provider/network behavior, AI governance, and app behavior as Core authority remain unclaimed.

## Branch Classes

- merged: `main`, `cdx/gate-9-screening-contract`, `cdx/gate-9-screening-recon`, `cdx/gate-9-dedup-local`, `cdx/gate-9-dedup-contract`, `cdx/gate-9-dedup-recon`, `cdx/gate-9-search-import-local`, `cdx/gate-9-search-import-contract`, `cdx/gate-9-search-local`, `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: no merged GitHub remote branch cleanup candidates remain; local historical branches listed above may be pruned locally when desired.
- active: none in the local branch graph after this refresh
- review: none
- blocked: PHP compatibility claims, generated PHP fixtures, persistence/API/UI/cloud, live provider/network behavior, Scopus API, Web of Science API, Google Scholar scraping, AI governance, and app integration claims remain out of scope.
- stale: none newly identified beyond merged historical branches.

Remote cleanup state from `git branch -r`: only `origin/main` remains.

## Safe Cleanup Candidates

- none on GitHub

## Not Safe To Delete

- `main`

## Next Work

- Next branch: `cdx/gate-9-screening-local`.
- Goal: implement local C# Screening behavior against ADR 0013.
- Focus areas: locked/reviewable candidate-set intake, criteria digest binding, human final decisions, AI/rule suggestions as proposal evidence, conflict detection, append-only adjudication, source decision links, immutable decision history, fixtures, conformance checks, and Gate 9 Screening evidence.
- Do not add persistence/API/UI/cloud, live provider/network behavior, PHP-generated fixtures, PHP compatibility claims, CLI/Web changes, AI governance implementation, full-text retrieval, artifact storage, or app behavior as Core authority.

## Unresolved Boundaries

- `CF-011`: implemented for local Dedup input shape; PHP compatibility pending.
- `CF-012`: implemented for local threshold `95` / `0.95`; PHP compatibility pending.
- `CF-016`: implemented for Search and local Dedup handoff without changing Search behavior.
- `CF-018`: remains narrowed for app consumer boundary.
- `CF-019`: implemented for local first-slice Search import parser behavior only; remaining parser families, live APIs, source-specific namespace expansion, PHP compatibility, and app alignment remain future.
- `CF-020`: narrowed by ADR 0012; Web hashes, snapshots, persisted runs, and app scoring remain app projections, not Core authority.
- `CF-021`: resolved for local Screening contract by ADR 0013; implementation pending.
- `CF-022`: resolved for local Screening contract by ADR 0013; implementation pending.
- `CF-023`: resolved for local Screening contract by ADR 0013; implementation pending.
- `CF-024`: narrowed for Core by ADR 0013; app workflow rows remain projections.
