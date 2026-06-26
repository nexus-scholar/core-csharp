# Gate 1: Repository Quality

Status: local verification complete, hosted CI execution pending.

## Goal

Prove repository quality only:

- clean .NET 10 restore,
- Release build with zero warnings,
- all test projects pass,
- formatting verification passes,
- Windows and Linux CI are configured,
- no live provider or LLM calls occur in tests,
- no forbidden domain dependencies are introduced.

Gate 1 does not adopt blueprint contracts, does not claim blueprint conformance, and does not claim PHP compatibility.

## Inputs

1. `AGENTS.md`
2. `docs/gates/GATE-00.md`
3. `docs/port/OPEN-CONFLICTS.md`
4. `docs/port/GOLDEN-FIXTURE-PLAN.md`
5. repository verification surface:
   - `global.json`
   - `Directory.Build.props`
   - `scripts/verify.ps1`
   - `scripts/verify.sh`
   - `.github/workflows/gate-01.yml`
   - `tests/NexusScholar.Architecture.Tests/*`

## Gate 1 Evidence

See `docs/gates/GATE-01-EVIDENCE.md`.

## Exit Checks

- .NET 10 restore succeeds.
- Release build succeeds with warnings treated as errors.
- Unit, architecture, and conformance tests pass.
- Formatting verification passes.
- CI is configured for Windows and Linux.
- No live provider or model call occurs in tests.
- Domain projects have no host-framework or provider-SDK dependency.

## Gate 1 Scope Limits

Allowed implementation scope for this gate:

- `docs/gates/GATE-01.md`
- `docs/gates/GATE-01-EVIDENCE.md`
- CI and verification scripts only when needed for Gate 1 checks
- tests only when needed to prove architecture or offline execution

Excluded from this gate:

- Gate 2+ domain behavior
- blueprint contract adoption
- PHP code changes
- PHP compatibility fixture generation
- blueprint conformance claims
- PHP compatibility claims
- implicit resolution of Gate 2+ conflicts

## Current Verdict

Repository-quality evidence is green locally on Windows, and GitHub Actions CI is now configured for Windows and Linux. Hosted CI execution was not performed from this session, so Gate 1 should be treated as ready for hosted CI, not as having hosted CI evidence already recorded.
