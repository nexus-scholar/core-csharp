# Phase 6 - Release Engineering Plan

Status: complete through Hardening 24.

## Goal

Produce a tagged, rebuildable, verifiable release artifact set from a clean machine while preserving early-alpha non-claims. Publication remains disabled until package contracts, security controls, and provenance evidence are complete.

## Dependency-Ordered Gates

1. Hardening 19: accept license, versioning, package topology, reproducibility, signing, SBOM, provenance, and publication policy; default all projects to non-packable.
2. Hardening 20: define the first supported package set and complete metadata, dependency topology, deterministic pack, and clean-install smoke tests.
3. Hardening 21: add locked restore, artifact manifest, SBOM, provenance inputs, and reproducibility comparison.
4. Hardening 22: add validation-only release workflow, test artifacts, dependency review, CodeQL/SARIF, and current action runtimes.
5. Hardening 23: repair Pages and update operational docs, security contact, branch board, merge queue, roster, and port matrix.
6. Hardening 24: apply and verify branch protection, private vulnerability reporting, protected release environment, and final clean-machine tagged-release rehearsal.

## Exit Criteria

- MIT license and package/release ADR accepted;
- only explicitly approved projects are packable;
- restore and build inputs pinned and recorded;
- packages install and execute supported smoke paths on a clean machine;
- artifacts include checksums, SBOM, and provenance evidence;
- CI runs dependency, static-analysis, package, and artifact checks without publication secrets;
- Pages and operational/security docs are current;
- `main` protection and private vulnerability reporting are verified live;
- a release-candidate tag rebuilds to the expected artifact manifest.

## Exclusions

- no NuGet publication before a protected publication gate;
- no production, audit-grade, or PHP compatibility claim;
- no live provider, persistence, cloud, PDF/OCR, or product UI expansion.
