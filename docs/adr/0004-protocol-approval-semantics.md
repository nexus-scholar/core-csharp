# ADR 0004: Protocol Approval Semantics

Status: Accepted

Date: 2026-06-26

## Context

`CF-008` records that protocol approval semantics are not frozen. The blueprint contains approval modes such as `single_researcher`, `dual_independent`, specialist approval, institutional signoff, and custom role expressions, but Gate 0 found blueprint defaults unsafe to adopt silently.

Product laws require approval to bind an actor, timestamp, exact content digest, and version. They also require that automation never becomes scientific authority.

This ADR defines the local Gate 3 approval semantics for protocol approval. It does not implement workflow gates, AI governance, institutional policy engines, or blueprint conformance.

## Decision

### 1. Approval record shape

`ProtocolApproval` must carry:

- `approval_id`
- `target_type`
- `target_id`
- `protocol_id`
- `protocol_version_id`
- `protocol_version_number`
- `content_digest`
- `policy_id`
- `policy_version`
- `policy_mode`
- `decision`
- `approved_by`
- `approved_at`
- optional `role`
- optional `rationale`
- optional `supersedes_approval_id`
- `approval_record_digest`

`target_type` is `protocol-version` for Gate 3.

`decision` uses these values:

- `approved`
- `rejected`
- `changes_requested`
- `withdrawn`

Only `approved` records can satisfy an approval policy. Other decisions remain audit records.

`approval_record_digest` uses `DigestScope.ApprovalRecord`. Its digest input includes approval identity, target protocol version, bound `content_digest`, policy id/version, decision, actor, role when present, timestamp, and rationale when present. It must not include its own digest value.

### 2. Actor identity

`approved_by` must be an identified human actor.

Automation, plugins, LLMs, import jobs, and host services may prepare approval proposals, warnings, or validation reports, but they cannot approve protocol content.

Gate 3 implementation must reject approval attempts by non-human actors or missing actors.

### 3. Timestamp rules

`approved_at` must be a canonical UTC timestamp produced from an injected clock.

Approval logic must not call wall-clock time directly during canonical serialization. The timestamp is part of the approval record, not the approved protocol-content digest.

### 4. Content digest binding

Every approval binds exactly one `ProtocolVersion.content_digest`.

Approval must be rejected when:

- the supplied digest differs from the current protocol-content digest;
- the draft changed after the digest was computed;
- the target version id does not match the digest material;
- the protocol version is already superseded or withdrawn.

This is stale digest rejection. A stale approval is not updated in place; a new approval attempt must bind the new digest.

### 5. Approval policy model

`ApprovalPolicy` must carry:

- `policy_id`
- `policy_version`
- `mode`
- `required_roles`
- `minimum_approvals`
- `requires_distinct_actors`
- `allows_automation`
- optional `method_pack_id`
- optional `custom_rule_id`

Gate 3 supports these modes:

- `single_researcher`
- `dual_independent`
- `methodologist`
- `information_specialist`
- `statistician`
- `project_owner`
- `institutional_signoff`
- `custom_role_expression`

`allows_automation` must be false for protocol approval authority. Automation may only propose or validate.

### 6. Single approval

`single_researcher` is satisfied by one approved record from one authorized human actor bound to the exact protocol-content digest.

Single approval is allowed for custom local reviews only when no method/template pack requires a stricter policy.

### 7. Dual independent approval

`dual_independent` is satisfied only when:

- at least two approved records exist;
- approvals are from distinct human actors;
- each approval binds the same `protocol_version_id`;
- each approval binds the same `content_digest`;
- neither approval supersedes or withdraws the other;
- required roles, if any, are satisfied.

The same actor cannot satisfy both sides of a dual-independent policy.

### 8. Method-pack-specific requirements

The method/template pack chooses the required approval policy for protocol approval.

The kernel may support multiple policy modes, but it must enforce the policy selected by the pinned method/template identity. A host must not silently downgrade a method pack from `dual_independent` to `single_researcher`.

If no method/template policy is available, Gate 3 may use `single_researcher` for a custom local review, but that fallback must be explicit in the protocol record and fixture.

Method packs may require stricter policy than `single_researcher`. The default must never silently weaken a pinned method pack's declared policy.

### 9. Approval supersession and withdrawal

Approval records are immutable.

An approval cannot be edited or deleted. If an approval was made in error, a new approval record with `decision = withdrawn` and `supersedes_approval_id` may be added. Withdrawal does not mutate the original approval; it changes whether that approval may satisfy a policy for future transitions.

When protocol content changes, old approvals become stale because they bind the old digest. A new version requires new approval records.

### 10. Post-approval mutation

After the approval policy is satisfied and a `ProtocolVersion` is approved:

- protocol content must not be mutated in place;
- decisions must not be overwritten;
- waivers must not be added to the approved version;
- unresolved decisions must not be silently removed;
- status may only move through explicit lifecycle transitions such as supersession or withdrawal.

Any scientific change requires an amendment and a new version.

### 11. Rejections remain evidence

`rejected` and `changes_requested` records do not approve a version, but they remain audit records tied to the target digest and actor.

They may be used to explain why a draft returned from review to draft state, but they do not mutate the approved content.

### 12. Waiver and deviation approval boundaries

Waivers and deviations use the same actor, timestamp, digest-binding, and policy validation principles as protocol approval, but their target type is not `protocol-version`.

Gate 3 implementation must not allow a waiver or deviation approval to mutate an approved protocol version. A waiver that changes planned conduct is protocol content before approval. A deviation that records actual conduct is linked evidence after approval.

### 13. Domain error categories

Gate 3 implementation must expose stable error categories for:

- `unauthorized-approval`
- `missing-approval-actor`
- `non-human-approval-actor`
- `stale-content-digest`
- `approval-target-mismatch`
- `same-actor-dual-approval`
- `insufficient-approval-policy`
- `automation-cannot-approve`
- `post-approval-mutation`

## Consequences

- `CF-008` has an accepted local Gate 3 protocol approval semantics decision. Implementation and conformance fixture closure remain pending.
- Workflow, AI, plugin, and institutional approval engines may still require later ADRs or implementation gates.
- Method packs can require stricter approval than the custom local default.
- Protocol approval is deterministic and reconstructable because it binds actor, timestamp, version id, and exact content digest.

## Fixture Effect

Gate 3 implementation must add fixture-backed cases for:

- single approval accepted for an explicit custom local policy
- dual-independent approval accepted only with two distinct actors
- dual-independent approval rejected for same actor twice
- approval rejected for stale digest
- approval rejected when an `approval-record` digest is supplied where `protocol-content` is required, or the reverse
- approval rejected for wrong version id
- approval rejected for missing or non-human actor
- approval withdrawal preserving the original approval record
- post-approval mutation rejected
- automation proposal rejected as an approval authority

## Explicit Claims Not Made

- no blueprint approval conformance claim
- no institutional role-engine implementation
- no workflow gate implementation
- no AI governance parity claim
- no PHP compatibility claim
- no persistence or API schema commitment
