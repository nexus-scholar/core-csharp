# Codex Branch Board

Source: live branch probes from local `main` after the Gate 9 Search reconnaissance merge.

## Main Baseline

- Current `main` head: `7f344bb70b2a8b275a236a6c581af2c091a2f5c8` (`docs: refresh operations board after search recon merge`).
- Gate 0 through Gate 6 are merged into `main`.
- Gate 9 shared identity is merged into `main`; Gate 9 was intentionally started before Gate 6.
- Gate 9 Search reconnaissance is merged into `main` as docs/planning only.
- Gate 9 Search recon branch CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28285488732`.
- Gate 9 Search recon push-triggered `main` CI is green: `https://github.com/nexus-scholar/core-csharp/actions/runs/28285547851`.
- Search implementation readiness remains `No`.

## Branch Classes

- merged: `main`, `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/shared-identity-adr-0007`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: `cdx/gate-9-search-recon`, `cdx/gate-6-bundle-planning`, `cdx/gate-9-shared-identity`, `cdx/gate-5-provenance`, `cdx/two-model-codex-workflow`, `cdx/main-gate2-merge`, `cdx/gate-4-workflow`, `cdx/gate-4-workflow-planning`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/run-gate-zero-discovery`
- active: `cdx/app-recon-cli-web-core-usage`
- blocked: Search implementation, provider/network behavior, PHP compatibility claims, generated PHP fixtures, Deduplication/Search integration, and app integration claims remain blocked until ADR 0010 resolves the Search trace and plan contract plus the app consumer boundary.
- stale: `cdx/run-gate-0-discovery`, `cdx/main-gate2-merge`
- review: none identified by current git state

Cleanup candidates above should be rechecked with `git branch --merged main` before deletion.

## Safe Cleanup Candidates

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
- `cdx/run-gate-zero-discovery`

## Not Safe To Delete

- `main`
- `cdx/run-gate-0-discovery`
- `cdx/app-recon-cli-web-core-usage` until its docs branch is merged or closed

## Next Work

- Current active branch target: `cdx/app-recon-cli-web-core-usage`.
- Goal: persist CLI/Web app consumer reconnaissance as integration evidence.
- Scope: docs only under `docs/recon/apps/`, plus app-boundary updates to `docs/port/OPEN-CONFLICTS.md` and this branch board.
- Next primary branch after app recon docs: `cdx/gate-9-search-contract` for ADR 0010 Search Trace and Plan Contract.
- Do not implement Search, providers, network behavior, persistence, API/UI/cloud, PHP compatibility, generated PHP fixtures, or blueprint conformance in the app recon branch unless a new prompt explicitly changes scope.

## Unresolved Ambiguity

- `cdx/run-gate-0-discovery` is still retained for historical reference.
- `cdx/app-recon-cli-web-core-usage` is an active docs-only app evidence branch and should not be treated as a gate implementation branch.
