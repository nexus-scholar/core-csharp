# FE-08 Slice 4 Completion Evidence

Date: 2026-07-17

Authority: ADR 0037 and
`docs/gates/FE-08-SCREENING-AUTHORITY-RESOLUTION-SLICE-4.md`.

Status: complete.

## Delivered Scope

- strict canonical approved-Protocol authority package and rehydration;
- strict canonical title/abstract Screening criteria rehydration;
- immutable, pointer-last ResearchWorkspace Screening authority packages;
- exact bindings to current FE-01 generation, result, decision set, and snapshot;
- workspace-bound approved Protocol and criteria authority;
- explicit ready, unavailable, stale, invalid, and recovery-required projection;
- read-only Desktop.AppServices readiness facade with no scientific mutation;
- monotonic cross-generation project revision handling without weakening writer
  concurrency or authority pointer identity.

## Enforced Invariants

- Protocol content, approval policy, approval records, human actor roster, and
  approval sufficiency reproduce after restart;
- title/abstract criteria bind the exact approved Protocol content digest;
- foreign-workspace Protocol and unverified Workflow governance fail closed;
- package revision must be current, while source authority remains current only
  if its generation id and manifest digest are unchanged;
- stale packages can be replaced by a revision-bound successor;
- altered manifest or artifact bytes never become ready authority;
- desktop receives readiness projections only and cannot create Screening state.

## Validation

- focused Screening authority and criteria tests: 25 passed;
- ResearchWorkspace authority-generation tests: 9 passed;
- Desktop.AppServices readiness tests: 2 passed;
- architecture tests: 39 passed;
- Release build: 0 warnings and 0 errors;
- full solution: 906 passed, 0 failed, 0 skipped;
- `scripts/verify.ps1`: passed with the pinned `10.0.301` SDK, including release
  policy, restore/build, package reproducibility, 23-package clean-source smoke,
  SBOM validation, release evidence, tests, CLI doctor/demo, and formatting;
- independent architecture and scientific-invariant reviews required fixes,
  then accepted the corrected authority boundary with no blocking or
  high-severity findings.

PR 66 hosted validation passed on Ubuntu and Windows, including CodeQL/analyze
and automated dependency review. PR 66 merged to `main` as `7a071cc`.
Post-merge `scripts/verify.ps1` passed on that exact commit, including all 906
tests and release evidence bound to
`7a071ccaa6b3b53ff7a7755db7e8e6409350838f`.

## Claim Boundary

This evidence covers local durable authority resolution only. It does not admit
desktop Screening decisions, Workflow completion, Protocol or criteria
authoring, authentication, provider/network use, AI, plugins, database, API,
cloud, multi-user operation, PHP compatibility, installer, deployment,
accessibility certification, or production-security claims.
