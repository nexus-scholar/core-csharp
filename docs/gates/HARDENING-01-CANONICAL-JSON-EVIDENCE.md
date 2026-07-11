# Hardening 01: Canonical JSON evidence

Status: complete on 2026-07-11

## Behavior Implemented

- JSON numeric tokens now enter one finite IEEE 754 binary64 path instead of preferring `decimal`.
- Number output follows RFC 8785 and ECMAScript thresholds, including decimal output for `1e-6` through values below `1e21`, scientific output outside that range, and `0` for negative zero.
- Non-finite, out-of-range, non-exact `long`, and non-exact `decimal` inputs are rejected rather than silently changing scientific values.
- The canonicalization profile is now `nexus-jcs-nfc-v1`, making the Nexus NFC preprocessing rule explicit instead of claiming pure `rfc8785-jcs` behavior.
- Provisional Workflow, Provenance, and Bundle fixture digests affected by the profile migration were regenerated from the corrected implementation.

## Invariants Enforced

- Direct and parsed representations of the same binary64 value serialize identically.
- Every finite RFC 8785 Appendix B bit pattern matches its required JSON representation.
- Appendix B NaN and infinity patterns are rejected.
- Exact values wider than the accepted numeric model must be represented as schema-typed strings.
- Old and corrected canonicalization semantics cannot share a profile identifier.

## Tests And Fixtures

- Added complete RFC 8785 Appendix B finite and non-finite bit-pattern fixtures.
- Added regressions for `1e-6`, `1e20`, `1e21`, negative zero, parsed `333333333.33333329`, out-of-range numeric tokens, exact/non-exact `long`, and non-exact/overflowing `decimal` values.
- Focused Kernel tests: 29 passed.
- Focused Kernel conformance tests: 8 passed.
- Full solution: 426 passed, 0 failed, 0 skipped.

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

`scripts/verify.ps1` also passed the CLI `doctor`, `sample`, and `demo` smoke checks with no live providers or network behavior.

## Files Changed

- `src/NexusScholar.Kernel/CanonicalJson.cs`
- `tests/NexusScholar.Core.Tests/DeterministicKernelTests.cs`
- `tests/NexusScholar.Conformance.Tests/KernelFixtureTests.cs`
- `fixtures/conformance/kernel/`
- affected local profile-migration fixtures under `fixtures/conformance/workflow/`, `fixtures/conformance/provenance/`, and `fixtures/conformance/bundles/`
- `docs/adr/0017-canonical-json-profile-correction.md`
- `docs/gates/HARDENING-01-CANONICAL-JSON.md`

## Remaining Risks

- The implementation uses the .NET shortest-roundtrip binary64 formatter as its digit source, then applies ECMAScript notation thresholds. Appendix B is fully covered; broader randomized cross-runtime differential testing remains useful test-strategy work.
- Validated digest-envelope rehydration is intentionally deferred to Hardening Phase 2.
- All pre-correction digests are provisional and invalid under `nexus-jcs-nfc-v1`; no automatic migration path is provided.

## ADR And Compatibility Impact

ADR 0017 records the profile correction and supersedes the misleading unqualified profile portions of ADR 0002. This gate makes no PHP, blueprint, package, persistence, or cross-version compatibility claim.
