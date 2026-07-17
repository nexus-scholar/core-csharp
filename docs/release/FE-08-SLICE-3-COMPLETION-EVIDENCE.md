# FE-08 Slice 3 Completion Evidence

Date: 2026-07-17

Authority: ADR 0036 and
`docs/gates/FE-08-DESKTOP-DEDUPLICATION-REVIEW-SLICE-3.md`.

Status: implementation and local/hosted validation complete; protected merge pending.

## Delivered Scope

- shared structured ResearchWorkspace deduplication review operation;
- CLI adapter over the shared operation with preserved preview/commit rendering;
- authority-aware queue with active decision ids and digests;
- desktop actor/role, policy action/reason, rationale, and exact supersession;
- canonical confirmation token over complete authority and effect material;
- product review queue, effect inspector, explicit confirm, cancel, and refresh;
- verified base-generation projection after immutable authority successors;
- solution-derived restore-lock evidence and Windows PowerShell-compatible
  release-evidence hashing/path serialization.

## Enforced Invariants

- verified policy assignment, not caller text or UI state, authorizes actor/role;
- a decided target requires exact active-decision supersession;
- preview binds workspace, revision, authority generation, source, policy,
  snapshot, decision set, target, request, actor, action, rationale, candidate
  membership, invalidations, and resulting unresolved state;
- cancellation is non-mutating;
- changed preview or authority fails stale before commit;
- lock contention is recovery-required;
- Screening and all other scientific decision families remain unavailable.

## Validation

Final local results:

- CLI tests: 79 passed;
- Desktop.AppServices tests: 28 passed;
- Desktop product-host tests: 10 passed;
- ResearchWorkspace tests: 37 passed;
- architecture tests: 39 passed;
- full solution: 878 passed, 0 failed, 0 skipped;
- Release build: 0 warnings and 0 errors;
- `dotnet format --verify-no-changes`: passed;
- `scripts/verify.ps1`: passed, including release policy, 23-package smoke,
  SBOM validation, 27-artifact/45-lock release evidence, CLI doctor/sample/demo,
  tests, and formatting;
- native Windows visual QA: passed after finding and closing the reusable-control
  re-render crash; see `FE-08-SLICE-3-VISUAL-QA.md`;
- three independent implementation, scientific-invariant, and test/release
  reviews: accepted with no blocking or high-severity finding;
- PR 64 hosted validation: Ubuntu, Windows, CodeQL/analyze, and automated review
  passed.

Protected merge and post-merge validation remain pending.

## Claim Boundary

This evidence is limited to local FE-02 deduplication review through the desktop
product. It makes no Screening, authentication, identity-provider, provider,
network, AI, database, API, cloud, multi-user, PHP, blueprint, installer,
deployment, accessibility-certification, or production-security claim.
