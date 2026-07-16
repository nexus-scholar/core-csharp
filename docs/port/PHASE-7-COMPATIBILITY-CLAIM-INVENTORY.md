# Phase 7 Compatibility Claim Inventory (H25-H29)

## Comparator entry points

- H25 compatibility comparator: `tests/NexusScholar.Conformance.Tests/PhpSharedIdentityGoldenTests.cs`
- H26 compatibility comparator: `tests/NexusScholar.Conformance.Tests/PhpSearchGoldenTests.cs`
- H27 compatibility comparator: `tests/NexusScholar.Conformance.Tests/PhpDeduplicationGoldenTests.cs`
- H28 compatibility comparator: `tests/NexusScholar.Conformance.Tests/PhpScreeningFullTextGoldenTests.cs`
- H29 and aggregate closure comparator: `tests/NexusScholar.Conformance.Tests/PhpCompatibilityEvidenceClosureTests.cs`

## Retained claims and exclusions

| H item | Fixture set | Total cases | Equivalent | Intentional changes | PHP defects | C# defects | Scoped claims | Comparator assertions | Uncovered surfaces |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| H25 | `php-shared-identity-v1` at `fixtures/php-golden/shared-identity/v1/` | 12 | 9 | 3 (`ADR 0007`) | 0 | 0 | Shared identity normalization, overlap identity, primary precedence, and no-id candidate separation are retained only by fixture-classified set | `tests/NexusScholar.Conformance.Tests/PhpSharedIdentityGoldenTests.cs` | Uncovered merge fields, persistence projections, and runtime-object fallback parity remain unclaimed |
| H26 | `php-search-v1` at `fixtures/php-golden/search/v1/` | 18 | 15 | 3 (`ADR 0010` / `ADR 0011`) | 0 | 0 | Search query/cache/provider selection/raw trace handling are retained only where comparator classification is explicit | `tests/NexusScholar.Conformance.Tests/PhpSearchGoldenTests.cs` | Live providers, provider SDKs, imported-export PHP parity, and broad Search compatibility remain unclaimed |
| H27 | `php-deduplication-v1` at `fixtures/php-golden/deduplication/v1/` | 16 | 8 | 8 (`ADR 0012` / `ADR 0026`) | 0 | 0 | Exact namespace matching, transitive clustering, and fill-only representative merge are fixture-scoped; snapshot behavior is intentionally non-adopted | `tests/NexusScholar.Conformance.Tests/PhpDeduplicationGoldenTests.cs` | Snapshot equality, persistence, app-run projections, and broad corpus lock claims remain unclaimed |
| H28 | `php-screening-fulltext-v1` at `fixtures/php-golden/screening-fulltext/v1/` | 26 | 16 | 9 | 1 (`php_defect`) | 0 | Screening and local Full Text overlap is retained only for classified cases | `tests/NexusScholar.Conformance.Tests/PhpScreeningFullTextGoldenTests.cs` | OCR, PDF extraction, live retrieval, network behavior, and app projection parity remain unclaimed |
| H29 | `php-citation-export-observations-v1` at `fixtures/php-golden/citation-export/v1/` | 14 | 0 | 14 (`ADR 0027`) | 0 | 0 | Evidence-only graph construction, vocabulary, BibTeX, local GraphML, and filename validation observations; no C# replay target | `tests/NexusScholar.Conformance.Tests/PhpCompatibilityEvidenceClosureTests.cs` | Metrics, shortest paths, snowballing, external graph serializers, dissemination handlers, persistence, PHP export parity, and broad compatibility remain unclaimed; ADR 0033 local review-flow Reporting is separate |

## H29-specific notes

- H29 has no C# replay/comparator target and therefore no Network or PHP Dissemination export implementation claim.
- H29 is non-overlapping with H25–H28 in scope: citation/extras are evidence records only.
- Network and PHP Dissemination export parity claims are explicitly deferred; ADR 0033 separately authorizes local review-flow Reporting without a PHP compatibility claim.

Broad PHP compatibility remains unclaimed. Every retained statement above is limited to its named fixture set and reviewed classification result.
