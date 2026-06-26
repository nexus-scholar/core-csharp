# ADR 0005: Workflow Template Contract

Status: Accepted

Date: 2026-06-26

## Context

Gate 4 needs a local workflow template contract so `NexusScholar.Workflow` can move beyond its prior hardcoded scaffold. Gate 0 found drift between blueprint markdown, JSON schemas, contract sketches, and templates. In particular, templates reference missing schema ids, produced artifacts are not always declared, and `hybrid` nodes are ambiguous.

The blueprint remains a discovery input. This ADR defines the local Gate 4 template contract without claiming blueprint conformance, PHP compatibility, persistence schema support, plugin execution, AI execution, or bundle export compatibility.

## Decision

### 1. Workflow template identity

A workflow template is identified by:

- `template_id`
- `template_version`
- `template_digest`
- `schema_id`
- `schema_version`

`template_id` is a stable logical identifier. `template_version` is the author-declared version of the template contract. `schema_id` and `schema_version` identify the template record shape used to validate and digest the template.

### 2. Template digest

`template_digest` uses the Kernel digest vocabulary from `ADR 0002`.

Gate 4 template digests use:

- `scope = canonical-json-record`
- `schemaId = nexus.workflow-template`
- `schemaVersion = 1.0.0`

The digest content includes all authoritative template fields: identity, version, inputs, nodes, edges, gates, artifact declarations, capability requirements, waiver policy references, invalidation policy references, and template-level metadata that affects compilation.

The template digest excludes transport metadata, local file paths, cache state, generated previews, fixture harness metadata, bundle checksums, and the digest value being computed.

### 3. Template version

`template_version` is immutable once a template is used to compile a workflow. Any semantic change to nodes, edges, gates, required inputs, produced artifacts, capability requirements, waiver policy references, invalidation policy references, or artifact declarations requires a new `template_version` and new `template_digest`.

### 4. Required inputs

A template declares required inputs before compilation. Each required input carries:

- `input_id`
- `input_kind`
- `schema_id`
- `schema_version`
- `required`
- optional `source_protocol_decision_key`
- optional `default_value`

`input_kind` is either `scientific_conduct` or `execution_parameter`.

Scientific conduct inputs affect review conduct and must be satisfied by approved protocol content, an approved protocol waiver, or an approved amendment/invalidation context. They cannot be supplied only as compile parameters.

Execution parameters tune non-scientific workflow planning details such as fixture labels, local execution hints, or deterministic expansion choices. They may be supplied as explicit compile parameters only when they are declared by the template and recorded as resolved input bindings in the compiled workflow.

Defaults are allowed only when they are part of the template digest and cannot silently replace a required human protocol decision.

### 5. Node shape

A workflow template node carries:

- `node_id`
- `kind`
- `mode`
- `label`
- `requires`
- `produces`
- `approval_requirement_ref`
- `capability_requirement_refs`
- `waiver_policy_ref`
- `invalidation_policy_ref`
- optional `condition`

`node_id` is stable within a template version. It is not a generated runtime id. `kind` classifies the planned work, for example human task, approval, automated task, milestone, or hybrid task. `mode` determines whether the node requires human-only conduct, automation assistance, plugin capability, AI proposal support, or hybrid orchestration.

### 6. Edge shape

A workflow template edge carries:

- `from_node_id`
- `to_node_id`
- optional `condition`

Edges define compile-time dependency order. Edges must reference existing node ids. Self-edges are invalid. Cycles are invalid.

### 7. Gate shape

A gate is an explicit transition requirement. A gate carries:

- `gate_id`
- `target_node_id`
- `policy_ref`
- `required_artifact_refs`
- `required_decision_refs`
- `required_actor_roles`

Gates are not UI hints. A gate changes whether downstream workflow nodes may proceed. Gate 4 may compile gates into approval nodes or node requirements, but it must not treat automation output as gate authority.

Human review gates are gates whose `policy_ref` points to a review requirement rather than an approval requirement. A review requirement must identify a human role, evidence inputs to inspect, and the downstream node it unlocks. Human review records are future execution records and are outside the compiled workflow digest; the requirement itself is inside the workflow digest.

### 8. Approval requirement reference

Approval requirements reference an approval policy by:

- `approval_requirement_id`
- `policy_id`
- `policy_version`
- `policy_mode`
- `required_roles`
- `minimum_approvals`
- `requires_distinct_actors`
- `allows_automation`

Workflow approval requirements reuse the human-authority principles from `ADR 0004`: automation may propose or validate, but cannot approve. `allows_automation` must be false for approval authority.

