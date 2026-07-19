# Chat Roster

Status date: 2026-07-19

## Active Lanes

- Main integration lane: last pre-release protected `main` at `425e9bc`.
- Active implementation lane: `cdx/release-readiness-alpha2`; Windows portable
  distribution, runtime recovery, native acceptance, and release execution.
- Product planning lane: FE-10 plugin-runtime design and capability security;
  no runtime implementation yet.
- Public documentation lane: current project docs and `site/` on `main`.
- Compatibility lane: case-scoped fixture evidence only; no broad PHP claim.
- Release/security lane: validation-only NuGet packages, one unsigned desktop
  prerelease, governance, dependency review, CodeQL, SBOM, checksums,
  attestation, and release evidence.

## Ownership

- Gate manager: dependency order, accepted ADR/gate status, completion evidence,
  and protected-main closeout.
- Scientific owner: human authority, Protocol and criteria binding, immutable
  history, invalidation, and claim boundaries.
- Application owner: command contracts, durable local transactions, stale-state
  checks, recovery, and projections.
- Desktop owner: composition root, interaction states, accessibility, and
  routing only through admitted application commands.
- Release manager: package policy, reproducibility, SBOM, validation workflows,
  governance verification, and rehearsal artifacts.
- Security owner: dependency review, CodeQL, threat boundaries, private
  vulnerability reporting, and security policy.
- Documentation owner: README, roadmap, Pages, module maps, CLI references,
  branch board, merge queue, and roster.

## Current Authority Boundary

- FE-01 through FE-09 are complete within accepted scope.
- The desktop routes the accepted local review continuation through
  `NexusScholar.Desktop.AppServices`, including title/abstract Screening,
  corrections and adjudication, local Full Text conduct, reporting, Bundle v2,
  and export verification.
- FE-09 admits bounded live Search providers, policy-specific provider cache
  evidence, recorded-byte Full Text retrieval verification, and local
  direct-citation snapshots.
- UI state, paths, row ids, selection, and action descriptors are never
  scientific authority.
- Local file-backed generations and ledgers are durable persistence. Database,
  API, cloud, synchronization, authentication, and multi-user behavior remain
  absent.

## Release Readiness Work (RR-01..RR-06)

- RR-01: release contract and claims boundary.
- RR-02: current public and operator documentation.
- RR-03: locked, reproducible Windows x64 portable distribution.
- RR-04: sanitized local diagnostics and verified backup/restore.
- RR-05: native Avalonia acceptance and accessibility metadata.
- RR-06: protected-main validation, tag-only publication, attestation, and
  downloaded-asset verification.

## Current Non-Claims

- early alpha, not production-ready;
- no published or signed NuGet packages; the desktop prerelease is unsigned;
- no broad PHP compatibility;
- no live Crossref or Full Text retrieval, scraping, or built-in PDF/OCR;
- no unrestricted provider caching, provider parity, or live citation graph;
- no plugin runtime or arbitrary-code sandbox;
- no live model execution or AI decision authority;
- no database, server API, cloud sync, authentication, tenancy, or multi-user
  collaboration.

## Historical Lane

The `gh-pages` branch is historical. Current Pages deployments use `site/` on
`main` through `.github/workflows/pages.yml`.
