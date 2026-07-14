# FE-01: Decision And Snapshot Authority

Status: complete and accepted for the local FE-01 scope under ADR 0028.

Completion evidence: `docs/gates/FE-01-DECISION-SNAPSHOT-AUTHORITY-EVIDENCE.md`.

## Goal And Scientific Behavior

Create the minimum local authority foundation for a later human Deduplication action to change scientific state without losing evidence or history.

The completed gate must be able to verify, save, reopen, and reproduce:

- one closed Deduplication authority policy;
- the Deduplication decision, supersession, and invalidation contracts through deterministic domain fixtures without production append;
- one immutable empty-decision-set baseline corpus snapshot containing every grouped or unresolved candidate exactly once;
- one snapshot-publication provenance event;
- one atomic ResearchWorkspace authority initialization generation containing the policy, baseline snapshot, provenance event, and manifest.

The only admitted production transition is `InitializeAuthorityGeneration`. The gate does not append a decision, expose an executable merge command, apply merge/keep-separate/unresolved semantics, publish invalidations, or produce a successor snapshot; those are FE-02.

## Reading Checklist And Authority Order

Authority remains: approved specifications, accepted ADRs, golden fixtures, pinned PHP observations, current C# behavior, then plans and discovery notes. Read this implementation checklist in that order:

1. `specs/SOURCE.lock.json` and applicable approved specifications
2. accepted `docs/adr/0001`, `0002`, `0004`, `0008`, `0012`, `0013`, `0015`, `0016`, `0023`, `0025`, `0026`, and `0028`
3. existing local and golden fixtures without editing historical expected output
4. pinned PHP observations as compatibility evidence only
5. current Deduplication, Kernel, Provenance, ResearchWorkspace, AppServices, architecture, and conformance code and tests as feasibility evidence
6. `AGENTS.md`, `PLANS.md`, `docs/plans/2026-07-14-feature-expansion-priority.md`, and `docs/port/OPEN-CONFLICTS.md`
7. Nexus CLI, Nexus Web, and blueprint material as lower-authority discovery evidence only

Stop and return to ADR review if an implementation requires guessing beyond ADR 0028, broad blueprint adoption, PHP persistence parity, or a generic cross-domain decision abstraction.

## Dependency-Ordered Work Packages

Each package has one primary module owner and a disjoint source path. Test and fixture changes for a package stay with that owner until the integration package.

### FE-01.0 Contract Acceptance

Owner: architecture/governance docs owner.

1. Manager acceptance of ADR 0028 and this gate is recorded by their Accepted status.
2. The accepted choices are the Deduplication-local authority-policy schema, `NexusScholar.CorpusSnapshots`, dual snapshot digests, canonical ordering rules, non-packable project status, and baseline-only production initialization.
3. Record the narrowly resolved `CF-014` scope and update the operating plan only after implementation evidence exists.

Source implementation may start only inside the allowed paths below.

### FE-01.1 Deduplication Authority Digests And Decision Contract

Owner: `NexusScholar.Deduplication`.

Depends on: FE-01.0.

1. Implement the accepted canonical verified-result, candidate, review-target, and evidence digest envelopes and exact collection ordering from ADR 0028.
2. Implement `nexus.deduplication.authority-policy` version `1.0.0`, including the fixed local source kind, authorized human actor/role pairs, actions, reason codes, rationale policy, issuer, supersession fields, and policy digest.
3. Implement `nexus.deduplication.decision` version `1.0.0`, closed action/reason values, exact policy binding, source bindings, evidence references, supersession, FE-01 invalidation references, and decision digest.
4. Add unverified/verified wrappers, deep immutable rehydration, digest reproduction, stale source/policy checks, canonical-order enforcement, and stable error categories.
5. Keep action application/reduction in deterministic domain fixtures only; do not expose a production append or merge command.

Primary paths:

- `src/NexusScholar.Deduplication/**`
- `tests/NexusScholar.Core.Tests/*Deduplication*`
- new FE-01 Deduplication fixture subtree

### FE-01.2 Focused Corpus Snapshot Domain

Owner: `NexusScholar.CorpusSnapshots`.

Depends on: FE-01.1.

