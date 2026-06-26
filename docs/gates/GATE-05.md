# Gate 5: Provenance Ledger

Status: implemented for local provenance ledger scope on `cdx/gate-5-provenance`.

## Goal

Implement local append-only scientific provenance events that record actor, activity, protocol and workflow binding, input and output digest references, event digest, ordering, and in-memory store behavior.

Gate 5 records reconstructability evidence. It does not implement bundles, artifact storage, AI governance, persistence, API, cloud sync, Search, Deduplication, Screening, workflow execution, or PHP ports.

## Source Inputs

1. `AGENTS.md`
2. `docs/scientific-invariants/PRODUCT-LAWS.md`
3. `docs/adr/0002-canonical-json-and-digests.md`
4. `docs/adr/0008-provenance-ledger.md`
5. `docs/port/OPEN-CONFLICTS.md`
6. `docs/port/GOLDEN-FIXTURE-PLAN.md`
7. Current `src/NexusScholar.Provenance` scaffold

## Decision Summary

- Provenance events carry immutable event id, agent, activity, timestamp, subject/entity refs, optional protocol binding, optional workflow binding, input refs, output refs, and event digest.
- Event digest uses `DigestEnvelope(DigestScope.ProvenanceEvent, "nexus.provenance-event", "1.0.0", content)`.
- In-memory ledger behavior is append-only, duplicate rejecting, ordered, and snapshot based.
- Projection/cache/wiki/generated/local-path entities are not canonical provenance references.
- `NexusScholar.Provenance` remains Kernel-only.

## Conflict Status

- `CF-004`: resolved only for local provenance ledger behavior by `ADR 0008` and `NexusScholar.Provenance`.
- AI governance, context manifests, evidence policy, provider behavior, and AI task records remain unresolved for Gate 11.

## Allowed Paths

- `docs/adr/0008-provenance-ledger.md`
- `docs/gates/GATE-05.md`
- `docs/gates/GATE-05-EVIDENCE.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `src/NexusScholar.Provenance/`
- `tests/NexusScholar.Core.Tests/`
- `tests/NexusScholar.Architecture.Tests/`
- `tests/NexusScholar.Conformance.Tests/`
- `fixtures/conformance/provenance/`

## Excluded Paths And Claims

- No edits to `src/NexusScholar.Protocol/` or `src/NexusScholar.Workflow/`.
- No persistence, EF Core, API, UI, cloud sync, bundle implementation, AI governance implementation, provider/network behavior, Search, Deduplication, Screening, PHP ports, PHP-generated fixtures, PHP compatibility claim, or blueprint conformance claim.

## Required Fixtures

- `provenance-event-protocol-approved.json`
- `provenance-event-workflow-node-completed.json`
- `provenance-ledger-append-order.ndjson`
- `provenance-ledger-duplicate-reject.json`
- `provenance-invalid-missing-actor.json`
- `provenance-invalid-missing-required-input.json`
- `provenance-invalid-missing-required-output.json`
- `provenance-invalid-projection-as-canonical.json`

## Exit Checklist

- Event digest uses `provenance-event`, `nexus.provenance-event`, `1.0.0`.
- Protocol binding preserves protocol id, version id, version number, and protocol-content digest.
- Workflow binding preserves workflow id, workflow digest, and workflow node id when supplied.
- Store rejects duplicate event ids and preserves append order.
- Store and event collections are immutable snapshots.
- Event content cannot be mutated after append.
- Projection/cache/wiki/generated/local-path content is excluded from canonical provenance.
- Architecture tests confirm `NexusScholar.Provenance` remains Kernel-only with no provider, persistence, UI, API, bundle, AI, Protocol, or Workflow dependency.
- Local and hosted verification pass.

## Explicit Non-Claims

- no bundle parity
- no artifact storage implementation
- no AI governance parity
- no PHP compatibility
- no blueprint conformance
- no persistence schema
- no cloud sync
- no workflow execution engine
- no provider/network behavior
