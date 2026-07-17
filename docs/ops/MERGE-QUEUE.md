# Merge Queue

Status date: 2026-07-17

## Current Queue

No feature implementation is queued. FE-08 Slice 5 requires an accepted gate
before implementation.

Recently landed:

| PR | Gate | Result |
|---|---|---|
| #42 | Hardening 19 release policy | Landed |
| #43 | Hardening 20 package topology | Landed |
| #44 | Hardening 21 locked restore and release evidence | Landed |
| #45 | Hardening 22 release and security workflows | Landed |
| #46 | Hardening 23 Pages and operations | Landed |
| #47 | Hardening 24 governance verifier | Landed |
| #48 | Phase 6 closeout | Landed |
| #49 | Hardening 25 shared-identity compatibility evidence | Landed |
| #50 | Hardening 26 Search compatibility evidence | Landed |
| #51 | Hardening 27 Deduplication compatibility evidence | Landed |
| #52 | Hardening 28 Screening/Full Text compatibility evidence | Landed |
| #53 | Hardening 29 Phase 7 closeout | Landed |
| #54 | Hardening 30 post-Phase 7 remediation | Landed |
| #55 | Hardening 30 protected-main closeout | Landed |
| #62 | FE-08 desktop slices 1 and 2 | Landed |
| #64 | FE-08 Slice 3 desktop deduplication review | Landed |
| #65 | FE-08 Slice 3 closeout | Landed |
| #66 | FE-08 Slice 4 durable Screening authority resolution | Landed |

## Do Not Queue

ADR 0037 authorizes authority-package persistence, verification, and read-only
desktop readiness only. Do not queue desktop Screening mutation, Workflow
completion, Protocol or criteria authoring, live providers, scraping, API/cloud,
AI, plugin execution, package publication, or broader compatibility claims
without the relevant accepted ADR and gate.
