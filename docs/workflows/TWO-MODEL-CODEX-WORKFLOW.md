# Two-Model Codex Workflow

## Purpose

This repository uses a two-model operating model for Codex work:

- `gpt-5.5` is the manager, architect, reviewer, gate owner, and final Codex recommendation authority.
- `gpt-5.3-codex-spark` is the implementation worker for constrained code, fixture, test, refactor, and documentation tasks.

The split is encoded in `.codex/config.toml`, project-scoped `.codex/agents/*.toml` files, branch naming, task contracts, and review prompts. Do not rely on informal memory to decide which model owns a task.

## Roles

GPT-5.5 owns:

- gate planning and acceptance recommendations
- architecture and dependency direction
- source-of-truth conflict detection
- ADR consistency
- scientific invariants
- protocol, approval, amendment, waiver, deviation, and bundle semantics
- digest and provenance boundaries
- plugin security and capability boundaries
- LLM governance and human authority boundaries
- PHP compatibility decisions
- fixture comparator design
- final review

Spark owns:

- implementation from an approved task contract
- focused domain tests from an exact test list
- deterministic fixture files from a frozen fixture format
- mechanical refactors after design is frozen
- namespace, reference, and using-directive updates
- CI path and formatting fixes
- docs updates from supplied run data

## Rule

Spark implements only manager-approved tasks.

GPT-5.5 reviews all gate-affecting changes before acceptance is recommended. Passing tests are necessary evidence, not acceptance. The human operator remains the acceptance and merge authority.

## Agent Map

Manager and review agents pinned to `gpt-5.5`:

- `manager_reviewer`
- `blueprint_auditor`
- `scientific_invariant_reviewer`
- `conformance_auditor`
- `dotnet_architect`
- `plugin_security_reviewer`
- `llm_governance_reviewer`
- `test_engineer`
- `php_behavior_explorer`
- `docs_researcher`

Spark worker agents pinned to `gpt-5.3-codex-spark`:

- `spark_worker`
- `spark_fixture_writer`
- `spark_refactor_worker`
- `spark_docs_updater`

## Branch Flow

Use one branch or worktree per coherent task:

1. GPT-5.5 creates or approves the planning task.
2. Spark implements in a worktree branch.
3. GPT-5.5 reviews read-only.
4. Spark fixes only blocking and important findings.
5. GPT-5.5 reviews the delta and recommends accept or reject.
6. Hosted CI must pass before gate evidence is accepted.

Recommended branch names:

- `cdx/plan-gate-3-protocol`
- `cdx/impl-gate-3-protocol-lifecycle`
- `cdx/review-gate-3-protocol`
- `cdx/fix-gate-3-protocol-review`

One Spark branch equals one narrow implementation slice.

## Prompt Cycle

### A. GPT-5.5 Plans

```text
You are the manager. Use GPT-5.5 reasoning.

Read AGENTS.md, the relevant accepted ADRs, gate document, open conflicts,
fixture plan, and current implementation scaffold.

Prepare an implementation task for spark_worker.

Do not implement code.
Return:
- task goal
- allowed paths
- forbidden paths
- exact behavior
- fixture IDs
- negative tests
- verification commands
- explicit non-claims
```

### B. Spark Implements

```text
Use spark_worker.

Implement the task exactly as written by the manager.

Do not change scope.
Do not make design decisions.
Do not edit ADRs unless explicitly allowed.
Do not claim conformance.
Run the requested verification commands.
Stop if the task requires a decision not present in the prompt.
```

### C. GPT-5.5 Reviews

```text
Use manager_reviewer plus:
- scientific_invariant_reviewer
- dotnet_architect
- conformance_auditor
- test_engineer

Review the Spark implementation against the manager task.

Do not edit files.

Classify findings:
- blocking
- important
- minor

Check:
- gate scope
- source-of-truth order
- immutability
- digest scope
- actor authority
- fixture integrity
- negative tests
- forbidden compatibility claims
- dependency direction

Finish with:
- safe to merge: yes/no
- exact required fixes
```

### D. Spark Fixes

```text
Use spark_worker.

Address only the blocking and important findings from manager review.
Do not refactor unrelated code.
Run verification again.
```

### E. GPT-5.5 Accepts

```text
Use manager_reviewer.

Review only the delta since the previous review.
Confirm whether the branch satisfies the gate.
Do not introduce new scope.
```

## Non-Claims

No branch may claim:

- blueprint conformance
- PHP compatibility
- protocol conformance
- bundle conformance
- scientific validity of a model output

unless the relevant gate explicitly permits the claim and fixture or audit evidence exists.

## Spark Stop Conditions

Spark must stop when:

- source-of-truth levels conflict
- behavior requires a new ADR
- fixture expectations are unclear
- implementation would resolve an open conflict implicitly
- approval, amendment, waiver, deviation, or bundle semantics are required but not in the contract
- tests require live provider or live LLM access
- a requested change would weaken an invariant or delete provenance

## Gate 3 Example

Manager task:

```text
Prepare a Spark task for the first Gate 3 slice:
ProtocolDraft + RequiredDecisionDefinition + ProtocolDecision only.

Do not include approval, amendment, waiver, or deviation yet.
Return a task contract with allowed paths, tests, fixtures, and stop conditions.
```

Spark task:

```text
Use spark_worker.

Implement only the task contract below.

[PASTE CONTRACT]

Stop if approval semantics, amendment semantics, or bundle behavior is required.
```

Review task:

```text
Use manager_reviewer.

Review the ProtocolDraft slice against the accepted ADRs and the task contract.
Focus on immutability, duplicate decisions, unresolved decisions, digest boundaries,
and missing negative tests.
Do not edit files.
```
