# ADR 0025: Domain Package Topology

- Status: Accepted
- Date: 2026-07-13

## Context

ADR 0024 defaults every project to non-packable until a dedicated gate defines package identity, dependency closure, metadata, and clean-install validation. The repository contains domain contracts, application orchestration, CLI/workspace adapters, AI contracts, UI contracts, Avalonia components, previews, and samples. Publishing all projects would expose unstable operational surfaces as supported package contracts.

## Decision

The first validation-only package set is version `0.1.0-alpha.1` and contains twelve domain/evidence libraries:

- `NexusScholar.Kernel`
- `NexusScholar.Shared`
- `NexusScholar.Protocol`
- `NexusScholar.Workflow`
- `NexusScholar.Provenance`
- `NexusScholar.Artifacts`
- `NexusScholar.Bundles`
- `NexusScholar.Search`
- `NexusScholar.Deduplication`
- `NexusScholar.Screening`
- `NexusScholar.FullText`
- `NexusScholar.Extensibility`

The machine-readable allowlist is `eng/package-topology.json`. Every package uses the common repository version and MIT/repository/readme metadata. Project-reference dependencies become exact same-release NuGet dependencies.

The smoke roots are Bundles, Extensibility, FullText, and Screening. Together their dependency graph installs all twelve packages. A local-source-only smoke application must restore, build, run, and load all expected assemblies.

Accepted feature ADRs may extend this validation-only topology. ADRs 0030,
0031, and 0032 add the WorkflowExecution, Screening.WorkflowExecution,
WorkflowExecution.Provenance, and Screening.FullText bridge packages. At
`0.1.0-alpha.2`, the machine-readable topology therefore contains sixteen
packages. Its smoke roots are Bundles, Extensibility, Screening.FullText,
Screening.WorkflowExecution, Search, and WorkflowExecution.Provenance; their
dependency closure installs all sixteen packages. Release policy requires the
smoke project references to match those roots exactly, and the clean smoke run
loads every assembly named by the topology.

Package archives are packed twice. Raw `.nupkg` digests are recorded, but NuGet-generated ZIP relationship/core-property metadata is not byte-stable. Reproducibility therefore compares normalized content digests over the nuspec, license, readme, and package payload entries while excluding only NuGet container metadata. Hardening 21 will retain and attest the resulting artifact manifest.

`NexusScholar.AI`, `NexusScholar.AppServices`, `NexusScholar.Avalonia.Blocks`, `NexusScholar.Cli`, `NexusScholar.ResearchWorkspace`, and `NexusScholar.UiContracts` remain non-packable. Samples, previews, and tests remain non-packable.

## Consequences

- Domain contracts can be validated as a closed package graph without publishing operational adapters.
- Package consumers receive explicit early-alpha maturity and non-claim wording through the package README.
- Any package addition or removal requires updating the accepted topology, clean-install roots, and validation evidence.
- This gate creates local validation artifacts only; it does not authorize NuGet publication.
