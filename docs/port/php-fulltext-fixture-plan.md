# PHP Full Text Fixture And Comparator Plan

Status: H28 generated subset implemented; broader retrieval catalog remains planned.

Pinned PHP source:

- Repository: `../core`
- Commit: `b24d0d71ec7b64003465182477e7edb7f49994f4`
- Source lock: `specs/SOURCE.lock.json`

## Scope

This plan defines fixture and comparator families for Full Text retrieval, artifact evidence, extraction evidence, and Screening handoff. H28 implements the shared no-network domain subset in `fixtures/php-golden/screening-fulltext/v1/`; CLI manifests, Web rows, and runtime retrieval behavior remain projections.

`ADR 0014` resolves the local contract for artifact evidence shape, raw-byte digest binding, source attempts, extraction records, no-network first implementation scope, provider/network boundaries, and app projection boundaries. Local C# implementation is ready only for the no-network slice described by `ADR 0014`.

## Fixture Families

### Retrieval Input And Candidate Boundary

Planned fixtures:

- `fulltext-input-screening-handoff-candidates.json`
- `fulltext-input-locked-candidate-set.json`
- `fulltext-input-no-primary-id-skipped.json`
- `fulltext-input-raw-search-trace-rejected.json`
- `fulltext-input-title-match-cli-projection.json`

Coverage:

- Full Text retrieval candidates come from Screening/Dedup handoff or another accepted candidate set.
- Final title/abstract `include` and `needs_review` records may be retrieval candidates.
- Final `exclude` records are not retrieval candidates by default.
- Raw Search traces are not Full Text retrieval input without Screening/Dedup/candidate-set binding.
- CLI title matching is classified as app projection, not C# Core candidate identity.

### Source Candidate Resolution

Planned fixtures:

- `fulltext-source-direct-pdf-url.json`
- `fulltext-source-unpaywall-oa-pdf.json`
- `fulltext-source-unpaywall-closed-skipped.json`
- `fulltext-source-pmc-oai-xml.json`
- `fulltext-source-europe-pmc-pdf.json`
- `fulltext-source-europe-pmc-xml-fallback.json`
- `fulltext-source-arxiv-url.json`
- `fulltext-source-openalex-pdf-url.json`
- `fulltext-source-semantic-scholar-pdf-url.json`
- `fulltext-source-shadow-library-rejected.json`

Coverage:

- Source aliases and source-specific metadata are preserved.
- Closed or non-OA sources do not produce artifacts.
- Shadow-library or bypass sources are rejected.
- Live provider calls are not required for local C# fixtures; source outputs must be deterministic recorded or stub evidence.

### Acquisition Records And Source Attempts

Planned fixtures:

- `fulltext-acquisition-user-supplied-file.json`
- `fulltext-acquisition-manual-evidence.json`
- `fulltext-acquisition-deterministic-stub-artifact.json`
- `fulltext-acquisition-source-reference-no-download.json`
- `fulltext-acquisition-source-attempt-order.json`
- `fulltext-acquisition-missing-actor-negative.json`

Coverage:

- Acquisition records use `nexus.fulltext.acquisition-record` version `1.0.0`.
- User-supplied and manual acquisition require a local actor.
- Source references do not authorize live downloads.
- Source attempts preserve success, failure, skipped, and manual-needed states.
- Failed attempts remain evidence when a later source succeeds.

### Artifact Evidence And Digest

Planned fixtures:

- `fulltext-artifact-pdf-raw-byte-digest.json`
- `fulltext-artifact-xml-raw-byte-digest.json`
- `fulltext-artifact-text-raw-byte-digest.json`
- `fulltext-artifact-text-sidecar-derived.json`
- `fulltext-artifact-path-not-identity.json`
- `fulltext-artifact-invalid-pdf-signature.json`
- `fulltext-artifact-invalid-content-type.json`
- `fulltext-artifact-oversized-rejected.json`
- `fulltext-artifact-missing-raw-digest-negative.json`

Coverage:

