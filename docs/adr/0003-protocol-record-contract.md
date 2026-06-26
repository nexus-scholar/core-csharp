# ADR 0003: Protocol Record Contract

Status: Accepted

Date: 2026-06-26

## Context

`CF-001` records that the current protocol scaffold and fixture are too thin for an audit-grade protocol lifecycle. The current shape is effectively `subject + required_decisions + decisions`, while the sibling blueprint protocol schema includes version identifiers, template identity, intent, unresolved decisions, approvals, timestamps, amendment links, and digest fields.

The blueprint is a discovery input, not an adopted authority. Gate 0 found drift between blueprint markdown, JSON schemas, contract sketches, examples, and templates. This ADR therefore defines the local Gate 3 protocol record contract without claiming blueprint conformance or PHP compatibility.

`ADR 0002` already defines the Kernel digest vocabulary and `protocol-content` digest scope. Gate 3 must use those Kernel primitives instead of inventing protocol-local digest rules.

## Decision

### 1. Protocol aggregate records

Gate 3 recognizes these protocol records:

- `ProtocolDraft`
- `RequiredDecisionDefinition`
- `ProtocolDecision`
- `UnresolvedDecision`
- `ProtocolVersion`
- `ProtocolAmendment`
- `ProtocolWaiver`
- `ProtocolDeviation`
- `ProtocolInvalidationNotice`

These records are domain contracts. They are not yet a persistence schema, API schema, or blueprint conformance claim.

### 2. Protocol status

`ProtocolStatus` uses these values:

- `draft`
- `ready_for_review`
- `approved`
- `superseded`
- `withdrawn`

A draft is never an approved protocol. An approved version is immutable. A superseded version remains reconstructable and linked to its successor.

### 3. Draft record shape

`ProtocolDraft` must carry:

- `protocol_id`
- `draft_id`
- `project_id`
- `status`
- `template`
- `intent`
- `values`
- `required_decisions`
- `decisions`
- `unresolved_decisions`
- `waivers`
- `created_by`
- `created_at`
- `updated_at`
- optional `base_version_id` when drafting an amendment

`template` must include:

- `template_id`
- `template_version`
- `template_digest`

`intent` must preserve:

- `raw_subject`
- `review_goal`
- optional `selected_review_family`

Raw research intent is authoritative input and must not be replaced by normalized labels alone.

### 4. Required decision definition shape

`RequiredDecisionDefinition` must carry:

- `decision_key`
- `title`
- `description`
- `value_schema`
- `required_before`
- `approval_gate_id`
- `source_requirement_id`
- `allows_unresolved`

Required decision definitions are part of protocol contract planning, not UI hints. A required decision either has a recorded `ProtocolDecision` or an `UnresolvedDecision` that explicitly states whether it blocks approval.

### 5. Decision record shape

`ProtocolDecision` must carry:

- `decision_id`
- `decision_key`
- `value`
- optional `rationale`
- `decided_by`
- `decided_at`
- optional `source_proposal_digest`
- optional `supersedes_decision_id`

`value` is canonical JSON content, not an untyped string blob. `source_proposal_digest` may point to an AI or tool proposal, but the decision remains the identified human actor's decision. Machine suggestions are never decisions.

Decision keys are stable scientific identifiers within a protocol version. A decision cannot be overwritten in place. A correction creates a new decision revision in draft state or an amendment after approval.

### 6. Unresolved decision shape

`UnresolvedDecision` must carry:

- `unresolved_id`
- `decision_key`
- `question`
- `reason`
- `required_before`
- `created_by`
- `created_at`
- `blocks_protocol_approval`

Any unresolved decision with `blocks_protocol_approval = true` prevents protocol approval. Gate 3 implementation must reject approval while blocking unresolved decisions remain.

### 7. Approved version shape

`ProtocolVersion` must carry:

- `version_id`
- `protocol_id`
- `project_id`
- `version_number`
- `status = approved`
- `template`
- `intent`
- `values`
- `required_decisions`
- `decisions`
- `waivers`
- `content_digest`
- `approval_policy_id`
- `approval_ids`
- `approved_at`
- optional `supersedes_version_id`
- optional `superseded_by_version_id`
- optional `amendment_id`

An approved version must have no blocking unresolved decisions. The version content is immutable after approval. Changes require an amendment and a new version.

Approved records must be deeply immutable in implementation:

- collection inputs are defensively copied;
- exposed collections cannot be mutated through retained references;
- no public copy path may rewrite `content_digest`, approval ids, version ids, or supersession fields without constructing a new valid record;
- amendments construct new versions instead of mutating existing versions.

### 8. Amendment shape

`ProtocolAmendment` must carry:

- `amendment_id`
- `protocol_id`
- `amends_version_id`
- `produces_version_id`
- `previous_content_digest`
- `requested_by`
- `requested_at`
- `rationale`
- `changed_decision_keys`
- `invalidation_notices`
- optional `invalidation_plan_digest`
- `approval_policy_id`
- `approval_ids`

