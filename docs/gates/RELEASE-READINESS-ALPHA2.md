# Release Readiness Alpha 2

Status: Accepted on 2026-07-19.

Authority: ADR 0046.

## Goal

Publish one source-bound, self-contained Windows x64 technical-preview artifact
for controlled first testers while preserving local-first scientific authority,
package nonpublication, workspace integrity, and explicit production nonclaims.

## Sources of truth

1. `AGENTS.md`
2. ADR 0046
3. ADR 0024 for package topology and all clauses not superseded by ADR 0046
4. ADR 0023 for transactional workspace generations
5. ADRs 0035-0038 for the desktop and human-authority boundary
6. `eng/package-topology.json`
7. Current Research Workspace and desktop implementation

No PHP behavior or fixture defines desktop distribution, crash diagnostics, or
workspace backup/restore.

## Dependency-ordered tasks

### RR-01: release contract

Owner: `docs/adr`, `docs/gates`.

- Accept the technical-preview scope, artifact identity, publication boundary,
  unsigned status, recovery contract, and nonclaims.

### RR-02: documentation truth reset

Owner: repository and public product documentation.

- Reconcile protected-main state and completed FE-08 slices.
- Add version-specific changelog and release notes.
- Document supported runtime, clean-host path, checksums, attestation,
  diagnostics, backup/restore, and limitations.
- Preserve historical evidence as historical rather than rewriting it.

### RR-03: portable desktop distribution

Owner: `eng`, `scripts`, `src/NexusScholar.Desktop`, release workflow.

- Add machine-readable distribution policy.
- Add product/version/icon metadata.
- Publish `win-x64`, self-contained, portable output twice.
- Compare complete file inventories and digests.
- Create a deterministic ZIP, distribution manifest, checksums, and SPDX SBOM.
- Run the extracted executable without repository or .NET environment
  dependencies.

### RR-04: runtime resilience and recovery

Owner: `src/NexusScholar.ResearchWorkspace`,
`src/NexusScholar.Desktop.AppServices`, and `src/NexusScholar.Desktop`.

- Add manifest-verified backup and new-directory restore.
- Add framework-neutral preview/result contracts and desktop controls.
- Add sanitized local crash reports and next-launch recovery notice.

### RR-05: native acceptance

Owner: desktop acceptance tests.

- Initialize a real Avalonia application using the headless platform.
- Exercise initialize, import, analyze, verify, backup, restore, reopen, and
  failure recovery through desktop-visible command surfaces.
- Inspect critical automation names and keyboard focus.
- Verify usable layout at 100%, 125%, and 150% rendering scale.

### RR-06: release execution

Owner: `.github/workflows/release-validation.yml` and release evidence.

- Keep core validation on Ubuntu.
- Build and smoke the Windows artifact on Windows.
- Upload test and release evidence on all runs.
- Attest artifacts and publish only for the exact matching tag.
- Verify the final GitHub prerelease and downloadable assets.

## Required negative cases

- package version and tag differ;
- branch build attempts publication;
- desktop publish is framework-dependent or has the wrong RID;
- repeated publish inventories differ;
- executable needs an installed .NET runtime;
- archive target is inside the workspace;
- source changes during backup;
- archive contains an absolute, escaping, duplicate, linked, extra, missing, or
  digest-mismatched entry;
- restore target already exists;
- failed restore leaves a promoted partial workspace;
- crash diagnostics expose messages, paths, actor values, credentials, or
  scientific contents;
- UI mutation bypasses preview/confirm authority paths.

## Allowed paths

- `.github/workflows/release-validation.yml`
- `.gitignore`
- `CHANGELOG.md`
- `Directory.Build.props`
- `Directory.Packages.props`
- `NexusScholar.Core.slnx`
- `README.md`
- `PLANS.md`
- `SECURITY.md`
- `docs/adr`
- `docs/gates`
- `docs/ops`
- `docs/release`
- `docs/ui`
- `eng`
- `scripts`
- `site/src/pages/status`
- `site/src/pages/tutorials/getting-started`
- `site/src/pages/developers/modules/desktop`
- `src/NexusScholar.ResearchWorkspace`
- `src/NexusScholar.Desktop.AppServices`
- `src/NexusScholar.Desktop`
- focused Research Workspace, AppServices, desktop, architecture, and acceptance
  tests

## Excluded paths and scope

- no domain behavior outside Research Workspace recovery;
- no PHP fixtures or compatibility changes;
- no NuGet publication;
- no installer, updater, code-signing certificate, telemetry, network storage,
  cloud sync, authentication, PDF/OCR expansion, plugin runtime, or AI runtime;
- no rewrite of historical authority records or accepted completion evidence.

## Verification

```powershell
./scripts/verify-release-policy.ps1
./scripts/verify-desktop-portable.ps1
./scripts/verify.ps1
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build --filter "TestCategory!=LiveProvider"
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
git diff --check
```

Hosted verification additionally requires:

- `verify (ubuntu-latest)`
- `verify (windows-latest)`
- `analyze`
- `review`
- release core validation
- Windows desktop distribution validation
- artifact attestation
- tag-bound GitHub prerelease publication

## Exit checklist

- [ ] The tag, distribution manifest, and release identity bind the exact
  protected-main release commit.
- [x] Version-specific changelog and release notes exist.
- [x] Portable ZIP is self-contained and reproducible by file inventory.
- [ ] Distribution manifest, checksums, SPDX SBOM, and attestation exist.
- [x] Extracted executable passes clean-directory smoke without a .NET runtime.
- [x] Backup archive reopens and verifies before success.
- [x] Restore is byte-exact and new-directory-only.
- [x] Crash reports are local, sanitized, bounded, and tested.
- [x] Native headless acceptance covers the main desktop and recovery flow.
- [x] Automation labels, keyboard focus, and three scaling levels pass.
- [x] Full local build, tests, mutation, packages, format, and release evidence pass.
- [ ] Protected-main CI is green at the release commit.
- [ ] `v0.1.0-alpha.2` resolves to that commit.
- [ ] GitHub prerelease assets download and match published checksums.

## Claims

Completion authorizes only a controlled Windows x64 technical preview. It does
not authorize production, compliance, accessibility certification,
authenticated-user, multi-user, installer/update, signed-publisher, NuGet,
provider-completeness, PDF/OCR, PHP-parity, plugin-runtime, or AI-runtime claims.
