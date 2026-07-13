# Core Hardening Plan - 2026-07-11

Status: active hardening plan.

Implementation progress:

- Phase 1 canonical foundation: complete through Hardening 01 and Hardening 02.
- Phase 2 authority-safe rehydration: complete through Hardening 11 across Kernel, Protocol, Workflow, Provenance, Bundle, Deduplication, Screening, and Full Text.
- Phase 3 scholarly pipeline correctness: complete through Hardening 16 across Shared identity, Search parsers, Deduplication metadata, Screening conflicts, and Full Text cross-record validation.
- Phase 4 transactional workspace integrity: complete through Hardening 17.
- Phase 5 test strategy upgrade: complete through Hardening 18.
- Phase 6 release engineering: in progress through Hardening 22 validation-only release and security-analysis workflows.
- Next gate: Hardening 23 Pages recovery and operational/security documentation.

Source review: [full-technical-review.md](full-technical-review.md)

Baseline:

- Repository: `nexus-scholar/core-csharp`
- Commit: `7f9e2850dc312cb0b8e8ac0007421937bf5fad1c`
- Date recorded: 2026-07-11
- Verified state from source review: clean worktree, full repository gate passed, 419 tests passed, build and formatting passed.

## Decision

Feature expansion is frozen. The next development phase is integrity hardening.

The repository remains useful for contract development, deterministic local fixtures, architecture exploration, and controlled alpha demonstrations. It must not be used yet for real evidence reviews, final scientific decisions, published NuGet packages, durable workspace storage, PHP compatibility claims, or production desktop workflows.

This plan supersedes the 2026-06-29 public-readiness plan as the current operating plan. Older review folders remain historical context only.

## Hardening Order

1. Phase 0 - freeze and record:
   - open one issue per confirmed blocker;
   - correct public maturity wording;
   - protect `main`;
   - assign each blocker an owner, test case, and dependency order.
2. Phase 1 - canonical foundation:
   - correct RFC 8785/JCS behavior;
   - decide digest migration/versioning;
   - add official cross-language vectors;
   - reject invalid default IDs and digests.
3. Phase 2 - authority-safe rehydration:
   - apply the unverified DTO, validated factory, resolver, recomputed digest, verified result pattern across authority-bearing modules.
4. Phase 3 - scholarly pipeline correctness:
   - repair Shared transitive merge, Search parsing, Dedup representative metadata, Screening bindings/conflicts, and Full Text cross-record validation.
5. Phase 4 - transactional workspace:
   - add strict project schema, generation manifests, staging, atomic promotion, locking/CAS, safe path resolution, and stale-generation rejection.
6. Phase 5 - test strategy upgrade:
   - add permanent regression coverage for every reproduced defect, including canonical vectors, property tests, parser fixtures, malformed rehydration, finite-number checks, and crash/concurrency tests.
7. Phase 6 - release engineering:
   - define package topology, license, versioning, reproducible build, SBOM/signing/provenance, release workflow, package smoke tests, Pages recovery, and current operational docs.
8. Phase 7 - compatibility evidence:
   - only after local correctness, generate pinned PHP fixtures and semantic comparators before making compatibility claims.

## Current Blocker Themes

- Canonical JSON number rendering and the Nexus NFC profile were corrected in Hardening 01; Kernel verified rehydration and default-value rejection were completed in Hardening 02.
- Authority-safe rehydration is complete for the Phase 2 module set; pipeline semantics remain subject to Phase 3 hardening.
- Provenance append now recomputes event digests, reruns event invariants, and serializes concurrent in-memory appends through Hardening 07.
- Shared bridge records now merge the complete overlap-connected component and validated rehydration rejects duplicate stable identity.
- Search import parsers now preserve RIS comma-form authors, multiline quoted CSV records, and nested/multiline BibTeX fields.
- Screening now resolves Protocol and Deduplication authority, and Full Text verifies its complete input/acquisition/attempt/artifact chain.
- ResearchWorkspace imports and analysis now use staged atomic promotion, locking/CAS, revision-bound generation manifests, digest verification, and safe path resolution.
- Delivery posture is not release-managed: branch protection, Pages, and security contact remain incomplete.

## Rules For Follow-Up Work

- Do not add new product surface before the relevant hardening phase is complete.
- Do not broaden Full Text into live retrieval, provider SDKs, OCR, PDF text extraction, persistence, API, UI product shell, cloud behavior, or PHP compatibility claims.
- Do not call the project audit-grade in public-facing material until the hardening plan's authority and release-engineering blockers are closed.
- Treat legacy PHP behavior as evidence only after fixture-backed comparison.
- Keep Core domain projects free of EF Core, ASP.NET Core, UI frameworks, provider SDKs, storage SDKs, and concrete model clients.

## Verification Baseline

Normal changes must still run:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Hardening branches must also run affected architecture and conformance tests. CI must not call live scholarly providers or live LLMs.
