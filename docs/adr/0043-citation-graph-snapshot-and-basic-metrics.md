# ADR 0043: Citation Graph Snapshot And Basic Metrics

Status: Accepted

Date: 2026-07-17

## Context

ADR 0027 preserved PHP Citation Network observations without adopting them.
FE-09C now needs a persistence-independent C# graph contract before any live
provider, snowballing, export, or compatibility work.

## Decision

Create packable `NexusScholar.Network`, depending only on Kernel and Shared.
The first slice admits resolved graph nodes with stable `WorkIdSet` identity,
explicit unresolved citation targets, evidence-backed directed citation edges,
and immutable direct-citation snapshots.

A snapshot binds its source corpus snapshot id and digest, ordered nodes,
ordered edges, algorithm id and version, and canonical content digest. Titles,
paths, database rows, PHP graph ids, runtime object identities, and provider
positions are not scientific identity.

Every edge requires source evidence. The citing endpoint must be an admitted
resolved node. An unresolved cited target remains edge evidence and cannot be
promoted to graph membership without stable identity. Duplicate normalized
edges are rejected.

The first metric contract is deterministic and snapshot-bound: node count, edge
count, isolated-node count, and per-node in/out degree. Centrality, shortest
paths, snowballing, weighted derived graphs, and exports remain out of scope.

## Compatibility Effect

ADR 0027 remains authoritative for H29 classifications. FE-09C adds a local C#
contract but does not reclassify or edit PHP observations. Compatibility may be
widened only by a dedicated generated-fixture comparison.

## Consequences

Local recorded evidence can produce reproducible graph snapshots and basic
metrics without network, persistence, or a graph library.

No live provider graph, Search-cache coupling, dissemination export, GraphML,
PHP parity, production scale, or scientific-impact interpretation is claimed.

## Reversal Conditions

New graph types, metrics, persistence, exports, or PHP equivalence each require
their own accepted gate and fixture evidence.
