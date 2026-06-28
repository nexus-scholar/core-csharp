# Dedup Review Workspace v0

Deduplication is the best first serious workflow prototype because it makes Nexus Scholar's strictness visible and useful. It uses imported/search evidence, duplicate evidence, human review boundaries, and provenance preview without requiring full Screening or full-text implementation first.

This document is a workspace concept, not a final schema.

## Purpose

Help a researcher review duplicate candidates safely:

- preserve raw source sightings;
- show exact identifier evidence separately from fuzzy/title-only evidence;
- explain why a case requires review;
- allow human merge decisions;
- preview what authority/provenance will be recorded;
- avoid silent auto-merge for weak evidence.

## Required Blocks

### CandidateClusterBlock

Shows a cluster or candidate group. It should distinguish:

- automatic exact-identifier clusters;
- review-required fuzzy/title candidates;
- no-id candidates;
- source-specific-id-only candidates;
- unresolved candidates.

### RecordComparisonBlock

Compares candidate records:

- title;
- authors;
- year;
- abstract;
- stable identifiers;
- source-specific identifiers;
- provider/import source;
- parser warnings;
- raw evidence availability.

Desktop can render side by side. Mobile can stack. CLI can number records and fields.

### IdentifierOverlapBlock

Explains exact identifier evidence:

- DOI with DOI;
- OpenAlex with OpenAlex;
- Semantic Scholar with Semantic Scholar;
- arXiv with arXiv;
- PubMed with PubMed.

It should not promote source-specific identifiers into stable identity.

### TitleSimilarityBlock

Explains title-based candidate evidence:

- normalized title;
- score;
- threshold;
- policy id/version;
- why review is required.

Fuzzy/title-only evidence is review-required and must not silently auto-merge.

### SourceSightingsBlock

Shows all preserved sightings:

- search trace ids;
- search sighting ids;
- import source ids;
- import record ids;
- source file digest when available;
- raw record digest when available.

### AIExplanationBlock

AI may explain why records look similar, why an identifier conflict matters, or why Core requires review. The output is explanatory only. It cannot approve the merge.

### MergeDecisionGate

Requires an identified human action for review-required cases.

Actions:

- accept merge;
- reject merge;
- mark unresolved;
- request more evidence;
- open raw evidence.

The action should show what will be recorded and which Core command or future app command it maps to.

### ProvenancePreviewBlock

Shows the user what record, decision, or provenance event would be created if the action succeeds. It should include actor requirement, evidence refs, policy refs, and non-claims.

## Beginner Rendering

Beginner mode should say:

- "These records may describe the same work."
- "The title is similar, but there is no stable identifier match, so you must review it."
- "Accepting merge will record your decision and keep the source evidence."

It should show enough detail to make a decision without overwhelming the user.

## Audit Rendering

Audit mode should show:

- candidate ids;
- evidence edge ids;
- policy id and version;
- fuzzy threshold;
- source bindings;
- digests;
- warning categories;
- representative projection details;
- exact action payload preview.

## Non-Claims

This workspace must not claim:

- fuzzy title evidence is scientific identity;
- no-id candidates can auto-merge;
- source-specific ids are stable identifiers;
- AI explanation is a merge decision;
- app display grouping is Core authority;
- PHP compatibility without generated fixtures.
