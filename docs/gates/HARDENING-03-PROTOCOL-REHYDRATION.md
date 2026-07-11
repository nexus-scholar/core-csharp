# Hardening 03: Protocol authority-safe rehydration

Status: accepted for implementation

## Goal

Ensure persisted Protocol approval and version records can become authoritative domain state only after their identities, actors, template-selected policy, approval bindings, and canonical digests have been independently resolved and verified.

Scientific behavior targeted: an approved protocol cannot be fabricated by supplying an `approved` status, digest text, and approval identifiers to a public constructor.

## Sources

1. `AGENTS.md`
2. `docs/adr/0002-canonical-json-and-digests.md`
3. `docs/adr/0003-protocol-record-contract.md`
4. `docs/adr/0004-protocol-approval-semantics.md`
5. `docs/adr/0017-canonical-json-profile-correction.md`
6. `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`
7. Hardening 02 verified Kernel digest-envelope boundary

## Dependency-Ordered Tasks

1. Protocol owner: make direct `ProtocolVersion` authority construction non-public while preserving domain-owned candidate, approval, and supersession transitions.
2. Protocol owner: define unverified approval and protocol-version inputs that carry persisted claims without granting authority.
3. Protocol owner: define resolvers for the template-selected approval policy and referenced approval records.
4. Protocol owner: validate and rehydrate approval records by rebuilding the approval-record envelope and recomputing its digest.
5. Protocol owner: validate version content, reject duplicate identities and blocking unresolved decisions, rebuild the protocol-content envelope, and recompute its digest.
6. Protocol owner: resolve every approval ID, reject missing or extra authority, enforce exact target/content/policy/actor bindings, and apply the accepted policy rules.
7. Test and fixture owner: add positive single/dual cases and malformed, stale, unresolved, duplicate, missing-resolution, weaker-policy, non-human, wrong-target, and tampered-digest cases.
8. Gate owner: run focused, full, architecture, conformance, formatting, repository, scientific-invariant, and hosted CI verification.

## Required Cases

- an approved single-researcher version rehydrates only with its exact template-selected policy and verified approval;
- a dual-independent version requires two distinct verified human approvals;
- direct external construction of `ProtocolVersion` is unavailable;
- tampered protocol content or content digest is rejected;
- tampered approval content or approval-record digest is rejected;
- missing, extra, duplicate, unresolved, wrong-target, stale-digest, wrong-policy, or non-human approvals are rejected;
- a resolver cannot downgrade the policy selected for the pinned template;
- duplicate required-decision keys, decision identities, waiver identities, unresolved identities, or approval IDs are rejected;
- blocking unresolved decisions cannot enter approved state;
- successful output exposes verified approval and version state without retaining mutable caller collections.

## Allowed Paths

- `src/NexusScholar.Protocol/ProtocolModels.cs`
- `src/NexusScholar.Protocol/ProtocolDraft.cs`
- `src/NexusScholar.Protocol/ProtocolRehydration.cs`
- focused Protocol construction call sites required by constructor visibility
- `tests/NexusScholar.Core.Tests/ProtocolTests.cs`
- focused Workflow and Bundle test helpers required by constructor visibility
- `tests/NexusScholar.Conformance.Tests/ProtocolFixtureTests.cs`
- focused Workflow and Bundle conformance helpers required by constructor visibility
- `fixtures/conformance/protocol/`
- `docs/gates/HARDENING-03-PROTOCOL-REHYDRATION.md`
- `docs/gates/HARDENING-03-PROTOCOL-REHYDRATION-EVIDENCE.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`

## Excluded Paths

- waiver, amendment, deviation, or method-pack feature expansion
- persistence, API, CLI, UI, provider, or workspace adapters
- Workflow, Provenance, Bundle, Screening, and Full Text production behavior
- existing golden-output regeneration
- PHP fixtures or compatibility claims
- production dependencies

## Risks And Decisions

- Rehydration validates the accepted Protocol records; it does not define a persistence or API schema.
- The resolver selects policy from pinned template identity. The unverified record's policy ID is only a claim and cannot choose a weaker policy.
- Approval records remain outside the protocol-content digest but must independently reproduce their approval-record digests.
- Existing test-only direct construction must move to domain factories or internal test access; production consumers receive no public fabrication path.
- This gate does not complete waiver/amendment/deviation authority transitions identified by the review.
- No ADR is required because ADRs 0003 and 0004 already define the authority and digest rules.

## Verification

```powershell
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore
dotnet test tests/NexusScholar.Core.Tests/NexusScholar.Core.Tests.csproj -c Release --no-build --filter ProtocolTests
dotnet test tests/NexusScholar.Conformance.Tests/NexusScholar.Conformance.Tests.csproj -c Release --no-build --filter ProtocolFixtureTests
dotnet test tests/NexusScholar.Architecture.Tests/NexusScholar.Architecture.Tests.csproj -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
git diff --check
```

## Exit Checklist

- [x] No public path can fabricate approved Protocol state.
- [x] Every persisted authority claim is independently resolved and digest-verified.
- [x] Template-selected policy cannot be downgraded by rehydrated input.
- [x] Every required positive and negative case has a permanent test or fixture.
- [x] Existing fixture digests remain unchanged.
- [x] Only allowed paths changed.
- [x] Focused and full verification commands pass.
- [x] Scientific-invariant review is clear.
- [x] Evidence records behavior, commands, totals, risks, ADR impact, and compatibility impact.

Completion evidence: `docs/gates/HARDENING-03-PROTOCOL-REHYDRATION-EVIDENCE.md`.
