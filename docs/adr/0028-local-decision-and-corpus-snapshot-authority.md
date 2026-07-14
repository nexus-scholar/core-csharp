# ADR 0028: Local Decision And Corpus Snapshot Authority

- Status: Accepted
- Date: 2026-07-14
- Decision owner: Nexus Scholar maintainer/manager

## Context

The local Core can verify Deduplication results and display review-required pairs, but it cannot yet durably record an authorized Deduplication decision or publish an immutable scientific candidate snapshot. `ADR 0012` intentionally stops at evidence-backed clusters, representatives, unresolved candidates, and read-only review candidates. `ADR 0015` keeps AppServices decision actions locked. `ADR 0026` preserves PHP corpus-lock observations as non-adoption evidence and leaves general snapshot identity and equality unresolved under `CF-014`.

The missing authority boundary blocks later Deduplication execution, Screening conduct, workflow execution, reporting, and UI commands. A file path, current workspace projection, persisted row, PHP lock, Web membership hash, or ResearchWorkspace generation cannot fill this gap. A scientific mutation must bind a human actor, the exact source generation, exact target and evidence digests, an approved policy, and append-only provenance. A corpus snapshot must preserve raw sightings, unresolved no-id candidates, representative relationships, and deterministic equality without becoming a mutable current-state file.

Current code supports a narrow design:

- `NexusScholar.Deduplication` owns verified results, candidates, clusters, evidence, representatives, and review pairs.
- `NexusScholar.Kernel` owns canonical JSON, scoped SHA-256 digests, clocks, identifiers, and rehydration primitives.
- `NexusScholar.Provenance` owns append-only, digest-verified events without outward domain dependencies.
- `NexusScholar.ResearchWorkspace` owns expected-revision checks, staging, atomic generation promotion, verification, and quarantine.
- `NexusScholar.AppServices` is a read-only projection boundary.

There is not enough implemented cross-domain decision behavior to justify a generic decision framework. Screening and protocol decisions have distinct schemas and invariants. FE-01 therefore defines the first executable-authority foundation only for Deduplication and a focused corpus snapshot owner.

## Threat Model And Trust Boundary

FE-01 must defend the local authority record against:

- a missing, unknown, unauthorized, or non-human actor creating a final decision;
- a caller replaying a valid decision against a newer result, target, snapshot, decision set, or workspace generation;
- an id remaining unchanged while its bound content or evidence digest changes;
- omission of no-id or inconvenient candidates from a supposedly complete snapshot;
- representative projections deleting or obscuring raw sightings and member evidence;
- correction by overwrite, deletion, or an invalid supersession chain;
- tampering with canonical records, provenance, persisted files, or the generation manifest;
- partial publication after a crash or two writers both committing from one expected generation;
- a path, app row, current projection, PHP lock, or workspace manifest being substituted for scientific identity;
- automation, plugins, imports, or AI outputs being treated as human authority.

The controls are exact digest bindings, closed schemas, human policy authorization, append-only supersession, complete membership checks, immutable snapshots, verified rehydration, provenance, expected-generation compare-and-swap, and atomic ResearchWorkspace promotion. The local workspace and host process remain inside the trust boundary. This ADR does not claim protection from a malicious operating-system administrator, compromised process, stolen human credentials, or cryptographic non-repudiation; signatures and institutional identity assurance require later decisions.

## Present Requirements

FE-01 must provide all of the following before a later gate may execute merge, keep-separate, or unresolved commands:

1. A closed Deduplication-owned authority-policy record.
2. A Deduplication-owned, human-authored, append-only decision contract.
3. Canonical digest boundaries for a verified Deduplication result, a decision target, and cited evidence.
4. A focused, persistence-independent corpus snapshot record derived from one exact verified Deduplication result and one exact decision set.
5. Verified rehydration that rejects malformed, stale, tampered, incomplete, or non-canonical authority records.
6. Append-only invalidation and supersession contracts rather than edits or deletion.
7. Provenance events that bind authority initialization and later decision/snapshot inputs and outputs.
8. One ResearchWorkspace atomic generation that initializes authority by publishing the policy, an empty-decision-set baseline snapshot, its provenance event, its manifest, and the updated project pointer under expected-generation compare-and-swap.
9. Read-only AppServices projections of the committed baseline authority records.

