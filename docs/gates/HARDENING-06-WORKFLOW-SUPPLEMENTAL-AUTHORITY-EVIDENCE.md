# Hardening 06 Evidence: Workflow Supplemental Authority

Status: complete locally

## Behavior Implemented

- Workflow compilation now accepts Protocol waivers only through an exact set of `VerifiedProtocolWaiver` wrappers matching the approved Protocol version.
- Amended Workflow compilation requires one `VerifiedProtocolAmendment` whose produced verified Protocol version is the exact compile target.
- Invalidation plans derive only from immutable notice membership on the verified amendment. The compile contract has no caller-supplied invalidation-notice list.
- Amendment source bindings use the canonical Protocol-owned amendment digest instead of a duplicate Workflow serializer.
- Workflow rehydration independently resolves verified waiver and amendment authority, reproduces waiver bindings and invalidation entries, and preserves compile/rehydrate identity and digest parity.

## Invariants Enforced

- Raw waiver, amendment, approval-ID, and replacement-notice data cannot establish Workflow authority.
- Every approved Protocol waiver requires exactly one matching verified wrapper; missing, extra, duplicate, or foreign wrappers fail closed.
- Unamended Protocol versions reject amendment authority and invalidation plans.
- Amended Protocol versions require exact amendment identity, produced-version identity, and produced content digest.
- Persisted invalidation entries must match amendment identity, produced version, previous digest, amendment digest, notice digest, affected requirement, artifact, node, and template-required action.
- Model or automation output receives no approval authority.

## Tests And Recipes

- Workflow focused tests: 29 passed.
- Workflow conformance focused tests: 12 passed.
- Architecture tests: 25 passed.
- Full solution: 490 passed, 0 failed, 0 skipped.
- Five deterministic Hardening 06 recipes cover verified waiver compile, verified amendment compile, rehydration parity, replacement-notice rejection, and missing waiver authority.
- Existing historical Workflow fixtures were not edited or regenerated.

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

All commands passed. The solution build reported zero warnings and zero errors. Repository verification passed the doctor and deterministic no-network local demo.

## Scientific-Invariant Review

No blocking, important, or minor findings remain. The final diff preserves Protocol-owned human authority, exact approved-state binding, canonical digest provenance, immutable invalidation membership, deterministic replay, and fail-closed rehydration.

## Files Changed

- `docs/gates/HARDENING-06-WORKFLOW-SUPPLEMENTAL-AUTHORITY.md`
- `docs/gates/HARDENING-06-WORKFLOW-SUPPLEMENTAL-AUTHORITY-EVIDENCE.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `src/NexusScholar.Workflow/Workflow.cs`
- `src/NexusScholar.Workflow/WorkflowRehydration.cs`
- `tests/NexusScholar.Core.Tests/WorkflowCompilerTests.cs`
- `tests/NexusScholar.Conformance.Tests/WorkflowFixtureTests.cs`
- five new files under `fixtures/conformance/workflow/`

## Remaining Risk And Next Dependency

The accepted Protocol and Workflow authority sequence is complete for versions, waivers, and amendments. Protocol deviations and authority-bearing transitions in later modules remain separate hardening work and were not widened into this gate.

## ADR Impact

This gate satisfies the ADR 0018 reversal conditions using the verified authority records accepted by ADR 0019. No ADR was added, changed, or superseded.

## Compatibility Impact

No PHP or blueprint compatibility claim is made. The new recipes are local hardening contracts only.
