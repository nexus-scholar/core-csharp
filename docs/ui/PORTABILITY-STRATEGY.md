# Portability Strategy

Block contracts should be shared. Renderers should be platform-specific. This keeps the scientific interaction model stable while allowing each surface to present the same workflow appropriately.

## Contract First

A future `NexusScholar.UiContracts` package should define block descriptors and workspace plans without referencing Avalonia, web, mobile, terminal, persistence, or provider SDK libraries.

The same typed block plan can then be rendered by:

- desktop renderer;
- CLI renderer;
- web renderer;
- future mobile renderer.

The renderer receives a plan. It does not invent authority, change allowed actions, or silently mutate Core records.

## Platform Examples

### Desktop

Desktop can show rich comparison and inspection layouts.

Example: `DedupReviewWorkspace`

- candidate cluster list on the left;
- side-by-side `RecordComparisonBlock` in the center;
- `IdentifierOverlapBlock` and `TitleSimilarityBlock` below;
- raw evidence/provenance inspector on the side;
- action buttons in `MergeDecisionGate`.

### Mobile

Mobile should be designed for but not built first. The same block should render with lower density and more sequential interaction.

Example: `DedupReviewWorkspace`

- stacked record cards;
- swipe or tab between candidate members;
- collapsed evidence sections;
- explicit action confirmation screens;
- audit details available through drill-down.

### Web

Web can share many desktop concepts but should assume browser navigation, collaboration, and deployment boundaries will introduce separate app concerns. Web row identities and app state must not become Core authority unless a future ADR admits them.

### CLI

CLI can render the same block plans textually.

Example: `DedupReviewWorkspace`

```text
Candidate cluster C-001 requires review.
1. Compare records
2. Show identifier overlap
3. Show title similarity
4. Open raw evidence summary
5. Accept merge
6. Reject merge
7. Mark unresolved
```

CLI output should include block ids, evidence refs, warning codes, and command-safe action ids so terminal workflows remain auditable.

## Desktop First, Not Mobile First

Desktop should come first because the early workflows need dense comparison, evidence inspection, local files, bundle export, and audit peel-back. Mobile support should influence contract portability and responsive assumptions, but it should not drive the first implementation.

## CLI As A Serious Renderer

CLI should not be treated as an afterthought. It is valuable for reproducible demos, scripted tests, and audit-friendly workflows. A CLI renderer can validate whether block contracts are truly platform-neutral because it cannot rely on complex visual affordances.

## Portability Rules

- Shared contracts carry meaning, actions, evidence references, and authority requirements.
- Renderers carry layout and interaction details.
- Application services compose plans from Core state and validation.
- Core remains UI-free.
- Local paths can be display references, not scientific identity.
- Platform app state remains projection until a future ADR promotes it.