The only production state transition admitted by FE-01 is `InitializeAuthorityGeneration`. It accepts one exact verified Deduplication result, one verified local authority policy, the authorized human publisher, and stale-write expectations. It publishes a baseline snapshot with an empty decision set. It does not append a Deduplication decision, reduce a merge/keep-separate/unresolved action, change group membership, resolve a review pair, or invalidate downstream records.

Decision, supersession, and invalidation records are implemented and replayed as persistence-independent domain contracts and deterministic fixtures. Production decision append, successor snapshot publication, and action reduction remain locked until FE-02 is accepted.

## Decision

### 1. Module Ownership And Dependency Direction

`NexusScholar.Deduplication` owns the first decision contract because its targets, action vocabulary, policies, and evidence are Deduplication-specific. It may continue to depend only on Kernel, Shared, and Search.

A new focused domain project, `NexusScholar.CorpusSnapshots`, owns corpus snapshot, snapshot content, supersession, equality, and snapshot invalidation records. It may depend inward on Kernel and Deduplication. Deduplication must not reference CorpusSnapshots. The project is non-packable in FE-01 and is excluded from the twelve-package validation topology. Making it packable requires a successor package-topology decision and release evidence.

`NexusScholar.Provenance` remains Kernel-only. It receives canonical entity references from orchestration and does not reference Deduplication or CorpusSnapshots.

`NexusScholar.ResearchWorkspace` is the local persistence and atomic publication adapter. It may depend inward on Deduplication, CorpusSnapshots, and Provenance. It does not define scientific identity, recalculate policy outcomes, or reinterpret a workspace generation as a corpus snapshot.

`NexusScholar.AppServices` may depend on the admitted domain contracts only to construct read-only projections. It must not persist, authorize, or execute a decision.

No generic `Decision`, `DecisionStore`, or cross-domain decision base type is introduced. Existing Kernel identifiers, canonical JSON, digests, clocks, and guards may be shared; domain meaning remains with the owning module.

### 2. Deduplication Result, Target, And Evidence Authority

Before a decision can be accepted, Deduplication must produce or rehydrate a verified source result and reproduce a canonical result digest. FE-01 defines four local digest-material schemas:

- `nexus.deduplication.result` version `1.0.0` for the complete accepted result;
- `nexus.deduplication.candidate` version `1.0.0` for one complete raw candidate and sighting binding;
- `nexus.deduplication.evidence` version `1.0.0` for one evidence record;
- `nexus.deduplication.review-target` version `1.0.0` for one review-candidate pair and its bound candidate/evidence material.

All four use digest scope `canonical-json-record`. The result digest uses:

- schema id `nexus.deduplication.result` and schema version `1.0.0`;
- digest scope `canonical-json-record`;
- all semantically authoritative result fields, including policy, ordered source bindings, raw candidates and sightings, clusters, representatives, evidence, unresolved candidates, review pairs, warnings, errors, and non-claims;
- the deterministic ordering rules owned by the result schema.

The result id is included because it identifies the exact generation. Incidental object identity, paths, serializer defaults, and workspace generation ids are excluded unless an admitted source binding makes them scientific content.

A decision target is a Deduplication-owned descriptor with a schema id/version, target kind, stable target id, source result id/digest, ordered candidate ids, and target content digest. FE-01 admits `review-candidate-pair` as the first target kind. Candidate ids are ordered by ordinal string order before canonicalization; duplicate or missing candidates are rejected. The target digest covers the exact candidate content and relevant review-pair values from the verified result, not only the ids.

