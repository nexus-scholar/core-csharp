# ADR 0014: Full Text Acquisition, Artifact, and Extraction Contract

Status: Accepted

Date: 2026-06-28

## Context

Gate 9 Full Text reconnaissance mapped pinned PHP Core, CLI, and Web behavior before any C# Full Text implementation.

The pinned PHP reference retrieves one `ScholarlyWork` at a time through `RetrieveFullTextHandler`. It can resolve live source candidates, download over HTTP, validate PDF/XML/text payloads, store files through storage ports, record `pdf_fetches` rows, retry or skip sources, and expose fetch history through read ports. CLI adds a local full-text manifest over title-matched screening results. Web adds full-text batches, item rows, download routes, and full-text Screening handoff workflows.

Those behaviors are useful evidence, but they conflict with local C# authority unless constrained:

- `ADR 0002` requires raw binary payloads to use `raw-artifact-bytes` digest scope.
- `ADR 0007` rejects title-only and runtime-object identity.
- `ADR 0008` defines provenance events but does not make app audit rows provenance.
- `ADR 0009` defines bundle/artifact logical path and raw-byte digest rules, but does not define Full Text acquisition.
- `ADR 0010` says Search traces are raw evidence, not screenable/retrievable candidate identity.
- `ADR 0011` distinguishes user-supplied imported evidence from live provider integrations.
- `ADR 0012` defines Deduplication results and reviewable candidate evidence.
- `ADR 0013` defines `full_text` Screening decisions and requires digest-bound artifact evidence, not local paths.

The Full Text recon opened these conflicts:

- `CF-025`: Full Text artifact evidence and raw-byte identity.
- `CF-026`: Full Text provider/network and legal-access boundary.
- `CF-027`: Full Text app projection and Screening handoff boundary.

This ADR defines the local C# contract needed before implementation. It does not implement Full Text, generate fixtures, claim PHP compatibility, add live providers, add artifact storage, add persistence/API/UI/cloud behavior, or change Screening behavior.

## Decision

### 1. Full Text Input Boundary

Full Text consumes a Screening output handoff or a locked/reviewable candidate set.

Accepted local input sources are:

- final title/abstract `include` decisions from `ADR 0013`;
- final title/abstract `needs_review` decisions from `ADR 0013` when the protocol or workflow allows retrieval for unresolved/maybe candidates;
- a locked/reviewable candidate set that already binds to Deduplication or Screening evidence;
- a manual acquisition record linked to a candidate from one of the above.

Final title/abstract `exclude` decisions are not retrieval candidates by default.

Raw Search traces are not valid Full Text input. Raw Search import outputs are not valid Full Text input. Raw Dedup member records are not valid Full Text input by themselves. They may be referenced only through an accepted candidate set or Screening handoff that preserves the source bindings.

The local conceptual input schema is:

```text
nexus.fulltext.input
```

with version:

```text
1.0.0
```

The input record carries:

- `input_id`
- `schema_id`
- `schema_version`
- `source_kind`
- `candidate_set_id`
- `candidate_id`
- optional `screening_decision_id`
- optional `screening_stage`
- optional `dedup_result_id`
- optional `dedup_cluster_id`
- optional `work_id` or primary `ADR 0007` identifier
- `eligibility`
- `source_refs`
- `non_claims`

### 2. Acquisition Source Model

Full Text acquisition records describe how artifact evidence was obtained or attempted.

Supported acquisition kinds for the local contract are:

- `user-uploaded-file`
- `user-supplied-local-file`
- `manual-acquisition`
- `deterministic-stub-artifact`
- `external-url-reference`
- `doi-or-landing-page-reference`
- `open-access-source-reference`

`user-uploaded-file` and `user-supplied-local-file` mean the operator supplied bytes to Nexus. The local filesystem path is an input reference only; it is not artifact identity.

