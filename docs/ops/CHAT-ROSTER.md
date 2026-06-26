# Chat Roster

Branch-derived Codex lane roster from current git state.

## Active lanes
- Lane `two-model-codex-workflow`: active process branch `cdx/two-model-codex-workflow`, carrying process commit `4ec0eec` (`chore: configure two-model Codex workflow`) plus closeout fixes.
- Lane `gate-3-protocol-lifecycle`: current head `cdx/gate-3-protocol-lifecycle`, commit `b513d6a`.
- Lane `gate-3-planning-decisions`: planning decisions branch `cdx/gate-3-planning-decisions`, commit `d925796`.
- Lane `gate-2-digest-kernel-cleanup`: kernel cleanup lane `cdx/gate-2-digest-kernel-cleanup`, commit `5e5dde1`.
- Lane `gate-2`: archived evidence branch `cdx/run-gate-zero-discovery`, commit `e17ec4f`.
- Lane `gate-0`: legacy bootstrap branch `cdx/run-gate-0-discovery`, commit `ee46eb4`.
- Lane `main`: merged baseline `main`, commit `0339d99` (`Merge Gate 3 protocol lifecycle`).

## Branch containment relationships
- `two-model-codex-workflow` contains `0339d99` (`Merge Gate 3 protocol lifecycle`) and therefore carries Gates 0-3 into its head.
- `gate-3-planning-decisions` (`d925796`) is already contained in `gate-3-protocol-lifecycle`.
- `gate-2-digest-kernel-cleanup` (`5e5dde1`) is contained in both `gate-3-planning-decisions` and `gate-3-protocol-lifecycle`.
- `run-gate-zero-discovery` (`e17ec4f`) is contained in `gate-2-digest-kernel-cleanup`, `gate-3-planning-decisions`, and `gate-3-protocol-lifecycle`.
- `main` is fixed at `0339d99` for Gates 0-3 and remains the handoff target for the next branch.
- `gate-3-protocol-lifecycle` (`a8b9f68`) is the terminal historical head for this gate set.

## Merge readiness signals
- Gates 0-3 are archived at `0339d99` and already merged to main lineage as of this branch snapshot.
- Process branch `cdx/two-model-codex-workflow` is active and currently contains Gates 0-3 as context.
- Hosted CI verification recorded at `https://github.com/nexus-scholar/core-csharp/actions/runs/28265022053`:
  - `ubuntu-latest` success
  - `windows-latest` success

## Stale and cleanup notes
- Stale lane candidates: `cdx/run-gate-0-discovery` and `cdx/main-gate2-merge`.
- Do not classify `main` as stale; it is the current merge baseline.
