# Persistence Planning Sweep

Status: sweep of `nexus_persistence_planning_base.md` against current repo state.

## Verdict

The persistence planning base is directionally sound and consistent with existing Nexus product laws.

It should be treated as planning input only until it is split into repo docs and an ADR stub. It should not be used to justify adding EF Core, SQLite, database code, artifact storage, app services, backup/sync, or cloud behavior in the current UI sample-host branch.

## Strong Decisions To Preserve

- Persistence is not one concept.
- Canonical scientific records, artifact bytes, app/UI state, bundles, backups, and cloud sync are separate responsibilities.
- Core remains persistence-free.
- Application services are the future bridge between UI/Web/CLI and Core/persistence.
- File paths are references, not scientific identities.
- Artifact identity is raw bytes plus digest.
- Exports are inspection surfaces, not editable authority.
- Backups and bundles are different.
- Cloud is transport/coordination, not scientific authority.
- Storage/audit foundations can be method-neutral while schemas, workflows, validation, projections, and exports are method-aware.
- First implementation should be local-first and no-cloud.

## Repo State

Current repo state confirms this is not implemented yet:

- `docs/persistence/` does not exist.
- `docs/adr/0015-local-workspace-persistence-boundary.md` does not exist.
- No persistence projects were found in solution membership.
- No EF Core or SQLite package reference was found in the current branch sweep.
- Architecture tests already guard against EF Core, ASP.NET Core, provider SDKs, live-call primitives, and unauthorized framework references.

## Recommended Documentation Slice

The next safe persistence step is docs-only:

```text
docs/persistence/PERSISTENCE-MENTAL-MODEL-v0.md
docs/persistence/WORKSPACE-LAYOUT-v0.md
docs/persistence/STATE-CLASSIFICATION-v0.md
docs/persistence/APP-SERVICE-BOUNDARY-v0.md
docs/persistence/METHOD-PROFILES-AND-PERSISTENCE-v0.md
docs/adr/0015-local-workspace-persistence-boundary.md
```

ADR 0015 should remain a proposed/stub ADR until conflicts are resolved.

## Must Not Happen In The First Persistence Step

- Do not add EF Core, SQLite, migrations, or database schema code.
- Do not add a production persistence dependency without ADR acceptance.
- Do not make UI state or sample block plans Core authority.
- Do not treat local paths as scientific identity.
- Do not treat exported CSV/JSON as editable canonical state.
- Do not introduce cloud backup/sync/collaboration under a generic "persistence" label.
- Do not connect Avalonia sample host actions to Core mutation.

## Questions To Resolve Before Implementation

- One SQLite database or multiple stores?
- Canonical JSON only, or canonical JSON plus typed read models?
- Artifact bytes on disk only, or small artifacts in SQLite?
- Workspace/project ID generation policy.
- First supported method profile.
- Minimum actor identity for local desktop use.
- Minimum backup and bundle formats.
- Projection rebuild policy.
- Bundle import overwrite policy.
- Whether draft AI suggestions are app state or canonical proposal records before acceptance.
- Append-only versus replaceable record categories.
- Human-readable workspace folder surfaces.

## Bottom Line

Persistence should become a documentation and ADR slice before any code slice. The supplied planning base is the right raw material for that, but the repo should keep the current UI branch focused on renderer/sample-host boundaries.