1. Add non-packable `src/NexusScholar.CorpusSnapshots/` and its test coverage without changing `eng/package-topology.json` or the twelve-package validation set.
2. Implement `nexus.corpus.snapshot` version `1.0.0`, content and record digest envelopes, deterministic ordering, complete membership, representative relations, unresolved no-id entries, decision-set binding, supersession, and deep immutable rehydration.
3. Implement `nexus.corpus.snapshot-invalidation` version `1.0.0` and deterministic stale-reference planning.
4. Prove persistence-independent equality and verification.

Primary paths:

- `src/NexusScholar.CorpusSnapshots/**`
- `tests/NexusScholar.Core.Tests/*CorpusSnapshot*`
- new FE-01 CorpusSnapshots fixture subtree

### FE-01.3 Provenance Event Projection

Owner: `NexusScholar.Provenance`.

Depends on: FE-01.1 and FE-01.2.

1. Add the baseline `corpus-snapshot-published` activity construction helper if existing public factories cannot express the required canonical inputs and outputs.
2. Fixture-test future `deduplication-decision-recorded` and `corpus-snapshot-invalidated` direction without publishing those events from ResearchWorkspace in FE-01.
3. Keep Provenance Kernel-only and represent Deduplication/snapshot records through canonical entity references.
4. Reuse append-time event validation and digest reproduction.

Primary paths:

- `src/NexusScholar.Provenance/**`
- `tests/NexusScholar.Core.Tests/ProvenanceTests.cs`
- new FE-01 provenance fixture subtree if needed

### FE-01.4 Atomic Local Authority Persistence

Owner: `NexusScholar.ResearchWorkspace`.

Depends on: FE-01.1 through FE-01.3.

1. Add optional `currentAuthorityGenerationId`, `authorityGenerationManifestPath`, and `authorityGenerationManifestSha256` project fields with all-or-none validation and legacy-null read behavior.
2. Add `nexus.workspace-authority-generation.v1` with the exact accepted source-analysis, result, policy, empty decision-set, predecessor, revision, and artifact bindings.
3. Implement only `InitializeAuthorityGeneration`, staging the canonical policy, baseline snapshot, snapshot-publication provenance event, and canonical manifest together.
4. Under the workspace lock, re-read and compare workspace revision, source analysis generation/manifest digest, source result digest, and absence of an authority pointer.
5. Rehydrate all staged domain records and reproduce exact canonical file and manifest digests before atomic promotion.
6. Replace the project file last; quarantine failed promoted generations; preserve the prior state after any crash, validation failure, or stale compare-and-swap.
7. Reject import or analysis while an authority pointer is active with `authority-generation-active` until FE-02 defines successor behavior.
8. Add save, reopen, verify, stale-writer, active-authority, and crash-boundary tests.

Primary paths:

- `src/NexusScholar.ResearchWorkspace/**`
- `tests/NexusScholar.ResearchWorkspace.Tests/**`

ResearchWorkspace must not invent decision policy, snapshot equality, or provenance semantics.

### FE-01.5 Read-Only Application Projection

Owner: `NexusScholar.AppServices`.

Depends on: FE-01.4.

1. Add UI-neutral read models for current authority generation health, policy identity, baseline snapshot identity/content digest, unresolved count, and empty decision-set status.
2. Preserve existing locked action behavior.
3. Do not add command handlers, file I/O, UI dependencies, or scientific mutation.

Primary paths:

- `src/NexusScholar.AppServices/**`
- `tests/NexusScholar.AppServices.Tests/**`

### FE-01.6 Architecture And Conformance Integration

Owner: architecture/conformance test owner.

Depends on: FE-01.1 through FE-01.5.

1. Add the non-packable CorpusSnapshots project and affected tests to `NexusScholar.Core.slnx`.
2. Add architecture rules for all new dependency directions and forbidden framework references.
3. Replay the fixture catalog through public unverified-to-verified boundaries.
4. Verify canonical output digests, cross-platform determinism, stale/tamper categories, and deep immutability.
5. Produce a completion evidence report bound to the tested commit.

Primary paths:

- `NexusScholar.Core.slnx`
- `tests/NexusScholar.Architecture.Tests/**`
- `tests/NexusScholar.Conformance.Tests/**`
- `fixtures/conformance/decision-snapshot-authority/**`
- `docs/gates/FE-01-DECISION-SNAPSHOT-AUTHORITY-EVIDENCE.md`
- implementation closeout updates to `docs/port/OPEN-CONFLICTS.md` and `PLANS.md`

