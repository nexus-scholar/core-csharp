# Nexus Scholar C# Core UI/UX Design Spec v3

**Design direction:** JetBrains-style professional desktop shell for a local research evidence workspace.
**Implementation target:** C# core repository, not the old PHP implementation.
**Preferred UI technology:** Avalonia-style C# desktop app, sharing the same workflow/domain services as the CLI.
**CLI spine:** `init → status → import → verify → analyze → review → clusters`.

---

## 1. Updated verdict

The IntelliJ/PyCharm inspiration helps a lot.

It solves a missing product-design layer from the first spec: the app should not only be a set of command screens; it should have a familiar desktop **workspace shell**. JetBrains products are a strong reference for:

- welcome screen
- recent projects/workspaces
- new project/workspace wizard
- open existing folder flow
- left navigation
- calm professional dark UI
- project-as-folder mental model
- bottom-right primary actions in dialogs

But Nexus should not become an IDE. Nexus is a **research evidence workspace**, so every borrowed pattern must be translated into research language.

The design direction becomes:

> JetBrains-style professional desktop shell, but for local research evidence workflows instead of code projects.

---

## 2. C# Core implementation boundary

This design is for the current C# core. It should not reference, depend on, or preserve old PHP UI/product assumptions.

### 2.1 Correct technical framing

The UI should be designed as a C# desktop client over the same C# workflow model that powers the CLI:

```text
NexusScholar.Core / Kernel / Search / Deduplication / UiContracts
        ↑
Research workspace services
        ↑                 ↑
NexusScholar.Cli      NexusScholar.Desktop
```

The CLI remains the truthful workflow contract, but the UI should ideally call shared C# services, not shell out to CLI forever.

### 2.2 Recommended C# project shape

Current CLI research workspace classes live under the CLI project. For a durable Avalonia UI, move reusable workspace logic into a shared C# library and keep CLI/UI as adapters.

Recommended later structure:

```text
src/
  NexusScholar.Kernel/
  NexusScholar.Search/
  NexusScholar.Deduplication/
  NexusScholar.UiContracts/
  NexusScholar.AppServices/

  NexusScholar.ResearchWorkspace/          # new or refactored shared library
    ResearchWorkspaceProject.cs
    ResearchWorkspaceInput.cs
    ResearchWorkspacePaths.cs
    ResearchWorkspaceStore.cs
    ResearchWorkspaceVerifier.cs
    ResearchWorkspaceAnalyzer.cs
    SearchImportAliases.cs
    WorkspacePlanReader.cs
    WorkspaceReadModelService.cs
    WorkspaceCommandService.cs

  NexusScholar.Cli/                        # thin command adapter
    ResearchWorkspace/*.cs

  NexusScholar.Desktop/                    # Avalonia app shell
    App.axaml
    MainWindow.axaml
    Views/
    ViewModels/
    Services/
```

### 2.3 Short-term prototype option

For the first prototype, the desktop app can shell out to `nexus` commands if that is fastest. But treat that as a prototype adapter:

```text
Prototype: Avalonia UI → invoke CLI → parse structured files/output
Durable:   Avalonia UI → shared C# ResearchWorkspace services
```

The durable version is cleaner because it avoids fragile text parsing and keeps CLI/UI behavior consistent from the same code.

---

## 3. Product identity

### Borrow from IntelliJ/PyCharm

```text
Welcome screen
Recent workspaces
New workspace wizard
Open folder
Left project navigation
Professional dark UI
Clean modal/dialog structure
Project folder mental model
```

### Do not borrow

```text
plugins as a primary UX concept
interpreter setup
remote development
run configurations
git branch UI
tool-window overload
developer settings everywhere
```

### Nexus identity

```text
research workflow
local evidence
file verification
deduplication clusters
review queue
human decision authority
reproducible local outputs
```

---

## 4. Application information architecture

## 4.1 Windows

### WelcomeWindow

Shown when no workspace is open.

Contains:

```text
Recent Research Workspaces
New Research Workspace
Open Existing Workspace
Run Local Demo
Learn Nexus
Feedback / Report Issue
Settings
```

### WorkspaceWindow / MainWindow

Shown after a workspace is opened.

Contains:

```text
Top project bar
Left navigation
Workflow stepper
Main routed content
Bottom command/status area, optional
```

---

## 4.2 Shell layout

```text
┌──────────────────────────────────────────────────────────────────┐
│ Nexus Scholar · AI screening tools review                         │
│ Review ready · Local-only · No providers · Decisions locked        │
├───────────────────┬──────────────────────────────────────────────┤
│ Project           │ Workspace → Import → Verify → Analyze → Review│
│  Overview         ├──────────────────────────────────────────────┤
│  Imports          │                                              │
│  Verification     │ Current screen                               │
│  Analysis         │                                              │
│  Review Queue     │                                              │
│  Duplicate Clusters│                                             │
│  Reports          │                                              │
│                   │                                              │
│ Diagnostics       │                                              │
│  System Check     │                                              │
│  Core Sample      │                                              │
│  Local Demo       │                                              │
│                   │                                              │
│ Help              │                                              │
│  Tutorial         │                                              │
│  Feedback         │                                              │
└───────────────────┴──────────────────────────────────────────────┘
```