An amendment never edits an approved version. It preserves supersession links and produces a new approved `ProtocolVersion` when accepted.

### 9. Waiver shape

`ProtocolWaiver` must carry:

- `waiver_id`
- `affected_requirement_id`
- optional `condition`
- optional `expires_at`
- `rationale`
- `consequence_warning`
- `disclosure_mapping`
- `requested_by`
- `requested_at`
- `approval_policy_id`
- `approval_ids`

A waiver is permitted only when the owning method/template requirement declares that it can be waived. A waiver is visible protocol content and must be included in the approved version's `protocol-content` digest.

### 10. Deviation shape

`ProtocolDeviation` must carry:

- `deviation_id`
- `protocol_version_id`
- `planned_requirement_id`
- `actual_conduct_summary`
- `rationale`
- `classification`
- `recorded_by`
- `recorded_at`
- `effect`
- `disclosure_mapping`

Deviation classification values are:

- `approved_amendment_required`
- `protocol_deviation`
- `operational_variance_no_scientific_effect`
- `unresolved_inconsistency`

A deviation records actual conduct against an approved version. It does not mutate the version digest. If the planned method changes, an amendment must create a new version.

### 11. Invalidation notice shape

`ProtocolInvalidationNotice` must carry:

- `notice_id`
- `source_amendment_id`
- `affected_requirement_id`
- `affected_artifact_digest`
- `affected_workflow_node_id`
- `effect`
- `required_action`
- `created_at`

Invalidation notices are first-class audit records. They must identify downstream impact instead of burying it in amendment prose.

### 12. Protocol content digest

The approved `ProtocolVersion.content_digest` uses `DigestScope.ProtocolContent`.

The digest input is a Kernel `DigestEnvelope` with:

- `scope = protocol-content`
- `schemaId = nexus.protocol-content`
- `schemaVersion = 1.0.0`

The envelope content must include:

- `protocol_id`
- `version_id`
- `project_id`
- `version_number`
- `template`
- `intent`
- `values`
- `required_decisions`
- `decisions`
- `waivers`
- optional `supersedes_version_id`
- optional `amendment_id`

The envelope content must not include:

- the digest value being computed
- approval records
- detached signatures
- transport or bundle checksums
- persistence metadata
- UI state
- generated summaries, caches, embeddings, or wiki projections
- machine-local file paths
- wall-clock generation time not already part of the scientific record

Approvals bind to `content_digest` but are outside that digest. Approval records use their own approval semantics defined by `ADR 0004`.

### 13. Ordering and canonicalization

Digest content must use `ADR 0002` canonical JSON rules.

The implementation must use deterministic ordering for collections inside digest material:

- decisions ordered by `decision_key`, then `decision_id`
- required decisions ordered by `decision_key`
- unresolved decisions ordered by `decision_key`, then `unresolved_id`
- waivers ordered by `affected_requirement_id`, then `waiver_id`
- invalidation notices ordered by `affected_requirement_id`, then `notice_id`
- changed decision keys ordered ordinally

Array order remains semantic. Any logically unordered collection must be converted into a deterministic ordered representation before digesting.

### 14. Domain error categories

Gate 3 implementation must expose stable error categories for:

- `missing-required-decision`
- `blocking-unresolved-decision`
- `duplicate-decision`
- `post-approval-mutation`
- `unauthorized-approval`
- `stale-content-digest`
- `invalid-amendment`
- `invalid-waiver`
- `invalid-deviation`

Tests must not rely only on free-form exception messages.

## Consequences

- `CF-001` has an accepted local Gate 3 planning decision. Implementation and conformance fixture closure remain pending.
- Current `src/NexusScholar.Protocol` remains provisional until updated by a separate implementation task.
- The existing `fixtures/conformance/protocol-minimal.json` remains a discovery fixture only. It is not authoritative for Gate 3 implementation and must be replaced through a dedicated fixture-generation task rather than edited in place.
- Blueprint protocol conformance remains unclaimed until schema drift and bundle export semantics are separately resolved.

## Fixture Effect

Gate 3 implementation must add fixture-backed cases for:

- draft protocol with required decisions
- approved protocol version with `protocol-content` digest
- amended protocol version preserving supersession links
- invalidation notice attached to amendment impact
- waiver included in protocol digest
- deviation linked to an approved version without mutating it
- rejection when blocking unresolved decisions remain
- rejection of duplicate decision overwrite
- rejection of post-approval mutation
- rejection of digest material that includes approval records or generated projections
- rejection of old newline `key=value` digest material as non-authoritative protocol content

## Explicit Claims Not Made

- no blueprint conformance claim
- no PHP compatibility claim
- no bundle contract adoption
- no persistence or API schema commitment
- no workflow compiler implementation
- no provenance parity claim
- no AI governance parity claim
