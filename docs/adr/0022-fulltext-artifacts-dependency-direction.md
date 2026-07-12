# ADR 0022: Full Text Artifacts Dependency Direction

Status: Accepted

Date: 2026-07-12

## Context

ADR 0009 defines the portable logical artifact path rules. Full Text previously duplicated no validator and therefore accepted paths that the artifact contract rejects.

## Decision

`NexusScholar.FullText` may depend inward on `NexusScholar.Artifacts` and `NexusScholar.Kernel`. It reuses `ArtifactDescriptor.NormalizeLogicalPath` for optional artifact projections. Artifacts does not depend on Full Text.

This dependency does not make paths scientific identity, authorize filesystem writes, or add storage behavior.

## Consequences

- Full Text and review-bundle artifacts share one accepted logical-path contract.
- Architecture tests permit only Kernel and Artifacts as Full Text domain dependencies.
- Filesystem resolution and workspace containment remain later transactional-workspace responsibilities.
