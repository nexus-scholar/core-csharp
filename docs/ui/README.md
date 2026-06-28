# Nexus Scholar UI Planning

This folder prepares future UI and UX work for Nexus Scholar. It is planning material only. It does not define Core authority, scientific record shape, compatibility behavior, or accepted product law. Authoritative scientific behavior remains in `specs/`, accepted ADRs, fixtures, observable pinned PHP behavior, and the current C# implementation.

The intent is to make future desktop, CLI, web, and mobile work easier without pulling UI dependencies into Core. These notes describe how strict Core records can be translated into understandable research workflows while preserving audit-grade evidence, provenance, validation, and human authorization.

## Documents

- `UI-PHILOSOPHY.md`: Product philosophy for strict internals, simple workflows, AI assistance, and human-authorized science.
- `PRODUCT-POSITIONING.md`: Market and wedge positioning for an audit-grade research workflow system rather than another paper summarizer.
- `BLOCK-FRAMEWORK-BLUEPRINT.md`: Early architecture for Nexus Scholar Blocks as typed research interaction units.
- `BLOCK-CATALOG-v0.md`: Candidate block families and first prototype candidates around Import and Deduplication.
- `PORTABILITY-STRATEGY.md`: How shared block plans can render across desktop, CLI, web, and mobile without making Core UI-aware.
- `AI-ASSISTED-UI-RULES.md`: Safe and unsafe AI roles in the user experience.
- `RESEARCH-COCKPIT-CONCEPT.md`: Desktop shell concept with workflow navigation, adaptive workspace, assistant, and evidence/provenance inspector.
- `BEGINNER-VS-AUDIT-MODE.md`: How the same block can be rendered differently for beginner and audit users.
- `UI-CONTRACTS-v0.md`: Phase 1 contract-layer summary for `NexusScholar.UiContracts`.
- `DEDUP-REVIEW-WORKSPACE-v0.md`: First serious workflow prototype concept for review-required duplicate candidates.
- `SCREENING-WORKSPACE-v0.md`: Early screening workspace concept aligned with human decision authority.
- `ROADMAP.md`: Staged path from documentation to UI contracts, sample block plans, renderers, and later AI proposal support.
- `OPEN-QUESTIONS.md`: Product and technical questions that should be resolved before contract implementation.

## Boundary

The planning documents may propose future projects such as `NexusScholar.UiContracts`, `NexusScholar.AppServices`, or renderer packages. Creating these documents does not create those packages, does not add Avalonia, and does not change Core behavior.

Any future implementation that affects scientific authority, record schemas, digest material, provenance, AI acceptance, app persistence, or PHP compatibility requires the normal ADR, fixture, and verification path.
