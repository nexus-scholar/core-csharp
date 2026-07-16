# FE-06: Reporting, Audit Bundle, And Rapid Review Profile

Status: implementation complete locally under accepted ADR 0033; hosted CI and
merge verification pending.

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

## Progress Evidence

### FE-06.0: Snapshot-To-Screening Authority

Status: complete locally; independent scientific and test reviews accepted.

- `NexusScholar.CorpusSnapshots` is packable and the new packable
  `NexusScholar.Screening.CorpusSnapshots` bridge depends only on its authority
  inputs.
- `nexus.screening.corpus-binding / 1.0.0` binds the verified Deduplication
  result, snapshot record, decision set, groups, representatives, members, and
  unresolved candidates through strict canonical bytes.
- snapshot membership must cover every source candidate exactly once;
  unresolved candidates become explicit Screening units.
- only the bridge can call the internal verified-candidate-set conduct factory;
  downstream code must require `VerifiedSnapshotBoundScreeningPolicy` and a
  legacy raw-candidate policy fails verification.
- deterministic local fixtures cover valid replay, stale source, altered
  representative, duplicate/missing unit, wrong decision-set/snapshot digest,
  and noncanonical order. They make no PHP or blueprint compatibility claim.
- local verification: Release build succeeded with zero warnings/errors; 741
  tests passed; format verification passed; package verification reproduced and
  smoke-loaded all 18 approved packages.
- the test review's grouped-conservation and strict-codec gaps were corrected:
  the fixture now contains a two-member duplicate group, removal of only the
  non-representative member fails, root/nested unknown fields are isolated, and
  source-symbol architecture coverage protects the bridge boundary.

### FE-06.1: Deterministic Reporting Projection

Status: complete locally; final phase-wide review remains pending.

- packable `NexusScholar.Reporting` consumes verified Protocol, Workflow,
  Deduplication, corpus snapshot, snapshot-bound Screening, Full Text, and
  provenance authorities without persistence, provider, UI, or model access;
- `nexus.reporting.review-slice-binding / 1.0.0` records the exact authority and
  workspace-generation cut, and `nexus.reporting.review-flow-report / 1.0.0`
  records conserved counts, exclusion reasons, audit counts, disclosures, and
  explicit non-claims;
- projection counts only current conduct replay outcomes, requires one terminal
  Full Text case per title/abstract include, exposes missing cases as gaps, and
  rejects duplicate, extra, stale, mismatched, or non-terminal authorities;
- finalization enforces all six flow equations and exact reason totals;
  canonical JSON and Markdown are deterministic, and Markdown cannot introduce
  content outside structured report fields;
- focused tests cover the two-member duplicate group, complete conservation,
  incomplete Full Text coverage, duplicate cases, deterministic replay, and
  altered canonical bytes. Package topology now contains 19 libraries;
- independent scientific and test reviews identified mutable presentation
  state, missing explicit non-claims, opaque supplemental references, optional
  extraction binding, and the old PHP Dissemination wording. The implementation
  now snapshots presentation fields, requires non-claims, exactly matches
  Workflow supplemental bindings, binds extraction attempts structurally,
  includes structured report bindings, and distinguishes local review-flow
  Reporting from unclaimed PHP export parity;
- final local verification: Release build succeeded with zero warnings/errors;
  751 tests passed; format verification passed; package verification packed,
  restored, and smoke-loaded all 19 approved assemblies.

### FE-06.2: Rapid Review And Verified Deviations

Status: complete locally; final phase-wide fixture consolidation remains in
FE-06.6.

- `nexus.workflow-profile.rapid-review / 1.0.0` binds one verified Workflow,
  approved Protocol, no-default scientific-conduct activation inputs, affected
  requirements/nodes, consequence, mitigation artifacts, human approval,
  disclosure, invalidation coverage, and the closed protected-invariant set;
- `nexus.protocol.deviation / 1.0.0` is a new verified supplemental authority;
  the historical `ProtocolDeviation` note remains non-authoritative. The new
  record binds exact Protocol content, optional profile/shortcut, conduct,
  consequence, mitigation evidence, disclosure, human recorder, approval set,
  invalidation effects, and required successor amendment when applicable;
- deviation approvals target the exact canonical deviation digest, automation
  is prohibited, collections are canonical and immutable, and strict codecs
  reject altered bytes or stale digests;
- Reporting requires an explicit complete deviation set, binds profile and
  deviation digests, matches the exact Workflow template, cross-checks shortcut
  requirement/consequence/mitigation/disclosure/artifact refs, and rejects
  `unresolved_inconsistency` at finalization;
