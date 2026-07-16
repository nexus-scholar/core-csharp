# Package Topology

Status: validation-only early alpha. No package publication is enabled.

The accepted package graph is recorded in `eng/package-topology.json`, ADR 0025,
and later accepted package-boundary ADRs. It contains eighteen domain and
evidence-contract libraries. `NexusScholar.CorpusSnapshots` and
`NexusScholar.Screening.CorpusSnapshots` were added by ADR 0033 so downstream
Reporting consumers can restore the immutable corpus authority and its
snapshot-to-Screening bridge. CLI, workspace orchestration, application
services, AI, UI contracts, Avalonia components, previews, samples, and tests
are not packages.

## Local Validation

```powershell
dotnet build NexusScholar.Core.slnx -c Release
./scripts/verify-packages.ps1
```

The verifier:

1. packs the approved graph twice;
2. rejects missing or extra package IDs;
3. validates version, MIT license, README, and LICENSE metadata;
4. compares normalized package content digests across both packs;
5. restores the smoke application using only the generated local package source;
6. loads all eighteen expected assemblies;
7. writes `artifacts/packages/package-manifest.json` with raw and normalized SHA-256 values.

Raw `.nupkg` SHA-256 values may differ because NuGet regenerates container relationship/core-property metadata. The normalized digest excludes only that container metadata and includes the nuspec, documentation, license, assemblies, XML documentation, and other package payload entries.

These packages are not production-ready, are not published, and make no PHP compatibility claim.
