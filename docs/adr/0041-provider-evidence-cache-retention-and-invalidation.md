# ADR 0041: Provider Evidence Cache Retention And Invalidation

Status: Accepted

Date: 2026-07-17

## Context

ADR 0040 admits live Search transport but requires runtime response bodies to
remain transient and leaves FE-09E out of scope. Reproducible provider parsing
needs a private local cache without turning provider responses into scientific
authority or silently exceeding provider terms.

The current official OpenAlex documentation describes its complete dataset as
CC0. The current Semantic Scholar API license limits API use and downstream
handling of response data. Observed API access does not widen those rights.

## Decision

FE-09E adds provider-neutral immutable cache contracts to
`NexusScholar.Search` and a non-packable BCL filesystem adapter in
`NexusScholar.Search.Providers.Cache`.

Existing `runtime-transient-digest-only` evidence is not reinterpreted. A cache
write creates a successor `runtime-retained-local-cache` record only when an
explicit versioned policy admits the provider and operation.

The initial policy matrix is:

| Provider | Operation | Body retention |
| --- | --- | --- |
| OpenAlex | `openalex.works` | private local cache allowed |
| Semantic Scholar | bulk and batch | digest-only; body write denied |
| Crossref | recorded fixtures only | no runtime cache |

Only complete identity-encoded `200` responses may be retained. The entry binds
provider, operation, sanitized request digest, page request digest, parser
identity, original response evidence, exact body digest and length, media type,
original timestamps, stored timestamp, policy identity, and expiry.

Entries and bodies are immutable and content-addressed. A lookup index is
operational, rebuildable, and not authority. Expiry makes an entry ineligible
for a cache hit but does not erase or invalidate its evidence. A cache hit keeps
the original response timestamps and never masquerades as a new live attempt.

Credentials, credential references, contact identity, raw URLs, authorization
headers, cookies, arbitrary headers, exception bodies, and provider SDK objects
are forbidden.

## Consequences

OpenAlex responses can be replayed locally with exact-byte verification.
Semantic Scholar remains digest-only by default. Cache records are operational
evidence, not Search truth, corpus identity, a workspace generation, or proof of
scientific correctness.

No CLI, UI, cloud cache, database, background refresh, provider parity, legal
certification, or production-readiness claim is introduced.

## Reversal Conditions

Revisit the provider matrix when provider terms or an operator's written license
change. A wider policy requires a successor ADR and tests; configuration alone
cannot widen retention rights.
