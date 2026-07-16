# ADR 0033: Reporting, Audit Bundle, And Rapid Review Profile

- Status: Accepted
- Date: 2026-07-16
- Decision owner: Nexus Scholar maintainer/manager

## Context

FE-01 through FE-05 now provide verified Protocol, Deduplication, immutable
corpus snapshot, Workflow, execution, title/abstract Screening, and per-candidate
Full Text authorities. FE-06 must produce a deterministic end-to-end account
without treating current projections, paths, generated narrative, or an archive
container as scientific authority.

Three gaps block that outcome. FE-04 conduct currently binds raw Deduplication
candidates rather than the immutable corpus snapshot units, so report count
continuity is not yet proven. `ProtocolDeviation` is an in-memory human note
without canonical digest, approval membership, rehydration, or invalidation
effects. ADR 0009 Bundle `1.0.0` has no closed byte codec, exact observed
inventory, source-generation binding, or intentional external-reference shape.
ADR 0009 requires a schema-version change for those semantic changes.

## Decision

### Package boundaries

Promote the existing domain-only `NexusScholar.CorpusSnapshots` project to the
validation package topology; otherwise packable Reporting could depend on a
project that package consumers cannot restore. Create packable
`NexusScholar.Screening.CorpusSnapshots` as the outward bridge
between Deduplication, CorpusSnapshots, and Screening. It owns verified mapping
from one immutable snapshot to the exact Screening units and may create a
snapshot-bound Screening conduct policy. Existing Screening and CorpusSnapshots
remain independent.

Create packable `NexusScholar.Reporting` as a persistence-independent outward
projection package. It may depend on Kernel, Protocol, Workflow,
Deduplication, CorpusSnapshots, Screening, Screening.CorpusSnapshots, FullText,
and Screening.FullText. Those packages never depend on Reporting.

Protocol owns verified deviation authority. Workflow owns the Rapid Review
profile companion. Bundles owns Bundle v2. AppServices coordinates verified
report/export requests. ResearchWorkspace owns immutable export directories and
the append-only ledger. CLI performs independent report/bundle/export
verification; it does not invent unresolved authorities from ids or paths.

### Snapshot-to-Screening continuity

The bridge schema is:

```text
nexus.screening.corpus-binding / 1.0.0
```

It binds the canonical Deduplication result digest, corpus snapshot id and
record digest, decision-set digest, ordered Screening units, and canonical
binding digest. Each grouped unit identifies the snapshot group, representative
candidate, and every member candidate. Each unresolved unit identifies the
snapshot unresolved candidate and its candidate digest.

The bridge verifies complete, non-overlapping membership and creates a locked
Screening candidate set containing one representative per group plus each
unresolved candidate. A final FE-06 report rejects legacy FE-04 conduct that
does not bind this exact candidate-set digest. Historical FE-04 records remain
valid for their original scope but cannot support a final post-dedup flow claim.

### Report cut and count conservation

The Reporting schemas are:

```text
nexus.reporting.review-slice-binding / 1.0.0
nexus.reporting.review-flow-report / 1.0.0
```

A report slice binds the approved Protocol content digest, verified Workflow
definition digest, Deduplication result digest, corpus snapshot record digest,
snapshot-to-Screening binding digest, title/abstract conduct policy and handoff
digests, every represented Full Text admission/artifact/conduct/handoff digest,
Rapid Review profile digest when used, verified waiver/amendment/deviation
digests, provenance event digests, workspace id, project revision, and exact
source generation ids plus manifest digests.

The projector receives verified domain objects, never paths or raw ids. A final
report requires a terminal FE-04 handoff and exactly one terminal FE-05 case for
every FE-04 include. Missing cases remain explicit diagnostic gaps and cannot be
mislabelled as not sought, not retrieved, or excluded.

Counts use these equations:

```text
identified = snapshot_group_members + unresolved_snapshot_candidates
duplicates_consolidated = snapshot_group_members - snapshot_groups
post_dedup = snapshot_groups + unresolved_snapshot_candidates
post_dedup = title_abstract_included + title_abstract_excluded
title_abstract_included = full_text_included + full_text_excluded
included = full_text_included
```

Only current replay projections are counted. Invalidated or superseded
decisions never add counts. Resolved conflicts and adjudications remain visible
as audit counts and references but yield one terminal outcome. Exclusion-reason
breakdowns must sum to their stage exclusion count. Any failed equation rejects
finalization.

The canonical report contains structured bindings, counts, reason breakdowns,
conflict/adjudication/correction/invalidation counts, waiver/amendment/deviation
references, disclosures, and explicit non-claims. Deterministic JSON is the
report record. Markdown is a byte-digested presentation artifact and cannot add
conclusions absent from the canonical report.

### Rapid Review profile

The companion schema is:

```text
nexus.workflow-profile.rapid-review / 1.0.0
```

It binds one verified Workflow template and approved Protocol. Every shortcut
declares a unique id, scientific-conduct activation input, affected requirement
and node refs, consequence, mitigation, required mitigation artifacts, human
approval requirement, reporting disclosure, and invalidation policy.

