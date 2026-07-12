# Hardening 17 - Transactional Workspace Integrity

Status: complete.

## Implemented

- strict validation of all persisted project fields, identifiers, timestamps, digests, aliases, and paths;
- atomic project-file replacement;
- exclusive mutation locking and project revision compare-and-swap;
- staged, atomically promoted search imports with source and parser trace committed together;
- immutable analysis generations with complete digest manifests;
- stale, foreign, missing, or corrupt generation rejection before generated data is consumed;
- failed-generation quarantine and stale-analysis invalidation after import;
- reparse-point and junction rejection for workspace-relative paths;
- centralized CLI, desktop, and workflow analysis persistence through `ResearchWorkspaceTransaction`.

## Regression Coverage

- malformed project inputs and duplicate identifiers;
- stale revision rejection while preserving the current generation;
- generated-output corruption rejection;
- reparse-point ancestor rejection;
- CLI and desktop consumers following committed generation paths;
- missing and changed immutable import artifacts.

## Invariants

- `nexus.project.json` is the final commit pointer.
- A consumer never combines artifacts from different committed generations.
- A project revision cannot be overwritten by a writer that read an older revision.
- Imported bytes and their parser trace become visible together.
- Generated artifacts are accepted only when their manifest binds the current workspace, revision, ordered inputs, and digests.

## ADR And Compatibility Impact

ADR 0023 accepts the transactional generation contract. No PHP compatibility claim or fixture change is made.
