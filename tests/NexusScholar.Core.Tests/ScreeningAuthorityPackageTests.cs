using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.ResearchWorkspace;
using NexusScholar.Screening;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class ScreeningAuthorityPackageTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Package_commits_and_reopens_exact_verified_authority()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var criteria = BuildCriteria(protocol);

        var commit = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, criteria);
        var reopened = ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root);
        var readiness = ResearchWorkspaceScreeningAuthorityPackage.Inspect(workspace.Root);

        Assert.IsFalse(commit.AlreadyApplied);
        Assert.AreEqual(protocol.Version.Id, reopened.Protocol.Version.Id);
        Assert.AreEqual(protocol.Version.ContentDigest, reopened.Protocol.Version.ContentDigest);
        Assert.AreEqual(criteria.ComputeDigest(), reopened.Criteria.ComputeDigest());
        Assert.AreEqual(reopened.SourceResultAuthority.Result.ResultId, reopened.Deduplication.Result.ResultId);
        Assert.AreEqual(reopened.DeduplicationAuthorityChain.CurrentSnapshot.SnapshotId, reopened.Manifest.SourceSnapshotId);
        Assert.IsTrue(readiness.Ready);
        Assert.IsFalse(readiness.WorkflowGoverned);
        Assert.AreEqual(2, reopened.Manifest.Artifacts.Count);
        Assert.AreNotEqual(WorkspaceState.Missing, ResearchWorkspaceReadModelBuilder.Build(workspace.Root).State);
        Assert.IsTrue(ResearchWorkspaceDeduplicationReview.Inspect(workspace.Root).Completed);
    }

    [TestMethod]
    public void Package_reports_unavailable_before_any_package_pointer_exists()
    {
        using var workspace = TestWorkspace.Create();

        var readiness = ResearchWorkspaceScreeningAuthorityPackage.Inspect(workspace.Root);

        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, readiness.Status);
        Assert.AreEqual(ResearchWorkspaceScreeningAuthorityPackage.UnavailableCategory, readiness.Category);
    }

    [TestMethod]
    public void Package_exact_replay_is_idempotent()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var criteria = BuildCriteria(protocol);
        var first = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, criteria);

        var replay = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, criteria);

        Assert.IsTrue(replay.AlreadyApplied);
        Assert.AreEqual(first.Project.Revision, replay.Project.Revision);
        Assert.AreEqual(first.Package.Manifest.GenerationId, replay.Package.Manifest.GenerationId);
    }

    [TestMethod]
    public void Package_tamper_fails_closed_and_readiness_requires_recovery()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var commit = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));
        var criteriaArtifact = commit.Package.Manifest.Artifacts.Single(item => item.Name == "criteria");
        var path = ResearchWorkspacePaths.InProject(workspace.Root, criteriaArtifact.RelativePath);
        File.WriteAllBytes(path, File.ReadAllBytes(path).Concat([(byte)' ']).ToArray());

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root));
        var readiness = ResearchWorkspaceScreeningAuthorityPackage.Inspect(workspace.Root);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, readiness.Status);
        Assert.AreEqual(ResearchWorkspaceScreeningAuthorityPackage.InvalidCategory, readiness.Category);
    }

    [TestMethod]
    public void Package_rejects_missing_artifact_and_changed_manifest_bytes()
    {
        using (var workspace = TestWorkspace.Create())
        {
            var protocol = BuildProtocol();
            var commit = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));
            var artifact = commit.Package.Manifest.Artifacts.Single(item => item.Name == "criteria");
            File.Delete(ResearchWorkspacePaths.InProject(workspace.Root, artifact.RelativePath));
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root));
        }

        using (var workspace = TestWorkspace.Create())
        {
            var protocol = BuildProtocol();
            var commit = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));
            var path = ResearchWorkspacePaths.InProject(
                workspace.Root, commit.Project.ScreeningAuthorityPackageManifestPath!);
            File.WriteAllBytes(path, File.ReadAllBytes(path).Concat([(byte)' ']).ToArray());
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root));
        }
    }

    [TestMethod]
    [DataRow("source_result_id")]
    [DataRow("source_result_digest")]
    [DataRow("decision_set_digest")]
    [DataRow("source_snapshot_id")]
    [DataRow("source_snapshot_record_digest")]
    public void Package_rejects_manifest_source_binding_tamper(string field)
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var commit = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var path = ResearchWorkspacePaths.InProject(workspace.Root, project.ScreeningAuthorityPackageManifestPath!);
        var bytes = Mutate(File.ReadAllBytes(path), root =>
            root[field] = field.EndsWith("digest", StringComparison.Ordinal)
                ? ContentDigest.Sha256Utf8($"tampered-{field}").ToString()
                : $"tampered-{field}");
        File.WriteAllBytes(path, bytes);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project with
        {
            ScreeningAuthorityPackageManifestSha256 = ContentDigest.Sha256(bytes).ToString()
        });

        var error = Assert.ThrowsExactly<ResearchWorkspaceScreeningAuthorityException>(() =>
            ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root));
        Assert.AreEqual(ResearchWorkspaceScreeningAuthorityPackage.StaleCategory, error.Category);
    }

    [TestMethod]
    public void Package_pointer_is_not_published_when_promotion_is_interrupted()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();

        Assert.ThrowsExactly<InjectedFailure>(() =>
            ResearchWorkspaceScreeningAuthorityPackage.Commit(
                workspace.Root,
                protocol,
                BuildCriteria(protocol),
                point =>
                {
                    if (point == ResearchWorkspaceAuthorityFaultPoint.AfterPromotion)
                    {
                        throw new InjectedFailure();
                    }
                }));

        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.IsNull(project.CurrentScreeningAuthorityPackageGenerationId);
        Assert.IsTrue(Directory.Exists(ResearchWorkspacePaths.InProject(
            workspace.Root, ResearchWorkspacePaths.GenerationQuarantine)));
    }

    [TestMethod]
    public void Package_rejects_stale_deduplication_authority_pointer()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        _ = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project with
        {
            CurrentAuthorityGenerationId = "authority-foreign"
        });

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root));
    }

    [TestMethod]
    public void Package_rejects_stale_project_revision()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        _ = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project with { Revision = project.Revision + 1 });

        var readiness = ResearchWorkspaceScreeningAuthorityPackage.Inspect(workspace.Root);

        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale, readiness.Status);
        Assert.AreEqual(ResearchWorkspaceScreeningAuthorityPackage.StaleCategory, readiness.Category);
    }

    [TestMethod]
    public void Package_can_publish_fresh_generation_after_project_revision_makes_prior_package_stale()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var criteria = BuildCriteria(protocol);
        var first = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, criteria);
        ResearchWorkspaceStore.WriteProject(
            workspace.Location,
            first.Project with { Revision = first.Project.Revision + 1 });

        var replacement = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, criteria);

        Assert.IsFalse(replacement.AlreadyApplied);
        Assert.AreNotEqual(first.Package.Manifest.GenerationId, replacement.Package.Manifest.GenerationId);
        Assert.AreEqual(replacement.Project.Revision, replacement.Package.Manifest.ProjectRevision);
        Assert.IsTrue(ResearchWorkspaceScreeningAuthorityPackage.Inspect(workspace.Root).Ready);
    }

    [TestMethod]
    public void Deduplication_successor_can_follow_package_revision_without_breaking_authority_lineage()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        _ = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));
        var target = ResearchWorkspaceDeduplicationReview.Inspect(workspace.Root).Targets.Single();
        var preview = ResearchWorkspaceDeduplicationReview.Preview(new ResearchWorkspaceDeduplicationReviewRequest(
            workspace.Root,
            target.TargetId,
            DeduplicationAuthorityPolicyConstants.MergeAction,
            "duplicate",
            "Reviewed after Screening authority package publication.",
            "alice",
            "owner",
            null,
            FixedTime.AddMinutes(1)));

        var commit = ResearchWorkspaceDeduplicationReview.Commit(preview);

        Assert.IsTrue(commit.Completed);
        Assert.AreEqual(
            ResearchWorkspaceOperationStatus.Stale,
            ResearchWorkspaceScreeningAuthorityPackage.Inspect(workspace.Root).Status);
        Assert.IsTrue(ResearchWorkspaceDeduplicationReview.Inspect(workspace.Root).Completed);
    }

    [TestMethod]
    public void Package_rejects_foreign_protocol_and_unresolved_workflow_binding()
    {
        using var workspace = TestWorkspace.Create();
        var foreign = BuildProtocol("workspace-foreign");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, foreign, BuildCriteria(foreign)));

        var local = BuildProtocol();
        var workflowCriteria = BuildCriteria(local, "workflow-claimed");
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, local, workflowCriteria));
    }

    [TestMethod]
    public void Protocol_authority_codec_round_trips_and_rejects_tamper()
    {
        var protocol = BuildProtocol();
        var bytes = ProtocolAuthorityPackageCanonicalCodec.Serialize(protocol);

        var reopened = ProtocolAuthorityPackageCanonicalCodec.Rehydrate(bytes, ContentDigest.Sha256(bytes));

        Assert.AreEqual(protocol.Version.ContentDigest, reopened.Version.ContentDigest);
        Assert.AreEqual(protocol.Approvals.Single().Approval.ApprovalRecordDigest,
            reopened.Approvals.Single().Approval.ApprovalRecordDigest);
        Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolAuthorityPackageCanonicalCodec.Rehydrate(bytes, ContentDigest.Sha256Utf8("wrong")));

        var missingApproval = Mutate(bytes, root => root["approvals"] = new JsonArray());
        Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolAuthorityPackageCanonicalCodec.Rehydrate(missingApproval, ContentDigest.Sha256(missingApproval)));
        var nonHuman = Mutate(bytes, root =>
            root["approvals"]![0]!["approved_by_is_human"] = false);
        var nonHumanError = Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolAuthorityPackageCanonicalCodec.Rehydrate(nonHuman, ContentDigest.Sha256(nonHuman)));
        Assert.AreEqual(ProtocolErrorCodes.NonHumanApprovalActor, nonHumanError.Category);

        var duplicateApproval = Mutate(bytes, root =>
            root["approvals"]!.AsArray().Add(root["approvals"]![0]!.DeepClone()));
        Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolAuthorityPackageCanonicalCodec.Rehydrate(
                duplicateApproval, ContentDigest.Sha256(duplicateApproval)));
        var wrongTarget = Mutate(bytes, root =>
            root["approvals"]![0]!["target_id"] = "protocol-version-foreign");
        Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolAuthorityPackageCanonicalCodec.Rehydrate(wrongTarget, ContentDigest.Sha256(wrongTarget)));
    }

    [TestMethod]
    [DataRow("draft")]
    [DataRow("withdrawn")]
    public void Protocol_authority_codec_rejects_nonapproved_status(string status)
    {
        var bytes = ProtocolAuthorityPackageCanonicalCodec.Serialize(BuildProtocol());
        var changed = Mutate(bytes, root => root["protocol"]!["status"] = status);

        Assert.ThrowsExactly<ProtocolRuleException>(() =>
            ProtocolAuthorityPackageCanonicalCodec.Rehydrate(changed, ContentDigest.Sha256(changed)));
    }

    [TestMethod]
    public void Package_commit_surface_requires_verified_authority_not_ids_or_digests()
    {
        var method = typeof(ResearchWorkspaceScreeningAuthorityPackage).GetMethod(
            nameof(ResearchWorkspaceScreeningAuthorityPackage.Commit))!;
        var parameters = method.GetParameters();

        Assert.AreEqual(typeof(string), parameters[0].ParameterType);
        Assert.AreEqual(typeof(VerifiedProtocolVersion), parameters[1].ParameterType);
        Assert.AreEqual(typeof(ScreeningCriteria), parameters[2].ParameterType);
        Assert.IsFalse(parameters.Skip(1).Any(item =>
            item.ParameterType == typeof(string) || item.ParameterType == typeof(ContentDigest)));
    }

    [TestMethod]
    public void Protocol_authority_codec_resolves_non_approver_human_decision_actor()
    {
        var protocol = BuildProtocol(includeDecisionActor: true);
        var bytes = ProtocolAuthorityPackageCanonicalCodec.Serialize(protocol);

        var reopened = ProtocolAuthorityPackageCanonicalCodec.Rehydrate(bytes, ContentDigest.Sha256(bytes));

        Assert.AreEqual("bob", reopened.Version.Decisions.Single().DecidedBy.ToString());
    }

    private static VerifiedProtocolVersion BuildProtocol(
        string projectId = "workspace-screening",
        bool includeDecisionActor = false)
    {
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var template = new ProtocolTemplate("template-screening", "1.0.0", ContentDigest.Sha256Utf8("template-screening"));
        var required = includeDecisionActor
            ? new[]
            {
                new RequiredDecisionDefinition(
                    "population", "Population", "Eligible population", CanonicalJsonValue.From("string"),
                    "approval", "approval", "population", false)
            }
            : Array.Empty<RequiredDecisionDefinition>();
        var decisions = includeDecisionActor
            ? new[]
            {
                new ProtocolDecision(
                    "decision-population", "population", CanonicalJsonValue.From("adults"),
                    "Scoped by protocol.", ActorId.From("bob"), FixedTime)
            }
            : Array.Empty<ProtocolDecision>();
        var provisional = new ProtocolVersion(
            "protocol-screening-v1", "protocol-screening", projectId, 1, ProtocolStatus.ReadyForReview,
            template, new ProtocolIntent("screening", "screen title and abstract evidence"), new CanonicalJsonObject(),
            required, decisions, Array.Empty<ProtocolWaiver>(),
            ContentDigest.Sha256Utf8("placeholder"), policy.PolicyId, Array.Empty<string>(), null);
        var candidate = new ProtocolVersion(
            provisional.Id, provisional.ProtocolId, provisional.ProjectId, provisional.VersionNumber, provisional.Status,
            provisional.Template, provisional.Intent, provisional.Values, provisional.RequiredDecisions, provisional.Decisions,
            provisional.Waivers, provisional.ToProtocolContentDigestEnvelope().ComputeDigest(), policy.PolicyId,
            Array.Empty<string>(), null);
        var resolver = new ProtocolResolver(policy, new[] { ActorId.From("alice") });
        var approval = ProtocolApproval.Create(
            new FixedIdGenerator("00000000-0000-0000-0000-000000000901"),
            candidate,
            policy,
            ProtocolActor.Human("alice"),
            new FixedClock(),
            candidate.ContentDigest,
            role: "owner",
            rationale: "Approved for local Screening.");
        var verifiedApproval = ProtocolRehydrator.RehydrateApproval(
            ToUnverified(approval, policy), candidate, policy, resolver);
        var approved = new ProtocolVersion(
            candidate.Id, candidate.ProtocolId, candidate.ProjectId, candidate.VersionNumber, ProtocolStatus.Approved,
            candidate.Template, candidate.Intent, candidate.Values, candidate.RequiredDecisions, candidate.Decisions,
            candidate.Waivers, candidate.ContentDigest, policy.PolicyId, new[] { approval.ApprovalId }, FixedTime);
        return new VerifiedProtocolVersion(approved, policy, new[] { verifiedApproval });
    }

    private static ScreeningCriteria BuildCriteria(
        VerifiedProtocolVersion protocol,
        string? workflowBinding = null) => new(
        "criteria-title-abstract", "1.0.0", ScreeningStages.TitleAbstract,
        CanonicalJsonValue.From("Include eligible study designs."),
        CanonicalJsonValue.From("Exclude ineligible populations."),
        true,
        protocol.Version.Id,
        protocol.Version.ContentDigest.ToString(),
        workflowBinding,
        approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
        approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
        currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());

    private static UnverifiedProtocolApproval ToUnverified(ProtocolApproval value, ApprovalPolicy policy) => new(
        value.ApprovalId, value.TargetType, value.TargetId, value.ProtocolId, value.ProtocolVersionId,
        value.ProtocolVersionNumber, value.ContentDigest, value.PolicyId, value.PolicyVersion, policy.Mode,
        value.Decision, value.ApprovedBy, value.ApprovedAt, value.Role, value.Rationale,
        value.SupersedesApprovalId, value.ApprovalRecordDigest);

    private static byte[] Mutate(byte[] bytes, Action<JsonObject> mutation)
    {
        var root = JsonNode.Parse(bytes)!.AsObject();
        mutation(root);
        using var document = JsonDocument.Parse(root.ToJsonString());
        return CanonicalJsonSerializer.SerializeToUtf8Bytes(CanonicalJsonValue.FromJsonElement(document.RootElement));
    }

    private sealed class ProtocolResolver(
        ApprovalPolicy policy,
        IEnumerable<ActorId> humans,
        IEnumerable<VerifiedProtocolApproval>? approvals = null) : IProtocolAuthorityResolver
    {
        private readonly HashSet<ActorId> _humans = humans.ToHashSet();
        private readonly IReadOnlyDictionary<string, VerifiedProtocolApproval> _approvals =
            (approvals ?? Array.Empty<VerifiedProtocolApproval>()).ToDictionary(
                item => item.Approval.ApprovalId, StringComparer.Ordinal);

        public ApprovalPolicy ResolveApprovalPolicy(ProtocolTemplate template) => policy;
        public bool IsHumanActor(ActorId actorId) => _humans.Contains(actorId);
        public VerifiedProtocolApproval ResolveApproval(string approvalId) =>
            _approvals.TryGetValue(approvalId, out var value) ? value : null!;
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root, ResearchWorkspaceLocation location)
        {
            Root = root;
            Location = location;
        }

        public string Root { get; }
        public ResearchWorkspaceLocation Location { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"nexus-screening-authority-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var location = new ResearchWorkspaceLocation(root, ResearchWorkspacePaths.ProjectFile(root));
            foreach (var directory in ResearchWorkspacePaths.RequiredDirectories)
            {
                Directory.CreateDirectory(ResearchWorkspacePaths.InProject(root, directory));
            }

            var project = ResearchWorkspaceProject.Create("Screening authority", FixedTime, "workspace-screening");
            var relative = $"{ResearchWorkspacePaths.SearchInputs}/input.csv";
            var bytes = Encoding.UTF8.GetBytes(
                "eid,title,doi\n1,Example record,10.1000/example-a\n2,Example record,10.1000/example-b\n");
            File.WriteAllBytes(ResearchWorkspacePaths.InProject(root, relative), bytes);
            project = project.WithInput(new ResearchWorkspaceInput
            {
                InputId = "input",
                Kind = "search-export",
                Source = "scopus",
                Format = "csv",
                RelativePath = relative,
                Sha256 = ContentDigest.Sha256(bytes).ToString(),
                QueryId = "input",
                ImportTracePath = $"{ResearchWorkspacePaths.ImportOutputs}/input.import-trace.json"
            });
            ResearchWorkspaceStore.WriteProject(location, project);
            var analysis = ResearchWorkspaceTransaction.AnalyzeAndCommit(location, project);
            var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(analysis.Analysis.DeduplicationResult);
            var policy = DeduplicationAuthorityPolicy.CreatePolicyMaterial(new UnverifiedDeduplicationAuthorityPolicy(
                DeduplicationAuthorityPolicyConstants.SchemaId,
                DeduplicationAuthorityPolicyConstants.SchemaVersion,
                DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
                source.Result.PolicyId!,
                DeduplicationService.PolicyVersion,
                new[] { new DeduplicationAuthorityPolicyActorRole("alice", "owner") },
                DeduplicationAuthorityPolicyConstants.ClosedActions,
                new[]
                {
                    new DeduplicationAuthorityPolicyReasonGroup(
                        DeduplicationAuthorityPolicyConstants.MergeAction, new[] { "duplicate" }),
                    new DeduplicationAuthorityPolicyReasonGroup(
                        DeduplicationAuthorityPolicyConstants.KeepSeparateAction, new[] { "different" }),
                    new DeduplicationAuthorityPolicyReasonGroup(
                        DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, new[] { "uncertain" })
                },
                false, "alice", "owner", FixedTime));
            var analysisManifest = File.ReadAllBytes(
                ResearchWorkspacePaths.InProject(root, analysis.Project.GenerationManifestPath!));
            _ = ResearchWorkspaceTransaction.InitializeAuthorityGeneration(
                location, analysis.Project, analysis.Project.CurrentGenerationId!,
                ContentDigest.Sha256(analysisManifest).ToString(), "snapshot-screening-authority",
                source, policy, "alice", "owner", new FixedClock(),
                new FixedIdGenerator("00000000-0000-0000-0000-000000000902"));
            return new TestWorkspace(root, location);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => FixedTime;
    }

    private sealed class FixedIdGenerator(string id) : IIdGenerator
    {
        public Guid NewId() => Guid.Parse(id);
    }

    private sealed class InjectedFailure : Exception;
}
