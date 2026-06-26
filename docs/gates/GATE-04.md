# Gate 4: Workflow Compiler

Status: planning decisions accepted; implementation not started.

## Goal

Implement deterministic workflow compilation from approved protocol versions, method templates, parameters, approval gates, waivers, and invalidation requirements. Gate 4 must turn approved scientific conduct intent into an auditable workflow graph without treating drafts, suggestions, projections, or automation outputs as authority.

## Source Inputs

1. `AGENTS.md`
2. `PLANS.md`
3. `docs/scientific-invariants/PRODUCT-LAWS.md`
4. `docs/adr/0002-canonical-json-and-digests.md`
5. `docs/adr/0003-protocol-record-contract.md`
6. `docs/adr/0004-protocol-approval-semantics.md`
7. `docs/adr/0005-workflow-template-contract.md`
8. `docs/adr/0006-workflow-compiler-semantics.md`
9. `docs/port/OPEN-CONFLICTS.md`
10. `docs/port/GOLDEN-FIXTURE-PLAN.md`
11. Current `src/NexusScholar.Workflow` scaffold

## Planning Decisions

- `ADR 0005` defines the local workflow template contract, including template identity, template digest, template version, node shape, edge shape, gate shape, approval requirement references, required inputs, produced artifacts, capability requirements, waiver policy references, invalidation policy references, artifact declaration rules, and schema closure expectations.
- `ADR 0006` defines deterministic workflow compiler semantics, including compile input, compile output, deterministic node ids, deterministic ordering, duplicate node rejection, missing dependency rejection, cycle rejection, hybrid mode semantics, approval node semantics, waiver node semantics, protocol-version binding, output workflow digest, and exclusions from the workflow digest.
- Workflow approval handoff from `ADR 0004` is explicit: protocol approval approves protocol versions; workflow approval nodes gate conduct and cannot be satisfied by automation.

## Conflict Status

- `CF-003`: resolved for Gate 4 planning by `ADR 0006`; source implementation remains scaffold-only until a separate implementation branch.
- `CF-006`: resolved for Gate 4 local scope by `ADR 0005`; missing schema ids are compile/template validation failures, and blueprint schema gaps remain non-claims.
- `CF-007`: resolved for Gate 4 planning by `ADR 0006`; `hybrid` means human-directed work with declared capability requirements and explicit human review or approval semantics.

Do not implement Gate 4 source behavior in this planning branch.

## Dependency-Ordered Tasks

1. Fixture owner: generate Gate 4 conformance fixtures with fixed ids, fixed clocks, deterministic ordering, template digest metadata, workflow digest metadata, and schema refs.
2. Workflow owner: implement template and workflow definition records from `ADR 0005` and `ADR 0006`.
3. Workflow owner: implement compiler validation for approved protocol input, duplicate nodes, unknown dependencies, cycles, schema closure, approval requirements, capability requirements, waiver handling, and invalidation planning.
4. Architecture owner: keep workflow code inward-facing and free of persistence, UI, provider SDK, AI provider, plugin runtime, and bundle dependencies.
5. Conformance owner: add fixture replay for positive and negative Gate 4 cases.
6. Manager/reviewer: verify the implementation does not claim blueprint conformance, PHP compatibility, workflow execution, provenance ledger behavior, artifact storage, plugin runtime, AI execution, or persistence/API/UI support.

## Required Fixtures

Positive fixtures:

- `workflow-compile-rapid-review.json`
- `workflow-compile-hybrid-ai-audit.json`
- `workflow-compile-authorized-waiver.json`
- `workflow-compile-invalidation-plan.json`
- `workflow-compile-order-permutation-same-digest.json`
- `workflow-compile-digest-exclusion-stable.json`
- `workflow-compile-digest-inclusion-changed.json`

Negative fixtures:

- duplicate node id
- unknown edge endpoint
- unknown node requirement
- self-edge
- dependency cycle
- waivable node without waiver policy
- unknown approval role
- missing schema id
- unknown schema id
- missing schema version
- undeclared produced artifact
- artifact declaration with unknown producing node
- unknown capability reference
- missing required input
- scientific conduct input supplied only by compile parameter
- draft protocol used as workflow authority
- ready-for-review protocol used as workflow authority
- withdrawn protocol used as workflow authority
- superseded protocol rejected for normal compile mode
- stale protocol digest
- stale template digest
- workflow id mismatch
- approval authority with `allows_automation = true`
- hybrid node without capability requirement
- hybrid node without human review or approval semantics
- missing waiver disclosure mapping
- missing waiver consequence warning
- expired waiver
- waiver affected-requirement mismatch
- waiver missing approval binding
- unauthorized waiver
- missing invalidation notice source
- stale invalidation notice digest
- affected artifact mismatch
- affected node not present

## Allowed Paths

For this planning branch:

- `docs/adr/0005-workflow-template-contract.md`
- `docs/adr/0006-workflow-compiler-semantics.md`
- `docs/gates/GATE-04.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

For a later implementation branch after fixture planning:

- `src/NexusScholar.Workflow/`
- `tests/NexusScholar.Core.Tests/` for focused workflow domain tests
- `tests/NexusScholar.Conformance.Tests/` for fixture replay
- `tests/NexusScholar.Architecture.Tests/` for dependency rules
- `fixtures/conformance/workflow/`

## Excluded Paths And Claims

- No persistence, API, CLI, UI, bundle export, provenance ledger, AI execution, plugin host, PHP compatibility, or blueprint conformance claims.
- Do not change protocol lifecycle semantics except to consume approved protocol records.
- Do not generate PHP differential fixtures for Gate 4.
- Do not edit `src/`, `tests/`, `fixtures/`, `specs/`, or the PHP reference in this planning branch.

## Verification

Required commands:

1. `dotnet restore NexusScholar.Core.slnx`
2. `dotnet build NexusScholar.Core.slnx -c Release --no-restore`
3. `dotnet test NexusScholar.Core.slnx -c Release --no-build`
4. `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`
5. `powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1`

Verification boundary:

- Gate 4 does not claim offline NuGet reproducibility or locked package restore.
- Restore/build/test/format evidence is sufficient for this local planning and implementation gate, but release reproducibility may require a later package-lock decision.

## Exit Checklist

- `ADR 0005` and `ADR 0006` close `CF-003`, `CF-006`, and `CF-007` for local Gate 4 planning scope.
- Workflow compilation starts only from approved protocol versions.
- Drafts, suggestions, projections, and automation outputs cannot authorize workflow conduct.
- Workflow graph output is deterministic and fixture replayable.
- Waivers and invalidation notices affect graph planning without mutating approved protocol digests.
- Tests cover required positive and negative fixtures.
- Architecture tests confirm workflow has no outward dependency leaks.
- Non-claims remain explicit.
