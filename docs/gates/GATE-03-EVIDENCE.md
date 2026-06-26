# Gate 3 Evidence

Status: accepted for local Gate 3 protocol lifecycle behavior; hosted matrix verified.

Branch: `cdx/gate-3-protocol-lifecycle`

Implementation commit: `8e3d5c456d98120a43e931236f7da059481e5a12`

Hosted CI run: `https://github.com/nexus-scholar/core-csharp/actions/runs/28253855625`

Hosted matrix:

```text
verify (ubuntu-latest): success
verify (windows-latest): success
```

## Scope Implemented

- Protocol lifecycle records in `src/NexusScholar.Protocol`.
- Local approval policy and approval record semantics.
- Kernel-scoped protocol content and approval record digests.
- Approval candidate review state, immutable approved versions, stale digest rejection, and approval record replay checks.
- Non-human decision/approval rejection, distinct actor enforcement, policy-role enforcement, waiver eligibility checks, deviation validation, and amendment invalidation checks.
- Gate 3 positive and negative conformance fixtures under `fixtures/conformance/protocol/`.
- Focused core, architecture, and conformance tests.

## Local Test Counts

Latest full test run before final verification:

```text
NexusScholar.Core.Tests: 59 passed
NexusScholar.Architecture.Tests: 8 passed
NexusScholar.Conformance.Tests: 13 passed
```

## Commands

```text
dotnet restore NexusScholar.Core.slnx
Result: pass

dotnet build NexusScholar.Core.slnx -c Release --no-restore
Result: pass
Warnings: 0
Errors: 0

dotnet test NexusScholar.Core.slnx -c Release --no-build
Result: pass
NexusScholar.Core.Tests: 59 passed
NexusScholar.Architecture.Tests: 8 passed
NexusScholar.Conformance.Tests: 13 passed

dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore
Result: pass

scripts/verify.ps1
Result: pass
Warnings: 0
Errors: 0
NexusScholar.Core.Tests: 59 passed
NexusScholar.Architecture.Tests: 8 passed
NexusScholar.Conformance.Tests: 13 passed
```

## Explicit Claims Not Made

- no PHP compatibility claim
- no blueprint conformance claim
- no bundle contract adoption
- no persistence or API schema commitment
- no workflow compiler implementation
- no provenance parity claim
- no AI governance parity claim
- no institutional role-engine implementation