- independent reviews identified optional deviation membership, synthetic test
  authority, unnormalized refs, null collection handling, and missing template
  continuity. All were corrected with focused negative tests;
- final local verification: Release build succeeded with zero warnings/errors;
  761 tests passed; format verification passed; deterministic package
  verification packed, restored, and smoke-loaded all 19 approved assemblies.

### FE-06.3: Bundle V2 Exact Inventory

Status: complete locally; export transaction integration follows in FE-06.4.

- additive `nexus.review-bundle.manifest / 2.0.0` coexists with unchanged Bundle
  v1 and has a strict byte-only canonical decoder;
- scoped report, workspace-cut, source-generation, embedded-byte, and optional
  external expected digests are explicit; source bindings preserve optional
  Full Text candidate identity and reject foreign generations;
- ordered embedded entries require contiguous ordinals, safe unique logical
  paths, exact sizes and raw-byte digests, including an embedded canonical
  report; external entries have no path or bytes and require safe non-secret
  locators, availability notes, and stable ids;
- verification compares the exact observed ordered inventory with fixed
  `manifest.json` plus declared embedded paths, detects duplicate/missing/extra,
  altered manifest/artifact bytes, traversal, mis-scoped digests, and marks
  mixed external bundles non-self-contained;
- independent review gaps for byte-only replay, duplicate observed paths,
  scoped semantic digests, candidate/source-cut binding, local file locators,
  credential queries, and observed-path validation were corrected.

### FE-06.4: Append-Only Workspace Export Ledger

Status: complete locally; independent CLI verification follows in FE-06.5.

- AppServices prepares exports only from a verified final report, exact matching
  workspace cut, valid Bundle v2 inventory, and an identified human actor;
- ResearchWorkspace persists immutable export directories without writing or
  incrementing `nexus.project.json`, and records canonical request, report,
  Markdown, Bundle inventory, and hash-chained ledger entry bytes;
- one workspace lock covers predecessor compare-and-swap, exact source-project
  revalidation, promotion, and atomic pointer-last head publication;
- replay verifies canonical entry/head/request bytes, contiguous ordinals,
  predecessor digests, source bindings, actor kind, exact directory inventory,
  path containment, reparse-point rejection, and independently reconstructed
  Bundle inventory identity;
- faults before head publication quarantine promoted output and preserve the old
  ledger; a post-head retry is idempotent, while concurrent readers report the
  workspace lock instead of observing the promotion window;
- 11 focused tests cover verified orchestration, human authority, unchanged
  project bytes/revision, multi-entry chaining, stale writers, source drift,
  all publication fault points, idempotence, byte/extra-file tampering, empty
  heads, promotion-window replay, mismatched report authority, and an
  internally consistent false inventory digest.

### FE-06.5: Independent CLI Verification And Status

Status: complete locally; fixture consolidation and release closeout follow in
FE-06.6.

- `report verify <export-id>` reopens ledger-bound canonical report and slice
  envelopes and explicitly does not claim full source-authority replay;
- `bundle verify <export-id>` rehydrates Bundle v2 from persisted bytes and
  verifies the exact observed inventory and ledger inventory digest;
- `export verify <export-id>` requires successful full-history replay before
  reporting one immutable export, while `export status` reports verified count,
  head, and ordered history;
- callers cannot supply report counts, authority ids, generation bindings,
  digests, actors, or roles, and no CLI path creates reports or exports;
- focused CLI tests cover all valid commands, empty and populated status,
  tampered Bundle bytes, missing export identity, exact usage, and advertised
  command surface.

### FE-06.6: Release Fixtures, Review, And Validation

Status: complete locally; hosted CI and merge verification pending.

- phase-wide local conformance fixtures pin Rapid Review/deviation, Bundle v2,
  and export-ledger contracts, required negative cases, source implementation
  commits, generator command, and explicit PHP/PRISMA/blueprint non-claims;
- finalized Reporting fixtures pin canonical slice/report digests rather than
  comparing only two live serializations;
- independent architecture, scientific-invariant, conformance, and test reviews
  found crash recovery, mutable verified bytes, Bundle-manifest binding,
  report/slice linkage, structural report verification, root reparse, human
  action digest, and fixture provenance defects; each was corrected and covered
  by focused regression tests;
- durable closeout evidence is recorded in
  `docs/release/FE-06-COMPLETION-EVIDENCE.md`;
- final local validation passed with a zero-warning Release build, 790 tests,
  format verification, release policy checks, deterministic package comparison,
  and clean smoke loading of all 19 packages at `0.1.0-alpha.2`.
