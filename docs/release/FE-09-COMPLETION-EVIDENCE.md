# FE-09 Completion Evidence

Status: complete and merged to protected `main`.

Date: 2026-07-17

Authority:

- ADR 0039: provider-neutral acquisition and recorded Crossref adapter;
- ADR 0040: live-provider host, credential, and runtime-evidence policy;
- ADR 0041: provider evidence cache retention and invalidation;
- ADR 0042: recorded Full Text retrieval and access evidence;
- ADR 0043: citation-network snapshots and basic metrics;
- FE-09A through FE-09F gate records under `docs/gates/`.

## Delivered Scope

- provider-neutral request, response, pagination, attempt, and completeness
  evidence in `NexusScholar.Search`;
- deterministic recorded Crossref `/works` response parsing;
- a non-packable, exact-host live transport with credential injection only at
  send time, bounded response reads, no redirects, and no automatic retries;
- deterministic OpenAlex `/works` cursor and Semantic Scholar bulk-search and
  paper-batch adapters;
- immutable provider cache records plus a non-packable BCL filesystem store:
  OpenAlex complete `200` bodies may be retained for 14 days, Semantic Scholar
  remains digest-only, and Crossref runtime caching is denied;
- exact retained-byte Full Text retrieval verification with explicit rights,
  route, redirect, completeness, media type, digest, and size evidence;
- packable `NexusScholar.Network` direct-citation snapshots over stable work
  identities, explicit unresolved targets, evidence-backed edges, and
  deterministic node, edge, isolation, in-degree, and out-degree metrics.

## Protected-Main Delivery

- Pull request: [#69](https://github.com/nexus-scholar-org/core-csharp/pull/69).
- Merge commit: `ea665eb53285c3874efec490755dce9520c12fb5`.
- [Ubuntu repository gate](https://github.com/nexus-scholar-org/core-csharp/actions/runs/29609611238/job/87980942148):
  passed.
- [Windows repository gate](https://github.com/nexus-scholar-org/core-csharp/actions/runs/29609611238/job/87980942054):
  passed.
- [Dependency review](https://github.com/nexus-scholar-org/core-csharp/actions/runs/29609611240/job/87980941758):
  passed.
- [CodeQL analysis](https://github.com/nexus-scholar-org/core-csharp/actions/runs/29609611248/job/87980942141):
  passed.

## Verification

- Release build: passed.
- Full solution: 1,011 passed, 0 failed.
- Opt-in live-provider smokes: 2 skipped by default.
- Format verification: passed.
- Deterministic package verification: 24 validation-only packages passed
  reproducible pack and clean local-source smoke.

The default test and CI paths do not call live scholarly providers. OpenAlex and
Semantic Scholar live smoke tests require explicit opt-in; authenticated
Semantic Scholar bulk and batch smoke remained credential-blocked at closeout.

## Enforced Boundaries

- provider payloads, URLs, paths, cache indexes, and runtime receipts are not
  scientific identity;
- credentials never enter Core records or logs;
- cache eligibility is operation- and provider-specific, and expired evidence
  remains verifiable but cannot be served as fresh;
- only explicitly admitted open-access, licensed, or public-domain recorded Full
  Text evidence can become an artifact;
- retrieval failure remains evidence and never becomes a Screening verdict;
- citation edges require exact evidence and snapshots bind the source corpus
  digest and algorithm identity;
- live hosts and filesystem stores remain outward adapters.

## Nonclaims

- no live Crossref transport;
- no live Full Text downloader, scraping, paywall bypass, shadow-library route,
  PDF parsing, OCR, or legal certification;
- no Semantic Scholar response-body retention right;
- no citation centrality, shortest path, snowballing, impact interpretation,
  export, provider-backed graph acquisition, or scale claim;
- no provider parity, broad PHP compatibility, production readiness, public
  package publication, database, API, cloud, authentication, tenancy, or
  multi-user claim.

## Next Gate

FE-10 plugin-runtime design and capability security. Existing Extensibility
contracts do not constitute a runtime, arbitrary-code sandbox, or authority
transfer.
