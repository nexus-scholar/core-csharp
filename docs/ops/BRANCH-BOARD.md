# Codex Branch Board

Source: live branch probes from local `main` after the Gate 9 Search import contract merge and GitHub branch cleanup.

## Main Baseline

- Current `main` head: `89f065b` (`docs: tighten search import contract review findings`).
- Gate 0 through Gate 6 are merged into `main`.
- Gate 9 shared identity is merged into `main`.
- Gate 9 Search reconnaissance is merged into `main` as docs/planning only.
- ADR 0010 Search Trace and Plan Contract is merged into `main`.
- Gate 9 Search local stub-provider implementation is merged into `main`.
- ADR 0011 Search Import Source Contract is merged into `main`.
- ADR 0010 branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289131170`.
- ADR 0010 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289224733`.
- Gate 9 Search local branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290113371`.
- Gate 9 Search push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290167673`.
- ADR 0011 branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290630584`.
- ADR 0011 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290718641`.
- GitHub remote branch cleanup is complete; remote branches now are only `origin/main`.
- Search local implementation is complete only for deterministic stub-provider Search traces.
- Imported-export parser readiness is `Yes` for future local parser implementation over user-supplied export files, but no parser is implemented yet.

## Branch Classes

- merged: `main`, `cdx/gate-9-search-import-contract`, `cdx/gate-9-search-local`, `cdx/gate-9-search-contract`, `cdx/app-recon-cli-web-core-usage`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: local-only historical branches listed above may be pruned locally when desired; GitHub merged branch cleanup is already complete.
- active: none in the local branch graph after this refresh
- review: none
- blocked: live provider/network behavior, Scopus API, Web of Science API, Google Scholar scraping, PHP compatibility claims, generated PHP fixtures, Deduplication, Screening, persistence/API/UI/cloud, and app integration claims remain out of scope.

Remote cleanup state should be rechecked with `git branch -r`; at this refresh only `origin/main` remains.

## Safe Cleanup Candidates

- no GitHub remote cleanup candidates remain after this refresh

## Not Safe To Delete

- `main`

## Next Work

- Next branch: `cdx/gate-9-search-import-local`.
- Goal: implement local import parsers over user-supplied export files.
- Recommended first implementation slice: RIS, BibTeX, Scopus CSV/export, `source_file_digest`, local import actor, and parser warnings.
- Do not implement live providers, provider/network calls, Scopus API, Web of Science API, Google Scholar scraping, PHP-generated fixtures, PHP compatibility, Deduplication, Screening, persistence/API/UI/cloud, or CLI/Web alignment in the import-local branch.

## Unresolved Boundaries

- `CF-013`: implemented for local Search cache identity.
- `CF-016`: implemented for raw Search trace and no-Dedup boundary.
- `CF-017`: implemented for local schema-closed Search plans.
- `CF-018`: remains narrowed for app consumer boundary.
- `CF-019`: resolved for local Search import source contract by ADR 0011; parser implementation is now allowed as a local-only future gate with explicit non-claims.
