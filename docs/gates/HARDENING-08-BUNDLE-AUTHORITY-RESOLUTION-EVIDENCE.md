# Hardening 08 Evidence: Bundle Authority Resolution

Status: complete locally

## Implemented

- Added an application-owned resolver for approved Protocol versions, compiler/rehydrator-owned Workflow definitions, and validated Provenance events.
- Bundle verification compares complete Protocol identity/version/status/digest, Workflow/template/Protocol bindings, and Provenance event digest/activity/time/actor.
- Caller-owned known-digest dictionaries no longer establish authority.
- Artifact schemas must be declared by the manifest required-schema set.
- Artifact provenance event IDs and digests must be supplied together and resolve exactly.
- The deterministic CLI sample now composes its real Protocol, Workflow, and Provenance records into Bundle verification.

## Verification

- Bundle focused tests: 20 passed.
- Bundle conformance tests: 4 passed.
- Architecture tests: 25 passed.
- Full solution: 505 passed, 0 failed, 0 skipped.
- Solution build, formatting, `scripts/verify.ps1`, CLI sample, and `git diff --check` passed.

## Scientific Review

No findings remain. Bundle fields remain portable claims until matched against resolved authority; no import, persistence, or compatibility claim is introduced.

## Remaining Risk

Archive parsing, staged atomic import, all-record destructive overwrite checks, and unification with `NexusScholar.Artifacts` remain later work outside Phase 2 authority rehydration.

## ADR Impact

ADR 0020 resolves the former architecture conflict by treating Bundles as an outer verifier that may depend inward on authority-owning modules. Authority modules remain independent of Bundles.

## Compatibility Impact

No PHP or blueprint compatibility claim is made. Historical fixtures were not regenerated.
