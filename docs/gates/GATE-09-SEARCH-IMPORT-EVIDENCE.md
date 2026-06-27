# Gate 9 Search Import Evidence Sheet (Local Slice)

## Scope Implemented on Branch

- local parser slice for `acquisition_kind = imported-export`
- formats implemented: `ris`, `bibtex`, `scopus-csv`
- supported metadata fields persisted in `SearchImportMetadata`
- raw file digests computed from exact source bytes with `raw-artifact-bytes` scope carried in metadata
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
  - source-file digest fixture asserts canonical `sha256:<hex>` rendering and `raw-artifact-bytes` scope

## Local Verification

- `dotnet build NexusScholar.Core.slnx -c Release`
- `dotnet test NexusScholar.Core.slnx -c Release --no-build`
  - Architecture: 13 passed
  - Conformance: 52 passed
  - Core: 141 passed
- `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1`

## Hosted Verification

- Implementation commit: `06c28bd69aa20a5525baec9f3a7d3b7b8f311f85`
- Hosted CI run: `https://github.com/nexus-scholar/core-csharp/actions/runs/28291815861`
- `verify (ubuntu-latest)`: success
- `verify (windows-latest)`: success
- Steps passed on both: checkout, .NET setup, restore, build, test, format

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
