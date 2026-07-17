# FE-09E: Provider Evidence Cache

Status: Complete; merged through PR #69 to protected `main`

Date: 2026-07-17

## Goal

Retain policy-admitted complete provider responses in a private, immutable,
verifiable local cache without making cache state scientific authority.

## Scope

- provider-neutral key, policy, and entry contracts;
- non-packable BCL filesystem store;
- OpenAlex `openalex.works` retained bodies;
- Semantic Scholar digest-only denial;
- expiry, replay verification, atomic promotion, and focused tests.

## Exit Criteria

- exact body, request, parser, policy, and timestamp bindings reproduce;
- expired entries verify but cannot be served fresh;
- incomplete, non-200, mutated, secret-bearing, or policy-denied writes fail;
- no Search domain filesystem dependency;
- full build, tests, format, architecture, and package gates pass.

## Nonclaims

No database, cloud cache, background refresh, CLI/UI, S2 body-retention right,
provider parity, legal certification, or production readiness.

## Completion Evidence

Implemented provider-neutral cache records and a non-packable filesystem store.
OpenAlex complete `200` responses can retain exact bytes; Semantic Scholar is
digest-only and Crossref runtime caching is denied. Entries are immutable and
content-addressed, provider drift appends evidence, the latest index is
rebuildable, and expired entries remain verifiable but stale.

Focused tests: 10 passed. Full solution: 1,011 passed, 2 opt-in live tests
skipped. Release build and format verification passed.
