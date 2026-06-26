# ADR 0007: Shared Scientific Identity

Status: Accepted

Date: 2026-06-26

## Context

Gate 9 needs a C# shared identity rule before porting `WorkId`, `WorkIdSet`, `ScholarlyWork`, and `CorpusSlice` behavior from the pinned PHP reference.

`ADR 0001` requires intentional incompatibilities to be documented when PHP behavior is not safe to port. `ADR 0002` forbids runtime object identity in scientific digests and scientific identity. The PHP behavior map records one non-portable fallback: `CorpusSlice::fromWorksUnsafe()` and no-primary-id corpus paths can use `spl_object_hash` or `spl_object_id` to keep objects distinct.

Runtime object identity is not a scientific identity. It is process-local, non-reconstructable, and not portable across languages, machines, serialized fixtures, or audit replay. This ADR therefore defines the local C# shared scientific identity contract and the no-primary-id fallback policy.

This decision uses PHP behavior as evidence, but it does not claim PHP compatibility. Compatibility requires generator-backed fixtures and comparators later in Gate 9.

## Decision

### 1. WorkId namespace set

C# adopts the PHP namespace set for Gate 9 local shared identity:

- `doi`
- `arxiv`
- `openalex`
- `s2`
- `pubmed`
- `pmcid`
- `ieee`
- `doaj`
- `internal`

No other namespace is accepted by the shared identity value object unless a later ADR extends the set.

### 2. WorkId normalization contract

`WorkId` normalizes namespace and value at construction:

- namespace is lowercased and matched against the approved namespace set;
- value is trimmed;
- DOI values strip `https://doi.org/`, `http://dx.doi.org/`, and `doi:` prefixes;
- arXiv values strip `arxiv:` prefixes;
- normalized values are lowercased;
- the normalized value must be non-empty.

The canonical rendered form is:

```text
{namespace}:{normalized-value}
```

Normalization is part of scientific identity, not display formatting.

### 3. WorkId parsing strictness

`WorkId.Parse` or its equivalent accepts only exactly one namespace separator in the logical form:

```text
<namespace>:<value>
```

Parsing rejects:

- missing separator;
- empty namespace;
- empty value after namespace-specific normalization;
- unknown namespace;
- values that normalize to empty;
- multiple leading namespace tokens that would make authority ambiguous.

C# constructor validation is intentionally stricter than PHP where PHP accepts blank raw values through direct construction. This is an intentional incompatibility: blank identifiers cannot be scientific identities.

### 4. WorkIdSet primary-id precedence

`WorkIdSet.Primary` uses fixed precedence:

1. `doi`
2. `openalex`
3. `s2`
4. `arxiv`
5. `pmcid`
6. `pubmed`
7. `ieee`
8. `doaj`
9. `internal`

Duplicate namespace/value pairs collapse to one identifier. Different namespaces with the same normalized value remain distinct.

An empty `WorkIdSet` has no primary id.

### 5. ID-overlap equality

Two identified works are the same scientific work when their normalized `WorkIdSet` values overlap by at least one exact namespace/value pair.

Identity overlap is:

- namespace-sensitive;
- value-normalized;
- independent of title, author names, venue, year, provider, object identity, insertion order, or raw payload;
- sufficient for `ScholarlyWork` equality and `CorpusSlice` deduplication when both sides carry stable identifiers.

### 6. No title-based scientific identity

Title lookup may exist as a search or convenience helper, but title is not scientific identity.

C# must not deduplicate, merge, or assert work equality from title-only comparison. Fuzzy title matching remains a later Search/Deduplication concern and is not resolved by this ADR.

### 7. No runtime object identity in C#

C# must not use runtime object identity, reference identity, object hash code, allocation identity, or process-local handles as scientific work identity.

This rejects the PHP `spl_object_hash` / `spl_object_id` fallback as a C# scientific identity rule.

### 8. Works without stable identifiers

A `ScholarlyWork` without stable identifiers may exist only as an unresolved candidate record.

No-id works:

- must carry a non-empty title;
- must carry provenance or source context when admitted into an aggregate;
- have no `PrimaryWorkId`;
- cannot be treated as equal to any other no-id work;
- cannot satisfy scientific identity references;
- cannot be used as canonical corpus membership identity in immutable snapshots.

No-id records are allowed so ingestion and review can preserve evidence before identifier resolution, but they remain unresolved candidates.

### 9. CorpusSlice membership for no-id works

No-id works can exist in a mutable `CorpusSlice` only as unresolved candidate entries.

`CorpusSlice` may contain multiple no-id candidates even when their titles match. They remain distinct because no stable identity overlap exists. A later identifier-resolution or deduplication step may convert or merge them only by producing an explicit decision or stable identifier evidence under a later gate.

