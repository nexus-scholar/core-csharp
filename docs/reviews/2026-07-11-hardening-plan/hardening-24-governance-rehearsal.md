# Hardening 24 - Governance and Tagged Rehearsal

Status: complete.

## Live Controls

- `main` requires a current pull request, resolved conversations, strict `analyze`, `review`, Windows verify, and Linux verify checks;
- administrators are subject to protection; force pushes and deletion are disabled;
- dependency graph/security updates, secret scanning, push protection, and private vulnerability reporting are enabled;
- the `release` environment accepts only `v*` tag deployments;
- Pages uses the HTTPS Actions artifact pipeline.

## Rehearsal Contract

The tag must exactly match `v0.1.0-alpha.1`. A clean hosted runner restores locked inputs, builds, runs all tests, packs twice, compares normalized package contents, executes local-source package smoke, generates and validates the SPDX SBOM, emits release evidence, uploads the artifact set, and creates GitHub build-provenance attestations.

No NuGet source, publication credential, signing certificate, GitHub Release, or publish command is authorized.

## Completion Evidence

- protected Hardening 24 PR: `#47`;
- merge commit and tagged source: `8436af32a99077dd3cdcccd2183d47f4b46d340b`;
- validation tag: `v0.1.0-alpha.1`;
- clean hosted release-validation run: `29231634501`;
- evidence manifest: `nexus.release-evidence.v1`, clean tree, SDK `10.0.301`, 30 lock files, 12 packages, and 16 recorded artifact files;
- retained workflow artifacts: `release-evidence-8436af32a99077dd3cdcccd2183d47f4b46d340b` and `release-test-results`;
- `gh attestation verify` passed for the package set and bound the workflow, tag, environment, hosted runner, commit, and 17 attested subjects.

The rehearsal created validation artifacts only. It did not publish or sign a NuGet release.
