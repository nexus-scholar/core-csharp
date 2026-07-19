# Changelog

## 0.1.0-alpha.2 - 2026-07-19

Windows x64 technical preview. Not production-ready.

### Added

- Self-contained portable desktop ZIP with exact commit/version/RID manifest.
- Dedicated locked `win-x64` restore topology and repeated-publish inventory
  comparison.
- SHA-256 checksums, SPDX SBOM, GitHub provenance attestation, extracted-host
  smoke, and exact-tag prerelease publication.
- Manifest-verified workspace backup and byte-exact restore into a new folder.
- Sanitized, bounded, local-only crash diagnostics and next-launch notice.
- Native Avalonia headless acceptance for the visible desktop and recovery flow.

### Preserved

- FE-01 through FE-09 remain within their accepted local scopes.
- FE-08 desktop slices 1 through 9 remain complete.
- Twenty-four NuGet packages remain validation-only and unpublished.
- Scientific mutations still require an exact preview, authorized human action,
  immutable evidence, and durable provenance.

### Limitations

- Unsigned executable; no installer, updater, or Windows publisher identity.
- No production, compliance, accessibility-certification, authentication,
  multi-user, database, API, cloud-sync, PDF/OCR, plugin-runtime, or AI-runtime
  claim.
- No broad PHP compatibility claim and no NuGet publication.

See [the version-specific release notes](docs/release/RELEASE-NOTES-0.1.0-alpha.2.md).
