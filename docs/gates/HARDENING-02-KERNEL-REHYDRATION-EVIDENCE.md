# Hardening 02: Kernel verified rehydration evidence

Status: local verification complete on 2026-07-11; hosted CI pending publication.

## Behavior Implemented

- Added `DigestEnvelope.RehydrateAndVerify` as the only Kernel path from untrusted envelope JSON to `VerifiedDigestEnvelope`.
- Verification requires a caller-selected expected digest, scope, schema ID, and schema version.
- Envelope roots are closed to exactly algorithm, canonicalization profile, content, schema, schema version, and scope.
- Rehydration rebuilds immutable canonical content and recomputes the digest before returning verified state.
- Default `ContentDigest`, `DigestAlgorithm`, `DigestScope`, and `EntityId<TTag>` values now fail closed when used as authority values.
- Entity ID factories reject `Guid.Empty`, including misbehaving ID generators.
- Bundle verification validates digest-bearing artifacts before manifest canonicalization, preserving structured invalid-digest findings instead of leaking default-value exceptions.

## Invariants Enforced

- Untrusted envelope scope and schema metadata cannot select the verification contract.
- Unknown, missing, duplicate, null, and wrongly typed root fields are rejected.
- Wrong algorithm, profile, scope, schema, version, digest, and tampered nested content are rejected.
- Transport whitespace and property order do not affect the reconstructed canonical digest.
- A verified wrapper cannot hold a digest that differs from its envelope's recomputed digest.
- Invalid bundle digest state cannot be rendered into an authoritative manifest digest.

## Tests And Fixtures

- Added 14 metadata-bearing Kernel rehydration fixtures: one valid transport case and 13 adversarial cases.
- Focused deterministic Kernel tests: 33 passed.
- Focused Bundle tests: 18 passed.
- Focused Kernel conformance tests: 10 passed.
- Full solution: 437 passed, 0 failed, 0 skipped.
- Existing fixture digests were unchanged.
- A read-only scientific-invariant audit completed after remediation and found no remaining blocker in the touched scope.

## Verification

The following completed successfully:

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter DeterministicKernelTests
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter KernelFixtureTests
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
git diff --check
```

`scripts/verify.ps1` also passed CLI doctor, sample, and demo smoke checks with no live providers or network behavior.

## Files Changed

- `src/NexusScholar.Kernel/ContentDigest.cs`
- `src/NexusScholar.Kernel/DigestEnvelope.cs`
- `src/NexusScholar.Kernel/DigestTypes.cs`
- `src/NexusScholar.Kernel/KernelPrimitives.cs`
- `src/NexusScholar.Bundles/BundleVerifier.cs`
- `tests/NexusScholar.Core.Tests/DeterministicKernelTests.cs`
- `tests/NexusScholar.Core.Tests/BundleTests.cs`
- `tests/NexusScholar.Conformance.Tests/KernelFixtureTests.cs`
- new `fixtures/conformance/kernel/kernel-rehydrate-*.json` fixtures
- Hardening 02 plan and evidence documents

## Remaining Risks

- Generic envelope verification establishes Kernel integrity only; Protocol, Workflow, Provenance, Bundles, Deduplication, Screening, and Full Text still require module-specific unverified DTOs, validated factories, resolvers, and semantic checks.
- Invalid bundle manifests carry a default non-authoritative manifest digest only alongside structured verification errors. Accessing it fails closed; a later Bundle authority gate may model this explicitly as an optional digest.
- No persistence migration is provided or implied.

## ADR And Compatibility Impact

No new ADR was required. This gate implements the typed scope/schema/profile and deterministic digest rules already accepted in ADR 0002 and corrected by ADR 0017. It makes no PHP, blueprint, package, persistence, or cross-version compatibility claim.