---

## 5. Visual style

## 5.1 Theme direction

Use a JetBrains-inspired dark professional theme as the default candidate, with a light theme available.

### Nexus Dark

```text
Background: near-black / charcoal
Panels: slightly lighter charcoal
Cards: muted dark surface
Borders: subtle gray
Text: high-contrast off-white
Secondary text: muted gray
Accent: calm blue
Warnings: amber
Blocking/human-review: orange
Invalid/broken: red
Verified: green
Locked/disabled: gray
```

Do not make the app look like an AI chatbot or marketing dashboard.

### Nexus Light

```text
Background: white / soft gray
Panels: white
Cards: white with border
Text: near-black
Accent: calm blue
Warnings: amber
Blocking: orange
Invalid: red
Verified: green
```

## 5.2 Typography

Use a professional desktop-app typographic hierarchy:

```text
Window title: 18–22 px
Screen title: 20–24 px
Card title: 14–16 px semibold
Body: 13–14 px
Secondary metadata: 12–13 px
Monospace: command snippets, paths, IDs, digests
```

## 5.3 Tone

The app should sound like a careful research assistant:

```text
Good:  File changed since import
Bad:   Hash mismatch

Good:  Duplicate candidates detected
Bad:   Duplicates resolved

Good:  Human merge decision required
Bad:   Merge now

Good:  Local files only. No provider queries.
Bad:   Sync complete
```

---

## 6. Welcome screen spec

**Route/window:** `WelcomeWindow`
**Inspired by:** IntelliJ/PyCharm Welcome screen
**Purpose:** Open or create a local research workspace.

### Layout

```text
┌──────────────────────────────────────────────────────┐
│ Nexus Scholar                                        │
│ Local research evidence workspace                    │
├─────────────────┬────────────────────────────────────┤
│ Workspaces      │ [ Search workspaces... ]            │
│ Learn           │                                    │
│ Feedback        │ Recent Research Workspaces          │
│ Settings        │                                    │
│                 │ AI screening tools review           │
│                 │ Review ready · 18 candidates         │
│                 │ ~/NexusWorkspaces/AI screening...    │
│                 │                                    │
│                 │ Tomato disease SLR                   │
│                 │ Imported · verify next               │
│                 │ ~/NexusWorkspaces/Tomato...          │
│                 │                                    │
│                 │ Actions                              │
│                 │ [New Workspace] [Open Workspace]     │
│                 │ [Run Local Demo] [View Tutorial]     │
└─────────────────┴────────────────────────────────────┘
```

### Recent workspace card model

```csharp
public sealed record RecentWorkspaceCardViewModel(
    string Title,
    string WorkspaceId,
    string DisplayPath,
    string FullPath,
    WorkspaceState State,
    int SearchExportCount,
    int? ReviewRequiredCandidateCount,
    int? ParserWarningCount,
    DateTimeOffset LastOpenedAt,
    bool Exists,
    bool IsValidNexusWorkspace);
```

### Display rules

- Show title first.
- Show state second.
- Show a shortened path by default.
- Full absolute path appears only in tooltip/context menu/copy action.
- Missing workspaces remain visible but marked “Folder missing.”
- Invalid project files remain visible but marked “Needs attention.”

### Actions

```text
New Workspace
Open Existing Workspace
Remove from Recent
Reveal in Folder
Copy Path
Run Local Demo
View Tutorial
Send Feedback
```

### Empty state

```text
No recent research workspaces yet.
Create a new local workspace or open an existing folder containing nexus.project.json.
```

---

## 7. New Research Workspace screen

**Window/route:** `NewWorkspaceDialog` or `WelcomeWindow/NewWorkspaceView`
**CLI equivalent:** `nexus init --title "<title>"`
**Inspired by:** PyCharm New Project wizard

### Key correction

The current C# CLI supports a generic research workspace. It does not yet support separate executable templates for “Systematic Review,” “Scoping Review,” etc. So the left template list should either have only one enabled item or clearly mark the others as presets/coming later.

### Recommended template list

```text
Evidence Review        enabled
Systematic Review      coming later
Scoping Review         coming later
Dataset Audit          coming later
Demo Workspace         local demo only
```

### Layout