`manual-acquisition` records that a human acquired or supplied the artifact outside Nexus. It must still bind the accepted bytes through raw digest evidence before becoming artifact evidence.

`deterministic-stub-artifact` is allowed for local implementation and fixtures. It may use fixed bytes for deterministic validation and evidence-shape tests.

`external-url-reference`, `doi-or-landing-page-reference`, and `open-access-source-reference` may be recorded as source evidence. They do not authorize Nexus to download, scrape, crawl, authenticate, bypass access controls, or call provider APIs in the first implementation.

PHP-observed live sources such as Direct PDF, Unpaywall, PMC OAI, Europe PMC, arXiv, OpenAlex, and Semantic Scholar are admitted as source-reference evidence only. Live source adapters remain future work.

### 3. Acquisition Record Shape

The local acquisition record schema is:

```text
nexus.fulltext.acquisition-record
```

with version:

```text
1.0.0
```

An acquisition record carries:

- `acquisition_id`
- `schema_id`
- `schema_version`
- `input_ref`
- `acquisition_kind`
- `source_alias`
- `source_reference`
- optional `source_url`
- optional `doi_or_landing_page`
- optional `source_metadata`
- `acquired_by`
- `acquired_at`
- `status`
- `source_attempts`
- optional `artifact_evidence_id`
- `warnings`
- `errors`
- `non_claims`

`acquired_by` is required for user-supplied or manual acquisition. It identifies the local human/import actor who accepted the evidence into Nexus. It is not a license certification.

`acquired_at` must be supplied by an injected clock or fixture harness.

### 4. Artifact Evidence Record Shape

The local artifact evidence schema is:

```text
nexus.fulltext.artifact-evidence
```

with version:

```text
1.0.0
```

A Full Text artifact evidence record carries:

- `artifact_id`
- `schema_id`
- `schema_version`
- `input_ref`
- `candidate_id`
- optional `candidate_set_id`
- optional `screening_decision_id`
- optional `work_id` or primary identifier
- optional `dedup_cluster_id`
- `acquisition_id`
- `acquisition_kind`
- `source_alias`
- optional `source_reference`
- optional `source_metadata`
- `artifact_kind`
- `media_type`
- `size_bytes`
- `raw_byte_digest`
- `raw_byte_digest_scope`
- optional `logical_path`
- optional `original_file_name`
- `validation_status`
- `warnings`
- `errors`
- `non_claims`

`raw_byte_digest_scope` must be:

```text
raw-artifact-bytes
```

`raw_byte_digest` is computed over the exact accepted artifact bytes. Line endings, text normalization, local paths, file names, MIME sniffing results, source URLs, and extracted text are not substitutes for the raw bytes.

`logical_path` is an export or bundle-facing logical reference when one exists. It must follow the existing logical path constraints from `ADR 0009`. It is not artifact identity.

`original_file_name`, upload path, local file path, temp path, app route, storage path, app row id, and CLI manifest path are projections only and must not enter artifact identity.

### 5. Artifact Kinds

The local artifact kind vocabulary is:

- `pdf`
- `xml`
- `text`
- `derived-text`

`pdf`, `xml`, and `text` are raw accepted artifact kinds when their exact bytes are preserved or digest-bound.

`derived-text` is extracted or transformed evidence. It is not a replacement for the raw artifact and must bind back to the source artifact id and raw digest.

No OCR image layer, page image layer, HTML snapshot, publisher webpage snapshot, or browser-rendered artifact kind is accepted by this ADR. Those require later decisions.

### 6. Raw-Byte Identity

Full Text artifact identity is exact bytes plus `raw-artifact-bytes` digest.

Missing raw digest is invalid. Wrong digest scope is invalid. Digest mismatch is invalid.

Local filesystem paths, storage paths, app routes, app row ids, database ids, CLI manifest paths, download URLs, and source URLs are not artifact identity.

Two records that point to the same local path are not the same artifact unless their raw byte digests match. Two records with the same source URL are not the same artifact unless their raw byte digests match.

