# Hardening 06: Workflow Supplemental Authority

Status: accepted for implementation

## Goal

Restore deterministic Workflow waiver and amendment compilation and rehydration by consuming only the verified Protocol authority wrappers accepted in ADR 0019.

## Sources

1. `AGENTS.md`
2. `PLANS.md`
3. `docs/adr/0005-workflow-template-contract.md`
4. `docs/adr/0006-workflow-compiler-contract.md`
5. `docs/adr/0018-workflow-authority-hardening-sequence.md`
6. `docs/adr/0019-protocol-supplemental-authority-records.md`
7. `docs/gates/HARDENING-05-PROTOCOL-SUPPLEMENTAL-AUTHORITY-EVIDENCE.md`

## Dependency-Ordered Tasks

1. Workflow owner: replace raw compile-time amendment and notice inputs with verified waiver and amendment authority.
2. Workflow owner: require exact waiver authority membership for every waiver in the approved Protocol version and reject foreign, missing, duplicate, or extra wrappers.
3. Workflow owner: bind verified amendment authority to the exact compiled Protocol version and derive invalidation notices only from its immutable membership.
4. Workflow owner: remove the ADR 0018 temporary rejection only for inputs satisfying the verified contracts.
5. Workflow owner: extend definition rehydration to resolve and validate the same supplemental authority before accepting waiver bindings or invalidation entries.
6. Test owner: add positive, negative, determinism, immutability, and compile/rehydrate parity coverage.
7. Fixture owner: replace deferred-authority expectations with separate Hardening 06 deterministic replay recipes while preserving historical fixtures.
8. Gate owner: run focused, full, architecture, conformance, formatting, repository, scientific-invariant, and hosted CI verification.

## Required Cases

- valid verified waiver authority supplies the intended scientific input and reproduces its canonical digest;
- valid verified amendment authority produces an invalidation plan from immutable amendment notice membership;
- missing, extra, duplicate, foreign, stale, or raw waiver authority is rejected;
- wrong produced Protocol version, amendment identity, Protocol identity, or previous-version lineage is rejected;
- caller-supplied replacement invalidation notices remain impossible;
- rehydration resolves exact waiver/amendment authority and rejects missing or mismatched resolver output;
- compile and rehydrate reproduce the same Workflow identity and digest;
- verified Workflow outputs retain no mutable caller collection;
- unamended and no-waiver Workflow behavior remains unchanged.

## Allowed Paths

- `docs/gates/HARDENING-06-WORKFLOW-SUPPLEMENTAL-AUTHORITY.md`
- `docs/gates/HARDENING-06-WORKFLOW-SUPPLEMENTAL-AUTHORITY-EVIDENCE.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `src/NexusScholar.Workflow/Workflow.cs`
- `src/NexusScholar.Workflow/WorkflowRehydration.cs`
- `tests/NexusScholar.Core.Tests/WorkflowCompilerTests.cs`
- `tests/NexusScholar.Conformance.Tests/WorkflowFixtureTests.cs`
- `fixtures/conformance/workflow/`

## Excluded Paths

- Protocol authority contract changes
- deviation approval transitions
- persistence, API, CLI, UI, provider, plugin, AI, artifact storage, or workspace behavior
- existing fixture regeneration
- PHP or blueprint compatibility claims
- production dependencies

## Risks And Decisions

- The approved Protocol version is canonical waiver membership; supplied verified wrappers must match it exactly.
- An amended compile target must be the verified amendment's produced version. Previous-version authority is retained through the verified amendment wrapper.
- Invalidation notices are never accepted independently; Workflow consumes only `VerifiedProtocolAmendment.InvalidationNotices`.
- Definition rehydration must resolve supplemental authority independently rather than trusting persisted waiver bindings or invalidation entries.
- ADR 0018 already defines the reversal condition and ADR 0019 satisfies it. No new ADR is required.

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

- [x] Compile accepts only exact verified waiver/amendment authority.
- [x] Invalidation plans derive only from immutable verified amendment notices.
- [x] Rehydration independently resolves the same supplemental authority.
- [x] Raw and mismatched authority paths fail closed.
- [x] Compile/rehydrate identity and digest parity is permanent coverage.
- [x] Existing historical fixtures remain unchanged.
- [x] Only allowed paths changed.
- [x] Focused and full verification pass.
- [x] Scientific-invariant review is clear.
- [x] Evidence records behavior, commands, totals, risks, ADR impact, and compatibility impact.
