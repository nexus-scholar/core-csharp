# Shared Identity Behavior Map (PHP Reference)

## Source pin

- Locked PHP commit: `b24d0d71ec7b64003465182477e7edb7f49994f4` from `specs/SOURCE.lock.json`
- Source verification command: `git -C ../core rev-parse HEAD`

## PHP module scope

- Entry namespace: `Nexus\Shared`
- Primary files:
  - `src/Shared/ValueObject/WorkId.php`
  - `src/Shared/ValueObject/WorkIdNamespace.php`
  - `src/Shared/ValueObject/WorkIdSet.php`
  - `src/Shared/Domain/ScholarlyWork.php`
  - `src/Shared/Domain/CorpusSlice.php`
  - `src/Shared/ValueObject/AuthorList.php`
  - `src/Shared/ValueObject/Author.php`
- Port and policy evidence:
  - `src/Shared/Port/ProjectWorkMembershipPort.php`
  - `src/Shared/Application/CorpusLockPolicy.php`

## Behavior map

- `WorkIdNamespace` includes 9 namespaces:
  - `doi`
  - `arxiv`
  - `openalex`
  - `s2`
  - `pubmed`
  - `pmcid`
  - `ieee`
  - `doaj`
  - `internal`
- `WorkId` values are normalized in constructor:
  - DOI prefix removal: `https://doi.org/`, `http://dx.doi.org/`, `doi:`
  - arXiv prefix removal: `arxiv:`
  - lowercase normalization for all namespaces
- `WorkId::fromString()`:
  - expects exactly `<namespace>:<value>`
  - rejects missing `:` and empty value
  - rejects unknown namespace
- `WorkIdSet`:
  - stores ordered ids
  - reports `primary()` by fixed precedence order
  - `primary()` precedence: `doi`, `openalex`, `s2`, `arxiv`, `pmcid`, `pubmed`, `ieee`, `doaj`, `internal`
  - `add()` returns a new set (immutable usage style)
  - `merge()` deduplicates exact namespace/value pairs
  - `hasOverlapWith()` defines identity overlap
- `ScholarlyWork`:
  - created through `reconstitute()`
  - rejects empty/blank title
  - `isSameWorkAs()` delegates to `WorkIdSet::hasOverlapWith()`
  - `mergeWith()` keeps left-side existing fields and merges ids
  - `withRawData()` and `withoutRawData()` clone and only mutate raw payload
  - `isPreprint()` is true for arXiv source provider or repository venue
- `CorpusSlice`:
  - identity deduplication on `withWork()` via `isSameWorkAs()`
  - `merge()` deduplicates and reuses existing entries
  - `findById()` keys direct lookups by id string first, then scans ids
  - `findByTitle()` performs case-insensitive title match
  - `sortByYear()` and `sortByCitedByCount()` accept ascending/descending flags
  - `subtract()`, `filter()`, `retracted()`, `withoutRetracted()` expose workflow shaping
  - `fromWorksUnsafe()` bypasses dedup and keys by object id

## Stable behaviors to port

- ID-first identity:
  - work identity is defined by identifier overlap, never by title-only comparison
- Deterministic primary-id resolution using fixed namespace precedence
- Title and namespace normalization behavior used in key comparisons
- Idempotent merge semantics in `ScholarlyWork::mergeWith()`
- Deduplication behavior in `CorpusSlice` when `withWork()` or `merge()` sees overlapping `WorkIdSet`
- `WorkIdSet::merge()` removes duplicate ids by `namespace + value`
- Empty title prohibition in `ScholarlyWork::reconstitute()`

## Non-portable behavior and decisions needed before C#

- `CorpusSlice` uses `spl_object_hash` / `spl_object_id` when a work has no primary id.
- `CorpusSlice::fromWorksUnsafe()` uses object identity as a dedup bypass path for fixture/test shaping.
- `findByTitle()` is a title helper in the same aggregate and may conflict with ID-first scientific identity assumptions if treated as equality.
- `CorpusSlice` dedupe/identity internals use work object identity when an id is absent.
- `WorkId` constructor accepts blank raw values without explicit exception; only `fromString()` validates the separator and namespace.

## AuthorList dependency notes

- `AuthorList` can participate in work equality via `Author::isSamePerson()`, but no work-level identity depends on author names.
- Author equality is ORCID-first, then normalized full-name equality.

## Related PHP tests

- `tests/Unit/Shared/WorkIdTest.php`
- `tests/Unit/Shared/ScholarlyWorkTest.php`
- `tests/Unit/Shared/ValueObjectsTest.php`
- `tests/Unit/Shared/CorpusLockPolicyTest.php`

## Related fixtures

- No dedicated `Shared` conformance fixtures exist in `tests/Fixture`.
- Existing shared-relevant fixtures in PHP are indirect for transport and providers:
  - `tests/Fixture/search_plans/*`
  - `tests/Fixture/vcr_cassettes/*`
- Shared identity PHP compatibility is deferred to generator-backed fixtures with source metadata per `GOLDEN-FIXTURE-PLAN.md`.
