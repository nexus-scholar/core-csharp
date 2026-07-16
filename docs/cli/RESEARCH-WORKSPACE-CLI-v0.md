# Research Workspace CLI v0

Status: Implemented v0

Date: 2026-07-01

This document records the first implemented researcher-facing Nexus CLI workflow. It is a local first-tester workflow, not production systematic-review conduct. The commands are implemented through local folder state and generated or researcher-supplied local files.

## User Story

A Nexus research project is a local folder.

The CLI initializes that folder, imports local researcher-supplied evidence exports, verifies those local inputs, analyzes imported Search and Deduplication evidence, and shows read-only review queues.

```bash
nexus init --title "AI screening tools review"
nexus status
nexus import search ./exports/scopus.csv --source scopus --format csv
nexus verify
nexus analyze
nexus review
nexus clusters
nexus clusters exact
nexus clusters review
nexus clusters show <id>
```

Implemented through PR02-PR06:

- `nexus init` / `nexus status`
- `nexus import search`
- `nexus verify`
- `nexus analyze`
- `nexus review`
- `nexus clusters`, `clusters exact`, `clusters review`, and `clusters show <id>`

The workflow is designed for first-tester feedback and local evidence inspection. It is not production systematic-review conduct, live provider access, a database, a cloud service, a PDF/OCR pipeline, an AI workflow, or a PHP compatibility claim.

## Folder Layout

The project root is the folder where `nexus.project.json` lives.

```text
research-project/
  nexus.project.json
  inputs/
    search/
      search-001-scopus.csv
      search-002-wos.ris
  nexus-output/
    imports/
      search-001.import-trace.json
      search-002.import-trace.json
    dedup/
      current.deduplication-result.json
    workspace/
      current.workspace-plan.json
    reports/
```

Rules:

- `inputs/` is researcher-owned. Nexus records references and digests for files there, but does not treat paths as scientific identity.
- `nexus-output/` is generated and rebuildable. Generated records should be reproducible from `nexus.project.json` plus the referenced input bytes.
- Generated state must use relative paths from the project root. Absolute machine paths such as `C:\...`, `/Users/...`, or `/tmp/...` must not be written into generated project state.
- Raw local exports remain local user-supplied inputs. The CLI must not download, scrape, query live providers, or require provider credentials.

## Project File Shape

`nexus.project.json` is the local project index. It is not a database and not canonical scientific state.

```json
{
  "schema": "nexus.project.v0",
  "workspaceId": "workspace-ai-screening-tools-review",
  "title": "AI screening tools review",
  "createdAt": "2026-07-01T00:00:00Z",
  "inputs": [
    {
      "id": "search-001",
      "kind": "search-export",
      "source": "scopus",
      "format": "scopus-csv",
      "path": "inputs/search/search-001-scopus.csv",
      "sha256": "sha256:example"
    }
  ],
  "outputs": {
    "workspacePlan": "nexus-output/workspace/current.workspace-plan.json"
  },
  "nonClaims": [
    "local-folder-project",
    "no-live-providers",
    "no-cloud-sync",
    "no-database"
  ]
}
```

The project file should contain enough information to verify local input existence and digest consistency. It should not contain credentials, absolute paths, live provider responses, database connection strings, model outputs, or user merge decisions.

## Command Contract

### `nexus init`

Creates `nexus.project.json`, `inputs/search/`, and `nexus-output/`.

Expected behavior:

- exits `0` when a new project is created;
- exits non-zero if `nexus.project.json` already exists unless a later explicit overwrite flag is accepted;
- writes only local project scaffolding;
- does not create a database or cloud state.

### `nexus status`

Reports the nearest Nexus project from the current folder or a child folder and summarizes local workflow state.

Expected behavior:

- exits `0` for initialized, imported, analyzed, and review-ready workspaces;
- exits `2` when no `nexus.project.json` exists in the current folder or its parents;
- exits `3` when an imported input digest no longer matches the project index;
- reports one of `initialized`, `imported`, `imported-with-warnings`, `analyzed`, `review-ready`, or `needs-attention`;
- lists local Search exports, parser warnings, skipped records, generated output presence, and review-block counts;
- reports `Project location: current folder` or `Project location: parent workspace` without printing machine-local absolute paths;
- does not mutate project state.

### `nexus import search`

Imports a local Search export into generated Nexus output.

Expected shape:

```bash
nexus import search ./exports/scopus.csv --source scopus --format csv --query-id search-001 --query "systematic review screening software"
```

Accepted initial format aliases:

- `csv` maps to current `scopus-csv` parsing behavior;
- `ris` maps to current RIS parsing behavior;
- `bibtex` maps to current BibTeX parsing behavior.

Expected behavior:

- copies or records the input under project-relative `inputs/search/`;
- computes and records a digest of the input bytes;
- runs the local `SearchImportService`;
- writes an import trace under `nexus-output/imports/`;
- records parser warnings without failing unless the file is unsupported or unreadable;
- does not query live providers.

### `nexus verify`

Checks the local project index against local files and generated outputs.

Expected behavior:

