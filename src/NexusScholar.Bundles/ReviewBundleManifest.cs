using System.Collections.ObjectModel;
using NexusScholar.Kernel;
using NexusScholar.Shared;

namespace NexusScholar.Bundles;

public static class BundleErrorCodes
{
    public const string InvalidManifest = "invalid-manifest";
    public const string InvalidManifestDigest = "invalid-manifest-digest";
    public const string UnsupportedRequiredSchema = "unsupported-required-schema";
    public const string MissingRequiredSection = "missing-required-section";
    public const string InvalidArtifactPath = "invalid-artifact-path";
    public const string DuplicateArtifactPath = "duplicate-artifact-path";
    public const string InvalidArtifactDigest = "invalid-artifact-digest";
    public const string NegativeArtifactSize = "negative-artifact-size";
    public const string MissingArtifact = "missing-artifact";
    public const string ArtifactSizeMismatch = "artifact-size-mismatch";
    public const string ChecksumMismatch = "checksum-mismatch";
    public const string StaleManifestDigest = "stale-manifest-digest";
    public const string DestructiveOverwrite = "destructive-overwrite";
    public const string InvalidProtocolBinding = "invalid-protocol-binding";
    public const string InvalidWorkflowBinding = "invalid-workflow-binding";
    public const string InvalidProvenanceBinding = "invalid-provenance-binding";
}

public static class BundleConstants
{
    public const string ManifestSchemaId = "nexus.review-bundle.manifest";
    public const string ManifestSchemaVersion = "1.0.0";
    public const string BundleKindReview = "review-bundle";
    public const string ApprovedProtocolStatus = "approved";
}

public static class BundleArtifactPath
{
    public static string Normalize(string value)
    {
        value = Guard.NotBlank(value, nameof(value)).Trim();
        if (!TryValidate(value, out var reason))
        {
            throw new ArgumentException(reason, nameof(value));
        }

        return value;
    }

    public static bool TryValidate(string? value, out string reason)
    {
        reason = string.Empty;
        if (value is null)
        {
            reason = "Logical artifact path is required.";
            return false;
        }

        var path = value.Trim();
        if (path.Length == 0)
        {
            reason = "Logical artifact path is required.";
            return false;
        }

        if (path.IndexOf('\\') >= 0)
        {
            reason = "Logical artifact paths must use '/'.";
            return false;
        }

        if (path.StartsWith("/", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal))
        {
            reason = "Logical artifact paths cannot start or end with '/'.";
            return false;
        }

        if (path.Length >= 3 &&
            char.IsAsciiLetter(path[0]) &&
            path[1] == ':' &&
            path[2] == '/')
        {
            reason = "Logical artifact paths cannot be drive-letter paths.";
            return false;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
            !string.IsNullOrWhiteSpace(uri.Scheme))
        {
            reason = "Logical artifact paths cannot be URIs.";
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0)
            {
                reason = "Logical artifact paths cannot contain empty segments.";
                return false;
            }

            if (segment is "." or "..")
            {
                reason = "Logical artifact paths cannot contain traversal or dot segments.";
                return false;
            }
        }

        return true;
    }
}

public sealed class BundleSchemaRef : IEquatable<BundleSchemaRef>
{
    public BundleSchemaRef(string schemaId, string schemaVersion)
    {
        SchemaId = Guard.NotBlank(schemaId, nameof(schemaId));
        SchemaVersion = Guard.NotBlank(schemaVersion, nameof(schemaVersion));
    }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("schema_id", SchemaId)
            .Add("schema_version", SchemaVersion);
    }

    public bool Equals(BundleSchemaRef? other)
    {
        return other is not null &&
            string.Equals(SchemaId, other.SchemaId, StringComparison.Ordinal) &&
            string.Equals(SchemaVersion, other.SchemaVersion, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as BundleSchemaRef);

    public override int GetHashCode() =>
        HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(SchemaId),
            StringComparer.Ordinal.GetHashCode(SchemaVersion));
}

public sealed class BundleProtocolBinding
{
    public BundleProtocolBinding(
        string protocolId,
        string protocolVersionId,
        int versionNumber,
        string status,
        ContentDigest protocolContentDigest)
    {
        ProtocolId = Guard.NotBlank(protocolId, nameof(protocolId));
        ProtocolVersionId = Guard.NotBlank(protocolVersionId, nameof(protocolVersionId));
        VersionNumber = versionNumber;
        Status = Guard.NotBlank(status, nameof(status));
        ProtocolContentDigest = protocolContentDigest;
    }