### 7. Local First Implementation Boundary

The first C# Full Text implementation is no-network.

It may support:

- user-supplied local bytes;
- user-uploaded bytes from an application adapter;
- deterministic stub artifact bytes;
- manual acquisition records;
- source-reference metadata;
- validation of accepted bytes;
- digest-bound artifact evidence records;
- derived text records supplied by tests or callers when source artifact binding is preserved.

It must not support:

- live HTTP download;
- live provider SDKs;
- provider API credentials;
- Unpaywall API calls;
- PMC OAI calls;
- Europe PMC calls;
- arXiv download calls;
- OpenAlex or Semantic Scholar download calls;
- publisher scraping;
- Google Scholar scraping;
- paywall bypass;
- shadow-library sources.

Live provider/network acquisition requires a later provider/network/legal ADR or gate.

### 8. Source Attempt Model

Source attempts are ordered evidence records inside an acquisition record.

Each source attempt carries:

- `attempt_id`
- `source_alias`
- `attempt_order`
- `acquisition_kind`
- optional `source_url`
- optional `source_reference`
- `status`
- optional `artifact_kind`
- optional `media_type`
- optional `http_status`
- optional `error_category`
- optional `error_message`
- optional `source_metadata`
- optional `artifact_evidence_id`

Status vocabulary:

- `success`
- `failure`
- `skipped`
- `manual_needed`

When multiple source attempts are represented, the first successful artifact is the accepted artifact for the acquisition unless an explicit later policy chooses otherwise. Failed and skipped attempts remain audit evidence and must not be erased by the success summary.

`http_status` is recorded evidence only when already observed by a source or supplied by a deterministic fixture. It is not required for no-network local implementation.

### 9. Legal and Access Boundary

Nexus must not bypass paywalls or access controls.

This ADR forbids:

- paywall bypass;
- shadow-library acquisition;
- credentialed scraping;
- unauthenticated publisher scraping that violates access controls;
- Google Scholar scraping, crawling, captcha avoidance, or browser automation;
- provider SDK/API behavior without a later provider/network gate.

User-supplied files may be accepted as evidence with provenance fields, but Core does not certify license legality, copyright status, or redistribution rights. License and access notes are evidence metadata, not legal conclusions.

Open-access source metadata may be preserved, including OA status, license label, host type, version, landing page, repository/source name, or other source-supplied access evidence. Closed or non-OA source candidates do not produce local live-acquired artifacts under this ADR.

### 10. Validation Policy

The local contract adopts the PHP-observed validation categories as planning evidence but makes raw digest binding mandatory.

PDF validation must support:

- maximum byte size;
- `%PDF-` signature check;
- media type policy when media type is supplied;
- corrupted or invalid PDF error category.

XML validation must support:

- maximum byte size;
- begins-with-XML shape check;
- HTML rejection;
- XML parseability without network entity loading when parsing is implemented;
- XML media type policy when media type is supplied.

Text validation must support:

- maximum byte size;
- non-empty text content;
- media type policy when media type is supplied.

Validation does not require live network access.

### 11. Extraction Model

Raw artifact evidence and extracted evidence are separate records.

The local extraction record schema is:

```text
nexus.fulltext.extraction-record
```

with version:

```text
1.0.0
```

An extraction record carries:

- `extraction_id`
- `schema_id`
- `schema_version`
- `source_artifact_id`
- `source_raw_byte_digest`
- `source_raw_byte_digest_scope`
- `extractor_id`
- `extractor_version`
- `extracted_at`
- `extraction_kind`
- `status`
- optional `extracted_text_digest`
- optional `extracted_text_digest_scope`
- optional `page_text`
- optional `sections`
- `warnings`
- `errors`
- `non_claims`

`source_raw_byte_digest_scope` must be `raw-artifact-bytes`.

