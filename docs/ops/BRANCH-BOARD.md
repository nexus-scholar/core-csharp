# Branch Board

Status date: 2026-07-14

## Current Repository State

- `main`: hardening and closeout complete at merge `f0e5806` (PR #55).
- `gh-pages`: retained as historical static-site source at `9a76975`; new Pages deployments are sourced from `site/` on `main`.
- Active feature branch: `cdx/fe-01-decision-snapshot-authority`.
- Active plan: `docs/plans/2026-07-14-feature-expansion-priority.md`.
- Immediate job: FE-01 baseline decision-and-snapshot authority implementation under accepted ADR 0028.

## Completed Hardening

- Phases 1-7 and post-review Hardening 30 are complete on protected `main`.
- Phase 6 has accepted release policy, a 12-package validation topology, 30 locked solution restore graphs, normalized package reproducibility, clean local-source package smoke, SPDX SBOM and release evidence, retained test artifacts, dependency review, CodeQL SARIF, and validation-only artifact attestation.
- Current protected-main verification baseline is 573 tests on Windows and Linux.

## Current Closeout

- Pages is built through workflow run `29231264071`.
- `main`, private reporting, security analysis, and the tag-only `release` environment pass `scripts/verify-github-governance.ps1`.
- Tag `v0.1.0-alpha.1` completed attested clean-machine run `29231634501`.
- Phase 7 fixture-backed compatibility evidence landed through PRs #49-#53 with case-scoped claims only.
- Hardening 30 landed through PR #54 with green Windows/Linux gates, dependency review, and CodeQL checks.
- Hardening 30 closeout landed through PR #55; final `main` Gate 01 and CodeQL runs passed.
- The active validation package identity is `0.1.0-alpha.2`; `v0.1.0-alpha.1` remains the historical attested rehearsal and no NuGet package is published.

## Product Boundary

The roadmap sequences later product work but does not authorize it automatically. No live providers, scraping, persistence/API/cloud, PDF/OCR, product UI shell, package publication, signing, executable merge decisions, or broader PHP compatibility claims are authorized until their accepted gate.

## Protected References

- `main`
- `gh-pages` historical branch
- `origin/main`
- `origin/gh-pages`
