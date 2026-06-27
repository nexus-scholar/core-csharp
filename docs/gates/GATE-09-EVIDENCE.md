# Gate 9 Evidence: Shared Scientific Identity

Status: local verification passed; hosted CI pending for final branch head.

## Scope

Gate 9 implements local C# shared scientific identity behavior only.

Implemented conflict scope:

- `CF-010`: implemented for local shared identity behavior.

Still out of scope:

- PHP compatibility, PHP-generated fixtures, Search, Deduplication, Screening, provider behavior, persistence, API, UI, cloud sync, corpus snapshot equality, and blueprint conformance.

## Source Decisions

- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/port/php-shared-identity-behavior.md`
- `docs/port/php-shared-identity-fixture-plan.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

## Behavior Implemented

- `WorkIdNamespace` exposes the approved Gate 9 namespace set.
- `WorkId` normalizes namespace and value, strips DOI/arXiv prefixes in construction, lowercases normalized values, and strictly parses `<namespace>:<value>` strings.
- `WorkIdSet` deduplicates exact normalized IDs and resolves primary IDs by fixed `ADR 0007` precedence.
- `ScholarlyWork` rejects blank titles, compares identity only by stable identifier overlap, and preserves no-id records as unresolved candidates with source context.
- `CorpusSlice` deduplicates only by stable identifier overlap, preserves no-id candidates without title/object fallback dedupe, exposes title lookup as helper behavior only, and rejects no-id candidates from immutable membership identity projection.
- `CorpusSlice.FromUnvalidatedCandidates` preserves raw candidates for import/fixture staging without promoting runtime object identity to scientific identity.
- `NexusScholar.Shared` depends inward only on `NexusScholar.Kernel`.

## Fixture IDs

- `shared-identity-workid-normalization.json`
- `shared-identity-workid-namespaces.json`
- `shared-identity-workidset-primary.json`
- `shared-identity-workidset-merge.json`
- `shared-identity-scholarlywork-merge.json`
- `shared-identity-no-id-candidate.json`
- `shared-identity-corpus-slice-dedupe.json`
- `shared-identity-unvalidated-candidates.json`
- `shared-identity-title-lookup-helper.json`
- `shared-identity-bad-workid-string.json`
- `shared-identity-blank-workid-constructor.json`
- `shared-identity-empty-title-work.json`
- `shared-identity-overlap-false.json`
- `shared-identity-cross-namespace-normalized-clash.json`
- `shared-identity-spl-object-fallback-probe.json`
- `shared-identity-title-only-not-identity.json`
- `shared-identity-no-id-no-dedupe.json`
- `shared-identity-no-id-snapshot-reject.json`
- `shared-identity-bad-merge-order.json`

## Local Verification

- `git grep -n "corupslice\|correlation-by-title"`: no matches.
- `dotnet restore NexusScholar.Core.slnx`: passed.
- `dotnet build NexusScholar.Core.slnx -c Release --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test NexusScholar.Core.slnx -c Release --no-build`: passed, 140 tests total.
  - `NexusScholar.Architecture.Tests`: 11 passed.
  - `NexusScholar.Conformance.Tests`: 28 passed.
  - `NexusScholar.Core.Tests`: 101 passed.
- `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`: passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1`: passed.

## Hosted CI

- Pending final pushed branch run.

## Explicit Claims Not Made

- no PHP compatibility
- no generated PHP fixtures
- no Search behavior
- no Deduplication behavior
- no Screening behavior
- no provider behavior
- no persistence, API, UI, or cloud behavior
- no corpus snapshot equality rule
- no title-fuzzy matching rule
- no blueprint conformance