Extraction output is derived evidence. It must never replace the raw artifact. A Screening decision may cite extracted evidence only when it can trace back to the source artifact id and raw digest.

If extracted text is represented as a raw text byte artifact, its digest uses `raw-artifact-bytes`. If extracted text is represented as a structured extraction record with sections/pages, the record digest uses `canonical-json-record` under `ADR 0002`. The owning implementation must make that representation explicit.

This ADR defines extraction record shape but does not implement PDF parsing, XML parsing, OCR, page segmentation, section detection, or text extraction algorithms. The first local implementation may validate extraction records or use deterministic stub/user-supplied extracted text, but automated PDF/OCR extraction remains future work unless explicitly scoped later.

### 12. Failure Model

Full Text failures are audit evidence.

Stable error categories include:

- `missing-full-text`
- `inaccessible-full-text`
- `unsupported-file-type`
- `unsupported-acquisition-kind`
- `missing-candidate-binding`
- `raw-search-trace-not-fulltext-input`
- `raw-dedup-record-not-fulltext-input`
- `excluded-candidate-not-retrievable`
- `no-primary-id`
- `missing-human-or-import-actor`
- `missing-raw-artifact-digest`
- `invalid-raw-artifact-digest-scope`
- `raw-artifact-digest-mismatch`
- `local-path-not-artifact-identity`
- `app-projection-not-core-authority`
- `closed-or-non-oa-source`
- `paywall-bypass-forbidden`
- `shadow-library-forbidden`
- `scraping-forbidden`
- `invalid-pdf-signature`
- `invalid-media-type`
- `invalid-xml`
- `html-not-fulltext-xml`
- `empty-text-artifact`
- `artifact-too-large`
- `duplicate-artifact`
- `extraction-failure`
- `partial-extraction`
- `derived-text-missing-source-digest`

Tests and fixtures should assert categories rather than relying only on free-form exception text.

### 13. Duplicate Artifact Policy

Duplicate artifact detection is raw-digest based.

If two candidate acquisitions produce the same `raw-artifact-bytes` digest, they are duplicate artifact evidence. The system may record both acquisition/source contexts, but the underlying artifact bytes are the same evidence object.

Duplicate artifact detection is not Deduplication of scholarly works. It must not merge candidates, works, clusters, Screening decisions, or source attempts.

### 14. Screening Relation

Full-text Screening can cite only digest-bound artifact evidence or derived extraction evidence that traces back to digest-bound artifact evidence.

A successful acquisition with a valid artifact evidence record may become a full-text Screening evidence reference.

Failed, skipped, inaccessible, missing, manual-needed, or partial acquisition records are follow-up/audit states. They are not screenable full-text artifact evidence by themselves.

Local paths, storage paths, app routes, Web full-text item ids, CLI manifest entries, and PHP `pdf_fetches` ids do not satisfy `ADR 0013` full-text artifact evidence requirements.

Full-text Screening behavior itself remains governed by `ADR 0013`. This ADR does not change Screening decision shape, conflict detection, human authority, or adjudication behavior.

### 15. Provenance and Audit Relation

Full Text acquisition records, artifact evidence records, and extraction records are Core evidence records under this contract.

They are not automatically Gate 5 provenance events.

A later provenance workflow may create `provenance-event` records that reference Full Text records as subjects, inputs, or outputs. When that happens, the provenance event must use `ADR 0008` event digest rules.

PHP `pdf_fetches`, CLI manifests, Web full-text batches/items, Web audit rows, app job lifecycle rows, and download routes are source/integration evidence. They remain app or PHP projections unless transformed into the Core records defined by this ADR.

### 16. App Projection Boundary

CLI full-text manifest files are not `ADR 0009` bundle manifests.

Web full-text batches, Web full-text items, app audit rows, artifact download routes, queue/job rows, and UI status rollups are app projections. They may display or reference Core Full Text records later, but their row ids, statuses, paths, and routes are not Core artifact identity.

