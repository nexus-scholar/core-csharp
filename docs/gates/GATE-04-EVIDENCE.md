# Gate 4 Evidence: Workflow Compiler

Status: local implementation evidence recorded. Hosted CI evidence pending for final pushed commit.

## Scope

Gate 4 implements local deterministic workflow compilation from approved protocol versions and schema-closed workflow templates.

Implemented conflict scope:

- `CF-003`: implemented for local workflow compiler behavior.
- `CF-006`: implemented for local Gate 4 schema-closure validation and fixture metadata.
- `CF-007`: implemented for local hybrid workflow-node validation.

## Source Decisions

- `docs/adr/0005-workflow-template-contract.md`
- `docs/adr/0006-workflow-compiler-semantics.md`
- `docs/gates/GATE-04.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

## Behavior Implemented

- Workflow templates carry identity, version, digest, schema id/version, required inputs, nodes, edges, gates, approval roles, approval requirements, capability requirements, waiver policies, artifact declarations, and invalidation policies.
- Workflow compilation accepts approved protocol versions only.
- Workflow compilation rejects stale protocol and template digests.
- Required scientific conduct inputs bind to approved protocol decisions or authorized protocol waivers.
- Compile parameters are accepted only when declared by the template and are recorded in resolved input bindings.
- Optional declared execution parameters affect the workflow digest when supplied.
- Workflow ids are deterministic from protocol, template, and compiler identity material.
- Workflow digests use `canonical-json-record`, `nexus.workflow-definition`, and `1.0.0`.
- Workflow output ordering is deterministic.
- Duplicate nodes, unknown dependencies, self-edges, cycles, schema-closure violations, artifact declaration violations, approval authority violations, hybrid-node violations, waiver violations, and invalidation source violations are rejected with stable categories.
- Invalidation plan entries include amendment and invalidation notice source digests.

## Fixture IDs

Positive local Gate 4 fixture metadata:

- `workflow-compile-rapid-review.json`
- `workflow-compile-hybrid-ai-audit.json`
- `workflow-compile-authorized-waiver.json`
- `workflow-compile-invalidation-plan.json`
- `workflow-compile-order-permutation-same-digest.json`
- `workflow-compile-digest-exclusion-stable.json`
- `workflow-compile-digest-inclusion-changed.json`

Negative local Gate 4 fixture metadata:

- `workflow-compile-negative-cases.json`

The negative fixture pack covers required Gate 4 error categories. These are local contract fixtures, not PHP-generated goldens.

## Local Validation

Commands run:

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

Latest local result before evidence closeout:

- Architecture tests: 8 passed
- Conformance tests: 17 passed
- Core tests: 70 passed
- Total: 95 passed, 0 failed

## Explicit Claims Not Made

- no PHP compatibility claim
- no blueprint conformance claim
- no workflow execution engine
- no provenance ledger behavior
- no bundle export or import behavior
- no plugin runtime or credential grant behavior
- no AI execution or AI governance parity claim
- no persistence, API, CLI, or UI support claim
