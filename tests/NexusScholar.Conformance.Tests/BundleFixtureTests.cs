using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Workflow;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class BundleFixtureTests
{
    private const string FixtureSourceKind = "local-gate-6-contract";
    private const string ProtocolId = "protocol-1";
    private const string ProtocolVersionId = "protocol-version-1";
    private static readonly DateTimeOffset FixedTime = new(2026, 6, 27, 1, 0, 0, TimeSpan.Zero);

    private static readonly string[] RequiredArtifactFixtureIds =
    {
        "artifact-raw-byte-digest",
        "artifact-manifest-entry",
        "artifact-invalid-digest",
        "artifact-negative-size",
        "artifact-forbidden-path-absolute",
        "artifact-forbidden-path-traversal"
    };

    private static readonly string[] RequiredBundleFixtureIds =
    {
        "bundle-manifest-minimal",
        "bundle-manifest-with-protocol-workflow-provenance",
        "bundle-manifest-digest-stable",
        "bundle-roundtrip-local-equivalence",
        "bundle-duplicate-artifact-path",
        "bundle-missing-artifact",
        "bundle-checksum-mismatch",
        "bundle-unsupported-required-schema",
        "bundle-stale-manifest-digest",
        "bundle-destructive-overwrite-reject"
    };

    [TestMethod]
    public void Gate_6_bundle_and_artifact_fixtures_are_present()
    {
        AssertFixtureSet(ArtifactFixtureDirectory(), RequiredArtifactFixtureIds);
        AssertFixtureSet(BundleFixtureDirectory(), RequiredBundleFixtureIds);
    }

    [TestMethod]
    public void Gate_6_fixtures_have_required_local_metadata_and_non_claims()
    {
        foreach (var path in Directory.GetFiles(ArtifactFixtureDirectory(), "*.json")
                     .Concat(Directory.GetFiles(BundleFixtureDirectory(), "*.json")))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var fixtureId = root.GetProperty("fixtureId").GetString();

            Assert.AreEqual(FixtureSourceKind, root.GetProperty("sourceKind").GetString(), fixtureId);
            Assert.AreEqual("hand-authored local Gate 6 bundle fixture", root.GetProperty("generatorCommand").GetString(), fixtureId);
            Assert.AreEqual("gate-6-v1", root.GetProperty("generatorVersion").GetString(), fixtureId);
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0009-portable-bundle-and-artifact-contract.md", StringComparison.Ordinal)), fixtureId);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)), fixtureId);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "no-blueprint-conformance-claim", StringComparison.Ordinal)), fixtureId);
            var inputDigest = ContentDigest.Parse(root.GetProperty("inputDigest").GetString()!);
            var outputDigest = ContentDigest.Parse(root.GetProperty("outputDigest").GetString()!);

            AssertNotPlaceholderDigest(inputDigest, fixtureId!);
            AssertNotPlaceholderDigest(outputDigest, fixtureId!);
            Assert.AreEqual(
                ContentDigest.Sha256CanonicalJson(CanonicalJsonValue.FromJsonElement(root.GetProperty("case"))),
                inputDigest,
                fixtureId);
            Assert.AreEqual(ExpectedFixtureOutputDigest(root), outputDigest, fixtureId);
        }
    }

    [TestMethod]
    public void Positive_gate_6_fixtures_replay_local_contract()
    {
        using (var rawDigest = LoadFixture(ArtifactFixtureDirectory(), "artifact-raw-byte-digest.json"))
        {
            var root = rawDigest.RootElement;
            var bytes = Encoding.UTF8.GetBytes(root.GetProperty("case").GetProperty("bytesUtf8").GetString()!);
            Assert.AreEqual(
                root.GetProperty("case").GetProperty("expectedDigest").GetString(),
                BundleArtifactEntry.ComputeRawByteDigest(bytes).ToString());
        }

        using (var entryFixture = LoadFixture(ArtifactFixtureDirectory(), "artifact-manifest-entry.json"))
        {
            var entry = CreateArtifact();
            var fixtureCase = entryFixture.RootElement.GetProperty("case");
            Assert.AreEqual(fixtureCase.GetProperty("logicalPath").GetString(), entry.LogicalPath);
            Assert.AreEqual(fixtureCase.GetProperty("digestScope").GetString(), BundleArtifactEntry.RawByteDigestScope.Value);
            Assert.AreEqual(fixtureCase.GetProperty("expectedRawByteDigest").GetString(), entry.RawByteDigest.ToString());
        }

        foreach (var fixtureId in new[]
                 {
                     "bundle-manifest-minimal",
                     "bundle-manifest-with-protocol-workflow-provenance",
                     "bundle-manifest-digest-stable",
                     "bundle-roundtrip-local-equivalence"
                 })
        {
            using var document = LoadFixture(BundleFixtureDirectory(), $"{fixtureId}.json");
            var options = CreateOptions(CreateArtifact(), ArtifactBytes());
            ReviewBundleManifest manifest;
            if (fixtureId == "bundle-manifest-with-protocol-workflow-provenance")
            {
                var eventRecord = CreateProvenanceEvent();
                manifest = CreateManifest(
                    workflowBinding: CreateWorkflowBinding(),
                    provenanceBindings: new[] { CreateProvenanceBinding(eventRecord) });
                options = options with
                {
                    AuthorityResolver = new ReplayBundleAuthorityResolver(
                        VerifiedProtocolAuthority(),
                        CreateVerifiedWorkflowAuthority(CreateWorkflowBinding(), VerifiedProtocolAuthority()),
                        eventRecord)
                };
            }
            else
            {
                manifest = CreateManifest();
            }

            var verification = new BundleVerifier().Verify(manifest, options);

            Assert.IsTrue(verification.IsValid, fixtureId);
            Assert.AreEqual("bundle-manifest", document.RootElement.GetProperty("case").GetProperty("digestScope").GetString(), fixtureId);
            Assert.AreEqual(
                document.RootElement.GetProperty("case").GetProperty("expectedManifestDigest").GetString(),
                manifest.ComputeManifestDigest().ToString(),
                fixtureId);
            Assert.IsTrue(document.RootElement.GetProperty("case").GetProperty("nonClaims").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)), fixtureId);
        }
    }

    [TestMethod]
    public void Negative_gate_6_fixtures_replay_expected_error_categories()
    {
        AssertNegative("artifact-invalid-digest.json", ArtifactFixtureDirectory(), BundleErrorCodes.InvalidArtifactDigest, () =>
            new BundleVerifier().Verify(
                CreateLegacyManifest(new BundleArtifact("artifacts/search-plan.json", "application/json", ArtifactBytes().Length, default)),
                CreateOptions("artifacts/search-plan.json", ArtifactBytes())));

        AssertNegative("artifact-negative-size.json", ArtifactFixtureDirectory(), BundleErrorCodes.NegativeArtifactSize, () =>
            new BundleVerifier().Verify(
                CreateLegacyManifest(new BundleArtifact("artifacts/search-plan.json", "application/json", -1, BundleArtifactEntry.ComputeRawByteDigest(ArtifactBytes()))),
                CreateOptions("artifacts/search-plan.json", ArtifactBytes())));

        AssertNegative("artifact-forbidden-path-absolute.json", ArtifactFixtureDirectory(), BundleErrorCodes.InvalidArtifactPath, () =>
            new BundleVerifier().Verify(
                CreateLegacyManifest(new BundleArtifact("/escape.json", "application/json", ArtifactBytes().Length, BundleArtifactEntry.ComputeRawByteDigest(ArtifactBytes()))),
                CreateOptions("/escape.json", ArtifactBytes())));

        AssertNegative("artifact-forbidden-path-traversal.json", ArtifactFixtureDirectory(), BundleErrorCodes.InvalidArtifactPath, () =>
            new BundleVerifier().Verify(
                CreateLegacyManifest(new BundleArtifact("artifacts/../escape.json", "application/json", ArtifactBytes().Length, BundleArtifactEntry.ComputeRawByteDigest(ArtifactBytes()))),
                CreateOptions("artifacts/../escape.json", ArtifactBytes())));

        AssertNegative("bundle-duplicate-artifact-path.json", BundleFixtureDirectory(), BundleErrorCodes.DuplicateArtifactPath, () =>
        {
            var first = CreateArtifact();
            var second = CreateArtifact(artifactRef: "search-plan-copy");
            return new BundleVerifier().Verify(
                CreateManifest(artifacts: new[] { first, second }),
                CreateOptions(first, ArtifactBytes()));
        });

        AssertNegative("bundle-missing-artifact.json", BundleFixtureDirectory(), BundleErrorCodes.MissingArtifact, () =>
            new BundleVerifier().Verify(CreateManifest()));

        AssertNegative("bundle-checksum-mismatch.json", BundleFixtureDirectory(), BundleErrorCodes.ChecksumMismatch, () =>
            new BundleVerifier().Verify(CreateManifest(), CreateOptions(CreateArtifact(), Encoding.UTF8.GetBytes("different"))));

        AssertNegative("bundle-unsupported-required-schema.json", BundleFixtureDirectory(), BundleErrorCodes.UnsupportedRequiredSchema, () =>
            new BundleVerifier().Verify(
                CreateManifest(requiredSchemas: new[] { new BundleSchemaRef("unsupported.schema", "1.0.0") }),
                CreateOptions(CreateArtifact(), ArtifactBytes())));

        AssertNegative("bundle-stale-manifest-digest.json", BundleFixtureDirectory(), BundleErrorCodes.StaleManifestDigest, () =>
            new BundleVerifier().Verify(
                CreateManifest(),
                CreateOptions(CreateArtifact(), ArtifactBytes()) with { ExpectedManifestDigest = ContentDigest.Sha256Utf8("stale") }));

        AssertNegative("bundle-destructive-overwrite-reject.json", BundleFixtureDirectory(), BundleErrorCodes.DestructiveOverwrite, () =>
            new BundleVerifier().Verify(
                CreateManifest(),
                CreateOptions(CreateArtifact(), ArtifactBytes()) with
                {
                    ExistingArtifactDigests = new Dictionary<string, ContentDigest>(StringComparer.Ordinal)
                    {
                        ["artifacts/search-plan.json"] = ContentDigest.Sha256Utf8("existing")
                    }
                }));
    }

    private static void AssertFixtureSet(string directory, IEnumerable<string> expectedIds)
    {
        var actual = Directory.GetFiles(directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedId in expectedIds)
        {
            Assert.IsTrue(actual.Contains(expectedId), $"Missing Gate 6 fixture '{expectedId}'.");
        }
    }

    private static void AssertNegative(
        string fileName,
        string directory,
        string expectedCategory,
        Func<BundleVerification> action)
    {
        using var document = LoadFixture(directory, fileName);
        var fixtureCategory = document.RootElement.GetProperty("case").GetProperty("errorCategory").GetString();

        Assert.AreEqual(expectedCategory, fixtureCategory, fileName);

        var verification = action();
        Assert.IsTrue(
            verification.Errors.Any(error => string.Equals(error.Category, expectedCategory, StringComparison.Ordinal)),
            $"{fileName} expected '{expectedCategory}' but saw: {string.Join(", ", verification.Errors.Select(error => error.Category))}");
    }

    private static JsonDocument LoadFixture(string directory, string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, fileName)));

    private static string ArtifactFixtureDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "artifacts");

    private static string BundleFixtureDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "bundles");

    private static byte[] ArtifactBytes() => Encoding.UTF8.GetBytes("{\"query\":\"nexus scholar\"}\n");

    private static void AssertNotPlaceholderDigest(ContentDigest digest, string fixtureId)
    {
        Assert.IsTrue(
            digest.Value.Distinct().Count() > 1,
            $"{fixtureId} uses a placeholder digest value.");
    }

    private static ContentDigest ExpectedFixtureOutputDigest(JsonElement root)
    {
        var fixtureCase = root.GetProperty("case");
        if (fixtureCase.TryGetProperty("negative", out var negative) && negative.GetBoolean())
        {
            return ContentDigest.Sha256CanonicalJson(new CanonicalJsonObject()
                .Add("errorCategory", fixtureCase.GetProperty("errorCategory").GetString()!));
        }

        if (fixtureCase.TryGetProperty("expectedDigest", out var expectedDigest))
        {
            return ContentDigest.Parse(expectedDigest.GetString()!);
        }

        if (fixtureCase.TryGetProperty("expectedRawByteDigest", out var expectedRawByteDigest))
        {
            return ContentDigest.Parse(expectedRawByteDigest.GetString()!);
        }

        if (fixtureCase.TryGetProperty("expectedManifestDigest", out var expectedManifestDigest))
        {
            return ContentDigest.Parse(expectedManifestDigest.GetString()!);
        }

        Assert.Fail($"Fixture '{root.GetProperty("fixtureId").GetString()}' does not declare a replayable expected output.");
        return default;
    }

    private static ContentDigest ProtocolDigest() => ApprovedProtocolVersion().ToProtocolContentDigestEnvelope().ComputeDigest();

    private static BundleArtifactEntry CreateArtifact(string artifactRef = "search-plan")
    {
        var bytes = ArtifactBytes();
        return new BundleArtifactEntry(
            artifactRef,
            "artifacts/search-plan.json",
            "workflow-artifact",
            "application/json",
            bytes.Length,
            BundleArtifactEntry.ComputeRawByteDigest(bytes),
            "nexus.workflow.artifact",
            "1.0.0",
            requiredFor: "workflow");
    }

    private static ReviewBundleManifest CreateManifest(
        IEnumerable<BundleArtifactEntry>? artifacts = null,
        IEnumerable<BundleSchemaRef>? requiredSchemas = null,
        BundleWorkflowBinding? workflowBinding = null,
        IEnumerable<BundleProvenanceBinding>? provenanceBindings = null)
    {
        return new ReviewBundleManifest(
            "bundle-1",
            "researcher-1",
            new BundleProtocolBinding(
                ProtocolId,
                ProtocolVersionId,
                1,
                BundleConstants.ApprovedProtocolStatus,
                ProtocolDigest()),
            artifacts ?? new[] { CreateArtifact() },
            requiredSchemas ?? new[] { new BundleSchemaRef("nexus.workflow.artifact", "1.0.0") },
            FixedTime,
            workflowBinding,
            provenanceBindings);
    }

    private static ReviewBundleManifest CreateLegacyManifest(BundleArtifact artifact)
    {
        return new ReviewBundleManifest(
            "1.0.0",
            "project-1",
            ProtocolDigest(),
            "workflow-1",
            FixedTime,
            new[] { artifact });
    }

    private static BundleWorkflowBinding CreateWorkflowBinding()
    {
        return new BundleWorkflowBinding(
            "workflow-1",
            ContentDigest.Sha256Utf8("workflow"),
            "template-1",
            "1.0.0",
            ContentDigest.Sha256Utf8("template"),
            ProtocolVersionId,
            ProtocolDigest());
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

    private static BundleVerificationOptions CreateOptions(BundleArtifactEntry artifact, byte[] bytes) =>
        CreateOptions(artifact.LogicalPath, bytes);

    private static BundleVerificationOptions CreateOptions(string logicalPath, byte[] bytes)
    {
        return new BundleVerificationOptions
        {
            SupportedRequiredSchemas = new[] { new BundleSchemaRef("nexus.workflow.artifact", "1.0.0") },
            KnownProtocolContentDigests = new Dictionary<string, ContentDigest>(StringComparer.Ordinal)
            {
                [ProtocolVersionId] = ProtocolDigest(),
                ["project-1-version"] = ProtocolDigest()
            },
            AuthorityResolver = new ReplayBundleAuthorityResolver(VerifiedProtocolAuthority()),
            ArtifactBytes = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [logicalPath] = bytes
            }
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
        return new VerifiedProtocolVersion(version, ApprovalPolicy.ExplicitCustomSingleResearcher(), Array.Empty<VerifiedProtocolApproval>());
    }

    private static WorkflowDefinition CreateVerifiedWorkflowAuthority(BundleWorkflowBinding binding, VerifiedProtocolVersion protocol)
    {
        var definition = (WorkflowDefinition)typeof(WorkflowDefinition).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single().Invoke(new object[]
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

    private sealed class ReplayBundleAuthorityResolver : IBundleAuthorityResolver
    {
        private readonly VerifiedProtocolVersion? _protocol;
        private readonly WorkflowDefinition? _workflow;
        private readonly ResearchEvent? _event;

        public ReplayBundleAuthorityResolver(VerifiedProtocolVersion? protocol = null, WorkflowDefinition? workflow = null, ResearchEvent? @event = null)
        {
            _protocol = protocol;
            _workflow = workflow;
            _event = @event;
        }

        public VerifiedProtocolVersion ResolveProtocolVersion(string id) => id == _protocol?.Version.Id ? _protocol! : null!;
        public WorkflowDefinition ResolveWorkflowDefinition(string id) => id == _workflow?.WorkflowId ? _workflow! : null!;
        public ResearchEvent ResolveProvenanceEvent(string id) => id == _event?.EventId.ToString() ? _event! : null!;
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
