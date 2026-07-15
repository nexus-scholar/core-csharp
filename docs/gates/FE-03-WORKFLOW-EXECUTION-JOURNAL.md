# FE-03: Workflow Execution Journal

Status: first domain slice active under accepted ADR 0030.

## Current Implementation State

Started on `cdx/fe-03-workflow-execution-journal`:

- `NexusScholar.WorkflowExecution` exists with Kernel + Workflow dependencies;
- verified Workflow rehydration exposes its digest-verified template authority;
- canonical execution authority policy, header, request, and event digests exist;
- root readiness, the closed non-invalidation transitions, expected-head
  chaining, attempts,
  retries, role authorization, human/automation separation, and replay projection
  have focused tests;
- architecture tests enforce inward-only dependencies.

Still required before FE-03 completion:

- canonical unverified DTO parsing and byte-level rehydration fixtures;
- exhaustive approval, output-resolution, idempotency-conflict, and invalidation
  propagation cases;
- execution-to-provenance projection;
- atomic ResearchWorkspace journal persistence and recovery;
- AppServices and CLI orchestration;
- completion evidence and roadmap closeout.

## Goal

Deliver replayable, protocol-bound Workflow execution authority as canonical,
append-only records without adding persistence, scheduling, UI, plugin, or model
execution behavior to the first slice.

## Inputs

- accepted ADRs 0002, 0004, 0006, 0008, 0018, 0019, 0023, 0028, and 0029;
- verified `WorkflowDefinition` and approved Protocol authority;
- FE-02 executable human decision path as lifecycle evidence, not a reusable
  persistence format;
- the FE-03 section of the feature expansion priority plan.

## Dependency-Ordered Work

1. FE-03.1: accept ADR 0030 and the exhaustive state table.
2. FE-03.2: add `NexusScholar.WorkflowExecution` with canonical header/event,
   execution authority policy, transition reducer, verified rehydration, and
   deterministic replay.
3. FE-03.3: add deterministic execution-to-provenance projection without
   changing Provenance authority ownership.
4. FE-03.4: persist journals atomically in ResearchWorkspace generations with
   crash recovery and stale-writer rejection.
5. FE-03.5: expose UI-neutral preview/commit orchestration in AppServices and an
   explicit-confirmation local CLI surface.
6. FE-03.6: add conformance fixtures, architecture rules, completion evidence,
   and roadmap closeout.

## First Slice

Allowed paths:

- `docs/adr/0030-workflow-execution-journal.md`;
- this gate;
- `src/NexusScholar.WorkflowExecution/**`;
- focused additions to solution and dependency tests;
- focused domain tests and `fixtures/conformance/workflow-execution/**`.

The first slice must implement:

- verified Workflow and approved Protocol binding;
- generic execution-scope binding and digest-bound actor-role authority;
- deterministic root-node readiness;
- the closed eight-state transition reducer;
- expected-state and expected-head concurrency checks;
- append-only attempt history;
- automation exclusion from human task and approval completion;
- hash-chained canonical events;
- resolver-backed rehydration and replay equality.

## Required Negative Cases

- draft, stale, or unresolved Workflow authority;
- unknown node, state, event kind, actor kind, or role;
- role text without a matching execution authority assignment;
- completion before start and duplicate completion;
- dependency readiness before predecessors complete;
- attempt id reused or prior attempt overwritten;
- completion output reference missing or invalid;
- automation completes a human task or approval;
- wrong approval requirement or insufficient/distinct-role approvals;
- transition after invalidation or supersession;
- event ordinal gap, prior-digest mismatch, reorder, removal, or splice;
- stale expected head and concurrent node advance;
- partial invalidation propagation.

## Excluded From First Slice

- ResearchWorkspace files, generations, pointers, locks, or recovery;
- AppServices and CLI commands;
- database, API, cloud, UI, scheduler, queue, or background service;
- plugin execution, live providers, model execution, or AI authority;
- FE-04 Screening and FE-05 Full Text conduct records;
- PHP, blueprint, production, scale, security-certification, or institutional
  compatibility claims.

## Verification

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Also run focused WorkflowExecution, Workflow, architecture, and conformance tests.

## First-Slice Exit

- ADR 0030 remains satisfied after architecture and scientific review;
- every admitted transition is explicit and every other transition fails;
- replay from canonical records reproduces node state, attempts, and head digest;
- human authority cannot be forged with role text or automation actor kind;
- retries and invalidations preserve all predecessor history;
- project dependency and forbidden-symbol architecture tests pass;
- no production behavior outside the first-slice paths is added.
