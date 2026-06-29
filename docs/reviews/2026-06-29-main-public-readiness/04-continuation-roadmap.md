# Continuation Roadmap

## Hard Recommendation

Do not jump straight into broad implementation.

The repository state is now clean enough to show, but the next valuable work is a public-feedback runway and one tester-visible workflow path. Otherwise, you will keep adding correct internals while public readers cannot understand how to approach the project.

## Completed Since Earlier Review

- `main` was consolidated and pushed.
- Full Text local no-network implementation was ported onto current `main`.
- Remote `cdx/*` branches were deleted.
- Local obsolete `cdx/*` branches and clean worktrees were removed.
- README and UI README were refreshed.
- Hosted CI passed for pushed `main`.

## Next Four Moves

### Move 1: Finish Public Onboarding

Goal: make one person able to test something real in 10-15 minutes.

Tasks:

- replace placeholder getting-started tutorial on `gh-pages`;
- add sample host screenshot/GIF to `gh-pages`;
- add a first-tester checklist;
- add "what feedback I want" page/post;
- verify all commands locally and record exact output expectations.

Exit criteria:

- a tester can clone, verify, run CLI doctor/sample, run sample host, and file useful feedback.

### Move 2: Add Feedback Channels

Goal: make feedback structured enough to act on.

Tasks:

- add issue templates;
- add PR template;
- add a pinned feedback issue;
- add labels for architecture, docs, first-tester, workflow-use-case, and validation-failure.

Exit criteria:

- public feedback is routed into actionable buckets instead of free-form noise.

### Move 3: Refresh Maintainer Routing Docs

Goal: prevent future work from restarting at old gates.

Tasks:

- rewrite `CODEX-START-HERE.md`;
- update `PLANS.md` so historical gates are separated from current roadmap;
- keep `docs/ops/*` refreshed after each branch/PR;
- add a "current baseline" note pointing to `README.md`, `docs/adr/`, `docs/gates/`, and this review package.

Exit criteria:

- future Codex work starts from the current `main` state, not Gate 0.

### Move 4: First Useful Product Slice

Goal: bridge kernel truth to a tester-visible workflow without making Core depend on UI or persistence.

Best next slice:

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

- Do not start cloud/persistence before app-service boundaries are explicit.
- Do not add live providers or HTTP clients.
- Do not turn the sample host into a real desktop app without an application-service command boundary.
- Do not pitch the project as ready for full systematic-review use.
- Do not treat Full Text as live retrieval, PDF extraction, OCR, or PHP-compatible behavior.
- Do not recreate branch sprawl without a live branch board.

## Brainstorm: Public Product Wedge

The strongest first wedge is not "AI summaries."

The wedge is:

> Evidence-preserving import and duplicate review for systematic review work, with audit-visible warnings, human gates, and verifiable exports.

Why:

- Search import, Deduplication, Screening, Full Text, Bundles, and UiContracts support the concept.
- It is easier to demonstrate than full review execution.
- It exposes the project philosophy in a concrete way.
- It creates useful feedback from real researchers: "Does this match how messy search exports and duplicate candidates feel?"

First public demo path:

1. Import warning block.
2. Dedup candidate cluster block.
3. Human merge decision gate.
4. Bundle verification block.
5. Full Text evidence boundary note.
6. Non-authority labels and evidence refs.

The product story becomes:

> Nexus does not just summarize papers. It keeps the review workflow honest.
