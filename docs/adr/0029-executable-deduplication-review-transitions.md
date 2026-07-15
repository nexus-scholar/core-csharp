# ADR 0029: Executable Deduplication Review Transitions

- Status: Accepted
- Date: 2026-07-15
- Decision owner: Nexus Scholar maintainer/manager

## Context

ADR 0028 and FE-01 established verified Deduplication decisions, immutable corpus
snapshots, invalidation records, provenance projections, and atomic baseline
authority generations. They intentionally did not admit a production decision
append or change snapshot membership. FE-02 must execute the first human
scientific mutation without turning CLI state, files, or automation into
authority.

The current authority source is one verified Deduplication result, one fixed
local policy, and one predecessor snapshot. The only executable targets are exact
verified `review-candidate-pair` records from that result. There is still not
enough cross-domain evidence for a generic command or decision framework.

## Decision

### Command Boundary And Idempotency

`NexusScholar.Deduplication` owns a UI-neutral decision request and preview. The
request binds the exact authority generation manifest, predecessor snapshot id
and record digest, result id and digest, review-target id and digest, policy id
and digest, action, reason, rationale, actor and role, and optional superseded
decision. It contains no time, random id, path, or persistence handle.

The request is a canonical `nexus.deduplication.review-command` version `1.0.0`
record. Its digest derives `decision-{sha256}` and is the idempotency key.
Repeating the exact request returns the already committed transition. Reusing
that decision id with different material is a conflict. A newer action
for the same target must explicitly supersede the active decision; overwrite and
implicit replacement are forbidden.

### Closed Action Semantics

- `merge` unions the current groups or unresolved entries containing the pair,
  preserves every member and evidence reference, and removes merged candidates
  from candidate-level unresolved entries.
- `keep-separate` preserves membership and establishes an active separation
  constraint. No later merge may place that pair in one group unless the
  separation decision is explicitly superseded.
- `mark-unresolved` preserves membership and records an active unresolved target.
  It remains visible and prevents a final-corpus claim.

Only pairs present in the verified result review queue are executable. Title
equality alone is never merge authority. A merge representative is elected by
the existing deterministic Deduplication ranking over all merged members; FE-02
does not admit manual representative override.

The reducer rejects stale targets, missing evidence, duplicate active decisions,
cycles, implicit correction, and any merge violating an active keep-separate
constraint. Active decisions are one per policy target. Superseded decisions
remain in predecessor generations and are removed only from the active set.

### Successor Records

One accepted request creates, in order:

1. one immutable Deduplication decision bound to the predecessor snapshot;
2. one successor corpus snapshot with the reduced membership and active decision
   set;
3. one invalidation record invalidating the predecessor snapshot and, for a
   correction, the superseded decision;
4. `deduplication-decision-recorded`, `corpus-snapshot-invalidated`, and
   `corpus-snapshot-published` provenance events.

No raw Search/import record or analysis generation is rewritten. Invalidation
remains closed to `deduplication-decision` and `corpus-snapshot`; Screening, Full
Text, report, bundle, and workflow records are not yet admitted authority kinds.

### Atomic Persistence

`NexusScholar.ResearchWorkspace` owns `CommitDeduplicationDecision`. It writes a
new `nexus.workspace-authority-generation.v2` directory containing the canonical
request, fixed policy, decision, successor snapshot, invalidation, three
provenance events, and canonical manifest. The manifest binds the request digest, exact predecessor
authority generation and manifest digest, source analysis and result, policy,
decision, predecessor and successor snapshots, invalidation, active decision-set
digest, and every raw artifact digest.

Before promotion and again under the workspace lock, the handler rehydrates all
records, verifies the predecessor chain, and compares project revision, analysis
generation and manifest, authority generation and manifest, result, policy,
snapshot, target, and request. The generation directory is promoted first and
the project pointer last. Failure after promotion quarantines the unpointed
generation. Orphan recovery and stale-writer rejection follow FE-01.

Current authority verification traverses predecessor manifests to reconstruct
known decisions and snapshots. The project file retains the same three authority
pointer fields; no mutable journal or second current-state pointer is added.

### Application And CLI

`NexusScholar.AppServices` exposes immutable request, preview, and result models.
Preview reports affected candidates, membership change, representative, records
to invalidate, and whether unresolved work remains. It performs no file I/O.

The CLI admits `nexus dedup decide`. Without `--confirm`, it prints the exact
preview and performs no mutation. With `--confirm`, it executes that bound request
and reports committed or already-applied status. Actor, role, reason, rationale,
and target are explicit. APP-01 placeholders are unlocked only for these three
Deduplication actions.

## Consequences

FE-02 provides one complete actor-bound decision path and immutable successor
history. It also adds deliberate complexity: authority verification is now a
chain operation, snapshots can differ from raw Deduplication clustering, and
idempotency is a domain request property rather than a CLI retry convention.

Policy rotation, source-result refresh while authority is active, manual
representative override, generic workflow execution, database/API/cloud state,
desktop mutation, live providers, AI acceptance, and PHP/application parity
remain separate gates.

## Compatibility And Claims

This is a local C# contract. PHP observations, Nexus CLI behavior from other
repositories, and blueprint examples are not expected outputs. No PHP,
application, database, API, cloud, multi-user, production, signature, or
institutional identity compatibility is claimed.
