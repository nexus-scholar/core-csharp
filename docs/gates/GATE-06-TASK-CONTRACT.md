# Gate 6 Task Contract: Bundle and Artifact Planning

Mode: planning only until ADR 0009 is reviewed and accepted.

Branch:

```text
cdx/gate-6-bundle-planning
```

## Objective

Prepare Gate 6 for implementation by freezing the local portable bundle and artifact contract. Do not change `src/`, `tests/`, or `fixtures/` in this planning branch.

## Read First

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0003-protocol-record-contract.md`
- `docs/adr/0004-protocol-approval-semantics.md`
- `docs/adr/0005-workflow-template-contract.md`
- `docs/adr/0006-workflow-compiler-semantics.md`
- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0008-provenance-ledger.md`
- `docs/gates/GATE-06.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `docs/discovery/BLUEPRINT-AUDIT.md`
- `docs/scientific-invariants/PRODUCT-LAWS.md`
- `src/NexusScholar.Artifacts/**`
- `src/NexusScholar.Bundles/**`

## Allowed Paths

- `docs/adr/0009-portable-bundle-and-artifact-contract.md`
- `docs/gates/GATE-06.md`
- `docs/gates/GATE-06-TASK-CONTRACT.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

## Forbidden Paths

- `src/**`
- `tests/**`
- `fixtures/**`
- `specs/**`
- PHP reference repo

## Required ADR 0009 Decisions

ADR 0009 must define:

- bundle manifest identity;
- bundle manifest schema id and version;
- bundle manifest digest input and `bundle-manifest` scope;
- artifact entry shape;
- artifact logical path rules;
- raw artifact byte digest rules;
- manifest checksum rules;
- required versus optional bundle sections;
- protocol approved-version binding;
- workflow binding;
- provenance event binding;
- shared identity/corpus membership treatment;
- unresolved no-id candidate treatment;
- local snapshot equality rule;
- verification result shape;
- tamper report shape;
- import safety and destructive overwrite policy;
- deterministic ordering;
- what is outside bundle digest;
- future work and explicit non-claims.

## Fixture IDs To Plan

Positive:

- `artifact-raw-byte-digest.json`
- `artifact-manifest-entry.json`
- `bundle-manifest-minimal.json`
- `bundle-manifest-with-protocol-workflow-provenance.json`
- `bundle-manifest-digest-stable.json`
- `bundle-roundtrip-local-equivalence.json`

Negative:

- `artifact-invalid-digest.json`
- `artifact-negative-size.json`
- `artifact-forbidden-path-absolute.json`
- `artifact-forbidden-path-traversal.json`
- `bundle-duplicate-artifact-path.json`
- `bundle-missing-artifact.json`
- `bundle-checksum-mismatch.json`
- `bundle-unsupported-required-schema.json`
- `bundle-stale-manifest-digest.json`
- `bundle-destructive-overwrite-reject.json`

## Non-Claims

Do not claim:

- blueprint conformance;
- PHP compatibility;
- PHP-generated fixtures;
- provider behavior;
- persistence, API, UI, or cloud sync;
- Search, Deduplication, Screening, Citation Network, Full Text, or Reporting ports;
- workflow execution;
- plugin runtime;
- AI governance parity.

## Finish With

- ADR summary;
- `CF-002` status;
- `CF-014` status;
- fixture consequences;
- implementation readiness: yes/no;
- explicit claims not made.
