# Merge Queue

Status date: 2026-07-17

## Current Queue

No feature implementation is queued. FE-08 Slice 5 requires an accepted ADR and
gate before code is authorized.

## Recent Feature Delivery

| PR | Scope | Result |
| --- | --- | --- |
| #54-55 | Hardening 30 remediation and protected-main closeout | Landed |
| protected-main commits through `4eccd34` | FE-01 decision and snapshot authority | Landed |
| #56 | FE-02 executable Deduplication review | Landed |
| #57 | FE-03 workflow execution and FE-04 title/abstract Screening | Landed |
| #58 | FE-05 local Full Text workflow | Landed |
| #59 | FE-06 reporting, audit bundle, and Rapid Review | Landed |
| #60-61 | FE-07 Extraction, Appraisal, Synthesis, and closeout | Landed |
| #62-63 | FE-08 slices 1-2 desktop foundation and closeout | Landed |
| #64-65 | FE-08 Slice 3 desktop Deduplication review and closeout | Landed |
| #66-67 | FE-08 Slice 4 Screening authority resolution and closeout | Landed |

## Admission Rule

A future branch enters the queue only when it has:

1. an accepted ADR or an explicit accepted determination that existing ADRs
   fully authorize the behavior;
2. one coherent gate and primary owner;
3. schemas, digest scopes, rehydration, authority, invalidation, and recovery
   rules;
4. positive, adversarial, restart, tamper, stale-state, and architecture tests
   appropriate to the boundary;
5. explicit non-claims and completion evidence.

## Slice 5 Boundary

Do not queue desktop Screening mutation until the gate defines exact Protocol,
criteria, candidate, snapshot, workflow, actor, and role bindings; preview and
confirmation material; stale/concurrent rejection; correction, conflict,
supersession, and invalidation; atomic local persistence and recovery; and
provenance.

Desktop action descriptors remain presentation contracts. Only a dedicated,
accepted application command may create authoritative Screening state.

## Do Not Queue

Do not queue live providers, scraping, PDF/OCR, plugin execution, model calls,
database/API/cloud, multi-user work, package publication, or broad compatibility
claims without their own accepted gates.
