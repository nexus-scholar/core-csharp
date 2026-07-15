# ADR 0030: Workflow Execution Journal

- Status: Accepted
- Date: 2026-07-15
- Decision owner: Nexus Scholar maintainer/manager

## Context

Accepted Workflow contracts compile an approved Protocol into a verified,
immutable `WorkflowDefinition`. They intentionally exclude execution records,
approval records, provenance events, paths, and generated summaries from the
Workflow digest. FE-02 supplies one executable human decision path, but its
workspace generation chain is specific to Deduplication and is not a generic
Workflow runtime.

FE-03 must represent actual work without moving authority into CLI control flow,
mutable status fields, a scheduler, or a storage adapter. The minimum reusable
contract is an append-only journal whose complete ordered records reconstruct
the same protocol-bound node projection.

## Decision

### Project boundary

Create `NexusScholar.WorkflowExecution` as a persistence-independent domain
project. It may depend only on `NexusScholar.Kernel` and
`NexusScholar.Workflow` inside Nexus. `NexusScholar.Workflow` remains the
compiler and verified-definition owner and must not depend on execution history.

Provenance is a deterministic outward projection of accepted execution records.
`NexusScholar.Provenance` remains Kernel-only and does not depend on
WorkflowExecution. Storage, commands, UI, scheduling, plugin execution, and
model execution remain outside the domain project.

### Canonical records

The first schemas are:

```text
nexus.workflow-execution.authority-policy / 1.0.0
nexus.workflow-execution.header / 1.0.0
nexus.workflow-execution.event / 1.0.0
```

All use `canonical-json-record`. The authority policy binds a stable generic
execution-scope kind, id and digest; the verified Workflow and approved Protocol
bindings; immutable actor-to-role assignments; the approving human actor; and
approval timestamp. A caller-supplied role string is never authorization.

The header binds a stable execution id, the authority-policy id and digest, the
execution-scope reference, the
verified Workflow id and digest, approved Protocol id, version id, version
number and content digest, creating human actor, creation timestamp, and all
compiled node ids. Its digest is the journal chain root.

Each event binds the execution id, Workflow id and digest, Protocol version id
and content digest, authority-policy id and digest, one-based ordinal, prior
event digest or header digest for ordinal one, request id and request digest,
node id, event kind, expected prior node state, resulting node state, actor id,
actor kind, actor role, timestamp, rationale, and kind-specific references. The
event digest covers the complete record.
Ordinals are contiguous and prior digests form one chain. Reordering, removal,
insertion, duplication, or cross-execution splicing is invalid.

Persisted records are unverified input. Rehydration resolves an explicit
`VerifiedWorkflowDefinition`, reproduces the header binding and every event
digest, validates the chain and transition rules, and returns an explicit
verified journal wrapper. Raw records never acquire authority by deserialization.

### Node state projection

The closed node states are `pending`, `ready`, `active`, `blocked`, `completed`,
`failed`, `invalidated`, and `superseded`. At journal creation, root nodes are
`ready` and all other nodes are `pending`; this initial projection is derived
from the verified graph and is not hidden mutable state.

The first transition table is:

| Prior | Result | Required event |
| --- | --- | --- |
| pending | ready | dependencies-satisfied |
| ready | active | work-started |
| active | blocked | work-blocked |
| blocked | active | block-cleared |
| active | completed | work-completed |
| active | failed | work-failed |
| failed | ready | retry-authorized |
| ready, active, blocked, completed, failed | invalidated | work-invalidated |
| invalidated | superseded | successor-bound |

No other transition is admitted. `dependencies-satisfied` requires every
compiled predecessor to be completed and current. Completion may make direct
successors eligible, but each successor still receives its own append-only
transition. Invalidated and superseded nodes never resume in the same execution.

Each command binds the expected prior state and current journal head digest.
Stale or concurrent writers are rejected instead of being silently rebased.
Repeating an exact event id and digest is idempotent; reusing an event id with
different content is a conflict.

### Attempts and evidence

`work-started`, `work-completed`, and `work-failed` bind an attempt id. A retry
uses a new attempt id after `retry-authorized`; it never edits or replaces the
failed attempt. Attempt events bind an attempt sequence, stable request digest,
ordered input record references, responsible agent, start/completion timestamps,
ordered output record references, and optional controlled error. Completion
must resolve every artifact declared in the compiled node's `Produces` set.
Failure binds a controlled error category and a nonblank error summary. Inputs
and outputs are stable kind/id/digest references, never paths.

