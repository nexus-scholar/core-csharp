# ADR 0040: Live Provider Host, Credential, And Runtime Evidence Policy

Status: Accepted

Date: 2026-07-17

## Context

ADR 0039 admits provider-neutral acquisition contracts and a retained-fixture
Crossref parser, but explicitly blocks live transport until FE-09D accepts
legal, host, credential, contact-identity, retry, and runtime-retention policy.
The FE-09 roadmap also requires a successor implementation gate after FE-09D.

This decision is informed by:

- ADR 0039 and the FE-09 roadmap;
- the official OpenAlex Works, authentication/pricing, paging, and terms sources;
- the official Semantic Scholar API overview and API License Agreement;
- the operator-supplied Semantic Scholar Academic Graph Swagger 2.0 document,
  pinned at `specs/providers/semantic-scholar-academic-graph-v1.swagger.json`;
- bounded live probes on 2026-07-17:
  - anonymous OpenAlex `/works` returned `200`;
  - anonymous Semantic Scholar bulk search returned `200`, 1,000 records, and
    a continuation token;
  - an immediate anonymous Semantic Scholar paper-batch request returned `429`.

Observed access is not an availability, authorization, compatibility, or
production-readiness guarantee.

## Decision

### 1. Gate sequence

FE-09D is policy-only. It does not admit transport code. Live transport may be
implemented only under the successor FE-09F gate after this ADR and FE-09D are
accepted.

### 2. Legal and operator evidence

Sources were retrieved on 2026-07-17.

| Provider | Binding sources | Version/date | Allowed development use and duties | Retention and rate boundary |
| --- | --- | --- | --- | --- |
| OpenAlex | `https://developers.openalex.org/api-reference/works/list-works`, `https://developers.openalex.org/api-reference/authentication`, `https://openalex.org/OpenAlex_termsofservice.pdf` | Terms last revised 2024-02-07; API docs retrieved 2026-07-17 | Limited, revocable use under the terms. Do not imply endorsement, remove marks, bypass controls, overload the service, or redistribute contrary to the terms. | This slice keeps no runtime body. Respect documented budget/rate headers and `429`; no automatic retry. |
| Semantic Scholar | `https://www.semanticscholar.org/product/api`, `https://www.semanticscholar.org/product/api/license`, pinned Swagger digest `sha256:00d7302bcb07414971a0b483d332e57c01344e037ce878d5baab3c312df039ae` | API License Agreement last updated 2023-05-17; Swagger reports Academic Graph API 1.0 | Use only through compatible software and provider-documented endpoints. Public materials using S2 contributions require Semantic Scholar attribution; scientific publications using API-produced results must cite the Semantic Scholar Open Data Platform paper. Underlying third-party data licenses remain applicable. | Do not circumvent rate limits. This slice keeps no runtime body and does not repackage, sell, or redistribute S2 data. |

The operator requested OpenAlex and Semantic Scholar implementation and bounded
live verification on 2026-07-17 and supplied an OpenAlex credential. That is
recorded as development-test authorization only. It is not legal advice, does
not prove organizational authority, and does not authorize production,
redistribution, public display, or use outside provider terms.

### 3. Exact hosts and URL construction

The canonical outward project is `NexusScholar.Search.Providers.Live`.

It may contact exactly:

- `https://api.openalex.org`
- `https://api.semanticscholar.org`

The host owns the complete URI. Callers supply only a provider alias and
sanitized endpoint descriptor. The host rejects:

- non-HTTPS schemes;
- userinfo, fragments, raw absolute URLs, IP literals, aliases, alternate ports,
  and non-allowlisted hosts;
- redirects, including redirects to an allowlisted host;
- path traversal and endpoints outside the admitted path table;
- credentials, contact identity, or secrets already present in path/query/body.

Automatic redirects are disabled.

### 4. Endpoint and credential policy