```text
New Research Workspace

Left panel
  Evidence Review
  Systematic Review        later
  Scoping Review           later
  Dataset Audit            later
  Demo Workspace

Main panel
  Workspace name
  [ AI screening tools review ]

  Location
  [ ~/NexusWorkspaces/AI screening tools review ] [Browse]

  Workspace mode
  (•) Local folder only
      Uses nexus.project.json and local generated outputs.
  ( ) Advanced custom layout       disabled in v0

  What Nexus will create
  ✓ nexus.project.json
  ✓ inputs/search/
  ✓ nexus-output/imports/
  ✓ nexus-output/dedup/
  ✓ nexus-output/workspace/
  ✓ nexus-output/reports/

  Local-only guarantee
  Nexus will not query providers, upload files, create a database, or execute merge decisions.

                                      [Cancel] [Create Workspace]
```

### View model

```csharp
public sealed class CreateWorkspaceViewModel
{
    public string WorkspaceName { get; set; }
    public string Location { get; set; }
    public WorkspaceTemplate SelectedTemplate { get; set; }
    public bool IsLocalFolderOnly { get; } = true;
    public bool CanCreate { get; }
    public IReadOnlyList<ValidationMessage> ValidationMessages { get; }
    public ICommand BrowseLocationCommand { get; }
    public ICommand CreateWorkspaceCommand { get; }
    public ICommand CancelCommand { get; }
}
```

### Validation

- Workspace name required.
- Name must contain at least one letter or digit.
- Location required.
- Folder must be writable.
- If `nexus.project.json` exists, offer “Open existing workspace” instead of overwrite.
- No overwrite/replace action in v0.

### Success behavior

After creation:

```text
Workspace ready
No search exports imported yet.

Recommended next action
[Import Search Exports]
```

Open the workspace window automatically.

---

## 8. Workspace main window

**Window:** `WorkspaceWindow` / `MainWindow`
**Purpose:** Stable workspace shell.

### Top project bar

```text
AI screening tools review
Review ready · Local-only · No providers · Decisions locked
```

### Sidebar

```text
Project
  Overview
  Imports
  Verification
  Analysis
  Review Queue
  Duplicate Clusters
  Reports

Diagnostics
  System Check
  Core Sample Check
  Local Demo

Help
  Tutorial
  Feedback
```

### Sidebar rules

- Do not show developer diagnostics above researcher workflow.
- Do not expose raw block JSON as a primary navigation item.
- Disable screens that require missing generated outputs.
- Disabled screen tooltip explains the prerequisite.

Example:

```text
Duplicate Clusters disabled
Run analysis first to create nexus-output/dedup/current.deduplication-result.json.
```

---

## 9. Shared C# read models

The Avalonia UI should render from explicit read models, not raw CLI text.

### WorkspaceState

```csharp
public enum WorkspaceState
{
    Initialized,
    Imported,
    ImportedWithWarnings,
    Analyzed,
    ReviewReady,
    NeedsAttention
}
```

### WorkspaceOverviewViewModel data

```csharp
public sealed record WorkspaceOverviewReadModel(
    string ProjectTitle,
    string WorkspaceId,
    WorkspaceState State,
    ProjectLocationKind ProjectLocation,
    int SearchExports,
    int ImportTraces,
    int ParserWarnings,
    int SkippedRecords,
    int ExactDuplicateClusters,
    int ReviewRequiredCandidates,
    int BlockingMergeGates,
    bool DeduplicationResultPresent,
    bool WorkspacePlanPresent,
    bool ReviewReportPresent,
    IReadOnlyList<WorkspaceAttentionItem> AttentionItems,
    WorkspaceNextAction NextAction);
```

### Attention model

```csharp
public sealed record WorkspaceAttentionItem(
    AttentionKind Kind,
    string Title,
    string UserMessage,
    string? ProjectRelativePath,
    string? TechnicalDetail);

public enum AttentionKind
{
    MissingProject,
    UnsupportedSchema,
    MalformedProject,
    MissingInputFile,
    FileChangedSinceImport,
    MissingImportTrace,
    InvalidProjectRelativePath,
    MissingGeneratedOutput,
    MalformedWorkspacePlan
}
```

### Command result model

```csharp
public sealed record WorkspaceCommandResult(
    bool Succeeded,
    int ExitCode,
    string Summary,
    string? NextActionLabel,
    string? NextCommand,
    IReadOnlyList<WorkspaceAttentionItem> AttentionItems,
    string Stdout,
    string Stderr);
```

---

## 10. Screen specs

## 10.1 Project Overview

**CLI equivalent:** `nexus status`
**Purpose:** Compass screen.

### Layout

```text
Project Overview

State: Review ready
Local workspace · No providers · Decisions locked

[WorkflowStepper]

Recommended next action
18 duplicate candidates need human review.
[Open Review Queue]

Project health
✓ Files verified
✓ Analysis complete
! Parser warnings present
! Merge decisions locked in v0

Summary
Search exports        4
Imported records      428
Parser warnings       5
Skipped records       2
Exact clusters        31
Review candidates     18
Blocking gates        18
```

### Rules

- This is the default screen after opening a workspace.
- It should feel like JetBrains project dashboard, not a report page.
- Use state names from the C# CLI behavior.
- Use project-relative paths only in default UI.
- Advanced details can reveal full diagnostics.