## Allowed Implementation Paths

- `src/NexusScholar.Deduplication/**`
- `src/NexusScholar.CorpusSnapshots/**`
- `src/NexusScholar.Provenance/**`
- `src/NexusScholar.ResearchWorkspace/**`
- `src/NexusScholar.AppServices/**`
- affected project files and `NexusScholar.Core.slnx`
- `tests/NexusScholar.Core.Tests/**` for narrowly named Deduplication, CorpusSnapshots, and Provenance tests
- `tests/NexusScholar.ResearchWorkspace.Tests/**`
- `tests/NexusScholar.AppServices.Tests/**`
- `tests/NexusScholar.Architecture.Tests/**`
- `tests/NexusScholar.Conformance.Tests/**`
- `fixtures/conformance/decision-snapshot-authority/**`
- `docs/gates/FE-01-DECISION-SNAPSHOT-AUTHORITY-EVIDENCE.md`
- gate-closeout-only updates to `docs/port/OPEN-CONFLICTS.md`, `PLANS.md`, and the feature roadmap

Changes outside an owning package's primary paths require an explicit handoff to the named owner. Shared Kernel changes require proof that an existing primitive is insufficient and manager approval before edit.

## Forbidden Implementation Paths And Behavior

- existing ADRs, approved specifications, historical gates, or historical fixtures
- `src/NexusScholar.Protocol/**`, `Workflow/**`, `Screening/**`, `FullText/**`, `Bundles/**`, `Artifacts/**`, `Search/**`, `AI/**`, `Extensibility/**`, `Cli/**`, `UiContracts/**`, Avalonia projects, desktop samples, or UI tests
- live providers, HTTP, provider SDKs, credentials, scraping, or network calls
- database, ORM, API/server, cloud, synchronization, or multi-user persistence
- executable CLI, desktop, or AppServices merge/keep-separate/unresolved commands
- AI/model proposals or acceptance
- plugin execution
- generic cross-domain decision abstractions
- reinterpretation of ResearchWorkspace analysis generations as corpus snapshots
- editing PHP-generated outputs or claiming PHP/app/blueprint compatibility
- production dependency additions without a separate accepted rationale or ADR

## Fixture Catalog

All FE-01 fixtures use fixed ids, fixed clocks, canonical UTF-8 JSON, source metadata, generator command/version, and recomputable input/output digests.

### Valid Cases

- `fe01-valid-authority-policy`: closed local policy with deterministic actor/role, action, reason, issuer, and digest material.
- `fe01-valid-human-decision-contract`: persistence-independent authorized human decision over an exact verified result, review target, policy, and ordered evidence set; it is not appended by ResearchWorkspace in FE-01.
- `fe01-valid-initial-snapshot`: empty decision set; every result candidate appears exactly once; no-id candidates are explicit unresolved entries; raw sightings remain reachable.
- `fe01-valid-successor-snapshot-contract`: persistence-independent successor binds prior record digest, superseding decision, decision-set digest, and FE-01 invalidations; it is not published in FE-01.
- `fe01-content-equality`: distinct snapshot ids and creation metadata with identical scientific content have equal content digests and different record digests.
- `fe01-save-reopen-verify`: atomic baseline authority generation reopens and reproduces policy, snapshot, provenance, file, and manifest digests.
- `fe01-correction-by-supersession-contract`: domain replay preserves the original decision and snapshot and verifies a successor chain without production append.

### Actor And Authority Rejections

- missing or blank actor id;
- unknown actor or role not authorized by the bound policy;
- automation, system, import, plugin, or AI actor attempts a final decision;
- missing, stale, malformed, or mismatched authority-source digest;
- protocol-governed claim with missing, draft, superseded, withdrawn, or digest-mismatched protocol binding;
- missing required rationale or policy-defined reason code.

### Stale And Binding Rejections

- stale workspace revision or source analysis generation;
- stale source analysis-manifest digest;
- existing authority pointer during initialization;
- source result id matches but source result digest differs;
- target id matches but target content digest differs;
- source snapshot id matches but record digest differs;
- active decision-set digest differs;
- evidence id matches but digest or digest scope differs;
- correction cites a missing, inactive, cross-target, or cyclic superseded decision;
- two writers initialize from the same expected revision and only one can commit.

