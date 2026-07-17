using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Cli.Tests;

[TestClass]
public sealed class DeduplicationDecideCommandTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Dedup_decide_previews_without_mutation_then_commits_with_confirmation()
    {
        using var workspace = TestWorkspace.Create();
        var before = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var args = new[]
        {
            "dedup", "decide", "--target", workspace.TargetId,
            "--action", DeduplicationAuthorityPolicyConstants.MergeAction,
            "--reason", "duplicate", "--rationale", "Reviewed as duplicate.",
            "--actor", "alice", "--role", "owner"
        };

        var previewExit = Run(workspace.Root, args, out var preview, out var previewError);
        var afterPreview = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        Assert.AreEqual(0, previewExit, previewError);
        Assert.AreEqual(string.Empty, previewError);
        StringAssert.Contains(preview, "Status: preview-only");
        StringAssert.Contains(preview, "Membership changes: true");
        Assert.AreEqual(before.Revision, afterPreview.Revision);
        Assert.AreEqual(before.AuthorityGenerationManifestSha256, afterPreview.AuthorityGenerationManifestSha256);

        var commitExit = Run(workspace.Root, args.Append("--confirm").ToArray(), out var committed, out var commitError);
        var afterCommit = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(workspace.Location, afterCommit, workspace.Source);

        Assert.AreEqual(0, commitExit, commitError);
        Assert.AreEqual(string.Empty, commitError);
        StringAssert.Contains(committed, "Status: committed");
        Assert.AreEqual(before.Revision + 1, afterCommit.Revision);
        Assert.AreEqual(1, chain.Transitions.Count);
    }

    [TestMethod]
    public void Dedup_decide_requires_explicit_actor_target_action_reason_and_role()
    {
        using var workspace = TestWorkspace.Create();

        var exit = Run(workspace.Root, new[] { "dedup", "decide", "--confirm" }, out var output, out var error);

        Assert.AreEqual(1, exit);
        Assert.AreEqual(string.Empty, output);
        StringAssert.Contains(error, "Required: --target, --action, --reason, --actor, and --role");
    }

    [TestMethod]
    [DataRow(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, "different")]
    [DataRow(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, "uncertain")]
    public void Dedup_decide_previews_and_commits_non_membership_actions(string action, string reason)
    {
        using var workspace = TestWorkspace.Create();
        var args = new[]
        {
            "dedup", "decide", "--target", workspace.TargetId, "--action", action,
            "--reason", reason, "--actor", "alice", "--role", "owner", "--confirm"
        };

        var exit = Run(workspace.Root, args, out var output, out var error);
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(workspace.Location, project, workspace.Source);

        Assert.AreEqual(0, exit, error);
        Assert.AreEqual(string.Empty, error);
        StringAssert.Contains(output, "Membership changes: false");
        StringAssert.Contains(output, "Status: committed");
        Assert.AreEqual(action, chain.ActiveDecisions.Single().ActionType);
    }

    [TestMethod]
    public void Structured_review_operation_inspects_previews_and_commits_the_verified_authority_chain()
    {
        using var workspace = TestWorkspace.Create();
        var queue = ResearchWorkspaceDeduplicationReview.Inspect(workspace.Root);
        var request = new ResearchWorkspaceDeduplicationReviewRequest(
            workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.MergeAction,
            "duplicate", "Reviewed as duplicate.", "alice", "owner", null, Now);

        var preview = ResearchWorkspaceDeduplicationReview.Preview(request);
        var before = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var committed = ResearchWorkspaceDeduplicationReview.Commit(preview);
        var after = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        Assert.IsTrue(queue.Completed, queue.Message);
        Assert.AreEqual(workspace.TargetId, queue.Targets.Single().TargetId);
        CollectionAssert.Contains(queue.Policy!.AllowedActions.ToList(), DeduplicationAuthorityPolicyConstants.MergeAction);
        Assert.IsTrue(preview.IsReady, preview.Message);
        Assert.AreEqual("alice", preview.ActorId);
        Assert.IsTrue(preview.MembershipChanges);
        Assert.IsTrue(committed.Completed, committed.Message);
        Assert.AreEqual(before.Revision + 1, after.Revision);
        Assert.IsNotNull(committed.DecisionId);
    }

    [TestMethod]
    public void Structured_review_preview_rejects_unauthorized_actor_role()
    {
        using var workspace = TestWorkspace.Create();

        var preview = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.MergeAction,
                "duplicate", "Reviewed as duplicate.", "mallory", "owner", null, Now));

        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, preview.Status);
        StringAssert.Contains(preview.Message, DeduplicationReviewCommandErrorCodes.UnauthorizedActor);
    }

    [TestMethod]
    public void Structured_review_commit_rejects_changed_preview_binding_as_stale()
    {
        using var workspace = TestWorkspace.Create();
        var preview = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.KeepSeparateAction,
                "different", null, "alice", "owner", null, Now));

        var result = ResearchWorkspaceDeduplicationReview.Commit(
            preview with { ExpectedProjectRevision = preview.ExpectedProjectRevision + 1 });

        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale, result.Status);
        var project = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.AreEqual(preview.ExpectedProjectRevision, project.Revision);
    }

    [TestMethod]
    public void Structured_review_commit_reports_workspace_lock_as_recovery_required()
    {
        using var workspace = TestWorkspace.Create();
        var preview = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction,
                "uncertain", null, "alice", "owner", null, Now));
        using var workspaceLock = new FileStream(
            Path.Combine(workspace.Root, ResearchWorkspacePaths.ProjectLockFileName),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var result = ResearchWorkspaceDeduplicationReview.Commit(preview);

        Assert.AreEqual(ResearchWorkspaceOperationStatus.RecoveryRequired, result.Status);
    }

    [TestMethod]
    public void Structured_review_queue_requires_exact_supersession_after_reopen()
    {
        using var workspace = TestWorkspace.Create();
        var first = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.KeepSeparateAction,
                "different", null, "alice", "owner", null, Now));
        var firstCommit = ResearchWorkspaceDeduplicationReview.Commit(first);

        var reopened = ResearchWorkspaceDeduplicationReview.Inspect(workspace.Root);
        var active = reopened.Targets.Single().ActiveDecisions.Single();
        var freshSecond = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction,
                "uncertain", null, "alice", "owner", null, Now.AddMinutes(1)));
        var correction = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction,
                "uncertain", null, "alice", "owner", active.DecisionId, Now.AddMinutes(1)));

        Assert.IsTrue(firstCommit.Completed, firstCommit.Message);
        Assert.AreEqual(firstCommit.DecisionId, active.DecisionId);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Failed, freshSecond.Status);
        StringAssert.Contains(freshSecond.Message, "exact active decision id");
        Assert.IsTrue(correction.IsReady, correction.Message);
        Assert.AreEqual(active.DecisionDigest, correction.SupersedesDecisionDigest);
    }

    [TestMethod]
    public void Structured_review_exact_preview_retry_is_idempotent()
    {
        using var workspace = TestWorkspace.Create();
        var preview = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.MergeAction,
                "duplicate", "Reviewed as duplicate.", "alice", "owner", null, Now));

        var first = ResearchWorkspaceDeduplicationReview.Commit(preview);
        var afterFirst = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var replay = ResearchWorkspaceDeduplicationReview.Commit(preview);
        var afterReplay = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        Assert.IsTrue(first.Completed, first.Message);
        Assert.IsTrue(replay.Completed, replay.Message);
        Assert.IsTrue(replay.AlreadyApplied);
        Assert.AreEqual(first.DecisionId, replay.DecisionId);
        Assert.AreEqual(afterFirst.Revision, afterReplay.Revision);
    }

    [TestMethod]
    public void Structured_review_commit_classifies_same_target_authority_race_as_stale()
    {
        using var workspace = TestWorkspace.Create();
        var firstCaller = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.KeepSeparateAction,
                "different", null, "alice", "owner", null, Now));
        var secondCaller = ResearchWorkspaceDeduplicationReview.Preview(
            new ResearchWorkspaceDeduplicationReviewRequest(
                workspace.Root, workspace.TargetId, DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction,
                "uncertain", null, "alice", "owner", null, Now));

        var winningCommit = ResearchWorkspaceDeduplicationReview.Commit(secondCaller);
        var staleCommit = ResearchWorkspaceDeduplicationReview.Commit(firstCaller);

        Assert.IsTrue(winningCommit.Completed, winningCommit.Message);
        Assert.AreEqual(ResearchWorkspaceOperationStatus.Stale, staleCommit.Status);
        var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(
            workspace.Location,
            ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath),
            workspace.Source);
        Assert.AreEqual(1, chain.Transitions.Count);
    }

    private static int Run(string root, string[] args, out string output, out string error)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exit = CliApplication.Run(args, stdout, stderr, root, () => Now);
        output = stdout.ToString();
        error = stderr.ToString();
        return exit;
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root, ResearchWorkspaceLocation location,
            VerifiedDeduplicationAuthorityResultDigest source, string targetId)
        {
            Root = root;
            Location = location;
            Source = source;
            TargetId = targetId;
        }

        public string Root { get; }
        public ResearchWorkspaceLocation Location { get; }
        public VerifiedDeduplicationAuthorityResultDigest Source { get; }
        public string TargetId { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), $"nexus-fe02-cli-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var location = new ResearchWorkspaceLocation(root, ResearchWorkspacePaths.ProjectFile(root));
            foreach (var directory in ResearchWorkspacePaths.RequiredDirectories)
                Directory.CreateDirectory(ResearchWorkspacePaths.InProject(root, directory));
            var project = ResearchWorkspaceProject.Create("FE-02 CLI", Now);
            var relative = $"{ResearchWorkspacePaths.SearchInputs}/input.csv";
            var bytes = System.Text.Encoding.UTF8.GetBytes(
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
                    new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, new[] { "duplicate" }),
                    new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, new[] { "different" }),
                    new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, new[] { "uncertain" })
                },
                false, "alice", "owner", Now));
            var manifestBytes = File.ReadAllBytes(ResearchWorkspacePaths.InProject(root, analysis.Project.GenerationManifestPath!));
            _ = ResearchWorkspaceTransaction.InitializeAuthorityGeneration(
                location, analysis.Project, analysis.Project.CurrentGenerationId!, ContentDigest.Sha256(manifestBytes).ToString(),
                "snapshot-cli-baseline", source, policy, "alice", "owner", new FixedClock(),
                new FixedIdGenerator());
            var pair = source.Result.ReviewRequiredCandidates.Single();
            var ids = new[] { pair.CandidateAId, pair.CandidateBId }.OrderBy(item => item, StringComparer.Ordinal).ToArray();
            var evidence = source.Result.Evidence.Where(item => item.ObjectCandidateId is not null &&
                ids.Contains(item.SubjectCandidateId, StringComparer.Ordinal) && ids.Contains(item.ObjectCandidateId, StringComparer.Ordinal) &&
                item.SubjectCandidateId != item.ObjectCandidateId).ToArray();
            var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(source, pair, ids, evidence);
            return new TestWorkspace(root, location, source, target.TargetId);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class FixedIdGenerator : IIdGenerator
    {
        private int _value = 721;
        public Guid NewId() => Guid.Parse($"00000000-0000-0000-0000-{_value++:000000000000}");
    }
}
