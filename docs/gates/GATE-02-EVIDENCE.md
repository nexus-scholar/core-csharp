# Gate 2 Evidence

Status: verified for local deterministic-kernel scope on 2026-06-26.

## Behavior Implemented

- `DigestAlgorithm` and `DigestScope` value objects for the accepted Gate 2 digest vocabulary
- canonical `ContentDigest` parsing, validation, and rendering for `sha256:<64 lowercase hex>`
- raw byte SHA-256 hashing
- canonical JSON serializer for the local `rfc8785-jcs` profile decision in `ADR 0002`
- typed `DigestEnvelope` for JSON-based scientific digests
- canonical UTC timestamp emission using `yyyy-MM-ddTHH:mm:ss.fffffffZ`
- Unicode NFC normalization with a validation mode that rejects non-NFC input
- rejection of `NaN`, `Infinity`, and `-Infinity`
- preserved omission-vs-null semantics through the explicit canonical JSON tree
- LF-only NDJSON digest support with explicit CRLF normalization opt-in

## Files Changed

- [src/NexusScholar.Kernel/DigestTypes.cs](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/src/NexusScholar.Kernel/DigestTypes.cs:1)
- [src/NexusScholar.Kernel/CanonicalTimestamp.cs](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/src/NexusScholar.Kernel/CanonicalTimestamp.cs:1)
- [src/NexusScholar.Kernel/CanonicalJson.cs](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/src/NexusScholar.Kernel/CanonicalJson.cs:1)
- [src/NexusScholar.Kernel/NdjsonCanonicalizer.cs](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/src/NexusScholar.Kernel/NdjsonCanonicalizer.cs:1)
- [src/NexusScholar.Artifacts/Artifacts.cs](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/src/NexusScholar.Artifacts/Artifacts.cs:1)
- [src/NexusScholar.Artifacts/DigestEnvelope.cs](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/src/NexusScholar.Artifacts/DigestEnvelope.cs:1)
- [tests/NexusScholar.Core.Tests/DeterministicKernelTests.cs](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/tests/NexusScholar.Core.Tests/DeterministicKernelTests.cs:1)
- [tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj:1)
- [tests/NexusScholar.Conformance.Tests/KernelFixtureTests.cs](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/tests/NexusScholar.Conformance.Tests/KernelFixtureTests.cs:1)
- [fixtures/conformance/kernel/kernel-canonical-digest.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-canonical-digest.json:1)
- [fixtures/conformance/kernel/kernel-raw-bytes-digest.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-raw-bytes-digest.json:1)
- [fixtures/conformance/kernel/kernel-ndjson-lf.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-ndjson-lf.json:1)
- [fixtures/conformance/kernel/kernel-invalid-digest-uppercase.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-invalid-digest-uppercase.json:1)
- [fixtures/conformance/kernel/kernel-invalid-digest-missing-prefix.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-invalid-digest-missing-prefix.json:1)
- [fixtures/conformance/kernel/kernel-invalid-envelope-missing-fields.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-invalid-envelope-missing-fields.json:1)
- [fixtures/conformance/kernel/kernel-invalid-nfc.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-invalid-nfc.json:1)
- [fixtures/conformance/kernel/kernel-invalid-number.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-invalid-number.json:1)
- [fixtures/conformance/kernel/kernel-invalid-timestamp.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-invalid-timestamp.json:1)
- [fixtures/conformance/kernel/kernel-ndjson-crlf-reject.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-ndjson-crlf-reject.json:1)
- [fixtures/conformance/kernel/kernel-ndjson-bom-reject.json](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-ndjson-bom-reject.json:1)
- [fixtures/conformance/kernel/kernel-ndjson-lf.ndjson](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-ndjson-lf.ndjson:1)
- [fixtures/conformance/kernel/kernel-ndjson-crlf.ndjson](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-ndjson-crlf.ndjson:1)
- [fixtures/conformance/kernel/kernel-ndjson-bom.ndjson](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel/kernel-ndjson-bom.ndjson:1)
- [docs/gates/GATE-02.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/gates/GATE-02.md:1)

## Fixture IDs