- Artifact bytes use `raw-artifact-bytes` digest scope.
- Local file paths and storage paths are not artifact identity.
- PDF validation covers signature and media type.
- XML and text validation preserve source artifact and derived sidecar boundaries.
- Oversized artifacts are rejected before storage.

### Extraction Evidence

Planned fixtures:

- `fulltext-extraction-source-artifact-binding.json`
- `fulltext-extraction-derived-text-digest.json`
- `fulltext-extraction-page-text-projection.json`
- `fulltext-extraction-section-projection.json`
- `fulltext-extraction-partial-warning.json`
- `fulltext-extraction-failure.json`
- `fulltext-extraction-replaces-raw-artifact-negative.json`
- `fulltext-extraction-missing-source-digest-negative.json`

Coverage:

- Extraction records use `nexus.fulltext.extraction-record` version `1.0.0`.
- Extracted text and section/page projections bind back to the source artifact id and `raw-artifact-bytes` digest.
- Extraction output is derived evidence and cannot replace raw artifact evidence.
- Partial extraction and extraction failure remain auditable states.
- OCR and PDF parsing are not implied by contract fixtures.

### Retrieval Result And Audit

Planned fixtures:

- `fulltext-retrieval-success-audit.json`
- `fulltext-retrieval-failure-audit.json`
- `fulltext-retrieval-skipped-audit.json`
- `fulltext-retrieval-source-failure-then-success.json`
- `fulltext-retrieval-all-sources-failed.json`
- `fulltext-retrieval-cache-hit-projection.json`
- `fulltext-retrieval-cooldown-skips-recent-failure.json`
- `fulltext-retrieval-streamed-pdf.json`

Coverage:

- Success, failure, and skipped statuses are preserved.
- Source attempts and failures are audit evidence.
- Continuing after a failed source is preserved.
- Cache reuse by stored path is a projection unless mapped into digest-bound evidence.
- Durations and attempted timestamps require fixed clocks or comparator allowances.

### Screening Handoff

Planned fixtures:

- `fulltext-screening-artifact-evidence-ref.json`
- `fulltext-screening-successful-artifact-only.json`
- `fulltext-screening-failed-artifact-follow-up.json`
- `fulltext-screening-manual-needed-follow-up.json`
- `fulltext-screening-exclude-requires-basis.json`

Coverage:

- Full-text Screening decisions reference digest-bound artifact evidence.
- Failed, skipped, and manual-needed retrieval items remain follow-up states.
- Successful artifacts are screenable only when the artifact evidence is digest-bound.
- Full-text exclusion requires a rationale and exclusion basis under Screening policy.

### App Projection Boundary

Planned fixtures:

- `fulltext-cli-manifest-projection.json`
- `fulltext-web-batch-projection.json`
- `fulltext-web-item-projection.json`
- `fulltext-web-audit-row-not-provenance-event.json`
- `fulltext-download-route-not-identity.json`

Coverage:

- CLI manifest entries are app-local outputs, not `ADR 0009` bundle manifests.
- Web batch/item rows are app workflow evidence.
- Web audit rows are not Gate 5 provenance events.
- Download routes and storage paths are projections, not artifact identity.

## Negative Fixture Catalog

Required negative cases:

- no-primary-id work treated as retrievable scientific artifact target
- raw Search trace passed directly to Full Text retrieval
- CLI title match treated as C# Core candidate identity
- closed or non-OA Unpaywall result accepted as legal artifact source
- shadow-library or paywall bypass source accepted
- Google Scholar or publisher scraping accepted without provider gate
- local file path used as artifact identity
- storage path used as artifact identity
- missing raw artifact digest
- wrong digest scope for artifact bytes
- extracted text accepted without source artifact digest
- extraction output treated as replacement for raw artifact
- OCR behavior claimed by fixture metadata
- invalid PDF signature
- invalid PDF content type
- invalid XML
- HTML page accepted as XML full text
- oversized artifact stored
- XML text sidecar treated as canonical without source artifact digest
- failed retrieval item sent to full-text Screening as screenable
- skipped retrieval item sent to full-text Screening as screenable
- app batch/item/audit row treated as Core authority
- PHP compatibility claimed without generated fixtures

