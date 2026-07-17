# Chat Roster

Status date: 2026-07-17

## Active Lanes

- Main integration lane: protected `main` at `805f3d6`.
- Product planning lane: FE-08 Slice 5 ADR/gate design; no implementation yet.
- Public documentation lane: current project docs and `site/` on `main`.
- Compatibility lane: case-scoped fixture evidence only; no broad PHP claim.
- Release/security lane: validation-only packages, governance, dependency
  review, CodeQL, and release evidence.

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

- FE-01 through FE-07 and FE-08 slices 1 through 4 are complete.
- The desktop can execute accepted FE-02 Deduplication commands through
  `NexusScholar.Desktop.AppServices`.
- FE-08 Slice 4 resolves durable Screening authority and exposes readiness
  read-only. It does not admit a desktop Screening decision.
- UI state, paths, row ids, selection, and action descriptors are never
  scientific authority.
- Local file-backed generations and ledgers are durable persistence. Database,
  API, cloud, synchronization, authentication, and multi-user behavior remain
  absent.

## Current Non-Claims

- early alpha, not production-ready;
- no published or signed NuGet packages;
- no broad PHP compatibility;
- no live providers, scraping, or built-in PDF/OCR;
- no plugin runtime or arbitrary-code sandbox;
- no live model execution or AI decision authority;
- no database, server API, cloud sync, authentication, tenancy, or multi-user
  collaboration.

## Historical Lane

The `gh-pages` branch is historical. Current Pages deployments use `site/` on
`main` through `.github/workflows/pages.yml`.
