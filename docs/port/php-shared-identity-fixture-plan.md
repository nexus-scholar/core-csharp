# Shared Identity Fixture Plan

## Scope

- Shared identity target for shared identity reconnaissance.
- Focus: `WorkId`, `WorkIdNamespace`, `WorkIdSet`, `ScholarlyWork`, `CorpusSlice`, and shared identifier precedence.
- Phase 7 H25 adds the first PHP-generated fixture set and semantic comparator for the shared contract surface.

## Fixture sources

- Handcrafted seed JSON/NDJSON under `tests/` are planning/conformance seeds only.
- Local conformance fixtures remain local contract evidence only.
- The generated fixture set is `fixtures/php-golden/shared-identity/v1/` and includes complete source metadata per `GOLDEN-FIXTURE-PLAN.md`.
- Compatibility statements are limited to cases classified by `PhpSharedIdentityGoldenTests`; uncovered Shared behavior remains unclaimed.

## H25 implemented subset

- Generated coverage includes identifier normalization, primary precedence, overlap, merge/set-order semantics, left-biased title merge, direct corpus deduplication, no-id candidate separation, and title lookup.
- Generated coverage does not yet claim parity for authors, year, venue, abstract, citation count, retraction merge/filter behavior, raw data, completeness scoring, sorting, subtraction, or persistence projections.

## Planned positive fixture set

- `shared-identity-workid-normalization.json`
  - covers DOI/arXiv normalization, lowercase behavior, `toString`, and `fromString` round-trip
- `shared-identity-workid-namespaces.json`
  - covers the approved namespace set: DOI, arXiv, OpenAlex, Semantic Scholar, PubMed, PMCID, IEEE, DOAJ, and internal
- `shared-identity-workidset-primary.json`
  - covers namespace precedence, empty set primary null, and overlap checks
- `shared-identity-workidset-merge.json`
  - covers duplicate removal, namespace precedence, and `add()` immutability
- `shared-identity-scholarlywork-merge.json`
  - covers ID-overlap identity, merge field fill rules, raw data handling
- `shared-identity-no-id-candidate.json`
  - covers unresolved no-id work admission with non-empty title and source/provenance context
- `shared-identity-corpus-slice-dedupe.json`
  - covers add/merge id-based dedupe and retraction filters
- `shared-identity-unvalidated-candidates.json`
  - covers unsafe/unvalidated import preserving raw candidate records without runtime-id dedupe
- `shared-identity-title-lookup-helper.json`
  - covers `findByTitle()` case-insensitive behavior

## Planned negative fixture set

- `shared-identity-bad-workid-string.json`
  - bad namespace, missing colon, empty value
- `shared-identity-blank-workid-constructor.json`
  - direct construction with blank normalized value is rejected in C#
- `shared-identity-empty-title-work.json`
  - whitespace-only title rejection
- `shared-identity-overlap-false.json`
  - shared works with no overlapping identifier sets are not same
- `shared-identity-cross-namespace-normalized-clash.json`
  - same numeric suffix in different namespace remains distinct
- `shared-identity-spl-object-fallback-probe.json`
  - work without primary id must not use object-hash in C# scientific identity model
- `shared-identity-title-only-not-identity.json`
  - matching titles do not create scientific identity without identifier overlap
- `shared-identity-no-id-no-dedupe.json`
  - two no-id candidates with matching titles remain distinct unresolved candidates
- `shared-identity-no-id-snapshot-reject.json`
  - no-id candidate cannot satisfy immutable scientific identity membership
- `shared-identity-bad-merge-order.json`
  - confirm merge preserves left-side non-null fields and fills nulls only

## Semantic comparator rules

- Normalize identifiers from PHP test fixtures before comparison:
  - lowercased namespace and value for identity equality
  - DOI/arXiv prefix stripping behavior preserved where applicable
- Compare `ScholarlyWork` identity by overlap of any identifiers, not title
- Compare `CorpusSlice` membership by post-merge dedupe results, not insertion order
- Treat non-portable object-id fallback paths as intentional incompatibilities under `ADR 0007`
- Treat no-id works as unresolved candidates; they do not dedupe unless a later explicit duplicate decision or stable identifier exists
- Compare source-provider and timestamp fields for compatibility only where fixtures pin them

## Comparator exceptions (non-identity fields)

- Comparator should ignore runtime-only host identity when deciding work identity.
- `retrievedAt` is runtime-generated when a work is reconstituted and should be handled as metadata.
- `fromWorksUnsafe()` behavior should be represented as transport/test fixture behavior only, not canonical scientific identity.
- C# may expose an unsafe construction path only as an explicitly named unvalidated-candidate import primitive; comparator output must not treat it as a PHP-compatible scientific identity fallback.

## Evidence and test links

- `tests/Unit/Shared/WorkIdTest.php`
- `tests/Unit/Shared/ScholarlyWorkTest.php`
- `tests/Unit/Shared/ValueObjectsTest.php`
- `tests/Feature/Laravel/DeduplicateCorpusJobTest.php` for `CorpusSlice::fromWorksUnsafe()` usage
- `scripts/php-golden/shared-identity-export.php`
- `fixtures/php-golden/shared-identity/v1/manifest.json`
- `fixtures/php-golden/shared-identity/v1/comparison.json`
- `tests/NexusScholar.Conformance.Tests/PhpSharedIdentityGoldenTests.cs`
