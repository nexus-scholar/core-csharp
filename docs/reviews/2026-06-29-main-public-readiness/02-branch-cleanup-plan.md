# Branch Cleanup Record

Baseline before cleanup: `origin/main` at `16cabc3`.

Current baseline after cleanup: `origin/main` at `ebb7bba`.

## Verdict

Branch cleanup is complete for the current consolidation pass.

The original plan was correct:

1. Treat `origin/main` as the baseline.
2. Keep `gh-pages`.
3. Do not direct-merge stale branches.
4. Port the useful Full Text implementation onto current `main`.
5. Delete stale local and remote `cdx/*` branches after preservation.

## Completed Actions

- Fast-forwarded local `main` to `origin/main`.
- Cherry-picked the Full Text implementation commit `a520616` onto current `main` as `5a13abc`.
- Preserved the UI renderer and sample host while adding Full Text.
- Refreshed README and review docs in commit `ebb7bba`.
- Pushed `main` to `origin`.
- Deleted remote branches:
  - `cdx/gate-9-fulltext-contract`
  - `cdx/gate-9-fulltext-recon`
  - `cdx/ui-phase-3-5-avalonia-sample-host`
  - `cdx/ui-phase-3-avalonia-renderer`
- Pruned remote refs.
- Removed clean obsolete local `cdx/*` worktrees.
- Deleted local `cdx/*` branches.
- Kept `gh-pages`.

## Current Remote Branches

Verified with `git ls-remote --heads origin`:

```text
53d7aa429471faf65ea6b94c3febd1015c1e94a1 refs/heads/gh-pages
ebb7bba6131c27a5608a63d302de555b26db849e refs/heads/main
```

## Current Local Branches

Verified with `git branch -a --verbose --no-abbrev`:

```text
main     ebb7bba6131c27a5608a63d302de555b26db849e
gh-pages 53d7aa429471faf65ea6b94c3febd1015c1e94a1
```

Remote tracking refs:

```text
origin/main     ebb7bba6131c27a5608a63d302de555b26db849e
origin/gh-pages 53d7aa429471faf65ea6b94c3febd1015c1e94a1
```

## What Was Not Merged Blindly

The stale local branch `cdx/gate-9-screening-local-with-ui-base` was not merged. Its two-dot diff against current `origin/main` would have deleted accepted Full Text docs, UI renderer/host files, and current tests.

The old Full Text local branch was not direct-merged. Only its useful implementation commit was cherry-picked onto current `main`, preserving UI work.

## Hosted Verification

After pushing `main`, GitHub Actions run `28380516236` passed on:

- `ubuntu-latest`
- `windows-latest`

Run URL: https://github.com/nexus-scholar/core-csharp/actions/runs/28380516236

## Remaining Branch Policy

- Keep `main` as the implementation baseline.
- Keep `gh-pages` as the public site branch.
- Create future work on fresh short-lived branches from current `main`.
- Do not recreate long-lived `cdx/*` branch piles without an explicit branch board update.
- Refresh this record or `docs/ops/*` after the next pushed branch or PR.