---

## 10.2 Import Search Exports

**CLI equivalent:** `nexus import search <path> --source <source> --format <format>`
**Purpose:** Guided file import.

### Layout

```text
Import Search Exports

Drop files here or choose files
Supported: CSV · RIS · BibTeX

Files to import
File            Source             Format    Query ID
scopus.csv      Scopus             CSV       search-001
wos.ris         Web of Science     RIS       search-002
scholar.bib     Google Scholar     BibTeX    search-003

Optional query text
[ systematic review screening software ]

                                      [Cancel] [Import Files]
```

### C# UI services

```csharp
public interface IImportFileDialogService
{
    Task<IReadOnlyList<SelectedImportFile>> PickSearchExportFilesAsync();
}

public interface IWorkspaceCommandService
{
    Task<WorkspaceCommandResult> ImportSearchAsync(
        WorkspaceHandle workspace,
        SearchImportCommandRequest request,
        CancellationToken cancellationToken);
}
```

### Behavior

- Infer format from extension.
- Suggest source from filename.
- Autogenerate `search-001`, `search-002`, etc.
- Warn on duplicate query IDs.
- Do not offer reimport/replace unless the C# core supports it later.

---

## 10.3 Workspace Verification

**CLI equivalent:** `nexus verify`
**Purpose:** Trust and integrity pre-flight.

### Layout

```text
Workspace Verification
Status: Valid

File integrity
✓ 4 files unchanged
✓ 0 missing files
✓ 0 file changes since import

Parser result
✓ 428 records imported
! 5 parser warnings
! 2 skipped records

Generated state
✓ Import traces present

[Run Analysis]
```

### Changed-file state

```text
Status: Needs attention

File changed since import
inputs/search/search-001-scopus.csv

Nexus records file digests at import time. The current file bytes no longer match the imported record.

Recommended actions
Restore the original file, or re-import intentionally when replacement is supported.

[Copy Diagnostic Info]
```

### Wording rule

Main UI says:

```text
File changed since import
```

Advanced drawer may say:

```text
Digest mismatch
Expected sha256:...
Actual sha256:...
```

---

## 10.4 Analyze Evidence

**CLI equivalent:** `nexus analyze`
**Purpose:** Run deterministic local analysis.

### Layout before run

```text
Analyze imported evidence

Nexus will run deterministic local analysis over your imported search exports.

This creates:
✓ Deduplication result
✓ Workspace review plan
✓ Review report

No files will be uploaded.
No provider queries will run.
No merge decisions will be executed.

[Run Analysis]
```

### Layout after run

```text
Analysis complete
Mode: Review

Findings
31 exact duplicate clusters
18 duplicate candidates need review
5 parser warnings
2 skipped records

Generated files
nexus-output/dedup/current.deduplication-result.json
nexus-output/workspace/current.workspace-plan.json
nexus-output/reports/review.md

[Open Review Queue] [Open Duplicate Clusters] [Open Report]
```

### Important copy rule

Never say:

```text
Duplicates resolved
```

Say:

```text
Duplicate candidates detected
Human review required
```

---

## 10.5 Review Queue

**CLI equivalent:** `nexus review`
**Purpose:** Task inbox for research attention.

### Layout

```text
Review Queue
4 blocking · 4 review required · 5 warnings

[All] [Blocking] [Review Required] [Warnings]

BLOCKING
Human merge decision required
Rayyan — a web and mobile app for systematic reviews
Possible duplicate pair needs review.
Why: title similarity crossed review threshold.
Sources: Scopus + Web of Science
[View comparison]

WARNINGS
Import warning: source-specific identifier
3 records include provider-specific identifiers.
[View records]
```

### Card model

```csharp
public sealed record ReviewTaskCardReadModel(
    string BlockId,
    string Kind,
    string Title,
    string Summary,
    ReviewTaskSeverity Severity,
    string WhyThisAppears,
    IReadOnlyList<string> SourceLabels,
    int? AffectedRecordCount,
    IReadOnlyList<EvidenceRefReadModel> EvidenceRefs,
    ReviewTaskAction PrimaryAction,
    IReadOnlyList<LockedDecisionAction> LockedActions);
```

### Design rule

The Review Queue is where product value becomes obvious. Do not display raw block payload first. Translate block data into user meaning.

---

## 10.6 Duplicate Clusters

**CLI equivalents:**

```text
nexus clusters
nexus clusters exact
nexus clusters review
nexus clusters show <id>
```

### Dashboard layout

```text
Duplicate Clusters

Summary
Exact clusters      31
Need review         18

Tabs
[Exact clusters] [Needs review]

Search
[ title, DOI, source, candidate ID... ]
```

### Exact cluster row

```text
cluster-0001
Rayyan — a web and mobile app for systematic reviews
3 records · ExactIdentifier · Scopus + WoS + Semantic Scholar
[Open cluster]
```

