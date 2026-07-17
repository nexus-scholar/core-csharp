# FE-08 Slice 4: Screening Authority Resolution

Status: accepted for implementation under ADR 0037.

## Researcher Outcome

A researcher can open a local workspace and receive a fail-closed answer about
whether the exact verified authority package required for title/abstract
Screening is ready after restart.

## Required Behavior

- persist an immutable package manifest and exact authority artifacts;
- reopen the current FE-01 Deduplication authority chain;
- strictly rehydrate approved Protocol authority and all referenced approvals;
- strictly rehydrate title/abstract criteria and reproduce its digest;
- verify workspace revision, authority generation, result, decision-set,
  snapshot, Protocol, criteria, and optional Workflow bindings;
- distinguish ready, unavailable, stale, invalid, and recovery-required states;
- expose only read-only readiness through Desktop.AppServices;
- keep desktop and Avalonia projects free of direct scientific authority types.

## Required Negative Cases

- missing package or artifact;
- changed manifest or artifact bytes;
- stale project revision or FE-01 authority generation;
- wrong result, decision-set, or snapshot binding;
- draft, withdrawn, or digest-mismatched Protocol;
- missing, duplicate, non-human, wrong-target, or insufficient approvals;
- criteria with wrong stage, Protocol id, Protocol digest, or criteria digest;
- Workflow governance claimed without verified Workflow authority;
- UI or project-index strings used as a resolver.

## Excluded Scope

- Screening conduct initialization or decisions;
- Workflow completion;
- Protocol, criteria, or role authoring;
- authentication, providers, network, AI, plugins, database, API, cloud,
  synchronization, telemetry, or multi-user behavior;
- PHP, blueprint, installer, deployment, accessibility-certification, or
  production-security claims.

## Verification

Run focused canonical-codec, ResearchWorkspace, Desktop.AppServices, and
architecture tests, followed by the full Release build, test, format, package
verification, release-policy verification, and hosted protected-branch checks.

## Exit Criteria

- a package created from verified authorities reopens to equivalent verified
  Protocol, Deduplication, criteria, and optional Workflow bindings;
- every stale or altered authority input fails closed;
- desktop receives readiness only and cannot mutate Screening;
- independent architecture, scientific-invariant, and test reviews report no
  blocking or high-severity finding;
- local and hosted validation pass.