- exits `0` when referenced inputs exist and digests match;
- exits non-zero for missing files, digest mismatches, malformed project state, or unsupported schema;
- does not repair or rewrite files in the first version.

### `nexus analyze`

Runs deterministic local analysis over the imported Search evidence.

Expected behavior:

- runs Deduplication over imported Search evidence;
- composes a `WorkspacePlan` through `NexusScholar.AppServices`;
- writes `nexus-output/dedup/current.deduplication-result.json`;
- writes `nexus-output/workspace/current.workspace-plan.json`;
- every generated APP-01 block uses `BlockSourceKind.AppProjection`;
- does not execute block actions or mutate Core scientific records.

### `nexus review`

Shows a read-only review queue from `current.workspace-plan.json`.

Expected behavior:

- lists warning, review-required, and blocking blocks;
- displays human merge gates as required actions;
- does not accept, reject, mark unresolved, or otherwise execute merge commands.

### `nexus clusters`

Shows read-only Deduplication cluster information from generated analysis output.

Expected behavior:

- `nexus clusters` summarizes known clusters;
- `nexus clusters exact` lists exact duplicate clusters;
- `nexus clusters review` lists clusters or candidate pairs requiring review;
- `nexus clusters show <id>` displays one cluster or review candidate;
- no command in v0 finalizes a merge decision.

## APP-01 Action Rule

APP-01 merge actions remain placeholders.

The CLI may display action labels from `WorkspacePlan` blocks, but it must not execute `AcceptMerge`, `RejectMerge`, `MarkUnresolved`, Screening include/exclude, workflow continuation, or report/bundle/export creation commands in this CLI version.

Human decisions require a later accepted boundary for actor identity, persistence/provenance semantics, and mutation rules.

## Persisted Review Artifact Verification

ADR 0033 adds four read-only commands over exports already committed by the
AppServices and ResearchWorkspace authority boundary:

```text
nexus report verify <export-id>
nexus bundle verify <export-id>
nexus export verify <export-id>
nexus export status
```

`report verify` reopens the canonical report and slice envelopes and checks
their ledger-bound digests. `bundle verify` rehydrates Bundle v2 and compares
the exact persisted inventory. `export verify` replays the complete ledger and
selects one immutable export. `export status` reports the verified history and
head.

These commands do not accept protocol ids, workflow ids, generation ids,
digest text, actor text, role text, or report counts from the caller. They do
not create reports, bundles, exports, human decisions, or scientific authority.
Report verification explicitly states that full source-authority replay was not
performed.

## Exit Codes

Use a small, predictable exit-code convention:

```text
0  success
1  validation or command usage failure
2  missing project or missing input
3  digest mismatch
4  unsupported schema or import format
5  unexpected runtime failure
```

Command output should be stable enough for first-tester transcripts and CLI tests. Error output should name the failed file or command, but must not print machine-local absolute paths when a project-relative path is available.

## Happy Path Transcript

```bash
mkdir "AI screening tools review"
cd "AI screening tools review"

nexus init --title "AI screening tools review"
nexus status

nexus import search ../exports/scopus.csv --source scopus --format csv --query-id search-001 --query "systematic review screening software"
nexus import search ../exports/wos.ris --source web-of-science --format ris --query-id search-002 --query "systematic review screening software"
nexus import search ../exports/openalex.ris --source openalex --format ris --query-id search-003 --query "systematic review screening software"

nexus verify
nexus analyze
nexus review
nexus clusters
nexus clusters exact
nexus clusters review
nexus clusters show dedup-candidate-0001
```

For public first-tester demos, use the generated local APP-01 bundle in:

```text
tests/NexusScholar.AppServices.Tests/Fixtures/App01GeneratedLocalBundles/bundles/FB07-combined-app01-demo/
```

Those files are deterministic local fixtures. They are not Scopus exports, not Web of Science exports, not Google Scholar scrapes, not scientific authority, not conformance fixtures, and not PHP compatibility fixtures.

The combined demo bundle intentionally includes parser warnings and skipped records. `nexus verify` surfaces those issues before `nexus analyze`; the workflow continues for first-tester inspection of warning, deduplication, and human-gate blocks.

Expected researcher understanding:

```text
I created a local project folder.
I imported local search/export files.
Nexus verified the files and parser output.
Nexus analyzed deduplication evidence.
Nexus showed me which records require human review.
Nexus did not query live providers or execute merge decisions.
```

## Non-goals

This CLI v0 does not add:

- live provider search;
- scraping or bulk Google Scholar collection;
- provider credentials;
- PDF download, PDF extraction, or OCR;
- AI/model calls;
- cloud sync;
- API or web server behavior;
- database persistence;
- merge accept/reject command execution;
- Screening include/exclude decisions;
- PHP compatibility claims.

## Documentation Fixtures

Documentation-only fixtures for this proposal live under:

```text
docs/cli/fixtures/PR01-docs-research-workspace-cli-v0/
docs/cli/fixtures/PR07-public-cli-workflow-tutorial/
```

They are examples for reviewers and future tests. They are not conformance fixtures, not scientific Core authority, and not PHP compatibility evidence.
