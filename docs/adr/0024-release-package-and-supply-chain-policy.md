# ADR 0024: Release, Package, and Supply-Chain Policy

- Status: Accepted
- Date: 2026-07-13

## Context

The repository is an audit-oriented early alpha. Before Phase 6, every source project inherited the SDK's default packability despite there being no accepted package topology, license file, versioning policy, package validation, signing identity, SBOM, or release workflow. Publishing those assemblies would create an unsupported public contract.

The established `nexus-scholar/core` PHP package uses the MIT license. The C# repository has no conflicting license declaration.

## Decision

### License

The repository uses the MIT license. Package metadata uses `MIT` as its SPDX expression. This software-distribution license does not certify rights to third-party scholarly content processed by users.

### Default-Deny Packaging

`IsPackable` is false at repository level. A project may become packable only in a dedicated gate that defines its package ID and public surface, dependencies, metadata, deterministic pack and clean-install tests, signing/provenance behavior, and maturity non-claims.

Samples, tests, preview applications, and internal orchestration projects remain non-packable. No NuGet publication is authorized by this ADR.

### Versioning

Packages use Semantic Versioning. While early alpha, versions stay below `1.0.0`; minor versions may contain documented breaking changes. Tags use `vMAJOR.MINOR.PATCH[-PRERELEASE]` and must match package versions exactly. One repository release uses one version supplied explicitly by workflow or local property, never wall-clock time.

### Reproducibility

The SDK feature band is pinned exactly by `global.json`. CI enables deterministic continuous-integration builds. Restore, package, SBOM, and provenance inputs must be retained. A clean-machine job must restore, build, test, pack, install, and compare the expected artifact manifest before publication is enabled.

### Signing, SBOM, and Provenance

Unsigned local artifacts may be created for validation but are not releases. A release workflow must generate an SBOM and GitHub artifact attestation. NuGet signing remains disabled until signing identity, certificate custody, timestamp authority, rotation, and revocation procedures are documented and tested.

### Publication

Release automation is validation-only until a later accepted gate enables a package destination and protected release environment. Secrets must not be available to pull-request workflows.

## Consequences

- `dotnet pack` cannot accidentally emit source-project packages.
- Package topology can be introduced incrementally with explicit ownership and smoke tests.
- Developers need the exact pinned SDK instead of silently rolling feature bands.
- Phase 6 can build reproducible release evidence before publication.
- This ADR makes no production, audit-grade, or PHP compatibility claim.
