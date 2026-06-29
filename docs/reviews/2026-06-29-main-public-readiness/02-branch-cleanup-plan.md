# Branch Cleanup Plan

Baseline after `git fetch --all`: `origin/main` at `16cabc3`.

## Verdict

Do not merge every branch blindly.

The correct strategy is:

1. Treat `origin/main` as the baseline.
2. Keep `gh-pages`.
3. Delete remote branches that are ancestry-merged into `origin/main`.
4. Delete `origin/cdx/gate-9-fulltext-recon` only after accepting patch-equivalent cleanup.
5. Keep or rebase `cdx/gate-9-fulltext-local`; do not direct-merge it.
6. Delete stale local branches only after the valuable Full Text local branch is preserved.

## Remote Branches

Remote branches ancestry-merged into `origin/main`:

- `origin/cdx/gate-9-fulltext-contract`
- `origin/cdx/ui-phase-3-5-avalonia-sample-host`
- `origin/cdx/ui-phase-3-avalonia-renderer`

Remote branch not ancestry-merged but patch-equivalent:

- `origin/cdx/gate-9-fulltext-recon`

`git cherry -v origin/main origin/cdx/gate-9-fulltext-recon` returned:

```text
- 85d2e17 docs: map PHP full text behavior
```

That means its patch content is already represented on `origin/main`, but the branch is not an ancestry-merge cleanup candidate.

Remote branch to keep:

- `origin/gh-pages`

## Local Branches

Most local `cdx/*` branches are ancestry-contained by `origin/main` and can be deleted locally after you are comfortable with branch cleanup.

Branches that need special handling:

- `cdx/gate-9-fulltext-local`
- `cdx/gate-9-screening-local-with-ui-base`
- `gh-pages`

### `cdx/gate-9-fulltext-local`

Status: valuable but not merge-ready against current `origin/main`.

It has one unique commit:

```text
a520616 Implement local full text evidence slice
```

It adds:

- `src/NexusScholar.FullText/`
- `tests/NexusScholar.Core.Tests/FullTextTests.cs`
- `tests/NexusScholar.Conformance.Tests/FullTextFixtureTests.cs`
- `fixtures/conformance/fulltext/*.json`
- Full Text evidence docs.

But the branch is based before UI renderer/sample-host merges. The diff against current `origin/main` includes deletions of:

- `src/NexusScholar.Avalonia.Blocks/**`
- `samples/NexusScholar.Avalonia.Blocks.SampleHost/**`
- Avalonia test projects;
- current UI renderer docs.

Safe action: create a new branch from `origin/main` and cherry-pick or port only the Full Text implementation files. Resolve architecture tests so both Full Text and UI renderer boundaries coexist.

Unsafe action: direct merge `cdx/gate-9-fulltext-local` into `main`.

### `cdx/gate-9-screening-local-with-ui-base`

Status: do not merge.

This branch is stale and destructive relative to current `origin/main`. Its diff deletes ADR 0014, Full Text docs, much of `docs/ui`, Avalonia renderer/host files, and many current tests.

Safe action: delete after confirming no required work remains.

### `gh-pages`

Keep. It is intentionally separate public-site history.

## Recommended Cleanup Sequence

1. Fast-forward local `main` to `origin/main`.
2. Create a safety tag before cleanup:

```powershell
git tag pre-cleanup-2026-06-29 origin/main
```

3. Delete remote ancestry-merged branches:

```powershell
git push origin --delete cdx/gate-9-fulltext-contract
git push origin --delete cdx/ui-phase-3-avalonia-renderer
git push origin --delete cdx/ui-phase-3-5-avalonia-sample-host
```

4. If you accept patch-equivalent cleanup:

```powershell
git push origin --delete cdx/gate-9-fulltext-recon
```

5. Preserve Full Text local work before deleting old local branches:

```powershell
git switch --detach origin/main
git switch -c cdx/gate-9-fulltext-local-rebased
git cherry-pick a520616
```

Expect conflicts. Preserve current UI files and port only Full Text behavior.

6. After the rebased Full Text branch is green, delete stale local branches.

## Branch Cleanup Decision Table

| Branch | Classification | Action |
| --- | --- | --- |
| `origin/cdx/gate-9-fulltext-contract` | ancestry-merged | delete remote |
| `origin/cdx/ui-phase-3-avalonia-renderer` | ancestry-merged | delete remote |
| `origin/cdx/ui-phase-3-5-avalonia-sample-host` | ancestry-merged | delete remote |
| `origin/cdx/gate-9-fulltext-recon` | patch-equivalent | delete only after explicit acceptance |
| `origin/gh-pages` | public site | keep |
| `cdx/gate-9-fulltext-local` | valuable unmerged local work | rebase/cherry-pick, do not direct-merge |
| `cdx/gate-9-screening-local-with-ui-base` | stale destructive local branch | delete after confirmation |
| old local `cdx/*` branches with `AncestorMerged=True` | local clutter | delete after cleanup tag |
