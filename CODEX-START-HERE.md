# Start Here

Launch Codex from this directory and read `AGENTS.md` first.

## Current State

The active plan is integrity hardening, not feature expansion. Start with `docs/reviews/2026-07-11-hardening-plan/README.md`; the full persisted review is `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md`.

The public local Research Workspace CLI loop is implemented:

```bash
nexus init
nexus status
nexus import search
nexus verify
nexus analyze
nexus review
nexus clusters
nexus clusters exact
nexus clusters review
nexus clusters show <id>
```

The workflow is local-first and uses researcher-supplied export files. Review and cluster commands are read-only. Merge actions are display-only APP-01 placeholders.

A Nexus research project is a local folder. `nexus.project.json` is a local project index, not a database and not canonical scientific authority.

The CLI verifies local files, analyzes imported Search/Deduplication evidence, and shows records requiring human review. It does not query live providers or execute merge decisions.

## Current Routing

1. Read `docs/reviews/2026-07-11-hardening-plan/README.md` for the active hardening plan.
2. Read `docs/reviews/2026-07-11-hardening-plan/full-technical-review.md` before opening or implementing a hardening branch.
3. Read `README.md` for the current implementation surface and non-claims.
4. Read `docs/ops/BRANCH-BOARD.md` and `docs/ops/MERGE-QUEUE.md` for branch and queue state.
5. Read `docs/reviews/2026-06-29-main-public-readiness/README.md` only as historical public-readiness context.
6. For implementation work, read the relevant accepted ADRs in `docs/adr/` and the target gate/evidence docs in `docs/gates/`.
7. Preserve the non-claims in `README.md`, `AGENTS.md`, and active ops docs.

## Start Here

1. Run the existing validation commands.
2. Pick the next blocker from the hardening dependency order.
3. Confirm the blocker against current code, ADRs, fixtures, and tests before editing.
4. Add or preserve regression coverage for the reproduced defect.
5. Do not start merge-decision execution, persistence, providers, UI shell, PDF/OCR, AI/model calls, AppServices expansion, or PHP compatibility claims without a specific accepted task/ADR.

## Verification

For normal changes run:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
```

The repository script is also available:

```powershell
powershell -ExecutionPolicy Bypass -File ./scripts/verify.ps1
```

APP-01 merge-gate actions are placeholders only. They must not mutate Core records, execute commands, write files, call services, or imply that the CLI/UI can finalize a scientific decision.
