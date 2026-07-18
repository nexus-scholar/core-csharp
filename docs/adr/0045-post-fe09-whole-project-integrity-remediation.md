# ADR 0045: Post-FE-09 Whole-Project Integrity Remediation

Status: Accepted

Date: 2026-07-18

Decision owner: Nexus Scholar Core maintainers

## Context

An independent whole-project review after ADR 0044 reproduced additional
failures that the normal verification gate did not detect:

- case-insensitive workspace containment checks could admit a case-only sibling
  path on case-sensitive filesystems;
- provider-cache lookup and index rebuild did not verify retained response
  bytes before treating an entry as current;
- a successor corpus authority left downstream project pointers named
  `Current*` even though they were bound to the predecessor snapshot;
- workspace analysis could publish results after source inputs changed between
  analysis and commit;
- Crossref descriptor sanitization was weaker than the common live-provider
  secret policy;
- malformed RIS and Scopus inputs did not fail closed with stable parser
  evidence;
- `WorkId.From(...).ToString()` could produce a value that `WorkId.Parse`
  rejected;
- default UTC timestamps, one-way protocol authority serialization, mutable AI
  proposal values, and bypassable Extensibility records weakened public
  contracts;
- pinned-SDK discovery, the mutation matrix, project claims, and repository
  ownership controls had drifted.

The accepted scientific and compatibility boundaries remain authoritative.
This ADR repairs implementation consistency; it does not authorize FE-10 or
FE-11 runtime work.

## Decision

### Workspace containment and transactions

All Research Workspace paths use one containment implementation. Windows path
comparison is case-insensitive; case-sensitive platforms use ordinal
comparison. Fully qualified paths, parent traversal, and existing reparse or
symbolic-link segments fail closed.

`AnalyzeAndCommit` revalidates every declared source input while holding the
workspace commit lock and before generation promotion. A changed, missing, or
replaced input aborts the commit.

Publishing a successor corpus authority preserves immutable historical
generation directories but clears every downstream `Current*` pointer bound to
the predecessor authority. A later domain action must explicitly regenerate
and republish current downstream authority.

### Provider and parser integrity

Every retained provider-cache body is checked against its exact length and
digest during lookup and index rebuild. Missing or mutated retained bytes
produce a typed cache-integrity failure and are never returned fresh or added
to a rebuilt index.

All provider descriptors use one secret-shaped-value policy. The policy
inspects decoded parameter names and values and rejects credentials, contacts,
authorization material, raw URLs, and common secret forms.

A nested RIS `TY` invalidates the contaminated block; it cannot also produce an
accepted sighting. Duplicate normalized Scopus headers produce deterministic
malformed-input evidence and zero sightings rather than a host exception.

### Scientific identity

ADR 0007's separator is the first separator between authority namespace and
identifier value. Colons inside a normalized identifier value are retained so
canonical rendering round-trips. A value beginning with another approved
namespace token plus `:` remains ambiguous and is rejected, preserving the
existing intentional PHP incompatibility fixture.

### Canonical authority contracts

Canonical scientific timestamps must be both UTC and non-default. Construction
and rehydration enforce the same rule.

The protocol authority codec round-trips both approved and superseded verified
history. Consumers that require active authority, including Screening, perform
their own explicit `Approved` admission check after rehydration.

AI proposal values are snapshotted at construction and cannot be changed by
mutating the caller's object. This does not add model execution, acceptance, or
scientific authority.

Extensibility manifests and capability selections have validated factories,
immutable capability sets, no public constructor bypass, and reject undefined
capability values. This remains a validation-only contract, not a plugin
runtime or security sandbox.

### Delivery evidence

Repository scripts resolve the exact SDK pinned by `global.json` and report an
actionable error when unavailable. The scientific-invariant mutation gate uses
an explicit, count-checked test manifest rather than name substrings.

Current-state documentation distinguishes protected-main facts, historical
closeout evidence, active-branch evidence, implemented contracts, and future
FE-10/FE-11 work. `CODEOWNERS` records repository ownership; hosted protection
settings remain a separate remote operation and must not be claimed until
verified on GitHub.

## Alternatives

- Treating a passing full suite as sufficient was rejected because each
  blocking defect survived that suite.
- Lowercasing all paths was rejected because it changes filesystem identity on
  case-sensitive platforms.
- Deleting stale downstream generations was rejected because it destroys
  reconstructability.
- Rejecting every colon in an identifier value was rejected because a colon
  can be part of a real provider identifier and is not necessarily an authority
  separator.
- Making the protocol codec Screening-specific was rejected because historical
  authority replay and current authority admission are separate concerns.
- Describing Extensibility isolation as a sandbox was rejected by the product
  laws and ADR 0044 nonclaims.

## Consequences

The changes tighten malformed-input behavior and may reject data that was
previously accepted accidentally. Public alpha APIs in AI and Extensibility
become stricter. Successor authority publication requires downstream
regeneration before those workflows can again be current.

No historical generation, fixture output, accepted gate record, or provenance
event is rewritten.

## Migration Effect

No persisted schema version changes. Existing workspaces with a successor
authority and stale downstream current pointers are rejected by verification;
the next authorized successor or repair operation clears those pointers.
Provider caches with missing or mutated retained bodies fail typed verification
and must be reacquired rather than silently repaired.

## Fixture Effect

Existing PHP compatibility classifications remain unchanged. New local
adversarial tests cover case-only sibling traversal, input mutation before
commit, downstream pointer invalidation, cache-body mutation and loss, provider
secret values, malformed RIS/Scopus input, colon-bearing identifiers,
non-default timestamps, superseded protocol replay, mutable AI values, and
Extensibility constructor bypass.

## Reversal Conditions

Any future relaxation requires a successor ADR with evidence for filesystem
semantics, identifier ambiguity, provider retention rights, authority
admission, or extension isolation. FE-10 and FE-11 remain separately gated.
