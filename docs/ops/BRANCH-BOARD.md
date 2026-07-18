# Branch Board

Status date: 2026-07-18

## Protected Main

- `origin/main`: `bdd0d828547773a622316988d8d3dc825c4e7812` (PR #70 Astro Pages and FE-09 public baseline).
- `gh-pages`: retained as historical static-site source; deployments use
  `site/` on `main`.
- Active remediation branch: `cdx/fe-09-deep-review-remediation`.
- FE-10 design has not started.
- Active roadmap: `docs/plans/2026-07-14-feature-expansion-priority.md`.

## Verified Baseline

- Hardening Phases 1-7 and Hardening 30: complete.
- FE-01 through FE-08: complete within accepted local scopes.
- FE-09A through FE-09F: complete within accepted scope and merged through
  PR #69.
- Public Astro Pages baseline: merged through PR #70.
- Protected-main full solution: 1,011 passed, 0 failed, 2 opt-in live smokes
  skipped.
- Remediation candidate: ADR 0044 and ADR 0045 implementation and local
  verification complete; 1,084 passed, 0 failed, 2 Windows-host Linux-only
  skips, 2 opt-in live smokes skipped, and 150 exact mutation cases verified
  across 9 project suites.
- Remote closeout blocker: one repository collaborator cannot supply the
  required independent approval; main also lacks required CODEOWNER review,
  latest-push reapproval, linear history, and signed-commit enforcement.
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
