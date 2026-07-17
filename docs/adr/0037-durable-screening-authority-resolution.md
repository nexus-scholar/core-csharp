# ADR 0037: Durable Screening Authority Resolution

- Status: Accepted
- Date: 2026-07-17
- Decision owner: Nexus Scholar maintainer/manager

## Context

ADR 0031 requires title/abstract Screening conduct to resolve exact verified
Deduplication, Protocol, criteria, and, when claimed, Workflow authority.
ResearchWorkspace can already reopen the FE-01 Deduplication authority chain,
but Screening conduct callers must still supply the other verified objects from
memory. ADR 0036 therefore prohibited desktop Screening decisions.

Ids, digest strings, manifest pointers, and UI fields cannot reconstruct that
authority. A process-entry adapter needs a local package whose bytes can be
verified after restart without making the desktop or project index scientific
authority.

## Decision

### Slice boundary

FE-08 Slice 4 introduces durable Screening authority resolution only. It does
not admit a Screening decision, conduct initialization, handoff, Workflow
completion, Protocol authoring, criteria authoring, or authority approval from
the desktop.

### Package ownership

`NexusScholar.ResearchWorkspace` owns an immutable Screening authority package.
The package binds:

- the workspace id and expected project revision;
- the current FE-01 authority generation and manifest digest;
- the current verified Deduplication result, decision set, and corpus snapshot;
- one approved Protocol version, its approval policy, and exact approval records;
- one canonical title/abstract Screening criteria record;
- optional verified Workflow authority only when Workflow governance is claimed.

The project index stores only the current package generation, manifest path, and
manifest SHA-256. Those pointers are locators, not authority.

### Verification

Protocol and criteria artifacts use strict canonical JSON codecs. Rehydration
reproduces Protocol content and approval-record digests, resolves human approval
actors, verifies the approval policy, and returns `VerifiedProtocolVersion`.
Criteria rehydration reproduces its digest and verifies the approved Protocol
binding.

The workspace resolver verifies the package manifest and every artifact byte,
reopens the current FE-01 authority chain, and rejects stale project revision,
authority generation, result, decision-set, snapshot, Protocol, criteria, or
Workflow bindings. It returns verified domain objects only inside the
ResearchWorkspace boundary.

The package revision must exactly match the current project revision. Source
authority generations remain valid across later unrelated project revisions
only while their project pointer generation id and manifest digest are
unchanged. Authority lineage revisions are monotonic, not globally consecutive;
every writer still uses exact expected-project-revision concurrency.

When Workflow governance is absent, the package reports that fact explicitly.
When it is claimed, missing or unverifiable Workflow authority fails closed.

### Desktop projection

Desktop.AppServices may expose only immutable readiness status and identifiers
from the ResearchWorkspace resolver. Desktop and Avalonia projects do not gain
direct Protocol, Deduplication, Screening, Workflow, or persistence authority.

## Consequences

- a restarted process can determine whether the exact ADR 0031 authority
  package is ready;
- tampered or stale authority is distinguishable from unavailable authority;
- Slice 5 can add the first desktop Screening review over this resolver without
  inventing authority from UI state;
- upstream Protocol and criteria creation remain separate authorized workflows.

## Rejected Alternatives

- Persist ids and digests only: they cannot rehydrate verified authority.
- Put verified objects in desktop session state: restart loses authority and UI
  state becomes trusted.
- Admit Screening review in the same slice: resolver behavior needs adversarial
  validation before scientific mutation.
- Create a generic authority registry: broader than the exact ADR 0031 need.

## Compatibility And Claims

This is a local C# authority-resolution contract. It makes no PHP, blueprint,
authentication, identity-provider, database, API, cloud, synchronization,
multi-user, provider, AI, installer, deployment, or production-security claim.

## Reversal Criteria

Revise this ADR before allowing desktop mutation without successful package
resolution, treating project pointers as authority, or widening the package
into a generic authority registry.
