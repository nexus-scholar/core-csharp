# ADR 0008: Provenance Ledger

Status: Accepted

Date: 2026-06-27

## Context

Gate 5 needs local provenance ledger behavior before artifact storage, portable bundles, workflow execution, or governed AI can make reconstructability claims. The current `NexusScholar.Provenance` scaffold records only a thin event id, activity, subject, actor, timestamp, inputs, and outputs. It does not define canonical event identity, protocol and workflow bindings, event digest material, append-only invariants, projection exclusions, or stable error categories.

`ADR 0002` already defines `DigestScope.ProvenanceEvent`. Gate 5 uses that local digest vocabulary and does not adopt blueprint provenance or bundle contracts as authority.

## Decision

### 1. Event Record Shape

A provenance event carries:

- `event_id`
- `agent`
- `activity`
- `occurred_at`
- `subject`
- optional `protocol_binding`
- optional `workflow_binding`
- `inputs`
- `outputs`
- `event_digest`

The event is immutable after construction. Collection inputs are defensively copied and exposed as immutable snapshots.

### 2. Agent Model

An agent carries:

- `agent_id`
- `agent_kind`
- optional `display_name`

Supported local agent kinds are:

- `human`
- `automation`
- `plugin`
- `system`
- `import`

Automation, plugin, system, and import agents may record provenance for activity they perform, but they do not become scientific decision or approval authority.

### 3. Activity Model

An activity carries:

- `activity_id`
- `label`
- `requires_actor`
- `requires_input`
- `requires_output`

Gate 5 does not define workflow execution state. Activities describe provenance events only.

### 4. Entity References

Subject, input, and output references identify scientific entities without storage or persistence commitments. Entity references carry:

- `entity_kind`
- `entity_id`
- optional `content_digest`

Projection kinds such as cache, wiki, generated narrative, embedding index, and local path are not canonical provenance entities. They may be cited elsewhere as projections, but Gate 5 rejects them as canonical event subject/input/output references.

### 5. Protocol Binding

Protocol binding is optional, but when present carries:

- `protocol_id`
- `protocol_version_id`
- `protocol_version_number`
- `protocol_content_digest`

The digest must use the already accepted protocol-content scope. Gate 5 binds by stable ids and digests only and does not reference `NexusScholar.Protocol`.

### 6. Workflow Binding

Workflow binding is optional, but when present carries:

- `workflow_id`
- `workflow_digest`
- optional `workflow_node_id`

The workflow binding does not imply workflow execution. It only records that an event is related to a planned workflow graph or node.

### 7. Event Digest

The event digest uses:

- `scope = provenance-event`
- `schemaId = nexus.provenance-event`
- `schemaVersion = 1.0.0`

The digest envelope content includes event id, agent, activity, occurred_at, subject, protocol binding when present, workflow binding when present, inputs, outputs, and ordered event fields.

The event digest excludes:

- the event digest value itself;
- projections, caches, wiki pages, generated narrative content, embeddings, and search indexes;
- persistence metadata;
- local file paths;
- bundle/container metadata;
- provider responses;
- runtime object identity.

### 8. Append-Only Store

The in-memory provenance store:

- appends events only;
- rejects duplicate event ids;
- preserves append order;
- exposes immutable ordered snapshots;
- stores cloned event records so later source collection mutation cannot alter stored content or digest.

There is no deletion, update, persistence, database, API, UI, cloud sync, or bundle export behavior in Gate 5.

### 9. Validation Rules

Gate 5 rejects:

- missing/default actor when the activity requires an actor;
- missing required input digest when the activity requires input;
- missing required output digest when the activity requires output;
- duplicate event ids;
- projection/cache/wiki/generated/local-path entities as canonical provenance references.

### 10. Error Categories

Gate 5 exposes stable error categories:

- `duplicate-event-id`
- `missing-actor`
- `missing-required-input`
- `missing-required-output`
- `projection-not-canonical`

Tests must not depend only on free-form exception text.

## Consequences

- `CF-004` is resolved only for local provenance ledger behavior.
- AI governance, context manifests, evidence policy, approval result modeling, provider behavior, and AI task records remain unresolved for Gate 11.
- Bundle portability, artifact storage, persistence schema, API, Search, Deduplication, Screening, and PHP parity remain out of scope.

## Fixture Effect

Gate 5 local conformance fixtures must include:

- `provenance-event-protocol-approved.json`
- `provenance-event-workflow-node-completed.json`
- `provenance-ledger-append-order.ndjson`
- `provenance-ledger-duplicate-reject.json`
- `provenance-invalid-missing-actor.json`
- `provenance-invalid-missing-required-input.json`
- `provenance-invalid-missing-required-output.json`
- `provenance-invalid-projection-as-canonical.json`

Fixtures are local conformance fixtures, not PHP-generated goldens.

## Explicit Claims Not Made

- no bundle parity
- no artifact storage implementation
- no AI governance parity
- no PHP compatibility
- no blueprint conformance
- no persistence schema
- no cloud sync
- no workflow execution engine
- no provider or network behavior
