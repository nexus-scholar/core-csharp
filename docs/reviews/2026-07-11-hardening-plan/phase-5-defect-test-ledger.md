# Phase 5 Defect-to-Test Ledger

Status: maintained as a release-blocking regression index.

| Review defect family | Permanent evidence |
| --- | --- |
| RFC 8785 number rendering, UTF-16 ordering, escaping, NFC profile | `KernelFixtureTests`, `DeterministicKernelTests`, official Appendix B fixtures |
| Invalid default IDs, digests, timestamps, and non-finite canonical values | `DeterministicKernelTests`, `KernelFixtureTests` |
| Protocol authority, approval, amendment, waiver, lineage, and digest rehydration | `ProtocolTests`, `ProtocolFixtureTests` mutation matrices |
| Workflow authority resolution, scalar tampering, supplemental authority | `WorkflowCompilerTests`, `WorkflowFixtureTests` mutation matrices |
| Provenance forged event state and concurrent append | `ProvenanceTests`, `ProvenanceFixtureTests` |
| Bundle malformed manifest, duplicate paths, stale digest, destructive overwrite | `BundleServiceTests`, `BundleFixtureTests` |
| Shared transitive bridge merge and duplicate stable identity | `SharedIdentityTests.Generated_overlap_graphs_are_order_independent_and_identity_unique`, shared identity fixtures |
| RIS comma authors, multiline CSV, nested/multiline BibTeX, malformed records | `SearchImportServiceTests`, `SearchFixtureTests`, deterministic parser mutation corpus |
| Dedup fabricated result shapes, non-finite scores, representative metadata | `DeduplicationServiceTests`, `DeduplicationFixtureTests` |
| Screening authority bindings, conflicts, confidence bounds and non-finite confidence | `ScreeningServiceTests`, `ScreeningFixtureTests` |
| Full Text cross-record bindings, digest scopes, acquisition/artifact chains | `FullTextServiceTests`, `FullTextFixtureTests` |
| Workspace malformed project, stale revision, corrupt generation, junction escape | `ResearchWorkspaceServiceTests` |
| Concurrent workspace writers and interrupted staging/promotion | `ResearchWorkspaceProcessTests` |

Coverage reports are diagnostic evidence only. Passing the mapped invariant tests is the exit criterion.
