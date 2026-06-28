# UI Philosophy

Nexus Scholar should feel simple to use because the strictness is well translated, not because the strictness is hidden. The product goal is a research cockpit for evidence-bearing work: search, import, deduplication, screening, full-text handling, reporting, bundles, and AI-assisted operations that remain reconstructable.

## Strict Inside, Simple Outside

Core should remain deterministic and audit-grade:

- canonical JSON and digest-bound records;
- stable scientific identities instead of runtime object identities;
- append-only provenance and decision history;
- preserved raw search/import evidence;
- explicit validation categories;
- no silent deduplication or silent scientific mutation.

The UI should translate those rules into understandable workflows:

- explain what blocked an action;
- show why a warning matters;
- expose the evidence behind a recommendation;
- use beginner language by default;
- preserve audit detail for advanced review.

Simple does not mean permissive. A simple UI can still stop the user when Core would reject an action. It should make the rejection understandable and show the next valid step.

## AI-Assisted, Not AI-Authoritative

AI may help the researcher understand, draft, compare, summarize, and repair. AI must not become scientific authority by default.

Safe AI roles include proposing search queries, explaining parser warnings, summarizing raw traces, suggesting duplicate candidates, drafting screening rationales, and explaining bundle verification results. These outputs remain proposals, explanations, or drafts until an authorized human action accepts them where acceptance is permitted.

The UI should label AI outputs as proposals and show the evidence they depend on. It should avoid language that implies the model has approved a protocol, merged duplicates, made final screening decisions, or verified compatibility.

## Human-Authorized Science

The operating loop is:

```text
AI proposes -> Core validates -> Human approves -> Provenance records -> Bundle exports
```

Human approval must identify the actor, the action, the evidence, and the accepted content. A model suggestion is not enough. A generated narrative is not enough. A UI convenience action is not enough unless it maps to a valid Core command and records the appropriate decision or provenance.

## Translate Strictness Instead Of Hiding It

Audit-grade systems often fail at the product layer by making strictness feel like internal error handling. Nexus Scholar should instead show strictness as research support:

- a digest is a stability guarantee, not random technical noise;
- a parser warning is a reproducibility warning, not a nuisance;
- a human gate is scientific protection, not a UI obstacle;
- a preserved raw record is evidence, not clutter.

The UI should use progressive disclosure. A beginner can see "This imported row has no stable identifier, so it needs review." An auditor can expand the same block to inspect source file digest, raw record digest, parser warning code, policy id, and provenance links.

## Workspace-First, Not Chat-First

Nexus Scholar should not become a pure chat app. Chat is useful as a side assistant, but the primary experience should be a structured adaptive workspace. Research work has objects, stages, decisions, evidence, conflicts, bundles, and replay. These are better represented as typed workspaces and interaction blocks than as an unstructured conversation.

The right-side assistant can explain, suggest, and draft. The center workspace should remain the place where validated actions happen, evidence is inspected, and human decisions are recorded.
