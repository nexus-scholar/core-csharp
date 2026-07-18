using System.Diagnostics;
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
    public void AnalyzeAndCommit_rejects_mutated_search_export_after_lock_and_does_not_publish()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var expected = workspace.AnalyzedProject;
        var inputPath = ResearchWorkspacePaths.InProject(workspace.Root, expected.Inputs[0].RelativePath!);
        var generationsRoot = ResearchWorkspacePaths.InProject(workspace.Root, ResearchWorkspacePaths.Generations);
        var beforeGenerations = Directory.Exists(generationsRoot)
            ? Directory.GetDirectories(generationsRoot).OrderBy(item => item, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

        Assert.ThrowsExactly<ResearchWorkspaceDigestMismatchException>(() =>
            ResearchWorkspaceTransaction.AnalyzeAndCommit(
                workspace.Location,
                expected,
                point =>
                {
                    if (point == ResearchWorkspaceAnalysisFaultPoint.AfterLockAcquired)
                    {
                        File.WriteAllText(inputPath, "mutated input");
                    }
                }));

        var current = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
        Assert.AreEqual(expected.CurrentGenerationId, current.CurrentGenerationId);
        Assert.AreEqual(expected.Revision, current.Revision);
        var afterGenerations = Directory.Exists(generationsRoot)
            ? Directory.GetDirectories(generationsRoot).OrderBy(item => item, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        CollectionAssert.AreEqual(beforeGenerations, afterGenerations);
    }

    [TestMethod]
    public void AnalyzeAndCommit_leases_snapshotted_inputs_through_promotion_and_revalidates_before_publish()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var expected = workspace.AnalyzedProject;
        var inputPath = ResearchWorkspacePaths.InProject(workspace.Root, expected.Inputs[0].RelativePath!);
        var mutationWasBlocked = false;
        ResearchWorkspaceAnalysisCommit? commit = null;
        ResearchWorkspaceDigestMismatchException? rejection = null;

        try
        {
            commit = ResearchWorkspaceTransaction.AnalyzeAndCommit(
                workspace.Location,
                expected,
                point =>
                {
                    if (point != ResearchWorkspaceAnalysisFaultPoint.AfterPromotionBeforeFinalInputValidation)
                    {
                        return;
                    }

                    try
                    {
                        File.WriteAllText(inputPath, "mutated after promotion");
                    }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                    {
                        mutationWasBlocked = true;
                    }
                });
        }
        catch (ResearchWorkspaceDigestMismatchException exception)
        {
            rejection = exception;
        }

        Assert.IsTrue(
            mutationWasBlocked || rejection is not null,
            "A mutation after generation promotion must be blocked by the input lease or rejected by final revalidation.");
        if (mutationWasBlocked)
        {
            Assert.IsNotNull(commit);
            Assert.AreEqual(expected.Revision + 1, commit.Project.Revision);
            Assert.AreEqual(
                expected.Inputs[0].Sha256,
                ContentDigest.Sha256(File.ReadAllBytes(inputPath)).ToString());
        }
        else
        {
            Assert.IsNull(commit);
            var current = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
            Assert.AreEqual(expected.Revision, current.Revision);
            Assert.AreEqual(expected.CurrentGenerationId, current.CurrentGenerationId);
        }
    }

    [TestMethod]
    public void AnalyzeAndCommit_rejects_reparse_swap_between_path_validation_and_open()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var expected = workspace.AnalyzedProject;
        var inputPath = ResearchWorkspacePaths.InProject(workspace.Root, expected.Inputs[0].RelativePath!);
        var inputDirectory = Path.GetDirectoryName(inputPath)!;
        var backupDirectory = inputDirectory + ".race-backup";
        var externalRoot = Path.Combine(Path.GetTempPath(), $"nexus-rw-input-race-{Guid.NewGuid():N}");
        var externalInputDirectory = Path.Combine(externalRoot, "search-001");
        var externalInputPath = Path.Combine(externalInputDirectory, Path.GetFileName(inputPath));
        Directory.CreateDirectory(externalInputDirectory);
        File.Copy(inputPath, externalInputPath);
        var swapped = false;

        try
        {
            Assert.ThrowsExactly<ResearchWorkspaceConcurrencyException>(() =>
                ResearchWorkspaceTransaction.AnalyzeAndCommit(
                    workspace.Location,
                    expected,
                    point =>
                    {
                        if (point != ResearchWorkspaceAnalysisFaultPoint.AfterInputPathValidatedBeforeOpen || swapped)
                        {
                            return;
                        }

                        Directory.Move(inputDirectory, backupDirectory);
                        if (OperatingSystem.IsWindows())
                        {
                            using var process = Process.Start(new ProcessStartInfo(
                                "cmd.exe",
                                $"/c mklink /J \"{inputDirectory}\" \"{externalInputDirectory}\"")
                            {
                                CreateNoWindow = true,
                                UseShellExecute = false
                            });
                            process!.WaitForExit();
                            Assert.AreEqual(0, process.ExitCode);
                        }
                        else
                        {
                            Directory.CreateSymbolicLink(inputDirectory, externalInputDirectory);
                        }

                        swapped = true;
                    }));

            var current = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);
            Assert.AreEqual(expected.Revision, current.Revision);
            Assert.AreEqual(expected.CurrentGenerationId, current.CurrentGenerationId);
        }
        finally
        {
            if (swapped && Directory.Exists(inputDirectory))
            {
                Directory.Delete(inputDirectory);
            }

            if (Directory.Exists(backupDirectory))
            {
                Directory.Move(backupDirectory, inputDirectory);
            }

            if (Directory.Exists(externalRoot))
            {
                Directory.Delete(externalRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CommitDeduplicationDecision_clears_successor_bound_downstream_state_and_preserves_history_directories()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var baseline = Initialize(workspace);
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(workspace.Analysis.DeduplicationResult);
        var current = baseline.Project;

        var workflowGenerationId = "execution-old";
        var workflowRelativePath = $"{ResearchWorkspacePaths.WorkflowExecutionJournalRoot("existing-execution", workflowGenerationId)}/manifest.json";
        var workflowManifestPath = ResearchWorkspacePaths.InProject(workspace.Root, workflowRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(workflowManifestPath)!);
        File.WriteAllText(workflowManifestPath, "workflow-historic-manifest");

        var screeningConductGenerationId = "conduct-old";
        var screeningConductRelativePath = $"{ResearchWorkspacePaths.ScreeningConductRoot("existing-conduct", screeningConductGenerationId)}/manifest.json";
        var screeningConductManifestPath = ResearchWorkspacePaths.InProject(workspace.Root, screeningConductRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(screeningConductManifestPath)!);
        File.WriteAllText(screeningConductManifestPath, "screening-conduct-historic-manifest");

        var screeningAuthorityPackageGenerationId = "authority-package-old";
        var screeningAuthorityPackageRelativePath = $"{ResearchWorkspacePaths.ScreeningAuthorityPackageRoot(screeningAuthorityPackageGenerationId)}/manifest.json";
        var screeningAuthorityPackageManifestPath = ResearchWorkspacePaths.InProject(workspace.Root, screeningAuthorityPackageRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(screeningAuthorityPackageManifestPath)!);
        File.WriteAllText(screeningAuthorityPackageManifestPath, "screening-authority-package-historic-manifest");

        var fullTextCandidateId = "candidate-old";
        var fullTextGenerationId = "fulltext-old";
        var fullTextRelativePath = $"{ResearchWorkspacePaths.FullTextGenerationRoot(fullTextCandidateId, fullTextGenerationId)}/manifest.json";
        var fullTextManifestPath = ResearchWorkspacePaths.InProject(workspace.Root, fullTextRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullTextManifestPath)!);
        File.WriteAllText(fullTextManifestPath, "full-text-historic-manifest");

        var reportingGenerationId = "reporting-old";
        var reportingRelativePath = $"{ResearchWorkspacePaths.ReportingWorkflowGenerationRoot(reportingGenerationId)}/manifest.json";
        var reportingManifestPath = ResearchWorkspacePaths.InProject(workspace.Root, reportingRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportingManifestPath)!);
        File.WriteAllText(reportingManifestPath, "reporting-historic-manifest");

        current = current with
        {
            CurrentWorkflowExecutionJournalGenerationId = workflowGenerationId,
            WorkflowExecutionJournalManifestPath = workflowRelativePath,
            WorkflowExecutionJournalManifestSha256 = ContentDigest.Sha256(File.ReadAllBytes(workflowManifestPath)).ToString(),
            CurrentScreeningConductGenerationId = screeningConductGenerationId,
            ScreeningConductManifestPath = screeningConductRelativePath,
            ScreeningConductManifestSha256 = ContentDigest.Sha256(File.ReadAllBytes(screeningConductManifestPath)).ToString(),
            CurrentScreeningAuthorityPackageGenerationId = screeningAuthorityPackageGenerationId,
            ScreeningAuthorityPackageManifestPath = screeningAuthorityPackageRelativePath,
            ScreeningAuthorityPackageManifestSha256 = ContentDigest.Sha256(File.ReadAllBytes(screeningAuthorityPackageManifestPath)).ToString(),
            CurrentFullTextGenerationId = fullTextGenerationId,
            FullTextManifestPath = fullTextRelativePath,
            FullTextManifestSha256 = ContentDigest.Sha256(File.ReadAllBytes(fullTextManifestPath)).ToString(),
            FullTextCases = new Dictionary<string, ResearchWorkspaceFullTextPointer>(StringComparer.Ordinal)
            {
                [fullTextCandidateId] = new ResearchWorkspaceFullTextPointer(
                    fullTextGenerationId,
                    fullTextRelativePath,
                    ContentDigest.Sha256(File.ReadAllBytes(fullTextManifestPath)).ToString())
            },
            CurrentReportingWorkflowGenerationId = reportingGenerationId,
            ReportingWorkflowManifestPath = reportingRelativePath,
            ReportingWorkflowManifestSha256 = ContentDigest.Sha256(File.ReadAllBytes(reportingManifestPath)).ToString()
        };
        ResearchWorkspaceStore.WriteProject(workspace.Location, current);

        var (command, target) = BuildCommand(workspace, current, source);
        _ = ResearchWorkspaceTransaction.CommitDeduplicationDecision(
            workspace.Location, current, source, command, target, Clock, new SequenceIdGenerator(720));
        var committed = ResearchWorkspaceStore.ReadProject(workspace.Location.ProjectFilePath);

        Assert.IsNull(committed.CurrentWorkflowExecutionJournalGenerationId);
        Assert.IsNull(committed.WorkflowExecutionJournalManifestPath);
        Assert.IsNull(committed.WorkflowExecutionJournalManifestSha256);
        Assert.IsNull(committed.CurrentScreeningConductGenerationId);
        Assert.IsNull(committed.ScreeningConductManifestPath);
        Assert.IsNull(committed.ScreeningConductManifestSha256);
        Assert.IsNull(committed.CurrentScreeningAuthorityPackageGenerationId);
        Assert.IsNull(committed.ScreeningAuthorityPackageManifestPath);
        Assert.IsNull(committed.ScreeningAuthorityPackageManifestSha256);
        Assert.IsNull(committed.CurrentFullTextGenerationId);
        Assert.IsNull(committed.FullTextManifestPath);
        Assert.IsNull(committed.FullTextManifestSha256);
        Assert.IsNull(committed.FullTextCases);
        Assert.IsNull(committed.CurrentReportingWorkflowGenerationId);
        Assert.IsNull(committed.ReportingWorkflowManifestPath);
        Assert.IsNull(committed.ReportingWorkflowManifestSha256);

        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(workflowManifestPath)));
        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(screeningConductManifestPath)));
        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(screeningAuthorityPackageManifestPath)));
        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(fullTextManifestPath)));
        Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(reportingManifestPath)));
        Assert.IsTrue(File.Exists(workflowManifestPath));
        Assert.IsTrue(File.Exists(screeningConductManifestPath));
        Assert.IsTrue(File.Exists(screeningAuthorityPackageManifestPath));
        Assert.IsTrue(File.Exists(fullTextManifestPath));
        Assert.IsTrue(File.Exists(reportingManifestPath));
    }

    [TestMethod]
    public void Authority_chain_remains_current_after_unrelated_project_revision_when_pointer_is_unchanged()
    {
        using var workspace = Workspace.CreateAnalyzed();
        var baseline = Initialize(workspace);
        var source = DeduplicationAuthorityDigests.CreateResultDigestMaterial(workspace.Analysis.DeduplicationResult);
        var advanced = baseline.Project with { Revision = baseline.Project.Revision + 1 };
        ResearchWorkspaceStore.WriteProject(workspace.Location, advanced);

        var chain = ResearchWorkspaceAuthorityChainVerifier.VerifyCurrent(workspace.Location, advanced, source);

        Assert.AreEqual(baseline.Project.CurrentAuthorityGenerationId, chain.GenerationId);
        Assert.IsTrue(chain.ProjectRevision < advanced.Revision);
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
