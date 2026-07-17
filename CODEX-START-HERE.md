# Start Here

Launch Codex from this repository and read `AGENTS.md` first. Its source order,
product laws, domain rules, porting policy, change policy, and verification
requirements remain binding.

## Current Baseline

- Protected `main`: `805f3d6`.
- FE-08 Slice 4 implementation: `7a071cc`.
- Current verification: 906 tests; 23-package reproducibility and clean-source
  smoke; Release build and formatting green.
- Completed: hardening Phases 1-7, Hardening 30, FE-01 through FE-07, and FE-08
  slices 1 through 4.
- Next candidate: FE-08 Slice 5.
- Not authorized: Slice 5 implementation until its ADR and gate are accepted;
  FE-09 through FE-12 implementation; package publication.

The active operating roadmap is:

- `docs/plans/2026-07-14-feature-expansion-priority.md`

The hardening plan is completed historical evidence, not the active work queue:

- `docs/reviews/2026-07-11-hardening-plan/README.md`
- `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`

## Product State

The local Research Workspace supports durable file-backed project state,
authority generations, Deduplication decisions, corpus snapshots, Screening and
Full Text conduct records, workflow execution records, reporting exports, and
append-only export-ledger verification.

This is local durable persistence, but it is not database or cloud persistence.
`nexus.project.json` is a project-relative index and pointer surface, not
canonical scientific authority. Stable identifiers, content digests, canonical
records, verified lineage, and human authority remain the scientific boundary.

The desktop may invoke accepted, authority-checked application commands.
Desktop state, selection state, paths, and view-model rows never become
scientific authority.

## Current CLI Surface

```text
nexus doctor
nexus sample
nexus demo
nexus init --title "<title>"
nexus status
nexus import search <path> --source <source> --format <format>
nexus verify
nexus analyze
nexus review
nexus clusters
nexus clusters exact
nexus clusters review
nexus clusters show <id>
nexus dedup decide --target <id> --action <action> --reason <reason> --actor <id> --role <role>
nexus dedup decide ... --confirm
nexus screening status
nexus report verify <export-id>
nexus bundle verify <export-id>
nexus export verify <export-id>
nexus export status
```

`dedup decide` is preview-only unless `--confirm` is supplied. `screening
status` verifies persisted manifest and artifact integrity but does not replay
source authority. Report, bundle, and export commands verify already persisted
exports; they do not manufacture authority.

## Read Order

1. `AGENTS.md` for invariant and change policy.
2. `README.md` for the current implementation, module map, CLI, and non-claims.
3. `docs/plans/2026-07-14-feature-expansion-priority.md` for the active sequence
   and accepted current/next boundary.
4. `docs/ops/BRANCH-BOARD.md`, `docs/ops/MERGE-QUEUE.md`, and
   `docs/ops/CHAT-ROSTER.md` for live coordination.
5. The relevant accepted ADR in `docs/adr/`.
6. The relevant gate and completion evidence in `docs/gates/` or
   `docs/release/`.
7. `docs/reviews/2026-06-29-main-public-readiness/README.md` only when historical
   public-readiness context is required.

## Starting a Change

1. Verify the exact branch, commit, and clean worktree.
2. Confirm the requested behavior is authorized by an accepted ADR and coherent
   gate. A roadmap entry alone is not implementation authority.
3. Read the owning module, its tests, its subordinate `AGENTS.md` if present,
   and affected architecture/conformance tests.
4. Preserve actor binding, canonical digest scopes, immutable history,
   invalidation, recovery, and explicit non-claims.
5. Add focused positive, adversarial, rehydration, restart, and tamper coverage
   proportional to the boundary.
6. Record completion evidence and update current-state routing when the gate
   actually lands.

For FE-08 Slice 5, do not implement the first desktop Screening mutation until
an accepted ADR and gate define human authority, exact evidence and criteria
bindings, preview/confirmation material, stale-state rejection, supersession,
recovery, provenance, and desktop command-facade boundaries.

## Verification

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

For the complete repository gate:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
```

Also run affected architecture, conformance, package, workspace
crash/concurrency, and fixture checks. CI must not call live scholarly providers
or live model services.