## Comparator Plan

### General

- Compare semantic fields, not PHP object identity.
- Ignore generated ids only when fixture metadata marks them generated.
- Ignore runtime durations and attempted timestamps only when fixture metadata marks them non-semantic.
- Do not ignore artifact digest, digest scope, artifact type, source alias, status, or source attempt outcome.
- Do not compare local file paths as scientific identity.

### Source Comparator

Compare:

- source alias;
- source URL when fixture marks it semantic;
- source metadata such as license, OA status, host type, PMCID, and source-specific ids;
- source order and first-success behavior when applicable.

Do not compare credentials, email addresses except presence/absence policy, local config paths, or live HTTP timing.

### Artifact Comparator

Compare:

- artifact type;
- raw-byte digest;
- digest scope;
- byte size when fixture pins it;
- validation category;
- derived sidecar relationship if present.
- extraction source artifact id and source raw digest when derived evidence is present.

Local file paths may be compared only as projection fields in app-projection fixtures, not as artifact identity.

### Retrieval Audit Comparator

Compare:

- status;
- source alias;
- source URL shape where semantic;
- HTTP status;
- error category/message class when fixture pins it;
- metadata fields;
- successful/failure/skipped count.

Runtime durations and timestamps need fixed values or explicit ignore rules.

### Screening Handoff Comparator

Compare:

- candidate id;
- Screening stage;
- full-text artifact evidence refs;
- digest scope;
- source full-text evidence id when accepted;
- follow-up status for failed/skipped/manual-needed records.

Do not accept app full-text item ids as Core authority unless a later ADR admits them.

### Extraction Comparator

Compare:

- source artifact id;
- source raw-byte digest;
- source raw-byte digest scope;
- extractor id and version when pinned;
- extraction status;
- extracted text digest when present;
- extraction warning/error category;
- page/section projection markers when present.

Do not compare extracted text as a replacement for raw artifact evidence.

## Generator Requirements

The PHP fixture generator must:

1. run against commit `b24d0d71ec7b64003465182477e7edb7f49994f4`;
2. record exact command and environment;
3. avoid live providers and network calls unless a dedicated cassette-backed fixture plan permits them;
4. use deterministic fake downloaders and fake source clients where possible;
5. preserve exact input bytes for artifact digest fixtures;
6. record source commit, input digest, output digest, and comparator rules;
7. inject fixed ids, clocks, and durations where equality matters;
8. classify every difference as equivalent serialization, intentional incompatibility, PHP defect, C# defect, or unresolved conflict.

## Implementation Readiness

Implementation readiness: **yes, for local no-network C# Full Text implementation against `ADR 0014`**.

Ready local scope:

- user-supplied local bytes;
- deterministic stub artifacts;
- manual acquisition records;
- source-reference metadata with no download;
- raw artifact byte digest validation;
- source attempt records;
- artifact evidence records;
- extraction records or stub/user-supplied extracted text records that bind to source artifact digest;
- full-text Screening evidence references.

Implementation readiness remains **no** for:

- live providers;
- HTTP downloads;
- provider SDKs or credentials;
- actual PDF parsing implementation;
- OCR;
- persistence/API/UI/cloud behavior;
- broad PHP compatibility beyond the H28 generated subset;
- live retrieval or app/runtime projection compatibility.

## Explicit Non-Claims

- no broad PHP Full Text compatibility beyond H28
- no path/runtime projection compatibility
- no live provider/network behavior
- no provider SDK, credentials, or API integrations
- no paywall bypass or shadow-library support
- no PDF extraction implementation
- no OCR implementation
- no persistence schema
- no API/UI/cloud behavior
- no CLI/Web behavior changes
- no app behavior made authoritative
- no full-text Screening behavior change
- no artifact storage implementation
- no bundle behavior change
