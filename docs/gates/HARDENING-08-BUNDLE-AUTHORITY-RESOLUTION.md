# Hardening 08: Bundle Authority Resolution

Status: accepted for implementation

## Goal

Verify Bundle Protocol, Workflow, template, and Provenance bindings against resolved authoritative records instead of caller-owned digest dictionaries.

## Sources

1. `AGENTS.md`
2. `docs/adr/0009-portable-review-bundle.md`
3. `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`
4. Hardening 03, 04, and 07 verified authority contracts
5. `docs/adr/0020-bundle-authority-dependency-direction.md`

## Required Behavior

- resolve the exact approved Protocol identity, version number, and content digest;
- resolve exact verified Workflow definition, template identity/version/digest, and bound Protocol;
- resolve complete Provenance event identity, digest, activity, timestamp, and actor;
- require artifact provenance ID/digest pairs to be complete;
- require every artifact schema to appear in required schemas;
- preserve artifact bytes, path, overwrite, and manifest digest verification;
- keep archive parsing and atomic import out of scope.

## Allowed Paths

- `docs/gates/HARDENING-08-BUNDLE-AUTHORITY-RESOLUTION*.md`
- `docs/adr/0020-bundle-authority-dependency-direction.md`
- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `src/NexusScholar.Bundles/`
- `src/NexusScholar.Cli/CliApplication.cs`
- `tests/NexusScholar.Core.Tests/BundleTests.cs`
- `tests/NexusScholar.Architecture.Tests/DependencyRulesTests.cs`
- `tests/NexusScholar.Conformance.Tests/BundleFixtureTests.cs`
- `fixtures/conformance/bundle/`

## Verification

Run solution build, focused Bundle and conformance tests, architecture tests, full solution tests, formatting, `scripts/verify.ps1`, and `git diff --check`.

## Exit Checklist

- [x] Loose digest maps no longer establish authority.
- [x] Full Protocol, Workflow/template, and Provenance records are resolved.
- [x] Artifact schema closure and provenance pairs are enforced.
- [x] Historical fixtures remain unchanged.
- [x] Local verification passes; hosted verification is required before merge.
- [x] Evidence records risks, ADR impact, and compatibility impact.
