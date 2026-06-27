# Gate 9: Shared Scientific Identity

Status: implemented for local C# shared identity scope only.

## Goal

Implement local C# shared scientific identity behavior from `ADR 0007` without claiming PHP compatibility, Search behavior, Deduplication behavior, Screening behavior, or corpus snapshot semantics.

## Scope

- `WorkIdNamespace`
- `WorkId`
- `WorkIdSet`
- `ScholarlyWork`
- `CorpusSlice`
- local shared identity fixtures
- local conformance and architecture tests

## Implemented Local Behavior

- Approved `WorkId` namespace set: `doi`, `arxiv`, `openalex`, `s2`, `pubmed`, `pmcid`, `ieee`, `doaj`, and `internal`.
- Strict `WorkId` parsing for `<namespace>:<value>` with unknown namespace, missing separator, empty value, and ambiguous multi-token forms rejected.
- DOI and arXiv constructor normalization strips known DOI/arXiv prefixes and lowercases normalized values.
- `WorkIdSet` deduplicates exact normalized namespace/value pairs.
- `WorkIdSet.Primary` follows `ADR 0007` precedence: `doi`, `openalex`, `s2`, `arxiv`, `pmcid`, `pubmed`, `ieee`, `doaj`, `internal`.
- Scientific work equality uses stable identifier overlap only.
- Title-only equality is rejected as scientific identity.
- Runtime object identity is not used as a scientific fallback.
- No-id works can exist only as unresolved candidates with non-empty title and source context.
- No-id candidates do not deduplicate by title, insertion order, runtime identity, or object hash.
- `CorpusSlice.FromUnvalidatedCandidates` preserves raw candidates for import/fixture staging without treating runtime identity as scientific identity.
- Immutable membership identity projection rejects unresolved no-id candidates.

## Fixture IDs

Positive local fixtures:

- `shared-identity-workid-normalization.json`
- `shared-identity-workid-namespaces.json`
- `shared-identity-workidset-primary.json`
- `shared-identity-workidset-merge.json`
- `shared-identity-scholarlywork-merge.json`
- `shared-identity-no-id-candidate.json`
- `shared-identity-corpus-slice-dedupe.json`
- `shared-identity-unvalidated-candidates.json`
- `shared-identity-title-lookup-helper.json`

Negative local fixtures:

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

## Exit Standard

- `NexusScholar.Shared` builds as a domain project depending inward only on `NexusScholar.Kernel`.
- Core tests cover parsing, normalization, primary precedence, ID overlap, no-title identity, no-id candidates, corpus dedupe, unvalidated candidates, immutable membership rejection, and immutable collection snapshots.
- Conformance tests verify fixture presence, local metadata, explicit non-claims, and representative positive/negative fixture replay.
- Architecture tests include `NexusScholar.Shared` in domain dependency guards.
- Hosted CI passes on Windows and Ubuntu for the merge candidate.

## Conflict Status

- `CF-010`: implemented for local Gate 9 C# shared identity behavior.
- PHP `spl_object_hash` / `spl_object_id` fallback remains an intentional incompatibility and is not ported.
- PHP compatibility remains unclaimed until generator-backed PHP fixtures and semantic comparators exist.

## Non-Claims

- no PHP compatibility claim
- no generated PHP fixtures
- no Search behavior resolution
- no Deduplication behavior resolution
- no Screening behavior resolution
- no provider behavior
- no persistence, API, UI, or cloud behavior
- no corpus snapshot equality rule
- no title-fuzzy matching rule
- no blueprint conformance claim
