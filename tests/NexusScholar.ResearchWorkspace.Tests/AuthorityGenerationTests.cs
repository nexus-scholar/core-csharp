using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.ResearchWorkspace.Tests;

[TestClass]
public sealed class AuthorityGenerationTests
{
    private static readonly IClock Clock = new FixedClock(new DateTimeOffset(2026, 7, 14, 15, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public void InitializeAuthorityGeneration_commits_canonical_generation_and_project_pointer_last()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var commit = Initialize(workspace);
        var reopened = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        Assert.AreEqual(commit.Project.CurrentAuthorityGenerationId, reopened.CurrentAuthorityGenerationId);
        Assert.AreEqual(commit.Project.AuthorityGenerationManifestSha256, reopened.AuthorityGenerationManifestSha256);
        Assert.AreEqual(3, commit.Manifest.Artifacts.Count);
        Assert.AreEqual(0, commit.BaselineSnapshot.DecisionReferences.Count);
        var verifiedGeneration = ResearchWorkspaceAuthorityGenerationVerifier.VerifyCurrent(
            workspace.Location,
            reopened,
            DeduplicationAuthorityDigests.CreateResultDigestMaterial(workspace.Analysis.DeduplicationResult));
        Assert.IsNotNull(verifiedGeneration);
        Assert.AreEqual(commit.BaselineSnapshot.RecordDigest, verifiedGeneration.Snapshot.RecordDigest);

        foreach (var artifact in commit.Manifest.Artifacts)
        {
            var bytes = File.ReadAllBytes(ResearchWorkspacePaths.InProject(workspace.Root, artifact.RelativePath));
            Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf);
            Assert.IsFalse(bytes.Length > 0 && bytes[^1] == (byte)'\n');
        }

        var activeError = Assert.ThrowsExactly<ResearchWorkspaceAuthorityGenerationActiveException>(() =>
            ResearchWorkspaceTransaction.AnalyzeAndCommit(workspace.Location, reopened));
        Assert.AreEqual(ResearchWorkspaceAuthorityGenerationActiveException.StableCategory, activeError.Category);
        Assert.ThrowsExactly<ResearchWorkspaceAuthorityGenerationActiveException>(() =>
            ResearchWorkspaceTransaction.CommitImport(workspace.Location, reopened, null!, Array.Empty<byte>(), null!, "csv"));
    }

    [TestMethod]
    public void InitializeAuthorityGeneration_rejects_stale_writer_without_publishing_generation()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var stale = workspace.AnalyzedProject;
        ResearchWorkspaceStore.WriteProject(workspace.Location, stale with { Revision = stale.Revision + 1 });

        Assert.ThrowsExactly<ResearchWorkspaceConcurrencyException>(() => Initialize(workspace, stale));

