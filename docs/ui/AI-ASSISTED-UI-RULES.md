# AI-Assisted UI Rules

AI can make Nexus Scholar easier to use, but it must remain inside the authority boundary. A model may suggest, explain, draft, and summarize. It must not silently mutate authoritative scientific records.

## Safe AI Roles

AI may:

- draft protocol decisions for human review;
- suggest search queries and query variants;
- map imported file columns to candidate fields;
- explain validation errors;
- summarize search traces;
- explain parser warnings;
- suggest duplicate candidates;
- explain identifier overlap and title-similarity evidence;
- draft screening rationales;
- summarize conflicts and uncertainty;
- explain bundle verification results;
- produce typed block plans for renderer-neutral display when a trusted application layer validates the plan.

These outputs are proposals, explanations, drafts, or suggested commands. They do not become final scientific decisions by themselves.

## Unsafe AI Roles Without Human Approval

AI must not:

- approve protocols;
- approve amendments;
- merge duplicates finally;
- make final screening decisions;
- overwrite raw evidence;
- delete source sightings;
- claim PHP compatibility;
- claim bundle verification passed without actual verification;
- scrape restricted sources;
- silently mutate authoritative records;
- convert app projections into Core authority;
- accept its own suggestions;
- bypass validation errors.

## Allowed AI Output Types

Allowed UI-facing AI output types:

- `proposal`
- `explanation`
- `draft`
- `suggested_command`
- `typed_block_plan`

Each output should preserve evidence references where available. When prompt and response digests are available, they should be preserved as evidence for the proposal, not as scientific authority.

## Rejected Output Type

Arbitrary AI-generated XAML or C# mutation is rejected.

AI may suggest a UI concept or produce a typed block plan for a controlled renderer, but it should not generate unreviewed code that mutates Core, renderer packages, or scientific state. Code changes follow normal implementation review, tests, and ADR boundaries.

## UI Language

Use labels such as:

- "AI suggestion"
- "Draft rationale"
- "Suggested merge candidate"
- "Explanation"
- "Needs human decision"

Avoid labels such as:

- "AI approved"
- "Automatically verified"
- "Confirmed duplicate" for fuzzy/no-id evidence;
- "Final decision" for model output;
- "Compatible with PHP" without generator-backed fixtures and comparator evidence.

## Action Boundary

The UI should separate these steps:

```text
AI output shown -> user inspects evidence -> user chooses an allowed action -> Core validates -> accepted action records authority/provenance
```

No UI path should connect an AI proposal directly to a Core mutation without explicit human acceptance where acceptance is valid.
