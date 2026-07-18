# Security Policy

Nexus Scholar Core is currently a local-first research workflow kernel and sample/demo repository. It is not a hosted service, cloud product, live-provider client, or production desktop application.

## Supported Scope

Security reports are in scope when they affect:

- local repository behavior;
- build, test, or CLI execution;
- sample-host execution;
- dependency or supply-chain risk;
- accidental live network/provider behavior;
- accidental persistence/API/cloud behavior;
- evidence, provenance, or authority-boundary integrity.

The current repository does not provide:

- hosted accounts or authentication;
- server-side API endpoints;
- cloud sync;
- provider credentials;
- live scholarly provider calls;
- PDF/OCR processing services;
- production desktop-shell behavior.

## Reporting

For non-sensitive local failures, use the bug report template:

https://github.com/nexus-scholar-org/core-csharp/issues/new?template=bug-report.yml

For potentially sensitive security issues, use [GitHub private vulnerability reporting](https://github.com/nexus-scholar-org/core-csharp/security/advisories/new). This is the single private reporting channel for the repository. Do not include exploit details or private data in a public issue. File a public tracking issue only after details can be safely disclosed.

## Expectations

- Include the commit, OS, .NET SDK version, exact command, expected result, and actual result.
- Do not test against live providers, third-party services, paid databases, or private data unless a maintainer explicitly authorizes that scope.
- Do not request or submit provider credentials, API keys, PDFs under restricted access, or private research data.

## Boundary Reminder

Security fixes must preserve the same scientific boundaries as other changes: automation is not authority, paths are not scientific identity, app projections are not Core records, and live provider/network behavior remains out of scope until accepted by a later ADR.