Each cited evidence reference contains an evidence kind, stable evidence id, digest scope, and content digest. Rehydration resolves every reference against the bound verified result and reproduces its digest. A matching id with a mismatched digest is stale or tampered authority.

Canonical collection order is closed as follows:

- provider-priority entries by provider key ordinal;
- source Search and import trace ids ordinal;
- raw candidates, unresolved candidates, clusters, and result evidence by their stable ids ordinal;
- work ids, source-specific ids, source-sighting ids, source-file digests, digest scopes, raw-record digests, reason codes, keywords, and non-claims ordinal;
- authors preserve recorded source order because author order is semantic;
- parser warnings and record notices by category, source-record id, record index, then message ordinal;
- cluster members by candidate id and cluster evidence by evidence id;
- review pairs after each pair is normalized to ordinal candidate A/B order, then by candidate A, candidate B, threshold, and score;
- warnings and errors by category then message ordinal;
- decision evidence references by evidence kind, evidence id, digest scope, then digest ordinal.

Rehydration rejects non-canonical order rather than silently sorting persisted authority records. Producers may normalize transient inputs before constructing the immutable record.

### 3. Local Deduplication Authority Policy

The only FE-01 authority source kind is `local-deduplication-authority-policy`. Its schema is:

```text
schema_id = nexus.deduplication.authority-policy
schema_version = 1.0.0
digest_scope = canonical-json-record
```

The policy contains `policy_id`, `policy_version`, the fixed authority-source kind, an ordinal list of authorized human actor/role pairs, an ordinal list of allowed decision actions, policy-defined reason codes grouped by action, `requires_rationale`, `issued_by_actor_id`, `issued_by_role`, canonical UTC `issued_at`, optional `supersedes_policy_id` and `supersedes_policy_digest`, and `policy_digest`.

Authorized actor/role pairs are ordered by actor id then role. Actions use the closed order `merge`, `keep-separate`, `mark-unresolved`. Reason-code groups use that action order and reason codes are ordinal within each group. Duplicate actors, pairs, actions, or reason codes are rejected. The issuer must appear in the authorized actor/role list and is asserted by this local policy to be human. FE-01 verifies that assertion and policy self-consistency but makes no institutional identity, credential, signature, or non-repudiation claim.

Policy digests cover every field except `policy_digest`. A superseding policy is a new immutable record; no policy is edited. FE-01 production initialization accepts only an initial policy with no predecessor. FE-02 or a successor ADR must define policy rotation against an active authority generation.

Decision construction requires an actor/role pair present in the exact bound policy, an allowed action, an admitted reason code for that action, and non-blank rationale when required. There is no hidden registry or app-owned resolver. A matching policy id with a mismatched digest, an unlisted actor/role, or a stale superseded policy is rejected with a stable authority error category.

### 4. Deduplication Decision Record

The first decision schema is:

```text
schema_id = nexus.deduplication.decision
schema_version = 1.0.0
digest_scope = canonical-json-record
```

The record contains:

- `decision_id`;
- `schema_id` and `schema_version`;
- `action_type`: `merge`, `keep-separate`, or `mark-unresolved`;
- `policy_id` and `policy_version`;
- `target_kind`, `target_id`, and `target_content_digest`;
- `source_result_id` and `source_result_digest`;
- optional `source_snapshot_id` and `source_snapshot_record_digest`, required when correcting or advancing an existing snapshot;
- ordered `evidence_references`, each with kind, id, digest scope, and digest;
- `actor_id`, `actor_role`, `authority_source_id`, `authority_source_kind`, and `authority_source_digest`;
- `rationale` and a closed, policy-defined `reason_code`;
- canonical UTC `decided_at` supplied by an injected clock;
- optional `supersedes_decision_id`;
- ordered `invalidation_effects`, each naming a stable downstream record kind, id, and digest;
- `decision_digest`.

The decision digest covers every field above except `decision_digest` itself. Human-authored text is NFC-normalized. Omission and null remain distinct. Arrays are never silently sorted; producers must emit the schema-defined canonical order.

