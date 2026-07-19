# Nexus Scholar Desktop 0.1.0-alpha.2 Technical Preview

Release type: controlled Windows x64 technical preview.

Release identity: immutable tag `v0.1.0-alpha.2`. The downloadable
`desktop-distribution-manifest.json` records the exact protected-main commit,
SDK, runtime identifier, restore-input digest, and every file in the ZIP.

## Download

Download these five assets from the GitHub prerelease:

- `NexusScholar-Desktop-0.1.0-alpha.2-win-x64.zip`
- `desktop-distribution-manifest.json`
- `SHA256SUMS.txt`
- `NexusScholar-Desktop-0.1.0-alpha.2-win-x64.spdx.json`
- `sbom-validation.json`

Verify the ZIP SHA-256 against `SHA256SUMS.txt`. GitHub CLI users can also run:

```powershell
gh attestation verify .\NexusScholar-Desktop-0.1.0-alpha.2-win-x64.zip `
  --repo nexus-scholar-org/core-csharp
```

Extract the ZIP to a new directory and run `NexusScholar.Desktop.exe`. It is
self-contained and does not require a separately installed .NET SDK or runtime.

## Included

- Local workspace open, initialize, Search-export import, analyze, and verify.
- Accepted FE-08 human review surfaces from Deduplication through verified
  reporting/export when their exact authority packages are present.
- Manifest-verified workspace backup under the workspace mutation lock.
- Byte-exact restore into a new directory, with no overwrite or merge behavior.
- Sanitized local crash reports and a next-launch notice.
- A source-bound distribution manifest, checksums, SPDX SBOM, and GitHub
  build-provenance attestation.

Backup and restore are operational recovery actions. They cannot create,
approve, correct, supersede, or otherwise become scientific authority.

## Diagnostics

Crash reports are stored under `%LOCALAPPDATA%\NexusScholar\diagnostics`.
Reports contain release identity, UTC time, failure source, and exception type.
They omit raw messages, stack traces, workspace contents, actor values,
credentials, scientific records, and workspace paths. Reports are bounded by
retention and remain local; there is no telemetry or upload.

## Security And Trust

The executable is not Authenticode signed. Windows may show a reputation
warning. SHA-256 checksums and GitHub attestation bind the downloadable bytes to
the release workflow; they do not claim a signed publisher identity.

The release workflow validates Core on Ubuntu, builds and executes the desktop
artifact on Windows, and publishes only from the exact matching protected-main
tag. Manual and branch runs have no publication path.

## Limitations

This release does not claim:

- production or compliance readiness;
- accessibility certification;
- installer, updater, rollback service, support SLA, or signed publisher;
- published or supported NuGet packages;
- authentication, multi-user collaboration, database, API, or cloud sync;
- built-in PDF parsing, OCR, scraping, or paywall bypass;
- plugin execution, arbitrary-code sandboxing, or AI/model execution;
- provider completeness or broad PHP compatibility.

Use disposable or backed-up research material for first-tester evaluation.
Report sensitive security issues through GitHub private vulnerability reporting,
not a public issue.
