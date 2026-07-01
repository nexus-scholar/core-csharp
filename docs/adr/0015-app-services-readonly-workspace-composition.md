# ADR 0015: Read-only AppServices Workspace Composition

Status: Proposed

Date: 2026-07-01

## Context

Nexus Scholar Core now has local deterministic records for Search, imported Search evidence, Deduplication, Screening, and Full Text. It also has renderer-neutral UI contracts and an Avalonia block renderer/sample host.

The current UI path is intentionally safe but incomplete:

- `NexusScholar.UiContracts` defines `WorkspacePlan`, `ResearchBlockDescriptor`, evidence refs, validation refs, action descriptors, block severity, block source kind, and known block/action kinds.
- `NexusScholar.Avalonia.Blocks` renders `UiContracts` without referencing Core modules.
- `samples/block-plans` contains hand-written illustrative `WorkspacePlan` JSON.
- The sample host renders those samples and is explicitly non-authoritative.

This leaves a missing boundary. First testers can run the local CLI `demo` and visually inspect sample blocks, but no accepted application-service layer composes real Core Search-import and Deduplication evidence into renderer-neutral blocks.

Without this layer, renderers can only show hand-written samples. With too much application scope, app rows, paths, action callbacks, and UI state risk becoming accidental scientific authority.

Relevant prior decisions and constraints:

- `ADR 0002` defines canonical JSON and digest rules.
- `ADR 0007` defines stable scholarly identity and rejects title-only/runtime-object identity.
- `ADR 0010` defines Search traces as evidence, not app workflow authority.
- `ADR 0011` distinguishes user-supplied imported Search evidence from live provider behavior.
- `ADR 0012` defines Deduplication evidence, clusters, review candidates, and human merge authority.
- `ADR 0013` keeps Screening decisions human-authorized.
- `ADR 0014` keeps app rows and paths as projections unless transformed into Core records.
- `docs/ui/UI-CONTRACTS-v0.md` states that UI contracts are not Core authority, not an app service layer, not persistence, and not a command surface.
- Architecture tests currently protect Core projects from UI frameworks and `NexusScholar.UiContracts`, and protect the Avalonia renderer from Core dependencies.

This ADR defines the APP-01 planning boundary. It does not implement `NexusScholar.AppServices`, add projects, add fixtures, change CLI behavior, change the sample host, or accept persistence/API/cloud/provider behavior.

Decision owner: Nexus application-boundary maintainer.

## Decision

Add a future `NexusScholar.AppServices` project as a read-only composition layer.

The first APP-01 slice maps:

```text
SearchImportTrace + DeduplicationResult -> WorkspacePlan
```

The output is a renderer-neutral projection using `NexusScholar.UiContracts`.

Every block emitted by the first APP-01 composer must use:

```text
BlockSourceKind.AppProjection
```

It must not use:

- `BlockSourceKind.CoreValidated`, because the block is not a Core record;
- `BlockSourceKind.Sample`, because the block is generated from Core evidence rather than hand-written sample JSON;
- UI-framework-specific state.

The generated `WorkspacePlan` is a review convenience projection. It is not scientific authority, not a Core record, not a provenance event, not a protocol decision, not a merge decision, and not a Screening decision.

## Scope

Included in the first APP-01 composition boundary:

- import summary block;
- import warning summary blocks;
- exact duplicate cluster summary blocks;
- review-required duplicate candidate blocks;
- record-comparison blocks;
- human merge-decision placeholder blocks;
- evidence refs to Search import and Deduplication evidence;
- validation refs for warning/review/blocking conditions;
- deterministic payload JSON;
- deterministic block ordering;
- placeholder action descriptors only.

Excluded from APP-01:

- persistence;
- database rows;
- API or web server behavior;
- cloud sync;
- live providers;
- provider SDKs;
- HTTP/network primitives;
- provider credentials;
- scraping;
- PDF extraction;
- OCR;
- UI framework references inside AppServices;
- Core mutation;
- app command execution;
- workflow execution;
- AI/model calls;
- production product-shell behavior;
- PHP compatibility claims.

## Dependency Direction

Allowed:

- `NexusScholar.AppServices` -> `NexusScholar.Kernel`
- `NexusScholar.AppServices` -> `NexusScholar.Search`
- `NexusScholar.AppServices` -> `NexusScholar.Deduplication`
- `NexusScholar.AppServices` -> `NexusScholar.UiContracts`

Forbidden:

- any Core/domain module -> `NexusScholar.AppServices`
- any Core/domain module -> `NexusScholar.UiContracts`
- `NexusScholar.AppServices` -> Avalonia, MAUI, ASP.NET Core, EF Core, provider SDKs, storage SDKs, or model clients
- `NexusScholar.Avalonia.Blocks` -> Core/domain modules
- `NexusScholar.Avalonia.Blocks.SampleHost` -> Core/domain modules in the first APP-01 implementation

The first implementation must add or update architecture tests for these dependency rules before broadening the layer.

## Composition Rules

The first composer should accept an input shape equivalent to:

```text
workspace id
title
SearchImportTrace
DeduplicationResult
optional description
```

The resulting `WorkspacePlan` should use:

- caller-provided stable `WorkspaceId`;
- caller-provided `Title`;
- `BlockMode.Review` when import warnings or review-required dedup candidates exist;
- `BlockMode.Audit` when there are no warning/review-required conditions;
- a description stating that this is a read-only app projection from Core evidence and not scientific authority;
- context refs for the import trace and deduplication result.

Block ordering must be stable:

1. import summary;
2. import warning summaries by deterministic warning category;
3. exact duplicate cluster summaries by deterministic cluster/candidate key;
4. review-required record comparisons by deterministic candidate key;
5. human merge-decision placeholder blocks by deterministic candidate key.

Payload JSON must be deterministic. The implementation should use canonical JSON facilities from `NexusScholar.Kernel` where available instead of relying on runtime dictionary enumeration or ad hoc string concatenation.

Payload JSON must have an object root because `UiContracts` rejects non-object payloads.

## Block Mapping

### Import Summary

Kind:

```text
nexus.block.import.summary
```

Severity:

- `Warning` if parser warnings or skipped records exist;
- otherwise `Success`.

Evidence refs should include:

- import trace reference;
- source file digest when present;
- source format/source kind when available.

Payload should include:

- imported record count;
- sighting count;
- parser warning count;
- skipped record count when available;
- source format;
- source digest;
- explicit no-network/provider non-claim.

### Import Warning Summary

Kind:

```text
nexus.block.import.warning-summary
```

Create one block per deterministic warning category.

Severity:

- `ReviewRequired` for missing required fields or skipped records;
- otherwise `Warning`.

Evidence refs should include available import records, raw record digests, source file digest, and validation refs.

Actions may include `ShowDetails` only. The action is a descriptor, not command execution.

### Exact Duplicate Cluster

Kind:

```text
nexus.block.dedup.candidate-cluster
```

Severity:

- `Info` or `Success` for exact duplicate clusters that do not require human review;
- `ReviewRequired` only when the underlying Deduplication result requires review.

Evidence refs should include Deduplication result, candidate evidence, and related import/search refs.

Payload should include:

- cluster id;
- representative candidate id when available;
- member ids;
- evidence ids;
- match basis.

### Review-required Record Comparison

Kind:

```text
nexus.block.dedup.record-comparison
```

Severity:

```text
ReviewRequired
```

Evidence refs should include candidate import/search refs and Deduplication evidence refs.

Payload should include:

- left/right candidate ids;
- titles;
- normalized identifiers when available;
- similarity or score fields when available;
- threshold;
- source refs;
- reason review is required.

### Human Merge Gate

Kind:

```text
nexus.block.human-gate.merge-decision
```

Severity:

```text
Blocking
```

Actions may describe:

- accept merge;
- reject merge;
- mark unresolved.

Those actions are placeholders only in APP-01. They must not mutate Core records, execute commands, write files, call services, or imply that the UI can finalize a scientific decision.

## Validation And Architecture Tests

The first implementation must add tests proving:

- AppServices references only the allowed Nexus projects;
- Core/domain projects do not reference AppServices;
- Core/domain projects still do not reference UiContracts;
- Avalonia Blocks references UiContracts but no Core/domain projects;
- the sample host remains renderer/sample-only and does not reference Core/domain projects;
- AppServices source does not contain live-call, provider SDK, persistence, API, cloud, PDF/OCR, or model-client symbols;
- all emitted blocks use `BlockSourceKind.AppProjection`;
- output is deterministic across repeated runs;
- block order is stable;
- payload JSON root is an object;
- serialized `WorkspacePlan` round-trips through `UiContractJson.SerializerOptions`;
- no machine-local paths or current timestamps appear in output;
- placeholder actions remain descriptors and do not execute commands.

## Alternatives Considered

### Keep using only hand-written sample plans