### Review candidate row

```text
dedup-candidate-0001
Rayyan web and mobile app  ↔  Rayyan web application
Title similarity: 0.91 · Threshold: 0.85 · Human review required
[Compare]
```

### Rule

Do not include an “Unresolved/no ID” tab unless the current C# read model exposes that count.

---

## 10.7 Duplicate Detail / Record Comparison

**CLI equivalent:** `nexus clusters show <id>`
**Purpose:** Most polished evidence screen.

### Layout

```text
Duplicate comparison
Status: Human review required

Left record                         Right record
Rayyan — web and mobile app          Rayyan: web mobile application
2016                                 2016
Scopus                               Web of Science
DOI present                          DOI missing

Why Nexus flagged this
✓ Title similarity crossed threshold
! Identifier missing on one side
✓ Different sources

Similarity
Title similarity: 0.91
Threshold: 0.85

Evidence
Source file digest: sha256:...
Raw record digest: sha256:...
Deduplication result: dedup-...

Decision actions
[Accept merge] locked
[Reject merge] locked
[Mark unresolved] locked

Decision execution is not available in v0.
```

### C# read model

```csharp
public sealed record DuplicateComparisonReadModel(
    string DisplayId,
    DuplicateComparisonStatus Status,
    CandidateRecordDisplayModel Left,
    CandidateRecordDisplayModel Right,
    double? TitleSimilarity,
    double? Threshold,
    string ReviewReason,
    IReadOnlyList<string> ExplanationBullets,
    IReadOnlyList<EvidenceRefReadModel> EvidenceRefs,
    IReadOnlyList<LockedDecisionAction> LockedActions,
    string? RawPayloadJson);
```

### Locked action rule

Show the actions because they explain the future workflow, but keep them visually locked and non-executable.

---

## 10.8 Reports

**Data source:** `nexus-output/reports/review.md`
**Purpose:** Preview generated local analysis report.

### Layout

```text
Reports

Review report
Generated from local analysis projection.
Not Core scientific authority.

[Open Report] [Copy Report Text] [Reveal in Folder]

Preview
# Nexus workspace review report
Mode: Review
Import traces: 4
Imported records: 428
Parser warnings: 5
Skipped records: 2
Exact duplicate clusters: 31
Review-required duplicate candidates: 18
Workspace blocks: 60
```

---

## 10.9 Diagnostics screens

Keep these in a secondary section.

### System Check

```text
System Check

Runtime
✓ .NET detected
✓ OS detected

Nexus policy
Model outputs are proposals.
Approved protocols are immutable.

[Run System Check] [Copy Diagnostic Info]
```

### Core Sample Check

```text
Core Sample Check

This is a developer smoke test, not a research workspace.

✓ Protocol digest generated
✓ Workflow compiled
✓ Provenance event recorded
✓ Bundle verification passed
```

### Local Demo

```text
Local Demo

This demo uses local deterministic evidence.
It does not query providers.

[Run Demo]
```

---

## 11. Components

## 11.1 WorkspaceTopBar

```csharp
public sealed record WorkspaceTopBarModel(
    string ProjectTitle,
    WorkspaceState State,
    bool LocalOnly,
    bool NoProviders,
    bool DecisionsLocked);
```

Displays:

```text
AI screening tools review · Review ready · Local-only · No providers · Decisions locked
```

## 11.2 WorkflowStepper

Steps:

```text
Workspace → Import → Verify → Analyze → Review
```

States:

```text
Done
Current
NeedsAttention
Blocked
NotStarted
```

## 11.3 SummaryCard

Examples:

```text
Search exports: 4
Imported records: 428
Parser warnings: 5
Exact clusters: 31
Review candidates: 18
Blocking gates: 18
```

## 11.4 StatusBadge

Variants:

```text
Initialized
Imported
Imported with warnings
Analyzed
Review ready
Needs attention
Valid
Invalid
Warning
Review required
Blocking
Locked
```

## 11.5 CLIEquivalentDrawer

Every screen can show:

```text
CLI equivalent
nexus analyze
```

For cluster screens:

```text
nexus clusters
nexus clusters exact
nexus clusters review
nexus clusters show <id>
```

## 11.6 AdvancedDetailsDrawer

Contains:

```text
Workspace ID
Trace ID
Block ID
Candidate ID
Project-relative path
Source file digest
Raw record digest
Payload JSON
CLI transcript
Exit code
```

## 11.7 LockedDecisionPanel

```text
Decision actions
[Accept merge] locked
[Reject merge] locked
[Mark unresolved] locked

Decision execution is not available in v0.
```

## 11.8 EvidenceRefsPanel

Groups evidence refs by:

```text
Import source
Import record
Search trace
Deduplication result
Source file digest
Raw record digest
Validation report
```

---

## 12. Avalonia implementation notes

## 12.1 View/ViewModel mapping

