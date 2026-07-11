# ADR 0017: Canonical JSON profile correction

Status: Accepted

Date: 2026-07-11

## Context

ADR 0002 selected RFC 8785 number rendering and property ordering while also requiring Nexus-authored strings to be normalized to Unicode NFC before serialization. The current implementation labels that combined behavior `rfc8785-jcs`, but it does not implement RFC 8785 number rendering. It also parses JSON numbers as `decimal` before `double`, which preserves lexical decimal precision instead of using the RFC 8785 IEEE 754 binary64 data model.

RFC 8785 requires parsed strings to remain unchanged. NFC preprocessing is therefore a Nexus semantic rule around JCS serialization, not pure RFC 8785 behavior. Keeping the unqualified profile identifier would overstate interoperability.

The affected digests are early-alpha, local, and explicitly provisional. There is no supported production persistence or published package compatibility contract to preserve.

## Decision

1. The corrected canonicalization profile identifier is `nexus-jcs-nfc-v1`.
2. Nexus normalizes human-authored strings and property names to NFC, rejects post-normalization property collisions, and then applies RFC 8785 serialization rules to the normalized value tree.
3. JSON numbers are represented and rendered through finite IEEE 754 binary64 values for canonicalization. Parsed JSON number tokens must use the same path as direct `double` construction.
4. Number output follows ECMAScript serialization as required by RFC 8785, including `0` for negative zero, decimal notation for magnitudes from `1e-6` inclusive to `1e21` exclusive, and normalized scientific notation outside that range.
5. Integer and decimal construction must reject values that cannot be represented as a finite binary64 value without violating the caller-visible numeric value. Values requiring wider or exact decimal representation must be modeled as strings with schema meaning, as ADR 0002 already requires.
6. Official RFC 8785 Appendix B number vectors and parse/recanonicalize regressions are required conformance evidence.
7. Existing `rfc8785-jcs` digests produced by this repository are invalidated as provisional. They are not migrated or silently accepted as the corrected profile.
8. A validated digest-envelope rehydrator remains a separate Phase 2 authority-boundary task. This gate must not expand into downstream record rehydration.

## Consequences

- Corrected canonical digests may differ from all earlier provisional digests, including records whose numeric content is unchanged because the profile identifier changes.
- A profile bump prevents an old and corrected digest from claiming the same canonicalization semantics.
- RFC 8785 vectors can validate the JCS portion of the implementation, while the profile name keeps the additional NFC rule explicit.
- No PHP, blueprint, package, persistence, or cross-version compatibility claim is created.

## Fixture Effect

- Add an RFC 8785 Appendix B fixture with source metadata and IEEE 754 inputs.
- Add direct-construction and parse/recanonicalize tests for the reproduced `1e-6`, `1e20`, and `333333333.33333329` defects.
- Regenerate only Kernel fixtures whose digest input includes the canonicalization profile.
- Do not rewrite unrelated downstream fixtures in this gate.

## Supersession

This ADR narrows and corrects ADR 0002 sections 1, 7, and 8 where the unqualified `rfc8785-jcs` profile name implied behavior incompatible with the combined Nexus NFC rule. All other ADR 0002 decisions remain in force.
