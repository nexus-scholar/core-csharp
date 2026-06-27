# App Behavior Beyond Current Core

Status: integration evidence map.

This document classifies CLI/Web behavior that exists outside current C# Core gates. App behavior can inform future Core contracts, but it does not override accepted ADRs.

## Classification

| Behavior | Repo | Current owner | Recommended owner | Reason |
| --- | --- | --- | --- | --- |
| CLI run JSON files and latest pointer | CLI | App | App | Local workflow output and command UX. |
| CLI wiki ingestion | CLI | App | App | Wiki pages are projections, not canonical scientific state. |
| CLI file-based deterministic screening | CLI | App | Unclear, needs decision | Useful behavior, but not current Core Screening authority. |
| CLI file-based LLM screening | CLI | App | Future AI governance decision | Automation output remains proposal/evidence, not scientific authority. |
| CLI full-text manifest | CLI | App | App until artifact/bundle ADR maps it | Not an ADR 0009 manifest with logical paths and raw-byte digests. |
| CLI graph JSON output | CLI | App plus PHP Core graph use cases | Split | Graph construction may be future Core domain behavior; JSON file writing is app projection. |
| Web protocol form and snapshots | Web | App | Candidate Core mapping later | User-visible protocol lifecycle, but not Gate 3 approved protocol contract. |
| Web project workflow statuses | Web | App | App | Navigation/product state belongs outside Core domain. |
| Web search plan drafts and run UI | Web | App orchestration plus PHP Core execution | Split | Query/trace/result contract belongs in Core; UI and background dispatch are app layer. |
| Web corpus membership hash | Web | App | Unclear, needs decision | Useful stale-change guard, but not ADR 0002 canonical digest. |
| Web dedup representative scoring | Web | App after PHP Core Dedup | Candidate Dedup decision | Extends Core semantics and affects locked snapshots. |
| Web dedup cluster persistence | Web | App persistence | App unless Core defines persistence | Persistence/API/UI remain outside current C# Core. |
| Web corpus snapshots | Web | App | Candidate snapshot gate later | Not Gate 6 bundle equality and not general corpus snapshot equality. |
| Web screening assignments | Web | App | App workflow plus future Screening contract | Human work allocation is product behavior; verdict semantics may be Core. |
| Web screening conflicts and adjudication UI | Web | App plus PHP Core verdict ports | Split | Conflict workflow should inform future Screening reconnaissance. |
| Web full-text batches/items | Web | App plus PHP Core retrieval | Split | Retrieval status may inform Core; batching and UI are app layer. |
| Web audit events | Web | App | App projection unless mapped to provenance | Not Gate 5 event records with digests and protocol/workflow bindings. |
| Web workspace/auth/operator controls | Web | App | App | Access control and hosted UX are app concerns. |
| Provider configuration | Both | App config plus PHP Core providers | Future provider gate | C# Core currently makes no provider/network claim. |

## Cross-Gate Summary

Search:

- Apps need raw provider sightings, provider stats, partial failures, and display-ready run status.
- Search must not return canonical `CorpusSlice` membership as its primary output.
- Duplicate provider sightings must survive Search for later Deduplication.

Deduplication:

- Web already adds exact-identifier grouping and representative scoring after PHP Core Dedup.
- Future Dedup reconnaissance must decide whether those policies remain app-only or move into Core.

Screening:

- CLI has local file-based screening.
- Web has human assignment, conflict, and adjudication workflows.
- Future Screening reconnaissance must separate verdict/domain behavior from app workflow orchestration.

Protocol:

- Web protocol records are product forms and snapshots.
- They are not approved immutable protocol versions under ADR 0003 and ADR 0004.

Provenance:

- App audit events are useful activity projections.
- They are not Gate 5 provenance events.

Bundles and artifacts:

- App manifests and artifact paths are local outputs.
- They are not ADR 0009 review-bundle manifests or artifact entries.

## Non-Claims

- no implementation scope added
- no app behavior made authoritative
- no provider/network, persistence, API, UI, cloud, plugin, or AI governance behavior moved into Core
- no PHP compatibility claimed