CLI title matching is not candidate identity. Web candidate gating is integration evidence; Core candidate identity still comes from accepted Screening/candidate-set records.

### 17. Intentional Incompatibilities From PHP

The local C# contract intentionally differs from PHP in these ways:

- C# requires raw artifact byte digest evidence where PHP records storage paths and metadata.
- C# rejects local path, app route, app row id, storage path, and CLI manifest path as artifact identity.
- C# first implementation is no-network, while PHP can use live OA sources and HTTP downloads.
- C# does not use CLI title matching as Full Text candidate identity.
- C# does not treat `pdf_fetches`, CLI manifests, Web full-text item rows, or Web audit rows as Core authority.
- C# treats XML/text sidecars as derived evidence unless source artifact digest binding is preserved.

These are intentional local contract decisions. PHP compatibility remains unclaimed until generated fixtures and comparators classify them.

## Alternatives Considered

### Treat PHP `filePath` as artifact identity

Rejected.

File paths are local storage references. They are not portable, not stable across machines, and not sufficient for `ADR 0013` full-text Screening evidence.

### Implement live providers in the first C# Full Text slice

Rejected.

Live providers introduce HTTP clients, credentials, rate limits, retries, source availability, access policy, and legal/provider governance before raw artifact evidence is defined.

### Treat extracted text as a replacement for raw PDFs

Rejected.

Extraction is lossy and tool-dependent. Extracted text is derived evidence and must bind back to the raw artifact digest.

### Adopt CLI/Web full-text rows as Core records

Rejected.

CLI/Web rows are integration evidence and product workflow projections. Core admits the acquisition/artifact/extraction evidence shape, not app storage or UI workflow authority.

### Reject all user-supplied files until live provider behavior exists

Rejected.

User-supplied files are a safe local-first acquisition source when raw bytes, local actor, timestamp, candidate binding, and source notes are recorded. Core records evidence provenance, not legal certification.

## Consequences

Positive consequences:

- C# Full Text implementation can start with a no-network local slice.
- Artifact evidence becomes raw-byte digest-bound before it can feed full-text Screening.
- Failed, skipped, inaccessible, and partial acquisition states remain auditable evidence.
- Extracted text cannot overwrite or replace raw artifact evidence.
- App and PHP path-based rows remain useful evidence without becoming Core authority.

Negative consequences:

- C# Full Text intentionally diverges from PHP path-based records and live retrieval behavior.
- The first C# Full Text implementation will not retrieve from live providers.
- User-supplied files require careful local actor/source metadata to avoid implying license certification.
- Actual PDF parsing, OCR, and live source adapters remain unavailable until later gates.

## Migration Effect

No persisted C# data is migrated by this ADR.

Any existing CLI/Web/PHP full-text rows must be treated as non-authoritative Core Full Text records until transformed into `nexus.fulltext.acquisition-record`, `nexus.fulltext.artifact-evidence`, or `nexus.fulltext.extraction-record` shapes.

Existing local paths or manifests cannot be upgraded to artifact evidence without access to the exact bytes or a verified `raw-artifact-bytes` digest.

## Fixture Effect

Gate 9 Full Text fixtures must cover:

- Screening/candidate-set handoff input;
- raw Search trace rejection;
- raw Dedup member rejection;
- title/abstract include and needs-review eligibility;
- final exclude not retrievable by default;
- user-supplied local bytes;
- deterministic stub artifact bytes;
- manual acquisition metadata;
- source references without live download;
- raw PDF artifact digest;
- raw XML artifact digest;
- raw text artifact digest;
- local path not identity;
- wrong digest scope;
- missing digest;
- PDF signature validation;
- XML/HTML rejection;
- empty text rejection;
- max-size rejection;
- successful acquisition with source attempts;
- failed/skipped/manual-needed acquisition;
- source failure followed by success;
- duplicate artifact digest;
- derived text extraction binding to source artifact digest;
- extraction failure and partial extraction warnings;
- full-text Screening evidence refs;
- app manifest/batch/item/audit row projection boundaries.

