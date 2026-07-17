# FE-08 Slice 3: Desktop Deduplication Review

Status: accepted for implementation under ADR 0036.

## Researcher Outcome

A researcher can inspect the current deduplication review queue, identify
themselves with an actor id and active role, preview one policy-authorized
decision, explicitly confirm it, and see the refreshed local authority state.

## Sources

- ADR 0028, ADR 0029, ADR 0035, and ADR 0036;
- verified FE-01 authority generations and FE-02 successor transitions;
- the structured ResearchWorkspace deduplication-review operation, using the
  pure `DeduplicationReviewApplicationService` preview helper and
  `ResearchWorkspaceTransaction.CommitDeduplicationDecision`.

## Dependency-Ordered Work

1. Add a structured ResearchWorkspace deduplication review service.
2. Refactor the CLI decision adapter to use the shared service without changing
   its observable output.
3. Add actor-bound preview/confirm contracts to Desktop.AppServices.
4. Add the review queue, actor/role controls, decision form, and effect inspector
   to the product host.
5. Add stale, authorization, cancellation, lock/recovery, architecture,
   component, and native visual tests.
6. Complete independent review, local/hosted validation, merge, and evidence.

## Required Behavior

- reconstruct current authority from workspace bytes before every preview and
  confirmation;
- list exact current review targets with stable target ids;
- accept only actions and reasons admitted by the verified policy;
- bind actor, role, action, reason, rationale, target id/digest, source
  id/digest, policy id/version/digest, authority generation/manifest digest,
  active decision-set digest, snapshot id/digest, request id/digest, superseded
  decision id/digest, project revision, affected candidate membership,
  representative, invalidation record digests, and unresolved-work result into
  the confirmation token;
- expose active decision ids/digests per target and require exact supersession
  before another decision can be proposed for that target;
- reject actor changes and every changed authority input as stale;
- preserve append-only decision and snapshot history;
- refresh the queue only after a verified commit;
- classify a valid concurrent authority successor as stale, while malformed or
  unverifiable authority bytes require recovery;
- distinguish success, stale, validation failure, and recovery-required states.

## Required Negative Cases

- no initialized authority generation;
- target disappeared or target digest changed;
- actor/role is missing, automated, or not assigned by policy;
- action or reason is not allowed by policy;
- confirmation token or preview material changed;
- project revision, authority generation, source result, or snapshot changed;
- superseded decision is absent or no longer active;
- a previously decided target is submitted again without exact supersession;
- policy digest changes under the same policy id;
- request identity replays with conflicting material;
- workspace lock is held or an authority transition is incomplete;
- cancellation mutates authority;
- UI row id, selection index, label, or path enters decision material;
- desktop directly references concrete Deduplication authority types.

## Allowed Paths

- ADR 0036, this gate, FE-08 roadmap/UI/release evidence;
- focused ResearchWorkspace shared deduplication-review operation;
- CLI adapter refactor with compatibility tests;
- Desktop.AppServices contracts/facade and desktop host/view model;
- affected architecture, ResearchWorkspace, CLI, facade, and component tests.

## Excluded Scope

- Screening conduct and every other scientific decision family;
- authentication, identity providers, durable active-user settings, or role
  administration;
- providers, network, PDF/OCR, AI, plugins, database, API, cloud, telemetry,
  synchronization, or multi-user behavior;
- PHP, blueprint, installer, deployment, accessibility-certification, or
  production-security claims.

## Verification

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

Also run focused authority-transition, CLI compatibility, architecture,
Desktop.AppServices, product-host component, conformance fixture replay,
`scripts/verify.ps1`, and native Windows visual tests. Architecture coverage must
prove Desktop.AppServices has no direct Deduplication or AppServices reference
and that CLI and desktop enter through the same ResearchWorkspace operation.

## Exit Criteria

- one deduplication review decision completes end to end from product queue to
  verified successor authority generation;
- preview is non-mutating and confirmation is exact and stale-safe;
- unauthorized actor/role and changed authority fail closed;
- CLI decision behavior remains compatible;
- independent review reports no blocking or high-severity finding;
- protected hosted validation passes and the closeout merges.
