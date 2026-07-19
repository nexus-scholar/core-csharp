# ADR 0046: Windows Technical Preview Distribution and Recovery

- Status: Accepted
- Date: 2026-07-19
- Decision owner: Nexus Scholar Core maintainers
- Supersedes: ADR 0024 for the desktop technical-preview artifact only

## Context

Protected `main` contains the accepted FE-01 through FE-09 scopes and a
Windows-first Avalonia desktop host. The repository can build, test, pack
validation-only libraries, generate an SBOM, and attest workflow artifacts, but
it does not produce an end-user desktop artifact. ADR 0024 intentionally
prohibits publication until a later gate defines a destination, support
boundary, provenance, and clean-host validation.

The desktop also lacks process-level crash diagnostics and a verified
workspace backup/restore operation. Research Workspace mutations already use a
single project lock, pointer-last publication, immutable generations, and
workspace-relative path validation. Recovery must preserve those rules rather
than copy files through UI code.

This decision authorizes a narrow technical preview. It does not declare the
application suitable for production research conduct.

## Decision

### Release identity and audience

Version `0.1.0-alpha.2` may be published as a GitHub prerelease named
`Nexus Scholar Desktop 0.1.0-alpha.2 Technical Preview`.

The admitted audience is controlled first testers who understand the published
limitations. The artifact is Windows x64, local-first, source-available, and
unsupported. It is not an authenticated, multi-user, cloud, compliance, or
production systematic-review product.

The release tag is `v0.1.0-alpha.2`. The tag must resolve to protected `main`,
match repository version metadata exactly, and be created only after the
release gate is green.

### Desktop artifact

The admitted desktop artifact is a self-contained `win-x64` portable ZIP. It:

- runs without a separately installed .NET SDK or runtime;
- remains non-packable and outside the NuGet package topology;
- carries product, version, repository, and Windows icon metadata;
- contains a machine-readable distribution manifest with the exact commit,
  version, runtime identifier, framework dependence, and every published file
  digest;
- is built twice and compared by normalized file inventory before release;
- is executed from an extracted clean directory with repository and .NET
  environment variables removed;
- is accompanied by SHA-256 checksums and an SPDX SBOM; and
- is covered by GitHub artifact attestation.

The portable executable is not Authenticode signed. The release page, manifest,
and documentation must state that limitation. GitHub attestation and checksums
authenticate the release workflow output but do not claim Windows publisher
identity or SmartScreen reputation.

This ADR does not authorize NuGet publication. The 24 package artifacts remain
validation-only and are not attached to the desktop prerelease.

### Release automation

Pull-request and branch workflows may build and verify the portable artifact
without publication credentials.

Only a matching protected-main tag may enter the `release` environment and
create or verify the immutable GitHub prerelease. The release workflow receives
`contents: write`, `id-token: write`, and `attestations: write` only in the
tag-only publication job. Pull-request workflows remain read-only.

Release notes are committed and version-specific. Publication fails closed for
a dirty tree, version/tag mismatch, missing checksums, missing SBOM, failed
artifact smoke, unexpected artifact, or digest mismatch.

### Local crash diagnostics

The desktop host records sanitized process-level crash reports under the
current user's local application-data directory. Reports contain release
identity, UTC time, failure source, and exception type. They do not contain
workspace contents, actor values, credentials, raw exception messages, stack
traces, absolute workspace paths, or scientific records.

Diagnostics remain local. There is no telemetry, crash upload, remote logging,
or scientific-authority claim. A subsequent launch may surface the existence
and location of the last local report without changing workspace state.

### Verified workspace backup

`NexusScholar.ResearchWorkspace` owns backup and restore because it owns the
workspace lock, containment rules, and verification.

A backup:

- requires an existing readable Nexus workspace;
- acquires the project mutation lock;
- excludes only the runtime lock file;
- rejects reparse points, links, unsafe relative paths, duplicate paths, and a
  destination inside the workspace;
- captures every admitted regular file with byte length and SHA-256;
- records workspace identity, project revision, creation time, and exact file
  inventory in a versioned manifest;
- verifies source bytes did not change during capture; and
- reopens and verifies the completed archive before reporting success.

The backup archive is operational recovery evidence, not scientific authority.
Its manifest records the exact captured byte set. The manifest digest binds
that inventory to workspace identity, revision, and archive capture time.

### Verified workspace restore

A restore:

- accepts only the admitted archive schema;
- rejects absolute, escaping, duplicate, linked, extra, missing, oversized, or
  digest-mismatched entries;
- restores only into a target directory that does not exist;
- writes into a sibling staging directory and promotes the directory only
  after all bytes and the restored workspace verify;
- never overwrites or merges an existing workspace; and
- removes staging material after failure.

Restore preserves every archived workspace file byte exactly and recreates the
standard required workspace directory layout. Empty directories outside that
standard layout are not authority, are not represented by the file manifest,
and need not be preserved. Restore does not rewrite project identity,
revisions, timestamps inside records, decisions, provenance, generations,
invalidations, or export ledgers.

### Desktop recovery surface

`NexusScholar.Desktop.AppServices` exposes framework-neutral backup and restore
previews/results. The Avalonia host presents explicit target paths and effects.
Backup and restore remain operational actions and cannot create, approve,
correct, or supersede scientific authority.

## Alternatives

- Publishing the existing framework-dependent desktop output was rejected
  because it requires an exact developer SDK and is not an end-user artifact.
- Publishing NuGet packages with the desktop preview was rejected because
  package stability and signing remain separately governed.
- Requiring Authenticode before any first-tester artifact was rejected because
  no signing identity or certificate-custody process exists. The preview is
  instead explicitly unsigned, checksummed, SBOM-inventoried, and attested.
- Desktop-only file copying was rejected because it would bypass workspace
  locking, containment, and authority verification.
- Restoring over an existing workspace was rejected because merge and rollback
  semantics are not defined and could destroy historical evidence.
- Remote crash reporting was rejected by the local-first privacy boundary.

## Consequences

First testers receive one reproducible, inspectable Windows artifact that runs
without a developer SDK. The repository gains local crash evidence and
byte-preserving backup/restore without widening scientific authority.

The artifact remains an unsigned technical preview. Users may receive Windows
reputation warnings. There is no installer, updater, automatic rollback,
authentication, PDF/OCR intake expansion, production support, or NuGet
publication.

## Migration Effect

No existing workspace schema or scientific record changes. Backup adds an
external archive schema. Restore recreates the exact archived file set plus the
standard required directory layout in a new directory.

No existing package identity changes. The desktop remains non-packable.

## Fixture Effect

No PHP fixture changes and no PHP compatibility expansion.

New local tests cover deterministic backup manifests, byte-preserving restore,
unsafe archives, links, source mutation, existing-target rejection, local crash
report redaction, headless desktop acceptance, published-artifact smoke, and
release-policy failures.

## Reversal Conditions

A successor ADR is required before:

- publishing NuGet packages;
- claiming a signed Windows publisher identity;
- adding an installer or automatic updater;
- restoring over or merging into an existing workspace;
- uploading diagnostics or telemetry;
- treating a backup, release manifest, UI value, or diagnostic report as
  scientific authority; or
- claiming production, compliance, authenticated-user, or multi-user readiness.
