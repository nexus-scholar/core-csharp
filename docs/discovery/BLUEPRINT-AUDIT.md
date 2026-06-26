# Blueprint Audit

Status: Gate 0 discovery only. This document records blueprint evidence and conflicts. It does not adopt the blueprint as an authoritative repository spec.

## Inputs Read

- `specs/SOURCE.lock.json`
- `../nexus-scholar-2-blueprint/README.md`
- `../nexus-scholar-2-blueprint/spec/NEXUS-WORKFLOW-TEMPLATE-v1.md`
- `../nexus-scholar-2-blueprint/spec/NEXUS-REVIEW-BUNDLE-v1.md`
- `../nexus-scholar-2-blueprint/spec/schemas/*.json`
- `../nexus-scholar-2-blueprint/contracts/*.cs`
- `../nexus-scholar-2-blueprint/templates/*.yaml`
- `../nexus-scholar-2-blueprint/conformance/validation-report.md`
- `../nexus-scholar-2-blueprint/migration/PHP-TO-CSHARP-CONFORMANCE.md`

## Verdict

The sibling blueprint is a strong discovery input, but it is not yet a safe authoritative port contract. The main problem is drift between:

1. Narrative markdown specs.
2. JSON schemas.
3. C# contract sketches.
4. Template files.

Gate 0 should therefore treat the blueprint as a planning source, not as silently adopted behavior.

## Inventory

### Declared platform modules

The blueprint README declares these logical modules:

- `Kernel`
- `Protocol`
- `Workflow`
- `Artifacts`
- `Provenance`
- `Extensibility`
- `AI`
- `Search`
- `Corpus`
- `Screening`
- `Extraction`
- `Appraisal`
- `Synthesis`
- `Reporting`
- `Bundles`
- local and cloud persistence
- plugin host
- API, worker, CLI, desktop, and web hosts

### Public contract surfaces

- Workflow template narrative spec, schema, and `WorkflowContracts.cs`
- Review bundle narrative spec and manifest schema
- Review protocol schema and example
- Plugin manifest schema, example, and `PluginContracts.cs`
- AI task policy schema, example, and `AiContracts.cs`

## Blocking Audit Findings

### 1. Workflow/template drift

- The workflow spec says `ai_assisted` and `hybrid` nodes must declare AI task and approval behavior.
- Multiple templates use `mode: hybrid` with only `plugin_capability`.
- `WorkflowContracts.cs` does not model several spec concepts as first-class fields, including waiver, `on_complete`, reporting mappings, and the full `requires` shape.

Impact:
Workflow compilation cannot be treated as settled until the spec, schema, and contract sketch agree.

### 2. Missing schema closure

- Templates reference many schema ids that do not exist under `spec/schemas/`.
- Examples include review intent, search strategy, search manifest, corpus snapshot, screening decision, extraction record, appraisal record, synthesis plan, and AI output schemas.

Impact:
Template conformance is incomplete. Gate 0 cannot treat those schema ids as implemented contracts.

### 3. Artifact registry drift

- Templates produce artifacts that are not declared in the template `artifacts:` blocks.
- Examples include `method.selection`, `question.specification`, `search.approval`, `search.raw-results`, `dedup.clusters`, and `audit.validation-report`.

Impact:
Produced outputs are not fully typed or registry-backed, which breaks audit-grade planning.

### 4. Plugin contract drift

- The plugin schema requires execution details and data-handling policy.
- `PluginContracts.cs` does not round-trip those fields directly.

Impact:
The extensibility contract is discovery-only until schema-vs-contract authority is resolved.

### 5. AI contract drift

- The AI schema is explicit about context egress, redaction, tool approval, provider policy, retention, and evaluation invalidation.
- `AiContracts.cs` collapses much of that into policy identifiers instead of schema-closed fields.

Impact:
Governed AI cannot claim conformance from the current contract sketch alone.

### 6. Bundle audit surface drift

- The bundle spec requires protocol decisions, approvals, amendments, workflow instances, ledger events, AI records, and plugin records.
- Only a small subset has concrete schemas.
- The protocol schema models `decisions` and `approvals` only as UUID arrays.

Impact:
The bundle is not yet a closed exchange contract for the full audit surface.

## Hidden Defaults And Audit Risks

- `ApprovalPolicy` defaults can weaken `dual_independent` behavior unless another layer hardens them.
- Bundle `canonicalization` and `digest_algorithm` are schema defaults instead of required emitted values.
- The workflow schema is permissive in high-risk areas through repeated `additionalProperties: true`.
- The conformance report states only partial example validation and structural checks; it is not a proof of semantic closure.
- All 18 method-pack entries remain `draft`.

## Gate 0 Adoption Rule

Use the blueprint in Gate 0 only for:

- module discovery,
- contract discovery,
- fixture planning,
- conflict extraction,
- gate sequencing.

Do not use it yet as authority for:

- exact workflow compiler semantics,
- exact bundle serialization semantics,
- plugin round-trip compatibility,
- AI policy round-trip compatibility,
- full template conformance claims.

## Decisions Needed Before Gate 1+

### Owner: Spec owner + architecture owner

- Choose the authority order when blueprint markdown, JSON schema, and contract sketches disagree.

### Owner: Workflow owner

- Define `hybrid` exactly, then align spec, schema, and templates.

### Owner: Artifact/schema registry owner

- Decide whether every produced artifact and AI output schema must exist in-pack before implementation gates continue.

### Owner: Governance owner

- Freeze the enforceable semantics for `dual_independent`, waiver approval, and institutional sign-off.

### Owner: Conformance owner

- Define what fixture assets must exist before blueprint conformance can be marked `PASS`.

### Owner: Porting owner

- Freeze which blueprint-only modules are discovery-only versus within the PHP compatibility lane.
