using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Workflow;

namespace NexusScholar.Bundles;

public sealed class BundleVerifier
{
    public BundleVerification Verify(
        ReviewBundleManifest manifest,
        BundleVerificationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var verificationOptions = options ?? new BundleVerificationOptions();
        var errors = new List<BundleVerificationFinding>();
        var warnings = new List<BundleVerificationFinding>();
        var verifiedArtifacts = new List<BundleArtifactEntry>();

        ValidateManifestIdentity(manifest, errors);
        ValidateProtocolBinding(manifest, verificationOptions, errors);
        ValidateWorkflowBinding(manifest, verificationOptions, errors);
        ValidateRequiredSchemas(manifest, verificationOptions, errors);
        ValidateProvenanceBindings(manifest, verificationOptions, errors);
        ValidateArtifacts(manifest, verificationOptions, errors, verifiedArtifacts);
        ValidateDestructiveOverwrite(manifest, verificationOptions, errors);

        var manifestDigest = CanComputeManifestDigest(manifest)
            ? manifest.ComputeManifestDigest()
            : default;
        if (manifestDigest.IsValid &&
            verificationOptions.ExpectedManifestDigest is not null &&
            !string.Equals(verificationOptions.ExpectedManifestDigest.Value.ToString(), manifestDigest.ToString(), StringComparison.Ordinal))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.StaleManifestDigest,
                "Manifest digest does not match the expected manifest digest.",
                manifest.BundleId));
        }

        return new BundleVerification(
            errors.Count == 0,
            errors,
            warnings,
            verifiedArtifacts,
            manifestDigest);
    }

    private static void ValidateManifestIdentity(
        ReviewBundleManifest manifest,
        ICollection<BundleVerificationFinding> errors)
    {
        if (string.IsNullOrWhiteSpace(manifest.BundleId))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.MissingRequiredSection,
                "Missing bundle id.",
                "manifest_identity"));
        }

        if (!string.Equals(manifest.BundleKind, BundleConstants.BundleKindReview, StringComparison.Ordinal))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidManifest,
                "Bundle kind must be review-bundle.",
                manifest.BundleKind));
        }

        if (!string.Equals(manifest.SchemaId, BundleConstants.ManifestSchemaId, StringComparison.Ordinal))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidManifest,
                "Unsupported bundle manifest schema id.",
                manifest.SchemaId));
        }

        if (!string.Equals(manifest.SchemaVersion, BundleConstants.ManifestSchemaVersion, StringComparison.Ordinal))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidManifest,
                "Unsupported bundle manifest schema version.",
                manifest.SchemaVersion));
        }

        if (string.IsNullOrWhiteSpace(manifest.CreatedBy))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.MissingRequiredSection,
                "Missing bundle creator.",
                "manifest_identity"));
        }
    }

    private static void ValidateProtocolBinding(
        ReviewBundleManifest manifest,
        BundleVerificationOptions options,
        ICollection<BundleVerificationFinding> errors)
    {
        if (!manifest.ProtocolBinding.IsApproved)
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidProtocolBinding,
                "Bundle protocol binding must point to an approved protocol version.",
                manifest.ProtocolBinding.ProtocolVersionId));
        }

        if (!HasValidDigest(manifest.ProtocolBinding.ProtocolContentDigest))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidProtocolBinding,
                "Protocol binding digest must be a canonical content digest.",
                manifest.ProtocolBinding.ProtocolVersionId));
            return;
        }

        var authority = options.AuthorityResolver?.ResolveProtocolVersion(manifest.ProtocolBinding.ProtocolVersionId);
        if (authority is null)
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidProtocolBinding,
                "Protocol binding digest must match a known protocol-content envelope digest.",
                manifest.ProtocolBinding.ProtocolVersionId));
            return;
        }

        var protocol = authority.Version;
        if (protocol.Status != ProtocolStatus.Approved ||
            !string.Equals(protocol.ProtocolId, manifest.ProtocolBinding.ProtocolId, StringComparison.Ordinal) ||
            protocol.VersionNumber != manifest.ProtocolBinding.VersionNumber ||
            protocol.ContentDigest != manifest.ProtocolBinding.ProtocolContentDigest)
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidProtocolBinding,
                "Protocol binding digest does not match the known protocol-content envelope digest.",
                manifest.ProtocolBinding.ProtocolVersionId));
        }
    }

    private static void ValidateWorkflowBinding(
        ReviewBundleManifest manifest,
        BundleVerificationOptions options,
        ICollection<BundleVerificationFinding> errors)
    {
        if (manifest.WorkflowBinding is null)
        {
            return;
        }

        if (!manifest.WorkflowBinding.WorkflowDefinitionDigest.IsValid ||
            !manifest.WorkflowBinding.TemplateDigest.IsValid ||
            !manifest.WorkflowBinding.BoundProtocolContentDigest.IsValid ||
            !manifest.ProtocolBinding.ProtocolContentDigest.IsValid)
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidWorkflowBinding,
                "Workflow binding digests must be canonical content digests.",
                manifest.WorkflowBinding.WorkflowId));
            return;
        }

        if (!string.Equals(
                manifest.WorkflowBinding.BoundProtocolVersionId,
                manifest.ProtocolBinding.ProtocolVersionId,
                StringComparison.Ordinal))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidWorkflowBinding,
                "Workflow binding protocol version does not match manifest protocol binding.",
                manifest.WorkflowBinding.WorkflowId));
        }

        var authority = options.AuthorityResolver?.ResolveWorkflowDefinition(manifest.WorkflowBinding.WorkflowId);
        var definition = authority;
        if (definition is null ||
            definition.WorkflowDigest != manifest.WorkflowBinding.WorkflowDefinitionDigest ||
            !string.Equals(definition.TemplateId, manifest.WorkflowBinding.TemplateId, StringComparison.Ordinal) ||
            !string.Equals(definition.TemplateVersion, manifest.WorkflowBinding.TemplateVersion, StringComparison.Ordinal) ||
            definition.TemplateDigest != manifest.WorkflowBinding.TemplateDigest ||
            !string.Equals(definition.ProtocolVersionId, manifest.WorkflowBinding.BoundProtocolVersionId, StringComparison.Ordinal) ||
            definition.ProtocolContentDigest != manifest.WorkflowBinding.BoundProtocolContentDigest)
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidWorkflowBinding,
                "Workflow binding does not match a resolved verified Workflow definition.",
                manifest.WorkflowBinding.WorkflowId));
        }

        if (!string.Equals(
                manifest.WorkflowBinding.BoundProtocolContentDigest.ToString(),
                manifest.ProtocolBinding.ProtocolContentDigest.ToString(),
                StringComparison.Ordinal))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidWorkflowBinding,
                "Workflow binding protocol digest does not match manifest protocol binding.",
                manifest.WorkflowBinding.WorkflowId));
        }
    }

    private static void ValidateRequiredSchemas(
        ReviewBundleManifest manifest,
        BundleVerificationOptions options,
        ICollection<BundleVerificationFinding> errors)
    {
        if (!options.RequireSupportedSchemas)
        {
            return;
        }

        var supported = options.SupportedRequiredSchemas
            .Select(schema => $"{schema.SchemaId}\n{schema.SchemaVersion}")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var requiredSchema in manifest.RequiredSchemas)
        {
            if (!supported.Contains($"{requiredSchema.SchemaId}\n{requiredSchema.SchemaVersion}"))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.UnsupportedRequiredSchema,
                    "Required schema is not supported by the local importer.",
                    $"{requiredSchema.SchemaId}/{requiredSchema.SchemaVersion}"));
            }
        }


        var required = manifest.RequiredSchemas
            .Select(schema => $"{schema.SchemaId}\n{schema.SchemaVersion}")
            .ToHashSet(StringComparer.Ordinal);
        foreach (var artifact in manifest.Artifacts)
        {
            if (!required.Contains($"{artifact.SchemaId}\n{artifact.SchemaVersion}"))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.UnsupportedRequiredSchema,
                    "Artifact schema is not declared in required schemas.",
                    artifact.LogicalPath));
            }
        }
    }

    private static void ValidateProvenanceBindings(
        ReviewBundleManifest manifest,
        BundleVerificationOptions options,
        ICollection<BundleVerificationFinding> errors)
    {
        foreach (var binding in manifest.ProvenanceBindings)
        {
            if (!HasValidDigest(binding.EventDigest))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.InvalidProvenanceBinding,
                    "Provenance binding digest must be a canonical content digest.",
                    binding.EventId));
                continue;
            }

            var authority = options.AuthorityResolver?.ResolveProvenanceEvent(binding.EventId);
            if (authority is null)
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.InvalidProvenanceBinding,
                    "Provenance binding digest must match a known provenance-event envelope digest.",
                    binding.EventId));
                continue;
            }

            if (authority.EventDigest != binding.EventDigest ||
                authority.ToDigestEnvelope().ComputeDigest() != authority.EventDigest ||
                !string.Equals(authority.Activity.ActivityId, binding.ActivityKind, StringComparison.Ordinal) ||
                authority.OccurredAt != binding.RecordedAt ||
                !string.Equals(authority.Agent.AgentId, binding.ActorId, StringComparison.Ordinal))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.InvalidProvenanceBinding,
                    "Provenance binding digest does not match the known event digest.",
                    binding.EventId));
            }
        }
    }

    private static void ValidateArtifacts(
        ReviewBundleManifest manifest,
        BundleVerificationOptions options,
        ICollection<BundleVerificationFinding> errors,
        ICollection<BundleArtifactEntry> verifiedArtifacts)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var artifact in manifest.Artifacts)
        {
            var authorityDigestsValid = true;

            if ((artifact.ProvenanceEventId is null) != (artifact.ProvenanceEventDigest is null))
            {
                authorityDigestsValid = false;
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.InvalidProvenanceBinding,
                    "Artifact provenance event id and digest must be supplied together.",
                    artifact.LogicalPath));
            }

            if (!seen.Add(artifact.LogicalPath))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.DuplicateArtifactPath,
                    "Duplicate artifact logical path.",
                    artifact.LogicalPath));
            }

            if (!BundleArtifactPath.TryValidate(artifact.LogicalPath, out var pathReason))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.InvalidArtifactPath,
                    pathReason,
                    artifact.LogicalPath));
            }

            if (artifact.SizeBytes < 0)
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.NegativeArtifactSize,
                    "Artifact size must be non-negative.",
                    artifact.LogicalPath));
            }

            if (!HasValidDigest(artifact.RawByteDigest))
            {
                authorityDigestsValid = false;
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.InvalidArtifactDigest,
                    "Artifact raw byte digest must be a canonical SHA-256 digest.",
                    artifact.LogicalPath));
            }

            if (artifact.SourceRecordDigest is not null && !artifact.SourceRecordDigest.Value.IsValid)
            {
                authorityDigestsValid = false;
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.InvalidArtifactDigest,
                    "Artifact source record digest must be a canonical SHA-256 digest.",
                    artifact.LogicalPath));
            }

            if (artifact.ProvenanceEventDigest is not null)
            {
                var errorCount = errors.Count;
                ValidateArtifactProvenanceDigest(artifact, options, errors);
                authorityDigestsValid = authorityDigestsValid && errors.Count == errorCount;
            }

            if (options.ExistingArtifactDigests.TryGetValue(artifact.LogicalPath, out var existingDigest) &&
                !existingDigest.IsValid)
            {
                authorityDigestsValid = false;
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.DestructiveOverwrite,
                    "Existing artifact digest must be valid before overwrite safety can be verified.",
                    artifact.LogicalPath));
            }

            if (!options.ArtifactBytes.TryGetValue(artifact.LogicalPath, out var bytes))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.MissingArtifact,
                    "Artifact bytes were not supplied for verification.",
                    artifact.LogicalPath));
                continue;
            }

            if (!authorityDigestsValid)
            {
                continue;
            }

            var observedDigest = BundleArtifactEntry.ComputeRawByteDigest(bytes);
            if (!string.Equals(observedDigest.ToString(), artifact.RawByteDigest.ToString(), StringComparison.Ordinal))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.ChecksumMismatch,
                    "Artifact raw byte digest does not match supplied bytes.",
                    artifact.LogicalPath));
                continue;
            }

            if (bytes.LongLength != artifact.SizeBytes)
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.ArtifactSizeMismatch,
                    "Artifact byte length does not match manifest size.",
                    artifact.LogicalPath));
                continue;
            }

            verifiedArtifacts.Add(artifact);
        }
    }

    private static void ValidateArtifactProvenanceDigest(
        BundleArtifactEntry artifact,
        BundleVerificationOptions options,
        ICollection<BundleVerificationFinding> errors)
    {
        if (!HasValidDigest(artifact.ProvenanceEventDigest!.Value))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidProvenanceBinding,
                "Artifact provenance event digest must be a canonical content digest.",
                artifact.LogicalPath));
            return;
        }

        if (string.IsNullOrWhiteSpace(artifact.ProvenanceEventId))
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidProvenanceBinding,
                "Artifact provenance event digest requires a provenance event id.",
                artifact.LogicalPath));
            return;
        }

        var authority = options.AuthorityResolver?.ResolveProvenanceEvent(artifact.ProvenanceEventId);
        if (authority is null)
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidProvenanceBinding,
                "Artifact provenance event digest must match a known provenance-event envelope digest.",
                artifact.LogicalPath));
            return;
        }

        if (authority.EventDigest != artifact.ProvenanceEventDigest.Value ||
            authority.ToDigestEnvelope().ComputeDigest() != authority.EventDigest)
        {
            errors.Add(new BundleVerificationFinding(
                BundleErrorCodes.InvalidProvenanceBinding,
                "Artifact provenance event digest does not match the known provenance-event envelope digest.",
                artifact.LogicalPath));
        }
    }

    private static void ValidateDestructiveOverwrite(
        ReviewBundleManifest manifest,
        BundleVerificationOptions options,
        ICollection<BundleVerificationFinding> errors)
    {
        foreach (var artifact in manifest.Artifacts)
        {
            if (!options.ExistingArtifactDigests.TryGetValue(artifact.LogicalPath, out var existingDigest))
            {
                continue;
            }

            if (!existingDigest.IsValid || !artifact.RawByteDigest.IsValid)
            {
                continue;
            }

            if (!string.Equals(existingDigest.ToString(), artifact.RawByteDigest.ToString(), StringComparison.Ordinal))
            {
                errors.Add(new BundleVerificationFinding(
                    BundleErrorCodes.DestructiveOverwrite,
                    "Import would overwrite an existing artifact with different bytes.",
                    artifact.LogicalPath));
            }
        }
    }

    private static bool HasValidDigest(ContentDigest digest) => digest.IsValid;

    private static bool CanComputeManifestDigest(ReviewBundleManifest manifest)
    {
        return manifest.ProtocolBinding.ProtocolContentDigest.IsValid &&
            (manifest.WorkflowBinding is null ||
             (manifest.WorkflowBinding.WorkflowDefinitionDigest.IsValid &&
              manifest.WorkflowBinding.BoundProtocolContentDigest.IsValid &&
              manifest.WorkflowBinding.TemplateDigest.IsValid)) &&
            manifest.ProvenanceBindings.All(binding => binding.EventDigest.IsValid) &&
            manifest.Artifacts.All(artifact =>
                artifact.RawByteDigest.IsValid &&
                (artifact.SourceRecordDigest is null || artifact.SourceRecordDigest.Value.IsValid) &&
                (artifact.ProvenanceEventDigest is null || artifact.ProvenanceEventDigest.Value.IsValid));
    }
}
