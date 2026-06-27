# Gate 9 Dedup Evidence

Status: local implementation complete and pending hosted re-run on branch head.

## Scope Implemented Locally

- deterministic exact identifier clustering using `ADR 0007` identity
- namespace-sensitive matches only
- fuzzy title review candidates at local threshold `95` / `0.95`
- transitive exact-ID clustering
- deterministic representative election and projection behavior
- no-id unresolved candidates and review-only path
- raw search/import evidence retention in cluster output
- app projection boundary non-claims (membership hash/run/snapshot fields are non-authoritative)

## Branch and Command Surface

- Branch: `cdx/gate-9-dedup-local`
- Result schema: `nexus.deduplication.result` / `1.0.0`
- Default fuzzy threshold: `0.95`

## Evidence Artifacts

- local source and conformance fixtures under `fixtures/conformance/deduplication/`

## Required Fixture IDs

- `dedup-exact-doi-cluster`
- `dedup-exact-cross-provider-id-cluster`
- `dedup-transitive-cluster`
- `dedup-fuzzy-title-review-required`
- `dedup-threshold-95-boundary`
- `dedup-no-id-title-only-no-auto-merge`
- `dedup-representative-election`
- `dedup-representative-merge-preserves-evidence`
- `dedup-raw-sightings-preserved`
- `dedup-web-app-projection-not-authority`

## Verification Commands

```text
dotnet restore NexusScholar.Core.slnx
dotnet build NexusScholar.Core.slnx -c Release --no-restore

dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1
```

## Conflict Summary

- `CF-011`: resolved for local Dedup input.
- `CF-012`: resolved for local fuzzy threshold default.
- `CF-020`: narrowed for app projection behavior.
- `CF-016`: implemented for Search and consumed by Dedup local handoff.

## Explicit Non-Claims

- no PHP compatibility claim
- no PHP-generated fixture claim
- no Screening
- no persistence/API/UI/cloud behavior
- no App projection behavior treated as Core authority
