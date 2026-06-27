# Gate 9 Search Import Source Contract (Local First Slice)

Status: local imported-export parser slice implemented, non-local parser families remain future.

## Goal

Implement the first local imported-export parsing slice for user-supplied RIS, BibTeX, and Scopus CSV exports under `ADR 0011`.

This work is restricted to `acquisition_kind = imported-export` evidence parsing and local Search trace projection.
No live providers, APIs, or scraping behavior is implemented.

## Sources Read

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0009-portable-bundle-and-artifact-contract.md`
- `docs/adr/0010-search-trace-and-plan-contract.md`
- `docs/adr/0011-search-import-source-contract.md`
- `docs/gates/GATE-09-SEARCH.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `docs/port/php-search-behavior.md`
- `docs/port/php-search-fixture-plan.md`

## Branch Scope

Allowed paths:

- `src/NexusScholar.Search/**`
- `src/NexusScholar.Kernel/**` (only if a primitive is genuinely reusable)
- `src/NexusScholar.Shared/**` (only existing identity primitives)
- `tests/NexusScholar.Core.Tests/**`
- `tests/NexusScholar.Architecture.Tests/**` (no changes expected in this slice)
- `tests/NexusScholar.Conformance.Tests/**`
- `fixtures/conformance/search/**`
- `docs/gates/GATE-09-SEARCH-IMPORT.md`
- `docs/gates/GATE-09-SEARCH-IMPORT-EVIDENCE.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

Forbidden:

- live provider/network adapters
- provider network adapters
- HTTP clients
- Scopus API
- Web of Science API
- Google Scholar scraping
- provider SDKs
- API keys or credentials
- imported-export parser families outside this first slice
- PHP-generated fixtures
- Deduplication
- Screening
- persistence/API/UI/cloud
- nexus-cli changes
- nexus-web changes

## Implemented Local Slice

- Parser implementation for:
  - RIS (`ris`)
  - BibTeX (`bibtex`)
  - Scopus CSV (`scopus-csv`)
- Imported source metadata:
  - `acquisition_kind = imported-export`
  - `source_database_or_tool`
  - `export_format`
  - `parser_id`
  - `parser_version`
  - `source_file_digest` derived from exact source bytes
  - `imported_by`
  - `imported_at`
  - optional `original_query_text`
  - optional `exported_at`
  - `record_count`
  - `parser_warnings`
- Imported record projection:
  - required stable identifiers normalize through existing `ADR 0007` namespaces only
  - no new namespace expansion for source-specific ids
  - source-specific ids preserved in `source_identifiers`/`raw_identifiers`
  - no-id records become unresolved candidates
  - each parsed record maps into `SearchSighting`
  - skipped records are preserved as skipped imported records and skipped from sightings
  - parser warning/error evidence is preserved where possible
- Local policy:
  - raw exported bytes are required and digested directly as source file evidence
  - imported records are not deduplicated inside Search
  - title-only overlap does not deduplicate imported Trace evidence
  - Google Scholar scraping is rejected

## Conflict Status

`CF-019`: implemented for local first-slice import parser behavior only.

- Implemented:
  - local imported-export parser behavior
  - parser-slice for RIS/BibTeX/Scopus CSV
  - raw source file digest binding
- Still future:
  - live providers/API integrations
  - PHP compatibility
  - Scopus API
  - Web of Science API
  - broader app alignment
  - future import formats (WOS, Zotero, EndNote, Publish or Perish)

Unchanged:

- `CF-013`: implemented for local Search cache identity.
- `CF-016`: implemented for local raw Search trace and no-Deduplication boundary.
- `CF-017`: implemented for local schema-closed Search plans.
- `CF-018`: narrowed for Search consumer boundary; broader app alignment remains pending.

## Fixture Evidence

Implemented local conformance fixtures:

- `search-import-ris-trace.json`
- `search-import-bibtex-trace.json`
- `search-import-scopus-csv-trace.json`
- `search-import-source-file-digest.json`
- `search-import-parser-warning.json`
- `search-import-no-id-candidates.json`
- `search-import-dedup-not-applied.json`
- `search-import-source-specific-id-not-workid.json`
- `search-import-google-scholar-scraping-rejected.json`

Pending future fixture families:

- `search-import-wos-export-trace.json`
- `search-import-zotero-csl-json-trace.json`
- `search-import-endnote-export-trace.json`
- `search-import-publish-or-perish-csv-trace.json`

Negative cases covered:

- unsupported import format
- missing import actor
- malformed/missing required field
- skipped record evidence
- parser warning preservation
- parser metadata preservation
- source-specific id not promoted to WorkId
- unknown identifier handling
- title-only duplicate not deduped by Search
- Google Scholar scraping not allowed

## Not Ready

- no live provider/API behavior
- no Scopus or Web of Science API work
- no Google Scholar scraping
- no PHP compatibility
- no generated PHP fixtures
- no Deduplication
- no Screening
- no persistence/API/UI/cloud behavior
- no app authority
- no blueprint claim
