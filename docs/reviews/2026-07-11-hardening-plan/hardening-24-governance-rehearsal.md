# Hardening 24 - Governance and Tagged Rehearsal

Status: in progress.

## Live Controls

- `main` requires a current pull request, resolved conversations, strict `analyze`, `review`, Windows verify, and Linux verify checks;
- administrators are subject to protection; force pushes and deletion are disabled;
- dependency graph/security updates, secret scanning, push protection, and private vulnerability reporting are enabled;
- the `release` environment accepts only `v*` tag deployments;
- Pages uses the HTTPS Actions artifact pipeline.

## Rehearsal Contract

The tag must exactly match `v0.1.0-alpha.1`. A clean hosted runner restores locked inputs, builds, runs all tests, packs twice, compares normalized package contents, executes local-source package smoke, generates and validates the SPDX SBOM, emits release evidence, uploads the artifact set, and creates GitHub build-provenance attestations.

No NuGet source, publication credential, signing certificate, GitHub Release, or publish command is authorized.
