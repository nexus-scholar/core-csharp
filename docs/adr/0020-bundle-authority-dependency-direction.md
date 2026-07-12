# ADR 0020: Bundle Authority Dependency Direction

Status: Accepted

Date: 2026-07-12

## Context

ADR 0009 and the original architecture test kept Bundles on Kernel and Shared, using field-level Protocol, Workflow, and Provenance bindings. The 2026-07-11 hardening review demonstrated that caller-owned digest dictionaries cannot prove complete authority identity and requires full verified-record resolvers.

These requirements conflict: full verified types cannot be resolved while Bundles is forbidden from referencing their owning modules.

## Decision

1. Bundles is an outer verification boundary and may depend inward on Protocol, Workflow, and Provenance.
2. Protocol, Workflow, and Provenance must not depend on Bundles.
3. Bundle verification resolves `VerifiedProtocolVersion`, `VerifiedWorkflowDefinition`, and validated `ResearchEvent` records through an application-owned resolver.
4. Bundle manifest bindings remain portable field records, but those fields establish authority only after exact comparison with resolved records.
5. This decision does not authorize persistence, archive parsing, import commit, UI, API, cloud, or provider behavior.

## Consequences

- The former field-only architecture rule is replaced with an inward dependency-direction rule.
- Bundle verification can reject partial identity matches that digest dictionaries could not detect.
- Authority modules remain reusable and unaware of portable bundles.

## Compatibility Impact

No PHP or blueprint compatibility claim is made.
