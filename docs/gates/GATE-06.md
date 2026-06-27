# Gate 6: Portable Bundle and Artifact Contract

Status: planning only. Do not implement source code until the contract below is reviewed and accepted.

## Goal

Define the local C# contract for portable review bundles and artifact manifests before implementing export, verification, import, tamper checks, or round-trip behavior.

Gate 6 exists to make a review exportable and verifiable without Nexus Scholar cloud services. It must not silently adopt the blueprint bundle spec as authoritative while `CF-002`, `CF-005`, and `CF-014` remain open.

## Conflicts Addressed

- `CF-002`: bundle contract is blocking because current `NexusScholar.Bundles` is a thin scaffold and the blueprint bundle spec/schema are not closed local authority.
- `CF-014`: snapshot identity and equality affects bundle membership, round-trip equality, and import safety.
- `CF-005`: blueprint authority remains discovery-only; Gate 6 may use blueprint bundle materials as inputs but must not claim blueprint conformance.

## Planning Decisions To Freeze

Gate 6 needs one accepted ADR before implementation, provisionally named:

```text
docs/adr/0009-portable-bundle-and-artifact-contract.md
```

ADR 0009 must decide:

- bundle manifest identity and schema id/version;
- bundle manifest digest material and `bundle-manifest` digest scope;
- artifact entry shape;
- artifact digest rules, including raw byte digest versus manifest digest;
- logical artifact path rules and forbidden path forms;
- duplicate artifact path rejection;
- required bundle sections for local Gate 6;
- protocol version and `protocol-content` binding;
- workflow definition/compiled workflow binding;
- provenance ledger event inclusion rules;
- shared identity/corpus membership inclusion rules, if any;
- unresolved no-id candidate handling in bundles;
- snapshot equality rule for local Gate 6 only;
- tamper report shape and stable error categories;
- import validation behavior and destructive overwrite rejection;
- what is outside bundle digest;
- what remains future work.

## Implementation Scope After ADR Acceptance

Allowed implementation paths:

- `docs/adr/0009-portable-bundle-and-artifact-contract.md`
- `docs/gates/GATE-06.md`
- `docs/gates/GATE-06-EVIDENCE.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `src/NexusScholar.Artifacts/**`
- `src/NexusScholar.Bundles/**`
- `tests/NexusScholar.Core.Tests/**`
- `tests/NexusScholar.Architecture.Tests/**`
- `tests/NexusScholar.Conformance.Tests/**`
- `fixtures/conformance/bundles/**`
- `fixtures/conformance/artifacts/**`

Forbidden implementation paths:

- persistence, EF Core, SQLite, filesystem database adapters
- API/UI/cloud sync
- provider/network calls
- PHP reference repo changes
- PHP-generated compatibility fixtures
- Search, Deduplication, Screening, Citation Network, Full Text, Reporting ports
- plugin host/runtime
- AI governance behavior
- workflow execution engine

## Required Local Behavior After ADR Acceptance

The builder should implement only local Gate 6 behavior:

- create typed artifact entries with media type, byte size, raw-byte digest, logical path, artifact kind, schema id, schema version, and optional source record digest;
- reject blank artifact fields, invalid digests, negative size, duplicate logical paths, absolute paths, drive-letter paths, traversal paths, empty segments, and path separator drift;
- create deterministic bundle manifests from fixed inputs;
- compute bundle manifest digests with `DigestScope.BundleManifest`;
- preserve protocol approved version binding by id, version number, and `protocol-content` digest;
- preserve workflow definition or compiled workflow binding by id and digest when included;
- preserve provenance event references by `provenance-event` digest when included;
- verify raw artifact byte digests against manifest entries;
- produce stable tamper categories for missing artifact, checksum mismatch, duplicate path, unsupported required schema, stale manifest digest, and destructive overwrite attempt;
- import only after full verification succeeds;
- reject destructive overwrite attempts unless an explicit safe-import policy says otherwise;
- expose verification results as immutable snapshots.

## Fixture Plan

Required positive fixture IDs:

- `artifact-raw-byte-digest.json`
- `artifact-manifest-entry.json`
- `bundle-manifest-minimal.json`
- `bundle-manifest-with-protocol-workflow-provenance.json`
- `bundle-manifest-digest-stable.json`
- `bundle-roundtrip-local-equivalence.json`

Required negative fixture IDs:

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

## Verification Commands

```powershell
git grep -n "blueprint conformance\|PHP compatibility" docs/gates/GATE-06.md docs/gates/GATE-06-EVIDENCE.md docs/adr/0009-portable-bundle-and-artifact-contract.md
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

After push, hosted CI must pass on Windows and Ubuntu for the exact branch head.

## Explicit Non-Claims

- no blueprint conformance
- no PHP compatibility
- no PHP-generated fixtures
- no cloud portability guarantee beyond local file-level bundle semantics
- no persistence schema
- no API, UI, or cloud sync behavior
- no provider/network behavior
- no Search, Deduplication, Screening, Citation Network, Full Text, or Reporting behavior
- no workflow execution engine
- no plugin runtime
- no AI governance parity
