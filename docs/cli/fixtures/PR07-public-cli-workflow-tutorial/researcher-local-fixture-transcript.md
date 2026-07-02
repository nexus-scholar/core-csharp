# Public CLI workflow transcript - PowerShell

Run from the repository root after switching to a branch that contains the implemented Research Workspace CLI.

```powershell
$ErrorActionPreference = "Stop"

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

The combined demo bundle intentionally includes parser warnings and skipped records. `verify` surfaces those issues before `analyze`; the workflow continues for first-tester inspection of warning, deduplication, and human-gate blocks.

Expected researcher understanding:

```text
I created a local Nexus project folder.
I imported local generated fixture exports.
Nexus verified local bytes and parser output.
Nexus analyzed imported Search evidence through Deduplication and AppServices.
Nexus showed read-only review and cluster information.
Nexus did not query live providers or execute merge decisions.
```

Fixture boundary:

```text
The fixture files are generated local test data. They are not Scopus exports, not Web of Science exports, not Google Scholar scrapes, not conformance fixtures, and not scientific authority.
The review and cluster commands are read-only. They display APP-01 merge gates but do not accept, reject, mark unresolved, or execute merge decisions.
```
