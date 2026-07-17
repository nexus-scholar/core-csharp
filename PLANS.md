# Plans

This file is the concise gate map. The detailed active roadmap is
[`docs/plans/2026-07-14-feature-expansion-priority.md`](docs/plans/2026-07-14-feature-expansion-priority.md).

## Current Position

Protected `main` is documented at `805f3d6`.

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
| FE-08 Slices 1-2 local desktop foundation | Complete |
| FE-08 Slice 3 desktop Deduplication review | Complete |
| FE-08 Slice 4 durable Screening authority resolution | Complete |
| FE-08 Slice 5 first desktop Screening mutation | Next gate candidate; not yet authorized |
| FE-09 through FE-12 | Sequenced future work; not authorized |

The verification baseline at FE-08 Slice 4 closeout is 906 tests and 23
validation-only packages.

## Immediate Next Gate

FE-08 Slice 5 may propose the first desktop title/abstract Screening mutation.
Before implementation it requires an accepted ADR and gate covering:

- exact approved Protocol and title/abstract criteria authority;
- exact candidate, corpus snapshot, and workflow-task bindings;
- explicit human actor and admitted role;
- preview and confirmation material;
- stale generation, stale authority, and concurrent-writer rejection;
- conflict, correction, supersession, and invalidation behavior;
- atomic local persistence, recovery, provenance, and refresh;
- desktop command-facade direction without UI-owned authority.

No generic action descriptor, button, selection, or view model may create a
Screening decision.

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

The desktop currently covers workspace open/init/import/verify/analyze,
authority-checked Deduplication review, and read-only Screening readiness.

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
| FE-09 | Live providers and citation networks | Legal/network ADRs, reproducible acquisition evidence, immutable graph snapshots |
| FE-10 | Plugin runtime | Capability grants, staged outputs, out-of-process host, threat model, and no direct authority |
| FE-11 | Governed AI | Context/evidence provenance, privacy, validation, human acceptance, and retention policy |
| FE-12 | Database, API, cloud, and multi-user operation | Semantic equivalence, authorization, concurrency, migration, backup/restore, and tenant isolation |

Later design may proceed, but implementation must not bypass dependencies or its
own accepted gate.

## Claim Boundaries

The current plan does not authorize claims of:

- production readiness or completed security/accessibility certification;
- broad PHP compatibility;
- live provider access, scraping, or built-in PDF/OCR;
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
