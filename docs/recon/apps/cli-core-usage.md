# Nexus CLI Core Usage Recon

Status: app-layer reconnaissance evidence only.

This document records how `nexus-scholar/nexus-cli` consumes the PHP `nexus-scholar/core` package. It is not a C# Core source of truth, does not define C# behavior, and does not claim PHP compatibility.

## Authority Boundary

Use this order when evidence conflicts:

1. Accepted C# Core ADRs and specs.
2. Golden fixtures and generated comparator evidence.
3. Pinned PHP Core behavior under `specs/SOURCE.lock.json`.
4. CLI behavior as integration evidence.

The CLI is useful consumer evidence because it exposes Search, Screening, Full Text, Graph, Corpus Lock, and Export workflows to users. It must not override accepted Core ADRs.

## Inventory

Repository: `C:\Users\mouadh\Documents\AI in research\nexus-cli`

Observed shape:

- Language and framework: PHP, Laravel 13 Artisan app.
- Package dependency: `nexus-scholar/core:^1.0`.
- Primary entrypoints: `app/Console/Commands`.
- Local outputs: `storage/runs`, `storage/screens`, `storage/pdfs`, `storage/graphs`, and `docs/wiki`.
- Tests: Pest feature tests under `tests/Feature/Commands`.
- Provider configuration: Laravel `config/nexus.php` for scholarly providers and full-text sources.

User-visible commands include:

- `nexus:search`
- `nexus:run-stats`
- `nexus:ingest`
- `nexus:screen`
- `nexus:screen-adjudicate`
- `nexus:screen-compare`
- `nexus:corpus-lock`
- `nexus:fetch-full-text`
- `nexus:fetch-pdfs`
- `nexus:full-text-artifacts`
- `nexus:graph`
- `nexus:export-bibliography`
- `nexus:exports`
- `nexus:jobs`
- `nexus:wiki-init`

## Direct Core Usage

| File/path | Core concept | Usage | Risk | Notes |
| --- | --- | --- | --- | --- |
| `composer.json` | PHP Core package | Requires `nexus-scholar/core:^1.0` | Low | This is the PHP package, not C# Core. |
| `app/Console/Commands/NexusSearch.php` | Search | Calls `SearchAcrossProvidersHandler` or `SearchExecutorPort` | Medium | User-visible run files depend on PHP Core Search shape. |
| `app/Search/SearchQueryDefinition.php` | Search command | Builds `SearchAcrossProviders`; reflects optional constructor parameters | Medium | Reflection is a compatibility shim around a moving Core API. |
| `app/Search/SearchRunService.php` | `CorpusSlice`, `ScholarlyWork` | Merges query corpora and strips raw data | High | Uses `CorpusSlice::fromWorksUnsafe`; this must not become canonical Search output policy. |
| `app/Search/SearchResultSerializer.php` | `ScholarlyWorkDto` | Serializes Core works and derives fallback display keys | High | Title/year/provider fallback hashes are projection identifiers only. |
| `app/Console/Commands/NexusScreen.php` | Screening | Project mode calls `ScreenCorpusHandler`; file mode implements local screening | High | File mode is app behavior outside Core. |
| `app/Console/Commands/NexusCorpusLock.php` | Corpus lock | Calls `LockCorpusHandler` and `CorpusSnapshotRepositoryPort` | Medium | Snapshot behavior is PHP Core/app evidence, not C# snapshot authority. |
| `app/Console/Commands/NexusExportBibliography.php` | Export | Uses `CorpusLockPolicy`, `ProjectCorpusWorksPort`, and `ExportBibliographyHandler` | Medium | Citable/final metadata depends on PHP Core lock policy. |
| `app/FullText/ScreenedRunFullTextRetriever.php` | Full text | Calls `RetrieveFullTextHandler` and writes a local manifest | High | The manifest is not an ADR 0009 review-bundle manifest. |
| `app/Citation/CitationGraphRunAnalyzer.php` | Citation graph | Converts run JSON to PHP Core citation graph use cases | Medium | App parses raw provider relationship shapes before Core graph calls. |

## Behavior Outside Core

The CLI owns several projection and host behaviors:

- run JSON file layout and `latest.json` pointer
- `docs/wiki` paper pages
- command-line rendering and summaries
- file-based deterministic and LLM-assisted screening
- full-text retrieval manifest JSON
- graph JSON output files
- local path normalization for command arguments

These are app-level outputs unless a future accepted Core ADR maps them into Core records.

## Search Impact

The CLI reinforces that C# Search must be app-consumer aware:

- run displays need provider stats, total raw, total unique, duration, and partial failure evidence;
- raw provider sightings must be preserved before Deduplication;
- duplicate provider sightings must not be lost inside Search;
- no-id candidates must remain staged candidates;
- CLI display fallback hashes must not become scientific identity.

## Risks To Carry Forward

- `SearchRunService` uses `CorpusSlice::fromWorksUnsafe`.
- `SearchResultSerializer` uses a title/year/provider fallback hash when no primary id exists.
- file-based deterministic/LLM screening exists outside Core.
- CLI full-text manifest is not an ADR 0009 bundle artifact manifest.
- CLI wiki pages are projections, not canonical scientific state.

## Non-Claims

- no C# Core behavior defined
- no PHP compatibility claimed
- no app behavior modified
- no provider/network behavior implemented
- no Search, Deduplication, or Screening implementation started
- no bundle, provenance, protocol, persistence, API, UI, or cloud behavior moved into Core
