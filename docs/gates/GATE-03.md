# Gate 3: Protocol Lifecycle Planning

Status: planning only. No protocol behavior implementation is authorized until `CF-001` and `CF-008` are frozen.

## Goal

Freeze the protocol contract and approval semantics needed before implementing protocol lifecycle behavior.

Gate 3 must define draft, decision, approval, version, amendment, waiver, deviation, digest, and supersession rules without silently adopting unresolved blueprint defaults.

## Source Of Truth Inputs

1. `AGENTS.md`
2. `PLANS.md`
3. `docs/adr/0001-source-of-truth-and-porting.md`
4. `docs/adr/0002-canonical-json-and-digests.md`
5. `docs/port/OPEN-CONFLICTS.md`
6. `docs/discovery/BLUEPRINT-AUDIT.md`
7. `fixtures/conformance/protocol-minimal.json`
8. current scaffold under `src/NexusScholar.Protocol`

## Blocking Conflicts To Resolve First

### `CF-001`: Protocol Contract

The current fixture and model use a thin `subject + required_decisions + decisions` shape. The blueprint protocol schema requires version identifiers, template digest, intent, unresolved items, approvals, timestamps, amendment links, and full digest fields.

Before implementation, Gate 3 must choose the local protocol record shape and define which fields are required for draft records, approved versions, and amendments.

### `CF-008`: Approval Semantics

Approval semantics are not frozen. Gate 3 must decide the minimum enforceable approval model for protocol approval, including actor identity, timestamp, content digest, role or authority requirement, and whether dual-independent approval is required, optional, or method-pack-specific.

## Dependency-Ordered Planning Tasks

1. `spec-owner`: compare current protocol scaffold, existing fixture, blueprint protocol schema/example, and product laws.
2. `governance-owner`: define protocol approval authority, actor requirements, and waiver/deviation boundaries.
3. `architecture-owner`: define the Kernel digest envelope shape used by protocol content without adding outward dependencies.
4. `conformance-owner`: specify positive and negative fixtures before implementation.
5. `gate-owner`: update `OPEN-CONFLICTS.md` only after `CF-001` and `CF-008` have explicit decisions or accepted ADR coverage.

## Required Fixtures And Negative Cases

- protocol draft with required decisions
- approved protocol version with actor, timestamp, digest, and immutable content
- amended protocol version preserving supersession links
- rejection for missing required decision
- rejection for duplicate decision mutation
- rejection for post-approval mutation
- rejection for approval without authorized actor
- rejection for approval with stale or mismatched digest
- explicit fixture for single-approval versus dual-independent approval once `CF-008` is frozen

## Allowed Paths During Planning

- `docs/gates/GATE-03.md`
- `docs/port/OPEN-CONFLICTS.md`
- new ADR under `docs/adr/` if needed for protocol contract or approval semantics
- planned fixture descriptions under `docs/port/`

## Excluded Until Planning Is Accepted

- `src/NexusScholar.Protocol`
- `tests/NexusScholar.Core.Tests`
- `tests/NexusScholar.Conformance.Tests`
- generated or changed protocol fixtures

## Verification Commands For The Later Implementation Gate

1. `dotnet build NexusScholar.Core.slnx -c Release`
2. `dotnet test NexusScholar.Core.slnx -c Release --no-build`
3. `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`
4. affected architecture tests
5. affected conformance tests

## Exit Checklist

- `CF-001` has an accepted local protocol contract decision.
- `CF-008` has an accepted approval semantics decision.
- The protocol digest input shape uses Kernel digest primitives only.
- Required protocol fixtures and negative cases are listed before implementation.
- No protocol implementation starts until the planning decisions are recorded.
