# PHP Full Text Behavior Map

Status: reconnaissance and planning only. No C# Full Text behavior is implemented by this document.

Pinned PHP source:

- Repository: `../core`
- Commit: `b24d0d71ec7b64003465182477e7edb7f49994f4`
- Source lock: `specs/SOURCE.lock.json`

Local note: the pinned PHP checkout was inspected at the locked commit. The checkout contains unrelated local `composer.json` and `composer.lock` modifications; those files were not edited for this reconnaissance.

## Sources Read

- `AGENTS.md`
- `PLANS.md`
- `docs/adr/0001-source-of-truth-and-porting.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0008-provenance-ledger.md`
- `docs/adr/0009-portable-bundle-and-artifact-contract.md`
- `docs/adr/0013-screening-decision-and-conflict-contract.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `docs/recon/apps/**`
- `specs/SOURCE.lock.json`
- `../core/docs/v1.0/modules/07-core-full-text-and-dissemination.md`
- `../core/src/Dissemination/**`
- `../core/src/Laravel/NexusServiceProvider.php`
- `../core/src/Laravel/Persistence/EloquentPdfFetchRepository.php`
- `../core/src/Laravel/Persistence/EloquentFullTextFetchReader.php`
- `../core/src/Laravel/Migration/2026_04_27_000013_create_pdf_fetches_table.php`
- `../core/tests/Feature/Dissemination/RetrieveFullTextFeatureTest.php`
- `../core/tests/Unit/Dissemination/**`
- `../core/tests/Feature/Laravel/RetrieveFullTextJobTest.php`
- `../core/tests/Feature/Persistence/PdfFetchRepositoryTest.php`
- `../core/tests/Feature/Persistence/HostReadApiTest.php`
- `../nexus-cli/app/Console/Commands/NexusFetchFullText.php`
- `../nexus-cli/app/Console/Commands/NexusFetchPdfs.php`
- `../nexus-cli/app/Console/Commands/NexusFullTextArtifacts.php`
- `../nexus-cli/app/FullText/ScreenedRunFullTextRetriever.php`
- `../nexus-cli/tests/Feature/Commands/NexusFetchPdfsTest.php`
- `../nexus-web/docs/workflow-7-full-text-retrieval.md`
- `../nexus-web/docs/workflow-8-full-text-screening.md`
- `../nexus-web/app/Actions/Projects/BuildProjectFullTextCandidates.php`
- `../nexus-web/app/Actions/Projects/StartProjectFullTextBatch.php`
- `../nexus-web/app/Jobs/RunProjectFullTextBatchJob.php`
- `../nexus-web/app/Queries/Projects/ProjectFullTextReadModel.php`
- `../nexus-web/app/Http/Controllers/Projects/ProjectFullTextArtifactController.php`
- `../nexus-web/tests/Feature/ProjectFullTextWorkflowTest.php`

## Behavior Summary

PHP Full Text behavior lives in the `Dissemination` context. It retrieves legal open-access full-text artifacts for scholarly works, validates PDF/XML/text payloads, stores artifacts through storage ports, records `pdf_fetches` audit rows, and exposes fetch-history read ports.

The behavior is not a pure domain transformation. It combines:

- live source resolution;
- HTTP downloads;
- local or Laravel storage;
- fetch audit persistence;
- project lock checks;
- retry and cooldown policy;
- app-specific batch and manifest projections in CLI/Web.

That makes it unsuitable for immediate C# implementation without a local contract. C# already has `ADR 0002` raw-byte digest rules, `ADR 0009` artifact rules, and `ADR 0013` full-text Screening evidence requirements. Full Text must bind those together before any implementation can claim scientific artifact evidence.

## Retrieval Input Shape

PHP Core command: `RetrieveFullText`.

Fields:

- `work`: `ScholarlyWork`
- `destinationFolder`: default `pdfs`
- `maxDownloadAttempts`: default `2`, must be at least `1`
- `maxBytes`: default `50_000_000`, must be greater than `0`
- `failedAttemptCooldownSeconds`: default `3600`, must not be negative
- `projectId`: optional

The handler skips retrieval when the work has no primary id:

```text
status = skipped
reason = Work has no primary ID
```

When `projectId` is supplied and the project is locked, PHP checks that the work belongs to the locked project before storing artifacts. This is relevant to C# because Screening full-text handoff must not retrieve or inspect records outside the locked/reviewable candidate set.

## Relationship To Screening

PHP Core Full Text can operate on a single `ScholarlyWork`.

CLI behavior derives retrieval candidates from a screen JSON file. It reads included decisions by title, looks up the run JSON, converts run entries to `ScholarlyWork`, and retrieves full text only for included titles. That title linkage is app-local and too weak for C# Core scientific identity.

Nexus Web is stricter:

- it requires a completed title/abstract screening batch;
- it requires a latest locked representative snapshot;
- it includes final `include` and `needs_review` title/abstract outcomes;
- it excludes final `exclude` outcomes;
- it blocks automatic retrieval when the protocol says `manual_uploads_only`;
- it starts a full-text batch and item rows around Core retrieval;
- it only lets successful retrieval artifacts feed full-text Screening assignments.

`ADR 0013` already says full-text Screening requires artifact evidence and rejects local paths as scientific identity. This Full Text recon confirms that the missing C# contract is not the screening decision itself, but the artifact evidence record that can feed that decision.

## Source Behavior

PHP source order in the Laravel service provider is:

1. `direct`
2. `unpaywall`, only when enabled and email is configured
3. `pmc`
4. `europe_pmc`
5. `arxiv`
6. `openalex`
7. `semantic_scholar`

The source collection order matters because the handler returns on the first successful retrieved artifact and continues to later sources after a source failure.

### Direct

`DirectPdfSource` reads raw provider metadata and accepts HTTP(S) URL fields such as:

- `direct_pdf_url`
- `directPdfUrl`
- `pdf_url`
- `pdfUrl`
- `full_text_pdf_url`
- `fullTextPdfUrl`

It also searches nested `full_text`, `fullText`, `pdf`, and `document` objects. It ignores invalid schemes and generic landing page URLs.

### Unpaywall

`UnpaywallPdfSource` requires DOI and configured email. It calls the Unpaywall API, rejects closed or non-OA records, and searches `best_oa_location` plus `oa_locations` for `url_for_pdf`. Metadata includes OA status, license, host type, version, landing page URL, and evidence.

### PMC OAI

`PmcOaiFullTextSource` requires PMCID, `prefer_xml`, and enabled config. It builds an OAI `GetRecord` request, rejects OAI errors, and returns XML candidates when reusable full-text metadata exists. Metadata includes PMCID, OAI identifier, metadata prefix, license, and reusable marker.

### Europe PMC

`EuropePmcFullTextSource` searches by DOI or PMCID. It prefers open PDF links when configured to prefer PDF, then falls back to `fullTextXML` when XML is preferred and an open full-text signal exists. Metadata carries source id, DOI, PMCID, license, open-access fields, full-text ids, and availability signals.

### arXiv

`ArXivPdfSource` derives a PDF URL from an arXiv WorkId:

```text
https://arxiv.org/pdf/{id}.pdf
```

### OpenAlex

`OpenAlexPdfSource` reads `best_oa_location.pdf_url`, `primary_location.pdf_url`, `locations[*].pdf_url`, or `oa_locations[*].pdf_url` from raw provider metadata.

### Semantic Scholar

`SemanticScholarPdfSource` reads raw `openAccessPdf.url`. It is less strict than other sources and does not validate URL shape in the source itself.

### Disabled Or Forbidden Sources

Both CLI and Web config include `shadow_libraries` with `enabled = false`. Web workflow docs explicitly say not to add paywall bypass or shadow-library sources. This should be a C# product-law-level boundary: access-controlled publisher content must not be scraped or bypassed.

## Result Shape

PHP `FullTextResult` carries:

- `status`: `success`, `failure`, or `skipped`
- `filePath`
- `sourceAlias`
- `errorMessage`
- `httpStatus`
- `metadata`

`FullTextFetchRecord` carries persisted audit shape:

- `id`
- `workId`
- `sourceAlias`
- `sourceUrl`
- `status`
- `httpStatus`
- `filePath`
- `durationMs`
- `errorMessage`
- `attemptedAt`
- `metadata`
- `createdAt`
- `updatedAt`

PHP Web adds product workflow states outside Core:

- batch statuses: `queued`, `running`, `completed`, `completed_with_failures`, `failed`, `cancelled`
- item statuses: `queued`, `running`, `success`, `failed`, `skipped`, `manual_needed`

Those app statuses are useful workflow evidence but are not automatically C# Core Full Text records.

## Artifact Validation

PHP validates artifacts before storage:

- PDF byte size must not exceed `maxBytes`.
- PDF bytes must start with `%PDF-`.
- PDF content type, when present, must be `application/pdf`, `application/x-pdf`, or `application/octet-stream`.
- XML byte size must not exceed `maxBytes`.
- XML must begin with `<`, must not be an HTML page, and must parse as XML with external network loading disabled.
- XML content type, when present, must be XML-like.
- Text byte size must not exceed `maxBytes`.
- Text content must not be empty.

For XML artifacts, PHP may extract a text sidecar and store it as a separate path with metadata:

- `text_file_path`
- `text_extraction = xml_text_content`

C# must decide whether XML text sidecars are canonical artifacts, derived projections, or both with explicit digest links.

## Path And Storage Behavior

PHP stores through `FileStoragePort` or streaming storage ports. Stored paths are deterministic-ish sanitized storage references:

```text
{safe destination}/{safe work id}_{safe source alias}_{sha256(workId|sourceAlias)[0..16]}.{extension}
```

The handler sanitizes folders and path segments and removes traversal segments. CLI also sanitizes destination folders.

Important C# implication:

- PHP `filePath`, CLI `artifact_path`, and Web `artifact_path` are storage references.
- They are not scientific identity.
- C# Full Text artifact identity must use raw bytes and `raw-artifact-bytes` digests.
- Local filesystem paths, storage-disk paths, app row ids, and download routes must remain projections.

## Digest And Checksum Behavior

No PHP Full Text evidence found in this recon records a raw artifact byte digest or checksum as part of `FullTextResult`, `FullTextFetchRecord`, CLI manifest entries, or Web `project_full_text_items`.

This is a direct conflict with C# requirements:

- `ADR 0002` says opaque binary payloads are hashed as raw bytes under `raw-artifact-bytes`.
- `ADR 0009` defines artifact entries using logical paths and raw-byte digests.
- `ADR 0013` requires full-text Screening decisions to cite digest-bound artifact evidence, not local paths.

Therefore, PHP path-based audit rows are source evidence only. A C# Full Text contract must add raw-byte digest binding before local implementation.

## Manifest Behavior

CLI writes a local manifest at:

```text
{destination}/manifest.json
```

Each entry may include:

- `title`
- `primary_id`
- `ids`
- `status`
- `source_alias`
- `artifact_path`
- `http_status`
- `error`
- `metadata`

This manifest is not an `ADR 0009` review-bundle manifest. It lacks manifest digest, logical artifact entries, raw-byte digests, bundle schema identity, protocol/workflow/provenance bindings, and import verification semantics.

## Retry, Rate Limit, And Cooldown Behavior

PHP uses two related policies:

- source API clients use `FullTextSourceConfig` values such as `rate_limit`, `timeout`, and `max_retries`;
- artifact download uses `maxDownloadAttempts` in `RetrieveFullText`.

The handler also checks recent failed attempts by work id and source URL. If a recent failure exists inside the configured cooldown window, that source URL is skipped.

The C# local implementation should not use live providers or network behavior until a provider/network gate admits those dependencies. A first local implementation would need deterministic source fixtures or user-supplied local artifact evidence.

## Error And Partial Failure Behavior

PHP attempts sources in order. A failing source is saved as a failed fetch audit record, and the handler continues to the next source. The final result is:

- first successful source result, if any source succeeds;
- last failure result, if at least one source failed and no later source succeeded;
- generic failure `No full-text artifact found across all sources`, if no source produced a candidate and none failed;
- skipped when the work lacks a primary id.

This means failure evidence is part of the audit trail, not merely an exception.

## CLI Behavior

CLI commands:

- `nexus:fetch-full-text`
- `nexus:fetch-pdfs` as legacy alias
- `nexus:full-text-artifacts`

CLI `nexus:fetch-full-text`:

- defaults to a screen file inferred from `storage/runs/latest.json`;
- reads `storage/screens/{run}.json`;
- reads the referenced run JSON;
- includes only entries whose screen decision has `included = true`;
- matches included records by title;
- stores artifacts under `full-text/{run_id}` by default;
- writes a local manifest JSON;
- can output a machine-readable summary.

CLI `nexus:full-text-artifacts` reads `FullTextFetchReaderPort` records by work or project.

CLI behavior is app-local. It should not define C# Core candidate identity because it uses title matching and local file paths.

## Web Behavior

Nexus Web builds a richer full-text workflow:

- candidate construction from completed title/abstract outcomes;
- latest locked representative snapshot check;
- protocol policy check;
- full-text batch and item persistence;
- background retrieval job;
- item status rollups;
- artifact download route guarded by project policy;
- source audit display through `FullTextFetchReaderPort`;
- full-text Screening setup only for successful artifact items.

Web app rows include:

- `project_full_text_batches`
- `project_full_text_items`
- `source_full_text_batch_id`
- `source_full_text_item_id`
- `audit_events`

These are integration evidence and product workflow records. They are not Core authority until a later ADR maps them to digest-bound Full Text records.

## Behaviors To Port Later

Recommended C# local contract direction:

- define a `nexus.full-text.artifact-evidence` record;
- require exact raw bytes or a verified raw-byte digest;
- compute `raw-artifact-bytes` digest over exact bytes;
- preserve artifact type: `pdf`, `xml`, `text`, and future sidecar type if accepted;
- preserve source attempt records and failed/skipped attempts;
- preserve source URL and source alias as evidence, but not as identity;
- bind to Screening candidate set, Screening decision, and/or Dedup candidate ids where applicable;
- preserve legal-access policy and source policy;
- expose enough evidence for `ADR 0013` full-text decisions to reference artifact digests;
- keep CLI/Web batch and storage rows as projections.

## Intentional Incompatibilities To Consider

Likely C# incompatibilities:

- C# must require raw-byte digest evidence where PHP records only a file path.
- C# must reject local path, app route, app row id, or storage disk path as artifact identity.
- C# first implementation should not call live Unpaywall, PMC, Europe PMC, arXiv, OpenAlex, Semantic Scholar, or publisher URLs.
- C# should not use CLI title matching as candidate identity.
- C# should not treat Web full-text batch/item rows as Core authority without an ADR.
- C# should not treat XML text sidecars as canonical without explicit source artifact and derivation digest rules.
- C# should not claim PHP compatibility without generated PHP fixtures and comparators.

## Required C# Decisions Before Implementation

Implementation readiness is **no** until these decisions are made:

- Full Text artifact evidence record shape.
- Raw-byte digest requirement and accepted digest scope.
- Whether a C# local first slice uses user-supplied local bytes, stub artifacts, or imported artifacts.
- How source attempts bind to Search/Dedup/Screening candidate records.
- How full-text evidence binds to `ADR 0013` Screening decisions.
- Which PHP source aliases are admitted as planned source evidence versus future live providers.
- Whether XML and extracted text sidecars are canonical artifacts or projections.
- How to represent cache reuse and recent failure cooldown without local persistence.
- Which app batch/item/audit fields remain projections.
- Fixture and comparator policy for source URLs, local paths, generated ids, timestamps, durations, HTTP status, and failed attempts.

## Explicit Non-Claims

- no C# Full Text implementation
- no generated PHP fixtures
- no PHP compatibility
- no live provider/network behavior
- no Unpaywall, PMC, Europe PMC, arXiv, OpenAlex, Semantic Scholar, or publisher integration
- no paywall bypass
- no shadow-library source
- no Google Scholar scraping
- no persistence schema
- no API/UI/cloud behavior
- no CLI/Web behavior changes
- no app behavior made authoritative
- no full-text Screening implementation change
- no artifact storage implementation
- no bundle behavior change
- no blueprint conformance
