# Branch Board

Status date: 2026-07-17

## Protected Main

- `origin/main`: `805f3d6` (`docs: close FE-08 Slice 4 evidence`).
- FE-08 Slice 4 implementation: `7a071cc`.
- Active roadmap:
  `docs/plans/2026-07-14-feature-expansion-priority.md`.
- Immediate candidate: design and accept FE-08 Slice 5 before implementing the
  first desktop Screening mutation.
- Active feature branch: none recorded on protected `main`.

## Verified Baseline

- Hardening Phases 1-7 and Hardening 30: complete.
- FE-01 through FE-07: complete within accepted local scopes.
- FE-08 slices 1 through 4: complete.
- Full solution: 906 passed, 0 failed, 0 skipped.
- Package graph: 23 validation-only packages, reproducible pack, clean local
  source restore/load.
- Release build and formatting: green.
- Hosted PR 66 checks: Ubuntu, Windows, CodeQL, and dependency review passed.
- Package identity: `0.1.0-alpha.2`; publication disabled.

## Product Boundary

Implemented persistence is durable, project-relative local file persistence:
authority generations, scientific records, provenance, invalidation, generated
projections, and export ledgers. There is no database, server API, cloud sync,
authentication, tenancy, or multi-user operation.

The desktop can invoke admitted, authority-checked commands. It does not own
scientific authority. FE-08 Slice 4 provides fail-closed Screening readiness,
not a desktop Screening decision.

## Not Queued

- FE-08 Slice 5 implementation without an accepted ADR and gate;
- Workflow completion from the desktop;
- Protocol or Screening-criteria authoring from the desktop;
- live providers, HTTP retrieval, scraping, PDF/OCR;
- plugin runtime, live AI/model calls, or proposal acceptance;
- database, API, cloud, synchronization, authentication, or multi-user work;
- package signing or publication;
- broad PHP compatibility.

## Pages

- Deployable GitHub Pages source: `site/` on `main`.
- Deployment workflow: `.github/workflows/pages.yml`.
- `gh-pages` is retained only as historical branch state.
