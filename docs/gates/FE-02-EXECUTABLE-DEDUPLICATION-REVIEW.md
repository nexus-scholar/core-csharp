# FE-02: Executable Deduplication Review

Status: complete and accepted for the local FE-02 scope. See
`FE-02-EXECUTABLE-DEDUPLICATION-REVIEW-EVIDENCE.md`.

## Goal

Execute one verified human merge, keep-separate, or mark-unresolved request from
preview through atomic successor publication and verified reopen, while
preserving source evidence and predecessor authority.

## Dependency-Ordered Work

1. FE-02.1, Deduplication owner: canonical request, deterministic idempotency,
   preview material, decision construction, correction validation, and
   representative-election reuse.
2. FE-02.2, CorpusSnapshots owner: action reducer, active constraints, changed
   membership validation, successor creation/rehydration, and invalidation.
3. FE-02.3, ResearchWorkspace owner: v2 authority manifest, predecessor-chain
   rehydration, atomic commit, idempotent retry, stale CAS, recovery, and verify.
4. FE-02.4, AppServices owner: UI-neutral request, preview, and result contracts;
   unlock only the three admitted APP-01 actions.
5. FE-02.5, CLI owner: `dedup decide` preview and explicit `--confirm` execution.
6. FE-02.6, conformance owner: canonical fixtures, architecture rules, full
   regression, evidence, and plan closeout.

## Required Cases

- exact verified review pair merged with deterministic representative;
- fuzzy pair kept separate;
- no-id pair marked unresolved;
- repeated exact request returns already applied;
- replay after a clock change returns stored records rather than recreating them;
- non-canonical request, same derived decision id with different request material,
  or mismatched superseded decision digest is rejected;
- same target correction requires explicit supersession;
- stale result, target, snapshot, generation, policy, evidence, or request;
- automation, AI, import, plugin, or unknown actor attempts final action;
- transitive merge attempts to violate active keep-separate;
- grouped/grouped, grouped/unresolved, unresolved/unresolved, already-merged,
  active-separated, and superseded-separated transition-table cases;
- v2 manifest, predecessor chain, or active decision-set digest is tampered;
- partial write, stale writer, post-promotion failure, and orphan recovery;
- reopen reconstructs the full predecessor, decision, invalidation, snapshot, and
  provenance chain;
- preview without confirmation does not mutate the workspace;
- APP-01 display placeholders cannot execute except through the admitted FE-02
  command boundary.

## Allowed Paths

- `src/NexusScholar.Deduplication/**`
- `src/NexusScholar.CorpusSnapshots/**`
- `src/NexusScholar.ResearchWorkspace/**`
- `src/NexusScholar.AppServices/**`
- `src/NexusScholar.Cli/**`
- corresponding focused test projects, architecture tests, conformance tests,
  solution/project/lock files, and `fixtures/conformance/executable-deduplication/**`
- FE-02 completion evidence and closeout-only updates to `PLANS.md`, the feature
  roadmap, and conflicts register

## Excluded Behavior

- specifications, historical fixtures, or accepted ADR edits;
- policy rotation or source analysis/import refresh under active authority;
- Screening, Full Text, report, bundle, workflow, or generic invalidation kinds;
- generic decision/workflow frameworks or manual representative override;
- UI shell, database, API, cloud, synchronization, multi-user, provider, plugin,
  or AI behavior;
- PHP, app, blueprint, production, security, or institutional identity claims.

## Verification

Run focused domain, workspace, AppServices, CLI, architecture, and conformance
tests, then:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
```

## Exit Criteria

- the three actions preview and execute only for an exact verified target;
- every commit is actor-, evidence-, policy-, snapshot-, result-, request-, and
  generation-bound;
- retry is idempotent and conflicting reuse is rejected;
- correction is append-only and explicit;
- membership, representatives, separation constraints, unresolved work,
  provenance, invalidation, and predecessor history reproduce after reopen;
- crash/concurrency tests leave exactly one authoritative project pointer;
- all full validation commands pass and evidence is bound to the tested commit.
