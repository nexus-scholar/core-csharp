# Hardening 23 - Pages and Operations

Status: complete.

## Scope

- move the deployable static site source from legacy `gh-pages` history into `site/` on `main`;
- deploy Pages through current official Actions with OIDC and the `github-pages` environment;
- correct public project maturity wording to audit-oriented early alpha;
- establish GitHub private vulnerability reporting as the security contact;
- refresh the branch board, merge queue, chat roster, and port matrix from live Phase 6 state.

## Invariants

- Pages content cannot call the project production-ready, audit-grade, or PHP-compatible;
- site deployment has no repository write permission;
- historical review and public-feedback records remain historical and are not rewritten as current state;
- implemented-local port rows do not imply PHP compatibility.

This gate makes no production, publication, signing, audit-grade, or PHP compatibility claim.