    public static DigestScope ProtocolContentDigestScope => DigestScope.ProtocolContent;

    public string ProtocolId { get; }

    public string ProtocolVersionId { get; }

    public int VersionNumber { get; }

    public string Status { get; }

    public ContentDigest ProtocolContentDigest { get; }

    public bool IsApproved => string.Equals(Status, BundleConstants.ApprovedProtocolStatus, StringComparison.Ordinal);

    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("protocol_id", ProtocolId)
            .Add("protocol_version_id", ProtocolVersionId)
            .Add("version_number", VersionNumber)
            .Add("status", Status)
            .Add("protocol_content_digest", ProtocolContentDigest.ToString());
    }
}

public sealed class BundleWorkflowBinding
{
    public BundleWorkflowBinding(
        string workflowId,
        ContentDigest workflowDefinitionDigest,
        string templateId,
        string templateVersion,
        ContentDigest templateDigest,
        string boundProtocolVersionId,
        ContentDigest boundProtocolContentDigest)
    {
        WorkflowId = Guard.NotBlank(workflowId, nameof(workflowId));
        WorkflowDefinitionDigest = workflowDefinitionDigest;
        TemplateId = Guard.NotBlank(templateId, nameof(templateId));
        TemplateVersion = Guard.NotBlank(templateVersion, nameof(templateVersion));
        TemplateDigest = templateDigest;
        BoundProtocolVersionId = Guard.NotBlank(boundProtocolVersionId, nameof(boundProtocolVersionId));
        BoundProtocolContentDigest = boundProtocolContentDigest;
    }

    public string WorkflowId { get; }

    public ContentDigest WorkflowDefinitionDigest { get; }

    public string TemplateId { get; }

    public string TemplateVersion { get; }

    public ContentDigest TemplateDigest { get; }

    public string BoundProtocolVersionId { get; }

    public ContentDigest BoundProtocolContentDigest { get; }

    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("workflow_id", WorkflowId)
            .Add("workflow_definition_digest", WorkflowDefinitionDigest.ToString())
            .Add("template_id", TemplateId)
            .Add("template_version", TemplateVersion)
            .Add("template_digest", TemplateDigest.ToString())
            .Add("bound_protocol_version_id", BoundProtocolVersionId)
            .Add("bound_protocol_content_digest", BoundProtocolContentDigest.ToString());
    }
}

public sealed class BundleProvenanceBinding
{
    public BundleProvenanceBinding(
        string eventId,
        ContentDigest eventDigest,
        string activityKind,
        DateTimeOffset recordedAt,
        string actorId)
    {
        EventId = Guard.NotBlank(eventId, nameof(eventId));
        EventDigest = eventDigest;
        ActivityKind = Guard.NotBlank(activityKind, nameof(activityKind));
        RecordedAt = recordedAt;
        ActorId = Guard.NotBlank(actorId, nameof(actorId));
    }

    public static DigestScope EventDigestScope => DigestScope.ProvenanceEvent;

    public string EventId { get; }

    public ContentDigest EventDigest { get; }

    public string ActivityKind { get; }

    public DateTimeOffset RecordedAt { get; }

    public string ActorId { get; }

    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("event_id", EventId)
            .Add("event_digest", EventDigest.ToString())
            .Add("activity_kind", ActivityKind)
            .AddTimestamp("recorded_at", RecordedAt)
            .Add("actor_id", ActorId);
    }
}

public sealed class BundleArtifactEntry
{
    public BundleArtifactEntry(
        string artifactRef,
        string logicalPath,
        string artifactKind,
        string mediaType,
        long sizeBytes,
        ContentDigest rawByteDigest,
        string schemaId,
        string schemaVersion,
        ContentDigest? sourceRecordDigest = null,
        string? producedByWorkflowNode = null,
        string? provenanceEventId = null,
        ContentDigest? provenanceEventDigest = null,
        string? requiredFor = null)
        : this(
            artifactRef,
            logicalPath,
            artifactKind,
            mediaType,
            sizeBytes,
            rawByteDigest,
            schemaId,
            schemaVersion,
            sourceRecordDigest,
            producedByWorkflowNode,
            provenanceEventId,
            provenanceEventDigest,
            requiredFor,
            validate: true)
    {
    }

