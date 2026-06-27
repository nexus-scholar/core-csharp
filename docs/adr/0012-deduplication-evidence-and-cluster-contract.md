# ADR 0012: Deduplication Evidence and Cluster Contract

## Status

Accepted

## Date

2026-06-27

## Context

ADR 0007 defines scientific identity as stable identifier overlap, never runtime object identity or title-only equality. ADR 0010 defines Search output as raw `nexus.search.trace` evidence and keeps Search from calling Deduplication. ADR 0011 admits user-supplied imported search exports as Search acquisition evidence, while keeping import parsers separate from live provider integrations.

The pinned PHP reference exposes behavior that cannot be ported blindly:

- PHP Deduplication normally accepts `CorpusSlice`, but PHP Search aggregation may already deduplicate through `CorpusSlice::fromWorks(...)`.
- PHP has a raw-preserving `CorpusSlice::fromWorksUnsafe(...)`, but the C# contract must not depend on PHP naming or PHP object semantics.
- PHP exact identifier policies treat DOI, arXiv, OpenAlex, Semantic Scholar, and PubMed overlap as duplicate evidence.
- PHP title fuzzy policy has documentation/runtime drift: a constructor/default/comment path references `92`, while Laravel service registration, PHP docs, and Web persisted runs use `95` / `0.95`.
- PHP uses union-find style transitive clustering, then elects representatives and merges fields.
- PHP may fall back to `spl_object_hash(...)` for no-primary-id works; ADR 0007 rejects runtime object identity as scientific identity.
- Web app membership hashes, representative snapshots, persisted run rows, and stale-run checks are useful app evidence, but are not Core authority unless accepted by an ADR.

This ADR resolves the local C# Deduplication contract before implementation. It does not implement Deduplication.

## Decision

### Deduplication Input

C# Deduplication consumes raw Search trace sightings and imported-export sightings. Authoritative input records must bind back to one or more of:

- `search_trace_id`
- `search_sighting_id`
- imported source identity from ADR 0011
- imported source record id, when present
- source file digest or raw record digest, when present

C# Deduplication does not consume PHP's pre-deduplicated Search corpus as authoritative input. A materialized candidate record may be accepted only as a projection over raw Search/import evidence and only if its source evidence bindings are preserved.

### Duplicate Evidence Model

Deduplication evidence is recorded separately from representative records. Evidence entries include:

- exact identifier evidence
- fuzzy-title candidate evidence
- provider/source sighting evidence
- imported-export evidence
- no-id candidate evidence
- future human-review evidence, when a later gate defines review recording

Each evidence entry records its evidence type, the member records it connects, the source trace/import bindings, the policy id/version that produced it, confidence when applicable, and whether human review is required.

### Exact Identifier Match Rules

Exact identifier matching uses ADR 0007 normalized stable identifiers only. Matching is namespace-sensitive: a DOI can match a DOI, an OpenAlex id can match an OpenAlex id, and so on. Identifier text from a source-specific namespace is not promoted into `WorkIdNamespace` by Deduplication.

Exact identifier overlap creates deterministic duplicate evidence with confidence `1.0`. Exact identifier evidence may create an automatic duplicate cluster when every automatically included member is connected through accepted stable identifier evidence.

Title-only equality is never scientific identity.

### Fuzzy Title Matching

Fuzzy title matching creates candidate duplicate evidence. It does not create automatic scientific identity and does not automatically merge records unless an explicit future policy authorizes that behavior and records the policy in the Deduplication result.

The local default fuzzy title threshold is `95` / `0.95`. The PHP `92` value is treated as PHP documentation/configuration drift, not the local default. Deduplication output must record the threshold used by the policy so fixture comparators can distinguish default behavior from explicit custom policy.

### Transitive Clusters

Exact duplicate evidence is transitive for cluster formation. If A and B share accepted exact evidence and B and C share accepted exact evidence, A, B, and C belong to one automatic duplicate cluster.

Candidate-only evidence, including fuzzy-title-only and no-id evidence, is not enough to pull a member into an automatic cluster. Candidate edges may form review-required candidate groups.

All direct evidence edges are preserved. A transitive cluster summary must not erase which pairwise evidence connected which members.

### Representative Election

Representative election is deterministic, evidence-backed, and independent of runtime object identity. The representative is selected from cluster members using stable scoring material:

1. More complete scholarly metadata wins.
2. Accepted stable identifiers outrank source-specific identifiers.
3. Provider/source priority may be used only when the priority table is recorded in the policy.
4. DOI presence is a stable tie-breaker when other scores tie.
5. Remaining ties are broken by lexical normalized primary identifier, then deterministic member id.

Wall-clock execution time, object reference, object hash, database row order without explicit ordering, and local file path must not affect representative election.

### Representative Merge Projection

The representative record is a projection over preserved evidence, not a replacement for source sightings. Merge behavior fills missing representative fields from members but does not silently overwrite populated representative fields.

Identifier sets are unioned using ADR 0007 normalization. Source-specific identifiers remain source evidence unless a later ADR expands `WorkIdNamespace`. Raw sightings, imported records, source record ids, parser warnings, and evidence links remain available after representative projection.

### No-Id Candidate Behavior

Records without stable ADR 0007 identifiers may exist as unresolved Deduplication candidates. They cannot be automatically merged by title, runtime object identity, local handle, or source row order. No-id title similarity may create human-review candidate evidence only.

