# ADR 0032: Local Full Text Workflow Authority

- Status: Accepted
- Date: 2026-07-15
- Decision owner: Nexus Scholar maintainer/manager

## Context

ADR 0014 defines local Full Text input, acquisition, raw artifact, validation,
and extraction semantics. The current `NexusScholar.FullText` package implements
those in-memory rules and rehydrates exact input/acquisition/artifact chains, but
it does not consume verified FE-04 handoff authority, define canonical persisted
bytes, persist immutable workspace generations, or conduct durable full-text
Screening. The historical string-based `FullTextInput.FromScreeningDecision`
factory is useful local behavior but cannot prove that a decision or handoff was
accepted and current.

FE-05 must connect those existing contracts without moving paths, app rows,
parser output, or workflow state into scientific identity. It must remain local
and no-network. Deterministic PDF parsing is not admitted because no parser
library and compatibility contract has been accepted.

## Decision

### Package boundaries

`NexusScholar.FullText` remains the owner of input material, acquisition,
raw-byte artifact evidence, extraction attempts, canonical codecs, and verified
replay. It continues to depend inward only on Kernel and Artifacts.

A new packable `NexusScholar.Screening.FullText` bridge depends inward on
Screening and FullText. It owns verified FE-04 handoff admission and canonical
full-text human conduct. Screening and FullText do not depend on this bridge or
on infrastructure.

ResearchWorkspace owns local file reading, immutable byte retention, generation
manifests, locking, stale-writer rejection, and pointer-last publication.
AppServices exposes UI-neutral preview and commit ports. CLI may report verified
manifest and artifact integrity, but mutation requires already resolved verified
Protocol, Screening handoff, and Full Text authorities.

### Verified handoff admission

An FE-05 admission is created only from a canonical FE-04 handoff rehydrated
against its current conduct journal. The candidate must have an `include`
outcome. Admission binds:

- conduct id, policy digest, handoff id and digest;
- candidate-set id and candidate id;
- verdict and every supporting Screening decision digest;
- the derived Full Text input id and digest.

The historical string-only Full Text input factory remains non-authoritative and
cannot enter an FE-05 workspace generation without this verified admission.

### Canonical Full Text authority

The FE-05 persisted schemas are canonical JSON records:

```text
nexus.fulltext.admission / 1.0.0
nexus.fulltext.acquisition-record / 1.1.0
nexus.fulltext.artifact-evidence / 1.1.0
nexus.fulltext.extraction-attempt / 1.0.0
nexus.fulltext.screening-policy / 1.0.0
nexus.fulltext.screening-decision / 1.0.0
nexus.fulltext.invalidation / 1.0.0
nexus.fulltext.handoff / 1.0.0
```

Persisted bytes are unverified input. Strict rehydration requires exact canonical
bytes, reproduces every digest, resolves the verified FE-04 handoff and Protocol,
revalidates raw artifact bytes, and replays append-only decisions and
invalidations. Unknown fields, omission/null drift, chain gaps, reorder, removal,
insertion, or cross-candidate splicing fail closed.

### Local acquisition and artifact identity

The adapter accepts user-supplied PDF, XML, or text bytes only. A local path is
an input reference recorded outside artifact identity. Exact bytes are retained
under an immutable generation and identified by a `raw-artifact-bytes` digest.
The same bytes through different paths retain one byte identity but distinct
acquisition provenance; changed bytes at the same path produce a new artifact
identity.

Acquisition records identify the human/import actor, timestamp, acquisition
kind, source type/reference, access and availability notes, and legal-status
non-claim. Core records preserve supplied access metadata but make no legality or
redistribution certification.

Network URLs may be retained only as external source references. The local
adapter rejects URL input, live retrieval, scraping, authentication, paywall
bypass, provider SDKs, and shadow-library sources.

### Extraction attempts

Every extraction attempt binds the source artifact id and raw digest, extractor
id and version, canonical configuration digest, timestamp, status, output
representation and digest when present, warnings, and failure category/summary.
`success`, `partial`, `failure`, and `unsupported` remain distinct. Partial
output requires warnings. Failure or unsupported status cannot carry successful
content and never implies a Screening exclusion. Raw bytes remain authoritative
evidence regardless of extraction status.

The first concrete extractors are deterministic UTF-8 text and safe XML text
projection. PDF intake is retained and validated but PDF parsing is unsupported
under this ADR. OCR is excluded.

### Full-text Screening conduct

Full-text conduct binds the approved Protocol content digest, full-text criteria
digest, verified admission, exact raw artifact digest, and optional extraction
digest used by the reviewer. Final include/exclude/needs-review decisions require
an authorized human actor and rationale. Exclusion requires a policy-defined
full-text reason code. Automation and extraction failures cannot finalize a
decision.

Artifact, extraction, criteria, Protocol, or source-evidence changes append an
invalidation naming the exact affected current decision digests. Handoff is
blocked while decisions are missing, invalidated, conflicted, or needs-review.
History is append-only; corrections and adjudication follow ADR 0031 digest-link
semantics.

### Workspace transaction

ResearchWorkspace stages admission, acquisition, exact raw bytes, artifact
evidence, extraction attempts, conduct records, and manifest. It verifies all
digests, acquires one workspace lock, checks the expected revision and prior
Full Text pointer, promotes the complete immutable generation, then updates the
project pointer last. Failure preserves the prior pointer and quarantines any
unpointed promoted generation.

## Alternatives Rejected

- Let FullText depend directly on Screening: couples artifact identity to one
  workflow stage and violates the existing inward boundary.
- Trust decision ids or handoff ids supplied by CLI: ids and digest text do not
  prove current authority.
- Store only extracted text: loses raw evidence and makes parser behavior the
  scientific source.
- Parse PDFs with ad hoc text scanning: non-reproducible and unsupported by a
  parser contract.
- Treat extraction failure as exclusion: automation is not scientific authority.
- Persist mutable current rows: loses acquisition, correction, and invalidation
  history.

## Consequences

FE-05 becomes restart-safe and independently verifiable while retaining the
existing local validation behavior. The cost is a bridge package, canonical
record codecs, immutable byte storage, explicit parser attempts, and a strict
human conduct journal.

## Migration Effect

Existing ADR 0014 objects and fixtures remain valid historical local contracts.
They become FE-05 authority only after canonical rehydration and verified FE-04
admission. No existing output is silently upgraded.

## Fixture Effect

Add executable fixtures for text/XML extraction, PDF unsupported extraction,
same-path changed bytes, same-bytes different paths, partial/failure attempts,
wrong-candidate admission, stale handoff, artifact/extraction invalidation,
human decisions, and workspace tamper/failure recovery.

## Compatibility And Claims

This is a local C# contract. It makes no PHP, parser-equivalence, OCR, live
retrieval, paywall, legal-certification, database, API, cloud, production,
scale, or institutional compatibility claim.

## Reversal Conditions

Revise this ADR if verified handoff admission cannot remain in an outward bridge,
if deterministic PDF parsing is accepted with a versioned parser dependency, or
if exact raw bytes cannot be retained under the local workspace policy.
