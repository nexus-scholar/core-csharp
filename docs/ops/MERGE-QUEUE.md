# Merge Queue

Source: live status from branch probes after the Gate 9 Search import contract merge and GitHub branch cleanup.

## Completed Merges

- `cdx/run-gate-zero-discovery` (merged to `main`)
- `cdx/gate-2-digest-kernel-cleanup` (merged to `main`)
- `cdx/gate-3-planning-decisions` (merged to `main`)
- `cdx/gate-3-protocol-lifecycle` (merged to `main`)
- `cdx/gate-4-workflow-planning` (merged to `main`)
- `cdx/gate-4-workflow` (merged to `main`)
- `cdx/gate-5-provenance` (merged to `main`)
- `cdx/gate-9-shared-identity` (merged to `main`)
- `cdx/gate-6-bundle-planning` (merged to `main`)
- `cdx/gate-9-search-recon` (merged to `main`)
- `cdx/app-recon-cli-web-core-usage` (merged to `main`)
- `cdx/gate-9-search-contract` (merged to `main`)
- `cdx/gate-9-search-local` (merged to `main`)
- `cdx/gate-9-search-import-contract` (merged to `main`)
- `cdx/two-model-codex-workflow` (historical merged workflow setup branch)
- `cdx/shared-identity-adr-0007` (reconnaissance planning branch)

## Current Queue

- `main` includes ADR 0011 Search Import Source Contract at `89f065b`.
- ADR 0010 branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289131170`.
- ADR 0010 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28289224733`.
- Gate 9 Search local branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290113371`.
- Gate 9 Search push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290167673`.
- ADR 0011 branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290630584`.
- ADR 0011 push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28290718641`.
- GitHub remote branch cleanup is complete; only `origin/main` remains.
- Next primary branch: `cdx/gate-9-search-import-local`.
- Gate 9 Search import-local work may implement local parsers over user-supplied export files only.

## Not Queued Yet

- imported-export parser implementation
- live provider/network calls
- Scopus API
- Web of Science API
- Google Scholar scraping
- PHP compatibility claims
- generated PHP fixtures
- Deduplication implementation
- Screening behavior
- Search persistence/API/UI/cloud behavior
- CLI/Web app alignment

## Cleanup Candidates

- no GitHub remote cleanup candidates remain after this refresh

## Not Safe To Delete

- `main`

## Verification

- `git branch -r` returns only `origin/HEAD -> origin/main` and `origin/main`.
- `git branch --all --no-merged main` returned no unmerged local or remote branches at the time of this refresh.
