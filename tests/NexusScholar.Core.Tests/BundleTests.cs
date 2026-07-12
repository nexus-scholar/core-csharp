using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Shared;
using NexusScholar.Workflow;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class BundleTests
{
    private const string ProtocolId = "protocol-1";
    private const string ProtocolVersionId = "protocol-version-1";

    private static readonly DateTimeOffset FixedTime = new(2026, 6, 27, 1, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Raw_artifact_byte_digest_uses_exact_bytes()
    {
        var lf = Encoding.UTF8.GetBytes("line\n");
        var crlf = Encoding.UTF8.GetBytes("line\r\n");

        Assert.AreEqual(DigestScope.RawArtifactBytes, BundleArtifactEntry.RawByteDigestScope);
        Assert.AreNotEqual(
            BundleArtifactEntry.ComputeRawByteDigest(lf),
            BundleArtifactEntry.ComputeRawByteDigest(crlf));
    }

    [TestMethod]
    public void Logical_path_validator_rejects_escape_and_platform_paths()
    {
        var invalidPaths = new[]
        {
            string.Empty,
            "/absolute/path.json",
            "C:/absolute/path.json",
            "file:///tmp/artifact.json",
            "https://example.test/artifact.json",
            "./artifacts/search-plan.json",
            "artifacts\\artifact.json",
            "artifacts/../artifact.json",
            "artifacts/./artifact.json",
            "artifacts/search-plan.json/.",
            "artifacts//artifact.json",
            "artifacts/"
        };

        foreach (var path in invalidPaths)
        {
            Assert.IsFalse(BundleArtifactPath.TryValidate(path, out _), path);
            Assert.ThrowsExactly<ArgumentException>(() => CreateArtifact(path: path), path);
        }

        Assert.AreEqual("artifacts/search-plan.json", BundleArtifactPath.Normalize(" artifacts/search-plan.json "));
    }

    [TestMethod]
    public void Bundle_manifest_digest_uses_bundle_manifest_scope()
    {
        var manifest = CreateManifest();
        var envelope = manifest.ToDigestEnvelope();

        Assert.AreEqual(DigestScope.BundleManifest, envelope.Scope);
        Assert.AreEqual(BundleConstants.ManifestSchemaId, envelope.SchemaId);
        Assert.AreEqual(BundleConstants.ManifestSchemaVersion, envelope.SchemaVersion);
        Assert.AreEqual(envelope.ComputeDigest(), manifest.ComputeManifestDigest());
        Assert.AreEqual(DigestScope.ProtocolContent, BundleProtocolBinding.ProtocolContentDigestScope);
        Assert.AreEqual(DigestScope.ProvenanceEvent, BundleProvenanceBinding.EventDigestScope);
    }

    [TestMethod]
    public void Manifest_digest_is_stable_despite_input_ordering()
    {
        var first = CreateManifest(
            artifacts: new[]
            {
                CreateArtifact("z-artifact", "artifacts/z.json", "z"),
                CreateArtifact("a-artifact", "artifacts/a.json", "a")
            },
            requiredSchemas: new[]
            {
                new BundleSchemaRef("z.schema", "1.0.0"),
                new BundleSchemaRef("a.schema", "1.0.0")
            },
            provenanceBindings: new[]
            {
                new BundleProvenanceBinding("event-z", ContentDigest.Sha256Utf8("event-z"), "z", FixedTime, "actor-1"),
                new BundleProvenanceBinding("event-a", ContentDigest.Sha256Utf8("event-a"), "a", FixedTime, "actor-1")
            },
            sharedIdentityMembership: new[]
            {
                new BundleSharedIdentityMembership(ScholarlyWork.Identified("Second", WorkIdSet.From(WorkId.From("s2", "S2-2")))),
                new BundleSharedIdentityMembership(ScholarlyWork.Identified("First", WorkIdSet.From(WorkId.From("doi", "10.1000/XYZ"))))
            },
            unresolvedCandidates: new[]
            {
                new BundleUnresolvedCandidate(ScholarlyWork.UnresolvedCandidate("Candidate B", "import:row-b"), "candidate-b"),
                new BundleUnresolvedCandidate(ScholarlyWork.UnresolvedCandidate("Candidate A", "import:row-a"), "candidate-a")
            });
        var second = CreateManifest(
            artifacts: first.Artifacts.Reverse(),
            requiredSchemas: first.RequiredSchemas.Reverse(),
            provenanceBindings: first.ProvenanceBindings.Reverse(),
            sharedIdentityMembership: first.SharedIdentityMembership.Reverse(),
            unresolvedCandidates: first.UnresolvedCandidates.Reverse());

        Assert.AreEqual(first.ComputeManifestDigest(), second.ComputeManifestDigest());
        CollectionAssert.AreEqual(
            new[] { "artifacts/a.json", "artifacts/z.json" },
            first.Artifacts.Select(artifact => artifact.LogicalPath).ToArray());
    }

    [TestMethod]
    public void Manifest_digest_changes_when_authoritative_content_changes()
    {
        var first = CreateManifest();
        var changed = CreateManifest(artifacts: new[] { CreateArtifact(content: "changed") });

        Assert.AreNotEqual(first.ComputeManifestDigest(), changed.ComputeManifestDigest());
    }

    [TestMethod]
    public void Verifier_accepts_valid_manifest_and_preserves_immutable_result()
    {
        var artifact = CreateArtifact();
        var manifest = CreateManifest(artifacts: new[] { artifact });
        var verification = new BundleVerifier().Verify(manifest, CreateOptions(artifact, ArtifactBytes()));

        Assert.IsTrue(verification.IsValid);
        Assert.AreEqual(artifact.RawByteDigest, verification.VerifiedArtifacts[0].RawByteDigest);
        Assert.IsFalse(verification.Errors is BundleVerificationFinding[]);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<BundleVerificationFinding>)verification.Errors).Add(new BundleVerificationFinding("x", "x")));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<BundleArtifactEntry>)verification.VerifiedArtifacts)[0] = artifact);
    }

    [TestMethod]
    public void Verifier_rejects_duplicate_logical_paths()
    {
        var artifact = CreateArtifact();
        var duplicate = CreateArtifact(artifactRef: "artifact-2", path: " artifacts/search-plan.json ");
        var result = new BundleVerifier().Verify(
            CreateManifest(artifacts: new[] { artifact, duplicate }),
            CreateOptions(artifact, ArtifactBytes()));

        AssertHasCategory(result, BundleErrorCodes.DuplicateArtifactPath);
    }

    [TestMethod]
    public void Verifier_rejects_unapproved_unknown_or_stale_protocol_binding_digest()
    {
        var actualVersion = ApprovedProtocolVersion();
        var actualDigest = actualVersion.ToProtocolContentDigestEnvelope().ComputeDigest();

        Assert.AreEqual(DigestScope.ProtocolContent, actualVersion.ToProtocolContentDigestEnvelope().Scope);
        Assert.AreEqual(ProtocolDigest(), actualDigest);

        var unapproved = new BundleVerifier().Verify(
            CreateManifest(protocolStatus: "draft"),
            CreateOptions(CreateArtifact(), ArtifactBytes()));
        var unknown = new BundleVerifier().Verify(
            CreateManifest(),
            CreateOptions(CreateArtifact(), ArtifactBytes()) with
            {
                AuthorityResolver = new TestBundleAuthorityResolver()
            });
        var stale = new BundleVerifier().Verify(
            CreateManifest(protocolContentDigest: ContentDigest.Sha256Utf8("unscoped-protocol-payload")),
            CreateOptions(CreateArtifact(), ArtifactBytes()));

        AssertHasCategory(unapproved, BundleErrorCodes.InvalidProtocolBinding);
        AssertHasCategory(unknown, BundleErrorCodes.InvalidProtocolBinding);
        AssertHasCategory(stale, BundleErrorCodes.InvalidProtocolBinding);
    }

    [TestMethod]
    public void Verifier_rejects_negative_size_and_invalid_digest()
    {
        var bytes = ArtifactBytes();
        var digest = BundleArtifactEntry.ComputeRawByteDigest(bytes);
        var negative = new ReviewBundleManifest(
            "1.0.0",
            "project-1",
            ProtocolDigest(),
            "workflow-1",
            FixedTime,
            new[] { new BundleArtifact("artifacts/search-plan.json", "application/json", -1, digest) });
        var invalidDigest = new ReviewBundleManifest(
            "1.0.0",
            "project-1",
            ProtocolDigest(),
            "workflow-1",
            FixedTime,
            new[] { new BundleArtifact("artifacts/search-plan.json", "application/json", bytes.Length, default) });

        AssertHasCategory(
            new BundleVerifier().Verify(negative, CreateOptions("artifacts/search-plan.json", bytes)),
            BundleErrorCodes.NegativeArtifactSize);
        AssertHasCategory(
            new BundleVerifier().Verify(invalidDigest, CreateOptions("artifacts/search-plan.json", bytes)),
            BundleErrorCodes.InvalidArtifactDigest);
    }

    [TestMethod]
    public void Verifier_rejects_invalid_workflow_digests_without_valid_result()
    {
        var artifact = CreateArtifact();
        var invalidDefinition = new BundleWorkflowBinding(
            "workflow-1",
            default,
            "template-1",
            "1.0.0",
            ContentDigest.Sha256Utf8("template"),
            ProtocolVersionId,
            ProtocolDigest());
        var invalidTemplate = new BundleWorkflowBinding(
            "workflow-1",
            ContentDigest.Sha256Utf8("workflow"),
            "template-1",
            "1.0.0",
            default,
            ProtocolVersionId,
            ProtocolDigest());

        foreach (var binding in new[] { invalidDefinition, invalidTemplate })
        {
            var result = new BundleVerifier().Verify(
                CreateManifest(artifacts: new[] { artifact }, workflowBinding: binding),
                CreateOptions(artifact, ArtifactBytes()));

            Assert.IsFalse(result.IsValid);
            Assert.IsFalse(result.ManifestDigest.IsValid);
            AssertHasCategory(result, BundleErrorCodes.InvalidWorkflowBinding);
        }
    }

    [TestMethod]
    public void Verifier_invalid_bound_protocol_digest_returns_finding_not_exception()
    {
        var artifact = CreateArtifact();
        var binding = new BundleWorkflowBinding(
            "workflow-1",
            ContentDigest.Sha256Utf8("workflow"),
            "template-1",
            "1.0.0",
            ContentDigest.Sha256Utf8("template"),
            ProtocolVersionId,
            default);

        var result = new BundleVerifier().Verify(
            CreateManifest(artifacts: new[] { artifact }, workflowBinding: binding),
            CreateOptions(artifact, ArtifactBytes()));

        Assert.IsFalse(result.IsValid);
        Assert.IsFalse(result.ManifestDigest.IsValid);
        AssertHasCategory(result, BundleErrorCodes.InvalidWorkflowBinding);
    }

    [TestMethod]
    public void Verifier_invalid_artifact_source_record_digest_returns_structured_finding()
    {
        var bytes = ArtifactBytes();
        var artifact = new BundleArtifactEntry(
            "search-plan",
            "artifacts/search-plan.json",
            "workflow-artifact",
            "application/json",
            bytes.Length,
            BundleArtifactEntry.ComputeRawByteDigest(bytes),
            "nexus.workflow.artifact",
            "1.0.0",
            sourceRecordDigest: default(ContentDigest));

        var result = new BundleVerifier().Verify(
            CreateManifest(artifacts: new[] { artifact }),
            CreateOptions(artifact, bytes));

        Assert.IsFalse(result.IsValid);
        Assert.IsFalse(result.ManifestDigest.IsValid);
        Assert.AreEqual(0, result.VerifiedArtifacts.Count);
        AssertHasCategory(result, BundleErrorCodes.InvalidArtifactDigest);
    }

    [TestMethod]
    public void Verifier_invalid_artifact_provenance_digest_does_not_verify_artifact()
    {
        var bytes = ArtifactBytes();
        var artifact = new BundleArtifactEntry(
            "search-plan",
            "artifacts/search-plan.json",
            "workflow-artifact",
            "application/json",
            bytes.Length,
            BundleArtifactEntry.ComputeRawByteDigest(bytes),
            "nexus.workflow.artifact",
            "1.0.0",
            provenanceEventId: "event-1",
            provenanceEventDigest: default(ContentDigest));

        var result = new BundleVerifier().Verify(
            CreateManifest(artifacts: new[] { artifact }),
            CreateOptions(artifact, bytes));

        Assert.IsFalse(result.IsValid);
        Assert.IsFalse(result.ManifestDigest.IsValid);
        Assert.AreEqual(0, result.VerifiedArtifacts.Count);
        AssertHasCategory(result, BundleErrorCodes.InvalidProvenanceBinding);
    }

    [TestMethod]
    public void Verifier_invalid_existing_artifact_digest_blocks_overwrite_verification()
    {
        var artifact = CreateArtifact();
        var options = CreateOptions(artifact, ArtifactBytes()) with
        {
            ExistingArtifactDigests = new Dictionary<string, ContentDigest>(StringComparer.Ordinal)
            {
                [artifact.LogicalPath] = default
            }
        };

        var result = new BundleVerifier().Verify(
            CreateManifest(artifacts: new[] { artifact }),
            options);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(0, result.VerifiedArtifacts.Count);
        AssertHasCategory(result, BundleErrorCodes.DestructiveOverwrite);
    }

    [TestMethod]
    public void Verifier_rejects_invalid_path_missing_artifact_checksum_mismatch_and_size_mismatch()
    {
        var bytes = ArtifactBytes();
        var validDigest = BundleArtifactEntry.ComputeRawByteDigest(bytes);
        var invalidPath = new ReviewBundleManifest(
            "1.0.0",
            "project-1",
            ProtocolDigest(),
            "workflow-1",
            FixedTime,
            new[] { new BundleArtifact("/escape.json", "application/json", bytes.Length, validDigest) });
        var missing = CreateManifest();
        var checksumMismatchArtifact = CreateArtifact();
        var checksumMismatch = CreateManifest(artifacts: new[] { checksumMismatchArtifact });
        var sizeMismatchArtifact = CreateArtifact(sizeBytes: bytes.Length + 1, rawByteDigest: validDigest);
        var sizeMismatch = CreateManifest(artifacts: new[] { sizeMismatchArtifact });

        AssertHasCategory(
            new BundleVerifier().Verify(invalidPath, CreateOptions("/escape.json", bytes)),
            BundleErrorCodes.InvalidArtifactPath);
        AssertHasCategory(new BundleVerifier().Verify(missing), BundleErrorCodes.MissingArtifact);
        AssertHasCategory(
            new BundleVerifier().Verify(checksumMismatch, CreateOptions(checksumMismatchArtifact, Encoding.UTF8.GetBytes("different"))),
            BundleErrorCodes.ChecksumMismatch);
        AssertHasCategory(
            new BundleVerifier().Verify(sizeMismatch, CreateOptions(sizeMismatchArtifact, bytes)),
            BundleErrorCodes.ArtifactSizeMismatch);
    }

    [TestMethod]
    public void Verifier_rejects_unsupported_schema_stale_digest_workflow_mismatch_provenance_mismatch_and_overwrite()
    {
        var artifact = CreateArtifact();
        var manifest = CreateManifest(
            artifacts: new[] { artifact },
            requiredSchemas: new[] { new BundleSchemaRef("unsupported.schema", "1.0.0") });
        var staleDigest = ContentDigest.Sha256Utf8("stale");
        var workflowVersionMismatch = CreateManifest(
            workflowBinding: new BundleWorkflowBinding(
                "workflow-1",
                ContentDigest.Sha256Utf8("workflow"),
                "template-1",
                "1.0.0",
                ContentDigest.Sha256Utf8("template"),
                "wrong-protocol-version",
                ProtocolDigest()));
        var workflowDigestMismatch = CreateManifest(
            workflowBinding: new BundleWorkflowBinding(
                "workflow-1",
                ContentDigest.Sha256Utf8("workflow"),
                "template-1",
                "1.0.0",
                ContentDigest.Sha256Utf8("template"),
                ProtocolVersionId,
                ContentDigest.Sha256Utf8("wrong-protocol-content")));
        var eventRecord = CreateProvenanceEvent();
        var provenanceMismatch = CreateManifest(
            provenanceBindings: new[]
            {
                new BundleProvenanceBinding(eventRecord.EventId.ToString(), ContentDigest.Sha256Utf8("actual"), "activity", FixedTime, "actor-1")
            });
        var provenanceUnknown = CreateManifest(
            provenanceBindings: new[] { CreateProvenanceBinding(eventRecord) });

        var unsupportedResult = new BundleVerifier().Verify(manifest, CreateOptions(artifact, ArtifactBytes()));
        var staleResult = new BundleVerifier().Verify(CreateManifest(), CreateOptions(CreateArtifact(), ArtifactBytes()) with { ExpectedManifestDigest = staleDigest });
        var workflowVersionResult = new BundleVerifier().Verify(workflowVersionMismatch, CreateOptions(CreateArtifact(), ArtifactBytes()));
        var workflowDigestResult = new BundleVerifier().Verify(workflowDigestMismatch, CreateOptions(CreateArtifact(), ArtifactBytes()));
        var provenanceResult = new BundleVerifier().Verify(
            provenanceMismatch,
            CreateOptions(CreateArtifact(), ArtifactBytes()) with
            {
                KnownProvenanceEventDigests = new Dictionary<string, ContentDigest>(StringComparer.Ordinal)
                {
                    [eventRecord.EventId.ToString()] = eventRecord.ToDigestEnvelope().ComputeDigest()
                }
            });
        var provenanceUnknownResult = new BundleVerifier().Verify(
            provenanceUnknown,
            CreateOptions(CreateArtifact(), ArtifactBytes()));
        var overwriteResult = new BundleVerifier().Verify(
            CreateManifest(),
            CreateOptions(CreateArtifact(), ArtifactBytes()) with
            {
                ExistingArtifactDigests = new Dictionary<string, ContentDigest>(StringComparer.Ordinal)
                {
                    ["artifacts/search-plan.json"] = ContentDigest.Sha256Utf8("existing")
                }
            });

        AssertHasCategory(unsupportedResult, BundleErrorCodes.UnsupportedRequiredSchema);
        AssertHasCategory(staleResult, BundleErrorCodes.StaleManifestDigest);
        AssertHasCategory(workflowVersionResult, BundleErrorCodes.InvalidWorkflowBinding);
        AssertHasCategory(workflowDigestResult, BundleErrorCodes.InvalidWorkflowBinding);
        AssertHasCategory(provenanceResult, BundleErrorCodes.InvalidProvenanceBinding);
        AssertHasCategory(provenanceUnknownResult, BundleErrorCodes.InvalidProvenanceBinding);
        AssertHasCategory(overwriteResult, BundleErrorCodes.DestructiveOverwrite);
    }

    [TestMethod]
    public void Verifier_preserves_manifest_digest_when_tamper_or_verification_reports_change()
    {
        var artifact = CreateArtifact();
        var manifest = CreateManifest(artifacts: new[] { artifact });
        var valid = new BundleVerifier().Verify(manifest, CreateOptions(artifact, ArtifactBytes()));
        var stale = new BundleVerifier().Verify(
            manifest,
            CreateOptions(artifact, ArtifactBytes()) with { ExpectedManifestDigest = ContentDigest.Sha256Utf8("stale") });
        var tampered = new BundleVerifier().Verify(
            manifest,
            CreateOptions(artifact, Encoding.UTF8.GetBytes("different bytes")));

        Assert.IsTrue(valid.IsValid);
        AssertHasCategory(stale, BundleErrorCodes.StaleManifestDigest);
        AssertHasCategory(tampered, BundleErrorCodes.ChecksumMismatch);
        Assert.AreEqual(valid.ManifestDigest, stale.ManifestDigest);
        Assert.AreEqual(valid.ManifestDigest, tampered.ManifestDigest);
    }

    [TestMethod]
    public void Roundtrip_local_equality_uses_manifest_digest_and_no_id_candidates_are_not_membership_identity()
    {
        var artifact = CreateArtifact();
        var candidate = ScholarlyWork.UnresolvedCandidate("No id", "import:row-1");
        var first = CreateManifest(
            artifacts: new[] { artifact },
            unresolvedCandidates: new[] { new BundleUnresolvedCandidate(candidate, "candidate-1") });
        var roundTrip = CreateManifest(
            artifacts: first.Artifacts.Reverse(),
            unresolvedCandidates: first.UnresolvedCandidates.Reverse());

        Assert.AreEqual(first.ComputeManifestDigest(), roundTrip.ComputeManifestDigest());
        Assert.ThrowsExactly<ArgumentException>(() => new BundleSharedIdentityMembership(candidate));
    }

    [TestMethod]
    public void Verifier_requires_resolved_full_authority_and_artifact_schema_closure()
    {
        var artifact = CreateArtifact();
        var protocolAuthority = VerifiedProtocolAuthority();
        var eventRecord = CreateProvenanceEvent();
        var workflowBinding = new BundleWorkflowBinding(
            "workflow-1", ContentDigest.Sha256Utf8("workflow-definition"), "template-1", "1.0.0",
            ContentDigest.Sha256Utf8("template"), ProtocolVersionId, ProtocolDigest());
        var workflowAuthority = CreateVerifiedWorkflowAuthority(workflowBinding, protocolAuthority);
        var resolver = new TestBundleAuthorityResolver(protocolAuthority, workflowAuthority, eventRecord);
        var manifest = CreateManifest(
            artifacts: new[] { artifact },
            workflowBinding: workflowBinding,
            provenanceBindings: new[] { CreateProvenanceBinding(eventRecord) });
        var options = CreateOptions(artifact, ArtifactBytes()) with { AuthorityResolver = resolver };

        Assert.IsTrue(new BundleVerifier().Verify(manifest, options).IsValid);

        var wrongProtocol = new ReviewBundleManifest(
            manifest.BundleId, manifest.CreatedBy,
            new BundleProtocolBinding("other-protocol", ProtocolVersionId, 1, BundleConstants.ApprovedProtocolStatus, ProtocolDigest()),
            manifest.Artifacts, manifest.RequiredSchemas, manifest.CreatedAt, manifest.WorkflowBinding, manifest.ProvenanceBindings);
        AssertHasCategory(new BundleVerifier().Verify(wrongProtocol, options), BundleErrorCodes.InvalidProtocolBinding);

        var undeclaredSchema = CreateManifest(artifacts: new[] { artifact }, requiredSchemas: Array.Empty<BundleSchemaRef>());
        AssertHasCategory(new BundleVerifier().Verify(undeclaredSchema, options), BundleErrorCodes.UnsupportedRequiredSchema);
    }

    [TestMethod]
    public void Verifier_rejects_incomplete_artifact_provenance_pair()
    {
        var bytes = ArtifactBytes();
        var artifact = new BundleArtifactEntry(
            "search-plan", "artifacts/search-plan.json", "workflow-artifact", "application/json", bytes.Length,
            BundleArtifactEntry.ComputeRawByteDigest(bytes), "nexus.workflow.artifact", "1.0.0",
            provenanceEventId: "event-without-digest");

        var result = new BundleVerifier().Verify(CreateManifest(artifacts: new[] { artifact }), CreateOptions(artifact, bytes));

        AssertHasCategory(result, BundleErrorCodes.InvalidProvenanceBinding);
        Assert.AreEqual(0, result.VerifiedArtifacts.Count);
    }

    private static byte[] ArtifactBytes(string value = "{\"query\":\"nexus scholar\"}\n") =>
        Encoding.UTF8.GetBytes(value);

    private static ContentDigest ProtocolDigest() => ApprovedProtocolVersion().ToProtocolContentDigestEnvelope().ComputeDigest();

    private static BundleArtifactEntry CreateArtifact(
        string artifactRef = "search-plan",
        string path = "artifacts/search-plan.json",
        string content = "{\"query\":\"nexus scholar\"}\n",
        long? sizeBytes = null,
        ContentDigest? rawByteDigest = null)
    {
        var bytes = ArtifactBytes(content);
        return new BundleArtifactEntry(
            artifactRef,
            path,
            "workflow-artifact",
            "application/json",
            sizeBytes ?? bytes.Length,
            rawByteDigest ?? BundleArtifactEntry.ComputeRawByteDigest(bytes),
            "nexus.workflow.artifact",
            "1.0.0",
            requiredFor: "workflow");
    }

    private static ReviewBundleManifest CreateManifest(
        IEnumerable<BundleArtifactEntry>? artifacts = null,
        IEnumerable<BundleSchemaRef>? requiredSchemas = null,
        BundleWorkflowBinding? workflowBinding = null,
        IEnumerable<BundleProvenanceBinding>? provenanceBindings = null,
        IEnumerable<BundleSharedIdentityMembership>? sharedIdentityMembership = null,
        IEnumerable<BundleUnresolvedCandidate>? unresolvedCandidates = null,
        string protocolStatus = BundleConstants.ApprovedProtocolStatus,
        ContentDigest? protocolContentDigest = null)
    {
        return new ReviewBundleManifest(
            "bundle-1",
            "researcher-1",
            new BundleProtocolBinding(
                ProtocolId,
                ProtocolVersionId,
                1,
                protocolStatus,
                protocolContentDigest ?? ProtocolDigest()),
            artifacts ?? new[] { CreateArtifact() },
            requiredSchemas ?? new[] { new BundleSchemaRef("nexus.workflow.artifact", "1.0.0") },
            FixedTime,
            workflowBinding,
            provenanceBindings,
            sharedIdentityMembership,
            unresolvedCandidates);
    }

    private static BundleVerificationOptions CreateOptions(BundleArtifactEntry artifact, byte[] bytes)
    {
        return CreateOptions(artifact.LogicalPath, bytes) with
        {
            SupportedRequiredSchemas = new[] { new BundleSchemaRef("nexus.workflow.artifact", "1.0.0") }
        };
    }

    private static BundleVerificationOptions CreateOptions(string logicalPath, byte[] bytes)
    {
        return new BundleVerificationOptions
        {
            SupportedRequiredSchemas = new[] { new BundleSchemaRef("nexus.workflow.artifact", "1.0.0") },
            KnownProtocolContentDigests = KnownProtocolContentDigests(),
            AuthorityResolver = new TestBundleAuthorityResolver(VerifiedProtocolAuthority()),
            ArtifactBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [logicalPath] = bytes
            }
        };
    }

    private static Dictionary<string, ContentDigest> KnownProtocolContentDigests()
    {
        var protocolDigest = ProtocolDigest();
        return new Dictionary<string, ContentDigest>(StringComparer.Ordinal)
        {
            [ProtocolVersionId] = protocolDigest,
            ["project-1-version"] = protocolDigest
        };
    }

    private static ProtocolVersion ApprovedProtocolVersion()
    {
        var seed = CreateProtocolVersion(ContentDigest.Sha256Utf8("placeholder-protocol-content"));
        return CreateProtocolVersion(seed.ToProtocolContentDigestEnvelope().ComputeDigest());
    }

    private static VerifiedProtocolVersion VerifiedProtocolAuthority()
    {
        var version = ApprovedProtocolVersion();
        return new VerifiedProtocolVersion(
            version,
            ApprovalPolicy.ExplicitCustomSingleResearcher(),
            Array.Empty<VerifiedProtocolApproval>());
    }

    private static WorkflowDefinition CreateVerifiedWorkflowAuthority(
        BundleWorkflowBinding binding,
        VerifiedProtocolVersion protocol)
    {
        var definitionConstructor = typeof(WorkflowDefinition).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var definition = (WorkflowDefinition)definitionConstructor.Invoke(new object[]
        {
            binding.WorkflowId, binding.WorkflowDefinitionDigest, "compiler", "1.0.0", protocol.Version.ProtocolId,
            protocol.Version.Id, protocol.Version.VersionNumber, protocol.Version.ContentDigest,
            binding.TemplateId, binding.TemplateVersion, binding.TemplateDigest,
            Array.Empty<WorkflowResolvedInputBinding>(), Array.Empty<WorkflowCompiledNode>(), Array.Empty<WorkflowCompiledEdge>(),
            Array.Empty<WorkflowCompiledApprovalRequirement>(), Array.Empty<WorkflowCompiledCapabilityRequirement>(),
            Array.Empty<WorkflowCompiledArtifactDeclaration>(), Array.Empty<WorkflowInvalidationPlanEntry>()
        });
        return definition;
    }

    private static ProtocolVersion CreateProtocolVersion(ContentDigest contentDigest)
    {
        return new ProtocolVersion(
            ProtocolVersionId,
            ProtocolId,
            "project-1",
            1,
            ProtocolStatus.Approved,
            new ProtocolTemplate(
                "template-systematic-review",
                "1.0.0",
                ContentDigest.Sha256Utf8("template-systematic-review@1.0.0")),
            new ProtocolIntent(
                "tomato disease screening",
                "map the evidence for segmentation workflows",
                "scoping-review"),
            new CanonicalJsonObject().Add("review_family", "scoping"),
            Array.Empty<RequiredDecisionDefinition>(),
            Array.Empty<ProtocolDecision>(),
            Array.Empty<ProtocolWaiver>(),
            contentDigest,
            ApprovalPolicy.ExplicitCustomSingleResearcher().PolicyId,
            new[] { "approval-1" },
            FixedTime);
    }

    private static ResearchEvent CreateProvenanceEvent()
    {
        return ResearchEventFactory.Create(
            new FixedIdGenerator(Guid.Parse("00000000-0000-0000-0000-000000000601")),
            new FixedClock(),
            new ProvenanceActivity("workflow-node-completed", "Workflow node completed", false, false, false),
            new ProvenanceEntityRef("workflow", "workflow-1"),
            new ProvenanceAgent("researcher-1", "human"));
    }

    private static BundleProvenanceBinding CreateProvenanceBinding(ResearchEvent? record = null)
    {
        record ??= CreateProvenanceEvent();
        return new BundleProvenanceBinding(
            record.EventId.ToString(),
            record.ToDigestEnvelope().ComputeDigest(),
            record.Activity.ActivityId,
            record.OccurredAt,
            record.Agent.AgentId);
    }

    private static void AssertHasCategory(BundleVerification verification, string category)
    {
        Assert.IsTrue(
            verification.Errors.Any(error => string.Equals(error.Category, category, StringComparison.Ordinal)),
            $"Expected error category '{category}' but saw: {string.Join(", ", verification.Errors.Select(error => error.Category))}");
    }

    private sealed class TestBundleAuthorityResolver : IBundleAuthorityResolver
    {
        private readonly VerifiedProtocolVersion? _protocol;
        private readonly WorkflowDefinition? _workflow;
        private readonly ResearchEvent? _event;

        public TestBundleAuthorityResolver(
            VerifiedProtocolVersion? protocol = null,
            WorkflowDefinition? workflow = null,
            ResearchEvent? @event = null)
        {
            _protocol = protocol;
            _workflow = workflow;
            _event = @event;
        }

        public VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId) =>
            string.Equals(protocolVersionId, _protocol?.Version.Id, StringComparison.Ordinal) ? _protocol! : null!;

        public WorkflowDefinition ResolveWorkflowDefinition(string workflowId) =>
            string.Equals(workflowId, _workflow?.WorkflowId, StringComparison.Ordinal) ? _workflow! : null!;

        public ResearchEvent ResolveProvenanceEvent(string eventId) =>
            string.Equals(eventId, _event?.EventId.ToString(), StringComparison.Ordinal) ? _event! : null!;
    }

    private sealed class FixedIdGenerator : IIdGenerator
    {
        private readonly Guid _id;

        public FixedIdGenerator(Guid id)
        {
            _id = id;
        }

        public Guid NewId() => _id;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => FixedTime;
    }
}
