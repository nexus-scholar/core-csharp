# UI Guides And Specs Import - 2026-07-02

This folder preserves the UI guide/spec pack supplied for the next Nexus Scholar Core UI planning pass.

These files are design and planning inputs. They are not accepted ADRs, not Core authority, not conformance fixtures, not PHP compatibility evidence, and not production web code.

## Source

- Original archive: `C:\Users\mouadh\Downloads\nexus-ui-guides-and-specs (1).zip`
- Archive SHA-256: `DFCBA52F81ABD32F857187F026C88FD1ABE30D13A720816DB4FB09F3C221B69B`
- Import date: 2026-07-02

## Files

| File | Purpose | SHA-256 |
|---|---|---|
| `nexus-csharp-core-jetbrains-ui-spec-v3.md` | Main C# Core desktop UI/UX spec, JetBrains-style shell direction, workflow screens, read models, release slicing, and acceptance criteria. Trailing Markdown whitespace was normalized during import so `git diff --check` stays clean. | `81182B3D9114E3FB1D877105ECEE8AFF78F3BD5D357BE1A869E0AF9A82695C2F` |
| `nexus-ui-guide.html` | Static visual guide/mockup for screens and components. | `5FE2AEB2E08285AE39BD74BC662CAD05535174DF2FACD2E81BBCCE948CFF0F05` |
| `nexus-ui-react-guide.jsx` | React mockup guide for screen/component structure. Not production code for this C# repo. | `26B6982BC57031169FB3B71AD2124AC59F5A8E94E975FD6B3ADA2D8D0AAF5965` |
| `nexus-ui-react-guide.css` | Styling companion for the React/static guide. Not production styling for Avalonia. | `0D3E41A3AF1A107DBFB060196280D0A3A0D976342F948089C6E5D0A65903450B` |

## Boundary

The imported specs support a future local desktop UI plan, but they do not authorize:

- persistence/database/API/cloud behavior;
- live providers or scraping;
- provider credentials;
- PDF/OCR;
- AI/model calls;
- Core mutation;
- executable merge decisions;
- PHP compatibility claims;
- making `nexus.project.json` canonical scientific authority.

A Nexus research project remains a local folder. `nexus.project.json` is a local project index, not a database and not canonical scientific state.

APP-01 merge-gate actions remain display-only placeholders until a later accepted ADR/task defines actor identity, decision persistence, provenance semantics, and mutation rules.
