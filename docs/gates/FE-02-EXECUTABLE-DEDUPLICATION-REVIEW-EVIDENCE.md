# FE-02 Evidence: Executable Deduplication Review

Status: complete and accepted for the local FE-02 scope

Tested implementation: `614d30531e85551de7e1a8ef4c422fc43f4fe031` on
`cdx/fe-02-executable-deduplication`.

## Implemented Behavior

- Added canonical, actor-bound review commands whose request digest derives the
  request and decision identities and binds the exact authority generation,
  snapshot, decision set, result, policy, target, evidence, action, reason, and
  optional supersession.
- Added deterministic snapshot reduction for merge, keep-separate, and
  mark-unresolved, including grouped and unresolved transitions, representative
  election reuse, active separation constraints, and explicit correction.
- Added atomic v2 authority generations containing the command, decision,
  successor snapshot, invalidation, three provenance events, policy, and a
  canonical manifest linked to its predecessor.
- Added recursive chain verification, exact idempotent replay returning stored
  records, stale compare-and-swap rejection, unique staging, pointer-last
  publication, orphan recovery, and post-promotion quarantine.
- Added UI-neutral AppServices preview/result records and `nexus dedup decide`.
  Preview is non-mutating; `--confirm` is required to publish a successor.
- Added byte-reproducible local conformance fixtures for the command, decision,
  successor snapshot, invalidation, and fixture manifest.

## Invariants Enforced

- Only a policy-authorized human actor-role pair can issue a final action.
- Source evidence and raw sightings remain immutable and addressable.
- Merge is deterministic; keep-separate constrains later transitive merges;
  unresolved work remains explicit.
- Corrections append a new decision and require the exact superseded decision id
  and digest; prior records are never overwritten.
- Every successor binds one exact predecessor, increments its revision by one,
  preserves the immutable analysis and policy bindings, and reconstructs its
  active decision set from verified ancestors.
- Provenance subjects, inputs, outputs, agent, and event identities are verified
  exactly. Protocol and workflow bindings remain absent because FE-02 authority
  is the local Deduplication policy, not an approved protocol.
- Preview performs no file I/O and no mutation. Workspace mutation occurs only
  through the confirmed transactional command boundary.

## Verification

The full repository verifier passed against the tested implementation:

- 31 projects built in Release with 0 warnings and 0 errors;
- 671 tests passed, 0 failed, 0 skipped;
- 27 architecture tests, 127 conformance tests, 63 CLI tests, and 26
  ResearchWorkspace tests passed within that total;
- formatting verification passed;
- release policy retained 12 approved packages at `0.1.0-alpha.2`;
- deterministic package repeat, clean package-smoke restore, SBOM validation,
  CLI doctor, sample, and local no-network demo passed;
- release evidence contained 16 artifacts and 31 lock files and was bound to the
  tested commit.

Commands:

```powershell
dotnet build NexusScholar.Core.slnx -c Release
dotnet test NexusScholar.Core.slnx -c Release --no-build
dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
./scripts/verify.ps1
```

## Scope And Impact

ADR 0029 is fully implemented for its local C# scope; no additional ADR was
required. `CF-014` is resolved for local FE-02 decision reduction and successor
snapshot publication.

An initialized FE-01 authority generation remains a prerequisite; FE-02 does
not add policy rotation or source-analysis refresh. Screening, Full Text,
reporting, bundles, workflow execution, desktop mutation, live providers,
plugins, AI acceptance, database/API/cloud operation, PHP parity, application
parity, security certification, production readiness, and institutional
identity remain unclaimed.
