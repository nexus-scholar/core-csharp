# Nexus Scholar Core for .NET

Nexus Scholar Core is an audit-oriented, local-first early-alpha C# research workflow kernel. Its purpose is to make research methods, human decisions, automation, evidence, amendments, deviations, and outputs reconstructable from durable records.

The pinned PHP package remains behavioral evidence for proven workflows. This repo ports observable behavior into stricter local contracts instead of translating PHP classes or app storage shapes directly.

## Current Status

The current operating plan is integrity hardening, not feature expansion. See `docs/reviews/2026-07-11-hardening-plan/README.md`.

`main` contains the local Core implementation through the first no-network Full Text slice, plus a contract-backed UI renderer prototype:

- deterministic kernel primitives, canonical JSON, and digest scopes;
- protocol drafts, decisions, approvals, amendments, waivers, deviations, and invalidation notices;
- workflow template compilation with schema closure, approval gates, waivers, and invalidation planning;
- artifact and review-bundle contracts with immutable digest-bound entries;
- append-only provenance events and local in-memory ledger behavior;
- shared scientific identity, Search traces, imported Search evidence, Deduplication, Screening, and Full Text;
- plugin capability and governed-AI proposal contracts;
- UI contracts, sample block plans, an Avalonia block renderer, and a sample visual host;
- a local CLI with `doctor`, `sample`, deterministic `demo`, and the first Research Workspace commands: `init`, `status`, `import search`, `verify`, `analyze`, `review`, and `clusters`.

The Full Text slice is local and no-network. It accepts digest-bound user-supplied or deterministic artifact bytes, records acquisition/source-attempt evidence, validates PDF/XML/text shapes, detects duplicate artifacts by raw byte digest, and keeps extraction as derived evidence bound to the raw artifact. It does not download papers, call provider APIs, parse PDFs, run OCR, persist data, expose an API, or claim PHP compatibility.

## Public Walkthrough And Feedback

- First-tester walkthrough: https://nexus-scholar.github.io/core-csharp/tutorials/getting-started/
- Contributing guide: `CONTRIBUTING.md`
- Security policy: `SECURITY.md`
- First tester run issue: https://github.com/nexus-scholar/core-csharp/issues/new?template=first-tester-run.yml
- Architecture boundary review: https://github.com/nexus-scholar/core-csharp/issues/new?template=architecture-boundary-review.yml
- Documentation confusion: https://github.com/nexus-scholar/core-csharp/issues/new?template=documentation-confusion.yml

The walkthrough and issue templates are for first-tester feedback. They do not imply production systematic-review use, live providers, persistence/API/cloud behavior, PDF/OCR, or PHP compatibility.

## Try It Locally

Prerequisite: .NET SDK for `net10.0`.

Bootstrap:

```powershell
pwsh ./scripts/bootstrap.ps1
```

```bash
bash scripts/bootstrap.sh
```

Verify the solution:

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Or run the repository verification script:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
```

```bash
bash scripts/verify.sh
```

Run the CLI smoke commands:

```powershell
dotnet run --project src/NexusScholar.Cli -- doctor
dotnet run --project src/NexusScholar.Cli -- sample
dotnet run --project src/NexusScholar.Cli -- demo
```

The `demo` command is for first-tester feedback, not researcher production use. It is local-only, deterministic, and non-authoritative: it uses embedded sample Search-import bytes, does not call live providers or provider SDKs, does not download or scrape, does not persist data, does not expose an API/cloud workflow, does not run PDF extraction/OCR, and does not claim PHP compatibility.

The local Research Workspace CLI workflow is documented in `docs/cli/RESEARCH-WORKSPACE-CLI-v0.md`. It is implemented for first-tester local inspection using researcher-supplied or generated local Search export files. It does not query live providers, scrape Google Scholar, persist to a database, expose an API/cloud workflow, run PDF/OCR, execute merge decisions, or claim PHP compatibility.

Stable `demo` summary:

```text
Nexus Scholar Core local demo
Mode: deterministic local sample
Network: none
Live providers: none
Persistence: none
Import source: scopus-csv
Imported records: 5
Search sightings: 4
Parser warnings: 2
Source digest scope: raw-artifact-bytes
Dedup raw candidates: 4
Dedup exact clusters: 1
Dedup review-required pairs: 1
Non-claims: no live providers; no provider SDKs; no persistence/API/cloud; no PDF/OCR; no PHP compatibility
```

### Run the local Research Workspace workflow

The commands below use generated local APP-01 fixture exports already in the repository. They are not real Scopus, Web of Science, or Google Scholar exports and are not scientific authority.

```powershell
Remove-Item -Recurse -Force .nexus-demo -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force .nexus-demo/workspace | Out-Null
Push-Location .nexus-demo/workspace