```text
Views/WelcomeView.axaml                  WelcomeViewModel
Views/NewWorkspaceView.axaml             CreateWorkspaceViewModel
Views/WorkspaceShellView.axaml           WorkspaceShellViewModel
Views/OverviewView.axaml                 ProjectOverviewViewModel
Views/ImportsView.axaml                  ImportSearchExportsViewModel
Views/VerificationView.axaml             VerificationViewModel
Views/AnalysisView.axaml                 AnalyzeEvidenceViewModel
Views/ReviewQueueView.axaml              ReviewQueueViewModel
Views/DuplicateClustersView.axaml        DuplicateClustersViewModel
Views/DuplicateDetailView.axaml          DuplicateDetailViewModel
Views/ReportsView.axaml                  ReportsViewModel
Views/DiagnosticsView.axaml              DiagnosticsViewModel
```

## 12.2 Services

```csharp
public interface IWorkspaceRegistry
{
    IReadOnlyList<RecentWorkspace> GetRecentWorkspaces();
    void AddOrUpdate(WorkspaceHandle workspace);
    void Remove(string workspaceRoot);
}

public interface IWorkspaceLocator
{
    WorkspaceLocateResult LocateFrom(string folder);
}

public interface IWorkspaceReadModelService
{
    Task<WorkspaceOverviewReadModel> GetOverviewAsync(WorkspaceHandle workspace, CancellationToken ct);
    Task<ReviewQueueReadModel> GetReviewQueueAsync(WorkspaceHandle workspace, CancellationToken ct);
    Task<DuplicateClustersReadModel> GetDuplicateClustersAsync(WorkspaceHandle workspace, CancellationToken ct);
    Task<DuplicateComparisonReadModel?> GetDuplicateDetailAsync(WorkspaceHandle workspace, string id, CancellationToken ct);
}

public interface IWorkspaceCommandService
{
    Task<WorkspaceCommandResult> CreateWorkspaceAsync(CreateWorkspaceRequest request, CancellationToken ct);
    Task<WorkspaceCommandResult> ImportSearchAsync(WorkspaceHandle workspace, SearchImportCommandRequest request, CancellationToken ct);
    Task<WorkspaceCommandResult> VerifyAsync(WorkspaceHandle workspace, CancellationToken ct);
    Task<WorkspaceCommandResult> AnalyzeAsync(WorkspaceHandle workspace, CancellationToken ct);
}
```

## 12.3 Threading and UX

- Run import/analyze/verify off the UI thread.
- Show cancellable progress when safe.
- Do not cancel midway through a file write unless the underlying service is transactional.
- Refresh read models after every successful command.
- Preserve command transcript for diagnostics.

## 12.4 Local app settings

The desktop app may store UI preferences locally:

```text
recent workspaces
theme
window size
last selected route
```

Do not store scientific decisions, credentials, provider tokens, or old PHP state.

---

## 13. Release slicing

## 13.1 Preview slice aligned with current C# core

Build first:

```text
Welcome screen
New workspace
Open existing workspace
Project overview
Import search exports
Verification
Analyze evidence
Review queue
Duplicate detail
Reports preview
```

This is enough to make the UI feel like a product while staying inside current CLI/Core behavior.

## 13.2 Defer

```text
merge decision persistence
accept/reject/mark-unresolved execution
provider integrations
live search
PDF/OCR
AI/model features
cloud sync
collaboration
old PHP compatibility screens
advanced template modes
plugin system
remote development
```

---

## 14. Acceptance criteria

1. The app can create a C# core `nexus.project.json` workspace.
2. The welcome screen can open existing local workspaces.
3. The UI shows recent workspaces with state summaries.
4. The main window uses research workflow navigation, not developer IDE labels.
5. Import supports CSV/RIS/BibTeX source selection and query IDs.
6. Verification distinguishes warnings from broken file state.
7. Analyze produces/refreshes the same generated output paths as the C# CLI.
8. Review Queue shows user-language task cards, not raw block JSON.
9. Duplicate Detail shows side-by-side comparison, evidence, and locked decisions.
10. No executable merge decisions exist in the UI.
11. No provider queries, uploads, database, cloud sync, or PHP compatibility behavior exists.
12. The UI can display a CLI equivalent drawer for every workflow screen.
13. The default main workspace screens avoid full absolute path exposure.
14. Diagnostics can copy detailed info intentionally.
15. The app feels like a professional desktop research workspace, not an IDE clone.

---

## 15. Final product sentence

Nexus Scholar Desktop should feel like:

> A JetBrains-quality C# desktop workspace for local research evidence: it imports files, verifies trust, runs deterministic analysis, explains review work, and refuses to make scientific decisions without the researcher.

Not:

> A PHP-era dashboard, an IDE clone, or an AI app that magically completes a review.


---

# v3 Addendum — Generated UI guide + Zotero-style Evidence Records table

