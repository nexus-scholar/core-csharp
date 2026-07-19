# Plans

This file is the concise gate map. The detailed active roadmap is
[`docs/plans/2026-07-14-feature-expansion-priority.md`](docs/plans/2026-07-14-feature-expansion-priority.md).

## Current Position

The last pre-release protected-main baseline is `425e9bc`. The alpha.2 release
commit is resolved by `v0.1.0-alpha.2` and its distribution manifest.

| Work | Status |
| --- | --- |
| Hardening Phases 1-7 and Hardening 30 | Complete |
| FE-01 Decision and snapshot authority | Complete |
| FE-02 Executable Deduplication review | Complete |
| FE-03 Workflow execution journal | Complete |
| FE-04 Title/abstract Screening | Complete |
| FE-05 Local Full Text workflow | Complete |
| FE-06 Reporting, audit bundle, and Rapid Review profile | Complete |
| FE-07 Extraction, Appraisal, and Synthesis | Complete |
| FE-08 Slices 1-9 local desktop workflow | Complete |
| FE-09 providers, cache, recorded Full Text retrieval, and citation network | Complete within accepted scope |
| Release Readiness Alpha 2 (RR-01 through RR-06) | Implemented release gate; completion requires matching protected-main tag and verified assets |
| FE-10 plugin runtime | Immediate design and capability-security gate |
| FE-11 through FE-12 | Sequenced future work; not authorized |

The historical FE-09 closeout baseline at `ea665eb` is 1,011 passing tests, two
opt-in live-provider smokes skipped by default, and 24 validation-only packages.
The successor integrity work under ADR 0044 and ADR 0045 is historical and
preserved as evidence context.

## Immediate Next Gate

FE-10 may design a plugin runtime only after an accepted ADR and gate freeze:

- manifest and plugin identity;
- invocation-scoped capability grants;
- explicit input/output schemas and data classification;
- process, filesystem, network, credential, and resource boundaries;
- immutable invocation evidence and staged, non-canonical output;
- host validation, provenance, invalidation, and recovery;
- a separate authorized human or domain action before staged output becomes
  scientific authority.

Process isolation alone must not be described as a security sandbox.

## Alpha 2 Release Gate

ADR 0046 and `docs/gates/RELEASE-READINESS-ALPHA2.md` define one indivisible
release boundary:

- RR-01: accepted release contract and nonclaims;
- RR-02: current documentation, changelog, and version-specific notes;
- RR-03: reproducible self-contained Windows x64 portable ZIP;
- RR-04: local crash diagnostics and verified new-directory backup/restore;
- RR-05: native Avalonia acceptance, automation, focus, and scaling coverage;
- RR-06: split Ubuntu/Windows validation, attestation, exact-tag publication,
  and downloaded-asset verification.

This gate publishes only the desktop technical preview. NuGet publication,
installer/updater behavior, signing, and production claims remain prohibited.

## Completed Product Flow

The repository now contains local contracts and durable authority records for:

```text
local Search import
  -> deterministic Deduplication
  -> human Deduplication decisions
  -> immutable corpus snapshot
  -> workflow execution
  -> title/abstract Screening
  -> local Full Text and full-text Screening
  -> reporting and audit bundle
  -> Extraction, Appraisal, and Synthesis
```

The desktop covers workspace open/init/import/verify/analyze, authority-checked
Deduplication and Screening conduct, local Full Text review, reporting,
Bundle v2, export-ledger publication, and verification.

The CLI includes:

```text
doctor, sample, demo
init, status, import search, verify, analyze, review
clusters, clusters exact, clusters review, clusters show
dedup decide
screening status
report verify, bundle verify, export verify, export status
```

The Research Workspace uses durable local files and immutable generations. It
does not use a database, server API, or cloud synchronization.

## Future Sequence

| Gate | Outcome | Boundary before implementation |
| --- | --- | --- |
| FE-10 | Plugin runtime | Capability grants, staged outputs, out-of-process host, threat model, and no direct authority |
| FE-11 | Governed AI | Context/evidence provenance, privacy, validation, human acceptance, and retention policy |
| FE-12 | Database, API, cloud, and multi-user operation | Semantic equivalence, authorization, concurrency, migration, backup/restore, and tenant isolation |

Later design may proceed, but implementation must not bypass dependencies or its
own accepted gate.

## Claim Boundaries

The current plan does not authorize claims of:

- production readiness or completed security/accessibility certification;
- broad PHP compatibility;
- live Crossref or Full Text retrieval, scraping, unrestricted provider
  retention, live citation acquisition, or built-in PDF/OCR;
- database, API, cloud, sync, authentication, or multi-user behavior;
- plugin execution or safe arbitrary-code sandboxing;
- live model execution or AI scientific authority;
- published or signed packages.

## Historical References

Completed hardening:

- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`

Current coordination:

- `docs/ops/BRANCH-BOARD.md`
- `docs/ops/MERGE-QUEUE.md`
- `docs/ops/CHAT-ROSTER.md`

Historical public-readiness review:

- `docs/reviews/2026-06-29-main-public-readiness/README.md`

The original Gate 0-11 sequence remains historical architecture context. The
FE-01 through FE-12 roadmap is the active delivery sequence.
