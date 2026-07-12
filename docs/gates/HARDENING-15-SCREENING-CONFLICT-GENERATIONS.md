# Hardening 15: Screening Conflict Generations

Status: accepted and implemented locally

## Goal

Derive Screening conflicts consistently from the append-only decision log across adjudication and later human disagreement.

## Invariants

- conflict identity includes the exact current source-decision set;
- resolved conflicts remain immutable history;
- each stable candidate/stage/criteria conflict key has at most one open generation;
- adjudication establishes the baseline for the next generation;
- later disagreement with that adjudication creates a new open generation;
- unresolved regenerated conflicts block downstream screening stages;
- adjudication still requires the exact source decisions of the open conflict.

## Evidence

- unit tests cover generation 2 after adjudication and downstream blocking;
- conformance replay extends the accepted human-adjudication fixture with later disagreement;
- existing conflict creation, resolution, and source-link tests remain green.

## ADR Impact

This implements the stable-key plus generation option allowed by ADR 0013. No new ADR is required.
