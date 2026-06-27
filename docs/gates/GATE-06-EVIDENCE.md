# Gate 6 Evidence: Portable Bundle and Artifact Contract

Status: local verification passed; hosted CI passed for the Gate 6 implementation commit. The final branch head still requires hosted CI before merge when this evidence file changes.

## Scope

Gate 6 implements local C# bundle and artifact behavior only.

Implemented conflict scope:

- `CF-002`: implemented for local bundle/artifact manifest and verification behavior.
- `CF-014`: implemented only for local bundle round-trip equality and import safety.

Still out of scope:

- blueprint conformance, PHP compatibility, PHP-generated fixtures, persistence schema, API, UI, cloud sync, provider/network behavior, Search, Deduplication, Screening, Citation Network, Full Text, Reporting, workflow execution, plugin runtime, AI governance parity, and general corpus snapshot equality.

## Source Decisions

- `docs/adr/0009-portable-bundle-and-artifact-contract.md`
- `docs/adr/0002-canonical-json-and-digests.md`
- `docs/adr/0003-protocol-record-contract.md`
- `docs/adr/0006-workflow-compiler-semantics.md`
- `docs/adr/0007-shared-scientific-identity.md`
- `docs/adr/0008-provenance-ledger.md`
- `docs/gates/GATE-06.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`

## Behavior Implemented

- Artifact entries record artifact ref, logical path, artifact kind, media type, byte size, raw-byte digest, schema id/version, and optional source/workflow/provenance/requirement references.
- Logical artifact path validation rejects blank, backslash, absolute, drive-letter, URI, traversal, empty-segment, dot-segment, leading slash, and trailing slash forms.
- Raw artifact byte digests use exact bytes and `DigestScope.RawArtifactBytes`.
- Review bundle manifests use schema id `nexus.review-bundle.manifest`, schema version `1.0.0`, `bundle_kind = review-bundle`, canonical manifest identity, protocol binding, artifact entries, required schemas, verification policy, and optional workflow/provenance/shared-identity/unresolved-candidate/notes sections.
- Manifest digests use `DigestScope.BundleManifest`, schema id `nexus.review-bundle.manifest`, schema version `1.0.0`, and exclude the manifest digest itself.
- Bundle construction deterministically orders artifacts, required schemas, provenance bindings, shared-identity membership, and unresolved candidates.
- Verification returns immutable snapshots for validity, manifest digest, verified artifacts, errors, and warnings.
- Verification rejects duplicate logical paths, invalid paths, invalid digests, negative sizes, missing artifacts, size mismatches, checksum mismatches, unsupported required schemas, stale manifest digests, workflow/protocol mismatches, provenance digest mismatches, and destructive overwrite attempts.
- Import behavior is staged validation only. No persistence, filesystem writes, API, UI, or cloud sync behavior is implemented.

## Fixture IDs

Positive:

- `artifact-raw-byte-digest.json`
- `artifact-manifest-entry.json`
- `bundle-manifest-minimal.json`
- `bundle-manifest-with-protocol-workflow-provenance.json`
- `bundle-manifest-digest-stable.json`
- `bundle-roundtrip-local-equivalence.json`

Negative:

- `artifact-invalid-digest.json`
- `artifact-negative-size.json`
- `artifact-forbidden-path-absolute.json`
- `artifact-forbidden-path-traversal.json`
- `bundle-duplicate-artifact-path.json`
- `bundle-missing-artifact.json`
- `bundle-checksum-mismatch.json`
- `bundle-unsupported-required-schema.json`
- `bundle-stale-manifest-digest.json`
- `bundle-destructive-overwrite-reject.json`

Fixture source kind:

```text
local-gate-6-contract
```

Fixtures are hand-authored local conformance fixtures, not PHP-generated goldens.

## Local Verification

- `dotnet restore NexusScholar.Core.slnx`: passed.
- `dotnet build NexusScholar.Core.slnx -c Release --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release`: passed, 112 tests total.
- `dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release`: passed, 32 tests total.
- `dotnet test tests/NexusScholar.Architecture.Tests/NexusScholar.Architecture.Tests.csproj -c Release`: passed, 11 tests total.
- `dotnet test NexusScholar.Core.slnx -c Release --no-build`: passed, 155 tests total.
  - `NexusScholar.Architecture.Tests`: 11 passed.
  - `NexusScholar.Conformance.Tests`: 32 passed.
  - `NexusScholar.Core.Tests`: 112 passed.
- `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`: passed.
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1`: passed.

## Hosted CI

- `gate-01` workflow dispatch on `cdx/gate-6-bundle-planning`: passed.
- Commit: `a4cbd7b28f1543851c1a093d823b9f696304ea68`
- Run: `https://github.com/nexus-scholar/core-csharp/actions/runs/28274623890`
- Matrix:
  - `verify (ubuntu-latest)`: passed.
  - `verify (windows-latest)`: passed.

## Explicit Claims Not Made

- no blueprint conformance
- no PHP compatibility
- no PHP-generated fixtures
- no persistence schema
- no API, UI, or cloud sync
- no provider/network behavior
- no Search, Deduplication, Screening, Citation Network, Full Text, or Reporting behavior
- no workflow execution engine
- no plugin runtime
- no AI governance parity
- no general corpus snapshot equality
