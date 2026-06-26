# Gate 2: Deterministic Kernel

Status: entry checklist only. No Gate 2 implementation is implied by this document.

## Goal

Implement deterministic kernel primitives for identifiers, clocks, canonical serialization, digests, and related error handling without claiming protocol, bundle, blueprint, or PHP compatibility.

## Entry Checklist

- `ADR 0002` is accepted: [0002-canonical-json-and-digests.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/adr/0002-canonical-json-and-digests.md:1)
- Gate 1 repository-quality evidence exists and remains valid: [GATE-01-EVIDENCE.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/gates/GATE-01-EVIDENCE.md:1)
- `CF-009` is narrowed from “no local rule exists” to “local rule exists, implementation and fixtures pending”
- Fixture plan still governs Gate 2 fixture work: [GOLDEN-FIXTURE-PLAN.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/port/GOLDEN-FIXTURE-PLAN.md:1)
- Gate 2 scope is limited to kernel determinism and digest rules only

## Required Gate 2 Work Before Exit

1. Implement canonical JSON serialization for the approved local profile `rfc8785-jcs`.
2. Implement digest validation and rendering for `sha256:<64 lowercase hex>`.
3. Replace ad hoc digest material with typed digest scopes.
4. Preserve omitted-vs-null semantics instead of silently collapsing them.
5. Add fixed test vectors for:
   - property ordering
   - arrays
   - timestamps
   - Unicode NFC normalization
   - unsupported numbers
   - raw byte hashing
   - NDJSON LF handling
6. Add deterministic conformance fixtures with generator metadata for kernel digest behavior.
7. Prove stable output on both Windows and Linux.

## Gate 2 Non-Goals

- protocol contract adoption
- blueprint conformance
- PHP compatibility
- PHP-generated fixtures
- bundle parity
- provenance parity
- AI governance parity beyond kernel prerequisites

## Risks Still Blocking Gate 2 Exit

- No canonical JSON implementation exists yet.
- No Gate 2 canonical digest fixtures exist yet.
- Current protocol digest remains provisional scaffold behavior.
- Current bundle manifest and verifier remain thinner than any future bundle authority.
- Downstream conflicts remain open:
  - `CF-001` protocol contract
  - `CF-002` bundle contract
  - `CF-004` provenance and AI governance
  - `CF-005` blueprint authority
  - `CF-006` missing schema closure
  - `CF-008` approval semantics
  - `CF-014` snapshot identity and equality

## Exit Standard

Gate 2 may exit only when deterministic kernel behavior is implemented, fixture-backed, and verified cross-platform without claiming any higher-level compatibility than this gate explicitly covers.
