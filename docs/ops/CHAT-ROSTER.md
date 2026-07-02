# Chat Roster

Branch-derived Codex lane roster from current git state after the public local Research Workspace CLI workflow landed.

## Active Lanes

- Lane `main`: current implementation baseline at `0d93bd9`.
- Lane `gh-pages`: public documentation site at `589fc2e`.
- Lane `cdx/ops-refresh-after-cli-workflow`: docs-only ops refresh branch.

There are no active implementation `cdx/*` branches locally or remotely.

## Branch Containment Relationships

- `main` contains the implemented local review pipeline through Search, Import, Deduplication, Screening, local no-network Full Text, AppServices read-only workspace composition, and the local Research Workspace CLI loop through PR08.
- `main` contains UI contracts, sample block plans, Avalonia renderer prototype, and the polished Avalonia sample host.
- `main` contains README, issue templates, PR template, local CLI `doctor`, `sample`, deterministic `demo`, and the Research Workspace commands: `init`, `status`, `import search`, `verify`, `analyze`, `review`, and `clusters`.
- `gh-pages` contains the public first-tester getting-started walkthrough and the public Research Workspace CLI walkthrough.
- `gh-pages` remains separate public-site history.

## Status Notes

- Public feedback onboarding is merged on `main`.
- Public Research Workspace CLI workflow docs are merged on `main`.
- Public Research Workspace CLI walkthrough is merged on `gh-pages`.
- The sample host is still a sample-only visual inspection harness, not a product shell.
- ADR 0014 defines the Full Text input boundary, acquisition records, source attempts, artifact evidence records, raw byte digest identity, extraction records, failure categories, legal/access boundary, app projection boundary, and Screening handoff.
- Local C# Full Text implementation is no-network only.
- Raw artifact identity is exact bytes plus `raw-artifact-bytes` digest.
- Derived extraction evidence must bind back to source artifact id and raw digest, and must not replace raw artifact evidence.
- PHP `pdf_fetches`, CLI manifests, Web batches/items, app audit rows, storage paths, and download routes are projections unless transformed into ADR 0014 records.
- Live providers, scraping, paywall bypass, shadow libraries, artifact storage, actual PDF parsing, OCR, and app behavior as Core authority remain unclaimed.

## Recommended Next Conversation

Pause feature expansion and collect first-tester feedback on the public CLI workflow:

1. classify feedback as docs/site polish, CLI usability polish, or later ADR candidate;
2. keep review/cluster commands read-only until a later decision boundary exists;
3. keep provider/network/legal work planning-only unless a later accepted ADR/task authorizes implementation.

Do not start merge-decision execution, persistence, providers, UI product shell, PDF/OCR, AI/model calls, or AppServices expansion without a specific accepted task/ADR.

## Explicit Non-Claims For Next Lane

- no PHP compatibility
- no PHP-generated fixtures
- no persistence/API/cloud
- no live provider/network behavior
- no provider SDKs or credentials
- no paywall bypass
- no shadow-library source
- no Google Scholar scraping
- no actual PDF text extraction
- no OCR
- no artifact storage implementation
- no Screening behavior change
- no executable merge decisions
- no app behavior made authoritative
