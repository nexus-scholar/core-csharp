# Shared Identity Fixture Plan

## Scope

- Shared identity target for shared identity reconnaissance.
- Focus: `WorkId`, `WorkIdNamespace`, `WorkIdSet`, `ScholarlyWork`, `CorpusSlice`, and shared identifier precedence.
- Reconnaissance uses planning and conformance seed fixtures only. PHP-generated fixtures are not used in this phase.

## Fixture sources

- Handcrafted seed JSON/NDJSON under `tests/` are planning/conformance seeds only.
- PHP-generated fixtures are not accepted as this phase’s source-of-truth compatibility evidence.
- Future fixtures must include source metadata per `GOLDEN-FIXTURE-PLAN.md`.
- PHP compatibility proof must use generator-backed fixtures with complete source metadata as defined in `GOLDEN-FIXTURE-PLAN.md`.

## Planned positive fixture set

- `shared-identity-workid-normalization.json`
  - covers DOI/arXiv normalization, lowercase behavior, `toString`, and `fromString` round-trip
- `shared-identity-workidset-primary.json`
  - covers namespace precedence, empty set primary null, and overlap checks
- `shared-identity-workidset-merge.json`
  - covers duplicate removal, namespace precedence, and `add()` immutability
- `shared-identity-scholarlywork-merge.json`
  - covers ID-overlap identity, merge field fill rules, raw data handling
- `shared-identity-corpus-slice-dedupe.json`
  - covers add/merge id-based dedupe and retraction filters
- `shared-identity-title-lookup-helper.json`
  - covers `findByTitle()` case-insensitive behavior

## Planned negative fixture set

- `shared-identity-bad-workid-string.json`
  - bad namespace, missing colon, empty value
- `shared-identity-empty-title-work.json`
  - whitespace-only title rejection
- `shared-identity-overlap-false.json`
  - shared works with no overlapping identifier sets are not same
- `shared-identity-cross-namespace-normalized-clash.json`
  - same numeric suffix in different namespace remains distinct
- `shared-identity-spl-object-fallback-probe.json`
  - work without primary id must not use object-hash in C# scientific identity model
- `shared-identity-bad-merge-order.json`
  - confirm merge preserves left-side non-null fields and fills nulls only

## Semantic comparator rules

- Normalize identifiers from PHP test fixtures before comparison:
  - lowercased namespace and value for identity equality
  - DOI/arXiv prefix stripping behavior preserved where applicable
- Compare `ScholarlyWork` identity by overlap of any identifiers, not title
- Compare `CorpusSlice` membership by post-merge dedupe results, not insertion order
- Treat non-portable object-id fallback paths as comparator exceptions requiring explicit ADR
- Compare source-provider and timestamp fields for compatibility only where fixtures pin them

## Comparator exceptions (non-identity fields)

- Comparator should ignore runtime-only host identity when deciding work identity.
- `retrievedAt` is runtime-generated when a work is reconstituted and should be handled as metadata.
- `fromWorksUnsafe()` behavior should be represented as transport/test fixture behavior only, not canonical scientific identity.

## Evidence and test links

- `tests/Unit/Shared/WorkIdTest.php`
- `tests/Unit/Shared/ScholarlyWorkTest.php`
- `tests/Unit/Shared/ValueObjectsTest.php`
- `tests/Feature/Laravel/DeduplicateCorpusJobTest.php` for `CorpusSlice::fromWorksUnsafe()` usage
