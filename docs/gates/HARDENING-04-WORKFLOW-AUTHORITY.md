# Hardening 04: Workflow authority binding and rehydration

Status: accepted for implementation

## Goal

Require verified Protocol authority for Workflow compilation and ensure a compiled or persisted Workflow definition becomes authoritative only through deterministic construction, resolver-backed identity checks, digest recomputation, and immutable verified output.

## Sources

1. `AGENTS.md`
2. `docs/adr/0002-canonical-json-and-digests.md`
3. `docs/adr/0005-workflow-template-contract.md`
4. `docs/adr/0006-workflow-compiler-semantics.md`
5. `docs/adr/0017-canonical-json-profile-correction.md`
6. `docs/adr/0018-workflow-authority-hardening-sequence.md`
7. `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`
8. Hardening 03 verified Protocol boundary

## Dependency-Ordered Tasks

1. Protocol test-support owner: provide a validated way for compiler tests and hosts to retain verified output from an accepted approval transition without exposing a public fabrication constructor.
2. Workflow owner: require `VerifiedProtocolVersion` for authoritative compilation and reject raw authority.
3. Workflow owner: reject waiver/amendment/invalidation compilation until verified Protocol authority types exist, per ADR 0018.
4. Workflow owner: bind complete `ProtocolDecision` canonical records in resolved scientific inputs.
5. Workflow owner: replace the public positional Workflow definition with internally constructed, deeply immutable state.
6. Workflow owner: define unverified Workflow definition input, authority resolvers, a rehydrator, and explicit verified result.
7. Workflow owner: resolve exact Protocol/template identities, validate graph closure, recompute workflow ID and digest, and reject missing, extra, duplicate, reordered-semantic, or tampered state.
8. Test/fixture owner: add compile, rehydration, fail-closed authority, and mutation regressions plus deterministic replay recipes.
9. Gate owner: include the user-authorized `.codex/config.toml` cleanup in this commit and record it separately in evidence.
10. Gate owner: run focused, full, architecture, conformance, formatting, repository, scientific-invariant, and hosted CI verification.

## Required Cases

- authoritative compile accepts an exact `VerifiedProtocolVersion` and rejects raw or mismatched Protocol state;
- a protocol decision binding uses `DecisionId` and the digest of the complete canonical decision record;
- waiver/amendment/invalidation authority fails closed under ADR 0018;
- WorkflowDefinition has no public constructor or copy path capable of retaining a stale digest;
- valid persisted definition rehydrates only against its exact verified Protocol version and verified template;
- workflow ID and workflow digest are recomputed and must match;
- scalar, nested collection, graph, binding, capability, approval, artifact, and invalidation tampering is rejected;
- duplicate node, edge, binding, requirement, capability, artifact, and invalidation identities are rejected;
- verified output and nested collections cannot be mutated by retained references or casts;
- existing unamended deterministic compile behavior and fixture digests remain stable.

## Allowed Paths

- `.codex/config.toml` (explicitly authorized by the user for this slice)
- `docs/adr/0018-workflow-authority-hardening-sequence.md`
- `docs/gates/HARDENING-04-WORKFLOW-AUTHORITY.md`
- `docs/gates/HARDENING-04-WORKFLOW-AUTHORITY-EVIDENCE.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `src/NexusScholar.Protocol/` only for verified transition output needed by Workflow
- `src/NexusScholar.Workflow/`
- focused CLI/sample call sites required by the verified compile signature
- `tests/NexusScholar.Core.Tests/ProtocolTests.cs`
- `tests/NexusScholar.Core.Tests/WorkflowCompilerTests.cs`
- `tests/NexusScholar.Conformance.Tests/WorkflowFixtureTests.cs`
- `tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs`
- `fixtures/conformance/workflow/`

## Excluded Paths

- implementing waiver, amendment, deviation, or invalidation authority records
- workflow execution, provenance append, artifact storage, bundles, persistence, API, UI, provider, plugin, or AI runtime behavior
- existing golden-output regeneration
- PHP or blueprint compatibility claims
- production dependencies

## Risks And Decisions

- ADR 0018 resolves the source conflict by failing closed where accepted authority proof is unavailable.
- Existing tests that used raw approved Protocol records are not authority evidence and must move through the verified boundary.
- Rehydration validates a Workflow definition contract; it does not define a persistence or API schema.
- Template resolution must bind ID, version, digest, schema identity, and canonical content.
- No Workflow-owned generalized approval record is introduced because approval authority belongs to Protocol.

## Verification

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter WorkflowCompilerTests
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter WorkflowFixtureTests
dotnet test tests/NexusScholar.Architecture.Tests/NexusScholar.Architecture.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
git diff --check
```

## Exit Checklist

- [x] Raw Protocol state cannot enter authoritative Workflow compilation.
- [x] Workflow definitions cannot be fabricated or mutated with stale authority fields.
- [x] Decision source bindings cover the complete decision record.
- [x] Unsupported waiver/amendment authority fails closed.
- [x] Every required positive and negative case has a permanent test or recipe.
- [x] Existing authoritative fixture outputs remain unchanged.
- [x] Only allowed paths changed.
- [x] Focused and full verification commands pass.
- [x] Scientific-invariant review findings are addressed; the second reviewer retry was unavailable because delegated-agent quota was exhausted.
- [x] Evidence records behavior, commands, totals, risks, ADR impact, config impact, and compatibility impact.

Completion evidence: `docs/gates/HARDENING-04-WORKFLOW-AUTHORITY-EVIDENCE.md`.
