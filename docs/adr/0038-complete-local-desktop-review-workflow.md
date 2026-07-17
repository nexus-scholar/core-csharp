# ADR 0038: Complete Local Desktop Review Workflow (FE-08 Slices 5-9)

- Status: Accepted
- Date: 2026-07-17
- Decision owner: Nexus Scholar maintainer/manager
- Supersedes: ADR 0037 (screening authority-lifetime narrow scope only)

## Context

FE-08 introduced local desktop shell and first two scientific workflows through
Screening authority resolution only. Remaining slices now require local title/abstract
Screening, repeated correction/adjudication/handoff, local-only Full Text intake and
Full Text screening, and final reporting/export workflows in the same local,
human-authority model.

This ADR resolves the same hard boundary repeatedly reintroduced in prior attempts:
UI state, paths, labels, rows, and user session fields cannot be scientific authority.

## Decision

### Slice scope

ADR 0038 defines and admits FE-08 Slices 5, 6, 7, 8, and 9 as implementation-ready.

- Slice 5: title/abstract Screening review and handoff over a verified Screening authority
  package.
- Slice 6: repeated screening correction, adjudication, and handoff semantics over
  exact confirmation material and immutable supersession graphs.
- Slice 7: local-only Full Text intake from workspace-local files plus local Full Text
  screening and correction/adjudication over verified authority.
- Slice 8: local reporting, Bundle v2, Rapid Review verification, and export
  publication/verification from finalized authoritative records only.
- Slice 9: accessibility, recovery, visual regression, and architecture closeout.

### Authority lifetime rule (explicit supersession of ADR 0037)

ADR 0037 is superseded only for its project-revision lifetime rule.

The exact project revision check remains required whenever a new authority or
scientific mutation is authored. A verified Screening authority package is otherwise
preserved across later Screening, Full Text, and export revisions when:

- the package pointer identity is unchanged;
- the upstream FE-01 authority pointer and digest set are unchanged;
- the exact writer expected-revision contract remains satisfied at the moment of mutation.

Any change to those pointers and digests requires a fresh authority re-resolution.

### Human authority and operation model

All review and correction operations remain preview-then-confirm workflows:

- preview builds exact immutable material for the target and the current authority graph,
  including actor/role and expected superseded state;
- confirm requires the exact preview digest and validates all stale inputs;
- stale checks are fail-closed and require recovery or explicit regeneration.

All scientific control remains local and human-authorized. There is no AI, model,
provider, network, API, cloud storage, or database dependency for authority.

### Package and binding boundaries

- `NexusScholar.ResearchWorkspace` owns deterministic mutation operations with
  workspace lock, pointer-last updates, and exact revision compare-and-swap.
- `NexusScholar.Desktop.AppServices` exposes only projections and operation tokens
  for confirmation; it never carries authority objects or UI state.
- Domain project packages retain inward dependency and cannot import desktop/UI
  framework types.
- FE-08 Slice 8 behavior remains within accepted Core ADR boundaries for reporting,
  bundle, and export ledger invariants.

## Required Behavior Commitments

- Screening conduct uses verified FE-04 authority and verified authority package
  material only; legacy raw candidate-only input remains non-authoritative.
- Screenings and Full Text correction/adjudication use exact superseded-decision
  and actor-bound confirmation graphs.
- Local Full Text intake accepts local bytes only and preserves raw bytes and strict
  extraction attempt records.
- Slice 8 reporting/build-out is derived from verified report/report-slice records
  and does not treat path references as authority.
- Slice 9 closeout requires architecture proof that AppServices and ResearchWorkspace
  remain the only authority mutation path.

## Excluded Scope

- No UI/login/identity/session state is scientific authority.
- No authentication/identity-provider, provider SDK, network retrieval,
  OCR/PDF parser network fetch, AI/LLM, API, cloud, telemetry, or multi-user claims.
- No persistence claims beyond local workspace mutation guarantees.
- No PHP/blueprint/deployment/security-certification compatibility claims.

## Rejected Alternatives

- using project pointers, row ids, actor session names, or UI labels as authority;
- permitting confirm without exact preview digest equality;
- admitting stale authority revisions without recovery;
- allowing mutation from non-human actor paths.

## Compatibility and Claims

This is a local C# research-workflow ADR. It makes no external API/provider,
AI provider, cloud storage, network, database, production-security, or UI-framework
authority claim.

## Reversal Criteria

Revise this ADR if:
- any authority decision can commit without preview/confirm stale checks,
- a package remains usable after its own pointer or bound FE-01 lineage changes,
- non-human automation can finalize scientific decisions,
- or Core domain projects gain direct dependency on desktop/UI dependencies.
