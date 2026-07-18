# Contributing

Nexus Scholar Core welcomes early architecture, documentation, onboarding, and workflow-boundary feedback. The project is not yet a production researcher app.

## Good First Feedback

- Run the first-tester walkthrough: https://nexus-scholar-org.github.io/core-csharp/tutorials/getting-started/
- Run the local CLI path: `doctor`, `sample`, and `demo`.
- Review Core authority boundaries, especially where Search Import, Deduplication, Screening, Full Text, UI contracts, and sample-host behavior meet.
- Describe real research workflow pain points without assuming live providers, cloud sync, PDF/OCR, or production UI behavior exist today.

## Issue Routing

- First tester run: https://github.com/nexus-scholar-org/core-csharp/issues/new?template=first-tester-run.yml
- Architecture boundary review: https://github.com/nexus-scholar-org/core-csharp/issues/new?template=architecture-boundary-review.yml
- Research workflow use case: https://github.com/nexus-scholar-org/core-csharp/issues/new?template=research-workflow-use-case.yml
- Documentation confusion: https://github.com/nexus-scholar-org/core-csharp/issues/new?template=documentation-confusion.yml
- Bug report: https://github.com/nexus-scholar-org/core-csharp/issues/new?template=bug-report.yml

The issue templates use only repository labels that currently exist on GitHub. More specific labels can be added later by a maintainer.

## Pull Requests

Keep PRs narrow and cite the authority source for the change:

- accepted ADRs in `docs/adr/`;
- gate plans and evidence in `docs/gates/`;
- conformance fixtures in `fixtures/`;
- current implementation and tests.

For normal code changes, run:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- doctor
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- sample
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- demo
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Or use:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
```

```bash
bash scripts/verify.sh
```

## Non-Claims To Preserve

Do not add or imply these without an accepted ADR and explicit task:

- live provider/network behavior;
- provider SDKs, credentials, scraping, paywall bypass, or shadow-library behavior;
- persistence, API, cloud sync, or production desktop-shell behavior;
- PDF extraction or OCR;
- PHP compatibility;
- AI authority over scientific decisions;
- Core dependency on UI frameworks or app-service projections.

If a change touches scientific authority, provenance, evidence identity, compatibility claims, or dependency direction, explain the boundary in the PR.
