# Release Readiness Alpha 2 Completion Evidence

Status: implementation and local validation complete. Release closure is valid
only when the exact protected-main commit is tagged `v0.1.0-alpha.2`, hosted
checks pass, and the five immutable prerelease assets verify.

Date: 2026-07-19

Authority:

- ADR 0046;
- accepted Release Readiness Alpha 2 gate;
- the protected-main commit resolved by `v0.1.0-alpha.2`;
- `desktop-distribution-manifest.json` for exact source and artifact identity.

## Delivered Scope

- RR-01: accepted Windows x64 technical-preview distribution, recovery,
  publication, and nonclaim contract.
- RR-02: current repository, product, security, UI, site, changelog, and
  version-specific release documentation.
- RR-03: self-contained portable desktop ZIP, dedicated locked runtime graph,
  repeated-publish inventory comparison, exact distribution manifest,
  checksums, SPDX SBOM, extracted-host smoke, and tag-only publication policy.
- RR-04: lock-aware manifest backup, verified byte-exact new-directory restore,
  failure cleanup, sanitized bounded local crash diagnostics, and next-launch
  recovery notice.
- RR-05: rendered Avalonia headless acceptance through initialize, import,
  analyze, verify, backup, restore, reopen, failure recovery, keyboard focus,
  pointer input, automation names, and 100%, 125%, and 150% scaling.
- RR-06: split Ubuntu and Windows release validation, artifact attestation,
  immutable-release enforcement, downloaded-asset byte comparison, and
  exact-tag prerelease publication.

## Local Verification

The complete pre-commit gate passed on Windows under pinned SDK `10.0.301`:

- 60-project Release build: zero warnings and zero errors;
- full solution: 1,111 passed, zero failed, four expected skips;
- Architecture: 45 passed;
- Conformance: 142 passed;
- desktop acceptance: 2 passed, including rendered scaling and input;
- scientific-invariant manifest: 150 exact cases across nine projects;
- reproducible package validation: 24 packages with clean local-source smoke;
- release evidence: 28 artifacts and 60 lock files;
- portable desktop: 268 files, two-publish inventory match, extracted clean
  smoke, and exact five-file release root;
- release-policy regressions: tag reuse, wrong RID, branch publication,
  existing-release mutation, and dirty-manifest publication all rejected;
- CLI doctor, sample, and deterministic local demo: passed;
- pinned-SDK format verification: passed;
- public Astro site verification: 49 source files, 45 generated pages, and zero
  distribution issues.

The four skips are two live-provider probes that require explicit opt-in and
two Linux-only path assertions on the Windows host. Default CI remains free of
live scholarly-provider calls. Dirty working-tree evidence is validation-only;
the release artifact must be regenerated with `sourceTreeDirty=false` after
the final commit.

## Independent Review

Independent scientific-invariant, architecture, conformance, and test-gap
reviews reproduced and closed findings in:

- release identity and clean-source enforcement;
- downloaded-asset and immutable-release verification;
- truthful measured desktop smoke output;
- backup/restore failure injection and promotion collision behavior;
- diagnostic redaction, test-only boundaries, and timestamp uniqueness;
- exact release-root inventory and publication ordering;
- rendered input, hit testing, focus, and scaling acceptance.

No local code or test finding remains open. The only intentionally external
closure condition is the protected-main tag and published GitHub prerelease.

## Release Closure

Completion requires all of the following to hold for one exact commit:

1. protected-main `analyze`, `review`, Ubuntu, and Windows checks are green;
2. `v0.1.0-alpha.2` resolves to that protected-main commit;
3. the distribution manifest records that commit and a clean source tree;
4. the GitHub prerelease is neither draft nor mutable through this workflow;
5. the release exposes exactly the ZIP, manifest, checksums, SPDX SBOM, and
   SBOM-validation assets;
6. downloaded bytes match local publication bytes and `SHA256SUMS.txt`; and
7. GitHub artifact attestation verifies for the released ZIP.

The authoritative release endpoint is:

`https://github.com/nexus-scholar-org/core-csharp/releases/tag/v0.1.0-alpha.2`

## Invariants And Compatibility

- Scientific authority remains in immutable, digest-bound Core records.
- Backup, restore, diagnostics, manifests, UI values, and release metadata do
  not become scientific authority.
- Human preview and confirmation remain required for admitted desktop
  mutations.
- No golden fixture or `specs/SOURCE.lock.json` entry changed.
- No PHP compatibility claim is added.

## Nonclaims

This release does not authorize production, compliance, accessibility
certification, authenticated-user, multi-user, installer/update,
signed-publisher, NuGet publication, provider-completeness, PDF/OCR,
plugin-runtime, AI-runtime, database, API, cloud, or support-SLA claims.
