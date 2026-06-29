# Continuation Roadmap

## Hard Recommendation

Do not jump straight into more broad implementation.

The project needs one consolidation/public-feedback pass before the next big technical slice. Otherwise, you will keep adding correct internals while public readers cannot understand how to approach the project.

## Next Four Moves

### Move 1: Consolidate `main`

Goal: make the repository itself tell the current truth.

Tasks:

- fast-forward local `main` to `origin/main`;
- refresh `docs/ops/BRANCH-BOARD.md`, `MERGE-QUEUE.md`, and `CHAT-ROSTER.md`;
- rewrite `README.md`;
- rewrite `CODEX-START-HERE.md`;
- update `PLANS.md` to separate historical gate map from current roadmap;
- add issue templates and PR template;
- keep `gh-pages` separate.

Exit criteria:

- `main` says what is implemented now;
- branch board matches live branch state;
- public feedback has a structured channel.

### Move 2: Public Tester Walkthrough

Goal: make one person able to test something real in 10-15 minutes.

Tasks:

- replace placeholder getting-started tutorial;
- add sample host screenshot/GIF to `gh-pages`;
- add "first tester checklist";
- add "what feedback I want" page/post;
- verify all commands locally.

Exit criteria:

- a tester can clone, verify, run CLI doctor/sample, run sample host, and file useful feedback.

### Move 3: Rebase Full Text Local Slice

Goal: preserve valuable implementation work without deleting current UI work.

Tasks:

- create `cdx/gate-9-fulltext-local-rebased` from `origin/main`;
- port/cherry-pick Full Text files from `a520616`;
- preserve Avalonia renderer/sample host files;
- update architecture tests so Full Text and UI boundaries both hold;
- run full build/test/format;
- update Full Text evidence docs.

Exit criteria:

- Full Text no-network local implementation exists on top of current `main`;
- no UI regression;
- no persistence/API/UI/cloud/live-provider claims.

### Move 4: First Useful Product Slice

Goal: bridge kernel truth to a tester-visible workflow without making Core depend on UI or persistence.

Best next slice after Full Text or in parallel as docs/design:

> AppServices block composition for Import + Dedup sample state.

Why this slice:

- public testers can understand import warnings and duplicate review;
- it uses already-implemented Search import and Deduplication;
- it turns Core evidence into `WorkspacePlan` without letting UI mutate Core;
- it prepares a real app without needing persistence/cloud.

Scope:

- `NexusScholar.AppServices` or equivalent planning ADR first;
- read-only composition from existing Core records into `WorkspacePlan`;
- no persistence;
- no command execution;
- no real researcher project storage;
- tests asserting Core remains UI-free.

## What Not To Do Next

- Do not merge stale local branches blindly.
- Do not start cloud/persistence before app-service boundaries are explicit.
- Do not add live providers or HTTP clients.
- Do not turn the sample host into a real desktop app without an application-service command boundary.
- Do not pitch the project as ready for full systematic-review use.
- Do not delete `cdx/gate-9-fulltext-local` until its useful implementation work is safely ported or intentionally abandoned.

## Brainstorm: Public Product Wedge

The strongest first wedge is not "AI summaries."

The wedge is:

> Evidence-preserving import and duplicate review for systematic review work, with audit-visible warnings, human gates, and verifiable exports.

Why:

- Search import, Deduplication, Screening, Bundles, and UiContracts already support the concept.
- It is easier to demonstrate than full review execution.
- It exposes the project philosophy in a concrete way.
- It creates useful feedback from real researchers: "Does this match how messy search exports and duplicate candidates feel?"

First public demo path:

1. Import warning block.
2. Dedup candidate cluster block.
3. Human merge decision gate.
4. Bundle verification block.
5. Show the non-authority labels and evidence refs.

The product story becomes:

> Nexus does not just summarize papers. It keeps the review workflow honest.
