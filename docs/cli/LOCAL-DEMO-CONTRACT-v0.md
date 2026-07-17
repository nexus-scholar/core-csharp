# Local CLI Demo Contract v0

Status: historical CLI-02/CLI-03 demo contract. The `demo` command remains local-only, deterministic, and non-authoritative, but this file is not the current full CLI inventory.

Current command routing lives in `README.md`, `CODEX-START-HERE.md`, and
`site/developers/modules/cli/index.html`.

## Goal

Provide a deterministic local `demo` command that lets first testers see the current Nexus Scholar value without live providers, provider SDKs, persistence, API/cloud behavior, PDF/OCR, PHP compatibility claims, or UI product-shell behavior.

The command should demonstrate:

- user-supplied imported Search evidence;
- parser warnings and source-file digest binding;
- Deduplication over imported evidence;
- review-required duplicate candidates;
- explicit non-claims.

## Command

```powershell
dotnet run --project src/NexusScholar.Cli -- demo
```

Exit code:

- `0` when the local demo completes and produces the expected summary.
- non-zero only for command parsing or unexpected local validation failure.

## CLI Baseline When This Contract Was Accepted

At contract acceptance, `src/NexusScholar.Cli/Program.cs` dispatched:

- `doctor`
- `sample`
- `demo`

Current help text is:

```text
Usage: dotnet run --project src/NexusScholar.Cli -- [doctor|sample|demo]
```

## Required Project References

`src/NexusScholar.Cli/NexusScholar.Cli.csproj` references the local Search and Deduplication projects needed to run the deterministic demo:

- `../NexusScholar.Search/NexusScholar.Search.csproj`
- `../NexusScholar.Deduplication/NexusScholar.Deduplication.csproj`

The implementation did not change `src/NexusScholar.Search/**` or `src/NexusScholar.Deduplication/**`.

## Demo Input

Use embedded deterministic bytes inside the CLI implementation. Do not read from local files in v0.

Recommended input format: `scopus-csv`.

Recommended source database/tool: `scopus-csv`.

Recommended parser metadata:

- `parser_id`: `cli-local-demo-parser`
- `parser_version`: `1.0.0`
- `imported_by`: `cli-demo-user`
- `imported_at`: `2026-06-29T00:00:00Z`
- `original_query_text`: `nexus scholar local demo`
- `exported_at`: `2026-06-29T00:00:00Z`

Recommended embedded CSV:

```csv
eid,title,author names,year,source title,doi
2-s2.0-demo-001,Evidence preserving duplicate review,Alpha One,2024,Demo Journal,10.1000/demo-duplicate
2-s2.0-demo-002,Evidence-preserving duplicate review,Alpha One,2024,Demo Journal,10.1000/demo-duplicate
2-s2.0-demo-003,Title only candidate without stable id,Beta Two,2023,Demo Journal,
2-s2.0-demo-004,Title only candidate without stable id,Beta Two,2023,Demo Journal,
2-s2.0-demo-005,,Gamma Three,2022,Demo Journal,
```

Expected import behavior:

- 5 imported records total;
- 4 Search sightings because the blank-title row is skipped;
- parser warnings include `missing-required-field` and `skipped-record`;
- source-file digest is computed over exact embedded bytes with `raw-artifact-bytes` scope;
- Scopus EIDs remain source-specific identifiers, not WorkIds.

Expected dedup behavior:

- 4 raw candidates;
- 1 exact identifier cluster from the shared DOI records;
- at least 1 review-required candidate from the title-only duplicate pair;
- parser warnings and source-file digest evidence are preserved through Deduplication;
- no Search-time deduplication claim.

## Required Output

The output should be plain text, stable, and easy to copy into an issue.

Stable required lines:

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

The implementation may print additional details after these lines, but tests should assert the stable required lines.

Optional detail lines:

```text
Skipped records: 1
Exact cluster members: 2
Unresolved candidates: 2
Demo complete: inspect issue templates for feedback
```

Do not print machine-local paths.

Do not print timestamps generated from the current clock.

Do not print network or provider availability status.

## Implementation Shape

Preferred implementation in the next task:

1. Keep `Program.cs` command dispatch small.
2. Add CLI-local class `LocalDemoCommand` or equivalent under `src/NexusScholar.Cli/`.
3. Use `SearchImportService.Parse(...)` over embedded UTF-8 bytes.
4. Use `DeduplicationService.Execute(...)` with no live `SearchTrace` inputs and one `SearchImportTrace`.
5. Format a stable stdout summary from `SearchImportTrace` and `DeduplicationResult`.
6. Return exit code `0` if all expected counts are produced.

Do not add a production dependency.

Do not add file output in v0.

Do not add command-line options in v0.

Do not add persistence.

## Test Requirements

Preferred test location:

- add `tests/NexusScholar.Cli.Tests/` only if CLI output cannot be tested cleanly from an existing test project.

Alternative:

- add focused tests to `tests/NexusScholar.Core.Tests/` only if the repo pattern strongly favors keeping CLI smoke tests there.

Minimum test cases:

1. `demo` output contains every stable required line.
2. `demo` output is identical across repeated in-process invocations of the formatter or command helper.
3. help text includes `demo`.
4. unknown command still returns non-zero and prints usage.
5. output contains non-claim text for no live providers, no provider SDKs, no persistence/API/cloud, no PDF/OCR, and no PHP compatibility.

If a new test project is added, update:

- `NexusScholar.Core.slnx`
- relevant test project references only.

## Allowed Paths For Implementation Task

- `src/NexusScholar.Cli/**`
- `tests/NexusScholar.Cli.Tests/**` if needed
- `NexusScholar.Core.slnx` if a new test project is added
- test project files only when references are required
- `README.md` only in the later CLI-03 documentation task

## Forbidden Paths For Implementation Task

- `src/NexusScholar.Search/**`
- `src/NexusScholar.Deduplication/**`
- `src/NexusScholar.Screening/**`
- `src/NexusScholar.FullText/**`
- `docs/adr/**`
- `fixtures/**`
- provider/network code
- persistence/API/cloud code
- Avalonia renderer or sample-host product behavior

## Non-Claims

The `demo` command must not claim:

- live provider search;
- provider API access;
- provider SDK support;
- Scopus API or Web of Science API support;
- Google Scholar scraping;
- PDF download, PDF extraction, or OCR;
- persistence, API, cloud sync, or production desktop-shell behavior;
- PHP compatibility;
- AI authority over decisions.

The command must explicitly say it is deterministic and local.

## Validation For Implementation Task

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
dotnet run --project src/NexusScholar.Cli -- doctor
dotnet run --project src/NexusScholar.Cli -- sample
dotnet run --project src/NexusScholar.Cli -- demo
```

Also run:

```powershell
git diff --check
rg -n "live provider|download|scrape|production|cloud|PDF extraction|OCR|PHP compatibility" README.md docs src tests
```

The search command is a review prompt, not an automatic failure. It should be inspected to ensure claims remain negative or explicitly future-scoped.
