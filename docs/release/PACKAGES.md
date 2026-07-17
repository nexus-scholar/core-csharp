# Package Topology

Status: validation-only early alpha. No package publication or signing is
enabled.

The accepted graph is machine-readable in `eng/package-topology.json`. The
current version is `0.1.0-alpha.2` and contains 23 packable libraries.

## Packable Libraries

### Foundations and authority

- `NexusScholar.Kernel`
- `NexusScholar.Shared`
- `NexusScholar.Artifacts`
- `NexusScholar.Protocol`
- `NexusScholar.Workflow`
- `NexusScholar.Provenance`
- `NexusScholar.Bundles`

### Search, review, and immutable handoffs

- `NexusScholar.Search`
- `NexusScholar.Deduplication`
- `NexusScholar.CorpusSnapshots`
- `NexusScholar.Screening`
- `NexusScholar.Screening.CorpusSnapshots`
- `NexusScholar.FullText`
- `NexusScholar.Screening.FullText`

### Workflow execution

- `NexusScholar.WorkflowExecution`
- `NexusScholar.WorkflowExecution.Provenance`
- `NexusScholar.WorkflowExecution.ScientificRecords`
- `NexusScholar.Screening.WorkflowExecution`

### Reporting and analysis

- `NexusScholar.Reporting`
- `NexusScholar.Extraction`
- `NexusScholar.Appraisal`
- `NexusScholar.Synthesis`

### Extension contracts

- `NexusScholar.Extensibility`

These libraries are package-boundary validation artifacts. Their presence in the
graph is not a publication, stability, support, production, or broad
compatibility claim.

## Non-Packaged Projects

Application, orchestration, executable, policy-only, and UI projects are not
part of the 23-package graph:

- `NexusScholar.AI`
- `NexusScholar.AppServices`
- `NexusScholar.ResearchWorkspace`
- `NexusScholar.Cli`
- `NexusScholar.UiContracts`
- `NexusScholar.Avalonia.Blocks`
- `NexusScholar.Desktop.AppServices`
- `NexusScholar.Desktop`
- sample hosts and all test projects

`eng/package-topology.json` is authoritative for the exact expected package IDs
and clean-source smoke roots.

## Local Validation

```powershell
dotnet build NexusScholar.Core.slnx -c Release
./scripts/verify-packages.ps1
```

The verifier:

1. packs the approved 23-project graph twice;
2. rejects missing or extra package IDs;
3. validates version, MIT license, README, and LICENSE metadata;
4. compares normalized package content digests across both packs;
5. restores the smoke application using only the generated local package source;
6. loads all 23 expected assemblies;
7. writes `artifacts/packages/package-manifest.json` with raw and normalized
   SHA-256 values.

Raw `.nupkg` SHA-256 values may differ because NuGet regenerates container
relationship and core-property metadata. The normalized digest excludes only
that container metadata and covers the nuspec, documentation, license,
assemblies, XML documentation, and other package payload entries.

The FE-08 Slice 4 repository verification passed this 23-package gate. No package
is published, signed, supported for production, or advertised as broadly PHP
compatible.
