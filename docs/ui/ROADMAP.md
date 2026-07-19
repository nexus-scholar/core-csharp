# UI Roadmap

Status: FE-08 slices 1 through 9 are complete. FE-10 is the next external-design gate candidate and
is not implementation-authorized.

This roadmap describes product delivery without changing the rule that
scientific authority belongs to verified domain records and explicit human
actions, never UI state.

## Architecture

```text
Domain authority
    <- application commands and verified read models
    <- Desktop.AppServices
    <- UiContracts and Avalonia renderers
    <- product host
```

Dependencies point inward. Rendering and interaction state may simplify a
workflow, but cannot weaken identity, Protocol binding, actor authority,
provenance, invalidation, or reproducibility.

## Completed Foundation

### Phase 0: philosophy and product design

Completed documentation covers product positioning, typed blocks, research
cockpit, portability, beginner/audit modes, and AI interaction rules.

### Phase 1: renderer-neutral contracts

Implemented `NexusScholar.UiContracts` with:

- workspace plans and ordered research blocks;
- display modes, severity, source kind, and action kind;
- evidence and validation references;
- human-confirmation and destructive-action flags;
- JSON round-trip and architecture coverage.

It remains UI-framework-free and contains no domain authority.

### Phase 2: sample plans

Implemented illustrative Import warning, Deduplication review, and Bundle
verification plans under `samples/block-plans`.

They are samples, not scientific records, conformance fixtures, or compatibility
evidence.

### Phase 3: Avalonia renderer

Implemented `NexusScholar.Avalonia.Blocks` to render plans, blocks, evidence,
validation, action descriptors, flags, and structured payloads.

The renderer accepts caller-supplied callbacks. It does not call Core mutations.

### Phase 3.5: visual harness

Implemented `samples/NexusScholar.Avalonia.Blocks.SampleHost` for manual
inspection of the sample plans.

### UI-01 and UI-02A: historical preview

Implemented `samples/NexusScholar.Desktop.Preview` over ResearchWorkspace read
models, followed by safe local verify/analyze operations. This preview remains
useful for regression and architectural history but is not the current product
host.

## FE-08 Product Delivery

### Slices 1-2: local product shell

Status: complete under ADR 0035.

Delivered:

- Windows-first `NexusScholar.Desktop` host;
- `NexusScholar.Desktop.AppServices` facade;
- open and inspect existing workspaces;
- initialize local workspaces;
- import researcher-supplied local Search exports;
- verify and analyze;
- deterministic status, attention, failure, stale, and recovery states;
- architecture guards against direct host-to-domain/workspace references.

### Slice 3: desktop Deduplication review

Status: complete under ADR 0036.

Delivered:

- first human-authorized desktop scientific action;
- actor/role-bound preview and confirmation;
- canonical request digest and stale-safe commit;
- exact source result, policy, predecessor snapshot, and effect bindings;
- durable authority generation and provenance through ResearchWorkspace;
- refresh and already-applied/recovery behavior;
- native Windows visual QA.

The desktop invokes the accepted FE-02 command. It does not define
Deduplication authority.

### Slice 4: Screening authority resolution

Status: complete under ADR 0037.

Delivered:

- strict canonical approved-Protocol authority package;
- strict canonical title/abstract criteria;
- immutable pointer-last Screening authority generations;
- exact workspace, FE-01 generation, result, decision set, and snapshot binding;
- ready, unavailable, stale, invalid, and recovery-required projections;
- read-only Desktop.AppServices readiness facade;
- cross-generation revision handling.

No desktop Screening decision is admitted.

### Slices 5-9: desktop conduct and closeout

Status: complete.

Delivered:

1. title/abstract Screening conduct and correction handoff;
2. local Full Text review within approved authority packages;
3. reporting, audit-bundle closeout, and export-ledger verification;
4. recovery and attention-state lifecycle handling.

These outcomes remain downstream of the accepted domain/application commands;
the UI does not own their scientific authority.

### Release Readiness Alpha 2

Status: implemented under ADR 0046 and the accepted alpha.2 release gate.

Delivered:

1. unsigned self-contained Windows x64 portable distribution;
2. exact release manifest, checksums, SPDX SBOM, and attestation;
3. sanitized local crash diagnostics and next-launch notice;
4. manifest-verified backup and byte-exact new-directory restore;
5. native headless acceptance over the real product host.

## Later UI Work

After Slice 9, additional desktop work should follow demonstrated user need and
accepted gates:

- workflow-task completion and navigation;
- Extraction, Appraisal, and Synthesis workspaces;
- cross-platform desktop qualification;
- deeper accessibility and usability evaluation with real first testers;
- installer, signing, and update strategy under a successor ADR.

These are not automatically authorized by their appearance here.

FE-09 through FE-12 may later add provider, plugin, governed-AI, and
collaboration surfaces. Their UI must remain downstream of the corresponding
domain/application gates.

## Product Non-Claims

The current desktop does not claim:

- production readiness or accessibility certification;
- live provider access, scraping, PDF parsing, or OCR;
- plugin execution or arbitrary-code sandboxing;
- live model execution or AI decision authority;
- database, API, cloud sync, authentication, tenancy, or multi-user work;
- broad PHP compatibility.

The application has durable local file persistence. It does not yet have
database or cloud persistence.
