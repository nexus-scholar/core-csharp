# Nexus Scholar Core for .NET

Nexus Scholar is an audit-oriented, local-first research workflow system. It is
designed so that protocols, human decisions, automation, evidence, amendments,
deviations, invalidations, and exports can be reconstructed from durable
records.

This repository is an **early alpha**. It is suitable for contract development,
controlled local evaluation, and first-tester feedback. It is not yet a
production systematic-review platform.

The pinned PHP package is behavioral evidence for selected proven workflows.
The C# implementation ports observable behavior into stricter authority and
reproducibility contracts; it does not translate Laravel structure and does not
claim broad PHP compatibility.

## Current Status

The last pre-release protected-main baseline is `425e9bc` (PR #71). The
`0.1.0-alpha.2` release commit is identified by the immutable
`v0.1.0-alpha.2` tag and by `desktop-distribution-manifest.json`, which records
the exact full commit.
The FE-09 implementation closeout evidence is historically anchored at
`ea665eb` and should be treated as historical baseline evidence.
FE-09 and the remaining FE-08 desktop slices landed through
[`PR #69`](https://github.com/nexus-scholar-org/core-csharp/pull/69), with
protected-main closeout recorded in
[`docs/release/FE-09-COMPLETION-EVIDENCE.md`](docs/release/FE-09-COMPLETION-EVIDENCE.md).
The Astro Pages migration and public FE-09 baseline landed through
[`PR #70`](https://github.com/nexus-scholar-org/core-csharp/pull/70). The
post-FE-09 integrity repairs landed through
[`PR #71`](https://github.com/nexus-scholar-org/core-csharp/pull/71).
Post-FE-09 integrity remediation is governed by
[`ADR 0044`](docs/adr/0044-fe09-deep-review-integrity-remediation.md) with
branch evidence in
[`docs/release/FE-09-DEEP-REVIEW-REMEDIATION.md`](docs/release/FE-09-DEEP-REVIEW-REMEDIATION.md).
The successor whole-project review repairs are governed by
[`ADR 0045`](docs/adr/0045-post-fe09-whole-project-integrity-remediation.md),
with branch completion evidence in
[`docs/release/POST-FE09-WHOLE-PROJECT-INTEGRITY-REMEDIATION.md`](docs/release/POST-FE09-WHOLE-PROJECT-INTEGRITY-REMEDIATION.md).

The historical FE-09 closeout baseline is:

- FE-01 through FE-09 complete within their accepted scopes;
- FE-08 desktop slices 1 through 9 complete;
- 1,011 tests passing with zero failures and two opt-in live-provider smokes
  skipped by default;
- 24 validation-only packages reproducibly packed and clean-source smoke tested;
- Release build and formatting checks passing on Windows and Linux;
- no NuGet package published.

The release-readiness gate under
[`ADR 0046`](docs/adr/0046-windows-technical-preview-distribution-and-recovery.md)
adds a self-contained Windows x64 portable artifact, verified workspace
backup/restore, sanitized local crash diagnostics, native desktop acceptance,
and tag-only GitHub prerelease automation. The desktop remains an unsigned
technical preview. The 24 NuGet packages remain validation-only and unpublished.

FE-10 plugin-runtime design and capability security is the next gate. Existing
Extensibility contracts do not authorize third-party execution or constitute an
arbitrary-code sandbox. FE-11 governed AI and FE-12 connected operation remain
dependency-ordered future work.

The active roadmap is
[`docs/plans/2026-07-14-feature-expansion-priority.md`](docs/plans/2026-07-14-feature-expansion-priority.md).

RR-01 through RR-06 are one release gate. Completion is valid only when the
local gate, protected-main checks, matching tag, attestation, downloadable
assets, and post-download checksums all agree.

## What Is Implemented

| Gate | Implemented local capability |
| --- | --- |
| FE-01 | Verified decision and immutable corpus-snapshot authority |
| FE-02 | Previewed and confirmed human Deduplication decisions with durable generations, invalidation, and provenance |
| FE-03 | Protocol-bound workflow execution journal with validated state transitions and replay |
| FE-04 | Title/abstract Screening conduct, conflicts, adjudication, invalidation, and authorized handoff |
| FE-05 | Digest-bound local full-text intake, extraction evidence, full-text decisions, and immutable workspace generations |
| FE-06 | Deterministic review reporting, portable audit bundles, immutable export ledger, and governed Rapid Review profile |
| FE-07 | Structured Extraction, human Appraisal, and deterministic Synthesis records |
| FE-08.1-2 | Windows-first local desktop host for open, initialize, import, verify, and analyze workflows |
| FE-08.3 | First desktop scientific action: authority-checked FE-02 Deduplication review |
| FE-08.4 | Durable, fail-closed Screening authority resolution and read-only desktop readiness |
| FE-08.5-9 | Desktop Screening conduct and resolution, local Full Text review, reporting, Bundle v2, export verification, recovery, and accessibility closeout |
| FE-09 | Recorded Crossref parsing, bounded OpenAlex/Semantic Scholar Search, policy-specific provider caching, recorded Full Text retrieval verification, and immutable direct-citation snapshots |

The workflow model has two distinct layers:

```text
approved Protocol + Workflow template
                  |
                  v
       immutable compiled DAG
                  |
                  v
     FE-03 execution state machine
                  |
                  v
 decisions + snapshots + provenance + exports
```

The compiled Workflow DAG defines permitted work and dependencies. The
WorkflowExecution journal records actual state transitions. Domain modules
create authoritative scientific records; UI state never does.

## Local Persistence Model

A Nexus research project is a local folder. Nexus now performs durable local
file persistence; statements that the product has “no persistence” are obsolete.
The distinction is:

- **Implemented:** project-relative inputs, generated projections, immutable
  authority generations, canonical decision/snapshot records, provenance,
  invalidation records, and append-only export ledgers;
- **Not implemented:** database storage, server API, cloud synchronization,
  authentication, multi-user tenancy, or hosted collaboration.

`nexus.project.json` is a local project index and pointer surface. It is not a
database and is not scientific authority by itself. Scientific identity comes
from validated records, stable identifiers, content digests, and verified
lineage. Paths remain references, not identities.

## Try It Locally

### Windows technical preview

The admitted end-user artifact is the unsigned, self-contained
`NexusScholar-Desktop-0.1.0-alpha.2-win-x64.zip` on the
[`v0.1.0-alpha.2` prerelease](https://github.com/nexus-scholar-org/core-csharp/releases/tag/v0.1.0-alpha.2).
It does not require an installed .NET SDK or runtime.

Download the ZIP, `SHA256SUMS.txt`, distribution manifest, and SPDX SBOM into
one folder. Verify the ZIP digest against `SHA256SUMS.txt`; GitHub CLI users can
also verify build provenance:

```powershell
Get-FileHash .\NexusScholar-Desktop-0.1.0-alpha.2-win-x64.zip -Algorithm SHA256
gh attestation verify .\NexusScholar-Desktop-0.1.0-alpha.2-win-x64.zip `
  --repo nexus-scholar-org/core-csharp
Expand-Archive .\NexusScholar-Desktop-0.1.0-alpha.2-win-x64.zip .\NexusScholar
.\NexusScholar\NexusScholar-Desktop-0.1.0-alpha.2-win-x64\NexusScholar.Desktop.exe
```

Windows may warn because the executable is not Authenticode signed. Checksums
and GitHub attestation bind workflow output; they do not establish a signed
Windows publisher identity.

The desktop can create a manifest-verified backup of an open workspace and
restore it into a new, non-existing folder. Restore never overwrites or merges
an existing workspace. Sanitized crash reports stay local under
`%LOCALAPPDATA%\NexusScholar\diagnostics`; there is no telemetry or crash upload.

The technical preview is not a production, compliance, accessibility-certified,
authenticated, multi-user, cloud-sync, installer, updater, PDF/OCR, plugin, or
AI product.

### Repository development

Prerequisites are the SDK pinned by [`global.json`](global.json) and the .NET 8
runtime used by the repository-pinned SBOM tool. Repository builds use the
pinned .NET 10 SDK.

Bootstrap:

```powershell
pwsh ./scripts/bootstrap.ps1
```

```bash
bash scripts/bootstrap.sh
```

Verify the repository:

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Or run the full repository verification:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
```

```bash
bash scripts/verify.sh
```

Run the local desktop:

```powershell
dotnet run --project src/NexusScholar.Desktop/NexusScholar.Desktop.csproj -c Release
```

Run the deterministic command-line samples:

```powershell
dotnet run --project src/NexusScholar.Cli -- doctor
dotnet run --project src/NexusScholar.Cli -- sample
dotnet run --project src/NexusScholar.Cli -- demo
```

`demo` uses embedded deterministic bytes. It does not contact providers,
download papers, persist a research project, or create scientific authority.

## CLI Surface

Invoke commands during development as:

```powershell
dotnet run --project src/NexusScholar.Cli -- <command>
```

| Command | Current behavior |
| --- | --- |
| `doctor` | Reports runtime and core policy |
| `sample` | Builds an in-memory Protocol, Workflow, Provenance event, and verified Bundle sample |
| `demo` | Runs a deterministic, local, non-authoritative Search/Deduplication demonstration |
| `init --title "<title>"` | Initializes a local Research Workspace |
| `status` | Finds and reports the nearest workspace and its current local state |
| `import search <path> --source <source> --format <format>` | Imports a researcher-supplied local CSV, RIS, or BibTeX export |
| `verify` | Verifies project-relative inputs, digests, schemas, and generated state |
| `analyze` | Runs deterministic local Search/Deduplication analysis and projections |
| `review` | Displays the review queue |
| `clusters [exact\|review\|show <id>]` | Inspects Deduplication clusters and review candidates |
| `dedup decide ...` | Previews an actor/role-bound FE-02 decision; `--confirm` commits the admitted action through the durable authority transaction |
| `screening status` | Checks persisted Screening conduct manifest/artifact integrity; it explicitly does not replay source authority |
| `report verify <export-id>` | Verifies canonical report and review-slice bytes against export-ledger bindings |
| `bundle verify <export-id>` | Rehydrates and verifies the exact Bundle v2 inventory |
| `export verify <export-id>` | Verifies one immutable export against the replayed ledger |
| `export status` | Replays and reports the complete export ledger |

`dedup decide` requires `--target`, `--action`, `--reason`, `--actor`, and
`--role`; optional inputs include `--rationale` and `--supersedes`. Without
`--confirm`, it is preview-only.

The complete first-tester workflow and exit-code contract are documented in
[`docs/cli/RESEARCH-WORKSPACE-CLI-v0.md`](docs/cli/RESEARCH-WORKSPACE-CLI-v0.md).
Some sections of that historical v0 document describe the original read-only
lane; the table above reflects the current CLI dispatch surface.

## Project Map

The repository contains 37 source projects, 2 sample hosts, and 21 test
projects. Only the 24 projects listed in
[`docs/release/PACKAGES.md`](docs/release/PACKAGES.md) participate in the
validation-only package graph.

### Foundations and immutable evidence

- `NexusScholar.Kernel`: identifiers, clocks, canonical JSON, digests, and
  deterministic primitives.
- `NexusScholar.Shared`: stable scholarly identity and normalization primitives.
- `NexusScholar.Artifacts`: immutable artifact identities and digest-bound
  references.
- `NexusScholar.Provenance`: append-only agents, activities, entities, and
  research events.
- `NexusScholar.Bundles`: portable review-bundle manifests, inventory checks,
  authority resolution, and tamper verification.

### Protocol, workflow, and execution

- `NexusScholar.Protocol`: drafts, structured decisions, approvals, immutable
  versions, amendments, waivers, deviations, and invalidation.
- `NexusScholar.Workflow`: deterministic compilation of protocol-bound workflow
  templates into immutable DAG definitions.
- `NexusScholar.WorkflowExecution`: validated execution instances, node state
  transitions, readiness, replay, invalidation, and supersession.
- `NexusScholar.WorkflowExecution.Provenance`: provenance adapter for workflow
  execution events.
- `NexusScholar.WorkflowExecution.ScientificRecords`: completion-evidence bridge
  from Extraction, Appraisal, and Synthesis records into workflow execution.

### Evidence acquisition and review conduct

- `NexusScholar.Search`: Search plans/traces, local import parsers,
  provider-neutral acquisition evidence, and rights-aware cache policy.
- `NexusScholar.Search.Providers.*`: outward recorded Crossref,
  OpenAlex/Semantic Scholar adapters, exact-host live transport, credential
  resolution, and private filesystem cache storage.
- `NexusScholar.Deduplication`: deterministic clusters, review candidates,
  representatives, evidence, and human decisions.
- `NexusScholar.CorpusSnapshots`: immutable corpus membership, decision-set
  bindings, supersession, and invalidation planning.
- `NexusScholar.Screening`: human title/abstract Screening decisions, conflicts,
  adjudication, criteria, and AI suggestions that remain proposals.
- `NexusScholar.Screening.CorpusSnapshots`: verified bridge from corpus
  snapshots into Screening authority.
- `NexusScholar.Screening.WorkflowExecution`: verified bridge between Screening
  conduct and workflow tasks.
- `NexusScholar.FullText`: local intake and recorded retrieval evidence, exact
  raw artifacts, rights/redirect/byte validation, source attempts, and derived
  extraction records.
- `NexusScholar.Screening.FullText`: full-text Screening decisions, corrections,
  independent review, handoff, and invalidation.

### Reporting and evidence analysis

- `NexusScholar.Reporting`: deterministic review slices, flow accounting,
  Rapid Review deviations, and report records.
- `NexusScholar.Extraction`: structured, protocol-bound study extraction records
  and correction lineage.
- `NexusScholar.Appraisal`: versioned appraisal instruments, complete human
  judgments, rationale, and correction lineage.
- `NexusScholar.Synthesis`: deterministic synthesis plans and outputs bound to
  accepted Extraction and Appraisal evidence.
- `NexusScholar.Network`: corpus-bound direct-citation snapshots, stable nodes,
  unresolved targets, evidence-backed edges, and deterministic degree metrics.

### Extension and model policy contracts

- `NexusScholar.Extensibility`: extension manifests and capability-selection
  contracts. The FE-10 plugin runtime is not implemented.
- `NexusScholar.AI`: governed task-policy and immutable proposal contracts.
  Live model execution and proposal acceptance remain deferred to FE-11.

### Application and product surfaces

- `NexusScholar.AppServices`: use-case composition, reporting/export commands,
  and framework-neutral application boundaries.
- `NexusScholar.ResearchWorkspace`: local project discovery, durable
  transactions, authority generations, verification, recovery, projections,
  and export-ledger orchestration.
- `NexusScholar.UiContracts`: renderer-neutral workspace plans, research blocks,
  evidence references, validation references, and action descriptors.
- `NexusScholar.Avalonia.Blocks`: reusable Avalonia renderers for UI contracts.
- `NexusScholar.Desktop.AppServices`: desktop-safe open/init/import/verify/analyze,
  Deduplication and Screening conduct, local Full Text review, reporting, and
  export facades.
- `NexusScholar.Desktop`: Windows-first local Avalonia product host and
  composition root.
- `NexusScholar.Cli`: local command-line host.

### Samples and tests

- `samples/NexusScholar.Avalonia.Blocks.SampleHost`: non-authoritative renderer
  inspection harness.
- `samples/NexusScholar.Desktop.Preview`: historical read-only Research
  Workspace preview.
- `samples/block-plans`: illustrative, non-authoritative UI contract plans.
- `tests/NexusScholar.Core.Tests`, `NexusScholar.Conformance.Tests`, and
  `NexusScholar.Architecture.Tests`: domain, fixture, invariant, and dependency
  coverage.
- `tests/NexusScholar.AppServices.Tests`,
  `NexusScholar.ResearchWorkspace.Tests`, and `NexusScholar.Cli.Tests`:
  application, durable workspace, recovery, and command coverage.
- `tests/NexusScholar.UiContracts.Tests`,
  `NexusScholar.Avalonia.Blocks.Tests`,
  `NexusScholar.Avalonia.Blocks.SampleHost.Tests`,
  `NexusScholar.Desktop.Preview.Tests`,
  `NexusScholar.Desktop.AppServices.Tests`, and
  `NexusScholar.Desktop.Tests`: contract, renderer, facade, host, and visual
  behavior coverage.
- Provider, cache, Full Text retrieval, and Network projects have focused suites
  under `tests/NexusScholar.Search.Providers.*`,
  `tests/NexusScholar.FullText.Retrieval.Tests`, and
  `tests/NexusScholar.Network.Tests`.
- `tests/NexusScholar.PackageSmoke`: clean local-source loading for all 24
  validation packages.

## Authority Rules

The source-of-truth order is:

1. approved files in `specs/`;
2. accepted ADRs in `docs/adr/`;
3. golden fixtures in `fixtures/`;
4. observable behavior of the pinned PHP reference;
5. current C# implementation;
6. informal notes and comments.

When sources conflict, record the conflict and resolve it through an ADR or
fixture-backed gate. Do not guess silently.

Core domain projects do not depend on EF Core, ASP.NET Core, UI frameworks,
provider SDKs, storage SDKs, or concrete model clients. Infrastructure depends
inward. Desktop and CLI surfaces may invoke accepted, authority-checked
application commands; they do not create scientific authority from UI state.

## Public Walkthrough and Feedback

- [First-tester walkthrough](https://nexus.mouadh.org/tutorials/getting-started/)
- [Contributing guide](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [First tester run](https://github.com/nexus-scholar-org/core-csharp/issues/new?template=first-tester-run.yml)
- [Architecture boundary review](https://github.com/nexus-scholar-org/core-csharp/issues/new?template=architecture-boundary-review.yml)
- [Documentation confusion](https://github.com/nexus-scholar-org/core-csharp/issues/new?template=documentation-confusion.yml)

## Non-Claims

This repository does not currently claim:

- production readiness, clinical or regulatory fitness, or completed
  accessibility certification;
- broad PHP package compatibility beyond explicitly inventoried,
  fixture-backed cases;
- live Crossref or Full Text retrieval, unrestricted provider retention,
  provider completeness/parity, scraping, paywall bypass, or shadow-library
  acquisition;
- live citation acquisition, centrality, impact interpretation, or citation
  export;
- built-in PDF parsing, OCR, or page/section extraction algorithms;
- database persistence, server API, web application, cloud sync,
  authentication, tenancy, or multi-user collaboration;
- a working plugin runtime or safe arbitrary-code sandbox;
- live AI/model execution or AI authority over scientific decisions;
- published, signed, or supported NuGet packages.

Model outputs remain proposals. Drafts are not approved protocols. UI state,
database rows, and file paths are not scientific identity.
