# ADR 0021: Screening Authority Dependency Direction

Status: Accepted

Date: 2026-07-12

## Context

The original Screening architecture allowed only Kernel and Deduplication dependencies, while criteria carried caller-provided Protocol strings. The accepted hardening review requires criteria to bind to an actual verified Protocol approval record.

## Decision

Screening may depend inward on Protocol and Deduplication. Its public service construction requires `VerifiedProtocolVersion` and `VerifiedDeduplicationResult`; raw construction remains internal for historical characterization tests only. Protocol and Deduplication remain independent of Screening.

This does not authorize persistence, UI, API, automation authority, or conflict-model changes.

## Compatibility Impact

No PHP or blueprint compatibility claim is made.
