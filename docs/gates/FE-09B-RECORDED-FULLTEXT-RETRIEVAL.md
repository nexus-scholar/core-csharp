# FE-09B: Recorded Full Text Retrieval

Status: Complete locally

Date: 2026-07-17

## Goal

Verify exact retained Full Text response bytes and explicit access evidence
before conversion into the existing Full Text artifact chain.

## Scope

- versioned retrieval and redirect evidence;
- explicit rights and access status;
- exact bytes, media type, size, completeness, and timestamps;
- successful conversion to existing acquisition/artifact evidence;
- deterministic recorded-byte tests with no network.

## Exit Criteria

- only explicitly admitted rights produce artifacts;
- redirects and content validation fail closed;
- failures remain audit evidence and never Screening decisions;
- CF-026 records the narrow recorded-contract resolution;
- full build, tests, format, architecture, and package gates pass.

## Nonclaims

No live downloader, scraping, paywall bypass, shadow library, authenticated
publisher automation, OCR/PDF parsing, PHP parity, or legal certification.

## Completion Evidence

Implemented versioned recorded retrieval evidence, explicit rights and route
validation, HTTPS redirect policy, exact-byte verification, failure outcomes,
and successful conversion into the existing Full Text authority chain.

Focused tests: 11 passed. Full solution: 1,011 passed, 2 opt-in live tests
skipped. Release build and format verification passed.
