# Hardening 16: Full Text Residual Contracts

Status: accepted and implemented locally

## Goal

Complete Phase 3 by closing Full Text logical-path and extracted-representation cross-record validation.

## Invariants

- optional Full Text logical paths use the ADR 0009 Artifacts validator;
- paths remain projections and cannot contain traversal, absolute, URI, drive, or backslash forms;
- verified extraction binds the exact source artifact id, raw digest, and digest scope;
- successful and partial extraction carries exactly one representation: `page-text` or `sections`;
- failed and skipped extraction carries no text representation;
- extracted-text digest uses `canonical-json-record` over representation kind and ordered values;
- dual, missing, mismatched, or digest-inconsistent representations are rejected.

## Evidence

- unit tests cover traversal rejection, verified source binding, mismatch rejection, and dual representation rejection;
- Full Text conformance fixtures now use canonical representation digests;
- architecture tests enforce the accepted Kernel plus Artifacts dependency direction.

## Deferred

- live retrieval, OCR, PDF parsing, persistence, filesystem writes, and PHP compatibility remain out of scope.
