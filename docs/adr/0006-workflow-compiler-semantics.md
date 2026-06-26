# ADR 0006: Workflow Compiler Semantics

Status: Accepted

Date: 2026-06-26

## Context

The current `NexusScholar.Workflow` scaffold compiles every protocol into one hardcoded five-node search flow. That is insufficient for Gate 4 because workflow behavior must come from approved protocol content and schema-closed workflow templates.

`ADR 0003` defines approved protocol versions and immutable protocol content. `ADR 0004` defines protocol approval semantics. `ADR 0005` defines the local workflow template contract. This ADR defines local Gate 4 compiler semantics without implementing source code in this planning branch.

## Decision

### 1. Compile input

Workflow compilation input is:

- an approved `ProtocolVersion`;
- a schema-closed workflow template as defined by `ADR 0005`;
- explicit non-scientific compile parameters declared by the template;
- resolved input source material from approved protocol content;
- optional approved waiver records already present in protocol content;
- optional amendment and invalidation notice records, or their source digests, when compiling an amended protocol version with invalidation planning;
- the template digest;
- the protocol content digest;
- a deterministic compiler version;
- fixed ids and clock values supplied by the caller or fixture harness when needed.

The compiler must reject draft, ready-for-review, withdrawn, or superseded protocol versions unless a later ADR explicitly defines a superseded-version replay mode. Gate 4 normal compilation starts only from `ProtocolStatus.Approved`.

Explicit compile parameters cannot satisfy scientific conduct inputs. Any compile parameter that attempts to provide a conduct-affecting value required by the protocol or template must be rejected unless the same value is already bound to approved protocol content, an approved waiver, or an approved amendment context.

### 2. Compile output

Compilation output is a workflow definition carrying:

- `workflow_id`
- `workflow_digest`
- `compiler_id`
- `compiler_version`
- `protocol_id`
- `protocol_version_id`
- `protocol_version_number`
- `protocol_content_digest`
- `template_id`
- `template_version`
- `template_digest`
- resolved input bindings
- compiled nodes
- compiled edges
- approval requirements
- capability requirements
- artifact declarations
- invalidation plan entries

The output is a planned workflow graph. It is not a provenance ledger, workflow execution record, bundle manifest, persistence schema, or UI state.

### 3. Workflow identity

`workflow_id` is deterministic and authoritative.

For Gate 4 local scope, `workflow_id` is derived from canonical workflow identity material:

- `protocol_id`
- `protocol_version_id`
- `protocol_content_digest`
- `template_id`
- `template_version`
- `template_digest`
- `compiler_id`
- `compiler_version`

The rendered local id format is:

```text
workflow-{first-16-lowercase-hex-of-sha256(identity-material)}
```

`workflow_id` is included in the compiled workflow digest. A supplied or serialized workflow id that does not match the deterministic identity material is invalid.

### 4. Resolved input bindings

Every required input produces a resolved input binding in the compiled output. A resolved input binding carries:

- `input_id`
- `input_kind`
- `schema_id`
- `schema_version`
- `source_type`
- `source_ref`
- `source_digest`
- `value_digest`
- optional `waiver_id`
- optional `amendment_id`

Allowed `source_type` values are:

- `protocol-decision`
- `protocol-value`
- `protocol-waiver`
- `protocol-amendment`
- `compile-parameter`
- `template-default`

`compile-parameter` and `template-default` are valid only for `execution_parameter` inputs. Scientific conduct inputs must bind to approved protocol, waiver, or amendment sources. Resolved input bindings are included in the workflow digest.

### 5. Deterministic node ids

Compiled node ids are deterministic.

For Gate 4 local scope, a compiled node id is derived from the template node id and compile context, not from runtime object identity or wall-clock state. The stable logical id format is:

```text
{template_node_id}
```

If a future template allows expansion into repeated nodes, the expansion suffix must be deterministic and ordered from canonical input values. Random ids are forbidden in compiled workflow graph identity.

### 6. Deterministic ordering

Compiled output ordering is deterministic:

- nodes ordered topologically, then by `node_id` ordinally when multiple nodes are available;
- edges ordered by `from_node_id`, then `to_node_id`, then condition text when present;
- approval requirements ordered by `approval_requirement_id`;
- capability requirements ordered by `capability_ref`;
- artifact declarations ordered by `artifact_ref`;
- resolved input bindings ordered by `input_id`;
- invalidation plan entries ordered by affected node id, then affected artifact ref, then required action.

Array order remains semantic. Any logically unordered template collection must be normalized to the deterministic order above before digesting.

### 7. Duplicate node rejection

The compiler must reject duplicate template node ids and duplicate compiled node ids. Duplicate ids are domain errors, not last-write-wins maps.

### 8. Missing dependency rejection

The compiler must reject any edge endpoint or node dependency that references an unknown node id. Self-edges are invalid. Missing dependencies are validation failures before digest computation.

### 9. Cycle rejection

The compiler must reject dependency cycles. A workflow graph must be acyclic for Gate 4 local scope. Parallel branches are allowed only when they do not create cycles and are deterministically ordered.

### 10. Hybrid mode semantics

Gate 4 resolves `CF-007` locally with a strict `hybrid` rule:

- `hybrid` means human-directed work may use automation, plugin capability, or AI proposal support;
- a hybrid node must declare at least one capability requirement and one human approval gate or human review gate as defined by `ADR 0005`;
- automation, plugins, and LLMs may produce proposals, checks, transformations, or warnings;
- the human actor remains responsible for the scientific decision or approval;
- a hybrid node without explicit capability requirements is invalid;
- a hybrid node without explicit human review or approval semantics is invalid.

Gate 4 does not execute AI tasks or plugins. It only compiles the required capability and approval boundaries into the workflow graph.

### 11. Approval node semantics

Approval nodes are workflow gates. They reference approval requirements from `ADR 0005` and inherit the human-authority constraints from `ADR 0004`:

- approval authority must identify a human actor;
- automation cannot approve;
- `allows_automation` must be false for approval authority;
- required roles and minimum approvals are part of the compiled requirement;
- approval records are execution-time audit records and are outside the compiled workflow digest.

Workflow approval nodes do not approve protocol versions. Protocol approval remains governed by `ADR 0004`; workflow approval nodes govern whether workflow conduct may proceed.

### 12. Waiver node semantics

Waiver-sensitive nodes must reference a waiver policy. A requirement can be waived only when:

- the template marks the requirement as waivable;
- the approved protocol content contains a matching waiver or the compile input explicitly includes a waiver authorized by protocol content;
- the waiver carries disclosure and consequence metadata.

The compiler must reject expired waivers, waiver affected-requirement mismatches, missing waiver approval bindings, stale waiver authority, and missing waiver disclosure or consequence metadata.

A waiver affects workflow planning but does not mutate the approved protocol digest. Missing or unauthorized waivers are compile validation failures.

### 13. Invalidation plan semantics

Static invalidation policies from the template define potential downstream effects. Actual invalidation plan entries require amendment and invalidation notice source records, or source digests, in compile input.

When compiling an amended protocol version with invalidation planning, the compiler must bind:

- `amendment_id`
- `amends_version_id`
- `produces_version_id`
- `previous_content_digest`
- invalidation notice ids
- affected requirement ids
- affected artifact digests
- affected workflow node ids
- required actions

The compiler must reject missing invalidation notice source, stale notice digest, affected artifact mismatch, and affected node references not present in the compiled workflow.

### 14. Protocol-version binding

Every compiled workflow binds to exactly one approved protocol version:

- `protocol_id`
- `protocol_version_id`
- `protocol_version_number`
- `protocol_content_digest`

The compiler must reject inputs whose protocol identity or digest does not match the approved version content. Recompilation after a protocol amendment produces a new workflow definition bound to the new protocol version.

### 15. Output workflow digest

The compiled workflow digest uses:

- `scope = canonical-json-record`
- `schemaId = nexus.workflow-definition`
- `schemaVersion = 1.0.0`

The digest content includes:

