# Hardening 11: Full Text Authority

Status: accepted and implemented locally

## Goal

Complete Phase 2 by making persisted Full Text records untrusted until the input, acquisition, attempt history, artifact metadata, and exact bytes are validated as one authority chain.

## Invariants

- raw input, acquisition, and artifact constructors are internal to compatibility tests;
- public input factories allow only supported source kinds and eligibility states;
- `FromBytes` rejects an acquisition belonging to a different input;
- rehydration requires exact input, candidate set, candidate, acquisition kind, source, and artifact bindings;
- successful artifact authority requires contiguous attempts ending in success;
- accepted bytes are revalidated for format, media type, maximum size, exact size, digest, and digest scope;
- successful rehydration returns an explicit `VerifiedFullTextChain`.

## Deferred

- shared logical-path validation is Phase 3/4 integration work;
- one explicit extracted-text representation remains a Phase 3 contract decision;
- live retrieval, provider SDKs, OCR, PDF extraction, persistence, and PHP compatibility claims remain out of scope.
