# Hardening 07: Provenance Append Authority

Status: accepted for implementation

## Goal

Make the Provenance append boundary validate and reproduce every event before accepting it, so caller-fabricated event state cannot enter the append-only ledger.

## Sources

1. `AGENTS.md`
2. `PLANS.md`
3. `docs/adr/0002-canonical-json-and-digests.md`
4. `docs/adr/0008-provenance-ledger.md`
5. `docs/gates/GATE-05.md`
6. `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`

## Dependency-Ordered Tasks

1. Provenance owner: make raw `ResearchEvent` construction internal and centralize event validation.
2. Provenance owner: validate event, agent, activity, subject, inputs, outputs, protocol binding, and workflow binding at construction and append.
3. Provenance owner: recompute the provenance-event digest and reject stale or forged values at append.
4. Provenance owner: make in-memory append/read concurrency-safe while preserving append order and duplicate-ID rejection.
5. Test owner: add forged-event, unsupported-agent, invalid-binding, default-ID, canonical-entity, and concurrent append coverage.
6. Fixture owner: add separate deterministic Hardening 07 replay recipes without modifying historical Gate 5 fixtures.
7. Gate owner: run focused, full, architecture, conformance, formatting, repository, scientific-invariant, and hosted CI verification.

## Required Cases

- valid factory-created events append and reproduce their digest;
- forged or stale event digest is rejected;
- default event ID, blank agent ID, unsupported agent kind, non-UTC timestamp, or malformed activity is rejected;
- invalid subject/input/output identity, entity kind, or required digest is rejected at append;
- invalid Protocol or Workflow binding identity/digest is rejected;
- concurrent distinct-ID appends preserve every event exactly once;
- concurrent same-ID appends accept exactly one event and reject all duplicates;
- stored and returned records remain immutable snapshots;
- public callers cannot instantiate `ResearchEvent` directly.

## Allowed Paths

- `docs/gates/HARDENING-07-PROVENANCE-APPEND-AUTHORITY.md`
- `docs/gates/HARDENING-07-PROVENANCE-APPEND-AUTHORITY-EVIDENCE.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `src/NexusScholar.Provenance/`
- `tests/NexusScholar.Core.Tests/ProvenanceTests.cs`
- `tests/NexusScholar.Conformance.Tests/ProvenanceFixtureTests.cs`
- `fixtures/conformance/provenance/`

## Excluded Paths

- Protocol or Workflow dependencies
- persistence, database, API, CLI, UI, cloud, bundle import, provider, plugin, AI, Search, Deduplication, Screening, or Full Text behavior
- existing fixture regeneration
- PHP or blueprint compatibility claims
- production dependencies

## Risks And Decisions

- Append is the durable trust boundary even when the event originated from the local factory; it must rerun all invariants.
- Provenance remains Kernel-only. Protocol and Workflow bindings are validated structurally by IDs and digests under ADR 0008, not resolved through outward modules in this gate.
- Agent kinds are the closed ADR 0008 set: `human`, `automation`, `plugin`, `system`, and `import`.
- A lock is sufficient for the in-memory implementation and does not imply persistence transaction semantics.
- No source conflict or new ADR is required.

## Verification

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter ProvenanceTests
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter ProvenanceFixtureTests
dotnet test tests/NexusScholar.Architecture.Tests/NexusScholar.Architecture.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
git diff --check
```

## Exit Checklist

- [x] Raw event construction is non-public.
- [x] Append reruns all event invariants and digest reproduction.
- [x] Agent and binding values are structurally valid.
- [x] Concurrent append is duplicate-safe and lossless.
- [x] Immutable snapshot behavior remains intact.
- [x] Existing historical fixtures remain unchanged.
- [x] Only allowed paths changed.
- [x] Focused and full verification pass.
- [x] Scientific-invariant review is clear.
- [x] Evidence records behavior, commands, totals, risks, ADR impact, and compatibility impact.
