# FE-09A: Search Provider Contract And Crossref Recorded Adapter

Status: Complete; merged through PR #69 to protected `main`

Date: 2026-07-17

Scope:

- Branch: `cdx/fe-09a-live-search-provider`
- First implementation slice in FE-09, limited to contract and fixture-backed adapter behavior.

Goal:

Create a reproducible provider-neutral acquisition boundary under
`NexusScholar.Search` and validate it through deterministic Crossref request
description and exact recorded-response parsing. No transport is admitted.

## Context

- FE-09 scope from `docs/plans/2026-07-14-feature-expansion-priority.md`
- ADRs:
  - `docs/adr/0010-search-trace-and-plan-contract.md`
  - `docs/adr/0011-search-import-source-contract.md`
  - `docs/adr/0014-fulltext-acquisition-artifact-and-extraction-contract.md`
  - `docs/adr/0027-phase-7-citation-network-dissemination-evidence-boundary.md`
- Source maps:
  - `docs/port/php-search-behavior.md`
  - `docs/port/php-search-fixture-plan.md`
- Current contracts:
  - `NexusScholar.Search.SearchTrace`
  - `NexusScholar.Search.SearchCacheIdentity`
  - `NexusScholar.Search.ISearchProvider`

## Dependency order

1. ADR 0039 approved and bound to FE-09A scope.
2. `NexusScholar.Search` adds/updates provider-neutral acquisition interfaces and evidence records.
3. A non-packable `NexusScholar.Search.Providers.Crossref` outward project is
   added for sanitized request description and recorded-byte parsing.
4. Conformance fixtures and negative tests are added for pagination/retry/rate limits/partial completeness/raw-response evidence.
5. Gate artifact is passed and includes explicit non-claims and fixture comparator notes.

## Required behavior

- Keep Core search trace and plan contracts unchanged.
- Keep Core provider contracts provider-neutral:
  - normalized provider alias
  - pagination request and page state
  - observed response and rate-limit evidence
  - provider attempt evidence
  - raw response evidence and completeness markers
- Add one outward Crossref adapter:
  - caller-supplied exact response bytes
  - deterministic request description and parsing
  - no runtime network access
  - no file, clock, credential, cache, or sleep access
- Adapter must fail fast on unknown aliases before cache/execution path.
- Attempts must explicitly record:
  - provider alias
  - contract and parser version
  - page index and page request window
  - observed response category and final stop reason
  - observed retry-after and rate-limit headers
  - partial page and truncation reasons
  - raw response digest and scope
- No API credentials in Core records or logs.
- No scrape behavior.
- Accept only exact bytes retained in local repository fixtures.
- Record the local actor or fixture-generation agent, acceptance timestamp,
  source note, and exact fixture-byte digest.

## Required scope (Allowed)

- `docs/adr/0039-live-search-provider-acquisition-contract.md`
- one Crossref request/response adapter behind the external provider boundary
- conformance fixture set and comparator notes for provider evidence

## Excluded scope

- live network calls in CI or tests
- provider SDKs
- credential persistence in Core
- Google Scholar scraping
- paywall bypass
- imported export parser expansion
- Search-time Deduplication behavior changes
- persistence, API, UI, cloud, or plugin execution behavior
- any new PHP/Blueprint compatibility claim
- actual host transport, credential, contact identity, retry scheduling,
  throttling, caching, and provider registration
- runtime provider responses or digest-only runtime retention

## Negative tests (minimum required)

- search provider alias unknown before cache execution and before any external attempt
- query validation failures continue to block execution
- max-results zero or negative input rejected
- plan item non-positive limit rejected
- invalid offset values rejected
- pagination boundary handling for fixture pages:
  - next page requested after final page
  - empty provider page
- response classification:
  - transient response category is preserved as observed evidence
  - terminal response category is preserved as observed evidence
- rate-limit behavior:
  - recorded rate-limit refusal produces terminal page evidence
  - retry-after metadata captured in trace
- partial completeness:
  - truncated page marked partial
  - partial marker propagates to aggregate summary
- raw evidence handling:
  - raw response absent but digest required
  - raw response present with wrong scope rejected
  - retention disposition and exact byte length are preserved
- replay integrity:
  - secret-bearing query/header/contact/raw URL is rejected
  - one-byte fixture mutation fails declared-digest verification
  - parser-version mismatch is rejected
  - cross-page provider result or pagination-chain drift is rejected
- response-bearing failures produce valid non-empty evidence
- Crossref adapter misuse:
  - malformed fixture payload
  - fixture replay mismatch with declared schema
- no credentials emitted in trace/log schema

## Validation commands

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

Also run affected conformance and architecture checks for:

- `NexusScholar.Search` outward-boundary dependency direction
- fixture comparator tests
- package topology checks for outward provider split

## Exact exit criteria

- ADR 0039 is complete and no longer conflicting with ADR 0010/0011 constraints.
- Crossref request description and recorded-byte parsing are fully tested.
- Contract fields for pagination, observed response/rate-limit facts,
  partial completion, retention disposition, and raw-response evidence are
  present and deterministic.
- Crossref page results can be projected into raw Search sightings without
  Search-time Deduplication or live calls.
- Zero live outbound provider calls are required to complete test and build gates.
- No credential fields exist in provider evidence records and no logs persist secrets.
- Architecture tests prove Search cannot reference the Crossref adapter, the
  adapter is non-packable, and live-call primitives remain forbidden.
- Every admitted response is a retained local fixture with actor/agent,
  acceptance timestamp, source note, and verified exact-byte digest.
- Gate artifacts include explicit negative-case fixture IDs and comparator policy.
- Gate does not make PHP/provider compatibility claims except fixture-bounded records already in `docs/port/php-search-fixture-plan.md`.
- No scrape/paywall-bypass/shadow-library paths are approved.

## Non-claims

- no live provider/network calls in CI
- no production provider transport or SDK integration in this gate
- no accepted legal/data-retention policy for runtime provider acquisition
- no Google Scholar scraping
- no paywall bypass
- no shadow-library acquisition
- no Scopus API integration
- no Web of Science API integration
- no credentials in `NexusScholar.Search` records or logs
- no provider-level deduplication in Search trace
- no PHP full parity claim
- no persistence/API/UI/cloud changes
- no AI governance claims