### Human-Review Boundary

Automatic clusters are allowed for accepted exact stable identifier evidence. Review is required for:

- fuzzy-title-only evidence
- no-id candidate evidence
- source-specific-id-only evidence
- conflicting or malformed identifiers
- any policy override that changes the local default fuzzy threshold

This ADR defines review requirements but does not implement a human review workflow or approval record.

### Output Shape

Local C# Deduplication output uses the conceptual schema `nexus.deduplication.result` version `1.0.0`. The result contains:

- result id
- input trace/import references
- policy id, policy version, threshold values, and provider/source priority table when used
- duplicate clusters
- cluster members
- evidence links
- representative record projection
- unresolved candidates
- warnings and errors
- summary statistics
- explicit non-claims

Cluster members retain source Search trace ids, Search sighting ids, import source ids, import record ids, source file digests, and raw record digests when present.

### Binding Back To Search

Deduplication output references Search evidence; it does not replace Search evidence. Every cluster member and evidence link must be traceable to the source Search trace/import sighting that produced it.

Search remains unchanged by this ADR: Search does not call Deduplication, does not output a deduplicated corpus, and does not treat no-id or title-only records as canonical corpus membership.

### Web App Projection Boundary

Web membership hashes, representative snapshots, persisted run rows, stale-run checks, and app-specific clustering are integration evidence only. They are not Core Deduplication authority in this ADR.

Core may later consume app evidence through a dedicated contract, but this ADR does not adopt app persistence, app hashes, or app representative scoring as canonical Core behavior.

## Fixture And Comparator Consequences

Gate 9 Deduplication fixtures should be local C# contract fixtures first. They must cover:

- Search trace sightings as Deduplication input
- imported-export sightings as Deduplication input
- raw sightings preserved through Deduplication output
- exact identifier clusters for DOI, OpenAlex, Semantic Scholar, arXiv, and PubMed
- source-specific identifier evidence not promoted to `WorkIdNamespace`
- fuzzy title threshold `95` and explicit documentation of PHP `92` drift
- fuzzy title below threshold
- fuzzy title requiring review rather than automatic merge
- transitive exact clusters with all direct evidence links preserved
- representative election and stable tie-breakers
- representative merge field-fill behavior
- identifier union
- no-id candidates not automatically merged
- raw duplicate evidence preserved after representative projection
- output schema shape, warnings, and summary statistics
- app membership hashes and representative snapshots treated as projections

Comparators must ignore runtime duration and generated run ids when they are not part of scientific identity. Comparators must not ignore evidence links, policy id/version, threshold, source trace bindings, or representative tie-breaker material.

## Conflict Status

- `CF-011` is resolved for the local C# Deduplication contract: the input is raw Search trace/import sighting evidence, not PHP's pre-deduplicated Search corpus.
- `CF-012` is resolved for the local C# Deduplication contract: the default fuzzy title threshold is `95` / `0.95`, with PHP `92` treated as drift unless an explicit custom policy records otherwise.
- `CF-016` remains implemented for Search and is narrowed for Deduplication handoff: Search emits raw traces; Deduplication consumes those traces later.
- `CF-020` is narrowed for Core: Web hashes, snapshots, persisted runs, and representative scoring remain app projections, not Core authority.

## Consequences

Positive consequences:

- C# Deduplication can be implemented without importing PHP's pre-deduped Search aggregation behavior.
- Exact identifier overlap remains compatible with ADR 0007 scientific identity.
- Ambiguous fuzzy/no-id behavior is preserved for human review instead of silently becoming scientific identity.
- Raw provider/import evidence remains reconstructable after clustering and representative projection.

Negative consequences:

- The local C# contract intentionally diverges from PHP places that deduplicate before Deduplication receives data.
- PHP compatibility remains unclaimed until generator-backed fixtures and comparators exist.
- Fuzzy title behavior is more conservative than a fully automatic title-merge implementation.

## Alternatives Considered

### Consume PHP-Style CorpusSlice As Input

Rejected. PHP `CorpusSlice::fromWorks(...)` may premerge records before Deduplication. That erases the Search/Deduplication boundary accepted by ADR 0010.

### Use The PHP `92` Fuzzy Default

Rejected as the local default. The effective PHP runtime binding and Web persisted value use `95` / `0.95`, while `92` appears in default/comment/demo paths. The local contract chooses the effective runtime binding and records threshold in policy output.

### Treat Web Membership Hashes As Core Authority

Rejected. Web app rows and hashes are app projections. They can inform future contracts, but they are not Core authority here.

### Allow No-Id Title-Only Automatic Merge

Rejected. ADR 0007 forbids title-only scientific identity and runtime object identity fallback.

## Implementation Readiness

Local C# Deduplication implementation is ready to start against this contract.

Implementation is not ready for:

- PHP compatibility claims
- PHP-generated golden fixture comparison
- Screening behavior
- persistence/API/UI/cloud behavior
- app alignment as Core authority

## Explicit Claims Not Made

This ADR does not claim:

- C# Deduplication implementation exists
- PHP compatibility
- generated PHP fixtures
- Screening behavior
- persistence, API, UI, cloud, or app behavior
- Search behavior changes
- live provider/network behavior
- corpus snapshot equality
- human review workflow implementation
