# Hardening 27 - Deduplication Compatibility Evidence

Status: implemented; pending protected-branch merge verification.

## Behavior Implemented

- Generates deterministic Deduplication and corpus lock/snapshot observations from the PHP commit pinned by `specs/SOURCE.lock.json`.
- Compares exact DOI, arXiv, OpenAlex, Semantic Scholar, and PubMed matching, empty input, exact transitive clustering, and fill-only representative merge semantics.
- Classifies PHP singleton-cluster output, threshold `92` drift, fuzzy-title automatic clustering, no-id runtime identity, and pre-collapsed `CorpusSlice` input as intentional differences under ADR 0007 and ADR 0012.
- Classifies PHP lock rejection and snapshot-backed export metadata as intentional non-adoption under ADR 0026.
- Adds no live provider, Composer, database, Laravel container, or network dependency.

## Evidence

- Generator: `scripts/php-golden/deduplication-export.php`
- Fixture set: `fixtures/php-golden/deduplication/v1/`
- Comparator: `tests/NexusScholar.Conformance.Tests/PhpDeduplicationGoldenTests.cs`
- PHP source: `nexus-scholar/core@b24d0d71ec7b64003465182477e7edb7f49994f4`
- Cases: 16 total; 8 equivalent semantic cases and 8 intentional changes.

## Invariants Enforced

- Generation refuses a mismatched commit or dirty tracked PHP worktree.
- Input, output, source lock, and classifications are SHA-256 bound by the generated manifest.
- Every case has one reviewed classification; H27 cannot close with a known C# defect or unresolved specification conflict.
- Exact stable identifiers remain namespace-sensitive with confidence `1.0`.
- C# consumes raw Search/import evidence, preserves no-id candidates, and never treats PHP object hashes as scientific identity.
- Fuzzy title evidence remains review-required in C# and never creates automatic scientific identity.
- Runtime cluster ids, object hashes, retrieval timestamps, and durations are excluded.

## Explicit Non-Claims

- No broad PHP Deduplication compatibility beyond the generated H27 cases.
- No corpus lock implementation, snapshot identity/equality, snapshot persistence, or citable-export rule in C#.
- No Laravel repository, migration, transaction, queue, audit-row, or Web membership-hash compatibility.
- No Search, Screening, provider/network, API, UI, cloud, or production persistence expansion.

## ADR And PHP Impact

- ADR 0026 resolves only the H27 classification boundary for PHP lock/snapshot observations; broader `CF-014` snapshot semantics remain unresolved.
- No existing scientific identity or Deduplication contract is widened.
- PHP behavior remains evidence only. Local C# behavior continues to follow ADR 0007 and ADR 0012.
