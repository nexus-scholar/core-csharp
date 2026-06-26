# Codex Branch Board

Source: branch probes run at branch closeout time with the two-model workflow branch checked out.

## Active Branch Classes

- `cdx/two-model-codex-workflow` status: active
- `cdx/main-gate2-merge` status: merged
- `main` status: merged
- `cdx/gate-3-protocol-lifecycle` status: merged
- `cdx/gate-3-planning-decisions` status: merged
- `cdx/gate-2-digest-kernel-cleanup` status: merged
- `cdx/run-gate-zero-discovery` status: merged
- `cdx/run-gate-0-discovery` status: merged

- `cdx/two-model-codex-workflow` active lane contains process commit `4ec0eec` (`chore: configure two-model Codex workflow`) and carries Gates 0-3 via `0339d99`.

## Requested Classification Buckets

- merged: `cdx/main-gate2-merge`, `main`, `cdx/gate-3-protocol-lifecycle`, `cdx/gate-3-planning-decisions`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/run-gate-zero-discovery`, `cdx/run-gate-0-discovery`
- cleanup: `cdx/run-gate-zero-discovery`, `cdx/gate-2-digest-kernel-cleanup`, `cdx/gate-3-planning-decisions`, `cdx/gate-3-protocol-lifecycle`
- active: `cdx/two-model-codex-workflow`
- blocked: none
- stale: `cdx/run-gate-0-discovery`, `cdx/main-gate2-merge`
- review: none identified by current git state
- next_action: next product branch should open Gate 4 planning decisions next; implementation remains blocked until blockers are resolved.

Cleanup candidates above are confirmed by `git branch --merged main` output.

## Containment Summary
- Commit `d925796` (`gate-3-planning-decisions`) is in `cdx/gate-3-planning-decisions` and `cdx/gate-3-protocol-lifecycle`.
- Commit `5e5dde1` (`gate-2-digest-kernel-cleanup`) is in `cdx/gate-2-digest-kernel-cleanup`, `cdx/gate-3-planning-decisions`, and `cdx/gate-3-protocol-lifecycle`.
- Commit `b513d6a` (`gate-3-protocol-lifecycle` closeout) is the current `cdx/gate-3-protocol-lifecycle` head.
- Commit `e17ec4f` (`run-gate-zero-discovery`) is in `cdx/gate-2-digest-kernel-cleanup`, `cdx/gate-3-planning-decisions`, and `cdx/gate-3-protocol-lifecycle`.
- Commit `0339d99` (`Merge Gate 3 protocol lifecycle`) is in `cdx/main-gate2-merge`, `main`, and `cdx/two-model-codex-workflow`.

## Current Gate State
- Gate 0: merged to main baseline.
- Gate 1: merged to main baseline.
- Gate 2: merged to main baseline.
- Gate 3: merged to main baseline and currently carried into two-model workflow head.
- Gate 4: planning decisions required before implementation; active blockers: CF-003 workflow compiler contract, CF-006 schema closure, CF-007 hybrid workflow mode semantics.
