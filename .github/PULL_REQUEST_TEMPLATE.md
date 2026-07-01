## Summary

- Behavior changed:
- Public-facing impact:

## Authority And Scope

- Source of truth used:
- ADRs/docs/fixtures affected:
- Scientific invariants preserved:

## Non-Claims Check

Confirm whether this PR changes any of these. If yes, cite the ADR or approved task that authorizes it.

- [ ] Live provider/network behavior
- [ ] HTTP download, scraping, provider SDKs, or credentials
- [ ] Scopus API, Web of Science API, Google Scholar scraping, paywall bypass, or shadow-library behavior
- [ ] Persistence, API, cloud sync, or production desktop shell behavior
- [ ] PDF extraction or OCR
- [ ] PHP compatibility or generated PHP fixtures
- [ ] Core dependency on UI frameworks
- [ ] AI authority over scientific decisions

## Tests Run

Paste exact commands and results.

```text
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- doctor
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- sample
dotnet run --project src/NexusScholar.Cli --configuration Release --no-build -- demo
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

## Expected / Actual For User-Facing Changes

- Expected:
- Actual:

## Review Notes

- Risks:
- Follow-up work:
