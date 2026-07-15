# Plans

This file keeps the historical gate map and points to the current operating plan.

## Completed

The public local CLI workflow is implemented and documented:

- local workspace initialization and status;
- local Search export import;
- workspace verification;
- deterministic analysis over imported Search evidence;
- APP-01 workspace plan composition;
- read-only review queue display;
- read-only dedup cluster inspection;
- public tutorial on `gh-pages`.

The implemented command loop is:

```bash
nexus init
nexus status
nexus import search
nexus verify
nexus analyze
nexus review
nexus clusters
nexus clusters exact
nexus clusters review
nexus clusters show <id>
```

A Nexus research project is a local folder. `nexus.project.json` is a local project index, not a database and not canonical scientific authority.

The CLI verifies local files, analyzes imported Search/Deduplication evidence, and shows records requiring human review. It does not query live providers or execute merge decisions.

## Current Plan

Hardening Phases 1-7 and the Hardening 30 corrective closeout are complete on protected `main`. The active successor roadmap is:

- `docs/plans/2026-07-14-feature-expansion-priority.md`

The roadmap establishes the dependency-ordered feature sequence FE-01 through FE-12. FE-01 is complete under ADR 0028, and FE-02 is complete under ADR 0029 with all three local Deduplication actions and atomic successor authority generations. FE-03 is next but remains design-only until its own ADR and implementation gate are accepted; later features remain unauthorized.

Completed hardening references remain:

- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`

The completed dependency order was:

1. canonical foundation;
2. authority-safe rehydration;
3. scholarly pipeline correctness;
4. transactional workspace;
5. test strategy upgrade;
6. release engineering;
7. PHP compatibility evidence only after local correctness.

Hardening 30 corrected the post-phase review findings in AI proposal authority, Full Text rehydration, Search import parsing, compatibility-evidence guards, package version identity, and operating documentation. FE-01 and FE-02 are complete. FE-03 workflow execution journal is the next planning target and has no implementation authority yet.

## Deferred Until Their Feature Gate

- workflow execution state and human task journal: FE-03;
- durable title and abstract Screening conduct: FE-04;
- local Full Text intake, extraction, and full-text Screening: FE-05;
- reproducible reporting, portable audit bundle, and Rapid Review profile: FE-06;
- extraction, appraisal, and synthesis records: FE-07;
- UI product shell and command routing: FE-08;
- live providers, legal retrieval, and citation networks: FE-09;
- plugin runtime: FE-10;
- AI/model calls and proposal acceptance under a dedicated governance ADR: FE-11;
- database, API, cloud, synchronization, and multi-user operation: FE-12.

APP-01 merge-gate actions remain non-authority display hints. They must not mutate Core records, write files, or call services. Only the accepted FE-02 `nexus dedup decide --confirm` boundary may execute the three admitted Deduplication actions; no generic UI action becomes authority.

Do not implement a listed feature until its minimum dependencies are complete and its own gate is accepted. This roadmap is sequencing authority, not blanket implementation authority.

## Current Detailed References

- `docs/plans/2026-07-14-feature-expansion-priority.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`
- `docs/ops/BRANCH-BOARD.md`
- `docs/ops/MERGE-QUEUE.md`
- `docs/reviews/2026-06-29-main-public-readiness/README.md` - historical public-readiness context only

## Historical Implementation Gates

The gates below remain useful historical structure and source-routing context. They are not a command to restart Gate 0.

### Gate 0: evidence freeze

Map the blueprint and PHP reference, capture the PHP commit, define product laws, list open conflicts, and plan golden fixtures.

### Gate 1: repository quality

Keep restore, release build, tests, formatting, and architecture checks green on Windows and Linux.

### Gate 2: deterministic kernel

Implement typed identifiers, clocks, ID generation, canonical serialization, digests, errors, and actor identity.

### Gate 3: protocol lifecycle

Implement drafts, structured decisions, approval, immutable versions, amendments, waivers, and deviations.

### Gate 4: workflow compiler

Implement templates, parameters, nodes, edges, gates, validation, capability requirements, and invalidation planning.

### Gate 5: artifact and provenance ledger

Implement immutable artifacts, append-only events, agents, activities, inputs, outputs, and decision lineage.

### Gate 6: portable bundle

Export, verify, import, tamper-check, and round-trip the canonical review bundle.

### Gate 7: local application

Future gate. Add local application behavior only after application-service boundaries are explicit. Do not jump directly to persistence from the current public-feedback lane.

### Gate 8: first method pack

Future gate. Implement a Rapid Review pack with explicit shortcuts, consequences, mitigations, approvals, and reporting evidence.

### Gate 9: PHP behavior port

Port scholarly identity, normalization, deduplication, snapshots, screening, search, retrieval, graphs, and exports through differential fixtures.

Current Gate 9 local state includes Search, Search Import, Deduplication, Screening, and local no-network Full Text. Phase 7 generated pinned fixtures and semantic comparators for explicitly inventoried cases only; broad PHP compatibility remains unclaimed.

### Gate 10: plugins

Add capability-scoped official plugins, then an out-of-process host for third-party extensions.

### Gate 11: governed AI

Start with protocol clarification proposals. Add later AI tasks only after context, evidence, authority, validation, retention, and human-action policies are explicit.