Final decision creation requires an identified human actor whose actor/role pair is present in the exact `nexus.deduplication.authority-policy` record. `authority_source_kind` must be `local-deduplication-authority-policy`; the source id and digest must equal the bound policy id and policy digest. Missing, blank, unknown, system, automation, plugin, import, or AI actors are rejected. Structural rehydration verifies the recorded authority binding; command-time authorization verifies the exact policy before any future append.

Decision records are immutable and append-only. A correction creates a new decision with `supersedes_decision_id`; it never edits or deletes the earlier record. A supersession chain must be acyclic, must remain within the same target lineage, and must bind the then-current source snapshot and generation. Multiple active decisions for the same target and policy are invalid unless the later record explicitly supersedes the active one.

### 5. Corpus Snapshot Record

The focused snapshot schemas are:

```text
schema_id = nexus.corpus.snapshot
schema_version = 1.0.0
content_digest_scope = canonical-json-record
record_digest_scope = canonical-json-record
```

The snapshot record contains:

- `snapshot_id`;
- `schema_id` and `schema_version`;
- `source_result_id` and `source_result_digest`;
- ordered `decision_references`, each with decision id and decision digest;
- `decision_set_digest`, computed over decision references sorted by ordinal decision id;
- ordered `groups`, each with group id, representative candidate id, ordered member candidate ids, and ordered evidence references;
- ordered `unresolved_candidates`, each with candidate id, unresolved reason, raw-sighting references, and candidate content digest;
- `created_by_actor_id`, `created_by_role`, `authority_source_id`, and `authority_source_digest`;
- canonical UTC `created_at` supplied by an injected clock;
- optional `supersedes_snapshot_id` and `supersedes_snapshot_record_digest`;
- ordered `invalidation_references`;
- `content_digest`;
- `record_digest`.

Stable work identity is retained where available, but membership identity is the Deduplication candidate id bound to candidate content and the source result digest. A no-id candidate remains explicitly unresolved. It is never assigned a fabricated work id and may not be silently omitted from the snapshot. Every source result candidate must appear exactly once as a grouped member or unresolved candidate.

Representative relationships are projections recorded inside the immutable snapshot. They do not replace or delete members. Raw sightings remain preserved in the bound Deduplication result and are addressable through snapshot evidence references. Snapshot construction rejects a representative outside its group, duplicate membership, conflicting groups, omitted candidates, duplicated unresolved candidates, or evidence that cannot be reproduced from the bound result.

Snapshot ordering is closed: decision references by decision id; groups by group id; group members by candidate id; group evidence by evidence kind, id, scope, then digest; unresolved candidates by candidate id; raw-sighting references ordinal; and invalidation references by record kind, record id, then digest. A group id is deterministic: `group-` followed by the lowercase SHA-256 hex of canonical JSON containing only its ordered member candidate ids. A singleton candidate therefore receives a stable singleton group id. Persisted non-canonical order or a mismatched group id is rejected.

`created_by_actor_id` and `created_by_role` identify the human publishing actor for that snapshot, not the author of every cited decision. Each decision reference retains its own actor and policy authority. The publishing actor must be authorized by the exact bound local policy. FE-01 baseline publication uses the policy issuer as the publishing actor and an empty decision-reference list.

The `content_digest` covers the schema, source result binding, decision-set binding, groups, membership, representatives, evidence references, and unresolved candidates. It excludes snapshot id, creation authority/time, supersession, invalidation references, and both digest values. Two verified snapshots are scientifically content-equal only when their schema id/version and `content_digest` are equal.

The `record_digest` covers all snapshot record fields except `record_digest` itself. It includes snapshot id, creator, creation time, supersession, invalidation references, and the reproduced `content_digest`. Record equality requires matching snapshot id and record digest. This split permits deterministic corpus-content equality without allowing creation authority or history to be altered unnoticed.

