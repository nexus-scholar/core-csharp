# Beginner Vs Audit Mode

Beginner and audit modes should render the same block plan with different levels of detail. They should not produce different scientific meaning.

## Shared Contract

The block contract should remain stable:

- same block id;
- same block type and version;
- same evidence refs;
- same validation categories;
- same allowed actions;
- same authority requirements.

The renderer chooses how much detail to show by default.

## DigestBadge

Beginner rendering:

- label: "Evidence locked"
- explanation: "This record has a content fingerprint so it can be checked later."
- action: "Show details"

Audit rendering:

- digest algorithm;
- digest scope;
- schema id;
- schema version;
- digest value;
- source record reference.

## ImportWarningBlock

Beginner rendering:

- "Some imported rows need attention before they can be trusted for later steps."
- show affected row count;
- group warnings by plain-language cause;
- offer "review rows" and "open raw evidence" actions.

Audit rendering:

- parser id/version;
- warning code;
- source file digest;
- source row or record id;
- raw record digest;
- mapped fields;
- validation category.

## DedupCandidateBlock

Beginner rendering:

- "These records may describe the same work."
- show title, year, source, identifier overlap, and a short explanation.
- actions: accept merge, keep separate, mark unresolved, request more evidence.

Audit rendering:

- candidate cluster id;
- member ids;
- evidence edge ids;
- exact identifier namespaces;
- title similarity score and threshold;
- policy id/version;
- source sightings;
- raw evidence refs;
- provenance preview.

## BundleVerificationBlock

Beginner rendering:

- "Bundle can be verified" or "Bundle has problems."
- show pass/fail summary and clear next steps.

Audit rendering:

- manifest id;
- manifest digest;
- artifact digests;
- missing or mismatched entries;
- verification categories;
- import/export round-trip notes.

## Rules

- Beginner mode can hide detail by default, but not hide blockers.
- Audit mode can expose technical fields, but not create new authority.
- Switching modes should not change available scientific actions.
- Generated explanations should cite the block evidence, not invent new claims.
