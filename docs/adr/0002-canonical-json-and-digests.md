# ADR 0002: Canonical JSON and digest scope

Status: Accepted

Date: 2026-06-26

## Context

`ADR 0001` established that accepted ADRs outrank current C# behavior when sources disagree. The repository still lacks a local authoritative rule for canonical JSON and digest scope, which is tracked as `CF-009` in `docs/port/OPEN-CONFLICTS.md`.

Current C# behavior is insufficient as an authority:

- `ContentDigest` computes SHA-256 over bytes or UTF-8 text, but it does not define canonical JSON or digest scope.
- `ProtocolDraft` currently hashes a newline-joined `key=value` string for decisions only.
- `ReviewBundleManifest` and `BundleVerifier` do not yet encode canonicalization, digest-algorithm authority, or full checksum semantics.
- The current protocol conformance fixture is hand-written and lacks generator metadata.

The product laws and `AGENTS.md` require stronger guarantees:

- approval binds actor, timestamp, exact content digest, and version;
- scientific identity uses stable identifiers and content digests;
- every scientific mutation records actor and inputs;
- projections such as caches, embeddings, wikis, and generated narratives are not canonical evidence;
- compatibility claims require fixtures or focused tests.

The sibling blueprint names `rfc8785-jcs`, SHA-256, UTC `Z` timestamps, and checksum rules, but the repository explicitly does not treat blueprint defaults as locally authoritative until an ADR adopts them.

This ADR therefore decides a **local deterministic-kernel rule** for canonical JSON and digests. It does **not** claim blueprint conformance, protocol conformance, bundle conformance, or PHP compatibility.

## Decision

### 1. Canonical JSON profile

Scientific JSON digests in Nexus Scholar Core use the canonical JSON profile identifier `rfc8785-jcs`.

This is an explicit local decision, not an implicit blueprint adoption.

Canonical JSON output is encoded as UTF-8 bytes with no BOM before hashing.

### 2. Digest algorithms and rendering

The only approved digest algorithm for Gate 2 scientific content digests is SHA-256.

Digest strings rendered at interchange or persistence boundaries use:

```text
sha256:<64 lowercase hex>
```

Uppercase hex, omitted algorithm prefixes, and alternate separators are non-canonical.

### 3. Digest scopes

Nexus must distinguish digest scopes instead of overloading one digest for every purpose.

At minimum, Gate 2 recognizes these digest scopes:

- `raw-artifact-bytes`
- `canonical-json-record`
- `protocol-content`
- `approval-record`
- `provenance-event`
- `bundle-manifest`
- `ndjson-stream`

Each digest is computed over the canonical representation for its scope only. A digest from one scope is not equivalent to a digest from another scope even if the semantic payload overlaps.

### 4. Digest input envelope

For JSON-based scientific digests, the input to hashing is the canonical JSON of a typed digest envelope or typed record payload that includes the following as authoritative fields:

- the digest scope identifier;
- the schema identifier;
- the schema version, either as a separate field or inside the schema identifier when the schema convention already encodes version;
- all semantically authoritative content for that scope;
- stable scientific identifiers and version identifiers when they are part of the scientific state;
- actor and event timestamps when the scope is an approval or provenance record;
- referenced input and output digests when reconstructability depends on them.

The digest value itself, detached signatures, and transport/container checksums are never included inside the payload being hashed for that same digest.

### 5. Schema and version inclusion

Schema authority and version are part of canonical digest scope.

No scientific JSON digest may rely on an external assumption of schema identity or version. If a schema uses a combined identifier such as `nexus.review-protocol/v1`, that identifier is sufficient. If schema id and version are split, both must be present in the hashed content.

### 6. Timestamp format

Canonical timestamps use UTC RFC 3339 with `Z`.

Canonical emission rules are:

- timestamps are converted to UTC before canonical serialization;
- local offsets are forbidden in canonical output;
- timestamp strings must use a fixed emitted precision chosen by the owning schema or record type;
- if no narrower schema rule exists, Gate 2 fixtures and kernel records use `yyyy-MM-ddTHH:mm:ss.fffffffZ`.

Canonical serialization must never inject `now`. A timestamp may appear in a canonical digest only if that timestamp is already part of the scientific record for that digest scope.

### 7. Unicode normalization

All human-authored strings that participate in scientific JSON digests must be normalized to Unicode NFC before canonical JSON serialization.

Rationale:

- semantically equivalent composed and decomposed text must not produce accidental digest drift;
- normalization must happen before hashing, not be inferred by comparators afterward.

Opaque binary payloads are not normalized; they are hashed as raw bytes under the `raw-artifact-bytes` scope.

### 8. Number representation

Numbers inside canonical JSON digests follow the canonical number representation of `rfc8785-jcs`.

Additional local restrictions:

- `NaN`, `Infinity`, and `-Infinity` are forbidden;
- schema-defined integers must be emitted as integers, not as decimal strings;
- scientific values that require exact decimal preservation beyond JSON-number safety must be modeled explicitly as strings plus schema/type meaning, not left to serializer choice.

### 9. Property ordering

Object property ordering follows `rfc8785-jcs`.

Nexus does not define a custom property-ordering rule beyond that profile.

### 10. Array semantics

