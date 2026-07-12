# Hardening 18 - Test Strategy Upgrade

Status: complete.

## Delivered

- retained and revalidated the official RFC 8785 Appendix B number vectors and canonicalization negative fixtures;
- added fixed-seed generated identity tests covering 100 overlap-graph scenarios and 10 insertion permutations per scenario;
- added 450 deterministic parser mutations across RIS, Scopus CSV, and BibTeX seeds;
- indexed malformed rehydration, finite-number, authority, digest, parser, identity, and workspace defects in a permanent defect-to-test ledger;
- added real concurrent CLI process imports with revision and artifact assertions;
- added interrupted staging and unreferenced-generation tests proving incomplete state cannot look committed;
- added an executable 53-case scientific-invariant semantic mutation gate;
- added Coverlet collection for all test projects and an informational Cobertura artifact on Ubuntu CI;
- added `scripts/test-phase5.ps1` as the repeatable local test-plus-coverage gate.

## Verification

`scripts/test-phase5.ps1` passed:

- build: passed with zero warnings;
- normal test run: 539 passed;
- coverage test run: 539 passed;
- Cobertura reports: produced for all ten test projects;
- formatting: passed.

`scripts/mutation-phase5.ps1` passed:

- Core mutation matrix: 46 passed;
- conformance mutation matrix: 7 passed.

## Exit Assessment

Every defect family reproduced by the 2026-07-11 review is mapped to permanent regression evidence. Generated cases are deterministic and report their scenario or mutation number. Workspace process tests demonstrate that competing writers cannot silently lose a committed input and that abandoned or orphaned generations remain non-current.

Coverage remains diagnostic information, not a scientific-correctness or release-readiness claim.

## ADR And Compatibility Impact

No ADR was required because scientific behavior did not change. No PHP compatibility claim or golden PHP fixture changed.
