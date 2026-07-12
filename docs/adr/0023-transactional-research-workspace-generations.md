# ADR 0023: Transactional Research Workspace Generations

- Status: Accepted
- Date: 2026-07-12

## Context

Research workspace imports and analysis previously wrote several mutable files and then rewrote `nexus.project.json`. A crash or concurrent command could leave orphaned inputs, mixed analysis outputs, or a project assembled from different revisions. Consumers trusted conventional `current.*` paths without proving that the files belonged to the current workspace inputs.

## Decision

Workspace mutations use an exclusive workspace lock and compare the project revision before commit. Imports and analysis are written into staging directories and promoted by an atomic directory rename. The project file is replaced atomically and is the final commit pointer.

Analysis outputs are immutable generations. Each generation manifest binds:

- the workspace identifier and committed project revision;
- the ordered input identifiers, paths, and content digests;
- every generated import trace path and digest;
- every output name, path, and digest.

Status, review, cluster, desktop, and read-model consumers resolve generated files through the committed project outputs and verify the generation manifest before use. A new import clears the previous analysis pointer. Failed promoted generations are moved to quarantine.

Project validation rejects unsupported schemas, malformed timestamps and digests, duplicate input identifiers, ambiguous legacy aliases, unsafe identifiers, and non-canonical relative paths. Existing reparse-point ancestors are rejected during path resolution.

## Consequences

- Generated output paths are generation-specific rather than mutable fixed filenames.
- Imported source bytes and traces live together in immutable per-input directories.
- Concurrent writers fail with an explicit revision or lock conflict instead of losing updates.
- Corrupt, stale, foreign, incomplete, and junction-routed generations cannot be consumed as current scientific state.
- Legacy fixture workspaces without a generation pointer remain readable, but every new analysis commit uses the manifest contract.

## Compatibility

This is a C# workspace integrity contract. It makes no PHP compatibility claim and changes no golden PHP fixture.