All references must resolve in the bound template. Activation inputs cannot
have defaults. Approval requirements must prohibit automation. Mitigation
artifacts and invalidation policies are mandatory. Shortcuts may select or
constrain declared behavior; they cannot remove actor identity, provenance,
snapshot immutability, evidence identity, append-only history, or invalidation.
The profile record includes that closed protected-invariant set.

Planned shortcuts still require approved Protocol content or verified waiver
authority before Workflow compilation. A profile is not permission to depart
from an approved Protocol.

### Verified Protocol deviations

Add canonical schema:

```text
nexus.protocol.deviation / 1.0.0
```

The record binds deviation id, Protocol id/version/content digest, planned
requirement, optional profile/shortcut, actual conduct, rationale,
classification, consequence, mitigation applied, mitigation evidence refs,
effect, disclosure, human recorder/time, approval policy and exact approval ids,
and ordered invalidation effects naming target kind/id/digest/required action.

Add unverified and verified wrappers plus strict canonical codec and
supplemental-authority rehydration. Approval records target the exact deviation
digest. `approved_amendment_required` must resolve successor amendment
authority. `unresolved_inconsistency` blocks final reporting. Recording a
deviation never retrospectively authorizes conduct.

### Bundle v2

Retain Bundle `1.0.0` and its verifier. Add:

```text
nexus.review-bundle.manifest / 2.0.0
```

V2 has a strict canonical byte codec and required report and source-generation
bindings. Digests with semantic scope use `{scope,value}` records. Each artifact
is discriminated as `embedded` or `external`.

Embedded entries require a unique ordinal logical path, exact size, a
`raw-artifact-bytes` content digest, artifact role, and exact source binding.
External entries have no bundle path or bytes; they require stable reference id,
reference kind, non-secret locator, availability note, source binding, and an
expected scoped content digest when known. Absolute local paths, credentials,
and network retrieval are forbidden.

Verification compares the observed bundle paths with the declared embedded
paths plus the fixed manifest path. Missing, extra, duplicate, altered,
mis-scoped, traversal, foreign-generation, or structurally invalid external
entries fail with stable categories. A valid bundle containing external entries
is explicitly not self-contained and does not claim those bytes were verified.
Archive compression and container metadata remain outside scientific identity.

### Append-only export ledger

ResearchWorkspace persists immutable exports under
`nexus-output/exports/<export-id>/` and a separate pointer-last ledger head under
`nexus-output/exports/`. It does not increment `nexus.project.json` revision;
doing so would stale the source generations being exported.

Ledger schema:

```text
nexus.workspace-export-ledger-entry / 1.0.0
```

Each entry binds one-based ordinal, previous entry digest, export id, request
digest, human actor/time, exact source-generation binding, report digest, Bundle
manifest digest, canonical observed-inventory digest, and optional archive
transport digest explicitly labelled non-scientific. Staging, one workspace
lock, predecessor compare-and-swap, promotion, quarantine, exact inventory, and
ledger-pointer-last publication are mandatory. Replay reconstructs the full
export history; no export is overwritten.

### CLI trust boundary

AppServices may create and commit an export only from already verified report,
bundle, and source authorities. CLI provides `report verify`, `bundle verify`,
and `export verify/status` over persisted canonical bytes and inventories.
Until the process entry point can resolve all approved Protocol, Workflow,
snapshot, Screening, and Full Text authorities, CLI does not create a report or
export from caller-supplied ids, digest text, role text, or current projections.

## Alternatives Rejected

- Report directly from current workspace pointers: loses the exact historical cut.
- Collapse duplicate Screening outcomes heuristically: invents scientific state.
- Extend Bundle `1.0.0` in place: violates ADR 0009 versioning.
- Treat missing bytes as external: hides incomplete bundles.
- Put profile shortcuts into editable template metadata: weakens authority and changes template semantics.
- Treat the existing deviation note as verified: lacks digest, approvals, and replay.
- Update project revision for each export: stales the generations being exported.

## Consequences

FE-06 can produce a reconstructable local review account and portable audit
directory while keeping diagnostic gaps distinct from final output. The cost is
two outward packages, promotion of the existing CorpusSnapshots domain package,
a snapshot bridge, versioned Bundle model, verified deviation authority, and a
separate export ledger.

## Migration Effect

Existing FE-04 conduct remains valid but final FE-06 reports require new
snapshot-bound conduct. Existing Bundle v1 manifests and tests remain supported.
The old `ProtocolDeviation` factory remains historical/non-authoritative until
callers migrate to verified deviation records. No old record is silently
upgraded.

## Fixture Effect

Add deterministic fixtures for snapshot binding, report conservation, reason
totals, complete/incomplete Full Text coverage, Rapid Review profile references,
verified deviations, Bundle v2 embedded/external records, missing/extra/altered
and mis-scoped inventory, export replay, stale writer, and promotion failure.

## Compatibility And Claims

This is a local C# contract. It makes no PRISMA certification, PHP, blueprint,
archive-format, provider, network, AI-authorship, database, API, cloud,
production, scale, security-certification, journal-submission, or institutional
compatibility claim.

## Reversal Conditions

Revise this ADR if snapshot-bound Screening cannot preserve accepted FE-04
semantics, final counts require an external reporting standard not represented
here, Bundle v2 cannot coexist with v1, deviation approval cannot use existing
supplemental authority, or export history cannot remain independent of project
revision.
