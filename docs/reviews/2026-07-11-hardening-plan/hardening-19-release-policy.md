# Hardening 19 - Release Policy Foundation

Status: complete.

## Delivered

- accepted ADR 0024 for package topology, MIT licensing, Semantic Versioning, reproducibility, signing, SBOM, provenance, and publication controls;
- added the repository MIT license, matching the established `nexus-scholar/core` PHP package convention;
- defaulted all 18 source projects to `IsPackable=false`;
- added shared license and repository package metadata;
- pinned the SDK exactly to `10.0.301` with prerelease and feature-band roll-forward disabled;
- enabled deterministic continuous-integration build metadata;
- added an executable release-policy verifier to the normal repository gate;
- recorded the six dependency-ordered Phase 6 gates.

## Invariants

- no source project emits a package without an explicit accepted package gate;
- no package publication is authorized by this gate;
- release versions are explicit SemVer values and never clock-derived;
- unsigned validation artifacts are not releases;
- publication workflows cannot receive secrets from pull requests;
- licensing this software does not certify rights to scholarly content processed by users.

## Verification

- release-policy verifier: 18 source projects non-packable, SDK and MIT metadata pinned;
- build: passed with zero warnings;
- tests: 539 passed;
- formatting: passed;
- diff whitespace check: passed.

## ADR And Compatibility Impact

ADR 0024 is accepted. No scientific behavior, PHP fixture, or compatibility claim changed.
