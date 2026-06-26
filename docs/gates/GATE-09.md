# Gate 9: Shared Identity Reconnaissance

Status: planning and reconnaissance only. No C# implementation on this gate.

## Goal

Prepare porting evidence for shared scientific identity before asserting compatibility in later gates.

## Scope

- Shared identity value objects (`WorkId`, `WorkIdSet`, `WorkIdNamespace`)
- Scholarly work identity (`ScholarlyWork`)
- Corpus aggregation (`CorpusSlice`)
- Author identity helpers only when needed for work-authorship behavior
- External identifier precedence and portability risks

## Gate 9 Exit Standard

- PHP source lock is verified against `specs/SOURCE.lock.json`.
- Behavior map and fixture plan for shared identity are stored under `docs/port/`.
- Stable behaviors are mapped with negative cases and comparator rules.
- Non-portable behavior (object-id fallback) is explicitly tracked in `OPEN-CONFLICTS.md`.
- No implementation claims are made until conflicts and ADRs are resolved.

## Current status

- `specs/SOURCE.lock.json` pins `../core` commit `b24d0d71ec7b64003465182477e7edb7f49994f4`.
- Shared identity behavior exists in PHP as observed identity-first domain semantics.
- Existing shared identity conflict remains open: `CF-010` (object identity fallback).
- This gate currently produces reconnaissance artifacts only.
- `CF-010` remains open and is blocking any Gate 9 implementation work.

## Required follow-up

- Resolve `CF-010` before any Gate 9 acceptance claim.
- Resolve comparator exceptions around `CorpusSlice::fromWorksUnsafe()` and missing-primary-id identity fallback.
- Produce fixture-backed conformance after reconciliation of identity semantics.

## Non-Claims

- no PHP compatibility claim
- no implementation claim
- no ported fixture claim
- no blueprint/provenance/workflow/screening/AI claim
