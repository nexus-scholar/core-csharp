# Gate 9 Full Text

Status: reconnaissance complete and `ADR 0014` accepted for local Full Text contract. No C# Full Text behavior is implemented by this gate document.

## Goal

Map pinned PHP Full Text retrieval behavior and CLI/Web full-text behavior, then define the local Full Text acquisition, artifact, and extraction contract before C# implementation.

Full Text is the missing evidence bridge between Screening and actual paper artifacts. `ADR 0013` recognizes `full_text` Screening and requires digest-bound artifact evidence. `ADR 0014` now defines the local no-network Core contract for acquisition records, artifact evidence, source attempts, extraction records, and app projection boundaries.

## Sources Read

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0008-provenance-ledger.md`
- `docs/adr/0009-portable-bundle-and-artifact-contract.md`
- `docs/adr/0013-screening-decision-and-conflict-contract.md`
- `docs/adr/0014-fulltext-acquisition-artifact-and-extraction-contract.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `docs/recon/apps/**`
- `specs/SOURCE.lock.json`
- pinned PHP `Dissemination` full-text source, handler, persistence, and tests under `../core`
- CLI full-text commands and tests under `../nexus-cli`
- Web full-text batch/item/retrieval/screening workflows under `../nexus-web`

## Branch Scope

Allowed paths:

