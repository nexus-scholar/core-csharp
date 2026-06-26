# PHP Architecture Map

Status: Gate 0 discovery only. This is a behavior map for the pinned PHP reference at commit `b24d0d71ec7b64003465182477e7edb7f49994f4`.

## Source Lock Verification

- `specs/SOURCE.lock.json` pins `../core` as the PHP behavioral reference.
- `git -C ../core rev-parse HEAD` matched the pinned commit during this discovery pass.

## Porting Rule

Port observable behavior, invariants, and fixture-backed results. Do not port Laravel structure, Eloquent models, Artisan commands, Guzzle clients, or service-provider wiring into the C# domain.

## Module Map

| PHP module | Primary entry points | Audit and persistence evidence | Core invariants | Planned C# target | Fixture seed |
| --- | --- | --- | --- | --- | --- |
| `Shared` | `WorkId`, `WorkIdSet`, `ScholarlyWork`, `CorpusSlice`, `CorpusLockPolicy` | `work_external_ids`, `query_works`, corpus membership, project lock state | identity is ID-based, not title-based; non-empty title; snapshot membership is authoritative | `NexusScholar.Kernel` plus future `NexusScholar.Corpus` | normalized identity and merge fixtures |
| `Search` | `SearchAcrossProvidersHandler`, `PersistentSearchRunner`, `SearchPlanRunner`, `YamlSearchPlanParser` | `search_queries`, `search_query_providers`, `query_works`, provider stats | query normalization, provider-order-insensitive cache key, partial provider failure is expected, locked project blocks mutation | future `NexusScholar.Search` | stub-provider persisted trace and YAML plan fixtures |
| `Deduplication` | `DeduplicateCorpusHandler`, `LockCorpusHandler`, `UnlockCorpusHandler`, `WorkFuser`, election and match policies | `dedup_clusters`, `cluster_members`, snapshot lock flags | exact and fuzzy duplicate evidence is preserved; representative election is explainable; lock freezes corpus authority | future `NexusScholar.Corpus` | transitive cluster, representative election, lock fixtures |
| `Screening` | `ScreenWorkHandler`, `ScreenCorpusHandler`, `AdjudicateScreeningDecisionsHandler`, `CompareScreeningRunsHandler`, `CouncilDecisionAggregator` | `screening_runs`, `screening_decisions`, `screening_votes` | tri-state outcome including `needs_review`; locked corpus required; adjudication is explicit human authority | future `NexusScholar.Screening` | single-run, council conflict, adjudication fixtures |
| `CitationNetwork` | `BuildCitationGraphHandler`, `AnalyzeNetworkHandler`, `FindShortestCitationPathHandler`, `SnowballCorpusHandler` | citation graph tables, graph repositories, export inputs | identity links remain explicit; graph and snowball behavior tolerate partial provider behavior | future `NexusScholar.Network` | edge, metric, shortest-path fixtures |
| `Dissemination` | bibliography export, graph export, full-text retrieval handlers | export history, full-text fetch records, stored artifacts | citable/exportable outputs must honor snapshot authority and audit history | future `NexusScholar.Dissemination` and `NexusScholar.Reporting` | retrieval audit, bibliography, and graph fixtures |
| `Laravel host` | service provider, commands, jobs, listeners, migrations, Eloquent repositories | host persistence, CLI, scheduling, adapters | host concerns are observable but not domain law | host-only, not a domain target | none; exclude from domain port |

## Recommended Behavior Port Order

1. Shared identities and canonical scholarly work behavior.
2. Search request normalization and persisted search trace.
3. Deduplication and representative election.
4. Corpus lock and immutable snapshot behavior.
5. Screening, council disagreement, and adjudication.
6. Full-text retrieval audit.
7. Citation graph and snowball behavior.
8. Export and reporting projections.

This order matches the migration note and minimizes false conformance claims.

## Existing PHP Evidence Sources

- Unit tests under `../core/tests/Unit`
- Feature tests under `../core/tests/Feature`
- Provider integration tests under `../core/tests/Integration/Provider`
- Search-plan fixtures under `../core/tests/Fixture/search_plans`
- VCR cassette fixtures under `../core/tests/Fixture/vcr_cassettes`

No dedicated cross-language golden pack exists yet. Gate 0 therefore uses PHP tests and existing local fixtures as evidence sources, then plans new generated goldens.

## Cross-Cutting Invariants

- Scientific identity is based on normalized external identifiers and stable digests, not titles or runtime object identity.
- Locked corpus membership is authoritative for screening, adjudication, graph, full-text, and export behaviors.
- Partial provider failure is normal in search and snowballing; failure handling must stay explicit.
- Screening is tri-state, not boolean.
- Adjudication is an explicit authority change, not a silent overwrite.
- Exportable or citable state depends on immutable snapshot authority, not only on current mutable tables.

## PHP Ambiguities To Carry Into `OPEN-CONFLICTS.md`

### Title fuzzy threshold

- `TitleFuzzyPolicy` comments recommend `92`.
- `NexusServiceProvider` binds `95`.

Decision needed:
Freeze the compatibility threshold before claiming parity.

### Raw duplicate input representation

PHP uses an unsafe path to preserve duplicate candidates before deduplication. C# needs an explicit raw-result representation instead of silently copying the PHP convenience shape.

### Search-side host convenience

`EloquentSearchRunRecorder` auto-creates project rows. Gate 0 should treat that as host behavior until explicitly promoted to kernel law.

### Non-portable scientific identity fallback

`CorpusSlice` falls back to `spl_object_hash` in one path. That is process-local and not acceptable as a C# scientific identity rule.
