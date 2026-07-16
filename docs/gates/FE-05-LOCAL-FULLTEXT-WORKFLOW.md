# FE-05: Local Full Text Workflow

Status: implementation and local verification complete under accepted ADR 0032;
hosted CI and merge pending.

## Goal

Consume a verified FE-04 handoff, retain exact user-supplied Full Text bytes,
record deterministic extraction attempts, conduct human full-text Screening,
and reopen the complete authority from an immutable local workspace generation.

## Sources

- ADRs 0002, 0007, 0008, 0009, 0013, 0014, 0021, 0031, and 0032;
- the FE-05 section of the feature expansion priority plan;
- existing Full Text fixtures as local semantic evidence;
- current FE-04 conduct and ResearchWorkspace transaction behavior.

## Dependency-Ordered Work

1. FE-05.1: verified FE-04 handoff admission bridge.
2. FE-05.2: canonical acquisition, artifact, and extraction-attempt records with
   strict rehydration.
3. FE-05.3: deterministic UTF-8 text and safe XML extraction adapters; explicit
   unsupported PDF extraction.
4. FE-05.4: canonical human full-text conduct, invalidation, and handoff.
5. FE-05.5: atomic ResearchWorkspace Full Text generations retaining raw bytes.
6. FE-05.6: AppServices ports and integrity-only CLI status.
7. FE-05.7: executable fixtures, architecture review, completion evidence, and
   roadmap closeout.

## Required Behavior

- only current included FE-04 outcomes are admitted;
- admission binds the canonical handoff and all supporting decision digests;
- local paths and file names never become artifact identity;
- raw bytes use `raw-artifact-bytes` identity and remain available after parser
  failure or partial extraction;
- every parser attempt binds id, version, configuration, input/output digests,
  warnings, status, and failure category;
- PDF intake is valid evidence but parsing is explicitly unsupported;
- human full-text decisions bind Protocol, criteria, admission, raw artifact,
  and extraction evidence actually used;
- extraction failure cannot create an exclusion;
- changes append complete source-scoped invalidations;
- workspace publication is pointer-last under one lock and reopens identically.

## Allowed Scope

- `src/NexusScholar.FullText/**`;
- `src/NexusScholar.Screening.FullText/**`;
- focused AppServices, ResearchWorkspace, and CLI adapters;
- focused unit, architecture, conformance, and workspace tests;
- `fixtures/conformance/fulltext-workflow/**`;
- ADR 0032, this gate, completion evidence, solution/package topology, and
  feature-plan status.

## Excluded Scope

- live HTTP/provider retrieval, scraping, authentication, paywall bypass, or
  shadow-library behavior;
- OCR, browser rendering, deterministic PDF parsing, or hidden parser fallback;
- database, API, cloud, UI shell, scheduler, queue, or background service;
- AI final decisions or parser output treated as scientific authority;
- PHP, parser, production, scale, security, legal, or institutional claims.

## Required Negative Cases

- stale, altered, excluded, unresolved, or wrong-candidate FE-04 handoff;
- same path with changed bytes and same bytes through different paths;
- missing raw bytes, wrong digest scope, artifact tamper, and candidate splice;
- network URL submitted as local input;
- malformed/HTML XML, empty text, invalid/encrypted PDF, oversized input;
- partial extraction without warnings or output digest mismatch;
- failed/unsupported extraction carrying successful output;
- extraction failure used as an exclusion reason;
- automation attempts a final decision;
- stale Protocol/criteria/artifact/extraction decision binding;
- incomplete invalidation, malformed chain, stale writer, partial generation,
  missing raw artifact, and manifest/artifact tamper.

## Verification

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Also run focused FullText, Screening.FullText, ResearchWorkspace, AppServices,
CLI, architecture, conformance, package-policy, and package-smoke checks.

## Exit Criteria

- a verified FE-04 include can be admitted, ingested, extracted when supported,
  decided by a human, persisted, reopened, and handed off locally;
- raw and derived evidence retain distinct identities through replay;
- parser failure and partial status remain visible and non-authoritative;
- tamper, stale authority, and crash paths fail closed without pointer drift;
- independent scientific and test reviews report no remaining blocking defects;
- all local and hosted validation passes.
