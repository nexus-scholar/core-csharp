# Hardening 22 - Release and Security Workflows

Status: complete.

## Scope

- run the primary build, package smoke, tests, CLI smoke, and format gate on Windows and Linux with locked restore;
- retain per-OS TRX results and Linux coverage as workflow artifacts;
- review dependency changes on pull requests and reject high-severity additions or unapproved licenses;
- run manual-build CodeQL analysis and upload C# SARIF results;
- build, validate, retain, and attest release evidence on matching tags or manual dispatch.

## Safety Boundary

The release workflow is validation-only. It has no NuGet source, API key, signing certificate, or publish command. The protected `release` environment is a governance boundary for later publication work, not publication authorization.

Official actions use their current major runtimes as verified on 2026-07-13. The repository SDK remains exactly pinned by `global.json`; .NET SDK 8.0.422 is installed only to provide the .NET 8.0.28 runtime required by the pinned SBOM tool.

This gate makes no production, audit-grade, publication, or PHP compatibility claim.