Generated PHP compatibility fixtures are still required before PHP compatibility can be claimed. Local fixtures are local C# contract fixtures unless explicitly generated from the pinned PHP source with metadata.

Comparators must preserve:

- input candidate binding;
- source alias/reference;
- acquisition kind;
- status;
- artifact kind;
- media type;
- byte size when pinned;
- raw byte digest;
- digest scope;
- source attempts;
- error category;
- extraction source artifact digest;
- authority/projection markers.

Comparators may ignore generated ids, runtime durations, local paths, routes, and timestamps only when fixture metadata marks them non-semantic. They must not ignore artifact digest, digest scope, candidate binding, source attempt outcome, extraction source binding, or app projection markers.

## Conflict Effect

`CF-025` is resolved for the local Full Text contract by this ADR. C# Full Text artifact evidence uses exact bytes plus `raw-artifact-bytes` digest. PHP paths and app paths remain projections.

`CF-026` is narrowed by this ADR. Local no-network Full Text implementation may proceed with user-supplied bytes, deterministic stub artifacts, manual acquisition records, and source-reference metadata. Live providers, HTTP downloads, provider SDKs, credentials, scraping, paywall bypass, and shadow-library sources remain blocked until a later provider/network/legal gate.

`CF-027` is narrowed for Core by this ADR. Full-text Screening handoff uses digest-bound artifact/extraction evidence. CLI manifests, Web batches/items, `pdf_fetches`, app audit rows, download routes, and app row ids remain integration evidence and projections unless transformed into ADR 0014 records by a later adapter.

`CF-024` is unchanged: Screening app workflow rows remain projections.

## Implementation Readiness

Local C# Full Text implementation is ready to start against this ADR for a no-network slice:

- user-supplied local bytes;
- deterministic stub artifacts;
- manual acquisition records;
- raw artifact byte digest validation;
- source attempt records;
- artifact evidence records;
- derived extraction records or stub/user-supplied extracted text records;
- Full Text evidence references for `ADR 0013` Screening.

Implementation is not ready for:

- live providers;
- HTTP downloads;
- provider SDKs;
- credentials;
- paywall bypass;
- shadow-library sources;
- Google Scholar scraping;
- actual PDF parsing implementation;
- OCR;
- persistence/API/UI/cloud behavior;
- PHP compatibility claims;
- generated PHP fixture comparison;
- app behavior as Core authority.

## Reversal Conditions

Revise this ADR only if:

1. generated PHP fixtures prove a compatibility requirement that the project explicitly chooses over raw-byte artifact identity;
2. a later provider/network/legal ADR admits live full-text source adapters and needs a versioned acquisition schema change;
3. a later artifact or bundle ADR changes raw artifact digest scope or logical path rules;
4. a later extraction/OCR ADR defines stronger extracted-text/page/section schemas;
5. app-alignment work promotes specific CLI/Web full-text fields into Core records with digest and migration rules;
6. legal review requires a narrower user-supplied file evidence policy.

## Explicit Claims Not Made

- no C# Full Text implementation
- no source code changes
- no fixture generation
- no PHP compatibility
- no generated PHP fixtures
- no live provider/network behavior
- no HTTP clients
- no Unpaywall, PMC, Europe PMC, arXiv, OpenAlex, Semantic Scholar, publisher, or Direct PDF integration
- no provider SDKs or credentials
- no paywall bypass
- no shadow-library source
- no Google Scholar scraping
- no PDF extraction implementation
- no OCR implementation
- no persistence/API/UI/cloud behavior
- no CLI/Web behavior changes
- no app behavior made authoritative
- no Screening behavior change
- no Deduplication or Search behavior change
- no artifact storage implementation
- no bundle behavior change
- no AI governance behavior
- no blueprint conformance
