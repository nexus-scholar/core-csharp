# Hardening 09: Deduplication Rehydration

Status: accepted and implemented locally

## Goal

Provide an explicit verified Deduplication result boundary that rejects fabricated result DTOs before downstream scientific use.

## Enforced Invariants

- schema, policy, result identity, and finite threshold;
- unique candidate, cluster, and evidence identities;
- valid evidence and review-pair endpoints;
- non-overlapping cluster membership and member representative;
- finite evidence, representative, and review scores;
- unresolved candidates are no-ID members of the raw candidate set;
- deep immutable verified snapshots.

Metadata enrichment and representative completeness changes remain Phase 3.

## Verification

Run focused Deduplication and conformance tests, architecture tests, full solution tests, formatting, repository verification, and hosted CI.
