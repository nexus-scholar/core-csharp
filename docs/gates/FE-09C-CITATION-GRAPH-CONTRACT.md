# FE-09C: Citation Graph Contract

Status: Complete; merged through PR #69 to protected `main`

Date: 2026-07-17

## Goal

Create immutable evidence-backed direct-citation snapshots and deterministic
basic metrics from stable scholarly identities.

## Scope

- new packable `NexusScholar.Network` domain owner;
- resolved nodes and explicit unresolved cited targets;
- evidence-backed directed edges;
- corpus-bound canonical snapshots;
- count and degree metrics;
- deterministic focused tests.

## Exit Criteria

- duplicate, unsupported, stale, or evidence-free structures fail closed;
- snapshots and metrics reproduce independent of persistence;
- Network has no provider, HTTP, storage, UI, or graph-library dependency;
- H29 compatibility classifications remain unchanged;
- full build, tests, format, architecture, and package gates pass.

## Nonclaims

No live citation provider, snowballing, shortest path, centrality, export,
dissemination, PHP parity, scale, or impact interpretation.

## Completion Evidence

Implemented packable `NexusScholar.Network` with stable resolved identities,
explicit unresolved targets, evidence-backed directed edges, immutable
corpus-bound direct-citation snapshots, and deterministic count/degree metrics.
H29's 14 intentional PHP classifications remain unchanged.

Focused tests: 7 passed. Full solution: 1,011 passed, 2 opt-in live tests
skipped. Release build and format verification passed.
