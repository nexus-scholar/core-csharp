# Package Topology

Status: validation-only early alpha. No package publication or signing is
enabled.

The accepted graph is machine-readable in `eng/package-topology.json`. The
current version is `0.1.0-alpha.2` and contains 24 packable libraries.

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

### Reporting, network, and analysis

- `NexusScholar.Reporting`
- `NexusScholar.Network`
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
part of the 24-package graph:

- `NexusScholar.AI`
- `NexusScholar.AppServices`
- `NexusScholar.ResearchWorkspace`
- `NexusScholar.Cli`
- `NexusScholar.UiContracts`
- `NexusScholar.Avalonia.Blocks`
- `NexusScholar.Search.Providers.Cache`
- `NexusScholar.Search.Providers.Crossref`
- `NexusScholar.Search.Providers.Live`
- `NexusScholar.Search.Providers.OpenAlex`
- `NexusScholar.Search.Providers.SemanticScholar`
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

1. packs the approved 24-project graph twice;
2. rejects missing or extra package IDs;
3. validates version, MIT license, README, and LICENSE metadata;
4. compares normalized package content digests across both packs;
5. restores the smoke application using only the generated local package source;
6. loads all 24 expected assemblies;
7. writes `artifacts/packages/package-manifest.json` with raw and normalized
   SHA-256 values.

Raw `.nupkg` SHA-256 values may differ because NuGet regenerates container
relationship and core-property metadata. The normalized digest excludes only
that container metadata and covers the nuspec, documentation, license,
assemblies, XML documentation, and other package payload entries.

The FE-09 protected-main closeout passed this 24-package gate, including
`NexusScholar.Network`. No package is published, signed, supported for
production, or advertised as broadly PHP compatible.
