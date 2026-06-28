# Screening Workspace v0

Screening should be designed early but implemented after the candidate-set and decision contracts are ready. The key boundary is that AI may draft rationales, but final inclusion/exclusion decisions are human-authorized.

This document is a workspace concept, not an implementation plan for the current task.

## Purpose

Support human screening over a locked or reviewable candidate set:

- show candidate evidence;
- display criteria and criteria digest;
- preserve AI/rule suggestions as proposals;
- record human include, exclude, or needs-review decisions;
- expose conflicts and adjudication needs;
- block downstream progression when unresolved conflicts exist.

## Blocks

### ScreeningCard

Shows:

- candidate title;
- abstract;
- source evidence;
- dedup cluster or representative reference when available;
- current stage;
- prior decisions or suggestions.

### CriteriaChecklistBlock

Shows inclusion and exclusion criteria for the current stage. It should display beginner text by default and expose criteria id, version, stage, and digest in audit mode.

### EvidenceSummaryBlock

Summarizes evidence used for the screening decision:

- title/abstract fields;
- source sightings;
- import/source refs;
- raw evidence availability;
- dedup representative or unresolved-candidate status.

### AIRationaleDraftBlock

AI may draft a rationale using the candidate evidence and criteria. The draft must be labeled as a draft. It can be edited or rejected by the human reviewer.

### HumanScreeningDecisionBlock

Captures final human decision:

- include;
- exclude;
- needs_review.

It must require an identified human actor, rationale, criteria digest, candidate id, stage, and evidence refs according to the accepted Screening contract.

### ConflictWithProtocolBlock

Shows when a proposed decision conflicts with criteria, protocol binding, candidate-set status, or stage requirements.

## AI Boundary

AI may:

- draft rationale text;
- explain criteria;
- summarize evidence;
- highlight uncertainty;
- suggest `needs_review`.

AI must not:

- create final include/exclude decisions;
- resolve reviewer conflicts;
- bypass criteria digest mismatches;
- advance candidates downstream without human authority;
- turn raw Search traces into screenable candidate identity.

## Beginner Rendering

Beginner mode should guide the reviewer through:

- what the current candidate is;
- what criteria apply;
- what evidence is available;
- what uncertainty remains;
- what decision is allowed.

## Audit Rendering

Audit mode should expose:

- candidate set id and digest;
- criteria schema, version, and digest;
- stage;
- actor;
- evidence refs;
- source suggestion ids;
- conflict ids;
- append-only decision history.

## Open Implementation Boundary

This workspace should not be implemented until the relevant Core Screening behavior and fixtures exist. App assignment queues, batch status, web rows, and full-text item links remain projections unless future ADRs admit them as Core records.
