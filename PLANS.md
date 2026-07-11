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

Feature expansion is frozen. The current operating plan is integrity hardening from the 2026-07-11 full technical review:

- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`

Hardening starts with Phase 0:

1. open one issue per confirmed blocker;
2. correct public maturity claims;
3. protect `main`;
4. assign each blocker an owner, test case, and dependency order.

The dependency order is:

1. canonical foundation;
2. authority-safe rehydration;
3. scholarly pipeline correctness;
4. transactional workspace;
5. test strategy upgrade;
6. release engineering;
7. PHP compatibility evidence only after local correctness.

## Explicitly Deferred

- executable merge decisions;
- actor identity and decision persistence;
- provenance mutation for user decisions;
- app database/persistence;
- API/cloud;
- live providers/scraping;
- UI product shell;
- PDF/OCR;
- AI/model calls;
- AppServices expansion beyond current APP-01 read-only projection.

APP-01 merge-gate actions are placeholders only. They must not mutate Core records, execute commands, write files, call services, or imply that the CLI/UI can finalize a scientific decision.

Do not implement providers, persistence, API/cloud behavior, PDF/OCR, live HTTP, or a UI product shell under the current plan.

## Current Detailed References

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

Current Gate 9 local state includes Search, Search Import, Deduplication, Screening, and local no-network Full Text. PHP compatibility remains unclaimed without generated fixtures and comparators.

### Gate 10: plugins

Add capability-scoped official plugins, then an out-of-process host for third-party extensions.

### Gate 11: governed AI

Start with protocol clarification proposals. Add later AI tasks only after context, evidence, authority, validation, retention, and human-action policies are explicit.