        var current = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.IsNull(current.CurrentAuthorityGenerationId);
        Assert.AreEqual(0, Directory.Exists(ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspacePaths.AuthorityGenerations))
            ? Directory.GetDirectories(ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspacePaths.AuthorityGenerations)).Length
            : 0);
    }

    [TestMethod]
    public void InitializeAuthorityGeneration_quarantines_promoted_generation_when_pointer_write_does_not_complete()
    {
        using var workspace = Workspace.CreateAnalyzed();

        Assert.ThrowsExactly<InjectedFailure>(() => Initialize(
            workspace,
            faultInjector: point =>
            {
                if (point == ResearchWorkspaceAuthorityFaultPoint.AfterPromotion)
                {
                    throw new InjectedFailure();
                }
            }));

        var current = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.IsNull(current.CurrentAuthorityGenerationId);
        var quarantine = ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspacePaths.GenerationQuarantine);
        Assert.IsTrue(Directory.Exists(quarantine));
        Assert.AreEqual(1, Directory.GetDirectories(quarantine).Length);
    }

    [TestMethod]
    public void InitializeAuthorityGeneration_rejects_verified_result_not_committed_by_current_analysis()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var expected = workspace.AnalyzedProject;
        var manifestBytes = File.ReadAllBytes(ResearchWorkspacePaths.InProject(workspace.Root, expected.GenerationManifestPath!));
        var otherResult = workspace.Analysis.DeduplicationResult with { ResultId = "uncommitted-result" };
        var verifiedOther = DeduplicationAuthorityDigests.CreateResultDigestMaterial(otherResult);

        Assert.ThrowsExactly<ResearchWorkspaceConcurrencyException>(() =>
            ResearchWorkspaceTransaction.InitializeAuthorityGeneration(
                workspace.Location,
                expected,
                expected.CurrentGenerationId!,
                ContentDigest.Sha256(manifestBytes).ToString(),
                "snapshot-uncommitted",
                verifiedOther,
                BuildPolicy(verifiedOther.Result.PolicyId!),
                "alice",
                "owner",
                Clock,
                new FixedIdGenerator(Guid.Parse("00000000-0000-0000-0000-000000000702"))));
    }

    [TestMethod]
    public void InitializeAuthorityGeneration_recovers_orphaned_promoted_generation_before_commit()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var orphan = ResearchWorkspacePaths.InProject(
            workspace.Root,
            $"{ResearchWorkspacePaths.AuthorityGenerations}/authority-orphaned");
        Directory.CreateDirectory(orphan);
        File.WriteAllText(Path.Combine(orphan, "partial.json"), "{}");

        _ = Initialize(workspace);

        Assert.IsFalse(Directory.Exists(orphan));
        Assert.IsTrue(Directory.Exists(ResearchWorkspacePaths.InProject(
            workspace.Root,
            $"{ResearchWorkspacePaths.GenerationQuarantine}/authority-orphaned")));
    }

    [TestMethod]
    public void CommitDeduplicationDecision_publishes_verified_successor_and_reopens_chain()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var baseline = Initialize(workspace);
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(workspace.Analysis.DeduplicationResult);
        var (command, target) = BuildCommand(workspace, baseline.Project, source);

        var commit = ResearchWorkspaceTransaction.CommitDeduplicationDecision(
            workspace.Location, baseline.Project, source, command, target, Clock,
            new SequenceIdGenerator(711));
        var reopened = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(workspace.Location, reopened, source);

        Assert.IsFalse(commit.AlreadyApplied);
        Assert.AreEqual(baseline.Project.Revision + 1, reopened.Revision);
        Assert.AreEqual(command.RequestDigest.ToString(), commit.Manifest.RequestDigest);
        Assert.AreEqual(commit.Snapshot.RecordDigest, chain.CurrentSnapshot.RecordDigest);
        Assert.AreEqual(1, chain.Transitions.Count);
        Assert.AreEqual(1, chain.ActiveDecisions.Count);
        Assert.AreEqual(8, commit.Manifest.Artifacts.Count);
    }

    [TestMethod]
    public void CommitDeduplicationDecision_exact_replay_returns_stored_transition_without_time_or_ids()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var baseline = Initialize(workspace);
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(workspace.Analysis.DeduplicationResult);
        var (command, target) = BuildCommand(workspace, baseline.Project, source);
        var first = ResearchWorkspaceTransaction.CommitDeduplicationDecision(
            workspace.Location, baseline.Project, source, command, target, Clock,
            new SequenceIdGenerator(712));

        var replay = ResearchWorkspaceTransaction.CommitDeduplicationDecision(
            workspace.Location, first.Project, source, command, target, new ThrowingClock(), new ThrowingIdGenerator());

        Assert.IsTrue(replay.AlreadyApplied);
        Assert.AreEqual(first.Project.Revision, replay.Project.Revision);
        Assert.AreEqual(first.Decision.DecisionDigest, replay.Decision.DecisionDigest);
        Assert.AreEqual(first.Snapshot.RecordDigest, replay.Snapshot.RecordDigest);
    }

    [TestMethod]
    public void CommitDeduplicationDecision_quarantines_promoted_successor_when_pointer_write_fails()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var baseline = Initialize(workspace);
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(workspace.Analysis.DeduplicationResult);
        var (command, target) = BuildCommand(workspace, baseline.Project, source);

        Assert.ThrowsExactly<InjectedFailure>(() => ResearchWorkspaceTransaction.CommitDeduplicationDecision(
            workspace.Location, baseline.Project, source, command, target, Clock,
            new SequenceIdGenerator(713),
            point => { if (point == ResearchWorkspaceAuthorityFaultPoint.AfterPromotion) throw new InjectedFailure(); }));

        var reopened = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.AreEqual(baseline.Project.CurrentAuthorityGenerationId, reopened.CurrentAuthorityGenerationId);
        Assert.AreEqual(baseline.Project.Revision, reopened.Revision);
        Assert.AreEqual(1, Directory.GetDirectories(ResearchWorkspacePaths.InProject(
            workspace.Root, ResearchWorkspacePaths.GenerationQuarantine)).Length);
    }

    private static (VerifiedDeduplicationReviewCommand Command, VerifiedDeduplicationAuthorityReviewTargetDigest Target) BuildCommand(
        Workspace workspace,
        ResearchWorkspaceProject project,
        VerifiedDeduplicationAuthorityResultDigest source)
    {
        var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(workspace.Location, project, source);
        var policy = chain.Policy;
        var pair = source.Result.ReviewRequiredCandidates.Single();
        var ids = new[] { pair.CandidateAId, pair.CandidateBId }.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var evidence = source.Result.Evidence.Where(item => item.ObjectCandidateId is not null &&
            ids.Contains(item.SubjectCandidateId, StringComparer.Ordinal) && ids.Contains(item.ObjectCandidateId, StringComparer.Ordinal) &&
            item.SubjectCandidateId != item.ObjectCandidateId).ToArray();
        var target = DeduplicationAuthorityDigests.CreateReviewTargetDigestMaterial(source, pair, ids, evidence);
        var material = new UnverifiedDeduplicationReviewCommand(
            DeduplicationReviewCommandConstants.SchemaId,
            DeduplicationReviewCommandConstants.SchemaVersion,
            project.CurrentAuthorityGenerationId!,
            ContentDigest.Parse(project.AuthorityGenerationManifestSha256!),
            chain.CurrentSnapshot.DecisionSetDigest,
            source.Result.ResultId,
            source.ResultDigest,
            chain.CurrentSnapshot.SnapshotId,
            chain.CurrentSnapshot.RecordDigest,
            target.TargetKind,
            target.TargetId,
            target.TargetDigest,
            policy.PolicyId,
            policy.PolicyVersion,
            policy.PolicyDigest,
            DeduplicationAuthorityPolicyConstants.MergeAction,
            "duplicate",
            "Reviewed as duplicate.",
            "alice",
            "owner",
            null,
            null);
        return (DeduplicationReviewCommand.Create(
            material, policy, source, target, chain.CurrentSnapshot.DecisionSetDigest,
            chain.GenerationId, ContentDigest.Parse(project.AuthorityGenerationManifestSha256!),
            chain.CurrentSnapshot.SnapshotId, chain.CurrentSnapshot.RecordDigest), target);
    }

    private static ResearchWorkspaceAuthorityCommit Initialize(
        Workspace workspace,
        ResearchWorkspaceProject? expected = null,
        Action<ResearchWorkspaceAuthorityFaultPoint>? faultInjector = null)
    {
        expected ??= workspace.AnalyzedProject;
        var manifestBytes = File.ReadAllBytes(ResearchWorkspacePaths.InProject(workspace.Root, expected.GenerationManifestPath!));
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(workspace.Analysis.DeduplicationResult);
        var policy = BuildPolicy(source.Result.PolicyId!);
        return ResearchWorkspaceTransaction.InitializeAuthorityGeneration(
            workspace.Location,
            expected,
            expected.CurrentGenerationId!,
            ContentDigest.Sha256(manifestBytes).ToString(),
            "snapshot-fe01-baseline",
            source,
            policy,
            "alice",
            "owner",
            Clock,
            new FixedIdGenerator(Guid.Parse("00000000-0000-0000-0000-000000000701")),
            faultInjector);
    }

    private static VerifiedDeduplicationAuthorityPolicy BuildPolicy(string policyId) =>
        DeduplicationAuthorityPolicy.CreatePolicyMaterial(new UnverifiedDeduplicationAuthorityPolicy(
            DeduplicationAuthorityPolicyConstants.SchemaId,
            DeduplicationAuthorityPolicyConstants.SchemaVersion,
            DeduplicationAuthorityPolicyConstants.LocalAuthoritySourceKind,
            policyId,
            DeduplicationService.PolicyVersion,
            new[] { new DeduplicationAuthorityPolicyActorRole("alice", "owner") },
            DeduplicationAuthorityPolicyConstants.ClosedActions,
            new[]
            {
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MergeAction, new[] { "duplicate" }),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.KeepSeparateAction, new[] { "different" }),
                new DeduplicationAuthorityPolicyReasonGroup(DeduplicationAuthorityPolicyConstants.MarkUnresolvedAction, new[] { "uncertain" })
            },
            false,
            "alice",
            "owner",
            Clock.UtcNow));

    private sealed class Workspace : IDisposable
    {
        private Workspace(string root, ResearchWorkspaceLocation location, ResearchWorkspaceAnalysisCommit commit)
        {
            Root = root;
            Location = location;
            Analysis = commit.Analysis;
            AnalyzedProject = commit.Project;
        }

        public string Root { get; }
        public ResearchWorkspaceLocation Location { get; }
        public ResearchWorkspaceAnalysisResult Analysis { get; }
        public ResearchWorkspaceProject AnalyzedProject { get; }

        public static Workspace CreateAnalyzed()
        {
            var root = Path.Combine(Path.GetTempPath(), $"nexus-fe01-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            var project = ResearchWorkspaceProject.Create("FE-01", Clock.UtcNow);
            var projectPath = ResearchWorkspacePaths.ProjectFile(root);
            var location = new ResearchWorkspaceLocation(root, projectPath);
            foreach (var directory in ResearchWorkspacePaths.RequiredDirectories)
            {
                Directory.CreateDirectory(ResearchWorkspacePaths.InProject(root, directory));
            }

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
            var commit = ResearchWorkspaceTransaction.AnalyzeAndCommit(location, project);
            return new Workspace(root, location, commit);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
    }

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed class FixedIdGenerator(Guid value) : IIdGenerator
    {
        public Guid NewId() => value;
    }

    private sealed class ThrowingClock : IClock
    {
        public DateTimeOffset UtcNow => throw new InvalidOperationException("Replay consumed the clock.");
    }

    private sealed class ThrowingIdGenerator : IIdGenerator
    {
        public Guid NewId() => throw new InvalidOperationException("Replay consumed an id.");
    }

    private sealed class SequenceIdGenerator(int value) : IIdGenerator
    {
        private int _value = value;
        public Guid NewId() => Guid.Parse($"00000000-0000-0000-0000-{_value++:000000000000}");
    }

    private sealed class InjectedFailure : Exception;
}