**Status:** Design-guide addition for the C# Core/Avalonia UI direction.
**Purpose:** Add concrete screen/component guidance that can be translated into Avalonia views, while keeping the current v0 boundary: local, read-only around review decisions, no providers, no PHP compatibility layer.

## 16. Navigation update

Add **Evidence Records** to the workspace navigation. This fills the gap between “I imported files” and “I inspected duplicates.” Researchers need a familiar place to browse all imported records, not only the review queue.

Recommended project navigation:

```text
Project
  Overview
  Imports
  Evidence Records
  Verification
  Analysis
  Review Queue
  Duplicate Clusters
  Reports

Diagnostics
  System Check
  Core Sample Check
  Local Demo
```

Positioning rule:

- **Imports** answers: “Which files did I import?”
- **Evidence Records** answers: “What records are now in the workspace?”
- **Review Queue** answers: “What needs attention?”
- **Duplicate Clusters** answers: “What duplicate structure did Nexus detect?”

## 17. New component: EvidenceRecordsTable

### 17.1 Product purpose

`EvidenceRecordsTable` is a Zotero-inspired, read-only bibliography/evidence browser for imported search records.

It should feel familiar to researchers who expect a reference-library table, but it should remain a Nexus component, not a citation manager clone.

Borrow from Zotero-like tools:

```text
left library/filter pane
center sortable record table
right metadata/details inspector
fast search
visible source/identifier/status columns
```

Do not borrow yet:

```text
citation formatting
PDF attachment management
notes editing
tag persistence
library sync
record editing
merge execution
```

### 17.2 Screen placement

Screen name: **Evidence Records**

Primary route:

```text
WorkspaceWindow → Project → Evidence Records
```

Secondary entry points:

```text
Import result card → View imported records
Review warning card → View affected records
Duplicate comparison → Open left/right record in records table
Cluster detail → View cluster members in records table
```

### 17.3 Layout

Use a three-pane layout:

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ Evidence Records                                                          │
│ Search: [ title, author, DOI, source record ID... ]                       │
├───────────────────┬──────────────────────────────────────┬───────────────┤
│ Library / Filters  │ Records table                         │ Inspector     │
│                   │                                      │               │
│ All records 428    │ Title | Creators | Year | Source ... │ Selected      │
│ Needs review 18    │ Rayyan — web app...                  │ record title  │
│ Exact clusters 31  │ ASReview active learning...          │ source        │
│ Warnings 5         │ ...                                  │ identifiers   │
│                   │                                      │ digests       │
│ Sources            │                                      │ raw metadata  │
│ Scopus 120         │                                      │ actions       │
│ Web of Science 96  │                                      │               │
└───────────────────┴──────────────────────────────────────┴───────────────┘
```

### 17.4 Left filter pane

Recommended filter groups:

```text
Library
  All records
  Needs duplicate review
  Exact duplicate clusters
  Parser warnings
  Skipped records

Sources
  Scopus
  Web of Science
  Google Scholar
  OpenAlex
  Semantic Scholar
  Other

Analysis state
  Not analyzed
  Analyzed
  Exact cluster member
  Review candidate
  No duplicate evidence

Metadata quality
  DOI present
  DOI missing
  Source-specific identifier only
  Year missing
  Title missing or normalized
