# Branch Board

Status date: 2026-07-19

## Protected Main

- Last pre-release `origin/main`: `425e9bc` (PR #71 integrity closeout).
- Alpha.2 release authority: `v0.1.0-alpha.2` plus the exact commit in the
  distribution manifest.
- `gh-pages`: retained as historical static-site source; deployments use
  `site/` on `main`.
- Release candidate branch: `cdx/release-readiness-alpha2` (RR-01 through RR-06).
- FE-10 design has not started.
- Active roadmap: `docs/plans/2026-07-14-feature-expansion-priority.md`.

## Verified Baseline

- Hardening Phases 1-7 and Hardening 30: complete.
- FE-01 through FE-08: complete within accepted local scopes; FE-08 slices 1 through 9 complete.
- FE-09A through FE-09F: complete within accepted scope and merged through
  PR #69.
- Public Astro Pages baseline: merged through PR #70.
- Protected-main full solution: 1,011 passed, 0 failed, 2 opt-in live smokes
  skipped.
- ADR 0044 and ADR 0045 were historical integrity work and remain historical evidence.
- Remote governance limitation: one repository collaborator cannot supply an
  independent GitHub approval; main also lacks required CODEOWNER review,
  latest-push reapproval, linear history, and signed-commit enforcement. This
  limits governance claims but does not bypass the configured protected-main
  checks or the alpha.2 release gate.
- Package graph: 24 validation-only packages with reproducible pack and clean
  local-source restore/load.
- Release build and formatting: green.
- Package identity: `0.1.0-alpha.2`; NuGet publication disabled.
- Desktop identity: unsigned self-contained Windows x64 GitHub prerelease.

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

## Release Readiness Alpha 2

- Contract: ADR 0046 and accepted release gate.
- Artifact: deterministic-inventory portable ZIP, manifest, checksums, SPDX
  SBOM, and GitHub attestation.
- Runtime resilience: bounded sanitized local diagnostics.
- Recovery: lock-aware manifest backup and byte-exact new-directory restore.
- Acceptance: real Avalonia headless workflow, automation, focus, and scaling.
- Publication: exact matching protected-main tag only; branch and manual runs
  validate without publication credentials.

## Next

After alpha.2 is verified, FE-10 remains blocked behind its own ADR and gate
acceptance.

## Pages

- GitHub Pages source: Astro project under `site/` on `main`.
- Generated deployment artifact: `site/dist/` in CI only; never committed.
- Deployment workflow: `.github/workflows/pages.yml`.
- `gh-pages` is retained only as historical branch state.
