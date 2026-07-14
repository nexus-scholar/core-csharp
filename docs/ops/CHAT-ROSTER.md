# Chat Roster

Status date: 2026-07-14

## Active Lanes

- Main development lane: `main` at the Hardening 30 closeout merge `f0e5806`.
- Current manager lane: FE-01 implementation and integration on `cdx/fe-01-decision-snapshot-authority`.
- Compatibility evidence lane: Phase 7 complete; future additions remain case-scoped and ADR-mediated.
- Historical public-site lane: `gh-pages`; deployable site source now moves to `site/` on `main`.

## Ownership

- Release manager: package policy, release evidence, validation workflow, governance settings, and final rehearsal.
- Security lane: dependency review, CodeQL, private vulnerability reporting, and security policy.
- Documentation lane: Pages source, maturity non-claims, branch board, merge queue, roster, and port matrix.
- Compatibility lane: preserve pinned fixtures, semantic classifications, and explicit non-claims; do not broaden evidence into package-wide PHP compatibility.

## Current Boundaries

- Phase 6 artifacts are unsigned and validation-only.
- Model outputs remain proposals; human authorization boundaries are unchanged.
- No live provider, persistence, cloud, PDF/OCR, product UI, publication, or signing implementation is authorized before its accepted feature gate.
- The active operating roadmap is `docs/plans/2026-07-14-feature-expansion-priority.md`.
- Tag `v0.1.0-alpha.1` is a validated and attested rehearsal artifact set, not a published NuGet release.
- Active validation package identity is `0.1.0-alpha.2`; publication and signing remain disabled.