dotnet run --project ../../src/NexusScholar.Cli -- init --title "APP-01 demo review"
dotnet run --project ../../src/NexusScholar.Cli -- import search ../../tests/NexusScholar.AppServices.Tests/Fixtures/App01GeneratedLocalBundles/bundles/FB07-combined-app01-demo/combined_scopus_like.csv --source scopus --format csv --query-id search-001 --query "systematic review screening software"
dotnet run --project ../../src/NexusScholar.Cli -- import search ../../tests/NexusScholar.AppServices.Tests/Fixtures/App01GeneratedLocalBundles/bundles/FB07-combined-app01-demo/combined_wos_like.ris --source web-of-science --format ris --query-id search-002 --query "systematic review screening software"
dotnet run --project ../../src/NexusScholar.Cli -- import search ../../tests/NexusScholar.AppServices.Tests/Fixtures/App01GeneratedLocalBundles/bundles/FB07-combined-app01-demo/combined_scholar_style.bib --source google-scholar --format bibtex --query-id search-003 --query "systematic review screening software"
dotnet run --project ../../src/NexusScholar.Cli -- import search ../../tests/NexusScholar.AppServices.Tests/Fixtures/App01GeneratedLocalBundles/bundles/FB07-combined-app01-demo/combined_wos_like_source_specific.csv --source web-of-science --format csv --query-id search-004 --query "systematic review screening software"

dotnet run --project ../../src/NexusScholar.Cli -- verify
dotnet run --project ../../src/NexusScholar.Cli -- analyze
dotnet run --project ../../src/NexusScholar.Cli -- review
dotnet run --project ../../src/NexusScholar.Cli -- clusters
dotnet run --project ../../src/NexusScholar.Cli -- clusters exact
dotnet run --project ../../src/NexusScholar.Cli -- clusters review
dotnet run --project ../../src/NexusScholar.Cli -- clusters show dedup-candidate-0001

Pop-Location
```

The review and cluster commands are read-only. They display APP-01 merge gates but do not accept, reject, mark unresolved, or execute merge decisions. The public walkthrough mirrors this local workflow for first-tester feedback.

The combined demo bundle intentionally includes parser warnings and skipped records. `verify` surfaces those issues before `analyze`; the workflow continues so first testers can inspect warning, deduplication, and human-gate blocks.

`status` is state-aware and can be run from the workspace root or a child folder. It reports local state such as `initialized`, `imported-with-warnings`, `analyzed`, `review-ready`, or `needs-attention` without printing machine-local absolute paths.

Launch the UI sample host:

```powershell
dotnet run --project samples/NexusScholar.Avalonia.Blocks.SampleHost
```

The sample host renders non-authoritative sample `WorkspacePlan` JSON through `NexusScholar.UiContracts` and `NexusScholar.Avalonia.Blocks`. It is a visual inspection harness, not a product desktop shell.

## Project Map

- `src/NexusScholar.Kernel`: deterministic primitives, clocks, ids, canonical JSON, and digests.
- `src/NexusScholar.Protocol`: protocol lifecycle records and human approval semantics.
- `src/NexusScholar.Workflow`: deterministic workflow compilation.
- `src/NexusScholar.Artifacts`: immutable artifact identity helpers.
- `src/NexusScholar.Provenance`: append-only local provenance records.
- `src/NexusScholar.Bundles`: portable review-bundle manifests and verification.
- `src/NexusScholar.Shared`: stable scholarly identity primitives.
- `src/NexusScholar.Search`: Search traces, provider stubs, schema-closed plans, and imported evidence parsing.
- `src/NexusScholar.Deduplication`: local deduplication clusters, review candidates, representatives, and evidence preservation.
- `src/NexusScholar.Screening`: human-authorized Screening decisions, AI suggestions as proposals, conflicts, and adjudication.
- `src/NexusScholar.FullText`: local no-network acquisition, artifact evidence, source attempts, validation, duplicate artifact detection, and extraction records.
- `src/NexusScholar.Extensibility`: extension manifests and scoped capability selections.
- `src/NexusScholar.AI`: governed AI task policies and proposal acceptance contracts.
- `src/NexusScholar.UiContracts`: renderer-neutral workspace and research block contracts.
- `src/NexusScholar.Avalonia.Blocks`: Avalonia renderer controls for UI contract blocks.
- `src/NexusScholar.Cli`: local CLI entry point.
- `samples/block-plans`: non-authoritative UI sample plans.
- `samples/NexusScholar.Avalonia.Blocks.SampleHost`: sample-only Avalonia host.
- `tests`: unit, conformance, architecture, UI-contract, renderer, and sample-host tests.
- `fixtures/conformance`: local contract fixtures.
- `docs/adr`: accepted architecture decisions.
- `docs/gates`: gate plans and evidence.
- `docs/ui`: UI planning, contract, renderer, and sample-host notes.

## Authority Rules

The source-of-truth order is:

1. Approved files in `specs/`.
2. Accepted ADRs in `docs/adr/`.
3. Golden fixtures in `fixtures/`.
4. Observable behavior of the pinned PHP reference.
5. Current C# implementation.
6. Informal notes and comments.

When sources conflict, record the conflict and resolve it through an ADR or fixture-backed gate. Do not guess silently.

Core domain projects must not depend on EF Core, ASP.NET Core, UI frameworks, provider SDKs, storage SDKs, or concrete model clients. Infrastructure depends inward. `NexusScholar.UiContracts` and `NexusScholar.Avalonia.Blocks` are intentionally outside Core authority, and Core projects must not reference them.

## Non-Claims

This repository does not currently claim:

- PHP compatibility without generated PHP fixtures and semantic comparators;
- live scholarly provider access;
- HTTP download, scraping, paywall bypass, or shadow-library acquisition;
- persistence, API, web app, cloud sync, or production desktop-shell behavior;
- PDF extraction, OCR, or page/section parsing algorithms;
- AI authority over scientific decisions.

Model outputs remain proposals until an authorized human action accepts them. Drafts are not approved protocols. Paths and app rows are projections, not scientific identity.
