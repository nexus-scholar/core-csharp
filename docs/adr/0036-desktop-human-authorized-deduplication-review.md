# ADR 0036: Desktop Human-Authorized Deduplication Review

- Status: Accepted
- Date: 2026-07-17
- Decision owner: Nexus Scholar maintainer/manager

## Context

ADR 0035 introduced a non-authoritative desktop product host for operational
workspace actions. It intentionally admitted no scientific decisions. FE-08 now
needs one complete human review workflow without weakening the FE-01/FE-02
authority chain or making UI state authoritative.

The existing FE-02 deduplication decision path can reconstruct its verified
source result, policy, current snapshot, active decisions, and known history from
the local ResearchWorkspace. Title/abstract Screening cannot yet do the same:
ADR 0031 requires verified Protocol, Deduplication, criteria, and optional
Workflow authorities supplied by a trusted resolver, and the desktop process
does not have that resolver. It must not manufacture those authorities from ids
or digest strings.

## Decision

### Admitted scientific action

FE-08 Slice 3 admits only FE-02 deduplication review decisions for exact current
review-candidate pairs. The desktop may preview and confirm the existing policy
actions `merge`, `keep-separate`, and `mark-unresolved` when the verified policy
authorizes the selected human actor and role.

No Screening, Full Text, Protocol, workflow, appraisal, extraction, synthesis,
reporting, or export decision is admitted by this slice.

### Authority boundary

`NexusScholar.ResearchWorkspace` owns a structured deduplication-review operation
that reconstructs and verifies the same authority material used by the CLI. It
returns framework-neutral projections and executes the existing atomic
`CommitDeduplicationDecision` transaction. CLI and desktop become adapters over
that shared operation; neither parses the other adapter's output.

The active actor id and role are explicit command inputs. They are not login
claims, preferences, or UI session authority. The verified FE-01 policy remains
the sole authorization source. A caller-supplied role string cannot authorize an
actor absent an exact policy assignment.

### Preview and confirmation

The desktop facade returns an immutable preview bound to:

- workspace id and expected project revision;
- current authority generation and manifest digest;
- current decision-set snapshot id and digest;
- source deduplication result id and digest;
- exact review target id and digest;
- requested action, reason, rationale, actor, role, and superseded decision;
- expected candidate membership and invalidation effects.

Confirmation uses a canonical-JSON digest over that material. Execution rebuilds
the verified command from current workspace authority and rejects changed
workspace, source, policy, snapshot, target, actor material, or confirmation
material as stale. Lock contention and interrupted authority transitions remain
recovery-required states, never success.

### Product workflow

The product host exposes an authority-aware review queue from the shared
ResearchWorkspace operation. Each target includes any exact active decision id
and digest. A previously decided target cannot accept another fresh decision; a
correction must select and supersede one exact active decision. A researcher
selects one exact pair, enters actor id and active role, chooses an allowed policy
action and reason, inspects effects, then explicitly confirms. Success refreshes
the queue and authority projection. Cancel performs no mutation.

Paths, row indices, selection state, display labels, and keyboard focus never
enter authority records. The stable target digest and verified candidate ids do.

### Screening deferral

Desktop Screening decisions remain unavailable. A later ADR or FE-08 gate must
define how the process obtains and verifies the exact Protocol, Deduplication,
criteria, and Workflow authority package required by ADR 0031. UI fields or
workspace manifest ids alone are not a resolver.

## Consequences

- FE-08 gains one real human-authorized scientific workflow without broadening
  scientific authority.
- CLI and desktop share reconstruction, preview, and commit behavior.
- actor switching invalidates a pending preview because actor and role are
  confirmation material.
- the desktop can show only actions admitted by the verified policy.
- unresolved work remains explicit after `mark-unresolved`.

## Rejected Alternatives

- Invoke `nexus dedup decide` as a child process: console text is not a
  structured application contract.
- Trust actor and role fields from the UI: policy assignment, not caller text,
  grants authority.
- Start with Screening decisions: the desktop cannot yet rehydrate the required
  verified authority package.
- Persist an active actor in `nexus.project.json`: UI/session state is not
  scientific authority.

## Compatibility And Claims

This is a local C# product workflow over the accepted FE-02 authority. It makes
no PHP, blueprint, identity-provider, authentication, multi-user, database, API,
cloud, provider, AI, accessibility-certification, or production-security claim.

## Reversal Criteria

Revise this ADR before admitting another scientific command, treating an
external identity claim as a policy assignment, storing active UI identity in
scientific state, or changing the FE-02 authority material bound by preview.
