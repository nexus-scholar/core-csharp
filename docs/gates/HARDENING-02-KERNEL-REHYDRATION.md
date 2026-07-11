# Hardening 02: Kernel verified rehydration

Status: accepted for implementation

## Goal

Provide a fail-closed Kernel boundary that converts an untrusted canonical digest-envelope representation into verified domain state only after validating its closed shape, algorithm, profile, expected scope, expected schema identity, and recomputed digest.

Scientific behavior delivered: persisted or exchanged digest metadata cannot become authoritative merely because its strings are well formed; the exact canonical record must reproduce the expected digest under the caller-selected contract.

## Sources

1. `AGENTS.md`
2. `docs/adr/0002-canonical-json-and-digests.md`
3. `docs/adr/0017-canonical-json-profile-correction.md`
4. `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`
5. Current Kernel implementation and conformance fixtures

## Dependency-Ordered Tasks

1. Kernel owner: make default `ContentDigest`, `DigestAlgorithm`, `DigestScope`, and `EntityId<TTag>` values observably invalid and reject them at authority-bearing constructors.
2. Kernel owner: define a verified digest-envelope result that cannot be instantiated without validation.
3. Kernel owner: implement rehydration from `JsonElement` with exact root-field closure and caller-provided expected scope/schema/version/digest.
4. Kernel owner: rebuild canonical content, recompute the envelope digest, and return verified state only on exact equality.
5. Test owner: add focused positive, tamper, wrong-contract, unknown-field, malformed-content, duplicate-field, and default-value regressions.
6. Fixture owner: add metadata-bearing positive and negative Kernel rehydration fixtures without changing existing golden outputs.
7. Gate owner: run focused, full, architecture, conformance, formatting, repository, and hosted CI verification.

## Required Cases

- valid noncanonical transport JSON rehydrates to the expected canonical envelope and digest;
- wrong digest, scope, schema ID, schema version, algorithm, or profile is rejected;
- missing, duplicate, unknown, null, or wrongly typed root fields are rejected;
- non-object content is rejected;
- tampered nested content is rejected by digest recomputation;
- default `ContentDigest`, `DigestAlgorithm`, and `DigestScope` cannot render or enter verification;
- `EntityId<TTag>.New` rejects a generator that returns `Guid.Empty`;
- explicit entity-ID construction rejects `Guid.Empty`;
- verified state exposes the reconstructed immutable envelope and recomputed digest only.

## Allowed Paths

- `src/NexusScholar.Kernel/ContentDigest.cs`
- `src/NexusScholar.Kernel/DigestEnvelope.cs`
- `src/NexusScholar.Kernel/DigestTypes.cs`
- `src/NexusScholar.Kernel/KernelPrimitives.cs`
- `src/NexusScholar.Bundles/BundleVerifier.cs` (integration ordering only)
- `tests/NexusScholar.Core.Tests/DeterministicKernelTests.cs`
- `tests/NexusScholar.Core.Tests/BundleTests.cs` (default-digest integration regressions only)
- `tests/NexusScholar.Conformance.Tests/KernelFixtureTests.cs`
- `fixtures/conformance/kernel/`
- `docs/gates/HARDENING-02-KERNEL-REHYDRATION.md`
- `docs/gates/HARDENING-02-KERNEL-REHYDRATION-EVIDENCE.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`

## Excluded Paths

- Protocol, Workflow, Provenance, Bundle, Search, Deduplication, Screening, Full Text, workspace, CLI, UI, and provider implementations
- downstream record rehydration
- existing fixture digest regeneration
- PHP fixtures or compatibility claims
- production dependencies

## Risks And Decisions

- C# value-type defaults cannot be prevented syntactically; fail-closed accessors and authority-boundary validation make them unusable as valid scientific values.
- Generic envelope verification does not validate module-specific content semantics. Each downstream module still needs its own validated factory and resolvers in Phase 2.
- Bundle verification must validate digest-bearing fields before attempting manifest canonicalization so invalid defaults remain structured findings rather than escaping exceptions.
- The caller must supply expected scope and schema identity. Trusting those values from the untrusted envelope would permit record-type confusion.
- Transport whitespace and property order are non-authoritative; content is rebuilt and canonicalized before digest comparison.
- No ADR is required because ADR 0002 already requires typed scope/schema/profile binding and the hardening review explicitly requires validated rehydration.

## Verification

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

## Exit Checklist

- [x] Every required case has a permanent test or fixture.
- [x] No unverified envelope can produce a verified result.
- [x] Default Kernel value objects fail closed at authority boundaries.
- [x] Existing fixture digests remain unchanged.
- [x] Only allowed paths changed.
- [x] Focused and full verification commands pass.
- [x] Evidence records behavior, commands, totals, risks, ADR impact, and compatibility impact.

Completion evidence: `docs/gates/HARDENING-02-KERNEL-REHYDRATION-EVIDENCE.md`.
