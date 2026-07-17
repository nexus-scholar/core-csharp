# Merge Queue

Status date: 2026-07-17

## Current Queue

No feature pull request is currently queued.

## Recent Feature Delivery

| PR | Scope | Result |
| --- | --- | --- |
| #54-55 | Hardening 30 remediation and protected-main closeout | Landed |
| #56 | FE-02 executable Deduplication review | Landed |
| #57 | FE-03 workflow execution and FE-04 title/abstract Screening | Landed |
| #58 | FE-05 local Full Text workflow | Landed |
| #59 | FE-06 reporting, audit bundle, and Rapid Review | Landed |
| #60-61 | FE-07 Extraction, Appraisal, Synthesis, and closeout | Landed |
| #62-67 | FE-08 desktop slices 1-4 and closeouts | Landed |
| #68 | Public documentation and Pages closeout | Landed |
| #69 | FE-08 desktop closeout and FE-09 providers, cache, recorded Full Text retrieval, and citation network | Landed |

## Admission Rule

A future branch enters the queue only when it has:

1. an accepted ADR or an explicit accepted determination that existing ADRs
   authorize the behavior;
2. one coherent gate and primary owner;
3. schemas, digest scopes, rehydration, authority, invalidation, and recovery
   rules;
4. positive, adversarial, restart, tamper, stale-state, and architecture tests
   appropriate to the boundary;
5. explicit non-claims and completion evidence.

## Do Not Queue

Do not queue wider provider retention, live Full Text downloads, scraping,
paywall bypass, citation exports, broad PHP parity, plugin execution, model
calls, database/API/cloud, multi-user work, or package publication without their
own accepted gates.
