# Research Cockpit Concept

Nexus Scholar should become a structured research cockpit. The user should see where they are in the workflow, what evidence exists, what is blocked, what needs human judgment, and what the assistant can help explain or draft.

## App Shell

Proposed desktop shell:

- left workflow navigation;
- center adaptive workspace;
- right AI assistant;
- bottom or side evidence/provenance inspector.

The layout should make the current research task primary. Chat is a helper, not the whole product.

## Left Workflow Navigation

The left navigation should represent research stages:

- Project setup;
- Protocol;
- Search;
- Import;
- Deduplication;
- Screening;
- Full text;
- Reporting;
- Bundle export;
- Audit/replay.

Each stage can show status, blockers, warnings, and next required action.

## Center Adaptive Workspace

The center area renders block plans for the selected workflow step. It should adapt to the situation:

- import summary after a file parse;
- parser warnings when raw records need review;
- dedup candidate comparison when fuzzy evidence exists;
- screening cards when candidate sets are ready;
- bundle verification after export.

Blocks should include "why am I seeing this?" explanations. The answer should point to the triggering validation result, workflow step, or evidence condition.

## Right AI Assistant

The assistant can:

- explain the current block;
- summarize evidence;
- suggest next valid actions;
- draft rationale text;
- propose search strings;
- explain why Core rejected an action.

The assistant must not be the authority surface for final scientific decisions. Human gates stay in the workspace.

## Evidence And Provenance Inspector

The inspector provides evidence peel-back:

```text
simple explanation -> structured fields -> raw evidence -> digests/provenance
```

Example:

- simple: "This row has no stable identifier and needs review."
- structured: title, authors, year, source, parser warnings.
- raw: original import row or provider payload.
- audit: source file digest, raw record digest, evidence refs, provenance links.

## Beginner Mode

Beginner mode should emphasize:

- plain language;
- fewer simultaneous fields;
- clear warnings;
- guided next actions;
- explanations of audit concepts.

Beginner mode must not weaken Core validation or hide required human authority.

## Audit Mode

Audit mode should emphasize:

- schema ids and versions;
- digests;
- source refs;
- validation categories;
- policy ids and thresholds;
- provenance lineage;
- exact action payload previews.

Audit mode is for users who need to reconstruct, challenge, or export evidence.

## Workspace Principle

Every visible workflow action should answer:

- what record or evidence is affected;
- what Core rule applies;
- whether AI is involved;
- whether human authority is required;
- what will be recorded if the action succeeds.
