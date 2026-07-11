# ADR 0018: Workflow authority hardening sequence

Status: Accepted

Date: 2026-07-11

## Context

ADR 0005 and ADR 0006 define deterministic Workflow template and compiler behavior, including waiver and amendment inputs. The 2026-07-11 hardening review found that the current implementation treats approval identifiers, amendment identifiers, and caller-supplied invalidation notices as authority without resolving their actors, targets, policy, lineage, or membership.

Hardening 03 introduced verified Protocol approval/version rehydration, but explicitly deferred waiver, amendment, deviation, and invalidation authority transitions. Workflow therefore has no accepted verified type capable of proving waiver or amendment authority. Preserving the existing paths would violate the product laws that suggestion is not decision, current state is not historical record, and automation is not scientific authority.

There is no pinned PHP fixture or accepted specification that supplies the missing authority proof. Existing tests demonstrate deterministic local behavior, not verified persisted authority.

Decision owner: repository hardening lead acting under the user-authorized hardening plan. No scientific-method choice is changed.

## Decision

1. Authoritative Workflow compilation requires a `VerifiedProtocolVersion` produced by the accepted Protocol authority boundary.
2. Hardening 04 supports authoritative compilation only for approved Protocol versions with no waiver-backed scientific input and no amendment/invalidation context.
3. When an approved Protocol version contains waivers, carries an amendment identifier, or supplies amendment/invalidation records, authoritative compilation fails closed with a stable unverified-authority error.
4. Deterministic waiver and amendment compilation code may remain as non-authoritative implementation material, but no public authoritative entry point may reach it until Protocol defines verified waiver, amendment, invalidation-notice, and approval authority records.
5. Workflow definitions become authoritative only through compiler-owned construction or validated rehydration that resolves the exact verified Protocol version and Workflow template, recomputes workflow identity and digest, and returns an explicit verified result.
6. Scientific decision bindings identify and digest the complete `ProtocolDecision` record, not only its key and value.
7. Workflow output collections are deeply copied and exposed through non-castable read-only collections.
8. Because complete Protocol decision records replace key-only source bindings inside the Workflow digest, the Workflow definition schema advances from `nexus.workflow-definition:1.0.0` to `nexus.workflow-definition:1.1.0`. Existing 1.0.0 fixture outputs remain historical and are not regenerated in place.
9. Execution compile parameters are explicitly non-authoritative value inputs. Their source identity is the declared template input ID and their binding records the canonical value digest; they do not represent a human decision, approval, provider record, or scientific evidence source. Any future requirement for supplier or temporal provenance requires a versioned parameter-record contract.

## Alternatives

### Preserve existing waiver and amendment behavior

Rejected. Approval ID counts, ID-only amendment matching, and replaceable invalidation notices do not establish authority or lineage.

### Implement generalized authority approvals inside Workflow

Rejected for this gate. Waiver and amendment approvals are Protocol-owned scientific authority. Defining them in Workflow would invert dependencies and duplicate policy semantics.

### Delay all Workflow hardening

Rejected. Verified Protocol input, complete decision binding, immutable definitions, and definition rehydration are independently useful and remove public fabrication paths now.

## Consequences

- Existing deterministic unamended workflows remain supported through a verified Protocol input.
- Existing waiver/amendment positive tests move to explicit fail-closed expectations until the prerequisite Protocol authority gate is implemented.
- Workflow remains dependent only on Protocol and Kernel; no persistence, UI, provider, or host dependency is introduced.
- This narrows current behavior but does not revoke ADR 0005 or ADR 0006 as target semantics. It sequences their safe implementation.

## Migration Effect

- Callers must obtain `VerifiedProtocolVersion` before authoritative compilation.
- Raw `ProtocolVersion` compile entry points become unavailable or explicitly reject authority-bearing compilation.
- Persisted Workflow definitions must use the Hardening 04 rehydration boundary.
- Consumers must declare `nexus.workflow-definition:1.1.0`; 1.0.0 digests do not identify the hardened authority contract.
- Waiver/amendment callers must wait for a later Protocol authority gate and cannot treat the former behavior as authoritative.

## Fixture Effect

- Add verified Protocol compile fixtures and Workflow definition rehydration fixtures.
- Add fail-closed waiver, amendment, caller-notice replacement, raw Protocol, tampered definition, wrong resolver, and mutable-collection cases.
- Preserve existing deterministic fixture files as historical Gate 4 evidence; do not relabel them as verified authority fixtures.

## Reversal Conditions

The temporary waiver/amendment rejection may be removed only after accepted Protocol contracts provide:

- verified waiver approval target, actor, role, policy, and digest binding;
- verified amendment lineage to previous and produced Protocol versions;
- immutable invalidation-notice membership and digest verification;
- focused negative tests and deterministic fixtures for all three boundaries.