    private BundleArtifactEntry(
        string artifactRef,
        string logicalPath,
        string artifactKind,
        string mediaType,
        long sizeBytes,
        ContentDigest rawByteDigest,
        string schemaId,
        string schemaVersion,
        ContentDigest? sourceRecordDigest,
        string? producedByWorkflowNode,
        string? provenanceEventId,
        ContentDigest? provenanceEventDigest,
        string? requiredFor,
        bool validate)
    {
        ArtifactRef = Guard.NotBlank(artifactRef, nameof(artifactRef));
        LogicalPath = validate ? BundleArtifactPath.Normalize(logicalPath) : Guard.NotBlank(logicalPath, nameof(logicalPath)).Trim();
        ArtifactKind = Guard.NotBlank(artifactKind, nameof(artifactKind));
        MediaType = Guard.NotBlank(mediaType, nameof(mediaType));
        SizeBytes = validate && sizeBytes < 0
            ? throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Artifact size must be non-negative.")
            : sizeBytes;
        RawByteDigest = rawByteDigest;
        SchemaId = Guard.NotBlank(schemaId, nameof(schemaId));
        SchemaVersion = Guard.NotBlank(schemaVersion, nameof(schemaVersion));
        SourceRecordDigest = sourceRecordDigest;
        ProducedByWorkflowNode = NormalizeOptional(producedByWorkflowNode);
        ProvenanceEventId = NormalizeOptional(provenanceEventId);
        ProvenanceEventDigest = provenanceEventDigest;
        RequiredFor = NormalizeOptional(requiredFor);

        if (validate)
        {
            ValidateDigest(rawByteDigest, nameof(rawByteDigest));
        }
    }

    public static DigestScope RawByteDigestScope => DigestScope.RawArtifactBytes;

    public string ArtifactRef { get; }

    public string LogicalPath { get; }

    public string ArtifactKind { get; }

    public string MediaType { get; }

    public long SizeBytes { get; }

    public ContentDigest RawByteDigest { get; }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public ContentDigest? SourceRecordDigest { get; }

    public string? ProducedByWorkflowNode { get; }

    public string? ProvenanceEventId { get; }

    public ContentDigest? ProvenanceEventDigest { get; }

    public string? RequiredFor { get; }

    public static ContentDigest ComputeRawByteDigest(ReadOnlySpan<byte> contentBytes) =>
        ContentDigest.Sha256(contentBytes);

    internal static BundleArtifactEntry CreateUncheckedForVerification(
        string artifactRef,
        string logicalPath,
        string artifactKind,
        string mediaType,
        long sizeBytes,
        ContentDigest rawByteDigest,
        string schemaId,
        string schemaVersion)
    {
        return new BundleArtifactEntry(
            artifactRef,
            logicalPath,
            artifactKind,
            mediaType,
            sizeBytes,
            rawByteDigest,
            schemaId,
            schemaVersion,
            null,
            null,
            null,
            null,
            null,
            validate: false);
    }

    public CanonicalJsonObject ToCanonicalJson()
    {
        var value = new CanonicalJsonObject()
            .Add("artifact_ref", ArtifactRef)
            .Add("logical_path", LogicalPath)
            .Add("artifact_kind", ArtifactKind)
            .Add("media_type", MediaType)
            .Add("size_bytes", SizeBytes)
            .Add("raw_byte_digest", RawByteDigest.ToString())
            .Add("schema_id", SchemaId)
            .Add("schema_version", SchemaVersion);

        if (SourceRecordDigest is not null)
        {
            value.Add("source_record_digest", SourceRecordDigest.Value.ToString());
        }

        if (ProducedByWorkflowNode is not null)
        {
            value.Add("produced_by_workflow_node", ProducedByWorkflowNode);
        }

        if (ProvenanceEventId is not null)
        {
            value.Add("provenance_event_id", ProvenanceEventId);
        }

        if (ProvenanceEventDigest is not null)
        {
            value.Add("provenance_event_digest", ProvenanceEventDigest.Value.ToString());
        }

        if (RequiredFor is not null)
        {
            value.Add("required_for", RequiredFor);
        }

        return value;
    }

