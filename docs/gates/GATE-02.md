# Gate 2: Deterministic Kernel

Status: accepted for local deterministic-kernel scope only. No blueprint, protocol, bundle, provenance, or PHP compatibility claim is implied by this document.

## Goal

Implement deterministic kernel primitives for canonical serialization, digests, timestamps, normalization, and related validation without claiming protocol, bundle, blueprint, or PHP compatibility.

## Entry Checklist

- `ADR 0002` is accepted: [0002-canonical-json-and-digests.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/adr/0002-canonical-json-and-digests.md:1)
- Gate 1 repository-quality evidence exists and remains valid: [GATE-01-EVIDENCE.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/gates/GATE-01-EVIDENCE.md:1)
- `CF-009` is narrowed from “no local rule exists” to “local rule exists, implementation and fixtures pending”
- Fixture plan still governs Gate 2 fixture work: [GOLDEN-FIXTURE-PLAN.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/port/GOLDEN-FIXTURE-PLAN.md:1)
- Gate 2 scope is limited to kernel determinism and digest rules only

## Gate 2 Scope Implemented

1. `DigestAlgorithm` and `DigestScope` value objects exist for the approved local Gate 2 digest vocabulary.
2. `ContentDigest` now validates and renders canonical `sha256:<64 lowercase hex>` strings and rejects missing prefixes, uppercase hex, and wrong lengths.
3. Raw byte SHA-256 hashing is available and covered by a fixed known vector.
4. Canonical JSON serialization now uses an explicit value tree, deterministic object-property ordering, array-order preservation, UTC timestamp formatting, NFC normalization, and omission-vs-null preservation.
5. `DigestEnvelope` provides a kernel-level typed JSON digest input shape with required canonicalization profile, digest algorithm, scope, schema id, schema version, and authoritative content.
6. NDJSON LF-only canonicalization exists with explicit CRLF normalization opt-in and byte-level LF, CRLF, and BOM fixture coverage.
7. Fixed positive and negative conformance fixtures with metadata exist under [fixtures/conformance/kernel](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/fixtures/conformance/kernel).
8. Verification evidence is recorded in [GATE-02-EVIDENCE.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/gates/GATE-02-EVIDENCE.md:1).

## Gate 2 Non-Goals

- protocol contract adoption
- blueprint conformance
- PHP compatibility
- PHP-generated fixtures
- bundle parity
- provenance parity
- AI governance parity beyond kernel prerequisites

## Remaining Risks

- Current protocol digest remains provisional scaffold behavior in `src/NexusScholar.Protocol` and is not replaced by this gate.
- Current bundle manifest and verifier remain thinner than any future bundle authority.
- Hosted Windows/Linux CI evidence is recorded in [GATE-02-EVIDENCE.md](/C:/Users/mouadh/Documents/AI%20in%20research/core-csharp/docs/gates/GATE-02-EVIDENCE.md:1).
- Downstream conflicts remain open:
  - `CF-001` protocol contract
  - `CF-002` bundle contract
  - `CF-004` provenance and AI governance
  - `CF-005` blueprint authority
  - `CF-006` missing schema closure
  - `CF-008` approval semantics
  - `CF-014` snapshot identity and equality

## Exit Standard

Gate 2 exits for the local deterministic-kernel scope when the behavior above is implemented, fixture-backed, and verified without claiming any higher-level compatibility than this gate explicitly covers.
