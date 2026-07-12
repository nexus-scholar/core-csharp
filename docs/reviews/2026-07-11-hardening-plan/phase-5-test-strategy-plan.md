# Phase 5 - Test Strategy Upgrade Plan

Status: in progress.

## Goal

Convert every defect reproduced by the 2026-07-11 review into permanent, deterministic regression evidence. Tests must prove scientific identity, authority rehydration, parser, numeric, and transactional-workspace invariants rather than optimize for a coverage percentage.

## Sources

- `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`
- accepted ADRs 0017 through 0023;
- conformance fixtures under `fixtures/conformance/`;
- defects fixed by Hardening 01 through Hardening 17.

## Dependency Order

1. Kernel: retain the complete official RFC 8785 Appendix B vectors with source metadata and add the official serialization/property-order sample.
2. Shared: generate deterministic randomized overlap graphs, insertion permutations, and repeated additions.
3. Search: run deterministic parser mutation corpora for RIS, CSV, and BibTeX and preserve standards-oriented seeds.
4. Authority modules: maintain a defect ledger mapping malformed rehydration and finite-number boundaries to permanent tests.
5. ResearchWorkspace and CLI: exercise competing OS processes and interrupted staging states without accepting mixed generations.
6. Test infrastructure: add an executable semantic-mutation gate for forged, tampered, malformed-rehydration, non-finite, and explicit mutation cases, plus informational Coverlet reports in CI.

## Allowed Paths

- `tests/`, `fixtures/conformance/`, `scripts/`, `.github/workflows/`, `.config/`;
- test-only package configuration;
- ResearchWorkspace transaction seams only if deterministic fault injection cannot be achieved externally;
- Phase 5 plan, ledger, completion report, and an ADR only if a durable testing-policy decision is required.

## Excluded Paths

- new product features, providers, persistence, UI behavior, or compatibility claims;
- changes to accepted scientific behavior merely to satisfy generated tests;
- live network calls in tests;
- coverage percentage as a release or scientific-correctness claim.

## Required Negative Cases

- non-finite and out-of-range numeric values;
- malformed, missing, duplicate, stale, foreign, and forged authority fields;
- malformed/truncated parser records and delimiter/escaping boundary cases;
- overlapping identity components in arbitrary order;
- concurrent workspace mutations and abandoned staging directories;
- controlled mutations of authority, digest, immutability, and human-decision inputs.

## Verification

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet test NexusScholar.Core.slnx -c Release --collect:"XPlat Code Coverage"
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
dotnet tool restore
./scripts/mutation-phase5.ps1
```

## Exit Checklist

- every reproduced defect is mapped to at least one permanent test;
- canonical, generated identity, parser mutation, malformed rehydration, and finite-number suites pass;
- real concurrent CLI processes cannot lose an import or create a valid mixed generation;
- interrupted staging content is ignored and cannot become current;
- semantic mutation matrices reject every selected authority, digest, immutability, and human-decision mutation;
- CI publishes informational coverage artifacts;
- full local and hosted Windows/Linux gates pass.
