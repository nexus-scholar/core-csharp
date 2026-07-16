# ADR 0035: Local Product Desktop Shell And Command Facade

- Status: Accepted
- Date: 2026-07-16
- Decision owner: Nexus Scholar maintainer/manager

## Context

ADR 0016 authorized a sample desktop preview and deliberately prohibited a
product host, durable settings, initialization, import, and scientific
decisions. FE-01 through FE-07 are now complete. The FE-08 roadmap requires a
usable local operator shell whose UI state never becomes scientific authority.

The existing preview reads `NexusScholar.ResearchWorkspace` directly. The
roadmap instead requires desktop projections and mutations to pass through an
application-service boundary. `NexusScholar.ResearchWorkspace` already depends
on `NexusScholar.AppServices`, so placing a workspace facade in that existing
package would create a dependency cycle or require a broad unrelated refactor.

## Decision

### Product host

Create `src/NexusScholar.Desktop` as the Windows-first Avalonia product host.
It owns process startup, window composition, file/folder pickers, and rendering.
It is not Core authority and contains no domain or workspace mutation rules.
The host may use the official version-aligned Avalonia Fluent theme package for
control templates; this adds no network, storage, or scientific behavior.

The existing `samples/NexusScholar.Desktop.Preview` remains a sample and keeps
its current behavior for compatibility. It is not silently renamed or promoted.

### Desktop application-service boundary

Create `src/NexusScholar.Desktop.AppServices` as the FE-08 command facade. It
may reference `NexusScholar.ResearchWorkspace` and `NexusScholar.UiContracts`.
The product host may reference this facade and renderer packages. Core domain
projects must not reference either desktop project.

This focused bridge is the FE-08.1 AppServices owner. It resolves the current
dependency direction without moving ResearchWorkspace orchestration into the
Avalonia host or reversing established package dependencies.

### Admitted slices

Slices 1 and 2 admit only:

1. open an existing local workspace;
2. verify an existing local workspace;
3. analyze imported local evidence;
4. initialize a new local workspace folder;
5. import one researcher-selected local Search export.

Supported import sources are Scopus, Web of Science, Google Scholar, OpenAlex,
Semantic Scholar, and Other. Supported formats are CSV, RIS, and BibTeX.

Deduplication decisions, Screening decisions, Full Text decisions, report
publication, export publication, protocol actions, and every other scientific
mutation remain unavailable in these slices.

### Command lifecycle

Every admitted write command has an immutable preview. The preview binds:

- command kind and normalized request;
- absolute operator-selected source or target path for execution only;
- workspace id and expected project revision when a workspace exists;
- source byte digest for import;
- expected effects and explicit non-claims;
- one deterministic confirmation token over the preview material.

Execution accepts the exact preview, revalidates its token, reopens the current
workspace, and rejects stale revision, changed source bytes, changed target
state, active authority generation, or lock contention. A preview is not
scientific authority and is never written into workspace authority records.

Open and verify are read operations. Analyze, initialize, and import use the
existing ResearchWorkspace transaction and atomic-write boundaries. The facade
returns structured success, attention, failure, stale, and recovery-required
states; the desktop must never infer success from absence of an exception.

Shared analysis artifacts use neutral local-workspace parser/importer identity
and report wording. They must not claim that the desktop invoked the CLI.

### Actor boundary

These slices admit operational workspace actions only and no scientific
decision. The UI must state that no scientific actor or role is active and that
scientific actions are unavailable. A later FE-08 gate must define actor/role
selection and bind it to every admitted scientific command before those commands
can be enabled.

### Settings and telemetry

Slices 1 and 2 add no durable UI settings, recent-workspace store, telemetry,
network calls, or crash upload. No UI state is stored in `nexus.project.json`.

## Rejected Alternatives

### Put the facade in the Avalonia host

Rejected because workflow rules, stale checks, and result classification would
become renderer behavior.

### Make `NexusScholar.AppServices` depend on ResearchWorkspace

Rejected for these slices because ResearchWorkspace already uses AppServices;
reversing that boundary would require a broad extraction unrelated to the
researcher-visible outcome.

### Execute CLI commands from the desktop

Rejected because console text and exit-code parsing are not a structured
application contract and would duplicate process, path, and error handling.

## Consequences

- the desktop is a product host but remains non-authoritative;
- CLI and desktop share ResearchWorkspace operations rather than duplicating
  import behavior;
- local paths remain execution references, not scientific identifiers;
- import traces continue to bind source bytes, parser identity, import source,
  timestamp, warnings, and skipped records;
- live providers, PDF/OCR, AI, database, API, cloud, plugins, and PHP
  compatibility remain outside FE-08 slices 1 and 2.

## Reversal Conditions

Changing product-host placement, storing settings in a research workspace,
admitting scientific decisions, or bypassing preview/stale checks requires a new
accepted ADR or an explicit superseding FE-08 gate.