### Snapshot Structure Rejections

- duplicate candidate membership within or across groups;
- representative is absent from its group;
- one candidate is both grouped and unresolved;
- no-id unresolved candidate is omitted or assigned a fabricated work id;
- stable candidate is silently omitted;
- conflicting representative relation;
- duplicate decision or evidence reference;
- non-canonical group, member, decision-reference, or unresolved-candidate ordering;
- content digest matches while record metadata was tampered;
- content-equal comparison attempted across unsupported schema versions.

### Tamper And Rehydration Rejections

- unsupported or missing schema id/version;
- malformed digest, wrong scope, uppercase/non-canonical rendering, or digest mismatch;
- non-UTC timestamp, serializer-injected current time, or non-NFC text;
- duplicate ids, dangling refs, null/omission confusion, or caller-owned mutable collection mutation;
- tampered decision, decision-set, membership, evidence, invalidation, provenance, raw file, or authority manifest;
- direct construction of a verified record or bypass of rehydration.

### Atomicity Rejections

- crash after staging and before promotion;
- crash after generation promotion and before project-pointer replacement;
- partial or missing staged file;
- foreign or incomplete authority generation;
- failed promoted generation is not quarantined;
- prior project state changes after a failed initialization.

## Architecture Requirements

- Deduplication depends only on Kernel, Shared, and Search.
- CorpusSnapshots is non-packable and depends only on Kernel and Deduplication.
- Provenance remains Kernel-only and has no Deduplication, CorpusSnapshots, ResearchWorkspace, AppServices, storage, or UI reference.
- ResearchWorkspace may depend inward on Kernel, Shared, Search, Deduplication, CorpusSnapshots, Provenance, AppServices, and UiContracts; it remains free of UI frameworks, providers, database/ORM, cloud, API/server, and model clients.
- AppServices may add CorpusSnapshots only for read projections; it has no file I/O, persistence, UI framework, provider, or model-client dependency.
- Core domain projects do not reference ResearchWorkspace, AppServices, UiContracts, CLI, desktop, or Avalonia.
- Verified constructors are non-public; all persistence input crosses explicit rehydration boundaries.
- No new production dependency is added without written approval.

Architecture tests must inspect both assembly references and forbidden source symbols for the new project and changed application layers.

## Conformance Requirements

- Every valid fixture replays to byte-identical canonical JSON and the expected digest on Windows and Linux.
- Every malformed fixture fails with one stable category before becoming verified authority.
- Result, target, evidence, decision, decision set, snapshot content, snapshot record, invalidation, provenance, authority manifest, and raw-file digests are independently reproduced.
- Reopen verification proves persistence-independent domain validity and workspace-generation integrity separately.
- Content equality and record equality are tested as distinct contracts.
- Existing Gate 9, Screening, Provenance, ResearchWorkspace, architecture, and conformance suites remain green without historical fixture edits.
- No comparator treats generated ids/timestamps as ignorable when they are record-authority fields.

## Risks, Conflicts, And ADR Dependencies

- `ADR 0028` and this gate are Accepted for the narrow baseline-initialization scope.
- `CF-014` is resolved only for the local FE-01 snapshot identity/equality contract after accepted implementation evidence. PHP persistence parity remains unresolved and unclaimed.
- `CF-005` remains blocking for broad blueprint adoption. No blueprint schema or default may be copied implicitly.
- `CF-018`, `CF-020`, and `CF-024` remain app-projection boundaries. App ids, hashes, rows, runs, manifests, locks, and audit rows are not authority.
- `CF-021` permits downstream Screening to consume an accepted snapshot later; FE-01 does not migrate or reinterpret existing Screening inputs.
- Current Deduplication results and review pairs lack canonical authority digests. FE-01.1 must close that local gap without changing Search identity or historical fixtures.
- CorpusSnapshots and the dual content/record digest model are accepted; changing either after fixtures exist requires a successor ADR and migration classification.
- ResearchWorkspace currently has an analysis generation contract. Authority generations must remain distinguishable while reusing its proven staging/CAS/promotion mechanics.
- Crash tests can be platform-sensitive. Inject fault points around staging, promotion, quarantine, and pointer replacement rather than depending on timing.
- The only admitted authority source is the accepted `nexus.deduplication.authority-policy` record; no Protocol or app-owned hidden registry may substitute for it.

