# FE-08 Slice 3 Native Visual QA

Date: 2026-07-17

Host: `NexusScholar.Desktop` Release build on Windows at 1360 x 842, 100% scale.

Fixture: disposable verified ResearchWorkspace with two duplicate candidates,
one review target, and a local policy assigning actor `alice` the `owner` role.

## Verified

- the review form is readable without overlapping the navigation, workspace,
  status, or effect-inspector regions;
- target, action, reason, actor, and role begin without scientific defaults;
- the preview command remains disabled until all five explicit values exist;
- the exact target, policy action, reason, actor, role, and rationale remain
  visible before preview;
- preview is non-mutating and renders the exact merge, decision append, snapshot
  successor, generation advance, and invalidation effects;
- the confirmation token wraps inside the inspector without escaping its region;
- the scientific-decision boundary names human actor and policy authority and
  disclaims authentication, provider, AI, and cloud authority;
- cancel removes the pending confirmation, restores the empty inspector, and
  leaves the application responsive.

## Defect Found And Closed

The first preview attempt terminated the host with an Avalonia
`InvalidOperationException`: reusable field controls still had a visual parent
when `Render()` rebuilt the workspace grids. `MainWindow` now detaches every
reusable control before rebuilding, and a focused regression test proves a
control can move from the first render grid to the second. The complete native
preview and cancel flow passed after rebuilding the Release host.

## Boundary

No scientific decision was confirmed during visual QA. Commit, refresh,
supersession, stale-race, and replay behavior are covered through the real
workspace facade and product view-model tests.
