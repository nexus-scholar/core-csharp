# FE-08 Remaining Slices 5-9: Complete Local Desktop Review Workflow

Status: accepted for implementation under ADR 0038.

## Researcher Outcome

- A local researcher can complete title/abstract Screening, correction/adjudication,
  local-only Full Text review, and final reporting/export verification from a desktop
  host without leaving local evidence or trusting UI state as authority.
- Exact retries are idempotent where the underlying authority transaction supports
  them; changed intent or authority requires an explicit fresh preview, with clear
  stale, invalid, and recovery-required outcomes.

## Dependency-Ordered Work

1. Slice 5: title/abstract Screening review over verified authority package.
2. Slice 6: repeated correction/adjudication/handoff on the same target flow.
3. Slice 7: local-only Full Text intake and Full Text screening over verified handoff.
4. Slice 8: reporting, Rapid Review, Bundle v2, export publication, and verification.
5. Slice 9: accessibility, recovery, native visual QA, and architecture closeout.
6. Closeout: focused conformance tests, independent review, local/hosted validation.

## Required Behavior

- Keep exact human-actor/role and confirmation-token reconstruction for all mutating
  review/Screening/Full Text/export actions.
- Re-resolve verified authority before each preview and confirm path.
- Reject stale operations when any of these change: project revision, authority generation,
  manifest pointer, decision-set digest, source/snapshot digest, criteria digest,
  policy approval digests, workflow snapshot ids, superseded decision ids, actor id/role, or preview digest.
- Preserve failure mode distinctions:
  - success,
  - validation failure,
  - stale,
  - recovery required,
  - authority unavailable.
- Preserve append-only history for Screening and Full Text mutations, including corrections
  and adjudications and their exact invalidation references.
- Keep all scientific evidence in canonical bytes and pointer manifests;
  refuse to treat display path, table row id, label, sort order, keyboard focus,
  or session state as authority.
- Slice 8 reporting binds final report generation to finalized report-slice, verification
  artifacts, and exact ledger source.

## Required Negative Tests

### Slice 5 (Title/Abstract Screening review)

1. Confirm without preview returns validation failure.
2. Preview generated on authority A and confirm against authority B returns stale.
3. Actor/role missing or not assigned by verified policy returns validation failure.
4. Preview token replayed against a different workspace target returns stale.
5. Confirm uses path/id/row text instead of verified target digest and succeeds (must fail).
6. Replaying a superseded/closed target without exact supersession graph fails.
7. Workflow governance claimed as required but unverified fails closed.
8. Missing criteria/protocol/decision-set/binding digest in preview causes rejection.
9. Non-human automation actor confirms action and succeeds (must fail).

### Slice 6 (Repeated correction / adjudication / handoff)

1. correction submitted without exact superseded-decision input.
2. stale actor change between preview and confirm.
3. superseded decision changed after preview.
4. adjudication target changed but target summary digest unchanged.
5. duplicate correction chain produced without invalidation of prior terminal decision.
6. handoff emitted with partial authority (missing rationale or policy membership).
7. mutation after authority refresh but before lock release.
8. attempt to mutate with a different expected revision when writer pointer matches none.

### Slice 7 (Local-only Full Text intake and screening)

1. remote URL input accepted for intake (must fail).
2. missing local file/path, unsupported format, or changed bytes after preview.
3. extract attempt status changed post-preview (partial/failure/unsupported transition) and still confirmed.
4. Full Text decision without verified handoff target or without include outcome.
5. extraction failure treated as include/exclude decision basis.
6. artifact digest change without artifact evidence regeneration.
7. duplicate admissions for same candidate without exact expected supersession.
8. local intake allowed only through AppServices/provided bytes while bypassing preview/lock model.

### Slice 8 (Reporting / Bundle v2 / Rapid Review / Export publication / verification)

1. report finalized without complete FE-05/Full Text terminal cases for each FE-04 include.
2. stale report source digest or missing bundle manifest.
3. conservation equations not balanced (identified, duplicates, included, reasons, exclusions).
4. report includes non-authoritative markdown or path-derived assertion.
5. Bundle v2 verification with missing, duplicate, altered, mis-scoped, or traversal entry.
6. external entry treated as embedded bytes (must fail).
7. export ledger replay with out-of-order ordinals or wrong predecessor hash.
8. export request submitted with stale project revision or stale report manifest.
9. CLI-style verification claims made by desktop UI state rather than canonical records.

### Slice 9 (closeout)

1. UI control claims scientific authority.
2. architecture reference points from Core to Avalonia/UI frameworks.
3. recovery path mutates ledger/project revision on failed publish.
4. stale preview replayed automatically without explicit refresh.
5. accessibility/keyboard order/contrast/labels regressions in the critical review/researcher actions path.
6. visual state overlaps or hides warning/stale/recovery states on supported DPIs.
7. missing architecture/provenance tests for no-UI-authority boundary.

## Measurable Exit Criteria

- Slice 5 complete: at least one end-to-end reviewed title/abstract target from preview to
  confirmed decision with fail-closed stale/invalid/recovery behavior and actor-bound preview tokens.
- Slice 6 complete: at least one correction and one adjudication path exercised end-to-end with
  exact supersession and invalidation chain persistence.
- Slice 7 complete: at least one local intake + one Full Text screening decision executed through
  verified handoff, with artifact digest and extraction-attempt evidence persisted.
- Slice 8 complete: one verified final report, one Bundle v2 manifest with strict inventory replay,
  one export publication appended to export ledger, and one round-trip verify for report/bundle/export.
- Slice 9 complete: accessibility checks pass on supported desktop states; recovery tests confirm no partial
  promotion; architecture tests prove no UI/framework dependency in Core domain packages.
- Aggregate gate pass: all focused and closeout tests pass without high-severity findings in
  independent scientific/architecture review, and no blocked or unresolved negative test
  classes remain.

## Allowed Paths

- `docs/adr/0038-complete-local-desktop-review-workflow.md`,
  `docs/gates/FE-08-REMAINING-SLICES-5-9.md`,
  approved slices 1-4 evidence, FE-06 and FE-05 references.
- focused `NexusScholar.ResearchWorkspace`, `NexusScholar.Desktop.AppServices`,
  and shared desktop review surfaces.
- protocol, workflow, reporting, bundles, export, and CLI verification surfaces
  limited to verified domain objects.
- focused fixtures, architecture checks, recovery checks, and closeout visual/accessibility checks.

## Excluded Scope

- providers, scraping, network download, AI, providers, API, database, cloud, synchronization,
  telemetry, multi-user, deployment/security, plugin runtime.
- no direct treatment of paths, UI rows, focus state, labels, or session values as scientific
  authority.
- no claims of production readiness, benchmark parity, or external certification.

## Verification

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Also run focused authority-transition tests for Slices 5-8, recovery/lock tests, architecture
and conformance tests for FE-08 closeout, native accessibility/visual checks, and independent
review checklists.

## Sources

- ADR 0036, ADR 0037, ADR 0038.
- FE-08 slices 3-4 gates and FE-06 reporting/export evidence.
- existing plan and workspace recovery references in accepted FE-06 and FE-08 artifacts.
