# App/Core Gap Register

Status: planning register for future gates.

This register captures app behavior that can break or mislead future C# Core work if it is treated as authority. It is not an implementation backlog by itself.

| Id | Area | Gap | Evidence | Risk | Recommended handling |
| --- | --- | --- | --- | --- | --- |
| `APP-001` | Source authority | CLI/Web behavior is integration evidence, not Core authority | Both apps depend on PHP Core but add host behavior | App behavior could override accepted ADRs by accident | Keep ADR order explicit in every app-facing contract. |
| `APP-002` | Search output | PHP Core and apps often consume deduplicated `CorpusSlice` results | CLI `SearchRunService`; Web `RunProjectSearchPlanJob` | C# Search could accidentally collapse duplicate provider sightings | ADR 0010 must define raw trace/result output before Deduplication. |
| `APP-003` | Unsafe corpus staging | Apps use `CorpusSlice::fromWorksUnsafe` for staging | CLI run merge; Web corpus slice build | Unsafe staging could become scientific membership | Limit to raw import/staging evidence; require later Dedup/Snapshot decisions. |
| `APP-004` | Display identity | CLI fallback key hashes title/year/provider for no-id works | CLI `SearchResultSerializer` | Projection key could be mistaken for scientific identity | Mark display hashes as projection identifiers only. |
| `APP-005` | Search plans | CLI YAML and Web DB plan drafts carry app-specific fields | CLI plan loader; Web search-plan actions | C# plan parser could either reject app fields or silently accept unsafe fields | ADR 0010 must decide permissive versus schema-closed plan policy. |
| `APP-006` | Protocol | Web `ProjectProtocol` is not Gate 3 protocol | Web protocol model and version snapshots | App status `complete` could be mistaken for approved protocol | Add future app-alignment mapping before protocol integration. |
| `APP-007` | Provenance | Web `audit_events` are not Gate 5 provenance events | Web `RecordAuditEvent` | Activity rows lack event digest and Core bindings | Keep as UI audit projection until mapped by a provenance/app gate. |
| `APP-008` | Bundle/artifact | CLI/Web full-text manifests and artifact paths are not ADR 0009 artifacts | CLI full-text manifest; Web full-text items | Local storage path could be treated as logical artifact identity | Future artifact export must use ADR 0009 logical path and raw-byte digest rules. |
| `APP-009` | Corpus snapshot | Web corpus snapshots are app DB rows | Web `LockProjectCorpus` | App snapshot equality could be confused with Gate 6 bundle equality | Defer general snapshot equality to future Corpus/Dedup/Screening gate. |
| `APP-010` | Dedup policy | Web adds exact-ID grouping and representative scoring after Core Dedup | Web `RunProjectCorpusDeduplication` | C# Dedup port could miss user-visible representative behavior | Dedup reconnaissance must include Web app policy as consumer evidence. |
| `APP-011` | Screening workflow | Web assignments/conflicts are product workflow behavior | Web screening actions and routes | C# Screening port could ignore user-visible adjudication needs | Screening reconnaissance must map verdict semantics separately from app workflow. |
| `APP-012` | Full text | Apps batch and display full-text retrieval around PHP Core | CLI retriever; Web full-text job/read model | Retrieval artifacts and statuses could drift from future Core contract | Keep batching/UI app-level; later Full Text gate defines domain/audit records. |
| `APP-013` | Provider behavior | Apps expose provider config and live-provider expectations | `config/nexus.php` in both apps | C# local Search could imply provider support too early | First Search implementation should use stub providers only. |
| `APP-014` | AI governance | CLI/Web include LLM screening paths/config | CLI file screening; Web LLM config | LLM output could be treated as decision authority | Defer to governed AI gate; record as proposals/evidence only. |

## Immediate Search Blockers

These app gaps are blockers for pretending Search is app-aligned:

- `APP-002`: raw Search trace/result shape not defined.
- `APP-003`: unsafe corpus staging boundary not resolved for Search output.
- `APP-004`: display identity and scientific identity must stay separate.
- `APP-005`: app plan fields versus schema closure not decided.

They do not block Search reconnaissance or ADR 0010 drafting.

## Later Gate Blockers

Deduplication:

- `APP-010` must be considered before Dedup implementation claims.

Screening:

- `APP-011` must be considered before Screening implementation claims.

Protocol:

- `APP-006` must be mapped before Web protocol records are treated as Core protocol records.

Provenance:

- `APP-007` must be mapped before app audit rows are treated as Core provenance.

Bundle and snapshot:

- `APP-008` and `APP-009` must be resolved before app exports or snapshots are treated as portable review bundles or canonical corpus snapshots.

## Non-Claims

- no gap is resolved by this register
- no app behavior is accepted as Core behavior
- no implementation work started
- no PHP compatibility claimed
