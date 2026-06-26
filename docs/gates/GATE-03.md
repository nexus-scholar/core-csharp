# Gate 3: Protocol Lifecycle

Status: implementation complete locally; closeout hosted CI pending for the latest branch head.

## Goal

Implement the local protocol lifecycle contract accepted in:

- [ADR 0003: Protocol Record Contract](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/adr/0003-protocol-record-contract.md:1)
- [ADR 0004: Protocol Approval Semantics](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/adr/0004-protocol-approval-semantics.md:1)

Gate 3 implements protocol draft, decision, approval, version, amendment, waiver, deviation, digest, and supersession behavior without claiming PHP compatibility, blueprint conformance, bundle contract adoption, workflow compiler behavior, persistence/API schema support, provenance parity, or AI governance parity.

## Implemented Scope

- Protocol statuses: `draft`, `ready_for_review`, `approved`, `superseded`, `withdrawn`.
- Draft contract fields: protocol id, draft id, project id, status, template, intent, values, required decisions, decisions, unresolved decisions, waivers, creator, timestamps, and optional base version id.
- Required decision definitions with approval gate and source requirement identity.
- Protocol decisions with canonical JSON values, human actor, timestamp, optional rationale, source proposal digest, and supersession link.
- Blocking unresolved decisions that prevent approval.
- Nonblocking unresolved decisions preserved in approved protocol content.
- Approved protocol versions with Kernel `DigestEnvelope` using `DigestScope.ProtocolContent`.
- Approval records with Kernel `DigestEnvelope` using `DigestScope.ApprovalRecord`.
- Approval records bound to protocol id, version id, content digest, policy id, policy version, policy mode, actor, timestamp, and approval-record digest.
- Explicit single-researcher local approval fallback.
- Dual-independent approval with distinct actor enforcement.
- Stale digest, wrong target, wrong policy, non-human approval, and automation approval rejection.
- Protocol amendments preserving previous content digest, changed decision keys, invalidation notices, and supersession links.
- Waivers as protocol content included before approval digest computation.
- Deviations linked to approved versions without mutating version digests.
- Stable protocol error categories for Gate 3 failure modes, including missing/invalid approval actor and policy binding failures.

## Fixture Pack

Gate 3 fixtures live under `fixtures/conformance/protocol/`.

Positive fixtures:

- `protocol-draft-valid-v1.json`
- `protocol-approved-single-v1.json`
- `protocol-approved-dual-v1.json`
- `protocol-amended-v1.json`
- `protocol-waiver-valid-v1.json`
- `protocol-deviation-valid-v1.json`

Negative fixtures cover:

- `missing-required-decision`
- `blocking-unresolved-decision`
- `duplicate-decision`
- `post-approval-mutation`
- `unauthorized-approval`
- `stale-content-digest`
- `invalid-amendment`
- `invalid-waiver`
- `invalid-deviation`
- `same-actor-dual-approval`
- `automation-cannot-approve`
- wrong digest scope
- non-human approval actor
- old newline `key=value` digest material

The older `fixtures/conformance/protocol-minimal.json` remains discovery-only and is not a Gate 3 authority fixture.

## Source Of Truth Inputs

1. `AGENTS.md`
2. `src/NexusScholar.Protocol/AGENTS.md`
3. `docs/adr/0001-source-of-truth-and-porting.md`
4. `docs/adr/0002-canonical-json-and-digests.md`
5. `docs/adr/0003-protocol-record-contract.md`
6. `docs/adr/0004-protocol-approval-semantics.md`
7. `docs/port/OPEN-CONFLICTS.md`
8. `fixtures/conformance/protocol-minimal.json` as discovery-only prior art
9. current C# implementation under `src/NexusScholar.Protocol`

## Explicit Non-Claims

- no PHP compatibility claim
- no blueprint conformance claim
- no bundle contract adoption
- no persistence or API schema commitment
- no workflow compiler implementation
- no provenance parity claim
- no AI governance parity claim
- no institutional role-engine implementation

## Verification

Local evidence is recorded in [GATE-03-EVIDENCE.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/gates/GATE-03-EVIDENCE.md:1).

Required verification commands:

1. `dotnet restore NexusScholar.Core.slnx`
2. `dotnet build NexusScholar.Core.slnx -c Release --no-restore`
3. `dotnet test NexusScholar.Core.slnx -c Release --no-build`
4. `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`
5. `scripts/verify.ps1`

## Exit Checklist

- `CF-001` is implemented for local Gate 3 protocol lifecycle behavior.
- `CF-008` is implemented for local Gate 3 approval semantics.
- Protocol digest material uses Kernel digest primitives only.
- Approval records bind to protocol content digests but remain outside protocol-content digest material.
- Fixtures and tests cover positive lifecycle behavior and listed negative categories.
- Gate 3 remains bounded to protocol lifecycle only.
