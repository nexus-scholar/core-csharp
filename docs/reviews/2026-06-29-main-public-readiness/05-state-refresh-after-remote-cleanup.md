# State Refresh After Remote Cleanup

Date: 2026-06-29

## Current Git State

Local status:

```text
## main...origin/main
```

Current local branches:

```text
main
gh-pages
```

Current remote branches:

```text
origin/main
origin/gh-pages
```

`git ls-remote --heads origin`:

```text
53d7aa429471faf65ea6b94c3febd1015c1e94a1 refs/heads/gh-pages
ebb7bba6131c27a5608a63d302de555b26db849e refs/heads/main
```

## Current Main Head

```text
ebb7bba docs: refresh readmes after main consolidation
5a13abc Implement local full text evidence slice
16cabc3 Merge pull request #4 from nexus-scholar/cdx/ui-phase-3-5-avalonia-sample-host
```

## Remote Branches Deleted

- `cdx/gate-9-fulltext-contract`
- `cdx/gate-9-fulltext-recon`
- `cdx/ui-phase-3-5-avalonia-sample-host`
- `cdx/ui-phase-3-avalonia-renderer`

## Verification

Local:

```text
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
```

Result: passed, 318 tests.

Hosted:

```text
gate-01 run 28380516236
Commit: ebb7bba
Result: passed on ubuntu-latest and windows-latest
```

## Current Review Verdict

The repository is clean enough for public architecture and developer feedback. It is not yet ready as a general researcher product.

The next useful work is not more branch cleanup. It is public onboarding:

- replace the placeholder getting-started tutorial;
- add issue templates;
- add sample-host screenshot or GIF;
- publish a narrow first-feedback request;
- refresh maintainer routing docs that still point at Gate 0.