Automation may perform an automated node and may be recorded as the responsible
agent. This does not grant approval or scientific decision authority.

### Human tasks and approvals

A compiled `human-task` node can enter `active` only through a human actor whose
role is admitted by a digest-bound template gate or approval requirement and by
the execution authority policy. A human or hybrid node without a resolvable
required-role source fails closed. Its completion must bind a human-task record
reference and digest produced by the later conduct gate owning that decision.
Automation cannot complete the node.

A compiled `approval` node additionally binds the matching compiled approval
requirement, distinct human approval record references, required roles, and
minimum approval count. Every approving actor-role pair must resolve through the
execution authority policy. Automation is rejected even when supplied with a
human role string. FE-03 verifies approval shape and actor authority; it does not invent
Screening, Full Text, or other conduct records before their gates.

`VerifiedWorkflowDefinition` is extended to expose the resolved, digest-verified
template used by rehydration. WorkflowExecution uses its role registry, gates,
approval requirements, and artifact declarations; it never accepts an unrelated
template from the caller.

### Invalidation

`work-invalidated` binds a canonical source record id, kind, digest, reason, and
the Workflow invalidation-policy reference where one exists. Changed Protocol,
Workflow, snapshot, decision, or artifact authority must be represented by an
explicit verified source record before invalidation is appended.

Invalidation propagates through outgoing graph edges to every current dependent
node. The journal records one ordered invalidation event per affected node;
replay rejects a partial propagation set. A successor Workflow execution may
bind supersession only after all affected nodes in the predecessor execution are
invalidated.

The foundation slice fails closed on `work-invalidated` and `successor-bound`
until the batch propagation command and complete affected-node validation are
implemented. A caller cannot append a single-node invalidation and leave
dependents appearing current.

### Replay and current projection

Replay starts from the verified header projection and applies events in ordinal
order. The projection contains node state, current attempt id, complete attempt
history, human-task and approval references, latest event digest, and execution
completeness. It is derived output and is never persisted as independent
scientific authority.

The same verified Workflow, header, and ordered event bytes must produce the
same projection and head digest across process restarts and supported platforms.

### First implementation slice

The first FE-03 slice implements the domain project, header/event canonical
records, execution authority policy, the closed transition reducer, hash-chain
verification, verified rehydration, deterministic replay, and focused tests. It does not yet add
ResearchWorkspace persistence, AppServices commands, CLI commands, or a
provenance adapter. Those follow only after this state machine is accepted.

## Alternatives Rejected

- Add execution fields to `WorkflowDefinition`: mixes immutable plan identity
  with mutable history and changes accepted Workflow digest semantics.
- Store only current node status: destroys attempts, causal order, and replay.
- Reuse FE-02 authority generations as a generic journal: couples execution to
  one conduct domain and persistence format.
- Put execution records in Provenance: provenance describes accepted activity;
  it is not the Workflow state-machine authority owner.
- Implement a scheduler or queue first: introduces operational behavior without
  a stable scientific execution contract.

## Consequences

FE-03 gains deterministic protocol-bound work history and explicit concurrency
semantics. The cost is a separate package, immutable event chain, resolver-backed
rehydration, and later atomic workspace integration.

The journal proves recorded transitions, not that external work was scientifically
correct. Completion remains contingent on resolvable output and conduct records.

## Migration Effect

No existing Workflow or FE-02 record changes. Existing Workflow fixtures that
claim no execution engine remain correct for their historical scope. Later FE-03
fixtures add separate execution records and do not rewrite golden outputs.

## Fixture Effect

Add valid multi-node replay and negative fixtures for draft/stale Workflow
authority, duplicate completion, completion before start, invalid transition,
missing output, overwritten retry, automation human completion, reordered or
removed event, stale head, forged actor role, concurrent advance, and partial
invalidation.

## Compatibility And Claims

This is a local C# contract. It makes no PHP, blueprint, database, API, cloud,
multi-user, scheduler, plugin-host, AI-runner, production, or institutional
compatibility claim.

## Reversal Criteria

Revise this ADR if verified Workflow authority cannot reproduce execution
bindings, FE-04 or FE-05 require incompatible human-task semantics, invalidation
cannot be expressed without cross-domain dependencies, or deterministic replay
cannot survive canonical round-trip.
