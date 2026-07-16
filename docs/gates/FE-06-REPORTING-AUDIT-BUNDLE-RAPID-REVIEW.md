# FE-06: Reporting, Audit Bundle, And Rapid Review Profile

Status: active under accepted ADR 0033.

## Goal

Produce a deterministic, portable, independently verifiable local review slice
from immutable corpus authority through Full Text while adding a Rapid Review
profile that cannot weaken Core invariants.

## Sources

- ADRs 0002, 0004, 0007, 0008, 0009, 0021, 0025, 0030-0033;
- FE-01 through FE-05 completion evidence;
- the FE-06 section of the feature expansion priority plan;
- current verified snapshot, conduct, workspace, and Bundle behavior.

## Dependency-Ordered Work

1. FE-06.0: verified corpus-snapshot-to-Screening binding and snapshot-bound
   conduct creation.
2. FE-06.1: Reporting package, canonical slice/report records, conservation,
   deterministic JSON and Markdown.
3. FE-06.2: Rapid Review profile companion and verified Protocol deviations.
4. FE-06.3: Bundle v2 codec, exact embedded inventory, external refs, and verifier.
5. FE-06.4: AppServices export orchestration and ResearchWorkspace append-only
   export ledger transaction.
6. FE-06.5: independent CLI report, bundle, and export verification/status.
7. FE-06.6: fixtures, architecture/package topology, evidence, independent
   review, hosted CI, and roadmap closeout.

## Required Behavior

- final counts bind the immutable corpus snapshot and exact Screening units;
- all conservation equations and exclusion-reason totals reproduce or fail;
- every FE-04 include has exactly one terminal FE-05 case in a final report;
- invalidated/superseded records never inflate counts;
- conflicts, adjudications, corrections, invalidations, waivers, amendments,
  verified deviations, disclosures, and explicit gaps remain visible;
- Rapid Review shortcuts resolve activation, consequence, mitigation,
  artifacts, human approval, disclosure, and invalidation references;
- Bundle v2 detects missing, extra, duplicate, altered, mis-scoped, traversal,
  and foreign-generation artifacts;
- intentional external references are explicit and make self-containment false;
- export history is immutable, hash-chained, pointer-last, and does not change
  project revision;
- narrative output contains no scientific assertion absent from canonical data.

## Allowed Scope

- new `NexusScholar.Screening.CorpusSnapshots` and `NexusScholar.Reporting` projects;
- focused Protocol, Workflow, Bundles, AppServices, ResearchWorkspace, and CLI changes;
- focused unit, conformance, architecture, package-smoke, and workspace tests;
- FE-06 fixtures, ADR 0033, this gate, evidence, and roadmap status.

## Excluded Scope

- live providers, retrieval, Citation Network, dissemination, submission, OCR,
  PDF parsing, database, API, cloud, UI shell, scheduler, or plugin runtime;
- AI-authored conclusions or generated prose as scientific authority;
- PRISMA certification, PHP/blueprint conformance, signatures, encryption, or
  archive-container identity;
- implicit inference that absent Full Text means not retrieved or excluded.

## Required Negative Cases

- legacy raw-candidate Screening presented as snapshot-bound flow;
- altered snapshot membership, duplicate/missing Screening unit, stale handoff;
- hidden pending/conflict/needs-review case and incomplete FE-05 coverage;
- failed conservation or reason total and generated unsupported conclusion;
- shortcut missing consequence, mitigation, human approval, disclosure,
  activation input, mitigation artifact, or invalidation policy;
- deviation with automation, stale Protocol, wrong approval target, incomplete
  invalidation effects, or unresolved inconsistency finalized;
- Bundle missing/extra/duplicate/altered/mis-scoped/path traversal/foreign source;
- undeclared missing bytes treated as external and external entry supplied bytes;
- export stale ledger head, reordered/removed entry, partial promotion,
  unmanifested file, or source-generation drift.

## Verification

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Also run affected Protocol, Workflow, Reporting, Bundles, AppServices,
ResearchWorkspace, CLI, architecture, conformance, and package-policy checks.

## Exit Criteria

- a verified snapshot-bound local review slice projects one reproducible final report;
- final counts conserve and incomplete lineage fails closed;
- one export reopens from canonical bytes and exact inventory;
- Bundle v1 remains valid while v2 detects all required tamper classes;
- profile flexibility cannot weaken protected invariants;
- independent scientific and test reviews report no remaining blocking defects;
- all local and hosted validation passes and the branch merges.