Rejected.

Samples are useful for renderer development, but first testers need a bridge from real Core evidence to visible review blocks.

### Let the Avalonia renderer compose directly from Core records

Rejected.

That would make the renderer depend on Core modules and blur the UI/Core boundary. The renderer should consume `UiContracts`, not scientific Core records.

### Put workspace composition inside Core modules

Rejected.

Core records are scientific authority. A `WorkspacePlan` is a renderer-neutral projection for review convenience and should not become Core authority.

### Build a product desktop shell first

Rejected.

Product-shell behavior requires command routing, actor identity, persistence decisions, provenance preview semantics, and broader application boundaries. APP-01 is a smaller read-only bridge.

### Add providers or live imports first

Rejected.

Live providers introduce network, credential, terms-of-use, rate-limit, reproducibility, and legal/access concerns. APP-01 should use existing local Search-import and Deduplication evidence.

## Consequences

Positive consequences:

- first testers can inspect blocks generated from real Core evidence;
- Core remains UI-free;
- Avalonia renderer remains Core-free;
- the sample host can stay sample-only until a later product-shell decision;
- later CLI or dev-harness commands can emit `WorkspacePlan` JSON without adding UI authority.

Negative consequences:

- AppServices duplicates selected Core facts into projection payloads;
- projection output needs explicit tests to avoid drift from Core evidence;
- command execution remains unresolved;
- product-shell work remains blocked until a later ADR.

## Migration Effect

No persisted data is migrated by this ADR.

No existing samples are transformed into Core evidence.

Existing `samples/block-plans` files remain illustrative sample input. They are not replaced by APP-01 and do not become Core records.

If a later implementation adds generated APP-01 block-plan fixtures, those fixtures must be labeled as app projection fixtures, not scientific Core fixtures and not PHP compatibility fixtures.

## Fixture Effect

APP-01 implementation tests should include deterministic local fixtures or test builders covering:

- import without warnings;
- import with parser warnings;
- skipped or malformed imported records;
- exact duplicate cluster;
- review-required dedup candidate pair;
- both import warnings and dedup review candidates in one workspace;
- empty or unsupported composition input rejection;
- stable repeated output;
- JSON round trip through `UiContractJson.SerializerOptions`;
- evidence refs to import source digest and raw record digests when available;
- no `BlockSourceKind.Sample` in generated output.

These fixtures are not PHP-generated fixtures and do not create PHP compatibility claims.

## Conflict Effect

This ADR does not close any existing Core scientific conflict.

It narrows the application projection boundary for the first app-facing slice:

- Search Import and Deduplication records may be read by AppServices.
- AppServices may project selected facts into `WorkspacePlan`.
- The projection is not Core authority.
- UI renderers may display the projection but must not mutate Core through APP-01.

`CF-019`, `CF-024`, `CF-025`, `CF-026`, and `CF-027` remain governed by their existing ADRs and gate notes.

## Implementation Readiness

After this ADR is accepted, implementation may start for:

- `src/NexusScholar.AppServices`;
- `tests/NexusScholar.AppServices.Tests`;
- architecture tests for dependency direction;
- a read-only `SearchDedupWorkspacePlanComposer`;
- deterministic output tests;
- optional later CLI command that prints generated `WorkspacePlan` JSON.

Implementation is not ready for:

- UI product shell behavior;
- sample host direct Core wiring;
- command execution from blocks;
- persistence or app database behavior;
- live provider/network behavior;
- PDF/OCR behavior;
- AI/model calls;
- PHP compatibility claims.

## Reversal Conditions

Revise this ADR if:

1. a later product-shell ADR admits command execution from block actions;
2. a persistence/API ADR changes how app projections are stored or addressed;
3. a UI architecture ADR allows a specific host to reference Core through an application boundary;
4. generated compatibility fixtures require a different projection vocabulary;
5. `UiContracts` changes the `WorkspacePlan` or payload model;
6. provider/network/legal ADRs admit live evidence acquisition that needs different app projection semantics.

## Explicit Claims Not Made

- no `NexusScholar.AppServices` implementation
- no source code changes
- no test project changes
- no CLI behavior changes
- no sample host behavior changes
- no persistence/API/cloud behavior
- no live providers
- no provider SDKs or credentials
- no HTTP/network primitives
- no scraping
- no PDF extraction
- no OCR
- no AI/model call
- no PHP compatibility
- no production desktop shell
- no Core mutation
- no app projection authority over Core records
