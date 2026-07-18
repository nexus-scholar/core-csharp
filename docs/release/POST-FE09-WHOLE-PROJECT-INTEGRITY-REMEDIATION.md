# Post-FE-09 Whole-Project Integrity Remediation Evidence

Status: locally complete on `cdx/fe-09-deep-review-remediation`; not merged.

Date: 2026-07-18

Authority:

- ADR 0045;
- accepted Post-FE-09 Whole-Project Integrity Remediation gate;
- the repository commit containing this evidence record.

## Repaired Findings

- Research Workspace paths use one cross-platform containment boundary,
  transaction commits revalidate exact source bytes under the commit lock,
  and successor corpus authority clears predecessor-bound current pointers
  while preserving immutable history.
- Provider-cache reads, rebuilds, and idempotent records verify retained body
  length and digest. One decoded secret/contact policy now governs recorded and
  live provider descriptors with operation-scoped Semantic Scholar tokens.
- Nested RIS blocks and duplicate normalized Scopus headers fail closed with
  stable skipped-record evidence. Colon-bearing WorkIds round-trip without
  admitting ambiguous namespace-prefixed values.
- Canonical scientific construction and rehydration reject default timestamps.
  Superseded Protocol authority round-trips as historical evidence while
  active Screening admission still requires Approved status.
- AI proposals snapshot mutable values. Extension manifests and capability
  selections reject undefined values and restrict grants to the manifest's
  requested capability set.
- The pinned SDK resolver, exact mutation manifest, package metadata, SBOM
  namespace, current repository links, CODEOWNERS, governance verifier, and
  project-state documents now describe the same repository and branch state.

## Verification

Pre-commit validation under pinned SDK `10.0.301`:

- `scripts/verify.ps1`: passed;
- Release build: zero warnings and zero errors;
- full solution: 1,048 passed, zero failed, four skipped;
- Architecture: 44 passed;
- Conformance: 142 passed;
- scientific-invariant manifest: 136 exact cases across eight project suites;
- package policy and clean local-source smoke: 24 validation-only packages at
  `0.1.0-alpha.2`;
- release evidence: 28 artifacts and 59 lock files, with dirty pre-commit
  state explicitly recorded as validation-only;
- pinned-SDK `dotnet format`: passed;
- `npm run verify` under `site/`: 49 Astro files clean, 45 pages built, 1,022
  local references, and zero distribution issues;
- Bash syntax and pinned-SDK resolution: passed;
- tracked-source OpenAlex credential scan: no credential material found.

Two skips are Linux-only path assertions on the Windows host and must execute
in Linux CI. Two are opt-in live-provider smokes; default CI remains
network-free. The exact commit containing this record must also pass
`scripts/verify.ps1` with `sourceTreeDirty=false` before merge.

## Independent Review

Independent scientific-invariant, .NET architecture, conformance, test-gap,
and extension-security reviews were run. Their reproduced findings added:

- bare-email and signed-credential descriptor rejection;
- retained-body verification on idempotent cache writes;
- operation-scoped Semantic Scholar continuation tokens;
- manifest-bounded capability selection;
- exact parser evidence assertions;
- isolated Linux case-sensitivity and cache length-mismatch tests;
- an executable exact-name mutation manifest rather than substring selection.

No blocker remains in the local implementation after those repairs.

## Remote Governance Blocker

The repository currently has one collaborator, `nexus-scholar`. Live branch
protection inspection on 2026-07-18 found:

- zero required approvals;
- CODEOWNER review disabled;
- approval after the latest push disabled;
- linear history disabled;
- signed commits disabled.

The local verifier correctly rejects that state. Independent approval cannot
be satisfied until another eligible reviewer is added. No remote setting,
branch, package, release, or deployment was modified by this remediation.

## Invariants And Compatibility

- Human authority, immutable history, exact bytes, provenance, invalidation,
  and local-first behavior remain controlling.
- Provider observations and model/plugin outputs do not become scientific
  authority.
- No golden fixture or `specs/SOURCE.lock.json` entry changed.
- No new PHP compatibility claim is made.

## Nonclaims

This remediation does not authorize FE-10 or FE-11 runtime work, arbitrary
plugin execution, an isolation sandbox, live Full Text retrieval, scraping,
paywall bypass, provider completeness, package publication, deployment, cloud
state, authentication, tenancy, or production readiness.