## Exact Verification Commands

Run from the repository root after implementation:

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~DeduplicationDecision|FullyQualifiedName~CorpusSnapshot|FullyQualifiedName~Provenance"
dotnet test tests/NexusScholar.ResearchWorkspace.Tests/NexusScholar.ResearchWorkspace.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AuthorityGeneration"
dotnet test tests/NexusScholar.AppServices.Tests/NexusScholar.AppServices.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~DecisionSnapshotAuthority"
dotnet test tests/NexusScholar.Architecture.Tests/NexusScholar.Architecture.Tests.csproj -c Release --no-build
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~DecisionSnapshotAuthority"
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
git diff --check
git status --short
```

Hosted protected-merge evidence must show the repository CI green on both Windows and Linux for the tested commit. CI must not call live scholarly providers or live models.

## Measurable Exit Checklist

- [x] ADR 0028 and this gate are Accepted by the manager before source implementation begins.
- [ ] One closed Deduplication authority-policy schema/version is documented and implemented.
- [ ] One closed Deduplication decision schema/version and stable error vocabulary are documented and implemented as persistence-independent contracts.
- [ ] Verified result, target, evidence, decision, decision-set, snapshot content, snapshot record, invalidation, provenance, manifest, and file digests reproduce from canonical inputs.
- [ ] Missing, unknown, or non-human actors cannot create final decisions.
- [ ] Decision corrections append superseding records; no authority record is edited or deleted.
- [ ] CorpusSnapshots exists as a focused non-packable domain owner with the accepted inward dependency direction.
- [ ] Every source candidate appears exactly once as grouped or unresolved; no-id candidates remain explicit and raw sightings remain reachable.
- [ ] Snapshot content equality and record equality are deterministic, distinct, and fixture-backed.
- [ ] Representative and membership relations are complete, non-conflicting, immutable, and evidence-bound.
- [ ] Stale workspace, source analysis manifest, result, target, policy, snapshot, decision-set, evidence, or manifest bindings reject verification or initialization as applicable.
- [ ] Policy, empty-decision-set baseline snapshot, snapshot-publication provenance event, authority manifest, and project pointer publish as one expected-generation ResearchWorkspace initialization transaction.
- [ ] No decision, invalidation, successor snapshot, or action reduction is published by ResearchWorkspace in FE-01.
- [ ] Crash and concurrent-writer cases leave the prior generation authoritative and verifiable.
- [ ] Save, reopen, and verify succeeds without database, UI state, or machine-local path authority.
- [ ] AppServices exposes read-only projections and existing decision actions remain locked.
- [ ] Architecture tests enforce every new dependency and forbidden-symbol rule.
- [ ] Valid, malformed, stale, tampered, invalid-transition, and automation-overreach fixtures pass on Windows and Linux.
- [ ] Historical and PHP-generated fixtures are unchanged.
- [ ] Full build, test, format, repository verification, and hosted CI are green for the same commit.
- [ ] Completion evidence records behavior, invariants, files, commands, totals, risks, ADR impact, conflict effect, migration effect, fixture effect, and compatibility impact.
- [ ] `CF-014`, `PLANS.md`, and the feature roadmap are updated only to the scope proven by completion evidence.

## Explicit Claims Not Made

- This accepted gate authorizes only the implementation paths and baseline initialization behavior written above; completion evidence is still pending.
- FE-01 does not append or execute merge, keep-separate, or mark-unresolved decisions; FE-02 owns that behavior.
- No generic decision abstraction is created.
- No broad blueprint adoption or conformance is claimed.
- No PHP, Nexus CLI, or Nexus Web compatibility or persistence parity is claimed.
- No Screening, Full Text, workflow execution, reporting, bundle, export, or citable-corpus behavior is implemented.
- No database, API, cloud, synchronization, multi-user, UI, desktop mutation, provider, network, plugin, or AI behavior is introduced.
- No production readiness, security sandbox, signature, non-repudiation, or institutional identity assurance is claimed.