- `kernel-canonical-digest-v1`
- `kernel-raw-bytes-digest-v1`
- `kernel-ndjson-lf-v1`
- `kernel-invalid-digest-uppercase-v1`
- `kernel-invalid-digest-missing-prefix-v1`
- `kernel-invalid-envelope-missing-fields-v1`
- `kernel-invalid-nfc-v1`
- `kernel-invalid-number-v1`
- `kernel-invalid-timestamp-v1`
- `kernel-ndjson-crlf-reject-v1`
- `kernel-ndjson-bom-reject-v1`

## Commands And Outputs

### `dotnet restore NexusScholar.Core.slnx`

```text
Determining projects to restore...
All projects are up-to-date for restore.
```

### `dotnet build NexusScholar.Core.slnx -c Release --no-restore`

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

### `dotnet test NexusScholar.Core.slnx -c Release --no-build`

```text
Passed! NexusScholar.Conformance.Tests.dll - Failed: 0, Passed: 4, Skipped: 0, Total: 4
Passed! NexusScholar.Architecture.Tests.dll - Failed: 0, Passed: 3, Skipped: 0, Total: 3
Passed! NexusScholar.Core.Tests.dll - Failed: 0, Passed: 28, Skipped: 0, Total: 28
```

### `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`

```text
Exit code 0. No formatting changes required.
```

### `scripts/verify.ps1`

```text
Build succeeded.
0 Warning(s)
0 Error(s)
Passed! NexusScholar.Architecture.Tests.dll - Failed: 0, Passed: 3, Skipped: 0, Total: 3
Passed! NexusScholar.Core.Tests.dll - Failed: 0, Passed: 28, Skipped: 0, Total: 28
Passed! NexusScholar.Conformance.Tests.dll - Failed: 0, Passed: 7, Skipped: 0, Total: 7
```

## Reviewer Input Used

- `dotnet_architect`: confirmed the need for an explicit canonical JSON tree, typed digest scopes, no clock use during serialization, and no hidden protocol or bundle semantics in Gate 2. One remaining design risk stays open: `ContentDigest` is still physically located in `NexusScholar.Artifacts`.
- `test_engineer`: requested explicit proof of property ordering, nested arrays and objects, null-vs-omission separation, timestamp format, NFC normalization, non-NFC validation-mode rejection, non-finite number rejection, raw byte hashing, digest-prefix validation, NDJSON CRLF handling, and repeated-run stability. The added test suite covers those items.
- `scientific_invariant_reviewer`: flagged three integrity risks during review. All were addressed in this patch:
  - the canonical digest fixture now binds its own `envelope.content` instead of rebuilding content in code;
  - `DigestEnvelope` now deep-clones and freezes canonical content on construction;
  - decimal exponent canonicalization now lowercases and trims exponent output, with explicit decimal test coverage.

## Remaining Gate 2 Risks

- `ContentDigest` remains in `NexusScholar.Artifacts`, so the module placement is still thinner than the ideal inward kernel contract.
- `src/NexusScholar.Protocol` still uses provisional digest material and is not upgraded by this gate.
- `src/NexusScholar.Bundles` still verifies only a thin scaffold and is not elevated to bundle authority here.
- No fresh GitHub Actions matrix run was captured in this turn for Gate 2 specifically. Local verification passed, but cross-platform CI evidence still depends on a later workflow run.
- Open conflicts intentionally remain open: `CF-001`, `CF-002`, `CF-004`, `CF-005`, `CF-006`, `CF-008`, and `CF-014`.

## CF-009 Status

`CF-009` is implemented for the local deterministic-kernel behavior decided by `ADR 0002`, backed by fixed conformance fixtures and tests in this gate. The conflict register entry is already narrowed, and this change does not reopen or broaden it.

## Explicit Claims Not Made

- no blueprint conformance claim
- no PHP compatibility claim
- no protocol contract adoption
- no bundle contract adoption
- no provenance parity claim
- no AI governance parity claim
- no resolution of `CF-001`, `CF-002`, `CF-004`, `CF-005`, `CF-006`, `CF-008`, or `CF-014`
