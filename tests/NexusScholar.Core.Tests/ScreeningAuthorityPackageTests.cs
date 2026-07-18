using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Reporting;
using NexusScholar.ResearchWorkspace;
using NexusScholar.Screening;
using NexusScholar.Screening.CorpusSnapshots;

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
    public void Package_rejects_superseded_protocol_after_protocol_rehydrate()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var commit = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));

        var manifestPath = ResearchWorkspacePaths.InProject(
            workspace.Root, commit.Project.ScreeningAuthorityPackageManifestPath!);
        var protocolArtifact = commit.Package.Manifest.Artifacts.Single(item => item.Name == "protocol-authority");
        var protocolArtifactPath = ResearchWorkspacePaths.InProject(workspace.Root, protocolArtifact.RelativePath);

        var supersededProtocol = Mutate(
            File.ReadAllBytes(protocolArtifactPath),
            root =>
            {
                root["protocol"]!["status"] = "superseded";
                root["protocol"]!["superseded_by_version_id"] = "protocol-screening-superseded-by";
            });
        File.WriteAllBytes(protocolArtifactPath, supersededProtocol);

        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var protocolDigest = ContentDigest.Sha256(supersededProtocol).ToString();
        foreach (var artifact in manifest["artifacts"]!.AsArray())
        {
            if (artifact!["name"]?.GetValue<string>() == "protocol-authority")
            {
                artifact["sha256"] = protocolDigest;
            }
        }

        File.WriteAllText(manifestPath, manifest.ToJsonString());
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath) with
        {
            ScreeningAuthorityPackageManifestSha256 = ContentDigest.Sha256(File.ReadAllBytes(manifestPath)).ToString()
        };
        ResearchWorkspaceStore.WriteProject(workspace.Location, project);

        var error = Assert.ThrowsExactly<ScreeningRuleException>(
            () => ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root));
        Assert.AreEqual(ResearchWorkspaceScreeningAuthorityPackage.InvalidCategory, error.Category);
        Assert.AreEqual(
            "Screening authority requires an approved protocol; superseded versions are not admissible.",
            error.Message);
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
    public void Package_remains_current_across_later_revision_when_all_authority_pointers_are_unchanged()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        _ = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, BuildCriteria(protocol));
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project with { Revision = project.Revision + 1 });

        var readiness = ResearchWorkspaceScreeningAuthorityPackage.Inspect(workspace.Root);

        Assert.AreEqual(ResearchWorkspaceOperationStatus.Succeeded, readiness.Status);
        Assert.AreEqual(ResearchWorkspaceScreeningAuthorityPackage.ReadyCategory, readiness.Category);
    }

    [TestMethod]
    public void Package_commit_is_idempotent_after_unrelated_project_revision()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var criteria = BuildCriteria(protocol);
        var first = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, criteria);
        ResearchWorkspaceStore.WriteProject(
            workspace.Location,
            first.Project with { Revision = first.Project.Revision + 1 });

        var replacement = ResearchWorkspaceScreeningAuthorityPackage.Commit(workspace.Root, protocol, criteria);

        Assert.IsTrue(replacement.AlreadyApplied);
        Assert.AreEqual(first.Package.Manifest.GenerationId, replacement.Package.Manifest.GenerationId);
        Assert.AreEqual(first.Package.Manifest.ProjectRevision, replacement.Package.Manifest.ProjectRevision);
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
            ResearchWorkspaceOperationStatus.Failed,
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
    public void Protocol_authority_codec_round_trips_superseded_status()
    {
        var bytes = ProtocolAuthorityPackageCanonicalCodec.Serialize(BuildProtocol());

        var superseded = Mutate(bytes, root =>
        {
            root["protocol"]!["status"] = "superseded";
            root["protocol"]!["superseded_by_version_id"] = "protocol-screening-successor";
        });

        var reopened = ProtocolAuthorityPackageCanonicalCodec.Rehydrate(superseded, ContentDigest.Sha256(superseded));
        Assert.AreEqual(ProtocolStatus.Superseded, reopened.Version.Status);
    }

    [TestMethod]
    [DataRow("draft")]
    [DataRow("ready_for_review")]
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

    [TestMethod]
    public void Workspace_screening_review_commits_and_reopens()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var packageCommit = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol));
        var conduct = InitializeConduct(workspace, packageCommit.Package, requiredReviewCount: 1);
        var candidateId = conduct.Header.CandidateIds[0];

        var queue = ResearchWorkspaceScreeningReview.Inspect(workspace.Root);
        ResearchWorkspaceScreeningReviewPreview? preview = null;
        ResearchWorkspaceScreeningReviewCommitResult? commit = null;
        for (var index = 0; index < conduct.Header.CandidateIds.Count; index++)
        {
            preview = ResearchWorkspaceScreeningReview.Preview(
                new ResearchWorkspaceScreeningReviewRequest(
                    workspace.Root, conduct.Header.CandidateIds[index], "review", ScreeningVerdicts.Include,
                    "alice", ScreeningConductActorKinds.Human, "reviewer",
                    "Eligible title and abstract.", null,
                    FixedTime.AddMinutes(2 + index)));
            commit = ResearchWorkspaceScreeningReview.Commit(preview);
            Assert.IsTrue(commit.Completed);
        }
        var afterDecision = ResearchWorkspaceScreeningReview.Inspect(workspace.Root);
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var package = ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root);
        var reopened = ResearchWorkspaceScreeningConductVerifier.VerifyCurrent(
            workspace.Location, project, package.Deduplication, package.Protocol, package.Criteria,
            package.SourceResultAuthority, package.DeduplicationAuthorityChain.CurrentSnapshot);

        Assert.IsTrue(queue.Completed);
        Assert.IsTrue(preview!.IsReady);
        Assert.IsTrue(commit!.Completed);
        Assert.AreEqual(ScreeningVerdicts.Include,
            afterDecision.Targets.Single(item => item.CandidateId == candidateId).CurrentVerdict);
        Assert.IsTrue(afterDecision.HandoffReady);
        Assert.IsNull(reopened.Handoff);
        var closedTarget = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                workspace.Root, candidateId, "review", ScreeningVerdicts.Exclude,
                "bob", ScreeningConductActorKinds.Human, "reviewer",
                "A closed target requires Slice 6 supersession.", "wrong-population",
                FixedTime.AddMinutes(10)));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, closedTarget.Status);
    }

    [TestMethod]
    public void Workspace_screening_review_rejects_tampered_and_stale_previews()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var packageCommit = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol));
        var conduct = InitializeConduct(workspace, packageCommit.Package, requiredReviewCount: 1);
        var candidateId = conduct.Header.CandidateIds[0];
        var preview = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                workspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer", "Eligible.", null,
                FixedTime.AddMinutes(2)));

        var tampered = ResearchWorkspaceScreeningReview.Commit(
            preview with { Verdict = ScreeningVerdicts.Exclude });
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale, tampered.Status);

        var first = ResearchWorkspaceScreeningReview.Commit(preview);
        Assert.IsTrue(first.Completed);
        var stale = ResearchWorkspaceScreeningReview.Commit(preview);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale, stale.Status);

        var unauthorized = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                workspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                "mallory", ScreeningConductActorKinds.Human, "reviewer", "Unauthorized.", null,
                FixedTime.AddMinutes(4)));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, unauthorized.Status);
    }

    [TestMethod]
    public void Workspace_screening_review_rejects_incomplete_cross_workspace_and_nonhuman_authority()
    {
        using var firstWorkspace = TestWorkspace.Create();
        using var secondWorkspace = TestWorkspace.Create();
        var firstProtocol = BuildProtocol();
        var secondProtocol = BuildProtocol();
        var firstPackage = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            firstWorkspace.Root, firstProtocol, BuildCriteria(firstProtocol)).Package;
        var secondPackage = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            secondWorkspace.Root, secondProtocol, BuildCriteria(secondProtocol)).Package;
        var firstConduct = InitializeConduct(firstWorkspace, firstPackage, requiredReviewCount: 1);
        _ = InitializeConduct(secondWorkspace, secondPackage, requiredReviewCount: 1);
        var candidateId = firstConduct.Header.CandidateIds[0];
        var preview = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                firstWorkspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "Eligible.", null, FixedTime.AddMinutes(2)));
        Assert.IsTrue(preview.IsReady);

        ResearchWorkspaceScreeningReviewPreview[] incomplete =
        [
            preview with { SourceResultDigest = null },
            preview with { SourceSnapshotRecordDigest = null },
            preview with { DecisionSetDigest = null },
            preview with { ProtocolContentDigest = null },
            preview with { CriteriaDigest = null },
            preview with { CorpusBindingDigest = null },
            preview with { TargetDigest = null }
        ];
        foreach (var changed in incomplete)
            Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale,
                ResearchWorkspaceScreeningReview.Commit(changed).Status);

        var crossWorkspace = ResearchWorkspaceScreeningReview.Commit(
            preview with { WorkspaceDirectory = secondWorkspace.Root });
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale, crossWorkspace.Status);
        var pathTarget = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                firstWorkspace.Root, "nexus-output/row-1", "review", ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "UI row text is not authority.", null, FixedTime.AddMinutes(3)));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, pathTarget.Status);
        var automation = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                firstWorkspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                "automation-1", ScreeningConductActorKinds.Automation, "reviewer",
                "Automation cannot finalize.", null, FixedTime.AddMinutes(4)));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, automation.Status);
        var correction = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                firstWorkspace.Root, candidateId, "correction", ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "Slice 6 intent.", null, FixedTime.AddMinutes(5)));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, correction.Status);
        var noPreview = ResearchWorkspaceScreeningReview.Commit(
            preview with
            {
                Status = ResearchWorkspaceOperationStatus.Failed,
                ConfirmationToken = null
            });
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, noPreview.Status);
    }

    [TestMethod]
    public void Workspace_screening_correction_supersedes_exact_decision_and_publishes_handoff()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var package = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol)).Package;
        var conduct = InitializeConduct(workspace, package, requiredReviewCount: 1);
        foreach (var candidateId in conduct.Header.CandidateIds)
        {
            var review = ResearchWorkspaceScreeningReview.Preview(
                new ResearchWorkspaceScreeningReviewRequest(
                    workspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                    "alice", ScreeningConductActorKinds.Human, "reviewer",
                    "Initially eligible.", null, FixedTime.AddMinutes(2)));
            Assert.IsTrue(ResearchWorkspaceScreeningReview.Commit(review).Completed);
        }

        var target = ResearchWorkspaceScreeningReview.Inspect(workspace.Root).Targets[0];
        var superseded = target.CurrentDecisions.Single();
        var correction = ResearchWorkspaceScreeningResolution.Preview(
            new ResearchWorkspaceScreeningResolutionRequest(
                workspace.Root, target.CandidateId, "correction", ScreeningVerdicts.Exclude,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "The population is ineligible after rechecking the abstract.",
                "wrong-population", superseded.DecisionDigest, null, [],
                FixedTime.AddMinutes(10)));
        var correctionCommit = ResearchWorkspaceScreeningResolution.Commit(correction);

        Assert.IsTrue(correction.IsReady);
        Assert.IsTrue(correctionCommit.Completed);
        var afterCorrection = ResearchWorkspaceScreeningReview.Inspect(workspace.Root);
        var correctedTarget = afterCorrection.Targets.Single(item =>
            item.CandidateId == target.CandidateId);
        Assert.AreEqual(ScreeningVerdicts.Exclude, correctedTarget.CurrentVerdict);
        Assert.AreEqual("correction", correctedTarget.CurrentDecisions.Single().Kind);
        Assert.AreNotEqual(
            superseded.DecisionDigest,
            correctedTarget.CurrentDecisions.Single().DecisionDigest);
        Assert.IsTrue(afterCorrection.HandoffReady);

        var handoffPreview = ResearchWorkspaceScreeningResolution.PreviewHandoff(
            new ResearchWorkspaceScreeningHandoffRequest(
                workspace.Root, "alice", ScreeningConductActorKinds.Human, "reviewer",
                "All title and abstract outcomes are terminal and ready for Full Text handoff.",
                FixedTime.AddMinutes(11)));
        var handoffCommit = ResearchWorkspaceScreeningResolution.CommitHandoff(handoffPreview);

        Assert.IsTrue(handoffPreview.IsReady);
        Assert.IsTrue(handoffCommit.Completed);
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var reopenedPackage = ResearchWorkspaceScreeningAuthorityPackage.VerifyCurrent(workspace.Root);
        var reopened = ResearchWorkspaceScreeningConductVerifier.VerifyCurrent(
            workspace.Location, project, reopenedPackage.Deduplication,
            reopenedPackage.Protocol, reopenedPackage.Criteria,
            reopenedPackage.SourceResultAuthority,
            reopenedPackage.DeduplicationAuthorityChain.CurrentSnapshot);
        Assert.IsNotNull(reopened.Handoff);
        Assert.AreEqual(handoffCommit.HandoffDigest, reopened.Handoff.Digest.ToString());
        Assert.AreEqual("alice", reopened.Handoff.PublishedBy.ActorId);
        Assert.AreEqual(ScreeningConductActorKinds.Human, reopened.Handoff.PublishedBy.Kind);
        Assert.AreEqual("reviewer", reopened.Handoff.PublishedBy.Role);
        Assert.AreEqual(
            "All title and abstract outcomes are terminal and ready for Full Text handoff.",
            reopened.Handoff.Rationale);
        Assert.IsTrue(reopened.Handoff.ConfirmationMaterialDigest.IsValid);
        Assert.AreEqual(
            correctedTarget.CurrentDecisions.Single().DecisionDigest,
            reopened.Handoff.Outcomes.Single(item =>
                item.CandidateId == target.CandidateId)
                .SupportingDecisionDigests.Single().ToString());
        var postHandoff = ResearchWorkspaceScreeningResolution.Preview(
            new ResearchWorkspaceScreeningResolutionRequest(
                workspace.Root, target.CandidateId, "correction",
                ScreeningVerdicts.Include, "alice", ScreeningConductActorKinds.Human,
                "reviewer", "Attempted after terminal handoff.", null,
                correctedTarget.CurrentDecisions.Single().DecisionDigest,
                null, [], FixedTime.AddMinutes(12)));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, postHandoff.Status);
        Assert.AreEqual(
            handoffCommit.HandoffDigest,
            ResearchWorkspaceScreeningConductVerifier.VerifyCurrent(
                workspace.Location,
                ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath),
                reopenedPackage.Deduplication,
                reopenedPackage.Protocol,
                reopenedPackage.Criteria,
                reopenedPackage.SourceResultAuthority,
                reopenedPackage.DeduplicationAuthorityChain.CurrentSnapshot)
                .Handoff!.Digest.ToString());
    }

    [TestMethod]
    public void Workspace_screening_adjudication_resolves_exact_conflict()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var package = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol)).Package;
        var conduct = InitializeConduct(workspace, package, requiredReviewCount: 2);
        var candidateId = conduct.Header.CandidateIds[0];
        var alice = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                workspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "Eligible evidence.", null, FixedTime.AddMinutes(2)));
        Assert.IsTrue(ResearchWorkspaceScreeningReview.Commit(alice).Completed);
        var bob = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                workspace.Root, candidateId, "review", ScreeningVerdicts.Exclude,
                "bob", ScreeningConductActorKinds.Human, "reviewer",
                "Population appears ineligible.", "wrong-population",
                FixedTime.AddMinutes(3)));
        Assert.IsTrue(ResearchWorkspaceScreeningReview.Commit(bob).Completed);
        var conflict = ResearchWorkspaceScreeningReview.Inspect(workspace.Root)
            .Targets.Single(item => item.CandidateId == candidateId)
            .Conflicts.Single(item => !item.Resolved);

        var preview = ResearchWorkspaceScreeningResolution.Preview(
            new ResearchWorkspaceScreeningResolutionRequest(
                workspace.Root, candidateId, "adjudication", ScreeningVerdicts.Include,
                "carol", ScreeningConductActorKinds.Human, "chair",
                "Both reviews were considered; the protocol population is eligible.",
                null, null, conflict.ConflictId, conflict.SourceDecisionDigests,
                FixedTime.AddMinutes(4)));
        var commit = ResearchWorkspaceScreeningResolution.Commit(preview);

        Assert.IsTrue(preview.IsReady);
        Assert.IsTrue(commit.Completed);
        var resolved = ResearchWorkspaceScreeningReview.Inspect(workspace.Root)
            .Targets.Single(item => item.CandidateId == candidateId);
        Assert.AreEqual(ScreeningVerdicts.Include, resolved.CurrentVerdict);
        Assert.IsTrue(resolved.Conflicts.Single().Resolved);
        Assert.AreEqual("adjudication",
            resolved.CurrentDecisions.Single(item => item.ActorId == "carol").Kind);
    }

    [TestMethod]
    public void Workspace_screening_resolution_rejects_partial_tampered_and_stale_authority()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var package = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol)).Package;
        var conduct = InitializeConduct(workspace, package, requiredReviewCount: 1);
        var candidateId = conduct.Header.CandidateIds[0];
        var review = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                workspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "Eligible.", null, FixedTime.AddMinutes(2)));
        Assert.IsTrue(ResearchWorkspaceScreeningReview.Commit(review).Completed);
        var current = ResearchWorkspaceScreeningReview.Inspect(workspace.Root)
            .Targets.Single(item => item.CandidateId == candidateId)
            .CurrentDecisions.Single();
        var request = new ResearchWorkspaceScreeningResolutionRequest(
            workspace.Root, candidateId, "correction", ScreeningVerdicts.Exclude,
            "alice", ScreeningConductActorKinds.Human, "reviewer",
            "Corrected after exact evidence review.", "wrong-population",
            current.DecisionDigest, null, [], FixedTime.AddMinutes(3));

        var missingSource = ResearchWorkspaceScreeningResolution.Preview(
            request with { SupersedesDecisionDigest = null });
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, missingSource.Status);
        var automation = ResearchWorkspaceScreeningResolution.Preview(
            request with { ActorId = "automation", ActorKind = ScreeningConductActorKinds.Automation });
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, automation.Status);
        var preview = ResearchWorkspaceScreeningResolution.Preview(request);
        Assert.IsTrue(preview.IsReady);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale,
            ResearchWorkspaceScreeningResolution.Commit(
                preview with { ActorId = "bob" }).Status);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale,
            ResearchWorkspaceScreeningResolution.Commit(
                preview with
                {
                    TargetSummaryDigest = ContentDigest.Sha256Utf8("other-summary").ToString()
                }).Status);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale,
            ResearchWorkspaceScreeningResolution.Commit(
                preview with
                {
                    SupersedesDecisionDigest =
                        ContentDigest.Sha256Utf8("other-decision").ToString()
                }).Status);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale,
            ResearchWorkspaceScreeningResolution.Commit(
                preview with { ExpectedProjectRevision = preview.ExpectedProjectRevision + 1 }).Status);

        var committed = ResearchWorkspaceScreeningResolution.Commit(preview);
        Assert.IsTrue(committed.Completed);
        var duplicate = ResearchWorkspaceScreeningResolution.Preview(request);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, duplicate.Status);
    }

    [TestMethod]
    public void Workspace_screening_resolution_detects_lock_window_authority_refresh()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var package = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol)).Package;
        var conduct = InitializeConduct(workspace, package, requiredReviewCount: 1);
        var candidateId = conduct.Header.CandidateIds[0];
        var review = ResearchWorkspaceScreeningReview.Preview(
            new ResearchWorkspaceScreeningReviewRequest(
                workspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "Eligible.", null, FixedTime.AddMinutes(2)));
        Assert.IsTrue(ResearchWorkspaceScreeningReview.Commit(review).Completed);
        var decision = ResearchWorkspaceScreeningReview.Inspect(workspace.Root)
            .Targets.Single(item => item.CandidateId == candidateId)
            .CurrentDecisions.Single();
        var preview = ResearchWorkspaceScreeningResolution.Preview(
            new ResearchWorkspaceScreeningResolutionRequest(
                workspace.Root, candidateId, "correction", ScreeningVerdicts.Exclude,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "Corrected.", "wrong-population", decision.DecisionDigest,
                null, [], FixedTime.AddMinutes(3)));
        var before = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        var result = ResearchWorkspaceScreeningResolution.Commit(
            preview,
            point =>
            {
                if (point != ResearchWorkspaceAuthorityFaultPoint.AfterStaging)
                    return;
                var project = ResearchWorkspaceStore.ReadProject(
                    workspace.Location.ProjectFilePath);
                ResearchWorkspaceStore.WriteProject(
                    workspace.Location, project with { Revision = project.Revision + 1 });
            });

        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale, result.Status);
        Assert.IsFalse(result.Completed);
        var after = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.AreEqual(before.CurrentScreeningConductGenerationId,
            after.CurrentScreeningConductGenerationId);
        Assert.AreEqual(before.ScreeningConductManifestSha256,
            after.ScreeningConductManifestSha256);
    }

    [TestMethod]
    public void Workspace_screening_handoff_rejects_missing_rationale_and_policy_membership()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var package = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol)).Package;
        var conduct = InitializeConduct(workspace, package, requiredReviewCount: 1);
        foreach (var candidateId in conduct.Header.CandidateIds)
        {
            var review = ResearchWorkspaceScreeningReview.Preview(
                new ResearchWorkspaceScreeningReviewRequest(
                    workspace.Root, candidateId, "review", ScreeningVerdicts.Include,
                    "alice", ScreeningConductActorKinds.Human, "reviewer",
                    "Eligible.", null, FixedTime.AddMinutes(2)));
            Assert.IsTrue(ResearchWorkspaceScreeningReview.Commit(review).Completed);
        }

        var missingRationale = ResearchWorkspaceScreeningResolution.PreviewHandoff(
            new ResearchWorkspaceScreeningHandoffRequest(
                workspace.Root, "alice", ScreeningConductActorKinds.Human,
                "reviewer", "", FixedTime.AddMinutes(4)));
        var outsider = ResearchWorkspaceScreeningResolution.PreviewHandoff(
            new ResearchWorkspaceScreeningHandoffRequest(
                workspace.Root, "mallory", ScreeningConductActorKinds.Human,
                "reviewer", "Attempted unauthorized handoff.", FixedTime.AddMinutes(4)));

        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, missingRationale.Status);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, outsider.Status);
    }

    [TestMethod]
    public void Workspace_local_full_text_intake_and_review_commit_from_verified_handoff()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var package = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol)).Package;
        var conduct = InitializeConduct(workspace, package, requiredReviewCount: 1);
        for (var index = 0; index < conduct.Header.CandidateIds.Count; index++)
        {
            var include = index < 2;
            var review = ResearchWorkspaceScreeningReview.Preview(
                new ResearchWorkspaceScreeningReviewRequest(
                    workspace.Root, conduct.Header.CandidateIds[index], "review",
                    include ? ScreeningVerdicts.Include : ScreeningVerdicts.Exclude,
                    "alice", ScreeningConductActorKinds.Human, "reviewer",
                    include ? "Advance to Full Text." : "Population is ineligible.",
                    include ? null : "wrong-population",
                    FixedTime.AddMinutes(2 + index)));
            Assert.IsTrue(ResearchWorkspaceScreeningReview.Commit(review).Completed);
        }
        var handoff = ResearchWorkspaceScreeningResolution.PreviewHandoff(
            new ResearchWorkspaceScreeningHandoffRequest(
                workspace.Root, "alice", ScreeningConductActorKinds.Human, "reviewer",
                "Terminal title and abstract outcomes are ready for Full Text.",
                FixedTime.AddMinutes(5)));
        Assert.IsTrue(ResearchWorkspaceScreeningResolution.CommitHandoff(handoff).Completed);
        var candidateId = conduct.Header.CandidateIds[0];
        var localPath = Path.Combine(workspace.Root, "eligible-study.txt");
        var original = Encoding.UTF8.GetBytes(
            "Eligible adult population and admitted study design.");
        File.WriteAllBytes(localPath, original);
        var intake = ResearchWorkspaceFullTextWorkflow.PreviewIntake(
            new ResearchWorkspaceFullTextIntakeRequest(
                workspace.Root, candidateId, localPath, FullTextArtifactKinds.Text,
                "text/plain", "alice", FullTextActorKinds.Human,
                FixedTime.AddMinutes(6), 4096));

        Assert.IsTrue(intake.IsReady);
        Assert.AreEqual(FullTextExtractionAttemptStatuses.Success, intake.ExtractionStatus);
        File.WriteAllText(localPath, "changed after preview");
        Assert.AreEqual(
            ResearchWorkspaceOperationStatus.Stale,
            ResearchWorkspaceFullTextWorkflow.CommitIntake(intake).Status);
        File.WriteAllBytes(localPath, original);
        var intakeCommit = ResearchWorkspaceFullTextWorkflow.CommitIntake(intake);
        Assert.IsTrue(intakeCommit.Completed, intakeCommit.Message);
        Assert.AreEqual(candidateId, intakeCommit.CandidateId);
        Assert.AreEqual(
            intake.RawArtifactDigest,
            ResearchWorkspaceFullTextGenerationVerifier.VerifyCurrentIntegrity(
                workspace.Location,
                ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath))
                .RawArtifactDigest);

        var reviewPreview = ResearchWorkspaceFullTextWorkflow.PreviewReview(
            new ResearchWorkspaceFullTextReviewRequest(
                workspace.Root, ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "The complete local text satisfies the approved protocol criteria.",
                "Include studies matching the approved population and design.",
                "Exclude studies outside the approved population or design.",
                "wrong-population-full", null, FixedTime.AddMinutes(7)));
        var reviewCommit = ResearchWorkspaceFullTextWorkflow.CommitReview(reviewPreview);

        Assert.IsTrue(reviewPreview.IsReady, reviewPreview.Message);
        Assert.IsTrue(reviewCommit.Completed);
        Assert.IsTrue(reviewCommit.HandoffReady);
        var currentProject = ResearchWorkspaceStore.ReadProject(
            workspace.Location.ProjectFilePath);
        var manifest = ResearchWorkspaceFullTextGenerationVerifier.VerifyCurrentIntegrity(
            workspace.Location, currentProject);
        Assert.AreEqual(candidateId, manifest.CandidateId);
        Assert.IsTrue(manifest.Artifacts.Any(item => item.Name == "conduct-policy"));
        Assert.IsTrue(manifest.Artifacts.Any(item => item.Name == "criteria"));
        Assert.IsTrue(manifest.Artifacts.Any(item => item.Name == "conduct-entry-000001"));
        Assert.IsTrue(manifest.Artifacts.Any(item => item.Name == "conduct-handoff"));

        var secondCandidateId = conduct.Header.CandidateIds[1];
        var secondPath = Path.Combine(workspace.Root, "eligible-study-2.txt");
        File.WriteAllText(secondPath, "Second eligible complete local study.");
        var secondIntake = ResearchWorkspaceFullTextWorkflow.PreviewIntake(
            new ResearchWorkspaceFullTextIntakeRequest(
                workspace.Root, secondCandidateId, secondPath,
                FullTextArtifactKinds.Text, "text/plain", "alice",
                FullTextActorKinds.Human, FixedTime.AddMinutes(7), 4096));
        Assert.IsTrue(ResearchWorkspaceFullTextWorkflow.CommitIntake(secondIntake).Completed);
        var secondReview = ResearchWorkspaceFullTextWorkflow.PreviewReview(
            new ResearchWorkspaceFullTextReviewRequest(
                workspace.Root, ScreeningVerdicts.Include,
                "alice", ScreeningConductActorKinds.Human, "reviewer",
                "The second complete text satisfies the locked criteria.",
                "Include studies matching the approved population and design.",
                "Exclude studies outside the approved population or design.",
                "wrong-population-full", null, FixedTime.AddMinutes(8),
                secondCandidateId));
        Assert.IsTrue(ResearchWorkspaceFullTextWorkflow.CommitReview(secondReview).Completed);
        currentProject = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.AreEqual(2, currentProject.FullTextCases!.Count);
        Assert.AreEqual(candidateId,
            ResearchWorkspaceFullTextGenerationVerifier.VerifyCandidateIntegrity(
                workspace.Location, currentProject, candidateId).CandidateId);

        var reportingPreview = ResearchWorkspaceReportingWorkflow.Preview(workspace.Root);
        var reportingCommit = ResearchWorkspaceReportingWorkflow.Commit(reportingPreview);
        Assert.IsTrue(reportingPreview.IsReady, reportingPreview.Message);
        Assert.IsTrue(reportingCommit.Completed, reportingCommit.Message);
        currentProject = reportingCommit.Project!;
        var exportPreview = ResearchWorkspaceReviewExportWorkflow.Preview(
            new ResearchWorkspaceReviewExportRequest(
                workspace.Root, "export-complete-review", "alice", "reviewer",
                FixedTime.AddMinutes(9),
                ["Local-only review completed from verified authority records."],
                ["No PRISMA certification claim.", "No external compatibility claim."]));
        Assert.IsTrue(exportPreview.IsReady, exportPreview.Message);
        Assert.AreEqual(2, exportPreview.Counts!.TitleAbstractIncluded);
        Assert.AreEqual(2, exportPreview.Counts.FullTextIncluded);
        Assert.AreEqual(2, exportPreview.Counts.Included);
        Assert.AreEqual(
            ResearchWorkspaceOperationStatus.Stale,
            ResearchWorkspaceReviewExportWorkflow.Commit(
                exportPreview with { ActorId = "mallory" }).Status);
        Assert.AreEqual(
            ResearchWorkspaceOperationStatus.Stale,
            ResearchWorkspaceReviewExportWorkflow.Commit(
                exportPreview with { ActorRole = "chair" }).Status);
        var revisionBeforeFault = ResearchWorkspaceStore.ReadProject(
            workspace.Location.ProjectFilePath).Revision;
        var faulted = ResearchWorkspaceReviewExportWorkflow.Commit(
            exportPreview,
            point =>
            {
                if (point == ResearchWorkspaceExportFaultPoint.AfterStaging)
                    throw new IOException("injected export fault");
            });
        Assert.AreEqual(ResearchWorkspaceOperationStatus.RecoveryRequired, faulted.Status);
        Assert.AreEqual(revisionBeforeFault, ResearchWorkspaceStore.ReadProject(
            workspace.Location.ProjectFilePath).Revision);
        Assert.IsNull(ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location).Head);
        var exportCommit = ResearchWorkspaceReviewExportWorkflow.Commit(exportPreview);
        Assert.IsTrue(exportCommit.Completed, exportCommit.Message);
        Assert.IsTrue(exportCommit.RoundTripVerified);
        Assert.AreEqual(1L, exportCommit.Ordinal);
        var replay = ResearchWorkspaceExportLedgerVerifier.Replay(workspace.Location);
        Assert.AreEqual(exportCommit.EntryDigest, replay.Head!.EntryDigest.ToString());
        var corpusSource = replay.Entries.Single().Sources.Single(
            item => item.Role == ReviewGenerationRoles.CorpusSnapshot);
        Assert.AreEqual(package.Manifest.GenerationId, corpusSource.GenerationId);
        Assert.AreEqual(
            currentProject.ScreeningAuthorityPackageManifestSha256,
            corpusSource.ManifestDigest.ToString());
    }

    [TestMethod]
    public void Workspace_local_full_text_rejects_remote_duplicate_and_failed_extraction_authority()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var package = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol)).Package;
        var conduct = InitializeConduct(workspace, package, requiredReviewCount: 1);
        for (var index = 0; index < conduct.Header.CandidateIds.Count; index++)
        {
            var include = index == 0;
            var review = ResearchWorkspaceScreeningReview.Preview(
                new ResearchWorkspaceScreeningReviewRequest(
                    workspace.Root, conduct.Header.CandidateIds[index], "review",
                    include ? ScreeningVerdicts.Include : ScreeningVerdicts.Exclude,
                    "alice", ScreeningConductActorKinds.Human, "reviewer",
                    include ? "Advance." : "Exclude.", include ? null : "wrong-population",
                    FixedTime.AddMinutes(2 + index)));
            Assert.IsTrue(ResearchWorkspaceScreeningReview.Commit(review).Completed);
        }
        Assert.IsTrue(ResearchWorkspaceScreeningResolution.CommitHandoff(
            ResearchWorkspaceScreeningResolution.PreviewHandoff(
                new ResearchWorkspaceScreeningHandoffRequest(
                    workspace.Root, "alice", ScreeningConductActorKinds.Human,
                    "reviewer", "Ready for Full Text.", FixedTime.AddMinutes(5)))).Completed);
        var candidateId = conduct.Header.CandidateIds[0];
        var remote = ResearchWorkspaceFullTextWorkflow.PreviewIntake(
            new ResearchWorkspaceFullTextIntakeRequest(
                workspace.Root, candidateId, "https://example.test/study.txt",
                FullTextArtifactKinds.Text, "text/plain", "alice",
                FullTextActorKinds.Human, FixedTime.AddMinutes(6), 4096));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, remote.Status);

        var invalidTextPath = Path.Combine(workspace.Root, "invalid-text.txt");
        File.WriteAllBytes(invalidTextPath, [0xff, 0xfe, 0xfd]);
        var intake = ResearchWorkspaceFullTextWorkflow.PreviewIntake(
            new ResearchWorkspaceFullTextIntakeRequest(
                workspace.Root, candidateId, invalidTextPath, FullTextArtifactKinds.Text,
                "text/plain", "alice", FullTextActorKinds.Human,
                FixedTime.AddMinutes(6), 4096));
        Assert.IsTrue(intake.IsReady);
        Assert.AreEqual(FullTextExtractionAttemptStatuses.Failure, intake.ExtractionStatus);
        Assert.IsTrue(ResearchWorkspaceFullTextWorkflow.CommitIntake(intake).Completed);

        var duplicate = ResearchWorkspaceFullTextWorkflow.PreviewIntake(
            new ResearchWorkspaceFullTextIntakeRequest(
                workspace.Root, candidateId, invalidTextPath, FullTextArtifactKinds.Text,
                "text/plain", "alice", FullTextActorKinds.Human,
                FixedTime.AddMinutes(7), 4096));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, duplicate.Status);
        var failedBasis = ResearchWorkspaceFullTextWorkflow.PreviewReview(
            new ResearchWorkspaceFullTextReviewRequest(
                workspace.Root, ScreeningVerdicts.Exclude, "alice",
                ScreeningConductActorKinds.Human, "reviewer",
                "Attempted exclusion from failed extraction.",
                "Include eligible studies.", "Exclude ineligible studies.",
                "wrong-population-full", "wrong-population-full",
                FixedTime.AddMinutes(8)));
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, failedBasis.Status);
    }

    [TestMethod]
    public void Workspace_screening_review_rejects_unverified_workflow_governance_claim()
    {
        using var workspace = TestWorkspace.Create();
        var protocol = BuildProtocol();
        var package = ResearchWorkspaceScreeningAuthorityPackage.Commit(
            workspace.Root, protocol, BuildCriteria(protocol)).Package;
        _ = InitializeConduct(workspace, package, requiredReviewCount: 1);
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var manifestPath = ResearchWorkspacePaths.InProject(
            workspace.Root, project.ScreeningAuthorityPackageManifestPath!);
        var bytes = File.ReadAllBytes(manifestPath);
        var altered = System.Text.Encoding.UTF8.GetBytes(
            System.Text.Encoding.UTF8.GetString(bytes)
                .Replace("\"workflow_governed\":false", "\"workflow_governed\":true",
                    StringComparison.Ordinal));
        Assert.IsFalse(bytes.SequenceEqual(altered));
        File.WriteAllBytes(manifestPath, altered);
        ResearchWorkspaceStore.WriteProject(workspace.Location, project with
        {
            ScreeningAuthorityPackageManifestSha256 =
                ContentDigest.Sha256(altered).ToString()
        });

        var result = ResearchWorkspaceScreeningReview.Inspect(workspace.Root);

        Assert.IsFalse(result.Completed);
        Assert.AreNotEqual(ResearchWorkspaceOperationStatus.Succeeded, result.Status);
    }

    private static VerifiedResearchWorkspaceScreeningConduct InitializeConduct(
        TestWorkspace workspace,
        VerifiedResearchWorkspaceScreeningAuthorityPackage package,
        int requiredReviewCount)
    {
        var binding = ScreeningCorpusBindingAuthority.Create(
            "binding-desktop-screening",
            package.SourceResultAuthority,
            package.DeduplicationAuthorityChain.CurrentSnapshot);
        var policy = ScreeningCorpusBindingAuthority.CreateConductPolicy(
            binding,
            package.SourceResultAuthority,
            "policy-desktop-screening",
            "candidate-set-desktop-screening",
            package.Protocol,
            package.Criteria,
            requiredReviewCount,
            [
                new ScreeningConductRoleAssignment("alice", "reviewer"),
                new ScreeningConductRoleAssignment("bob", "reviewer"),
                new ScreeningConductRoleAssignment("carol", "chair")
            ],
            ["chair"],
            [new ScreeningExclusionReason("wrong-population", ScreeningStages.TitleAbstract)],
            new ScreeningConductActor("alice", ScreeningConductActorKinds.Human, "reviewer"),
            FixedTime.AddMinutes(1)).Policy;
        var header = ScreeningConductHeader.Create(
            "conduct-desktop-screening",
            policy,
            new ScreeningConductActor("alice", ScreeningConductActorKinds.Human, "reviewer"),
            FixedTime.AddMinutes(1));
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var commit = ResearchWorkspaceScreeningConductTransaction.Commit(
            workspace.Location, project, package.Deduplication, package.Protocol,
            package.Criteria, policy, header, [],
            corpusBinding: binding,
            sourceAuthority: package.SourceResultAuthority,
            corpusSnapshot: package.DeduplicationAuthorityChain.CurrentSnapshot);
        return ResearchWorkspaceScreeningConductVerifier.VerifyCurrent(
            workspace.Location, commit.Project, package.Deduplication,
            package.Protocol, package.Criteria, package.SourceResultAuthority,
            package.DeduplicationAuthorityChain.CurrentSnapshot);
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
