# Hardening 30 - Post-Phase 7 Review Remediation

Status: complete; protected-branch merge verified by PR #54 at merge `32d3c5c`.

## Scope

H30 closes the actionable defects found by the independent review of protected `main` after Phase 7. It does not add product surface or broaden compatibility claims.

## Behavior Implemented

- Removed the public `AiProposal<T>.Accept` transition and `AcceptedAiProposal<T>` record because accepted ADRs explicitly defer AI governance. AI proposals now bind a factory-validated task policy, snapshot evidence, enforce required evidence, and require a non-default UTC timestamp.
- Full Text rehydration now compares every persisted input field and the artifact-level candidate-set, screening-decision, work, and dedup-cluster links.
- Full Text acquisition chains allow failed/skipped attempts from different source kinds before or after the accepted attempt, while requiring exactly one success that matches the acquisition source/kind and any artifact metadata it carries.
- Manual and user-supplied Full Text acquisitions now require a `human` or `import` actor kind under ADR 0014.
- BibTeX author parsing now splits only top-level `and` separators and preserves comma-form single names such as `Smith, John`.
- H28 conformance now rejects empty classification authority-reference lists.
- The active validation package identity advances to `0.1.0-alpha.2`; release policy checks props/topology/smoke alignment and rejects reuse of a tag at another commit.

## Regression Evidence

- Complete Full Text input-link mutation matrix on both acquisition and artifact references.
- Artifact-level candidate, candidate-set, screening, work, and dedup-cluster mutation rejection.
- Mixed source failure/skipped/success rehydration, later skipped-evidence preservation, second-success rejection, and complete accepted-attempt binding mutations.
- Human/import actor acceptance and automation/plugin/system/unknown actor rejection.
- AI policy-construction guard, proposal evidence snapshot, required/invalid evidence rejection, UTC timestamp validation, and reflection guard proving no public acceptance transition.
- Single comma-form BibTeX author preservation.
- Negative H28 authority-reference guard.

## Invariants

- AI output remains a proposal until a later accepted ADR defines authority, evidence, provenance, and human-action semantics.
- Full Text accepted bytes cannot be rehydrated through a partially matching scientific identity chain.
- The first successful Full Text source attempt is authoritative for the acquisition; prior failures and skips remain evidence.
- Validation package bytes cannot reuse the historical `v0.1.0-alpha.1` identity.

## Non-Claims

- No governed AI execution or acceptance implementation.
- No live Full Text retrieval, OCR, PDF extraction, persistence, API, UI, or network behavior.
- No package publication or signing.
- No compatibility claim beyond the existing generated Phase 7 case inventories.

## ADR And PHP Impact

- No ADR changes. The AI change enforces the existing deferral in ADRs 0008 and 0013; Full Text changes enforce ADR 0014. ADR 0025 defines the first `0.1.0-alpha.1` validation set and unchanged twelve-package topology; advancing that same validation-only set to `0.1.0-alpha.2` does not add packages or authorize publication.
- No golden fixtures were changed. PHP behavior remains evidence only.

## Verification

- `./scripts/verify.ps1`: passed.
- Release build: passed with zero warnings and zero errors.
- Full solution: 573 tests passed, including 296 Core, 123 Conformance, and 25 Architecture tests.
- `./scripts/mutation-phase5.ps1`: 47 Core and 7 Conformance mutation cases passed.
- Package verification: 12 packages at `0.1.0-alpha.2` reproduced normalized content and passed clean local-source smoke.
- Release-policy regression: matching tag and untagged next version accepted; reused tag at a different commit rejected under Windows PowerShell.
- SBOM/release-evidence generation, CLI doctor/sample/demo, and format verification passed.
