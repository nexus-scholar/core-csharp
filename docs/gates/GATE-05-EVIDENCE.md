# Gate 5 Evidence: Provenance Ledger

Status: local verification passed; hosted CI passed on branch dispatch.

## Scope

Gate 5 implements local provenance ledger behavior only.

Implemented conflict scope:

- `CF-004`: resolved for local provenance ledger behavior only.

Still out of scope:

- AI governance, AI task records, context manifest policy, provider behavior, bundle portability, artifact storage, persistence schema, API, Search, Deduplication, Screening, workflow execution, PHP compatibility, and blueprint conformance.

## Source Decisions

- `docs/adr/0008-provenance-ledger.md`
- `docs/gates/GATE-05.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

## Behavior Implemented

- `ResearchEvent` records immutable provenance event id, agent, activity, timestamp, subject, optional protocol binding, optional workflow binding, ordered inputs, ordered outputs, and event digest.
- Event digest uses `DigestScope.ProvenanceEvent`, schema id `nexus.provenance-event`, and schema version `1.0.0`.
- `ResearchEventFactory` validates required actor, required input digests, required output digests, and projection/cache/wiki/generated/local-path exclusions.
- `InMemoryProvenanceStore` is append-only, rejects duplicate event ids, preserves append order, and returns immutable snapshots.
- `NexusScholar.Provenance` remains Kernel-only inside the Nexus domain dependency graph.

## Fixture IDs

- `provenance-event-protocol-approved.json`
- `provenance-event-workflow-node-completed.json`
- `provenance-ledger-append-order.ndjson`
- `provenance-ledger-duplicate-reject.json`
- `provenance-invalid-missing-actor.json`
- `provenance-invalid-missing-required-input.json`
- `provenance-invalid-missing-required-output.json`
- `provenance-invalid-projection-as-canonical.json`

## Local Verification

- `dotnet restore NexusScholar.Core.slnx`: passed.
- `dotnet build NexusScholar.Core.slnx -c Release --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test NexusScholar.Core.slnx -c Release --no-build`: passed, 121 tests total.
  - `NexusScholar.Conformance.Tests`: 24 passed.
  - `NexusScholar.Architecture.Tests`: 10 passed.
  - `NexusScholar.Core.Tests`: 87 passed.
- `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`: passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1`: passed.

## Hosted CI

- `gate-01` workflow dispatch on `cdx/gate-5-provenance`: passed.
- Run: `https://github.com/nexus-scholar/core-csharp/actions/runs/28271812104`
- Matrix:
  - `verify (ubuntu-latest)`: passed.
  - `verify (windows-latest)`: passed.

## Explicit Claims Not Made

- no bundle parity
- no artifact storage implementation
- no AI governance parity
- no PHP compatibility
- no blueprint conformance
- no persistence schema
- no cloud sync
- no workflow execution engine
- no provider/network behavior
