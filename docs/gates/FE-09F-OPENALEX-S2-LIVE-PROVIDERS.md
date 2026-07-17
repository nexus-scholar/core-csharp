# FE-09F: OpenAlex And Semantic Scholar Live Providers

Status: Complete; merged through PR #69 to protected `main`

Date: 2026-07-17

## Goal

Implement a bounded, non-packable live host and deterministic OpenAlex and
Semantic Scholar adapters without weakening Core authority or network-free CI.

## Scope

- `NexusScholar.Search.Providers.Live` transport and credential resolvers;
- OpenAlex `/works` cursor search;
- Semantic Scholar `/graph/v1/paper/search/bulk` token search;
- Semantic Scholar `/graph/v1/paper/batch`, 1 through 500 IDs;
- runtime transient digest evidence;
- synthetic parser samples and opt-in live smoke tests.

## Required Behavior

- exact host/method/path policy from ADR 0040;
- Windows Credential Manager with environment fallback;
- secrets injected only at send time and never emitted;
- redirects and encoded bodies disabled;
- 30-second default timeout, 8 MB default cap, no retries;
- OpenAlex page size at most 100;
- S2 bulk parser supports up to 1,000 results and token continuity;
- S2 batch validates unique IDs and maps results by identifier rather than
  assuming response order;
- normalized sightings retain DOI, OpenAlex, and S2 identifiers when present;
- missing identifiers remain unresolved rather than invented;
- parser failures retain digest evidence but create no successful page;
- no runtime response bytes persist after parsing.

## Negative Cases

- wrong scheme, host, alias, port, IP literal, path, method, redirect, userinfo,
  fragment, or raw URL;
- missing credential, secret-bearing descriptor/body, and exception leakage;
- timeout, cancellation, `429`, oversized body, non-identity encoding, malformed
  JSON, schema drift, mutation, truncation, and parser mismatch;
- cursor/token/result-chain drift;
- OpenAlex page size above 100;
- S2 batch empty, duplicate, or above 500 IDs;
- S2 batch body contains `fields` or secret material;
- response-order mismatch and missing/null S2 batch members;
- live test execution in CI.

## Validation

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Opt-in live tests require `RUN_LIVE_PROVIDER_TESTS=1` and applicable credentials.
They must emit only sanitized summaries.

## Exit Criteria

- deterministic fixture, transport-stub, architecture, and full solution gates
  pass;
- OpenAlex live smoke parses one bounded page using the secure credential;
- S2 bulk and batch live smokes either parse successfully with the secure S2
  credential or remain explicitly blocked by missing credential;
- independent architecture, security, and scientific reviews accept;
- completion evidence distinguishes successful live calls, throttled calls, and
  untested credential-dependent paths.

## Nonclaims

- no Crossref live transport in this gate;
- no cache, persistence, retries, orchestration, UI, API, cloud, provider parity,
  PHP parity, production readiness, scale, or legal-compliance guarantee.

## Completion Evidence

Completed locally on 2026-07-17.

Implemented:

- non-packable exact-host live transport with Windows Credential Manager and
  environment fallback;
- receipt-bound immutable response bytes, transient complete-body evidence, and
  non-parseable incomplete evidence for capped responses;
- OpenAlex `/works` cursor acquisition and deterministic normalization;
- Semantic Scholar bulk token acquisition and paper batch requests for 1 through
  500 unique identifiers;
- S2 batch mapping by S2, DOI, arXiv, PubMed, CorpusId, MAG, ACL, and DBLP
  response identifiers without response-order assumptions;
- decoded credential/contact value rejection, no redirects, no retries,
  identity encoding, bounded reads, controlled schema failures, and CI filters
  excluding `LiveProvider`;
- synthetic samples under `fixtures/samples/search/`; they are not retained
  provider evidence or compatibility fixtures.

Local verification:

- provider transport tests: 9 passed;
- OpenAlex adapter tests: 11 passed;
- Semantic Scholar adapter tests: 18 passed;
- architecture tests: 41 passed;
- full solution: 981 passed, 2 opt-in live tests skipped by default;
- package verification: 23 approved packages passed; the three new provider
  projects remained non-packable;
- OpenAlex opt-in live smoke: passed against a bounded real `/works` response
  using the secure credential;
- default live-smoke execution: skipped/inconclusive without opt-in.

Semantic Scholar live boundary:

- the supplied Academic Graph Swagger is pinned at
  `specs/providers/semantic-scholar-academic-graph-v1.swagger.json` with SHA-256
  `00d7302bcb07414971a0b483d332e57c01344e037ce878d5baab3c312df039ae`;
- an anonymous bulk endpoint shape was observed before implementation and an
  anonymous batch call was throttled with `429`;
- authenticated S2 bulk and batch smoke tests remain blocked because no S2
  credential is configured. No successful live S2 batch claim is made.

Independent architecture, scientific-invariant, and adversarial-test reviews
identified receipt mutability, incomplete-body evidence, decoded secret values,
null members, identifier collisions, raw indexes, total-chain drift, CI
isolation, and sample provenance issues. Those findings were corrected and
re-reviewed before completion.
