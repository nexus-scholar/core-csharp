# Branch Board

Status date: 2026-07-02

## Current Repository State

- `main`: current after PR08 CLI polish.
- Latest known main commit: `0d93bd9 polish(cli): improve workspace status and exit-code consistency`.
- `gh-pages`: current after public CLI workflow walkthrough.
- Latest known gh-pages commit: `589fc2e docs(site): add research workspace CLI walkthrough`.
- Open PRs: none expected.
- Active next branch: `cdx/ops-refresh-after-cli-workflow`.

## Completed Public CLI Workflow

PR02-PR08 delivered the local researcher-facing CLI loop:

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

A Nexus research project is a local folder. `nexus.project.json` is a local project index, not a database and not canonical scientific authority.

The CLI verifies local files, analyzes imported Search/Deduplication evidence, and shows records requiring human review. It does not query live providers or execute merge decisions.

## Current Product Boundary

The implemented workflow is local and read-only around review/cluster inspection.

It uses researcher-supplied local files and generated local outputs. It does not add:

- live providers;
- scraping;
- provider credentials;
- persistence/database/API/cloud;
- PDF/OCR;
- AI/model calls;
- Core mutation;
- PHP compatibility claims;
- executable merge decisions.

APP-01 merge gates remain display-only placeholders. They must not accept, reject, mark unresolved, mutate Core records, write decision state, call services, or execute commands.

## Current Next Move

Pause feature expansion and collect first-tester feedback on the public CLI workflow.

Triage feedback into:

1. docs/site polish;
2. CLI usability polish;
3. later ADR candidates.

Do not start merge decisions, persistence, providers, UI product shell, PDF/OCR, AI/model calls, or AppServices expansion without a later explicit ADR/task.

## Safe Cleanup Candidates

None expected.

## Not Safe To Delete

- `main`
- `gh-pages`
- `origin/main`
- `origin/gh-pages`
