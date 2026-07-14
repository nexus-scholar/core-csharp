# FE-01 Evidence: Decision And Snapshot Authority

Status: complete and accepted for the local FE-01 scope

Tested implementation: `330d559eba529a055d9b39f73ca7ba430777bdf0` on
`cdx/fe-01-decision-snapshot-authority`.

## Implemented Behavior

- Added closed, canonical Deduplication authority-policy and human-decision
  contracts with verified source, target, evidence, supersession, and digest
  bindings.
- Added the non-packable `NexusScholar.CorpusSnapshots` domain with immutable
  baseline and successor contracts, separate scientific-content and record
  digests, complete membership, verified rehydration, and invalidation planning.
- Reused public Provenance boundaries to represent publication, future decision,
  and invalidation activities without moving authority into Provenance.
- Added atomic ResearchWorkspace authority initialization. Policy, empty-decision
  baseline snapshot, publication event, and manifest are staged, rehydrated,
  digest-verified, promoted, and pointed to last under compare-and-swap checks.
- Added active-authority guards, stale-writer rejection, quarantine after failed
  promotion, orphan recovery, current-generation verification, and read-only
  AppServices projection.
- Added canonical conformance fixtures and architecture rules for the new inward
  dependency direction.

## Invariants Enforced

- Only policy-authorized human actor and role pairs can produce final decision
  records.
- Snapshot membership is complete and unique; no-ID candidates remain explicit
  unresolved entries.
- Scientific-content equality is distinct from record-envelope identity.
- Successors bind the exact predecessor record digest and active decision set.
- Authority generations bind the exact analysis generation, raw manifest,
  verified Deduplication result, policy, snapshot, event, and artifact bytes.
- Import and analysis cannot silently advance while an authority generation is
  active.
- Domain projects remain free of storage, UI, provider, and framework authority.

## Verification

The full repository verifier passed against the tested implementation:

- 31 projects built in Release with 0 warnings and 0 errors;
- 654 tests passed, 0 failed, 0 skipped;
- 27 architecture tests and 125 conformance tests passed within that total;
- formatting verification passed;
- release policy retained exactly 12 approved packages at `0.1.0-alpha.2`;
- deterministic package repeat, clean package-smoke restore, SBOM validation,
  CLI doctor, and local no-network demo passed;
- release evidence contained 16 artifacts and 31 lock files and was bound to the
  tested commit.

Command:

```powershell
./scripts/verify.ps1
```

## Review And Impact

Independent implementation reviews found no remaining blocker in the
CorpusSnapshots, ResearchWorkspace, or AppServices slices after corrections.
The implementation realizes ADR 0028; no additional ADR was required.

`CF-014` is resolved only for FE-01 local C# corpus-snapshot identity and equality.
No PHP persistence, application snapshot, blueprint, database, API, cloud, UI,
live-provider, or model compatibility claim is made. Executable decision append,
merge, keep-separate, unresolved reduction, successor publication, and resulting
invalidation remain FE-02.
