# Hardening 21 - Locked Restore and Release Evidence

Status: complete.

## Scope

- pin all 30 solution restore graphs with `packages.lock.json` and require locked mode in CI;
- pin the SBOM generator as a repository-local .NET tool;
- retain package checksums and normalized reproducibility digests;
- generate and validate an SPDX SBOM for the validation artifact set;
- bind commit, timestamp, SDK, topology, restore inputs, tool version, and artifacts in a release-evidence manifest.

## Invariants

- CI cannot silently update a dependency graph;
- the out-of-solution package smoke regenerates its ephemeral lock because NuGet container hashes are intentionally not reproducible;
- release evidence is generated from the exact pinned SDK and package topology;
- the SBOM timestamp and namespace derive from the source commit, not wall-clock time;
- unsigned artifacts remain validation-only under ADR 0024;
- generated artifacts stay outside source control and contain no publication credential.

## Verification

Run `scripts/verify.ps1`. The release evidence is emitted under `artifacts/release/` and includes `release-evidence.json`, `package-manifest.json`, packages, and `_manifest/spdx_2.2/manifest.spdx.json`.

This gate makes no publication, production, audit-grade, or PHP compatibility claim.