### 6. Immutability, Supersession, And Invalidation

A published decision, snapshot, or invalidation record is never overwritten. A corrected decision creates a successor decision and a successor snapshot. A successor snapshot cites the exact predecessor record digest.

Any future change to membership, representative relation, unresolved status, source result, or active decision set invalidates the prior snapshot. Invalidation is represented by an append-only `nexus.corpus.snapshot-invalidation` version `1.0.0` record containing its own id, cause decision/snapshot references and digests, invalidated record references and digests, human actor/authority binding, UTC timestamp, and canonical record digest.

FE-01 closes the invalidatable record-kind vocabulary to `deduplication-decision` and `corpus-snapshot`. It implements and fixture-tests this record but does not publish one during baseline initialization. Screening, Full Text, report, bundle, workflow, and app-projection invalidation kinds are not admitted. FE-02 or a later domain gate must add each downstream kind with its resolver and semantics.

Invalidation calculation is deterministic from the accepted command, source snapshot, known admitted bindings, and resulting snapshot. It may mark records stale; it may not delete, rewrite, or silently regenerate them. Unknown downstream records remain untouched and cannot be claimed current against a successor snapshot.

### 7. Stale-Generation Rejection

FE-01 baseline authority initialization requires all of these expectations to match the committed workspace state:

- expected workspace revision;
- expected current analysis generation id and raw authority-source analysis-manifest digest;
- expected source Deduplication result id and digest;
- an absent current authority-generation pointer.

A mismatch rejects the entire command as stale. The implementation must not refresh the caller's values and retry implicitly. Two writers against the same expected generation cannot both commit.

### 8. Persistence Boundary And Atomic Generation

Domain records are persistence-independent canonical records. They contain no absolute paths, database ids, ORM state, UI objects, or host handles.

FE-01 extends `nexus.project.v0` with three optional fields that must be either all absent or all present:

- `currentAuthorityGenerationId`;
- `authorityGenerationManifestPath`;
- `authorityGenerationManifestSha256`.

Existing workspaces deserialize with all three values absent and remain readable and decision-locked. Baseline initialization increments the project revision once and sets all three fields. The existing analysis generation fields remain unchanged and continue to identify analysis output only. While an authority pointer exists, import and analysis mutations reject with a stable `authority-generation-active` category until FE-02 defines successor/invalidation behavior; they do not silently clear or carry the authority pointer.

The authority manifest schema is `nexus.workspace-authority-generation.v1`. It contains schema, authority generation id, workspace id, committed project revision, source analysis generation id, raw SHA-256 of the source analysis manifest bytes, source Deduplication result id/digest, predecessor authority generation id/manifest digest (both null for FE-01), active authority policy id/digest, decision-set digest, and artifact entries ordered by name. Each artifact entry contains name, workspace-relative path, and raw-file SHA-256.

ResearchWorkspace persists `InitializeAuthorityGeneration` as one staged authority generation. The promoted generation contains exactly:

- the immutable local authority-policy record;
- the empty-decision-set baseline snapshot record;
- the snapshot-publication provenance event;
- an authority-generation manifest with ordered paths and raw-byte digests for every file plus the expected predecessor bindings.

The baseline `decision_set_digest` is the `canonical-json-record` digest of the canonical empty decision-reference array. No decision ledger, decision record, invalidation record, or downstream mutation is published by FE-01.

Every FE-01 authority file, including the authority manifest, is emitted as `CanonicalJsonSerializer.ProfileId` canonical UTF-8 bytes with no BOM and no trailing newline. ResearchWorkspace must not use its ordinary indented `System.Text.Json` writer for these files. Domain record digests use their declared `DigestEnvelope`; manifest artifact digests use SHA-256 over the exact canonical file bytes.

The workspace lock and expected-revision compare-and-swap are acquired before final validation. Every domain record is rehydrated and every domain and file digest is reproduced from staged bytes before promotion. The authority generation directory is atomically renamed, and `nexus.project.json` is atomically replaced last as the commit pointer. A crash, failed validation, stale generation, or partial promotion leaves the prior generation authoritative and moves failed promoted output to quarantine where applicable.