- `docs/adr/0014-fulltext-acquisition-artifact-and-extraction-contract.md`
- `docs/gates/GATE-09-FULLTEXT.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `docs/port/php-fulltext-fixture-plan.md` only for fixture consequence clarification

Forbidden paths:

- `src/**`
- `tests/**`
- `fixtures/**`
- `specs/**`
- PHP reference repo changes
- `nexus-cli` changes
- `nexus-web` changes
- generated PHP fixtures
- C# Full Text implementation
- live provider/network code
- PDF extraction implementation
- OCR implementation
- persistence/API/UI/cloud behavior

## Behavior Summary

Pinned PHP Full Text behavior:

- retrieves one `ScholarlyWork` at a time through `RetrieveFullTextHandler`;
- requires a primary id or returns skipped;
- optionally enforces locked project membership before retrieval;
- checks cached successful fetch paths before source attempts;
- tries sources in configured order and returns first successful artifact;
- persists failure attempts and continues to later sources;
- validates PDF, XML, and text payloads before storage;
- stores PDF/XML artifacts and may derive text sidecars from XML;
- records fetch audit rows in `pdf_fetches`;
- exposes read APIs through `FullTextFetchReaderPort`.

Observed PHP source aliases:

- `direct`
- `unpaywall`
- `pmc`
- `europe_pmc`
- `arxiv`
- `openalex`
- `semantic_scholar`

Observed result statuses:

- `success`
- `failure`
- `skipped`

Observed Web app statuses add product workflow state:

- `queued`
- `running`
- `completed`
- `completed_with_failures`
- `failed`
- `cancelled`
- `manual_needed`

## Core Boundary

The PHP implementation stores and reports `filePath` / `artifact_path`, but this cannot be C# scientific identity.

C# Full Text must use:

- exact raw bytes or verified raw-byte digest;
- `raw-artifact-bytes` digest scope from `ADR 0002`;
- artifact logical references compatible with `ADR 0009`;
- full-text Screening evidence refs compatible with `ADR 0013`.
- acquisition, artifact, source-attempt, and extraction records compatible with `ADR 0014`.

Local paths, storage-disk paths, app routes, app row ids, CLI manifests, Web batches/items, and Web audit rows are projections unless a later ADR maps them into Core records.

## Open Conflicts

`CF-025`: resolved for the local Full Text contract by `ADR 0014`.

Full Text artifact evidence uses exact accepted bytes plus `raw-artifact-bytes` digest. PHP storage paths, CLI manifest paths, Web routes, app row ids, and local paths remain projections.

`CF-026`: narrowed by `ADR 0014`.

The first C# Full Text implementation may proceed only as a no-network slice using user-supplied bytes, deterministic stub artifacts, manual acquisition records, source references, and digest-bound evidence. Live providers, HTTP downloads, provider SDKs, credentials, scraping, paywall bypass, and shadow-library sources remain future work.

`CF-027`: narrowed for Core by `ADR 0014`.

Full-text Screening handoff requires digest-bound artifact or extraction evidence. CLI manifests, Web full-text batches/items, `pdf_fetches`, app audit rows, download routes, and source-full-text item links remain projections unless transformed into `ADR 0014` records.

## Fixture Plan

Planned Full Text fixture families are recorded in `docs/port/php-fulltext-fixture-plan.md` and `docs/port/GOLDEN-FIXTURE-PLAN.md`.

Required future fixture groups:

- retrieval input and candidate boundary;
- source candidate resolution;
- artifact raw-byte digest and validation;
- retrieval result and source-attempt audit;
- acquisition record and no-network source-reference evidence;
- extraction record and derived-text source binding;
- full-text Screening handoff;
- app projection boundary.

Required negative categories:

- raw Search trace used directly as Full Text input;
- CLI title match used as Core candidate identity;
- no-primary-id work treated as retrievable target;
- closed/non-OA source accepted;
- shadow-library or paywall-bypass source accepted;
- local path or storage path used as artifact identity;
- missing raw artifact digest;
- wrong digest scope;
- invalid PDF/XML/text payload accepted;
- failed/skipped item sent to full-text Screening as screenable;
- derived text accepted without source artifact digest binding;
- extraction output treated as replacement for raw artifact;
- OCR or PDF parsing implementation claimed by contract fixtures;
- app batch/item/audit rows treated as Core authority;
- PHP compatibility claimed without generated fixtures.

## Comparator Plan

Comparators must preserve:

- source alias;
- artifact type;
- raw-byte digest;
- digest scope;
- success/failure/skipped status;
- source attempt outcome;
- acquisition kind;
- extraction source artifact digest when derived evidence is present;
- error category;
- metadata that affects legal/source evidence;
- Screening handoff candidate and artifact evidence refs.

Comparators may ignore:

- generated ids;
- runtime durations;
- attempted timestamps;
- local storage paths;
- app route URLs;

only when fixture metadata explicitly marks them non-semantic.

Comparators must not ignore artifact byte digest, digest scope, artifact type, source alias, Screening handoff binding, or authority/projection markers.

## Implementation Readiness

Implementation readiness: **yes, for local no-network C# Full Text implementation against `ADR 0014`**.

Allowed first implementation scope:

- Screening/candidate-set handoff input validation;
- user-supplied local bytes and deterministic stub artifact bytes;
- manual acquisition records and no-network source references;
- raw artifact byte digest validation with `raw-artifact-bytes`;
- source attempt records;
- artifact evidence records;
- derived extraction records or stub/user-supplied extracted text records with source artifact digest binding;
- full-text Screening evidence references compatible with `ADR 0013`.

Implementation readiness remains **no** for:

- live providers;
- HTTP downloads;
- provider SDKs or credentials;
- actual PDF parsing implementation;
- OCR;
- artifact storage implementation;
- persistence/API/UI/cloud behavior;
- PHP compatibility and generated PHP fixture comparison.

## Explicit Claims Not Made

- no C# Full Text implementation
- no generated PHP fixtures
- no PHP compatibility
- no live provider/network behavior
- no provider SDKs, credentials, API integrations, or HTTP clients in C# Core
- no Unpaywall, PMC, Europe PMC, arXiv, OpenAlex, Semantic Scholar, publisher, or direct download implementation
- no paywall bypass
- no shadow-library source
- no scraping
- no PDF extraction implementation
- no OCR implementation
- no persistence/API/UI/cloud behavior
- no CLI/Web behavior change
- no app behavior made authoritative
- no full-text Screening implementation change
- no artifact storage implementation
- no bundle behavior change
- no blueprint conformance