Array order is semantic and preserved exactly.

Canonicalization never sorts arrays. If a collection is logically unordered, the owning schema or producer must convert it into a deterministic ordered representation before digesting it.

### 11. Omitted versus null semantics

Omission and `null` are distinct.

- Omitted means a field is not present in the record shape for that instance.
- `null` means the schema explicitly allows a known empty or unknown value.
- Empty arrays mean explicitly no entries.

Required canonical fields must be emitted. Canonical digests must not rely on schema defaults, serializer defaults, or implicit omissions for:

- canonicalization profile,
- digest algorithm,
- schema authority,
- schema version,
- approval/version authority fields,
- required provenance/input/output fields.

### 12. Forbidden nondeterministic fields

The following must not participate in scientific canonical digests unless a narrower ADR or schema explicitly makes them authoritative for that scope:

- runtime object identity;
- absolute or machine-local file paths;
- drive letters, temp paths, current working directory, machine names, user names;
- unordered dictionary iteration artifacts;
- filesystem enumeration order;
- wall-clock generation at serialization time;
- random ids not already committed as stable scientific identifiers;
- serializer default insertion;
- schema-default inference;
- culture-sensitive formatting;
- local timezone offsets;
- non-normalized Unicode;
- secrets, access tokens, and environment-specific configuration;
- projection content such as caches, embeddings, wiki state, generated narratives, and other noncanonical views.

### 13. NDJSON and bundle-adjacent rules

For NDJSON digest scopes:

- bytes are UTF-8 without BOM;
- line endings are LF only;
- record order is semantic and preserved;
- a trailing final newline rule must be fixed by the owning schema or fixture and then held constant.

For bundle-adjacent digest scopes:

- ZIP metadata, ZIP entry order, and container-level incidental metadata are outside scientific identity unless a later ADR explicitly says otherwise;
- `checksums.sha256` is a verification artifact, not part of the payload for computing the digest of the manifest or component it describes.

## Alternatives Considered

### 1. Keep the current ad hoc string digests

Rejected.

The current `key=value` protocol digest is deterministic for one narrow case but is not safe as a general scientific digest rule and can collapse distinct records.

### 2. Adopt blueprint defaults implicitly without a local ADR

Rejected.

`CF-009` exists precisely because the repository must not silently treat blueprint defaults as local authority.

### 3. Define a Nexus-specific canonical JSON scheme

Rejected.

There is no evidence that a custom profile would be safer than a standard cross-language profile, and a custom scheme would increase fixture and comparator risk.

### 4. Avoid Unicode normalization and hash raw text exactly as entered

Rejected.

That would make semantically identical text drift across platforms and input methods without adding scientific value.

## Consequences

### Positive

- Gate 2 can implement one explicit deterministic-kernel target for canonical JSON.
- Approval, provenance, and artifact digest work can share one digest vocabulary.
- Fixture generation and negative tests can target a closed rule instead of ad hoc serializer output.
- Future compatibility work can compare against a defined local digest profile even before protocol or bundle parity is achieved.

### Negative

- Canonical serialization now has stricter validation obligations, especially around Unicode normalization, timestamps, null/omission, and unsupported numbers.
- Existing scaffold behavior such as `ProtocolDraft` string hashing and implicit `null` to empty-array coercion is now explicitly provisional and non-authoritative.
- A production-quality canonicalizer or tightly tested internal implementation will be required in Gate 2.

## Migration Effect

This ADR does not migrate persisted production data because Gate 2 implementation is not part of this change.

When Gate 2 implements this ADR:

- current scaffold digests must be treated as provisional;
- any content digest previously derived from ad hoc string material must be regenerated under the approved canonical rule before it can be treated as scientific authority;
- no compatibility or migration claim may be made for protocol or bundle records until their own open conflicts are resolved.

## Fixture Effect

Gate 2 fixtures must reflect this ADR explicitly.

Required expectations:

- fixed-clock canonical timestamp fixtures;
- fixed-id fixtures;
- canonical JSON digest fixtures;
- raw-byte digest fixtures;
- negative fixtures for non-canonical numbers, bad digest prefixes, uppercase hex, omitted required canonical fields, wrong timestamp forms, and non-NFC strings;
- generator metadata recording source refs, generator command, input digest, and output digest.

No fixture may be rewritten by hand to satisfy an implementation.

## Reversal Conditions

Replace or revise this ADR only if one of the following becomes true:

1. a later accepted ADR adopts a different canonicalization profile with stronger cross-language evidence;
2. fixture evidence shows that `rfc8785-jcs` cannot satisfy required deterministic behavior for approved Nexus record shapes;
3. a standards or platform constraint requires a different digest algorithm or encoding rule;
4. an approved schema policy requires a different Unicode or timestamp rule for scientific reasons and documents the migration effect explicitly.

## Explicit Claims Not Made

- This ADR does not adopt blueprint contracts as locally conforming.
- This ADR does not claim blueprint conformance.
- This ADR does not claim PHP compatibility.
- This ADR does not generate PHP fixtures.
- This ADR does not resolve `CF-001`, `CF-002`, `CF-004`, `CF-005`, `CF-006`, `CF-008`, or `CF-014`.
- This ADR does not implement Gate 2 behavior in `src/` or `tests/`.