ResearchWorkspace generation identity and manifest digests verify atomic publication only. They are not corpus snapshot identity, scientific equality, actor authority, or provenance.

### 9. Provenance Binding

The committed baseline generation includes one digest-verified `ADR 0008` `corpus-snapshot-published` event. It uses the authorized human publisher as agent, cites the source result, local authority policy, source analysis manifest, and empty decision set as inputs, and cites the baseline snapshot as output.

The domain contracts also define fixture expectations for later events:

- `deduplication-decision-recorded` cites policy, result, target, source snapshot when present, and evidence as inputs and the decision as output;
- `corpus-snapshot-published` cites policy, result, active decision set, and prior snapshot when present as inputs and the new snapshot as output;
- `corpus-snapshot-invalidated` cites the cause decision and successor snapshot as inputs and the invalidation record as output.

Protocol binding is required when the decision claims protocol-authorized conduct and must include the exact approved protocol version and `protocol-content` digest. A local policy decision that is not protocol-governed must say so explicitly and cannot claim protocol compliance. Provenance event append and authority-generation publication are one ResearchWorkspace transaction, while Provenance retains ownership of event validation and digest rules.

### 10. Rehydration And Verification

Every persisted authority-bearing record enters through an unverified wrapper and a verified rehydrator. Public callers cannot construct a verified record directly. Rehydration must:

- require exact known schema ids and versions;
- enforce canonical ids, UTC timestamps, NFC text, digest syntax/scopes, closed action/reason vocabularies, and canonical collection order;
- reproduce result, target, evidence, decision-set, snapshot content, snapshot record, invalidation, provenance, manifest, and raw-file digests;
- resolve all ids against the exact bound source records;
- reject duplicate ids, dangling references, cycles, conflicting active decisions, duplicate/omitted membership, and representative errors;
- reject stale source result, source snapshot, decision set, or workspace generation bindings;
- return deep immutable snapshots that do not retain caller-owned mutable collections.

Verification is persistence-independent for domain records. A valid record must verify from its canonical bytes and explicitly supplied bound records without access to a database, current UI state, or machine-local path.

## Alternatives Considered

### Introduce A Generic Cross-Domain Decision Module

Rejected. Protocol, Screening, and Deduplication currently have different target, policy, conflict, and supersession semantics. Generalization now would be speculative and could weaken domain-specific authority checks.

### Put Decisions And Snapshots In ResearchWorkspace

Rejected. ResearchWorkspace owns local atomic publication, not scientific meaning. Making files or generation manifests authoritative would couple domain identity to persistence and conflict with ADRs 0016, 0023, and 0026.

### Put Corpus Snapshots In Deduplication

Rejected. Deduplication owns evidence and decisions, but snapshots become inputs to Screening and later reporting. A focused inward-dependent owner prevents Deduplication from acquiring downstream lifecycle responsibilities.

### Adopt PHP Locks, Laravel Rows, Or Web Membership Hashes

Rejected. They are compatibility or app evidence under ADRs 0012 and 0026, not accepted C# authority. They also mix persistence and projection concerns into the Core contract.

### Mutate A Current Snapshot In Place

Rejected. It destroys reconstructability, obscures corrections, and cannot support deterministic downstream invalidation.

### Omit No-Id Candidates Until Identity Is Known

Rejected. Omission would erase source evidence and create a falsely complete corpus. No-id candidates remain unresolved and explicitly represented.

## Consequences

Positive:

- FE-02 receives a narrow, actor-bound foundation for the first executable scientific decision.
- Snapshot content equality, record integrity, supersession, and persistence are distinct and testable.
- Raw sightings and unresolved candidates remain reconstructable.
- Domain authority stays independent of files, app rows, databases, and UI state.
- Atomic local baseline publication extends the accepted ResearchWorkspace generation mechanism without redefining it as scientific authority.

