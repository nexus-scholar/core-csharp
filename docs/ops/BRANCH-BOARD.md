# Codex Branch Board

Source: live branch probes after main consolidation, remote push, remote `cdx/*` cleanup, and hosted CI.

## Main Baseline

- Current `main`: `ebb7bba` (`docs: refresh readmes after main consolidation`).
- Current `origin/main`: `ebb7bba`.
- Current public site branch: `gh-pages` / `origin/gh-pages` at `53d7aa4`.
- Remote heads are only `main` and `gh-pages`.
- Local heads are only `main` and `gh-pages`.
- Hosted `main` CI run `28380516236` passed Ubuntu and Windows restore, build, test, and format.

## Main Contains

- Gate 0 through Gate 6 local foundations.
- Gate 9 Shared Identity.
- Gate 9 Search and Search Import.
- Gate 9 Deduplication.
- Gate 9 Screening.
- Gate 9 Full Text reconnaissance, ADR 0014 contract, and local no-network Full Text implementation.
- UI contracts and sample block plans.
- Avalonia block renderer prototype.
- Avalonia sample host.
- README and review docs refreshed after consolidation.

## Branch Classes

- merged: all prior local `cdx/*` branches needed for the current baseline have been merged, cherry-picked, or superseded into `main`.
- cleanup: none pending locally or remotely.
- active: `main`.
- review: none.
- blocked: PHP compatibility claims, generated PHP fixtures, persistence/API/UI/cloud, live provider/network behavior, Scopus API, Web of Science API, Google Scholar scraping, paywall bypass, shadow-library sources, AI governance beyond proposal contracts, full-text artifact storage, actual PDF parsing, OCR, and app integration claims remain out of scope.
- stale: none retained locally or remotely.
- public-site: `gh-pages`.

## Remote Cleanup State

`git ls-remote --heads origin`:

```text
53d7aa429471faf65ea6b94c3febd1015c1e94a1 refs/heads/gh-pages
ebb7bba6131c27a5608a63d302de555b26db849e refs/heads/main
```

Remote branches deleted in this consolidation:

- `cdx/gate-9-fulltext-contract`
- `cdx/gate-9-fulltext-recon`
- `cdx/ui-phase-3-5-avalonia-sample-host`
- `cdx/ui-phase-3-avalonia-renderer`

## Safe Cleanup Candidates

None.

## Not Safe To Delete

- `main`
- `gh-pages`
- `origin/main`
- `origin/gh-pages`

## Next Work

Recommended next branch should be created fresh from current `main`.

Best next work:

1. Public onboarding and first-tester walkthrough.
2. Feedback issue templates and PR template.
3. Maintainer routing docs refresh (`CODEX-START-HERE.md`, `PLANS.md`).
4. AppServices/read-only block composition planning for Import + Dedup.

## Unresolved Boundaries

- `CF-025`: implemented/resolved for local Full Text artifact evidence; preserve exact raw bytes plus `raw-artifact-bytes` digest identity.
- `CF-026`: narrowed; live provider/network and legal-access behavior remain future.
- `CF-027`: narrowed for Core; app rows and paths remain projections unless transformed into Core Full Text records.
- `CF-024`: Screening app workflow rows remain projections.
- `CF-019`: Search import remaining parser families and live provider/API integration remain future.
