# Gate 9 Search Import Evidence Sheet (Local Slice)

## Scope Implemented on Branch

- local parser slice for `acquisition_kind = imported-export`
- formats implemented: `ris`, `bibtex`, `scopus-csv`
- supported metadata fields persisted in `SearchImportMetadata`
- raw file digests computed from exact source bytes with `raw-artifact-bytes` scope
- parser warning/error preservation
- no title-only deduplication in Search trace
- no source-specific id promotion to `WorkIdNamespace`
- Google Scholar scraping rejected by format gate

## Test Coverage Added

- `tests/NexusScholar.Core.Tests/SearchImportServiceTests.cs`
  - imported-by required
  - imported-at required
  - exact source file digest binding
- conformance fixture presence and manifest cases:
  - `tests/NexusScholar.Conformance.Tests/SearchFixtureTests.cs`
  - added 9 imported search fixture files under `fixtures/conformance/search/`

## Conformance Fixtures Added

- `search-import-ris-trace.json`
- `search-import-bibtex-trace.json`
- `search-import-scopus-csv-trace.json`
- `search-import-source-file-digest.json`
- `search-import-parser-warning.json`
- `search-import-no-id-candidates.json`
- `search-import-dedup-not-applied.json`
- `search-import-source-specific-id-not-workid.json`
- `search-import-google-scholar-scraping-rejected.json`

## Remaining Risks and Explicit Non-Claims

- no PHP-generated fixture or PHP compatibility claim
- no live providers, no Scopus/Web of Science API, no Google Scholar scraping
- no Deduplication, Screening, persistence, API/UI, or CLI/Web integration in this gate
