# PHP Deduplication Fixture And Comparator Plan

Status: H27 deterministic non-network subset implemented.

Pinned PHP source:

- Repository: `../core`
- Commit: `b24d0d71ec7b64003465182477e7edb7f49994f4`
- Source lock: `specs/SOURCE.lock.json`

## Scope

This plan defines the fixture and comparator catalog for C# Deduplication compatibility evidence. H27 implements the deterministic non-network subset under `fixtures/php-golden/deduplication/v1/`; existing PHP tests and app behavior outside that set remain source evidence only.

C# Deduplication should be implemented only after a contract decision resolves the raw input shape, title fuzzy threshold, no-id candidate handling, and app projection boundary.

## Fixture Families

### Input Shape

Planned fixtures:

- `dedup-input-search-trace-to-candidates.json`
- `dedup-input-imported-sightings-to-candidates.json`
- `dedup-input-raw-sightings-preserved.json`

Coverage:

- Search trace sightings become Dedup candidate records.
- Imported-export sightings become Dedup candidate records.
- Duplicate provider sightings remain visible before clustering.
- Local sighting/member handles are processing handles only, not scientific identity.
- Dedup does not call Search providers, import parsers, APIs, or live network code.

### Exact Identifier Clustering

Planned fixtures:

- `dedup-exact-doi-cluster.json`
- `dedup-exact-openalex-cluster.json`
- `dedup-exact-s2-cluster.json`
- `dedup-exact-arxiv-cluster.json`
- `dedup-exact-pubmed-cluster.json`
- `dedup-exact-multiple-namespace-cluster.json`
- `dedup-source-specific-id-not-workid.json`

Coverage:

- DOI normalization strips `doi:` and DOI URLs and lowercases.
- arXiv normalization strips `arxiv:` and lowercases.
- OpenAlex, Semantic Scholar, and PubMed values match by normalized exact namespace value.
- Scopus EID, Web of Science UT/accession numbers, and other source-specific ids remain source evidence unless a later ADR expands WorkId namespaces.

### Title Fuzzy Matching

Planned fixtures:

- `dedup-title-fuzzy-threshold-decision.json`
- `dedup-title-fuzzy-threshold-conflict-92-vs-95.json`
- `dedup-title-fuzzy-review-required.json`
- `dedup-title-fuzzy-year-gap.json`
- `dedup-title-fuzzy-empty-title-rejected.json`
- `dedup-title-fuzzy-below-threshold.json`

Coverage:

- Local default threshold `95` / `0.95` is asserted.
- PHP `92` is preserved as drift evidence, not as the local default.
- Year gap greater than the accepted maximum is rejected when both years exist.
- Missing year does not automatically block fuzzy comparison.
- Empty normalized title does not match.
- Title-only matching does not become shared scientific identity.
- Fuzzy title evidence is review-required by default.

### Transitive Clusters

Planned fixtures:

- `dedup-transitive-cluster.json`
- `dedup-transitive-evidence-preserved.json`
- `dedup-higher-priority-evidence-wins.json`
- `dedup-disconnected-evidence-rejected.json`

Coverage:

- A-B plus B-C yields one A-B-C cluster.
- Direct evidence edges are preserved.
- A pair already matched by a higher-priority policy is not overwritten by a lower-priority policy.
- Evidence must connect all members in an assembled cluster.

### Representative Election And Merge

Planned fixtures:

- `dedup-representative-election-completeness.json`
- `dedup-representative-election-provider-priority.json`
- `dedup-representative-election-doi-tiebreak.json`
- `dedup-representative-election-stable-tiebreak.json`
- `dedup-merge-field-preservation.json`
- `dedup-merge-identifier-union.json`

Coverage:

- Representative selection follows the accepted C# scoring contract.
- Tie-breakers are deterministic and fixture-pinned.
- Representative fields are filled from duplicate members only when missing.
- Existing representative fields are not overwritten.
- Cited-by count uses accepted max/null behavior.

### No-Id Candidates

Planned fixtures:

- `dedup-no-id-candidate-not-auto-merged.json`
- `dedup-no-id-title-only-not-canonical.json`
- `dedup-no-id-local-handle-not-identity.json`

Coverage:

- No-id records can remain unresolved candidates.
- Runtime object identity and local file paths are not scientific identity.
- Title-only duplicate evidence cannot create canonical corpus membership by itself.

### Raw Evidence And Output Shape

Planned fixtures:

- `dedup-raw-duplicate-evidence-preserved.json`
- `dedup-cluster-output-shape.json`
- `dedup-policy-stats.json`
- `dedup-duration-comparator.json`

Coverage:

- Duplicate evidence includes pair ids, reason, confidence, and source where accepted.
- Cluster membership is stable under ordering differences.
- Generated cluster ids and durations are compared by stable rules only.

### Lock And App Projection Planning

Planned fixtures:

- `dedup-locked-corpus-rejected.json`
- `dedup-stale-membership-rejected.json`
- `dedup-app-membership-hash-projection.json`
- `dedup-representative-snapshot-app-projection.json`
- `dedup-persisted-cluster-shape-app-projection.json`

