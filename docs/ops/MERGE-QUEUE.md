# Merge Queue

Source: live status from `git branch -vv` and `git branch --merged main`.

## Completed
- `cdx/run-gate-zero-discovery` (merged to `main`)
- `cdx/gate-2-digest-kernel-cleanup` (merged to `main`)
- `cdx/gate-3-planning-decisions` (merged to `main`)
- `cdx/gate-3-protocol-lifecycle` (merged to `main`)
- `cdx/run-gate-0-discovery` (merged to `main`)

## Active
- `cdx/two-model-codex-workflow` (current branch, atop `main`, contains process commit `4ec0eec`, and contains `0339d99`)

## Queue
- `cdx/two-model-codex-workflow` commit: `4ec0eec` (`chore: configure two-model Codex workflow`).
- CI evidence: `https://github.com/nexus-scholar/core-csharp/actions/runs/28265022053`
  (`ubuntu-latest` success, `windows-latest` success).
- `main` currently at `0339d99` (`Merge Gate 3 protocol lifecycle`).
- Gate 0 through Gate 3 are merged to `main` at `0339d99`.
- Recommended next branch activity: start Gate 4 planning decisions only after CF-003, CF-006, and CF-007 are resolved.

## Cleanup Candidates (confirmed by merge check)
- `cdx/run-gate-zero-discovery`
- `cdx/gate-2-digest-kernel-cleanup`
- `cdx/gate-3-planning-decisions`
- `cdx/gate-3-protocol-lifecycle`

## Branch Delete Guard
- Do not delete any cleanup candidate until it is confirmed by `git branch --merged main` and any remaining references are cleared.
- `git branch --merged main` now confirms all four candidates in this set.
