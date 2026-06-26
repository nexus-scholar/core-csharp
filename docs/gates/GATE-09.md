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
- `ADR 0007` resolves `CF-010` for Gate 9 planning scope by rejecting runtime object identity as scientific identity and treating no-primary-id works as unresolved candidates.
- This gate currently produces planning artifacts only.
- Gate 9 implementation remains blocked until fixtures and comparators are generated in a later implementation branch.

## Required follow-up

- Generate fixture-backed conformance after reconciliation of identity semantics.
- Implement `WorkId`, `WorkIdSet`, `ScholarlyWork`, and `CorpusSlice` against `ADR 0007`.
- Classify PHP `spl_object_hash` fallback as an intentional incompatibility in comparator output.
- Keep Search, Deduplication, Screening, and snapshot equality outside this gate unless later ADRs resolve them.

## ADR 0007 planning decisions

- Work identity is based on normalized stable identifier overlap.
- Title is not scientific identity.
- Runtime object identity is not scientific identity.
- No-id works may exist only as unresolved candidates.
- No-id works do not deduplicate by title, object identity, insertion order, or runtime hash.
- C# constructor and parser validation is stricter than PHP for blank identifiers.
- Any unsafe construction path must be scoped to unvalidated candidates, fixture replay, raw import staging, or negative-test setup.

## Required fixture consequences

Positive fixture planning must cover:

- all approved `WorkId` namespaces;
- DOI and arXiv normalization;
- primary-id precedence;
- ID-overlap equality;
- no-id unresolved candidate admission;
- raw candidate preservation through an explicitly unsafe or unvalidated import path.

Negative fixture planning must cover:

- bad namespace;
- missing separator;
- empty identifier value;
- blank identifier construction;
- title-only false-positive identity;
- cross-namespace same-value non-overlap;
- runtime object identity fallback rejection;
- no-id candidate dedupe rejection;
- no-id candidate rejection from immutable scientific identity contexts.

## Non-Claims

- no PHP compatibility claim
- no implementation claim
- no ported fixture claim
- no Search behavior resolution
- no Deduplication behavior resolution
- no Screening behavior resolution
- no immutable snapshot equality rule
- no blueprint/provenance/workflow/AI claim