Workflow approval requirements are compile-time requirements. They are not protocol approval records and they do not mutate protocol versions.

### 9. Approval role registry

A workflow template declares its approval role registry. Each role carries:

- `role_id`
- `label`
- `authority_description`
- optional `method_pack_ref`

Every `required_actor_roles` entry in a gate and every `required_roles` entry in an approval requirement must reference a role declared in the template role registry. Unknown approval roles are validation failures.

Custom local roles are allowed only when declared in the template role registry and included in the template digest. Role text supplied only at compile time is not authoritative.

### 10. Produced artifacts

Each produced artifact declaration carries:

- `artifact_ref`
- `artifact_kind`
- `schema_id`
- `schema_version`
- `produced_by_node_id`
- `required_for_gates`
- optional `retention_class`

Every artifact listed in a node's `produces` set must appear in the template artifact declarations. Every artifact declaration must reference an existing producing node. Undeclared produced artifacts are invalid for Gate 4.

### 11. Capability requirements

A capability requirement carries:

- `capability_ref`
- `capability_kind`
- `required_scopes`
- `data_access_class`
- `egress_allowed`
- optional `plugin_capability`
- optional `ai_task_policy_ref`

Capability requirements declare what a node needs. They do not grant runtime credentials, execute plugins, call providers, or transfer data. Plugin isolation remains a capability boundary, not a security sandbox.

### 12. Waiver policy reference

A waiver policy reference carries:

- `waiver_policy_id`
- `waivable_requirement_refs`
- `approval_requirement_ref`
- `disclosure_mapping`
- `consequence_warning`

Only template requirements that explicitly list a waiver policy may be waived. A waiver that changes planned conduct must already be present in protocol content before workflow compilation or be rejected as missing authority.

At compile time, a waiver is valid only when:

- its affected requirement matches a template requirement or source protocol requirement;
- it is present in approved protocol content or an approved amendment context;
- it has not expired at the authoritative protocol approval timestamp or explicit compile reference timestamp;
- it carries disclosure mapping and consequence warning;
- it carries approval policy and approval ids required by the protocol waiver record.

### 13. Invalidation policy reference

An invalidation policy reference carries:

- `invalidation_policy_id`
- `affected_requirement_refs`
- `affected_artifact_refs`
- `affected_node_refs`
- `required_action`

Invalidation policy references map protocol amendments and invalidation notices into workflow planning. They do not mutate approved protocol versions or previously released workflow snapshots.

Static invalidation policies declare how a template reacts to changed requirements. Actual invalidation plan entries require compile input that identifies the amendment and invalidation notice source records or their source digests.

### 14. Artifact declaration rules

Template artifact declarations are schema-closed for Gate 4 local scope:

- every produced artifact must be declared;
- every declared artifact must have a schema id and schema version;
- every schema id used by a Gate 4 fixture must be present in the local fixture source refs or explicitly marked as a local Gate 4 placeholder schema;
- placeholder schemas do not create blueprint conformance claims;
- missing schema ids are template validation failures, not implementation TODOs to ignore.

### 15. Schema closure expectations

Gate 4 closes `CF-006` locally by requiring schema closure for any template compiled by this repository:

- no unknown `schema_id` is accepted in a Gate 4 authoritative fixture;
- no template may depend on implicit blueprint schemas absent from the fixture metadata;
- generated fixtures must list all source refs and schema refs used for compilation;
- blueprint schema gaps remain discovery findings and do not become local authority.

## Consequences

- `CF-006` is resolved for Gate 4 local scope by rejecting missing schema references instead of silently adopting incomplete blueprint templates.
- Gate 4 can implement deterministic workflow compilation against a closed local template contract.
- Template conformance remains local. Blueprint conformance and PHP compatibility remain non-claims.

## Fixture Effect

Gate 4 workflow fixtures must include template identity, template digest, schema refs, required inputs, nodes, edges, gates, produced artifact declarations, capability requirements, waiver policy references, and invalidation policy references.

Negative fixture cases must include missing schema id, missing schema version, undeclared produced artifact, artifact declaration with unknown producing node, missing required input, scientific conduct input supplied only by compile parameter, approval requirement that allows automation, invalid approval requirement, unknown gate policy, unknown gate artifact reference, unknown gate decision reference, explicit compile input requirement, waiver without waiver policy, expired waiver, waiver affected-requirement mismatch, waiver missing approval binding, and unknown capability reference.

## Explicit Claims Not Made

- no blueprint conformance claim
- no PHP compatibility claim
- no plugin runtime implementation
- no AI execution or AI governance parity claim
- no persistence, API, UI, or bundle export schema commitment