Negative:

- FE-01 requires a new non-packable domain project and several explicit canonical schemas before user-visible decision execution exists.
- Deduplication must define canonical result, target, candidate, and evidence digest material that current DTOs do not expose.
- A dual content/record digest model adds verification work but is necessary to separate scientific equality from record provenance.
- AppServices remains read-only; FE-02 is still required for an operator command.

## Migration Effect

No existing Deduplication result, workspace output, PHP snapshot, Web row, or APP-01 projection is silently upgraded to authority.

Existing verified Deduplication results may be admitted as FE-01 sources only after the new canonical result digest is reproduced from their complete accepted schema. Existing workspaces without an authority-generation pointer remain readable and decision-locked. Their first FE-01 publication creates a baseline authority generation and adds the three optional authority-pointer fields; it does not rewrite historical analysis generations. Import and analysis reject while that pointer is active until FE-02 admits an explicit successor path.

Existing APP-01 locked actions remain locked. A later FE-02 migration may route admitted actions through the FE-01 contracts without changing historical read-only projections into decisions.

## Fixture Effect

FE-01 requires a new local C# fixture family with deterministic ids and clocks. It must not edit Gate 9, Phase 7, PHP-generated, or historical fixtures. Required fixture cases are enumerated in `docs/gates/FE-01-DECISION-SNAPSHOT-AUTHORITY.md`.

Fixtures must record schema ids/versions, canonicalization profile, digest scopes, generator command/version, source input digests, expected output digests, and whether they are local contract or negative authority cases. PHP observations may be cited as evidence but are not expected outputs. No fixture may be hand-edited to make an implementation pass.

## PHP, App, And Blueprint Evidence Boundaries

- PHP corpus locks, snapshots, export metadata, ids, timestamps, and persistence behavior are evidence only. This ADR makes no PHP compatibility or parity claim.
- Nexus Web runs, membership hashes, representative scoring, locked snapshots, audit rows, and database rows are app projections unless explicitly transformed into these verified records.
- Nexus CLI run files, latest pointers, paths, and display hashes are not authority.
- Blueprint schemas, method packs, examples, and defaults remain discovery inputs under `CF-005`. This ADR adopts only the local schemas and invariants written here.
- Existing Screening candidate-set language is downstream evidence. FE-01 does not revise Screening decisions or claim that every existing Screening input is an FE-01 snapshot.

## Reversal Conditions

Revisit this ADR only if:

1. implementation evidence shows that a focused CorpusSnapshots module creates an unavoidable dependency cycle;
2. at least two implemented decision domains demonstrate a stable shared contract that warrants extracting shared primitives without weakening their invariants;
3. accepted cross-language fixtures justify adopting a different snapshot equality or digest rule with an explicit migration plan;
4. a database, synchronization, or multi-user ADR replaces the local persistence adapter while preserving domain records, expected-generation semantics, and append-only history;
5. an accepted policy requires different human authority or correction semantics and states how existing decisions remain verifiable.

Reversal requires a successor ADR, migration and fixture classification, and no reinterpretation of historical ResearchWorkspace generations as corpus snapshots.

## Explicit Claims Not Made

- No FE-01 implementation is provided by this ADR.
- No merge, keep-separate, or unresolved command is executable until FE-02.
- No generic decision framework is accepted.
- No broad blueprint adoption or conformance is claimed.
- No PHP, Nexus CLI, or Nexus Web compatibility is claimed.
- No database, API, cloud, synchronization, multi-user, or production persistence is introduced.
- No UI or desktop mutation command is introduced.
- No live provider, network retrieval, plugin runtime, AI/model call, or AI acceptance is introduced.
- No Screening, Full Text, reporting, bundle, workflow-execution, or citable-export behavior is implemented.
- No security sandbox, cryptographic signature, non-repudiation, or institutional identity proof is claimed.