| Provider operation | Exact method/path | Credential handle | Placement | Missing credential |
| --- | --- | --- | --- | --- |
| OpenAlex Works search | `GET /works` | Windows Credential Manager `NexusScholar.OpenAlex`; environment fallback `OPENALEX_API_KEY` | injected as `api_key` only inside the host | reject before network |
| S2 bulk search | `GET /graph/v1/paper/search/bulk` | Windows Credential Manager `NexusScholar.SemanticScholar`; environment fallback `S2_API_KEY` | injected as `x-api-key` only inside the host | reject before admitted execution |
| S2 paper batch | `POST /graph/v1/paper/batch` | Windows Credential Manager `NexusScholar.SemanticScholar`; environment fallback `S2_API_KEY` | injected as `x-api-key` only inside the host | reject before admitted execution |

Anonymous probes are discovery evidence only and can never become admitted
provider evidence. Credential values and credential target names never enter
Core records, request descriptors, exceptions, logs, bundles, or persisted
artifacts. Redaction is not a substitute for excluding secrets.

Current admitted endpoints require no contact identity. No contact value is
sent. If a provider later requires contact identity, transport is blocked until
a successor ADR defines a separate opaque contact reference and disclosure
policy.

### 5. Endpoint-specific contract

OpenAlex:

- `/works` search uses `cursor=*` for the first page and `meta.next_cursor`
  thereafter;
- `per_page` is 1 through 100;
- `select` limits fields to the parser contract;
- `api_key` is never part of the sanitized descriptor.

Semantic Scholar:

- bulk search uses `GET /graph/v1/paper/search/bulk`, deterministic `paperId`
  ordering, and response `token` pagination;
- a bulk response can contain up to 1,000 records;
- paper batch uses `POST /graph/v1/paper/batch`;
- batch request body is `{"ids":[...]}`, with 1 through 500 unique IDs;
- `fields` is a query parameter, not a body member;
- the pinned specification documents a 10 MB response limit.

### 6. Runtime controls

- total timeout is configurable from 1 through 120 seconds and defaults to 30;
- response cap is configurable up to 10 MB and defaults to 8 MB;
- no automatic retry, sleep, jitter, parallel fan-out, or redirect;
- requests use `Accept-Encoding: identity`; encoded response bodies are rejected;
- status, admitted headers, request and receipt timestamps are observed once;
- cancellation and timeout are explicit terminal outcomes.

### 7. Runtime evidence schema

FE-09F may add:

- `nexus.search.runtime-provider-response / 1.0.0`
- retention disposition `runtime-transient-digest-only`

For a complete body, SHA-256 is computed over the exact entity-body bytes
delivered to the parser after HTTP transfer framing, with no content decoding
because only identity encoding is admitted. Evidence binds:

- provider alias, sanitized request digest, parser id/version;
- exact-byte digest and `raw-artifact-bytes` scope;
- byte length, media type, status, UTC request/receipt timestamps;
- body-complete flag, retention disposition, and allowlisted headers.

Allowed response headers are provider-specific rate-limit fields,
`Retry-After`, `Content-Type`, and `Content-Length`. Authentication, cookies,
contact, tracing, server, and arbitrary headers are excluded.

If the cap is exceeded, the host reads no more than cap plus one bytes, parsing
is forbidden, `body_complete=false`, and only a transport-local prefix digest
and observed prefix length may be reported. Such evidence cannot be accepted as
a provider page. Mutation, truncation, parser mismatch, secret leakage, encoded
bodies, and timestamp inversion are rejection cases.

Runtime bytes live only in memory until parsing completes. They are never
written to files, cache, database, logs, test results, or evidence records.

### 8. Live test policy

Live tests require `RUN_LIVE_PROVIDER_TESTS=1`, must also detect and skip in CI,
and require the applicable credential. They issue one bounded request per
operation, persist no body, print no raw exception body, and report only
sanitized status, count, digest, and schema result. A `200` response is transport
evidence, not scientific correctness or provider compatibility.

## Consequences

- CI remains deterministic and network-free.
- Live behavior is isolated in a non-packable outward host.
- OpenAlex and S2 response changes remain parser evidence, not Core authority.
- FE-09E cache remains out of scope.
- Production, scale, SLA, legal-compliance, PHP-parity, and provider-parity
  claims remain unmade.

## Reversal Conditions

Revisit this ADR if provider hosts, terms, credentials, paging, compression,
retention, or rate policy changes, or if persistence/retry/contact identity is
required.
