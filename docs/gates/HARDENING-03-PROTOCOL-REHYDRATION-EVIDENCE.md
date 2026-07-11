# Hardening 03: Protocol authority-safe rehydration evidence

Status: local verification complete on 2026-07-11; hosted CI pending publication.

## Behavior Implemented

- Direct `ProtocolVersion` construction and approval promotion are no longer public.
- Persisted approval and version claims use explicit unverified records.
- `IProtocolAuthorityResolver` resolves the template-selected policy, human actors, and referenced verified approvals.
- Approval rehydration rebuilds the approval-record envelope and rejects tampered digests, actors, targets, policies, or content bindings.
- Version rehydration rebuilds the protocol-content envelope, requires an exact policy snapshot, resolves the exact satisfying approval set, and derives `ApprovedAt` from the latest satisfying approval.
- Approved content rejects missing or duplicate scientific identities, blocking unresolved decisions, unidentified actors, policy downgrades, excess authority, and unlinked supersession.
- Verified collections and nested canonical JSON values are defensively copied, frozen, and exposed through non-castable read-only collections.

## Tests And Fixtures

- Focused Protocol domain tests: 45 passed.
- Focused Protocol conformance tests: 9 passed.
- Architecture tests: 25 passed.
- Full solution: 452 passed.
- Added 13 deterministic Hardening 03 replay recipes covering valid single and dual approval plus tamper, actor, target, policy, missing, extra, duplicate, downgrade, and blocking-unresolved cases.
- Recipe `inputDigest` and `outputDigest` explicitly mean SHA-256 of the compact JSON `case` object and are recomputed by conformance tests.
- Existing golden fixture outputs were not modified.

## Commands Run

```powershell
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter ProtocolTests
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter ProtocolFixtureTests
dotnet test tests/NexusScholar.Architecture.Tests/NexusScholar.Architecture.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
git diff --check
```

All commands passed. Build and format reported no warnings or errors. The repository verification script also passed its doctor, deterministic sample, bundle, and no-network checks.

## Scientific Invariants

- Approval authority remains human and resolver-backed.
- Approval records bind one exact protocol version, content digest, policy, actor, and timestamp.
- An approved version cannot silently replace its historical approval timestamp.
- Policy minimums, distinct actors, required roles, and exact approval membership are enforced during rehydration.
- Protocol content remains immutable and digest-reproducible after verification.
- A superseded version remains linked to its successor.

The required `scientific_invariant_reviewer` audit initially found excess approval, policy downgrade, unresolved actor, mutable collection, supersession, timestamp, and fixture-evidence gaps. Those findings were corrected. The final audit reported the gate clear with no runtime authority or provenance blockers.

## Remaining Risks

- This gate validates Protocol approval/version rehydration but does not complete waiver, amendment, or deviation authority transitions.
- The resolver is an application-owned trust boundary; persistence and host adapters must resolve template policy and actor identity from durable authoritative records.
- Replay recipes exercise deterministic boundary scenarios. They are not claimed to be persistence, API, blueprint, or cross-language serialization fixtures.
- Hosted CI evidence remains pending until the branch is published.

## ADR And Compatibility Impact

- ADR impact: none. The implementation applies accepted ADR 0002, ADR 0003, ADR 0004, and ADR 0017 semantics.
- PHP compatibility impact: none claimed or tested.
- Blueprint compatibility impact: none claimed.
