# Hardening 10: Screening Authority

Status: accepted and implemented locally

## Goal

Close public Screening construction around verified Protocol and Deduplication authority, validated human actors, and finite confidence values.

## Invariants

- the public service constructor requires a verified approved Protocol version and verified Deduplication result;
- criteria Protocol version ID, content digest, digest scope, and approved status match resolved authority;
- candidate sets are created locked from the verified Dedup result;
- `ScreeningActor` has no public constructor;
- NaN and infinite confidence are rejected;
- raw compatibility construction is internal to test assemblies.

Conflict regeneration and typed Full Text evidence resolution remain Phase 3 and the Full Text authority gate respectively.
