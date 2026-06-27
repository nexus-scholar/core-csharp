# Nexus Web Core Usage Recon

Status: app-layer reconnaissance evidence only.

This document records how `nexus-scholar/nexus-web` consumes PHP `nexus-scholar/core` and which product behavior it owns locally. It is not Core authority and must not override accepted C# Core ADRs.

## Authority Boundary

`nexus-web` is integration evidence. Accepted C# Core ADRs remain local authority. Pinned PHP Core remains the behavior reference for porting tasks. Web behavior can shape app compatibility and future gates, but it does not define Core records or digests.

## Inventory

Repository: `C:\Users\mouadh\Documents\AI in research\nexus-web`

Observed shape:

- Language and framework: PHP Laravel 13 backend, Inertia React 19 frontend.
- Package dependency: `nexus-scholar/core:^1.0`.
- Backend entrypoints: routes in `routes/web.php`, controllers under `app/Http/Controllers/Projects`, jobs under `app/Jobs`, actions under `app/Actions/Projects`.
- Frontend entrypoints: pages and components under `resources/js`.
- Tests: Laravel feature tests and Vitest component tests.
- Storage: Eloquent models and migrations for product workflows, audit rows, protocol snapshots, search plans/runs, dedup clusters, corpus snapshots, screening batches, full-text batches, workspaces, and auth.
- External configuration: `config/nexus.php` for scholarly providers, full-text sources, LLM screening, and project limits.

User-visible project routes include:

- protocol authoring and completion
- search plan authoring and search-run dispatch
- corpus review
- deduplication and corpus lock
- title/abstract screening queue and conflicts
- full-text retrieval
- full-text screening queue and conflicts
- activity/audit views

## Direct Core Usage

| File/path | Core concept | Usage | Risk | Notes |
| --- | --- | --- | --- | --- |
| `composer.json` | PHP Core package | Requires `nexus-scholar/core:^1.0` | Low | PHP package dependency only. |
| `app/Jobs/RunProjectSearchPlanJob.php` | Search | Calls `SearchExecutorPort` with `SearchAcrossProviders` | High | Web run/status UI depends on Search result shape. |
| `app/Actions/Projects/BuildProjectCorpusSlice.php` | `CorpusSlice`, `WorkId` | Loads internal works and uses `CorpusSlice::fromWorksUnsafe` | High | Intended to avoid premature premerge before Deduplication. |
| `app/Actions/Projects/RunProjectCorpusDeduplication.php` | Deduplication | Calls `DeduplicateCorpusHandler`, then adds exact-ID grouping and representative scoring | High | App extends PHP Core Dedup semantics. |
| `app/Actions/Projects/StartProjectScreeningBatch.php` | Screening run | Starts a Core screening run and creates app assignments | High | Human workflow orchestration is app-owned and user-visible. |
| `app/Actions/Projects/RecordProjectScreeningDecision.php` | Screening verdict | Records `ScreeningVerdict` through `ScreeningDecisionRepositoryPort` | High | Conflict and assignment rules live in the app. |
| `app/Actions/Projects/ResolveProjectScreeningConflict.php` | Screening adjudication | Records human adjudication verdicts through Core ports | High | App conflict workflow must inform future Screening gates. |
| `app/Jobs/RunProjectFullTextBatchJob.php` | Full text | Calls `RetrieveFullTextHandler` and maps result to app items | Medium | Artifact path and status records are app records. |
| `app/Queries/Projects/ProjectFullTextReadModel.php` | Full-text audit | Reads `FullTextFetchReaderPort` records for display | Medium | Read model is UI projection. |
| `app/Http/Controllers/Projects/ProjectSearchRunController.php` | Job lifecycle | Reads `JobLifecycleReaderPort` | Medium | App display depends on Core job progress shape. |

## App-Owned Product Behavior

Web owns broader product orchestration:

- workspace, project, membership, role, auth, and operator access
- protocol form fields, protocol status enum, readiness checks, and protocol snapshots
- project status enum and workflow navigation
- search-plan draft/version UI and run dispatch
- corpus membership hashing before deduplication
- app dedup cluster persistence and representative scoring
- app corpus snapshot rows and lock audits
- screening assignment batches, reviewer allocation, conflicts, and resolution UI
- full-text candidate gating, batches, item status rollups, and artifact download route
- `audit_events` rows and activity timeline

These are not C# Core protocol, workflow, provenance, bundle, or snapshot records unless mapped by a future accepted ADR.

## Gate Alignment Risks

- `ProjectProtocol` is not the Gate 3 protocol contract.
- `AuditEvent` rows are not Gate 5 provenance events.
- app corpus snapshots are not Gate 6 bundle equality or general C# snapshot equality.
- app dedup representative scoring extends Core semantics.
- screening assignments and conflicts are user-visible workflow execution behavior not covered by current C# Core.
- LLM screening config is app/provider/AI-governance evidence, not Core AI governance.

## Search Impact

Web needs Search output that can support:

- background run status and job lifecycle display;
- provider stats and partial failure evidence;
- raw and unique counts without forcing Search-time Deduplication;
- query item status and `core_search_query_id`;
- provider aliases and provider-work ids for later corpus, dedup, lock, screening, and full-text handoff.

ADR 0010 should include this app consumer boundary without moving Web persistence or UI behavior into C# Core.

## Non-Claims

- no C# Core behavior defined
- no Web behavior modified
- no PHP compatibility claimed
- no persistence/API/UI/cloud behavior moved into Core
- no provider/network or LLM implementation proposed
- no Search, Deduplication, Screening, Protocol, Provenance, Bundle, or Snapshot implementation started
