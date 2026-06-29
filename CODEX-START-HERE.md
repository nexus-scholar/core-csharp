# Start Here

Launch Codex from this directory and read `AGENTS.md` first.

This repository is no longer at Gate 0 discovery. The current baseline is `main` after the first public-feedback planning pass.

## Current Routing

1. Read `README.md` for the current implementation surface and non-claims.
2. Read `docs/ops/BRANCH-BOARD.md` and `docs/ops/MERGE-QUEUE.md` for live branch state and recommended next work.
3. For the current public-feedback lane, read `docs/ops/FIRST-PUBLIC-FEEDBACK-PLAN-2026-06-29.md`.
4. For any implementation work, read the relevant accepted ADRs in `docs/adr/` and the target gate/evidence docs in `docs/gates/`.
5. Preserve the non-claims in `README.md` and the public-feedback plan.

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

Do not add live providers, persistence, API/cloud behavior, PDF/OCR, provider SDKs, or UI product-shell behavior unless a later ADR and task explicitly authorize it.
