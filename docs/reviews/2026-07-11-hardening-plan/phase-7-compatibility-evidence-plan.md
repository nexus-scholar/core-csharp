# Phase 7 Compatibility Evidence Plan

Status: in progress.

## Objective

Limit every PHP compatibility statement to reproducible observations from the commit pinned in `specs/SOURCE.lock.json`, then classify C# differences through reviewed semantic comparators. PHP remains behavioral evidence, not authority over accepted specifications or ADRs.

## Evidence Contract

Each generated fixture set must contain:

- immutable replay input;
- PHP-generated expected output;
- a generated manifest with repository, pinned commit, source refs, exact command, generator version, environment assumptions, and SHA-256 input/output digests;
- a reviewed case-by-case classification using `equivalent_serialization`, `intentional_change`, `php_defect`, `csharp_defect`, or `unresolved_specification_conflict`;
- C# conformance tests that validate provenance, digests, classification coverage, equivalent semantics, and exact intentional boundaries.

Generation must fail when the PHP checkout is not at the pinned commit or has tracked modifications. Normal CI replays committed fixtures without calling PHP, live providers, or live LLMs.

## Jobs

| Job | Scope | State | Exit evidence |
| --- | --- | --- | --- |
| H25 | Fixture harness and Shared Identity | implemented on `cdx/hardening-phase-7-shared-identity` | deterministic PHP exporter, 12 cases, manifest digests, reviewed classifications, C# comparator |
| H26 | Search query, cache, provider selection, and local import behavior | pending | generated PHP Search fixtures and comparator report; intentional cache/import differences remain explicit |
| H27 | Deduplication plus corpus lock/snapshot behavior | pending | generated clustering/lock fixtures and comparators; runtime identity and threshold differences classified |
| H28 | Screening and local Full Text overlap | pending | generated PHP fixtures for the shared local contract surface; app/path/runtime projections excluded |
| H29 | Citation network, dissemination exports, and Phase 7 closeout | pending | generated graph/export fixtures, compatibility claim inventory, full validation, and final phase report |

## H25 Evidence

- Generator: `scripts/php-golden/shared-identity-export.php`
- Fixture set: `fixtures/php-golden/shared-identity/v1/`
- PHP source: `nexus-scholar/core@b24d0d71ec7b64003465182477e7edb7f49994f4`
- Equivalent behaviors: normalization, primary precedence, overlap, identifier-set semantics across ordering differences, left-biased title/id merge, direct corpus deduplication, no-id candidate separation, and title lookup.
- Intentional changes under ADR 0007: strict multiple-separator rejection, blank normalized identifier rejection, and no runtime-object-identity deduplication.

## Exit Condition

Phase 7 is complete only when H25-H29 are complete and every retained compatibility statement names its fixture set and comparison result. Uncovered behavior remains explicitly unclaimed.
