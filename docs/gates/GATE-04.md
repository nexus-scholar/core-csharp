# Gate 4: Workflow Compiler

Status: planning only; implementation blocked until the Gate 4 contract is frozen.

## Goal

Implement deterministic workflow compilation from approved protocol versions, method templates, parameters, approval gates, waivers, and invalidation requirements. Gate 4 must turn approved scientific conduct intent into an auditable workflow graph without treating drafts, suggestions, projections, or automation outputs as authority.

## Source Inputs

1. `AGENTS.md`
2. `PLANS.md`
3. `docs/scientific-invariants/PRODUCT-LAWS.md`
4. `docs/adr/0002-canonical-json-and-digests.md`
5. `docs/adr/0003-protocol-record-contract.md`
6. `docs/adr/0004-protocol-approval-semantics.md`
7. `docs/port/OPEN-CONFLICTS.md`
8. `docs/port/GOLDEN-FIXTURE-PLAN.md`
9. Current `src/NexusScholar.Workflow` scaffold

## Blocking Conflicts

- `CF-003`: workflow compiler semantics are still scaffold-only.
- `CF-006`: schema closure is missing for referenced templates and workflow schema ids.
- `CF-007`: `hybrid` mode semantics are ambiguous because workflow specs require AI task and approval behavior while templates may only declare plugin capability.

Do not implement Gate 4 behavior until these conflicts are resolved by an ADR or explicit gate decision.

## Dependency-Ordered Tasks

1. Workflow owner: freeze workflow record shapes for templates, parameters, nodes, edges, gates, capability requirements, and invalidation plans.
2. Spec owner: close the schema-id gap or explicitly mark missing schemas as non-authoritative for Gate 4.
3. Governance owner: decide `hybrid` node semantics and where human approval gates sit relative to automation and plugin capability execution.
4. Fixture owner: generate Gate 4 conformance fixtures with fixed ids, fixed clocks, deterministic ordering, and digest metadata.
5. Workflow owner: implement compiler validation for duplicate ids, unknown dependencies, cycles, approval-gate references, waiver handling, and invalidation planning.
6. Architecture owner: keep workflow code inward-facing and free of persistence, UI, provider SDK, AI provider, and bundle dependencies.

## Required Fixtures

Positive fixtures:

- `workflow-compile-rapid-review.json`
- `workflow-compile-ai-audit.json`

Negative fixtures:

- duplicate node id
- missing dependency
- dependency cycle
- waivable node without waiver policy
- unknown approval role
- draft protocol used as workflow authority
- unresolved blocking protocol decision used for compilation
- automation-only approval gate
- hybrid node without frozen AI/plugin/approval semantics

## Allowed Paths

- `src/NexusScholar.Workflow/`
- `tests/NexusScholar.Core.Tests/` for focused workflow domain tests
- `tests/NexusScholar.Conformance.Tests/` for fixture replay
- `tests/NexusScholar.Architecture.Tests/` for dependency rules
- `fixtures/conformance/workflow/`
- `docs/gates/GATE-04.md`
- a new ADR only if needed to close `CF-003`, `CF-006`, or `CF-007`

## Excluded Paths And Claims

- No persistence, API, CLI, UI, bundle export, provenance ledger, AI execution, plugin host, PHP compatibility, or blueprint conformance claims.
- Do not change protocol lifecycle semantics except to consume approved protocol records.
- Do not generate PHP differential fixtures for Gate 4.

## Verification

Required commands:

1. `dotnet restore NexusScholar.Core.slnx`
2. `dotnet build NexusScholar.Core.slnx -c Release --no-restore`
3. `dotnet test NexusScholar.Core.slnx -c Release --no-build`
4. `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`
5. `powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1`

## Exit Checklist

- Gate 4 ADR or gate decision closes `CF-003`, `CF-006`, and `CF-007` for local scope.
- Workflow compilation starts only from approved protocol versions.
- Drafts, suggestions, projections, and automation outputs cannot authorize workflow conduct.
- Workflow graph output is deterministic and fixture replayable.
- Waivers and invalidation notices affect graph planning without mutating approved protocol digests.
- Tests cover required positive and negative fixtures.
- Architecture tests confirm workflow has no outward dependency leaks.
- Non-claims remain explicit.
