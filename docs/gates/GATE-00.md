# Gate 0: Evidence Freeze

Status: complete as a planning gate. No production code is authorized in this gate.

## Goal

Freeze the discovery evidence needed before implementation:

- blueprint audit,
- PHP architecture map,
- fixture plan,
- conflict register,
- source-backed port matrix,
- gate sequencing and exit criteria.

## Source Of Truth Inputs

1. `AGENTS.md`
2. `PLANS.md`
3. `docs/adr/0001-source-of-truth-and-porting.md`
4. `specs/SOURCE.lock.json`
5. `../nexus-scholar-2-blueprint/*`
6. pinned PHP checkout at `../core` commit `b24d0d71ec7b64003465182477e7edb7f49994f4`
7. current C# scaffold under `src/`, `tests/`, and `fixtures/`

## Gate 0 Deliverables

- `docs/discovery/BLUEPRINT-AUDIT.md`
- `docs/discovery/PHP-ARCHITECTURE-MAP.md`
- `docs/port/GOLDEN-FIXTURE-PLAN.md`
- `docs/port/OPEN-CONFLICTS.md`
- `docs/port/PORT-MATRIX.csv`
- `docs/gates/GATE-00.md`

## Dependency-Ordered Tasks

1. `architecture-owner`: verify source lock, sibling blueprint path, and pinned PHP commit.
2. `spec-owner + architecture-owner`: audit blueprint contracts, schemas, templates, and conformance closure.
3. `porting-owner`: map PHP modules, entry points, invariants, tests, and host boundaries.
4. `governance-owner`: extract explicit and implied product laws, authority rules, and provenance requirements.
5. `conformance-owner`: define fixture catalog, semantic comparison rules, and differential sequencing.
6. `gate-owner`: freeze conflicts, source refs, exclusions, and measurable Gate 0 exit criteria.

## Required Fixture Categories

### Blueprint-first fixtures

- kernel determinism
- protocol lifecycle
- workflow compile snapshots
- artifact and provenance records
- bundle manifest, tamper, and import safety
- plugin manifest and capability grants
- AI task policy and approval boundaries

### PHP differential fixtures

- shared identity and scholarly work merge
- search trace and plan parsing
- deduplication and representative election
- corpus lock and snapshot authority
- screening and adjudication
- citation graph and snowball behavior
- full-text retrieval audit
- export and reporting projections

See `docs/port/GOLDEN-FIXTURE-PLAN.md` for the full catalog and negative cases.

## Allowed And Excluded Paths

Allowed during Gate 0:

- `docs/discovery/`
- `docs/port/`
- `docs/gates/GATE-00.md`

Excluded during Gate 0:

- `src/`
- `tests/`
- `fixtures/` generated outputs
- `specs/` contract adoption work

## Risks And ADR Needs

- Blueprint authority is not frozen when markdown spec, schema, and contract sketch disagree.
- Current C# scaffold contains placeholder behavior that must not silently become product law.
- PHP contains host concerns and one non-portable identity fallback that cannot cross into the C# domain.
- Exact canonical JSON and digest scope still need a local decision before deterministic compatibility claims.

If any implementation gate would require guessing on one of those points, stop the affected work and resolve it through an ADR or an explicit gate decision.

## Verification Commands

Source verification used for Gate 0:

1. `git -C ..\core rev-parse HEAD`
2. inspect `specs/SOURCE.lock.json`

Standard implementation-gate verification from `AGENTS.md`:

1. `dotnet build NexusScholar.Core.slnx -c Release`
2. `dotnet test NexusScholar.Core.slnx -c Release --no-build`
3. `dotnet format NexusScholar.Core.slnx --verify-no-changes --no-restore`

Gate 0 did not require running the .NET build, test, or format commands because no production code was changed.

## Exit Checklist

- Every planned module has a source recorded in `docs/port/PORT-MATRIX.csv`.
- Every selected PHP behavior has a fixture strategy in `docs/port/GOLDEN-FIXTURE-PLAN.md`.
- Every open conflict has an owner or explicit decision gate in `docs/port/OPEN-CONFLICTS.md`.
- Blueprint drift and PHP portability risks are documented.
- No production code, tests, or generated fixtures were changed in Gate 0.
