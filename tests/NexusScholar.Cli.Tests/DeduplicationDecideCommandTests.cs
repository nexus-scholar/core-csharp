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
