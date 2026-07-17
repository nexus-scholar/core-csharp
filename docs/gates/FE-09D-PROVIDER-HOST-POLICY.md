# FE-09D: Provider Host, Credential, And Runtime Evidence Policy

Status: Accepted

Date: 2026-07-17

Scope: policy acceptance only. No transport implementation is admitted by this
gate.

## Dependencies

- ADR 0039
- ADR 0040
- FE-09 roadmap
- pinned Semantic Scholar Swagger at
  `specs/providers/semantic-scholar-academic-graph-v1.swagger.json`

## Acceptance Criteria

- legal and operator evidence is bound before implementation;
- exact HTTPS hosts, methods, paths, credential placement, and missing-key
  behavior are decided;
- contact identity is explicitly absent or governed;
- redirect, scheme, host-alias, IP-literal, userinfo, and raw-URL bypasses are
  rejected;
- `runtime-transient-digest-only` defines exact digest bytes, encoding,
  retention, timestamp, header, truncation, and oversized semantics;
- no automatic retries or runtime persistence are admitted;
- live tests are opt-in, CI-disabled, credential-gated, and sanitized;
- FE-09E cache and production policy remain excluded;
- the successor FE-09F gate owns all implementation.

## Evidence

- OpenAlex and Semantic Scholar official sources are listed in ADR 0040.
- Operator authorization and its limitations are listed in ADR 0040.
- S2 Swagger SHA-256:
  `00d7302bcb07414971a0b483d332e57c01344e037ce878d5baab3c312df039ae`.

## Nonclaims

- no transport code;
- no provider compatibility or PHP parity;
- no production, scale, SLA, persistence, caching, retry, public-display, or
  legal-compliance claim.
