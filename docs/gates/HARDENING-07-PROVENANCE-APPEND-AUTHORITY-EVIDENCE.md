# Hardening 07 Evidence: Provenance Append Authority

Status: complete locally

## Behavior Implemented

- `ResearchEvent` raw construction is internal; public callers use the validated factory.
- A centralized validator checks event identity, UTC timestamp, agent identity and kind, canonical entities, required input/output digests, Protocol and Workflow bindings, and event digest reproduction.
- The factory validates normalized event material before computing its provenance-event digest.
- The in-memory store reruns full validation and digest reproduction inside the append lock before duplicate detection and storage.
- Append and snapshot reads are synchronized, preserving immutable copies and preventing concurrent loss or duplicate acceptance.

## Invariants Enforced

- Default event IDs, blank agents, unsupported agent kinds, non-UTC timestamps, invalid entity digests, and noncanonical entity kinds cannot enter the ledger.
- Protocol bindings require a positive version number and valid content digest.
- Workflow bindings require a valid Workflow digest and a nonblank node ID when present.
- Required inputs and outputs retain their digest requirements at append, not only factory creation.
- A forged or stale event digest is rejected before storage.
- Concurrent same-ID appends accept exactly one event; concurrent distinct-ID appends are lossless.

## Tests And Recipes

- Provenance focused tests: 23 passed.
- Provenance conformance focused tests: 6 passed.
- Architecture tests: 25 passed.
- Full solution: 503 passed, 0 failed, 0 skipped.
- Five deterministic Hardening 07 recipes cover valid append, forged digest, unsupported agent, invalid binding, and concurrent duplicate append.
- Existing historical Gate 5 fixtures were not edited or regenerated.

## Commands Run

```powershell
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter ProvenanceTests
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter ProvenanceFixtureTests
dotnet test tests/NexusScholar.Architecture.Tests/NexusScholar.Architecture.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
git diff --check
```

All commands passed. The solution build reported zero warnings and zero errors. Repository verification passed the doctor and deterministic no-network local demo.

## Scientific-Invariant Review

No blocking, important, or minor findings remain. The final diff preserves append-only history, deterministic event identity, canonical entity boundaries, actor attribution, immutable snapshots, and fail-closed append validation.

## Files Changed

- `docs/gates/HARDENING-07-PROVENANCE-APPEND-AUTHORITY.md`
- `docs/gates/HARDENING-07-PROVENANCE-APPEND-AUTHORITY-EVIDENCE.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `src/NexusScholar.Provenance/ProvenanceEvent.cs`
- `src/NexusScholar.Provenance/ProvenanceStore.cs`
- `src/NexusScholar.Provenance/ResearchEventFactory.cs`
- `tests/NexusScholar.Core.Tests/ProvenanceTests.cs`
- `tests/NexusScholar.Conformance.Tests/ProvenanceFixtureTests.cs`
- five new files under `fixtures/conformance/provenance/`

## Remaining Risk And Next Dependency

Provenance bindings remain structural Kernel-only references under ADR 0008. Bundle verification must next replace loose known-digest dictionaries with resolved Protocol, Workflow, template, and Provenance authority records; persistence and atomic import remain later gates.

## ADR Impact

This gate hardens the existing ADR 0008 append boundary. No ADR was added, changed, or superseded.

## Compatibility Impact

No PHP or blueprint compatibility claim is made. The new recipes are local hardening contracts only.
