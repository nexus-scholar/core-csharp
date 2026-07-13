# ADR 0026: Phase 7 Corpus Lock And Snapshot Compatibility Boundary

## Status

Accepted

## Date

2026-07-13

## Context

Hardening 27 requires generated evidence for pinned PHP Deduplication and corpus lock/snapshot behavior. PHP exposes `CorpusLockPolicy`, database-backed project locks, immutable snapshot rows, and export metadata that becomes citable only when a locked project has a snapshot. The local C# Deduplication contract in ADR 0012 consumes raw Search/import evidence but does not define general corpus lock state, snapshot identity, snapshot equality, or PHP persistence parity.

ADR 0009 resolves only local bundle round-trip equality. ADR 0012 explicitly leaves corpus snapshot equality unclaimed and treats Web membership hashes, persisted runs, and representative snapshots as projections. Treating missing C# lock/snapshot behavior as PHP-compatible, or silently importing Laravel persistence semantics into Core, would violate the authority order and dependency rules.

The compatibility evidence gate needs a reviewed classification for PHP lock/snapshot observations without expanding Phase 7 into a new persistence or snapshot product feature.

## Decision

For Hardening 27, deterministic PHP corpus lock and snapshot observations are retained as generated compatibility evidence and classified as `intentional_change` with the rationale `intentional non-adoption pending a dedicated corpus snapshot contract`.

This classification means:

- PHP locked-corpus mutation rejection, export lock metadata, and snapshot-presence behavior may be generated and digest-bound.
- C# Deduplication does not claim an equivalent lock port, snapshot record, persistence model, membership hash, citable-export rule, or snapshot equality rule.
- `NexusScholar.ResearchWorkspace` transaction locks and generation manifests are not substitutes for PHP scientific corpus locks or snapshots.
- Laravel repositories, migrations, transactions, audit rows, UUIDs, timestamps, and Web membership hashes remain framework or app evidence only.
- H27 comparators must assert the PHP observation and the absence of a C# compatibility claim; they must not fabricate a local replay result.
- A future corpus snapshot gate must define stable membership identity, unresolved-candidate treatment, actor and timestamp authority, immutability, supersession, equality, and persistence boundaries before C# implementation.

This decision resolves only how H27 classifies the observed PHP behavior. It does not resolve general corpus snapshot identity or equality.

## Alternatives Considered

### Port PHP Lock And Snapshot Persistence During H27

Rejected. It would add persistence and scientific snapshot semantics without an accepted contract and would exceed the compatibility-evidence phase.

### Treat Research Workspace Generation Locks As Equivalent

Rejected. Filesystem transaction exclusion protects workspace publication; it is not scientific corpus lock authority and does not create immutable corpus membership.

### Leave H27 Cases As Unresolved Specification Conflicts

Rejected for the H27 classification boundary. The local decision is to preserve the observations while intentionally not adopting them. Broader snapshot semantics remain unresolved under `CF-014`.

### Omit Lock And Snapshot Evidence

Rejected. H27 explicitly includes the lock/snapshot boundary, and omission would hide a material PHP behavior difference.

## Consequences

Positive:

- H27 can record PHP lock/snapshot behavior without overstating C# compatibility.
- Core remains free of Laravel and app persistence semantics.
- Future snapshot design remains explicit and evidence-informed.

Negative:

- C# still has no scientific corpus lock or general snapshot equality contract.
- Lock/snapshot fixtures demonstrate an intentional non-adoption, not implemented parity.
- Downstream release or citable-export claims cannot rely on PHP snapshot semantics.

## Migration Effect

No production data, schema, package, or runtime migration is introduced. A later accepted snapshot ADR may add new records and adapters; it must not reinterpret Research Workspace transaction locks as historical scientific corpus locks.

## Fixture Effect

The H27 generated fixture set may include:

- locked Deduplication rejection;
- locked export metadata with a snapshot;
- locked export metadata without a snapshot.

Each case must be classified `intentional_change`, cite this ADR, exclude generated ids and timestamps unless pinned, and carry an explicit no-C#-snapshot-compatibility rationale. Equivalent Deduplication comparisons remain governed by ADR 0012.

## Reversal Conditions

This boundary may be replaced only by an accepted corpus snapshot ADR that defines scientific snapshot identity, equality, authority, provenance, mutation, supersession, unresolved-candidate handling, and persistence-independent Core records. Generated H27 evidence must be reclassified when such a contract is implemented.
