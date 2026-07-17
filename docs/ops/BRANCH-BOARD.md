# Branch Board

Status date: 2026-07-17

## Protected Main

- `origin/main`: `ea665eb` (PR #69 FE-09 protected-main closeout).
- `gh-pages`: retained as historical static-site source; deployments use
  `site/` on `main`.
- Active feature branch: none; FE-10 design has not started.
- Active roadmap: `docs/plans/2026-07-14-feature-expansion-priority.md`.

## Verified Baseline

- Hardening Phases 1-7 and Hardening 30: complete.
- FE-01 through FE-08: complete within accepted local scopes.
- FE-09A through FE-09F: complete within accepted scope and merged through
  PR #69.
- Full solution: 1,011 passed, 0 failed, 2 opt-in live smokes skipped.
- Package graph: 24 validation-only packages with reproducible pack and clean
  local-source restore/load.
- Release build and formatting: green.
- Package identity: `0.1.0-alpha.2`; publication disabled.

## Product Boundary

Scientific authority remains in immutable, digest-bound Core records and local
workspace generations. The desktop and provider hosts invoke admitted commands;
they do not own scientific authority.

FE-09 admits bounded Search transport, provider evidence caching, recorded Full
Text retrieval verification, and local citation snapshots. Semantic Scholar
body retention remains digest-only by default. Live Full Text transport,
scraping, paywall bypass, citation exports, PHP parity, plugin execution,
database/API/cloud, authentication, tenancy, and multi-user operation remain
outside the accepted scope.

## Next

Begin FE-10 plugin-runtime design and capability-security review under a new
accepted gate.

## Pages

- GitHub Pages source: Astro project under `site/` on `main`.
- Generated deployment artifact: `site/dist/` in CI only; never committed.
- Deployment workflow: `.github/workflows/pages.yml`.
- `gh-pages` is retained only as historical branch state.
