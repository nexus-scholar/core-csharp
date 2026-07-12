# Hardening 14: Deduplication Representative Metadata

Status: accepted and implemented locally

## Goal

Preserve imported scholarly metadata through candidates, representative election, merge projection, and verified rehydration.

## Invariants

- candidates retain authors, year, venue, abstract, and keywords from Search import records;
- completeness scoring uses explicit metadata weights in addition to stable identifiers;
- populated elected fields are not overwritten;
- missing elected fields are filled deterministically from the next-ranked member;
- evidence, identifiers, sightings, notices, and raw digests remain preserved;
- rehydration rejects blank author and keyword entries;
- representative metadata collections are immutable snapshots.

## Evidence

- unit tests cover metadata-driven election and missing-field fill behavior;
- a canonical-digest conformance fixture proves rich imported metadata survives representative projection;
- no Search, Screening, persistence, or compatibility authority is added.

## ADR Impact

This implements the accepted representative election and merge projection rules in ADR 0012. No new ADR is required.