Coverage:

- Locked project/corpus rejects Deduplication when lock state is in scope.
- Web membership hash is app projection unless admitted as Core contract.
- Web representative snapshot is app projection unless admitted by snapshot gate.
- Persisted clusters/runs are not local C# Dedup authority unless a persistence gate admits them.

## Negative Fixture Catalog

Required negative cases:

- empty corpus returns no clusters and no duplicates
- one-work corpus returns one singleton/unique result according to the accepted output shape
- no-id runtime object fallback rejected as scientific identity
- title-only duplicate rejected as shared identity
- below-threshold title fuzzy pair rejected
- title fuzzy threshold conflict remains unresolved until ADR decision
- source-specific identifier promoted to WorkId namespace without ADR
- stale membership hash used as Core authority
- locked corpus mutation
- disconnected evidence graph
- duplicate pair evidence overwritten silently
- empty representative election
- app representative score treated as Core authority
- app membership hash treated as Core authority
- persisted run row treated as domain contract
- PHP compatibility claimed without generated PHP fixtures

## Comparator Plan

### General

- Compare semantic fields, not PHP object identity.
- Compare cluster membership as unordered sets.
- Compare pair evidence by normalized work id handles, reason, confidence, and evidence source.
- Compare representatives by accepted deterministic election fields.
- Ignore generated cluster ids unless fixture injects fixed ids.
- Ignore runtime durations except for presence, numeric type, and non-negative shape.
- Ignore retrieval timestamps unless fixtures pin them.

### Input Comparator

Compare:

- trace id or import source binding where accepted
- sighting/member handles
- provider/source alias
- source record id
- normalized ADR 0007 identifiers
- unresolved candidate marker
- raw sighting count

Do not compare local file paths, runtime object ids, app display hashes, or provider credentials.

### Identifier Comparator

Compare:

- namespace
- normalized value
- overlap groups
- exact duplicate reason
- confidence

Classify source-specific ids as source evidence unless a later ADR expands namespaces.

### Title Fuzzy Comparator

Compare:

- normalized title
- year gap behavior
- accepted threshold
- ratio/confidence rounding
- duplicate reason

Do not claim parity until `92` versus `95` is resolved.

### Cluster Comparator

Compare:

- connected component membership
- evidence edges
- duplicate reasons
- confidence values
- policy stats
- representative id
- singleton handling

### Merge Comparator

Compare:

- identifier union
- filled abstract/venue/year/authors according to accepted merge contract
- representative field preservation
- cited-count behavior

### App Projection Comparator

Compare Web-only fixtures separately:

- membership hash material
- representative snapshot membership
- persisted run summary
- stale run detection
- screening representative-snapshot requirement

These must be marked app projection unless a later ADR makes them Core authority.

## Generator Requirements

The PHP fixture generator must:

1. run against commit `b24d0d71ec7b64003465182477e7edb7f49994f4`
2. record exact command and environment
3. inject fixed ids where id equality matters
4. inject fixed clocks and retrieval timestamps where ordering matters
5. preserve raw input records before PHP `CorpusSlice` collapses them
6. record input digest and output digest
7. avoid live providers and network calls
8. classify every difference as equivalent serialization, intentional incompatibility, PHP defect, C# defect, or unresolved conflict

## H27 Implemented Subset

- Exact DOI, arXiv, OpenAlex, Semantic Scholar, and PubMed policy behavior.
- Empty, singleton, exact-transitive, and representative fill-only handler behavior.
- PHP constructor threshold `92` versus local/Laravel-bound `95`.
- PHP fuzzy-title automatic clustering versus C# review-required evidence.
- No-id runtime-object fallback and safe-versus-unsafe `CorpusSlice` input behavior.
- Locked Deduplication rejection and lock export metadata with and without a snapshot.
- Complete source lock, command, generator version, environment, digest, nondeterminism, and reviewed classification metadata.

Remaining unclaimed surfaces include persistence repositories, cluster rows, snapshot membership/equality, lock lifecycle audit, Web membership hashes, stale-run behavior, queues, provider/network behavior, and broad PHP compatibility.

## Implementation Readiness

Implementation readiness: **yes for local C# Deduplication implementation against ADR 0012**.

ADR 0012 resolved the local contract decisions:

- Dedup input is Search trace/import sighting evidence.
- The local title fuzzy threshold is `95` / `0.95`.
- No-id records are unresolved candidates and require review.
- Representative election uses deterministic evidence-backed tie-breakers.
- Web membership hashes, persisted runs, and representative snapshots are app projections.

Fixture-scoped H27 evidence is implemented. Broad PHP compatibility and corpus snapshot parity remain unclaimed.

## Explicit Non-Claims

- no broad PHP compatibility beyond generated H27 cases
- no C# corpus lock or snapshot implementation
- no Search behavior change
- no Search import behavior change
- no Screening behavior
- no persistence schema
- no API/UI/cloud behavior
- no app behavior made authoritative
