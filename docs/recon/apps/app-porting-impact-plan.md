# App Porting Impact Plan

Status: sequencing and contract guidance.

This document explains how CLI/Web consumer evidence affects the next C# Core porting tasks. It preserves the current order: Search remains next.

## Accepted Queue

Current immediate order:

1. Search reconnaissance on `cdx/gate-9-search-recon`.
2. App consumer evidence on `cdx/app-recon-cli-web-core-usage`.
3. ADR 0010 Search Trace and Plan Contract.
4. Local C# Search implementation with stub providers.
5. Deduplication reconnaissance.
6. Screening reconnaissance.

The app reconnaissance does not derail Search. It adds consumer evidence that ADR 0010 must account for before Search implementation.

## Impact On ADR 0010

ADR 0010 should include an app consumer compatibility section.

Required boundary language:

```text
App consumer boundary:
- C# Search must expose a raw trace/result shape that CLI/Web can consume later without forcing Search-time deduplication.
- Search output must preserve duplicate provider sightings.
- Search output must preserve unresolved no-id candidates as staged candidates only.
- Search output must carry enough provider stats/status evidence for CLI/Web run displays.
- Search output must not return canonical CorpusSlice membership as its primary output.
- CLI/Web display hashes or fallback keys are projection identifiers only, not scientific identity.
```

## Search Contract Implications

C# Search should define records for:

- request identity and normalized query fields;
- selected and active provider aliases;
- cache identity material;
- provider attempt order and status;
- raw provider sightings;
- normalized work identifiers under ADR 0007 namespaces;
- unresolved no-id candidates as staged evidence;
- provider stats and partial failure evidence;
- optional raw payload preservation or raw payload digest;
- explicit handoff to future Deduplication.

C# Search should not:

- call live scholarly providers in CI;
- implement provider/network adapters in the first local slice;
- persist Laravel/app database state;
- use title-only identity;
- deduplicate Search output into canonical corpus membership;
- claim PHP compatibility without generated fixtures and comparators.

## Impact On Deduplication

Deduplication remains after Search implementation and after a dedicated recon pass.

Web evidence to preserve for Dedup planning:

- exact identifier grouping extends PHP Core result semantics;
- representative scoring prefers richer records, including DOI, abstract, venue, authors, year, citation count, and non-retracted status;
- dedup clusters are persisted and later used for app corpus lock;
- membership hash protects against stale draft corpus changes;
- locked snapshots use representative-aware rows.

Open Dedup questions:

- Should exact-identifier grouping be Core domain behavior or Web policy?
- Should representative scoring move into Core or stay app-level?
- How should raw duplicate provider sightings flow from Search into Dedup?
- What fixture shape preserves duplicate evidence without relying on runtime object identity?

## Impact On Screening

Screening remains after Dedup reconnaissance.

CLI evidence:

- file-based deterministic and LLM screening exists outside Core;
- project-backed screening delegates to PHP Core.

Web evidence:

- screening starts from locked representative snapshots;
- assignments are allocated to human reviewers;
- conflicting human decisions create app conflict records;
- human adjudication creates a new screening verdict;
- full-text candidates are derived from final title/abstract outcomes.

Open Screening questions:

- Which verdict and adjudication semantics belong in Core?
- Which assignment and conflict workflows remain app-level?
- How should criteria hashes bind to approved protocol content later?
- How should AI or LLM screening outputs remain proposals until human authority accepts them?

## Impact On Protocol, Provenance, Bundle, And Snapshot Gates

Protocol:

- Web `ProjectProtocol` is a product record and snapshot, not a Gate 3 approved protocol.
- Future app-alignment must map Web protocol fields to Core protocol drafts, decisions, approvals, amendments, waivers, and deviations before treating them as Core records.

Provenance:

- Web `audit_events` are app activity rows.
- Gate 5 provenance requires event digests, agent/activity models, protocol/workflow bindings, input/output references, and projection exclusions.

Bundle and artifacts:

- CLI/Web manifests and artifact paths are local outputs.
- ADR 0009 requires logical artifact paths, raw-byte digests, schema ids, manifest digest, and staged import safety.

Snapshots:

- Web corpus snapshots are app persistence rows.
- Gate 6 defines only local bundle round-trip equality, not general corpus snapshot equality.
- Future snapshot work must not rely on app row identity or local paths.

## Recommended Next Branches

1. `cdx/gate-9-search-contract`
2. `cdx/gate-9-search-local-stub`
3. `cdx/gate-9-dedup-recon`
4. `cdx/gate-9-screening-recon`
5. later app-alignment branch after Search contract and local Search are stable

## Non-Claims

- no Search implementation started
- no Deduplication implementation started
- no Screening implementation started
- no provider/network implementation proposed
- no app behavior modified
- no app behavior made authoritative
- no PHP compatibility claimed
- no persistence/API/UI/cloud behavior moved into Core