- workflow id;
- compiler id and version;
- protocol id, version id, version number, and protocol content digest;
- template id, template version, and template digest;
- resolved input bindings;
- compiled nodes;
- compiled edges;
- approval requirements;
- capability requirements;
- artifact declarations;
- invalidation plan entries;
- amendment and invalidation source digests when invalidation planning is present.

### 16. Outside workflow digest

The workflow digest excludes:

- workflow execution records;
- approval records produced during workflow execution;
- provenance events;
- artifact bytes and artifact storage paths;
- bundle manifests and checksums;
- UI state;
- generated summaries, wiki projections, caches, embeddings, and search indexes;
- live provider responses;
- machine-local file paths;
- runtime object identity;
- wall-clock compile time unless a future schema explicitly makes it authoritative.

### 17. Workflow error categories

Gate 4 implementation must expose stable workflow error categories for negative fixtures. At minimum:

- `invalid-protocol-status`
- `stale-protocol-digest`
- `stale-template-digest`
- `missing-required-input`
- `conduct-input-from-compile-parameter`
- `duplicate-node-id`
- `unknown-edge-endpoint`
- `unknown-node-requirement`
- `self-edge`
- `dependency-cycle`
- `missing-schema-id`
- `unknown-schema-id`
- `missing-schema-version`
- `undeclared-produced-artifact`
- `unknown-producing-node`
- `unknown-capability-reference`
- `unknown-approval-role`
- `automation-approval-authority`
- `invalid-hybrid-node`
- `invalid-waiver`
- `missing-invalidation-source`
- `stale-invalidation-notice`
- `affected-artifact-mismatch`
- `affected-node-not-found`

Tests must not rely only on free-form exception messages.

### 18. Future work

The following remain future work outside Gate 4 planning:

- source code implementation;
- workflow execution state;
- provenance ledger append semantics;
- artifact hashing and storage;
- bundle export/import;
- plugin host and capability grant runtime;
- AI task execution and governance records;
- persistence, API, CLI, UI, or cloud sync;
- PHP compatibility;
- blueprint conformance.

## Consequences

- `CF-003` is resolved for Gate 4 planning by replacing the hardcoded compiler target with deterministic template-driven semantics.
- `CF-007` is resolved for Gate 4 planning by giving `hybrid` a human-bounded meaning.
- Workflow approval handoff from `ADR 0004` is explicit: protocol approvals approve protocol versions; workflow approval nodes gate conduct and cannot be satisfied by automation.

## Fixture Effect

Gate 4 fixtures must cover:

- deterministic rapid-review workflow compilation;
- deterministic AI-audit or hybrid workflow compilation with explicit human review gate;
- resolved input bindings included in workflow digest;
- conduct input supplied only by compile parameter rejection;
- workflow id mismatch rejection;
- workflow id included in digest differential;
- duplicate node rejection;
- unknown edge endpoint rejection;
- unknown node requirement rejection;
- self-edge rejection;
- cycle rejection;
- draft protocol rejection;
- ready-for-review protocol rejection;
- withdrawn protocol rejection;
- superseded protocol rejection for normal compile mode;
- missing required input rejection;
- missing schema id rejection;
- unknown schema id rejection;
- missing schema version rejection;
- hybrid node without capability requirement rejection;
- hybrid node without human review or approval rejection;
- automation approval rejection;
- waiver without waiver policy rejection;
- expired waiver rejection;
- waiver affected-requirement mismatch rejection;
- missing waiver approval binding rejection;
- unauthorized waiver rejection;
- undeclared produced artifact rejection;
- unknown capability reference rejection;
- missing invalidation notice source rejection;
- stale invalidation notice digest rejection;
- affected artifact mismatch rejection;
- affected node not present rejection;
- deterministic ordering permutation with same digest;
- digest exclusion differential pair;
- digest inclusion differential pair;
- stale protocol digest rejection.

## Explicit Claims Not Made

- no source code implementation
- no blueprint conformance claim
- no PHP compatibility claim
- no workflow execution engine
- no provenance ledger implementation
- no artifact storage implementation
- no plugin runtime
- no AI task execution or AI governance parity claim
- no persistence, API, CLI, UI, or bundle export commitment