```

### 17.5 Table columns

Minimum useful columns:

| Column | Purpose | Example |
|---|---|---|
| Status | compact icon/check/warning marker | `!`, `✓`, `🔒` |
| Title | primary human-readable record label | `Rayyan — a web and mobile app...` |
| Creators | author display string, truncated | `Ouzzani; Hammady; Fedorowicz` |
| Year | publication year | `2016` |
| Venue | journal/conference/source venue | `Systematic Reviews` |
| Source | import source/tool | `Scopus` |
| Identifier | DOI/stable ID status | `DOI present`, `DOI missing` |
| Warnings | parser/metadata warning count | `2` |
| Duplicate state | deduplication result state | `Review required`, `Exact cluster`, `No duplicate evidence` |
| Import ID | workspace input/query ID | `search-001` |

Optional advanced columns:

```text
Source record ID
Trace ID
Source file digest
Raw record digest
Candidate ID
Cluster ID
Title similarity
Threshold used
```

Advanced columns should be hidden by default and available through a **Columns** menu.

### 17.6 Row states

Recommended visual states:

```text
normal row                 imported record, no warning
yellow marker              parser/metadata warning
orange marker              duplicate review required
blue marker                analyzed / informational
red marker                 missing input or digest mismatch affects this record
gray marker                skipped or unavailable metadata
```

Important language rule:

- Use **Review required** for duplicate candidates.
- Use **Exact cluster** for detected exact duplicate groups.
- Do not say **Merged** unless a later decision-persistence ADR exists.

### 17.7 Right inspector

The inspector is read-only in v0. It should show:

```text
selected record title
creators
year
venue
source
source record ID
import ID / query ID
trace ID
identifier summary
parser warnings / notices
duplicate state
candidate or cluster link, if any
source file digest
raw record digest
projection authority note
```

Primary inspector actions:

```text
Open comparison
Open cluster
View raw metadata
Copy source digest
Copy raw digest
Show CLI equivalent
```

Disabled/future actions:

```text
Edit metadata — later
Add tag — later
Merge records — locked
Resolve warning — later
```

### 17.8 Data mapping to current C# outputs

The table should be built from the same local generated evidence used by the CLI:

```text
nexus.project.json
inputs[]
nexus-output/imports/*.import-trace.json
nexus-output/dedup/current.deduplication-result.json
nexus-output/workspace/current.workspace-plan.json
```

Suggested read model:

```csharp
public sealed record EvidenceRecordRowViewModel(
    string RecordId,
    string Title,
    string CreatorsDisplay,
    string? Year,
    string? Venue,
    string Source,
    string ImportId,
    string TraceId,
    string? SourceRecordId,
    string IdentifierDisplay,
    int WarningCount,
    string DuplicateState,
    string? CandidateId,
    string? ClusterId,
    string? SourceFileDigest,
    string? RawRecordDigest,
    bool IsSkipped,
    bool RequiresHumanReview);
```

Suggested screen view model:

```csharp
public sealed class EvidenceRecordsViewModel
{
    public ObservableCollection<EvidenceRecordRowViewModel> Records { get; }
    public ICollectionView FilteredRecords { get; }
    public EvidenceRecordRowViewModel? SelectedRecord { get; set; }

    public string SearchText { get; set; }
    public string ActiveFilter { get; set; }
    public IReadOnlyList<RecordFilterGroupViewModel> FilterGroups { get; }
    public IReadOnlyList<RecordColumnViewModel> AvailableColumns { get; }

    public ICommand OpenComparisonCommand { get; }
    public ICommand OpenClusterCommand { get; }
    public ICommand ViewRawMetadataCommand { get; }
    public ICommand CopyDigestCommand { get; }
}
```

### 17.9 Avalonia implementation notes

Recommended Avalonia mapping:

```text
Overall layout       Grid with 3 columns + GridSplitter
Filter pane          TreeView or grouped ListBox
Records table        DataGrid
Inspector            ContentControl inside ScrollViewer
Search box           TextBox bound to SearchText
Column selector      Flyout or ContextMenu
Status chips         DataGridTemplateColumn + styled Border/TextBlock
Actions              Buttons bound to commands
Advanced metadata    Expander sections
```

DataGrid behavior:

```text
CanUserResizeColumns = true
CanUserSortColumns = true
SelectionMode = Extended
IsReadOnly = true
Enable row virtualization for large imports
Persist column visibility/order in local app settings only
```

Do not write table preferences into `nexus.project.json`; UI layout settings are app preferences, not research workspace evidence.

### 17.10 Interaction rules

User interactions:

```text
single click row       select and update inspector
double click row       open comparison if candidate; otherwise open metadata inspector
search                 filters across title, creator, DOI, source record ID, trace ID
filter click           applies source/status/metadata filter
column click           sorts the table
right-click row        copy ID, copy digest, show raw metadata
```

Keyboard behavior:

```text
Up/Down                move selected row
Enter                  open comparison or detail
Ctrl/Cmd+F             focus search
Ctrl/Cmd+C             copy selected row summary
Esc                    clear search or close drawer
```

### 17.11 Empty and degraded states

No imports yet:

```text
No evidence records yet.
Import search exports to populate this table.
[Import Search Exports]
```

Imported but not analyzed:

```text
Records imported.
Run analysis to populate duplicate states.
[Run Analysis]
```

Digest mismatch / needs attention:

```text
Some records are linked to a file that changed since import.
Restore the original file or re-import intentionally before relying on analysis.
```

Large workspace:

```text
Showing 1–500 of 12,430 records.
Use search or filters to narrow the table.
```

### 17.12 Acceptance criteria

- User can browse all imported records in a single table.
- User can filter by source, warning state, duplicate state, and metadata quality.
- User can select a row and see record metadata, evidence refs, digests, and duplicate state in the inspector.
- User can open a duplicate comparison from a review-required record.
- User can view raw metadata through progressive disclosure.
- The table is read-only and does not imply metadata edits, merge execution, cloud sync, or provider queries.
- The component does not write absolute machine paths into generated project state.
- Column order/visibility, if persisted, is stored only in local UI settings.

## 18. Generated guide artifacts

This spec is accompanied by two implementation-guide artifacts:

```text
nexus-ui-guide.html
nexus-ui-react-guide.jsx
nexus-ui-react-guide.css
```

Use them as visual and structural guides for Avalonia views. They are not production web code and they intentionally use mock data.

Recommended translation priority:

```text
1. App shell, sidebar, topbar
2. Project Overview
3. Import Search Exports
4. Evidence Records table
5. Review Queue
6. Duplicate Detail
7. Duplicate Clusters
```
