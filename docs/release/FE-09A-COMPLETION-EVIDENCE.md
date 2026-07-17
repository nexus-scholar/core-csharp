# FE-09A Local Completion Evidence

Status: complete; merged through PR #69 to protected `main`.

Date: 2026-07-17

## Delivered Scope

- ADR 0039 and an accepted FE-09A implementation gate.
- Provider-neutral canonical acquisition, page, retained-fixture,
  raw-response, attempt, and page-result evidence contracts in
  `NexusScholar.Search`.
- A non-packable outward `NexusScholar.Search.Providers.Crossref` project.
- Sanitized Crossref `/works` request description with no host, contact,
  credential, authorization header, or raw URL.
- Deterministic parsing of caller-supplied exact retained JSON bytes.
- Duplicate sightings remain distinct; missing-DOI items remain unresolved.
- Exact raw-byte digest, fixture actor/agent, acceptance time, parser identity,
  response metadata, observed rate-limit fields, completeness, partial state,
  and pagination-chain evidence.

## Verification

Run with SDK `10.0.301`:

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release -m:2
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build -m:2
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Results:

- Release build: passed with zero warnings and zero errors.
- Full solution: 942 passed.
- Architecture: 40 passed.
- Existing conformance: 141 passed.
- Crossref adapter: 17 passed.
- Format verification: passed.
- Package verification: 23 approved packages at `0.1.0-alpha.2`; normalized
  repeat pack and clean local-source smoke passed.
- `git diff --check`: clean.

## Negative Evidence

- one-byte response mutation fails exact fixture digest verification;
- unsupported response or parser schema is rejected;
- full URLs and secret/contact-bearing descriptor parameter names are rejected;
- unadmitted observed headers are rejected before result construction;
- raw-response evidence digests bind the provider alias;
- response pages cannot bind to another acquisition request;
- later pages cannot be parsed without the preceding page result;
- a drifted next offset or page index is rejected;
- `429` preserves observed retry-after/rate-limit evidence without inventing a
  retry policy;
- short pages before declared totals remain explicitly partial;
- response-bearing failures retain evidence instead of becoming empty success;
- Core cannot reference the outward Crossref project;
- live-call primitives remain repository-forbidden;
- the adapter remains non-packable and outside the approved package graph.

## Nonclaims And Remaining Gate

- No live Crossref request was made.
- No Crossref, PHP, or provider parity claim is made.
- No credentials, contact identity, HTTP transport, retries, throttling, cache,
  persistence, CLI, desktop, API, or cloud behavior is implemented.
- No runtime provider response or provider-terms retention policy is accepted.
- FE-09D legal/network/credential/host policy and a successor transport gate are
  prerequisites for live acquisition.

## Independent Review

- Scientific-invariant re-review: accepted after the raw-response digest was
  bound to the provider alias and observed headers were restricted to admitted
  retry/rate-limit evidence.
- .NET architecture re-review: accepted after later-page chain proof was moved
  into the public parse boundary.
- No blocking or high findings remain in the local FE-09A scope.
