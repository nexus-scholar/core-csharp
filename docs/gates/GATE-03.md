# Gate 3: Protocol Lifecycle Planning

Status: planning decisions accepted; implementation and conformance fixtures pending. No protocol behavior implementation is included in this gate-planning pass.

## Goal

Freeze the protocol contract and approval semantics needed before implementing protocol lifecycle behavior.

Gate 3 must define draft, decision, approval, version, amendment, waiver, deviation, digest, and supersession rules without silently adopting unresolved blueprint defaults.

The planning decisions are:

- [ADR 0003: Protocol Record Contract](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/adr/0003-protocol-record-contract.md:1)
- [ADR 0004: Protocol Approval Semantics](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/adr/0004-protocol-approval-semantics.md:1)

## Source Of Truth Inputs

1. `AGENTS.md`
2. `PLANS.md`
3. `docs/adr/0001-source-of-truth-and-porting.md`
4. `docs/adr/0002-canonical-json-and-digests.md`
5. `docs/adr/0003-protocol-record-contract.md`
6. `docs/adr/0004-protocol-approval-semantics.md`
7. `docs/port/OPEN-CONFLICTS.md`
8. `docs/discovery/BLUEPRINT-AUDIT.md`
9. `fixtures/conformance/protocol-minimal.json`
10. current scaffold under `src/NexusScholar.Protocol`

## Conflicts With Accepted Local Gate 3 Decisions

### `CF-001`: Protocol Contract

The current fixture and model use a thin `subject + required_decisions + decisions` shape. The blueprint protocol schema requires version identifiers, template digest, intent, unresolved items, approvals, timestamps, amendment links, and full digest fields.

Planning decision accepted locally by `ADR 0003`. The current protocol scaffold and `protocol-minimal.json` remain non-authoritative until a fixture-backed implementation updates them. Blueprint protocol conformance remains unclaimed.

### `CF-008`: Approval Semantics

Planning decision accepted locally by `ADR 0004` for protocol approval semantics. Workflow, AI, plugin, and institutional approval engines remain future work.

## Dependency-Ordered Planning Tasks

1. `spec-owner`: compare current protocol scaffold, existing fixture, blueprint protocol schema/example, and product laws.
2. `governance-owner`: define protocol approval authority, actor requirements, and waiver/deviation boundaries.
3. `architecture-owner`: define the Kernel digest envelope shape used by protocol content without adding outward dependencies.
4. `conformance-owner`: specify positive and negative fixtures before implementation.
5. `gate-owner`: update `OPEN-CONFLICTS.md` only after `CF-001` and `CF-008` have explicit decisions or accepted ADR coverage.

## Required Fixtures And Negative Cases

- protocol draft with required decisions
- approved protocol version with `protocol-content` digest and immutable content
- amended protocol version preserving supersession links
- invalidation notice preserving downstream impact
- waiver included in approved protocol digest
- deviation linked to an approved version without mutating that version
- single approval accepted for an explicit custom local policy
- dual-independent approval accepted only with two distinct actors
- rejection for missing required decision
- rejection for blocking unresolved decision
- rejection for duplicate decision mutation
- rejection for post-approval mutation
- rejection for approval without authorized human actor
- rejection for approval with stale or mismatched digest
- rejection for using `approval-record` digest where `protocol-content` digest is required, or the reverse
- rejection for automation as approval authority
- rejection of old newline `key=value` digest material as non-authoritative protocol content

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

- `CF-001` has an accepted local protocol contract decision in `ADR 0003`; implementation and conformance fixtures remain pending.
- `CF-008` has an accepted approval semantics decision in `ADR 0004`; implementation and conformance fixtures remain pending.
- The protocol digest input shape uses Kernel digest primitives only.
- Required protocol fixtures and negative cases are listed before implementation.
- No protocol implementation was changed by this planning pass.