Immutable corpus or release snapshots must not rely on no-id runtime identity. A later snapshot ADR may either reject unresolved no-id membership or assign stable candidate ids with provenance; this ADR does not resolve snapshot equality.

### 10. No-id deduplication

No-id works do not deduplicate by title, runtime identity, insertion order, or object identity.

They can deduplicate only after they gain overlapping stable identifiers or after a later accepted Deduplication ADR defines an explicit human-reviewed duplicate decision record. This ADR does not resolve Search, Deduplication, Screening, or title-fuzzy behavior.

### 11. Unsafe corpus construction

C# may expose an unsafe construction path only as an explicit test/import primitive named:

```text
CorpusSlice.FromUnvalidatedCandidates
```

or an equivalent name that includes `Unvalidated` or `Unsafe`.

The method is not a scientific identity mechanism. Its scope is limited to fixture replay, raw import staging, and negative-test setup. It must preserve input records without deduplicating by runtime identity and must not create canonical corpus membership identity.

Production workflow code should use validated add/merge operations that deduplicate only by stable identifier overlap.

### 12. C# strictness versus PHP

C# is intentionally stricter than PHP in these areas:

- blank `WorkId` values are rejected at construction and parsing;
- runtime object identity is never a scientific fallback;
- title-only equality is never promoted to work identity;
- no-id corpus entries remain unresolved candidates rather than identity-bearing records.

These are intentional incompatibilities required by local scientific identity and auditability rules.

## Alternatives Considered

### 1. Preserve PHP `spl_object_hash` fallback

Rejected.

Object identity is process-local and cannot be reconstructed from fixtures, serialized records, bundles, or audit logs.

### 2. Use title as fallback identity

Rejected.

Title-only identity creates false positives, collapses unrelated works, and conflicts with the product law that scientific identity uses stable identifiers and content digests.

### 3. Reject all no-id works

Rejected for mutable ingestion and review.

Provider records may arrive without stable identifiers. The system must preserve them as unresolved evidence candidates without pretending they have scientific identity.

### 4. Introduce generated C# candidate ids as scientific identity

Rejected for Gate 9.

Generated candidate ids may become a future staging or snapshot mechanism, but this ADR does not authorize them as work identity.

## Consequences

### Positive

- `CF-010` is resolved for Gate 9 planning scope.
- C# identity behavior is deterministic, portable, and reconstructable.
- PHP object-id fallback is documented as an intentional incompatibility.
- Fixture planning can target no-id candidate behavior explicitly.

### Negative

- C# will not be byte-for-byte or behavior-identical to PHP in no-primary-id corpus fallback paths.
- PHP compatibility cannot be claimed until fixture comparators classify this incompatibility and generator-backed evidence exists.
- Downstream Search, Deduplication, Screening, and snapshot behavior still need separate decisions.

## Migration Effect

No persisted C# data is migrated by this planning ADR.

When implemented, any no-id records imported from PHP-like sources must be represented as unresolved candidates, not as identity-bearing corpus members. Any previous scaffold behavior that treated runtime identity or title equality as scientific identity must be considered non-authoritative.

## Fixture Effect

Gate 9 fixtures and comparators must cover:

- namespace normalization for all approved namespaces;
- DOI and arXiv prefix stripping;
- strict parser rejection for malformed ids;
- blank-value constructor rejection;
- fixed primary-id precedence;
- namespace-sensitive ID overlap;
- cross-namespace same-value non-overlap;
- title lookup as helper behavior only;
- no title-based scientific identity;
- no-id work admission as unresolved candidate;
- no-id candidate non-deduplication;
- no-id candidate rejection from immutable scientific identity contexts;
- `FromUnvalidatedCandidates` preserving raw candidates without runtime-id dedupe;
- intentional incompatibility classification for PHP `spl_object_hash` fallback.

Generated PHP compatibility fixtures must record source commit, generator command, input digest, output digest, and comparator rules before any compatibility claim.

## Reversal Conditions

Revise this ADR only if:

1. later fixture evidence shows a stable PHP-compatible no-id identity rule that is reconstructable without runtime object identity;
2. a later snapshot ADR defines stable candidate ids with provenance and migration rules;
3. Search or Deduplication ADRs introduce human-reviewed duplicate decision records that can safely merge no-id candidates;
4. an accepted specification supersedes the namespace set or normalization contract.

## Explicit Claims Not Made

- no C# implementation
- no generated fixtures
- no PHP compatibility claim
- no Search behavior resolution
- no Deduplication behavior resolution
- no Screening behavior resolution
- no immutable snapshot equality rule
- no title-fuzzy matching rule
- no blueprint conformance claim
