# Chat Roster

Branch-derived Codex lane roster from current git state after main consolidation and remote branch cleanup.

## Active Lanes

- Lane `main`: current implementation baseline at `ebb7bba`.
- Lane `gh-pages`: public documentation site at `53d7aa4`.

There are no active `cdx/*` branches locally or remotely.

## Branch Containment Relationships

- `main` contains the implemented local review pipeline through Search, Import, Deduplication, Screening, and local no-network Full Text.
- `main` contains UI contracts, sample block plans, Avalonia renderer prototype, and Avalonia sample host.
- `main` contains refreshed README and review docs.
- `gh-pages` remains separate public-site history.

## Status Notes

- Final `main` CI for `ebb7bba` is green on Ubuntu and Windows: https://github.com/nexus-scholar/core-csharp/actions/runs/28380516236
- ADR 0014 defines Full Text input boundary, acquisition records, source attempts, artifact evidence records, raw byte digest identity, extraction records, failure categories, legal/access boundary, app projection boundary, and Screening handoff.
- Local C# Full Text implementation is no-network only.
- Raw artifact identity is exact bytes plus `raw-artifact-bytes` digest.
- Derived extraction evidence must bind back to source artifact id and raw digest, and must not replace raw artifact evidence.
- PHP `pdf_fetches`, CLI manifests, Web batches/items, app audit rows, storage paths, and download routes are projections unless transformed into ADR 0014 records.
- Live providers, scraping, paywall bypass, shadow libraries, artifact storage, actual PDF parsing, OCR, and app behavior as Core authority remain unclaimed.

## Recommended Next Conversation

Focus next on public-feedback readiness:

1. getting-started tutorial;
2. sample host screenshot/GIF;
3. issue templates;
4. first-tester checklist;
5. maintainer routing docs refresh.

## Explicit Non-Claims For Next Lane

- no PHP compatibility
- no PHP-generated fixtures
- no persistence/API/UI/cloud
- no live provider/network behavior
- no provider SDKs or credentials
- no paywall bypass
- no shadow-library source
- no Google Scholar scraping
- no actual PDF text extraction
- no OCR
- no artifact storage implementation
- no Screening behavior change
- no app behavior made authoritative
