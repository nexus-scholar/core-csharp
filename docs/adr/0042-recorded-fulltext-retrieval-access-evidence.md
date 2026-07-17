# ADR 0042: Recorded Full Text Retrieval Access Evidence

Status: Accepted

Date: 2026-07-17

## Context

ADRs 0014 and 0032 deliberately block network Full Text retrieval. FE-09B must
define rights, redirects, exact bytes, and failure evidence before any transport
is admitted. PHP path-based retrieval rows do not provide C# artifact identity.

## Decision

FE-09B first admits a provider-neutral, network-free recorded retrieval contract
in `NexusScholar.FullText`. It verifies retained response bytes and may convert a
successful retrieval into the existing acquisition and artifact evidence chain.

The record binds the admitted Full Text candidate, source alias, HTTPS source
reference, access route, explicit rights status and rights reference, redirect
chain, HTTP status, media type, exact byte digest and length, request and receipt
timestamps, retention disposition, completeness, content encoding, and terminal
failure category.

Only explicitly open-access, licensed, or public-domain evidence can produce a
successful artifact. Closed, unknown, inaccessible, failed, incomplete,
encoded, oversized, mutated, or content-type-mismatched responses remain
failure evidence and cannot become screenable artifacts.

Redirects must remain HTTPS, contain no user information, use admitted DNS
hosts, and may not target IP literals. Artifact identity is exact accepted bytes
under `raw-artifact-bytes`; URLs and paths remain references only. Retrieval
never creates a Screening verdict.

## Consequences

CI can prove lawful-access and exact-byte invariants from recorded fixtures
without network calls. This resolves CF-026 only for the recorded retrieval
contract.

No live Full Text transport, scraping, authenticated publisher automation,
paywall bypass, shadow library, OCR, PDF parser, cache, provider parity, PHP
parity, legal certification, or production claim is introduced.

## Reversal Conditions

A live host requires a successor ADR naming exact providers, hosts, methods,
redirect policy, credential policy, byte limits, encodings, and retention.
