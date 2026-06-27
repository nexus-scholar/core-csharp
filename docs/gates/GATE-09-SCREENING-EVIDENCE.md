# Gate 09 Screening Evidence (Local Scope)

Branch: `cdx/gate-9-screening-local`

Date: `2026-06-27`

## Local implementation scope reached

- Screening candidate-set intake and proposal/decision contracts are implemented against ADR 0013 in `src/NexusScholar.Screening`.
- Criteria digest now uses ADR 0002 canonical JSON record rules with canonicalized content, stage included in digest input, and stage-aware criteria lookup.
- Final criteria lookups are stage-bound by `criteria_id + stage` and mismatch is rejected as unknown criteria scope.
- Final decisions require criteria bound to an approved protocol version id, a parseable `sha256` content digest, and explicit `protocol-content` digest scope.
- Human-only finality remains enforced (human actor + rationale + lock checks + path/projection guards).
- AI/model/rule outputs remain suggestion records only; they cannot be added as final decisions.
- Conflicts are detected and unresolved lower-stage conflicts block downstream stage handoff.
- Adjudication records preserve source decision references and mark conflicts as resolved rather than mutating prior decisions.
- App assignment/batch/conflict/audit rows and relative local file names are rejected as Core Screening authority.

## Fixtures covered

Conformance fixtures under `fixtures/conformance/screening/` now include:

- `screening-criteria-canonical-digest.json`
- `screening-criteria-key-order-stable.json`
- `screening-criteria-stage-specific.json`
- `screening-input-dedup-result-candidates.json`
- `screening-input-locked-candidate-set.json`
- `screening-input-raw-search-trace-rejected.json`
- `screening-human-include-decision.json`
- `screening-human-exclude-decision.json`
- `screening-human-needs-review-decision.json`
- `screening-human-missing-actor-negative.json`
- `screening-human-missing-rationale-negative.json`
- `screening-confidence-bounds-negative.json`
- `screening-ai-suggestion-not-final.json`
- `screening-conflict-created-from-disagreement.json`
- `screening-conflict-resolved-by-human.json`
- `screening-unresolved-conflict-blocks-handoff.json`
- `screening-adjudication-source-decision-links.json`
- `screening-app-assignment-projection-not-authority.json`
- `screening-cli-file-output-not-core-authority.json`

All Screening fixtures are hand-authored local contract fixtures with fixture id, local source metadata, source refs, comparison rules, and parseable input/output digest metadata. They are not PHP-generated fixtures.

## Local verification

- `dotnet restore NexusScholar.Core.slnx`: passed
- `dotnet build NexusScholar.Core.slnx -c Release --no-restore`: passed
- `dotnet test NexusScholar.Core.slnx -c Release --no-build`: passed
  - Architecture: 15 passed
  - Conformance: 76 passed
  - Core: 175 passed
- `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`: passed
- `powershell -ExecutionPolicy Bypass -File .\scripts\verify.ps1`: passed

Hosted Windows/Linux matrix evidence for the review-fix commit is not recorded yet; branch must be pushed and verified before merge.

## Leila review fixes

Focused review on prior commit `faacc9c` found the branch not safe to merge. This fix set addresses:

- protocol binding now enforces approved status, parseable digest, `protocol-content` scope, and stale digest mismatch rejection;
- `screening-input-locked-candidate-set.json` now exercises `locked-reviewable-candidate-set` independent of a Dedup result;
- app projection rejection includes app conflict/batch/audit row patterns;
- full-text decisions require digest-bound artifact evidence and reject relative file names;
- resolved adjudication prevents conflict resurrection for the same candidate/stage/criteria;
- fixtures now carry local-contract metadata and digest-shaped metadata.

## Open conflicts impacted

- CF-021 (input + candidate lock boundary): implemented for local ADR 0013 scope.
- CF-022 (human authority + AI proposal boundary): implemented for local ADR 0013 scope.
- CF-023 (criteria schema and digest contract): implemented for local ADR 0013 scope.
- CF-024 (app workflow projection boundary): narrowed for Core by ADR 0013.

## Explicit non-claims

- no PHP compatibility
- no PHP-generated fixtures
- no persistence/API/UI/cloud behavior
- no CLI/Web behavior changes
- no app behavior made authoritative
- no live LLM/provider/network behavior
- no AI governance implementation
- no full-text retrieval implementation
- no artifact storage behavior
- no Search or Deduplication behavior changes
- no bundle behavior change
- no blueprint conformance
