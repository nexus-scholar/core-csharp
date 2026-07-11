# Hardening 04: Workflow authority binding and rehydration evidence

Status: local verification complete on 2026-07-11; hosted CI pending publication.

## Behavior Implemented

- Authoritative compilation requires `VerifiedProtocolVersion`; raw or recast Protocol state fails closed.
- `ProtocolDraft.ApproveCandidateVerified` retains verified output from a validated live approval transition without exposing a public constructor.
- `WorkflowDefinition` is internally constructed, has no public copy/fabrication path, and defensively copies every authoritative collection.
- `UnverifiedWorkflowDefinition`, `IWorkflowAuthorityResolver`, `WorkflowRehydrator`, and `VerifiedWorkflowDefinition` establish the persisted authority boundary.
- Rehydration resolves the exact approved Protocol version and schema-closed Workflow template, validates template graph/reference closure, checks compiled projections, recomputes workflow identity and digest, and rejects tampering.
- Scientific input bindings identify `ProtocolDecision.DecisionId` and digest the complete canonical decision record.
- Execution compile parameters remain explicitly non-authoritative value inputs under ADR 0018.
- Waiver, amendment, and invalidation compilation fails closed until verified Protocol authority records exist.
- The hardened Workflow definition schema is `nexus.workflow-definition:1.1.0`; historical 1.0.0 fixtures remain unchanged migration evidence.

## Tests And Fixtures

- Focused Workflow domain tests: 24 passed.
- Focused Workflow conformance tests: 11 passed.
- Architecture tests: 25 passed.
- Full solution: 465 passed.
- Seven Hardening 04 replay recipes cover verified compile/rehydrate, raw Protocol rejection, scalar tamper, duplicate node, wrong template resolution, deferred waiver authority, and deferred amendment authority.
- Recipe cases drive the real compiler and rehydrator; negative cases assert actual stable error categories.
- Permanent tests cover template schema/closure, complete decision binding, resolver mismatch, collection-family tampering, nested collection mutation, and non-castable immutable output.
- Existing Gate 4 1.0.0 fixture files and digests were not edited.

## Commands Run

```powershell
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter WorkflowCompilerTests
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter WorkflowFixtureTests
dotnet test tests/NexusScholar.Architecture.Tests/NexusScholar.Architecture.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
git diff --check
```

All commands passed. Build and format reported no warnings or errors. Repository verification passed doctor, deterministic sample, bundle, CLI, and no-network checks.

## Scientific Invariants

- An approved Protocol content digest alone is insufficient; Workflow requires the explicit verified Protocol authority object.
- Workflow rehydration cannot substitute a different Protocol, template, graph, input source, policy requirement, capability, or artifact declaration while retaining authority.
- Decision actor, timestamp, rationale, supersession, and proposal provenance are included through the complete decision-record source digest.
- Unsupported waiver/amendment approval ID counts and caller-supplied invalidation notices no longer enter authoritative compilation.
- Compiled Workflow state remains immutable after construction and rehydration.

The required scientific reviewer completed an initial audit and identified template closure, recipe replay, compile-parameter classification, tamper coverage, and public test-API issues. Every finding was addressed with code, ADR wording, and permanent tests. A second reviewer invocation was attempted but could not run because delegated-agent usage quota was exhausted. A local follow-up audit, focused suites, architecture suite, full suite, formatting, and repository verification found no remaining blocker.

## Configuration Impact

The user explicitly requested that the existing `.codex/config.toml` change be committed with this slice. The file now retains only repository agent concurrency settings and removes repository-pinned model, reasoning, approval, sandbox, and document-size defaults. This delegates those execution settings to the active Codex host configuration. It does not change Core runtime or scientific behavior.

## Remaining Risks

- Waiver, amendment, deviation, and invalidation authority require a later Protocol hardening gate before Workflow can safely restore those compile paths.
- Template resolution is an application-owned trust boundary; hosts must resolve only durable local template records whose schema, graph closure, and digest verify.
- Execution compile parameters are values, not scientific evidence or historical authority records.
- Hosted CI evidence remains pending until publication.

## ADR And Compatibility Impact

- ADR impact: ADR 0018 records fail-closed sequencing, compile-parameter classification, and the Workflow definition 1.1.0 migration.
- PHP compatibility impact: none claimed or tested.
- Blueprint compatibility impact: none claimed.
