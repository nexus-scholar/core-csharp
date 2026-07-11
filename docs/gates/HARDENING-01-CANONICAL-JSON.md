# Hardening 01: Canonical JSON foundation

Status: accepted for implementation

## Goal

Correct the deterministic Kernel so every accepted numeric input has one RFC 8785-compatible IEEE 754 serialization, while preserving the explicit Nexus NFC preprocessing rule under the honest `nexus-jcs-nfc-v1` profile.

Scientific behavior delivered: content digests bind a versioned canonicalization profile whose number rendering is cross-language reproducible and whose additional Unicode normalization rule is explicit.

## Sources

1. `AGENTS.md`
2. `docs/adr/0002-canonical-json-and-digests.md`
3. `docs/adr/0017-canonical-json-profile-correction.md`
4. RFC 8785 sections 3.1, 3.2.2.3, and Appendix B
5. `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`
6. Current Kernel implementation and Gate 2 fixtures

## Dependency-Ordered Tasks

1. Kernel owner: replace decimal-first parsing and ad hoc exponent cleanup with one finite binary64 canonical-number path.
2. Kernel owner: implement ECMAScript decimal/scientific notation thresholds and negative-zero handling.
3. Conformance owner: add RFC 8785 Appendix B vectors plus direct and parse/recanonicalize regressions.
4. Kernel owner: change the profile identifier to `nexus-jcs-nfc-v1`.
5. Fixture owner: regenerate the dependency-closed local fixture values affected by the profile correction: Kernel, Workflow, Provenance event, and positive Bundle manifest digests.
6. Gate owner: run focused, full, architecture, conformance, format, and repository verification; record evidence.

## Required Cases

- RFC 8785 Appendix B finite-number vectors.
- `1e-6` serializes as `0.000001`.
- `1e20` serializes as `100000000000000000000`.
- parsed `333333333.33333329` serializes as `333333333.3333333`.
- `1e-7` and `1e21` use scientific notation.
- positive and negative zero serialize as `0`.
- non-finite values and out-of-range parsed tokens are rejected.
- direct construction and parse/recanonicalize produce identical output for the same binary64 value.
- the profile identifier and affected digest fixture use `nexus-jcs-nfc-v1`.

## Allowed Paths

- `src/NexusScholar.Kernel/CanonicalJson.cs`
- `tests/NexusScholar.Core.Tests/DeterministicKernelTests.cs`
- `tests/NexusScholar.Conformance.Tests/KernelFixtureTests.cs`
- `fixtures/conformance/kernel/`
- `fixtures/conformance/workflow/`
- `fixtures/conformance/provenance/provenance-event-*.json`
- `fixtures/conformance/bundles/bundle-manifest-*.json`
- `fixtures/conformance/bundles/bundle-roundtrip-local-equivalence.json`
- `docs/adr/0017-canonical-json-profile-correction.md`
- `docs/gates/HARDENING-01-CANONICAL-JSON.md`
- `docs/gates/HARDENING-01-CANONICAL-JSON-EVIDENCE.md`

## Excluded Paths

- Protocol, Workflow, Provenance, Bundle, Search, Deduplication, Screening, Full Text, workspace, CLI, UI, and provider implementations
- PHP fixtures or compatibility claims
- production dependencies
- persisted-record rehydration
- fixture changes outside the dependency-closed profile migration set above

## Risks And Decisions

- Corrected digests are intentionally incompatible with provisional early-alpha digests; ADR 0017 records the decision.
- .NET shortest-roundtrip formatting must not be assumed sufficient by itself; official vectors are the acceptance oracle.
- Exact decimals outside binary64 semantics must be modeled as strings rather than silently retaining a non-JCS decimal path.
- NFC preprocessing is not pure JCS; the versioned Nexus profile prevents an interoperability overclaim.

## Verification

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter DeterministicKernelTests
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter KernelFixtureTests
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
```

## Exit Checklist

- [x] All required cases have permanent tests or fixtures.
- [x] Every finite Appendix B vector matches RFC 8785.
- [x] Direct and parsed numeric paths agree.
- [x] No serializer output claims the old `rfc8785-jcs` profile.
- [x] Only allowed paths changed for implementation.
- [x] Focused and full verification commands pass.
- [x] Evidence records commands, test totals, risks, ADR impact, and compatibility impact.

Completion evidence: `docs/gates/HARDENING-01-CANONICAL-JSON-EVIDENCE.md`.
