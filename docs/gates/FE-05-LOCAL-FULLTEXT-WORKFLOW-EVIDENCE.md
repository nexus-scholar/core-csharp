# FE-05 Local Full Text Workflow Completion Evidence

Date: 2026-07-16  
Status: complete; hosted CI passed and PR 58 merged
Authority: ADR 0032

## Delivered Behavior

- verified admission from a current FE-04 included handoff, binding its conduct,
  policy, candidate set, candidate, verdict, supporting decisions, and derived
  Full Text input;
- canonical acquisition, artifact, extraction-attempt, conduct, invalidation,
  and handoff records with strict digest and byte rehydration;
- exact raw-byte retention with local path kept outside scientific identity;
- deterministic UTF-8 text and safe XML extraction, with PDF extraction
  explicitly recorded as unsupported;
- human-only full-text decisions bound to approved Protocol, criteria,
  admission, raw artifact, and any exact verified extraction attempt used;
- complete source-scoped invalidation, independent review, correction,
  conflict, adjudication, and handoff replay;
- pointer-last immutable ResearchWorkspace generations with one lock,
  stale-writer rejection, quarantine after failed promotion, exact inventory,
  case-correct path checks, and order-independent generation identity;
- AppServices preview/commit ports and authority-neutral CLI status that verifies
  the complete manifest and artifact inventory without claiming authority replay;
- a local source reader that rejects HTTP, HTTPS, UNC, and other network input
  while enforcing the configured byte limit.

## Invariants Enforced

- ids, digest strings, automation, paths, parser output, and app projections do
  not create Full Text authority;
- failed or unsupported extraction cannot support exclusion, including through
  generic evidence or canonical replay;
- extraction evidence must be exactly the policy-bound verified attempt;
- raw and derived representations retain separate identities;
- missing, extra, altered, misnamed, duplicate, or inconsistently declared
  generation artifacts fail closed;
- transaction failure cannot advance the project pointer or discard history.

## Review Remediation

Independent scientific-invariant and test-engineering reviews initially found
an extraction-failure evidence bypass, incomplete CLI artifact verification,
optional extraction inventory inconsistency, order-dependent generation ids,
cross-platform case handling, missing URL-boundary coverage, and host-specific
package verification. Commits `3e64d77` and `d7749fd` close those findings.
Both reviewers subsequently reported no remaining actionable defects.

## Local Verification

The final local gate uses the repository-pinned .NET SDK `10.0.301`:

```powershell
C:\Users\mouadh\.dotnet\dotnet.exe build NexusScholar.Core.slnx -c Release --no-restore
C:\Users\mouadh\.dotnet\dotnet.exe test NexusScholar.Core.slnx -c Release --no-build
C:\Users\mouadh\.dotnet\dotnet.exe format NexusScholar.Core.slnx --verify-no-changes --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-packages.ps1
```

The Release build has zero warnings and errors. The solution has 732 passing
tests with zero failures. Format verification is clean. Release-policy
regressions pass, sixteen packages reproduce normalized content, and a clean
local-source smoke installation loads all sixteen assemblies under Windows
PowerShell 5.1 without a PATH override.

Hosted Windows, Ubuntu, review, CodeQL, and analysis checks passed on PR 58.
The feature merged to `main` as commit `3d8ec37`.

## Claims

This evidence supports a local, no-network C# Full Text workflow contract. It
does not claim PHP, PDF parser, OCR, retrieval, paywall, legal-certification,
database, API, cloud, production, scale, security-certification, or
institutional compatibility.