    internal static bool HasValidDigest(ContentDigest digest) =>
        digest.Algorithm == DigestAlgorithm.Sha256 &&
        !string.IsNullOrWhiteSpace(digest.Value) &&
        digest.Value.Length == 64;

    private static void ValidateDigest(ContentDigest digest, string parameterName)
    {
        if (!HasValidDigest(digest))
        {
            throw new ArgumentException("Artifact raw byte digest must be a canonical SHA-256 digest.", parameterName);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record BundleArtifact(string Path, string MediaType, long SizeBytes, ContentDigest Digest);

public sealed class BundleSharedIdentityMembership
{
    public BundleSharedIdentityMembership(ScholarlyWork work)
    {
        Work = work ?? throw new ArgumentNullException(nameof(work));
        if (!work.HasStableIdentifier)
        {
            throw new ArgumentException("Shared identity membership requires at least one stable identifier.", nameof(work));
        }
    }

    public ScholarlyWork Work { get; }

    public string SortKey
    {
        get
        {
            var primary = Work.PrimaryWorkId!.Value;
            return $"{WorkIdNamespace.Precedence(primary.Namespace):D4}:{primary}";
        }
    }

    public CanonicalJsonObject ToCanonicalJson()
    {
        var value = new CanonicalJsonObject()
            .Add("title", Work.Title)
            .Add("work_ids", CanonicalJsonValue.Array(Work.WorkIds.Ids
                .Select(id => CanonicalJsonValue.From(id.ToString()))
                .ToArray()));

        if (Work.SourceContext is not null)
        {
            value.Add("source_context", Work.SourceContext);
        }

        if (Work.IsRetracted)
        {
            value.Add("is_retracted", true);
        }

        return value;
    }
}

public sealed class BundleUnresolvedCandidate
{
    public BundleUnresolvedCandidate(ScholarlyWork work, string? stableCandidateId = null)
    {
        Work = work ?? throw new ArgumentNullException(nameof(work));
        if (!work.IsUnresolvedCandidate)
        {
            throw new ArgumentException("Unresolved candidates cannot have stable identifiers.", nameof(work));
        }

        StableCandidateId = string.IsNullOrWhiteSpace(stableCandidateId) ? null : stableCandidateId.Trim();
    }

    public ScholarlyWork Work { get; }

    public string? StableCandidateId { get; }

    public string SortKey =>
        StableCandidateId ?? ContentDigest.Sha256Utf8(Work.SourceContext ?? Work.Title).Value;

    public CanonicalJsonObject ToCanonicalJson()
    {
        var value = new CanonicalJsonObject()
            .Add("title", Work.Title)
            .Add("source_context", Work.SourceContext ?? string.Empty);

        if (Work.IsRetracted)
        {
            value.Add("is_retracted", true);
        }

        if (StableCandidateId is not null)
        {
            value.Add("stable_candidate_id", StableCandidateId);
        }

        return value;
    }
}

public sealed class ReviewBundleManifest
{
    private static readonly ReadOnlyCollection<string> RequiredSections = new(
        new[]
        {
            "manifest_identity",
            "protocol_binding",
            "artifacts",
            "required_schemas",
            "verification_policy"
        });

    private readonly IReadOnlyList<BundleArtifactEntry> _artifacts;
    private readonly IReadOnlyList<BundleSchemaRef> _requiredSchemas;
    private readonly IReadOnlyList<BundleProvenanceBinding> _provenanceBindings;
    private readonly IReadOnlyList<BundleSharedIdentityMembership> _sharedIdentityMembership;
    private readonly IReadOnlyList<BundleUnresolvedCandidate> _unresolvedCandidates;
    private readonly IReadOnlyList<string> _notes;

    public ReviewBundleManifest(
        string bundleId,
        string bundleKind,
        string schemaId,
        string schemaVersion,
        DateTimeOffset createdAt,
        string createdBy,
        BundleProtocolBinding protocolBinding,
        IEnumerable<BundleArtifactEntry> artifacts,
        IEnumerable<BundleSchemaRef> requiredSchemas,
        BundleWorkflowBinding? workflowBinding = null,
        IEnumerable<BundleProvenanceBinding>? provenanceBindings = null,
        IEnumerable<BundleSharedIdentityMembership>? sharedIdentityMembership = null,
        IEnumerable<BundleUnresolvedCandidate>? unresolvedCandidates = null,
        IEnumerable<string>? notes = null)
    {
        BundleId = Guard.NotBlank(bundleId, nameof(bundleId));
        BundleKind = Guard.NotBlank(bundleKind, nameof(bundleKind));
        SchemaId = Guard.NotBlank(schemaId, nameof(schemaId));
        SchemaVersion = Guard.NotBlank(schemaVersion, nameof(schemaVersion));
        CreatedAt = createdAt;
        CreatedBy = Guard.NotBlank(createdBy, nameof(createdBy));
        ProtocolBinding = protocolBinding ?? throw new ArgumentNullException(nameof(protocolBinding));
        WorkflowBinding = workflowBinding;

        _artifacts = SnapshotArtifacts(artifacts);
        _requiredSchemas = SnapshotSchemas(requiredSchemas);
        _provenanceBindings = SnapshotProvenanceBindings(provenanceBindings);
        _sharedIdentityMembership = SnapshotSharedIdentityMembership(sharedIdentityMembership);
        _unresolvedCandidates = SnapshotUnresolvedCandidates(unresolvedCandidates);
        _notes = SnapshotNotes(notes);
    }

    public ReviewBundleManifest(
        string bundleId,
        string createdBy,
        BundleProtocolBinding protocolBinding,
        IEnumerable<BundleArtifactEntry> artifacts,
        IEnumerable<BundleSchemaRef> requiredSchemas,
        DateTimeOffset createdAt,
        BundleWorkflowBinding? workflowBinding = null,
        IEnumerable<BundleProvenanceBinding>? provenanceBindings = null,
        IEnumerable<BundleSharedIdentityMembership>? sharedIdentityMembership = null,
        IEnumerable<BundleUnresolvedCandidate>? unresolvedCandidates = null,
        IEnumerable<string>? notes = null)
        : this(
            bundleId,
            BundleConstants.BundleKindReview,
            BundleConstants.ManifestSchemaId,
            BundleConstants.ManifestSchemaVersion,
            createdAt,
            createdBy,
            protocolBinding,
            artifacts,
            requiredSchemas,
            workflowBinding,
            provenanceBindings,
            sharedIdentityMembership,
            unresolvedCandidates,
            notes)
    {
    }

    public ReviewBundleManifest(
        string schemaVersion,
        string projectId,
        ContentDigest protocolDigest,
        string workflowId,
        DateTimeOffset createdAt,
        IEnumerable<BundleArtifact> artifacts)
        : this(
            $"legacy-{Guard.NotBlank(projectId, nameof(projectId))}",
            BundleConstants.BundleKindReview,
            BundleConstants.ManifestSchemaId,
            BundleConstants.ManifestSchemaVersion,
            createdAt,
            projectId,
            new BundleProtocolBinding(
                projectId,
                $"{projectId}-version",
                1,
                BundleConstants.ApprovedProtocolStatus,
                protocolDigest),
            ToLegacyEntries(artifacts),
            Array.Empty<BundleSchemaRef>(),
            string.IsNullOrWhiteSpace(workflowId)
                ? null
                : new BundleWorkflowBinding(
                    workflowId,
                    protocolDigest,
                    "legacy-template",
                    "1.0.0",
                    protocolDigest,
                    $"{projectId}-version",
                    protocolDigest))
    {
    }

    public string BundleId { get; }

    public string BundleKind { get; }

    public string SchemaId { get; }

    public string SchemaVersion { get; }

    public DateTimeOffset CreatedAt { get; }

    public string CreatedBy { get; }

    public BundleProtocolBinding ProtocolBinding { get; }

    public BundleWorkflowBinding? WorkflowBinding { get; }

    public IReadOnlyList<BundleArtifactEntry> Artifacts => _artifacts;

    public IReadOnlyList<BundleSchemaRef> RequiredSchemas => _requiredSchemas;

    public IReadOnlyList<BundleProvenanceBinding> ProvenanceBindings => _provenanceBindings;

    public IReadOnlyList<BundleSharedIdentityMembership> SharedIdentityMembership => _sharedIdentityMembership;

    public IReadOnlyList<BundleUnresolvedCandidate> UnresolvedCandidates => _unresolvedCandidates;

    public IReadOnlyList<string> Notes => _notes;

    public static IReadOnlyList<string> RequiredSectionNames => RequiredSections;

    public DigestEnvelope ToDigestEnvelope()
    {
        return new DigestEnvelope(
            DigestScope.BundleManifest,
            BundleConstants.ManifestSchemaId,
            BundleConstants.ManifestSchemaVersion,
            ToCanonicalJson());
    }

    public ContentDigest ComputeManifestDigest() => ToDigestEnvelope().ComputeDigest();

    public CanonicalJsonObject ToCanonicalJson()
    {
        var value = new CanonicalJsonObject()
            .Add("manifest_identity", new CanonicalJsonObject()
                .Add("bundle_id", BundleId)
                .Add("bundle_kind", BundleKind)
                .Add("schema_id", SchemaId)
                .Add("schema_version", SchemaVersion)
                .AddTimestamp("created_at", CreatedAt)
                .Add("created_by", CreatedBy))
            .Add("protocol_binding", ProtocolBinding.ToCanonicalJson())
            .Add("artifacts", CanonicalJsonValue.Array(Artifacts
                .Select(artifact => artifact.ToCanonicalJson())
                .ToArray()))
            .Add("required_schemas", CanonicalJsonValue.Array(RequiredSchemas
                .Select(schema => schema.ToCanonicalJson())
                .ToArray()))
            .Add("verification_policy", new CanonicalJsonObject()
                .Add("import_mode", "staged-all-or-nothing")
                .Add("destructive_overwrite", "reject"));

        if (WorkflowBinding is not null)
        {
            value.Add("workflow_binding", WorkflowBinding.ToCanonicalJson());
        }

        if (ProvenanceBindings.Count > 0)
        {
            value.Add("provenance_bindings", CanonicalJsonValue.Array(ProvenanceBindings
                .Select(binding => binding.ToCanonicalJson())
                .ToArray()));
        }

        if (SharedIdentityMembership.Count > 0)
        {
            value.Add("shared_identity_membership", CanonicalJsonValue.Array(SharedIdentityMembership
                .Select(membership => membership.ToCanonicalJson())
                .ToArray()));
        }

        if (UnresolvedCandidates.Count > 0)
        {
            value.Add("unresolved_candidates", CanonicalJsonValue.Array(UnresolvedCandidates
                .Select(candidate => candidate.ToCanonicalJson())
                .ToArray()));
        }

        if (Notes.Count > 0)
        {
            value.Add("notes", CanonicalJsonValue.Array(Notes
                .Select(CanonicalJsonValue.From)
                .ToArray()));
        }

        return value;
    }

    private static IReadOnlyList<BundleArtifactEntry> SnapshotArtifacts(IEnumerable<BundleArtifactEntry> artifacts)
    {
        return new ReadOnlyCollection<BundleArtifactEntry>((artifacts ?? throw new ArgumentNullException(nameof(artifacts)))
            .OrderBy(artifact => artifact.LogicalPath, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<BundleSchemaRef> SnapshotSchemas(IEnumerable<BundleSchemaRef> requiredSchemas)
    {
        return new ReadOnlyCollection<BundleSchemaRef>((requiredSchemas ?? throw new ArgumentNullException(nameof(requiredSchemas)))
            .Distinct()
            .OrderBy(schema => schema.SchemaId, StringComparer.Ordinal)
            .ThenBy(schema => schema.SchemaVersion, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<BundleProvenanceBinding> SnapshotProvenanceBindings(IEnumerable<BundleProvenanceBinding>? bindings)
    {
        return new ReadOnlyCollection<BundleProvenanceBinding>((bindings ?? Array.Empty<BundleProvenanceBinding>())
            .OrderBy(binding => binding.EventId, StringComparer.Ordinal)
            .ThenBy(binding => binding.EventDigest.Value, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<BundleSharedIdentityMembership> SnapshotSharedIdentityMembership(
        IEnumerable<BundleSharedIdentityMembership>? memberships)
    {
        return new ReadOnlyCollection<BundleSharedIdentityMembership>((memberships ?? Array.Empty<BundleSharedIdentityMembership>())
            .OrderBy(membership => membership.SortKey, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<BundleUnresolvedCandidate> SnapshotUnresolvedCandidates(
        IEnumerable<BundleUnresolvedCandidate>? candidates)
    {
        return new ReadOnlyCollection<BundleUnresolvedCandidate>((candidates ?? Array.Empty<BundleUnresolvedCandidate>())
            .OrderBy(candidate => candidate.SortKey, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<string> SnapshotNotes(IEnumerable<string>? notes)
    {
        return new ReadOnlyCollection<string>((notes ?? Array.Empty<string>())
            .Select(note => Guard.NotBlank(note, nameof(notes)))
            .ToArray());
    }

    private static IEnumerable<BundleArtifactEntry> ToLegacyEntries(IEnumerable<BundleArtifact> artifacts)
    {
        return (artifacts ?? Array.Empty<BundleArtifact>())
            .Select(artifact => BundleArtifactEntry.CreateUncheckedForVerification(
                artifact.Path,
                artifact.Path,
                "legacy",
                artifact.MediaType,
                artifact.SizeBytes,
                artifact.Digest,
                "nexus.legacy-artifact",
                "1.0.0"));
    }
}

public sealed record BundleVerificationFinding(string Category, string Message, string? Subject = null);

public sealed class BundleVerification
{
    public BundleVerification(
        bool isValid,
        IEnumerable<BundleVerificationFinding> errors,
        IEnumerable<BundleVerificationFinding> warnings,
        IEnumerable<BundleArtifactEntry> verifiedArtifacts,
        ContentDigest manifestDigest)
    {
        IsValid = isValid;
        Errors = new ReadOnlyCollection<BundleVerificationFinding>((errors ?? throw new ArgumentNullException(nameof(errors))).ToArray());
        Warnings = new ReadOnlyCollection<BundleVerificationFinding>((warnings ?? throw new ArgumentNullException(nameof(warnings))).ToArray());
        VerifiedArtifacts = new ReadOnlyCollection<BundleArtifactEntry>((verifiedArtifacts ?? throw new ArgumentNullException(nameof(verifiedArtifacts))).ToArray());
        ManifestDigest = manifestDigest;
    }

    public bool IsValid { get; }

    public IReadOnlyList<BundleVerificationFinding> Errors { get; }

    public IReadOnlyList<BundleVerificationFinding> Warnings { get; }

    public IReadOnlyList<BundleArtifactEntry> VerifiedArtifacts { get; }

    public ContentDigest ManifestDigest { get; }
}

public interface IBundleAuthorityResolver
{
    NexusScholar.Protocol.VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId);

    NexusScholar.Workflow.WorkflowDefinition ResolveWorkflowDefinition(string workflowId);

    NexusScholar.Provenance.ResearchEvent ResolveProvenanceEvent(string eventId);
}

public sealed record BundleVerificationOptions
{
    public bool RequireSupportedSchemas { get; init; } = true;

    public ContentDigest? ExpectedManifestDigest { get; init; }

    public IReadOnlyList<BundleSchemaRef> SupportedRequiredSchemas { get; init; } =
        Array.Empty<BundleSchemaRef>();

    public IReadOnlyDictionary<string, byte[]> ArtifactBytes { get; init; } =
        new ReadOnlyDictionary<string, byte[]>(new Dictionary<string, byte[]>(StringComparer.Ordinal));

    public IReadOnlyDictionary<string, ContentDigest> ExistingArtifactDigests { get; init; } =
        new ReadOnlyDictionary<string, ContentDigest>(new Dictionary<string, ContentDigest>(StringComparer.Ordinal));

    public IReadOnlyDictionary<string, ContentDigest> KnownProtocolContentDigests { get; init; } =
        new ReadOnlyDictionary<string, ContentDigest>(new Dictionary<string, ContentDigest>(StringComparer.Ordinal));

    public IReadOnlyDictionary<string, ContentDigest> KnownProvenanceEventDigests { get; init; } =
        new ReadOnlyDictionary<string, ContentDigest>(new Dictionary<string, ContentDigest>(StringComparer.Ordinal));

    public IBundleAuthorityResolver? AuthorityResolver { get; init; }
}
