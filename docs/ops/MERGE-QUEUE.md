# Merge Queue

Status date: 2026-07-14

## Current Queue

| Order | Gate | Scope | Status |
|---|---|---|---|
| 1 | FE-01 | Decision-and-snapshot authority implementation on `cdx/fe-01-decision-snapshot-authority` | In progress |

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

## Do Not Queue

ADR 0028 authorizes only the FE-01 baseline authority-initialization scope. Do not queue FE-02 decision append or later package publication, signing, live providers, scraping, persistence/API/cloud, product UI, PDF/OCR, model calls, executable merge decisions, or compatibility claims without the relevant accepted ADR and gate.
