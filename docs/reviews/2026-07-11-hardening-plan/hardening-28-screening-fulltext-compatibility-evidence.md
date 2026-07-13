# Hardening 28 - Screening and Full Text Compatibility Evidence

Status: implemented; pending protected-branch merge verification.

## Behavior Implemented

- Generates deterministic screening/local Full Text fixture cases from the pinned PHP commit in `specs/SOURCE.lock.json`.
- Demonstrates the shared contract and explicit boundaries for:
  - verdict, stage, artifact, status vocabulary subset compatibility;
  - criteria key order and list order relations;
  - finite confidence bound rejection;
  - Full Text failure/skipped attempt mapping;
  - PDF/XML/text validation category mapping.
- Demonstrates one `php_defect` case where the PHP vote constructor accepts `NAN` confidence while C# rejects non-finite confidence.
- Documents intentional differences where C# does not mirror PHP behavior.
- Keeps execution local-only with no runtime retrieval, network, OCR, or PDF parser invocation.

## Evidence

- Generator: `scripts/php-golden/screening-fulltext-export.php`
- Fixture set: `fixtures/php-golden/screening-fulltext/v1/`
- Comparator: `tests/NexusScholar.Conformance.Tests/PhpScreeningFullTextGoldenTests.cs`
- PHP source: `nexus-scholar/core@b24d0d71ec7b64003465182477e7edb7f49994f4`
- Cases: 26 total
  - 16 `equivalent_serialization`
  - 9 `intentional_change`
  - 1 `php_defect`

### Equivalent Surface (16 cases)

- Verdict, stage, artifact, status vocab subsets
- Criteria key-order and list-order relations
- Finite confidence rejection boundary behavior
- Full Text failure/skipped attempt mapping
- PDF/XML/text validation category mapping

### Intentional Boundaries (9 cases)

- PHP raw criteria hash shape vs C# canonical digest envelope
- PHP council verdicts remain proposals; C# records human-decision-bound authority
- PHP `path` and runtime result fields are treated as projections in C# evidence processing
- C# requires raw-byte digest and source-bound extraction before acceptance
- No live retrieval/networked validation in C#

## Invariants

- Every generated case is replayed from immutable PHP input and comparator-classified output.
- All fixture files are SHA-bound by manifest metadata and checked against pinned commit/lock.
- Generation is pinned to a clean PHP revision matching `specs/SOURCE.lock.json`; mismatched commit or dirty worktree refuses.
- H28 demonstrates no C# defect and no unresolved specification conflict.
- ADR 0013/0014 continue to govern canonicality and contract behavior; ADR 0021/0022 preserve dependency/authority direction.

## Tests

- Comparator coverage in `tests/NexusScholar.Conformance.Tests/PhpScreeningFullTextGoldenTests.cs` pins provenance, inventory, classifications, equivalent semantics, intentional boundaries, and the PHP defect.
- Existing local Screening and Full Text conformance fixtures now carry scoped compatibility non-claims instead of stale no-fixture labels.

## Explicit Non-Claims

- No broad compatibility claim for app routes, jobs, queueing, persistence, or repository wiring.
- No live provider compatibility claims from H28.
- No OCR, PDF parsing, or runtime-path contract adoption claims.
- No broad Screening/Full Text compatibility claims beyond the 26 demonstrated cases.
- No network, queue, or external service boundary behavior claims.

## ADR and PHP Impact

- ADR 0013/0014 remain the governing behavior policy for this comparator slice.
- ADR 0021/0022 remain the binding authority direction for dependency and scientific authority boundaries.
- C# non-claim metadata is narrowed to the demonstrated fixture boundaries; scientific authority, runtime-path, and live-parsing behavior are not widened.
- PHP remains evidence source; C# conclusions remain aligned to accepted ADR behavior when the PHP behavior conflicts.
