# ADR 0031: Title And Abstract Screening Conduct

- Status: Accepted
- Date: 2026-07-15
- Decision owner: Nexus Scholar maintainer/manager

## Context

ADR 0013 defines Screening authority semantics and ADR 0021 requires verified
Protocol and Deduplication inputs. The existing `ScreeningService` enforces those
rules in memory, but its mutable collections cannot be reopened as durable
authority. FE-04 needs append-only conduct that survives restart, links to FE-03
work, and can be committed atomically without making workspace files, CLI rows,
or assignment projections scientific identity.

## Decision

### Project boundary

`NexusScholar.Screening` remains persistence independent and depends inward only
on Kernel, Protocol, and Deduplication. It does not depend on CorpusSnapshots,
WorkflowExecution, AppServices, ResearchWorkspace, or CLI.

The package owns canonical Screening authority records and deterministic replay.
An outward bridge maps a verified Screening handoff or decision reference into
an FE-03 human-task completion. AppServices coordinates preview and commit;
ResearchWorkspace owns atomic generations. CLI may invoke only those verified
ports and may not manufacture authority from ids or digest text.

### Canonical authority records

The FE-04 schemas are:

```text
nexus.screening.conduct-policy / 1.0.0
nexus.screening.conduct-header / 1.0.0
nexus.screening.conduct-decision / 1.0.0
nexus.screening.conduct-invalidation / 1.0.0
nexus.screening.conduct-handoff / 1.0.0
```

All use `canonical-json-record`. Persisted bytes are unverified input. Rehydration
requires the exact verified Protocol and Deduplication authorities, reproduces
every digest and binding, validates the hash chain, then returns an explicit
verified journal wrapper. Unknown fields, noncanonical bytes, ordinal gaps,
record removal, reorder, insertion, or cross-journal splicing fail closed.

The conduct policy binds one locked, package-pure candidate set derived from the
verified Deduplication result; approved Protocol version and content digest;
title/abstract criteria digest; allowed exclusion reason codes and their stage;
required review count; immutable actor-to-reviewer-role assignments; authorized
adjudicator roles; approving human; and approval time. A caller-supplied role
string is not authorization. UI assignment rows remain projections.

The header binds a stable conduct id, policy id and digest, candidate-set id and
digest, criteria id and digest, Protocol authority, creating human, creation
time, and the ordered candidate ids. Its digest is the journal chain root.

Each decision binds the conduct and policy authorities, candidate, stage,
verdict, actor and authorized role, rationale, evidence references, criteria and
Protocol digests, ordinal, prior digest, request id and digest, timestamp, and
decision kind. `exclude` requires a policy-defined title/abstract reason code;
other verdicts cannot carry an exclusion reason. Evidence uses stable
kind/id/digest references, never paths.

### Decisions, corrections, and conflicts

Final review and adjudication decisions require identified human actors.
Automation and model output remain nonfinal suggestions outside this authority
journal. One actor can satisfy at most one independent-review slot for a
candidate and decision generation.

Decisions are append-only. A correction is a new review decision that names one
current decision by digest as superseded; it cannot edit or erase history.
Conflicts are deterministic projections when the current independent review
decisions for a candidate disagree. Conflict identity and generation derive
from candidate, criteria digest, and the ordered source decision digests.

Adjudication is a new human decision by an authorized adjudicator. It names the
exact unresolved conflict and every source decision digest. Replay marks that
conflict generation resolved without mutating it. A later corrected disagreement
creates another conflict generation. Unresolved conflict or `needs_review`
blocks handoff.

### Invalidation and handoff

Changed candidate-set, criteria, Protocol, or source-evidence authority is an
explicit append-only invalidation record. An invalidation names a verified
source record, reason, affected current decision digests, ordinal, and prior
digest. Replay rejects partial affected-decision sets. Invalid decisions remain
historical but cannot support current outcomes or handoff.

A handoff is a derived, canonical terminal record for the current conflict-free
projection. It binds the journal head, all included candidate outcomes and
supporting decision digests, and all excluded outcomes and reason codes. Handoff
creation fails while any candidate lacks the policy-required reviews, has an
unresolved conflict, has `needs_review`, or depends on invalidated authority.
Downstream Full Text consumes a verified handoff, not decision-id strings.

### Workflow and storage integration

An FE-03 human task completes with a stable Screening record reference containing
the conduct id, record kind, and digest. The workflow actor must be the same
human recorded by the Screening command. Screening remains the decision
authority owner; WorkflowExecution records only completion and references.

ResearchWorkspace commits policy, header, newly appended records, replay
projection metadata, and any matching WorkflowExecution event as one pointer-last
generation under one lock. Stale expected heads are rejected. Recovery either
selects the complete generation or preserves the prior pointer; it never exposes
half a Screening decision and half a workflow completion.

### Compatibility boundary

The existing in-memory `ScreeningService` remains a historical API while the
canonical conduct path is introduced. Its records do not become verified merely
because they deserialize. No existing fixture is rewritten to claim durability.

## Alternatives Rejected

- Persist mutable `ScreeningService` collections: no canonical replay or stale
  writer protection.
- Depend directly on CorpusSnapshots: would invert the package boundary and make
  a non-packable persistence authority a Screening dependency.
- Treat FE-03 assignment rows as Screening authority: projections and execution
  roles cannot define scientific decision authority.
- Store only current candidate verdicts: loses corrections, disagreements,
  adjudication, and invalidation history.
- Allow handoff to inspect decision-id strings: does not verify the supporting
  record bytes or current conflict state.

## Consequences

FE-04 gains deterministic, restart-safe conduct authority and an atomic bridge
to workflow execution. The cost is canonical records, resolver-backed replay,
explicit correction and invalidation commands, and stricter handoff creation.

## Implementation State

FE-04 is complete for the local Core authority boundary. Canonical conduct,
strict replay, AppServices orchestration, immutable ResearchWorkspace
generations, verified status projection, and the FE-03 human-task bridge are
implemented. A paired Screening decision and Workflow completion can become
authoritative under one workspace lock and one project revision.

CLI mutation is intentionally deferred because the current process entry point
cannot resolve a durable verified Protocol and compiled Workflow authority
package. The CLI exposes manifest-and-artifact integrity verification only; it does not
manufacture authority from ids, digest strings, or caller-supplied roles.

## Fixture Effect

Add canonical valid fixtures for single review, dual agreement, conflict and
adjudication, correction, invalidation, replay, and handoff. Add negative fixtures
for same-actor duplicate review, unknown reason code, wrong stage, stale authority,
automation finalization, malformed chain, unresolved conflict handoff, and
noncanonical persisted bytes.

## Compatibility And Claims

This is a local C# contract. It makes no PHP, blueprint, database, API, cloud,
multi-user, AI-quality, production, scale, security-certification, or
institutional compatibility claim.

## Reversal Criteria

Revise this ADR if deterministic conflict projection cannot represent correction
and adjudication without hidden state, verified Deduplication cannot reproduce
candidate membership, or atomic Screening and FE-03 commit requires a dependency
from Screening into infrastructure.
