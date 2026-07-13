# Branch Board

Status date: 2026-07-13

## Current Repository State

- `main`: Phase 6 complete through Hardening 22 at merge `8c55bcd`.
- `gh-pages`: retained as historical static-site source at `9a76975`; new Pages deployments are sourced from `site/` on `main`.
- Active hardening branch: `cdx/hardening-23-pages-ops`.
- Active plan: `docs/reviews/2026-07-11-hardening-plan/README.md`.
- Feature expansion remains frozen.

## Completed Hardening

- Phases 1-5 are complete.
- Phase 6 has accepted release policy, a 12-package validation topology, 30 locked solution restore graphs, normalized package reproducibility, clean local-source package smoke, SPDX SBOM and release evidence, retained test artifacts, dependency review, CodeQL SARIF, and validation-only artifact attestation.
- Current main verification baseline is 539 tests on Windows and Linux.

## Remaining Phase 6 Work

1. Merge Hardening 23 Pages and operational/security documentation.
2. Apply and verify `main` protection, private vulnerability reporting, and the protected `release` environment.
3. Create the matching release-candidate tag and complete the clean hosted release-validation rehearsal.

## Product Boundary

No live providers, scraping, persistence/API/cloud, PDF/OCR, product UI shell, package publication, signing, executable merge decisions, or PHP compatibility claims are authorized.

## Protected References

- `main`
- `gh-pages` historical branch
- `origin/main`
- `origin/gh-pages`
