# Discussion Agenda

Status: suggested agenda for the next conversation after the first sweep.

## Decision 1: Close Or Polish Phase 3.5

Recommended question:

```text
Is the sample host good enough as a visual harness, or do we need one small visual polish pass before moving on?
```

Evidence:

- Build/test/format passed.
- No blocking architecture issue found.
- Manual visual inspection was not performed in this sweep.

If the answer is "polish," keep it narrow:

- layout density;
- readable severity/source markers;
- clearer action presentation;
- payload formatting;
- no Core calls;
- no app services;
- no persistence.

## Decision 2: Fix Minor UI Documentation Drift

Recommended question:

```text
Should we do a tiny docs cleanup so docs/ui/README.md reflects that UiContracts, Avalonia renderer, and sample host now exist?
```

This is low-risk and should not change behavior.

## Decision 3: Persistence Docs Slice

Recommended question:

```text
Should the next docs branch convert the persistence planning base into docs/persistence plus ADR 0015 stub, with no code implementation?
```

Recommended scope:

- add the five persistence planning docs;
- add proposed ADR 0015 stub;
- explicitly mark all documents planning/stub, not accepted implementation authority;
- preserve the no-cloud, Core-persistence-free, paths-not-identity, backup-vs-bundle boundaries.

## Decision 4: AppServices Timing

Recommended question:

```text
Should AppServices come before persistence implementation?
```

My sweep answer: yes. The persistence base itself says UI/Web/CLI should not directly mutate Core records or own database semantics. A thin application-service command boundary should be designed before persistence code becomes callable by UI actions.

## Recommended Next Prompt

```text
Do a docs-only persistence planning slice for Nexus Scholar Core.

Use C:/Users/mouadh/Downloads/nexus_persistence_planning_base.md as planning input.

Do not implement persistence yet.
Do not add EF Core, SQLite, database code, UI code, web API code, cloud code, or app services.

Create docs/persistence/ with:
- PERSISTENCE-MENTAL-MODEL-v0.md
- WORKSPACE-LAYOUT-v0.md
- STATE-CLASSIFICATION-v0.md
- APP-SERVICE-BOUNDARY-v0.md
- METHOD-PROFILES-AND-PERSISTENCE-v0.md

Create docs/adr/0015-local-workspace-persistence-boundary.md as a proposed/stub ADR only.

Preserve these boundaries:
- Core remains persistence-free.
- Application services are the future bridge.
- File paths are references, not scientific identities.
- Artifacts are identified by raw bytes and digest.
- Backups and bundles are different.
- Cloud is not scientific authority.
- First implementation is local-first and no-cloud.

Run build/test/format only if docs tooling or repo policy requires it; otherwise report that this was docs-only.
```
