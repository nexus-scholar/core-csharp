# Merge Queue

Status date: 2026-07-02

## Current Queue

No open PRs expected.

Recently completed:

| PR | Branch / area | Result |
|---|---|---|
| PR02 | CLI init/status | Landed |
| PR03 | CLI import search | Landed |
| PR04 | CLI verify | Landed |
| PR05 | CLI analyze | Landed |
| PR06 | CLI review/clusters | Landed |
| PR07A | main docs workflow tutorial | Landed |
| PR07B | gh-pages public workflow tutorial | Landed |
| PR08 | CLI status/exit-code polish | Landed |

## Do Not Queue Yet

Do not queue these until there is a separate accepted ADR/task:

- merge accept/reject/mark-unresolved execution;
- actor identity for decisions;
- persistence/database/API/cloud;
- live providers or scraping;
- provider credentials;
- UI product shell;
- PDF/OCR;
- AI/model calls;
- AppServices expansion beyond the accepted read-only projection.

APP-01 merge-gate actions are placeholders only. They must not mutate Core records, execute commands, write files, call services, or imply that the CLI/UI can finalize a scientific decision.

## Next Queue Item

PR09 only:

```text
docs/ops: refresh project state after CLI workflow completion
```

After PR09, pause and collect first-tester feedback.
