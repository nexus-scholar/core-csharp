# Branch Board

Status date: 2026-07-13

## Current Repository State

- `main`: Phase 6 complete through Hardening 24 at merge `8436af3`.
- `gh-pages`: retained as historical static-site source at `9a76975`; new Pages deployments are sourced from `site/` on `main`.
- Active closeout branch: `cdx/hardening-phase-6-closeout`.
- Active plan: `docs/reviews/2026-07-11-hardening-plan/README.md`.
- Feature expansion remains frozen.

## Completed Hardening

- Phases 1-5 are complete.
- Phase 6 has accepted release policy, a 12-package validation topology, 30 locked solution restore graphs, normalized package reproducibility, clean local-source package smoke, SPDX SBOM and release evidence, retained test artifacts, dependency review, CodeQL SARIF, and validation-only artifact attestation.
- Current main verification baseline is 539 tests on Windows and Linux.

## Phase 6 Closeout

- Pages is built through workflow run `29231264071`.
- `main`, private reporting, security analysis, and the tag-only `release` environment pass `scripts/verify-github-governance.ps1`.
- Tag `v0.1.0-alpha.1` completed attested clean-machine run `29231634501`.
- Next implementation phase is Phase 7 compatibility evidence. No compatibility claim exists yet.

## Product Boundary

No live providers, scraping, persistence/API/cloud, PDF/OCR, product UI shell, package publication, signing, executable merge decisions, or PHP compatibility claims are authorized.

## Protected References

- `main`
- `gh-pages` historical branch
- `origin/main`
- `origin/gh-pages`
