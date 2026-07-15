using System;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AppServices;
using NexusScholar.Deduplication;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.ResearchWorkspace;
using NexusScholar.Screening;
using NexusScholar.Screening.WorkflowExecution;
using NexusScholar.Workflow;
using NexusScholar.WorkflowExecution;
using NexusScholar.WorkflowExecution.Provenance;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class WorkflowCompilerTests
{
    private static readonly ProtocolActor Researcher = ProtocolActor.Human("researcher-1");
    private static readonly IClock Clock = new FixedClock();
    private static readonly IWorkflowExecutionRecordResolver ExecutionRecordResolver = new TestExecutionRecordResolver();
    private static readonly ConditionalWeakTable<ProtocolVersion, VerifiedProtocolVersion> ProtocolAuthorities = new();

    [TestMethod]
    public void Compile_approved_protocol_generates_deterministic_workflow_id_and_digest()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var input = BuildInput(protocol, template);
        var compiler = new WorkflowCompiler();

        var first = compiler.Compile(input);
        var second = compiler.Compile(input);

        Assert.AreEqual(first.WorkflowId, second.WorkflowId);
        Assert.AreEqual(first.WorkflowDigest, second.WorkflowDigest);
        Assert.IsTrue(first.WorkflowId.StartsWith("workflow-", StringComparison.Ordinal));
        Assert.AreEqual(25, first.WorkflowId.Length);
        Assert.AreEqual(4, first.Nodes.Count);
    }

    [TestMethod]
    public void Compile_binds_complete_protocol_decision_record()
    {
        var protocol = BuildApprovedProtocol();
        var workflow = new WorkflowCompiler().Compile(BuildInput(protocol, BuildTemplate()));
        var decision = protocol.Decisions.Single(item => item.DecisionKey == "review-type");
        var binding = workflow.ResolvedInputBindings.Single(item => item.InputId == "review-type");

        Assert.AreEqual(decision.DecisionId, binding.SourceRef);
        Assert.AreEqual(ContentDigest.Sha256CanonicalJson(decision.ToCanonicalJson()), binding.SourceDigest);
        Assert.AreEqual(ContentDigest.Sha256CanonicalJson(decision.Value), binding.ValueDigest);
    }

    [TestMethod]
    public void Workflow_definition_has_no_public_fabrication_constructor()
    {
        Assert.AreEqual(0, typeof(WorkflowDefinition).GetConstructors().Length);
        Assert.AreEqual(0, typeof(VerifiedWorkflowDefinition).GetConstructors().Length);
    }

    [TestMethod]
    public void Compiled_workflow_rehydrates_against_exact_protocol_and_template_authority()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var authority = ProtocolAuthorities.GetValue(protocol, _ => throw new InvalidOperationException());
        var compiled = new WorkflowCompiler().Compile(BuildInput(protocol, template));
        var resolver = new TestWorkflowAuthorityResolver(authority, template);

        var verified = WorkflowRehydrator.Rehydrate(WorkflowRehydrator.FromCompiled(compiled), resolver);

        Assert.AreEqual(compiled.WorkflowId, verified.Definition.WorkflowId);
        Assert.AreEqual(compiled.WorkflowDigest, verified.Definition.WorkflowDigest);
        Assert.AreSame(authority, verified.ProtocolAuthority);
        Assert.AreNotSame(template, verified.ResolvedTemplate);
        Assert.AreEqual(template.TemplateDigest, verified.ResolvedTemplate.TemplateDigest);
    }

    [TestMethod]
    public void Execution_journal_replays_hash_chained_attempt_and_dependency_history()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var header = WorkflowExecutionHeader.Create("execution-1", authority, policy, researcher, Clock.UtcNow);
        var journal = WorkflowExecutionJournal.Create(header, authority, policy, ExecutionRecordResolver);

        Assert.AreEqual(WorkflowExecutionState.Ready, journal.Projection.NodeStates["start"]);
        Assert.AreEqual(WorkflowExecutionState.Pending, journal.Projection.NodeStates["approve"]);

        Append(journal, "start-1", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, attemptId: "attempt-1", attemptSequence: 1);
        Append(journal, "complete-1", "start", WorkflowExecutionEventKind.WorkCompleted,
            WorkflowExecutionState.Active, WorkflowExecutionState.Completed, runner, attemptId: "attempt-1", attemptSequence: 1,
            outputs: new[] { Ref("workflow-artifact", "search-plan") });
        Append(journal, "ready-approve", "approve", WorkflowExecutionEventKind.DependenciesSatisfied,
            WorkflowExecutionState.Pending, WorkflowExecutionState.Ready, researcher);

        var replay = WorkflowExecutionJournal.Rehydrate(header, journal.Events, authority, policy, ExecutionRecordResolver);

        Assert.AreEqual(journal.Projection.HeadDigest, replay.Projection.HeadDigest);
        Assert.AreEqual(WorkflowExecutionState.Ready, replay.Projection.NodeStates["approve"]);
        Assert.AreEqual(1, replay.Projection.Attempts["start"].Count);
        Assert.AreEqual(WorkflowExecutionState.Completed, replay.Projection.Attempts["start"][0].State);
    }

    [TestMethod]
    public void Screening_decision_and_human_task_completion_commit_as_one_workspace_revision()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nexus-screening-workflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var location = new ResearchWorkspaceLocation(root, Path.Combine(root, ResearchWorkspacePaths.ProjectFileName));
            var project = ResearchWorkspaceProject.Create("Screening workflow", Clock.UtcNow, "screening-workflow");
            ResearchWorkspaceStore.WriteProject(location, project);
            var workflow = BuildExecutionAuthority(automatedStart: false);
            var protocol = workflow.ProtocolAuthority;
            var workflowActor = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
            var workflowPolicy = BuildExecutionPolicy(workflow);
            var workflowHeader = WorkflowExecutionHeader.Create("execution-screening", workflow, workflowPolicy, workflowActor, Clock.UtcNow);
            var resolver = new MutableExecutionRecordResolver();
            resolver.Add(Ref("workflow-artifact", "search-plan"));
            var workflowJournal = WorkflowExecutionJournal.Create(workflowHeader, workflow, workflowPolicy, resolver);
            Append(workflowJournal, "start-screening", "start", WorkflowExecutionEventKind.WorkStarted,
                WorkflowExecutionState.Ready, WorkflowExecutionState.Active, workflowActor, "attempt-1", 1);

            var criteria = BuildScreeningCriteria(protocol);
            var deduplication = BuildScreeningDeduplication();
            var screeningActor = new ScreeningConductActor("researcher-1", ScreeningConductActorKinds.Human, "methodologist");
            var screeningPolicy = ScreeningConductPolicy.Create(
                "screening-policy-workflow", "candidate-set-workflow", deduplication, protocol, criteria, 1,
                [new ScreeningConductRoleAssignment("researcher-1", "methodologist")], [], [], screeningActor, Clock.UtcNow);
            var screeningHeader = ScreeningConductHeader.Create("conduct-workflow", screeningPolicy, screeningActor, Clock.UtcNow);
            var decision = ScreeningConductDecision.Create(
                screeningHeader, 1, screeningHeader.Digest, "screening-decision-request", "candidate-1",
                ScreeningConductDecisionKind.Review, ScreeningVerdicts.Include, screeningActor,
                "Candidate meets the approved title and abstract criteria.", Clock.UtcNow);
            var screeningJournal = ScreeningConductJournal.Rehydrate(screeningHeader, screeningPolicy, [decision]);
            resolver.Add(ScreeningWorkflowExecutionBridge.CreateHumanTaskDecisionReference(screeningJournal, decision, workflowActor));
            var completion = ScreeningWorkflowExecutionBridge.CreateHumanTaskCompletion(
                screeningJournal, decision, workflowJournal, workflowActor, "complete-screening", "start", Clock.UtcNow,
                [Ref("workflow-artifact", "search-plan")]);
            workflowJournal.Append(completion);
            foreach (var faultPoint in new[]
            {
                ResearchWorkspaceAuthorityFaultPoint.AfterStaging,
                ResearchWorkspaceAuthorityFaultPoint.AfterPromotion
            })
            {
                var failedPreparation = ResearchWorkspaceWorkflowExecutionPreparation.Prepare(
                    location, project, workflow, workflowPolicy, workflowHeader, workflowJournal.Events, resolver);
                Assert.ThrowsExactly<InvalidOperationException>(() => ResearchWorkspaceScreeningConductTransaction.Commit(
                    location, project, deduplication, protocol, criteria, screeningPolicy, screeningHeader, [decision],
                    preparedWorkflow: failedPreparation, matchingDecision: decision, matchingWorkflowEvent: completion,
                    faultInjector: point => { if (point == faultPoint) throw new InvalidOperationException("Injected paired-commit failure."); }));
                var unchanged = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
                Assert.AreEqual(0L, unchanged.Revision);
                Assert.IsNull(unchanged.CurrentScreeningConductGenerationId);
                Assert.IsNull(unchanged.CurrentWorkflowExecutionJournalGenerationId);
                Assert.IsFalse(Directory.Exists(failedPreparation.StagingRoot));
                Assert.IsFalse(Directory.Exists(failedPreparation.FinalRoot));
            }

            var preparedWorkflow = ResearchWorkspaceWorkflowExecutionPreparation.Prepare(
                location, project, workflow, workflowPolicy, workflowHeader, workflowJournal.Events, resolver);
            Assert.IsTrue(Directory.Exists(preparedWorkflow.StagingRoot));
            Assert.IsFalse(Directory.Exists(preparedWorkflow.FinalRoot));

            var commit = ResearchWorkspaceScreeningConductTransaction.Commit(
                location, project, deduplication, protocol, criteria, screeningPolicy, screeningHeader, [decision],
                preparedWorkflow: preparedWorkflow, matchingDecision: decision, matchingWorkflowEvent: completion);

            Assert.AreEqual(1L, commit.Project.Revision);
            Assert.IsNotNull(commit.Project.CurrentScreeningConductGenerationId);
            Assert.IsNotNull(commit.Project.CurrentWorkflowExecutionJournalGenerationId);
            Assert.IsFalse(Directory.Exists(preparedWorkflow.StagingRoot));
            Assert.IsTrue(Directory.Exists(preparedWorkflow.FinalRoot));
            var reopenedScreening = ResearchWorkspaceScreeningConductVerifier.VerifyCurrent(
                location, commit.Project, deduplication, protocol, criteria);
            var reopenedWorkflow = ResearchWorkspaceWorkflowExecutionJournalVerifier.VerifyCurrent(
                location, commit.Project, workflow, resolver);
            Assert.AreEqual(completion.Digest.ToString(), reopenedScreening.Manifest.MatchingWorkflowEventDigest);
            Assert.AreEqual(completion.Digest, reopenedWorkflow.Events[^1].Digest);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [TestMethod]
    public void Execution_journal_preserves_failed_attempt_before_retry_success()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var journal = WorkflowExecutionJournal.Create(
            WorkflowExecutionHeader.Create("execution-retry", authority, policy, researcher, Clock.UtcNow), authority, policy, ExecutionRecordResolver);

        Append(journal, "start-1", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, attemptId: "attempt-1", attemptSequence: 1);
        Append(journal, "fail-1", "start", WorkflowExecutionEventKind.WorkFailed,
            WorkflowExecutionState.Active, WorkflowExecutionState.Failed, runner, attemptId: "attempt-1", attemptSequence: 1,
            errorCategory: "provider-error", errorSummary: "local adapter failed");
        Append(journal, "retry-1", "start", WorkflowExecutionEventKind.RetryAuthorized,
            WorkflowExecutionState.Failed, WorkflowExecutionState.Ready, researcher);
        Append(journal, "start-2", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, attemptId: "attempt-2", attemptSequence: 2);
        Append(journal, "complete-2", "start", WorkflowExecutionEventKind.WorkCompleted,
            WorkflowExecutionState.Active, WorkflowExecutionState.Completed, runner, attemptId: "attempt-2", attemptSequence: 2,
            outputs: new[] { Ref("workflow-artifact", "search-plan") });

        Assert.AreEqual(2, journal.Projection.Attempts["start"].Count);
        Assert.AreEqual(WorkflowExecutionState.Failed, journal.Projection.Attempts["start"][0].State);
        Assert.AreEqual(WorkflowExecutionState.Completed, journal.Projection.Attempts["start"][1].State);
    }

    [TestMethod]
    public void Execution_journal_resumes_blocked_attempt_without_overlap()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var journal = WorkflowExecutionJournal.Create(
            WorkflowExecutionHeader.Create("execution-block", authority, policy, researcher, Clock.UtcNow), authority, policy, ExecutionRecordResolver);

        Append(journal, "start", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, attemptId: "attempt-1", attemptSequence: 1);
        Append(journal, "block", "start", WorkflowExecutionEventKind.WorkBlocked,
            WorkflowExecutionState.Active, WorkflowExecutionState.Blocked, runner, attemptId: "attempt-1", attemptSequence: 1);
        Append(journal, "clear", "start", WorkflowExecutionEventKind.BlockCleared,
            WorkflowExecutionState.Blocked, WorkflowExecutionState.Active, runner, attemptId: "attempt-1", attemptSequence: 1);
        Append(journal, "complete", "start", WorkflowExecutionEventKind.WorkCompleted,
            WorkflowExecutionState.Active, WorkflowExecutionState.Completed, runner, attemptId: "attempt-1", attemptSequence: 1,
            outputs: new[] { Ref("workflow-artifact", "search-plan") });

        Assert.AreEqual(1, journal.Projection.Attempts["start"].Count);
        Assert.AreEqual(WorkflowExecutionState.Completed, journal.Projection.Attempts["start"][0].State);
    }

    [TestMethod]
    public void Execution_journal_strict_replay_rejects_duplicate_and_invalidation_fails_closed()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var header = WorkflowExecutionHeader.Create("execution-replay", authority, policy, researcher, Clock.UtcNow);
        var journal = WorkflowExecutionJournal.Create(header, authority, policy, ExecutionRecordResolver);
        Append(journal, "start", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, attemptId: "attempt-1", attemptSequence: 1);

        var replayError = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() => WorkflowExecutionJournal.Rehydrate(
            header, journal.Events.Concat(new[] { journal.Events[0] }), authority, policy, ExecutionRecordResolver));
        Assert.AreEqual(WorkflowExecutionErrorCodes.ConflictingRequest, replayError.Category);

        var invalidation = WorkflowExecutionEvent.Create(
            header, journal.Events.Count + 1, journal.Projection.HeadDigest, "invalidate", "start",
            WorkflowExecutionEventKind.WorkInvalidated, WorkflowExecutionState.Active, WorkflowExecutionState.Invalidated,
            researcher, Clock.UtcNow, "source changed", invalidationSource: Ref("protocol-amendment", "amendment-1"));
        var invalidationError = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() => journal.Append(invalidation));
        Assert.AreEqual(WorkflowExecutionErrorCodes.InvalidInvalidation, invalidationError.Category);
    }

    [TestMethod]
    public void Execution_journal_rejects_same_request_material_with_different_event_bytes()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var header = WorkflowExecutionHeader.Create("execution-idempotency", authority, policy, researcher, Clock.UtcNow);
        var journal = WorkflowExecutionJournal.Create(header, authority, policy, ExecutionRecordResolver);
        var first = WorkflowExecutionEvent.Create(
            header, 1, header.Digest, "same-request", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, Clock.UtcNow, "start", "attempt-1", 1);
        var changedTimestamp = WorkflowExecutionEvent.Create(
            header, 1, header.Digest, "same-request", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, Clock.UtcNow.AddSeconds(1), "start", "attempt-1", 1);
        journal.Append(first);

        var error = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() => journal.Append(changedTimestamp));

        Assert.AreEqual(first.RequestDigest, changedTimestamp.RequestDigest);
        Assert.AreNotEqual(first.Digest, changedTimestamp.Digest);
        Assert.AreEqual(WorkflowExecutionErrorCodes.ConflictingRequest, error.Category);
    }

    [TestMethod]
    public void Execution_journal_invalidates_and_supersedes_complete_dependency_closure_atomically()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var header = WorkflowExecutionHeader.Create("execution-invalidation", authority, policy, researcher, Clock.UtcNow);
        var journal = WorkflowExecutionJournal.Create(header, authority, policy, ExecutionRecordResolver);
        var source = Ref("protocol-amendment", "amendment-1");
        var invalidations = BuildBatch(
            journal,
            authority.Definition.Nodes.Select(node => node.NodeId),
            WorkflowExecutionEventKind.WorkInvalidated,
            id => journal.Projection.NodeStates[id],
            WorkflowExecutionState.Invalidated,
            researcher,
            invalidationSource: source);

        var partialError = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() =>
            journal.AppendInvalidationBatch("start", invalidations.Take(invalidations.Count - 1).ToArray()));
        Assert.AreEqual(WorkflowExecutionErrorCodes.InvalidInvalidation, partialError.Category);

        journal.AppendInvalidationBatch("start", invalidations);
        Assert.IsTrue(journal.Projection.NodeStates.Values.All(state => state == WorkflowExecutionState.Invalidated));

        var successor = Ref("workflow-execution", "execution-successor");
        var supersessions = BuildBatch(
            journal,
            authority.Definition.Nodes.Select(node => node.NodeId),
            WorkflowExecutionEventKind.SuccessorBound,
            _ => WorkflowExecutionState.Invalidated,
            WorkflowExecutionState.Superseded,
            researcher,
            successorExecution: successor);
        journal.AppendSupersessionBatch(supersessions);

        Assert.IsTrue(journal.Projection.NodeStates.Values.All(state => state == WorkflowExecutionState.Superseded));
    }

    [TestMethod]
    public void Execution_canonical_records_round_trip_and_reject_noncanonical_bytes()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var header = WorkflowExecutionHeader.Create("execution-codec", authority, policy, researcher, Clock.UtcNow);
        var item = WorkflowExecutionEvent.Create(
            header, 1, header.Digest, "start", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, Clock.UtcNow,
            "start deterministic work", "attempt-1", 1);

        var verifiedPolicy = WorkflowExecutionCanonicalCodec.RehydratePolicy(
            WorkflowExecutionCanonicalCodec.Serialize(policy), policy.Digest, authority);
        var verifiedHeader = WorkflowExecutionCanonicalCodec.RehydrateHeader(
            WorkflowExecutionCanonicalCodec.Serialize(header), header.Digest, authority, verifiedPolicy);
        var verifiedEvent = WorkflowExecutionCanonicalCodec.RehydrateEvent(
            WorkflowExecutionCanonicalCodec.Serialize(item), item.Digest, verifiedHeader);
        var replay = WorkflowExecutionJournal.Rehydrate(verifiedHeader, new[] { verifiedEvent }, authority, verifiedPolicy, ExecutionRecordResolver);

        Assert.AreEqual(policy.Digest, verifiedPolicy.Digest);
        Assert.AreEqual(header.Digest, verifiedHeader.Digest);
        Assert.AreEqual(item.Digest, verifiedEvent.Digest);
        Assert.AreEqual(WorkflowExecutionState.Active, replay.Projection.NodeStates["start"]);

        var nonCanonical = WorkflowExecutionCanonicalCodec.Serialize(item).Concat(new byte[] { (byte)'\n' }).ToArray();
        var error = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() =>
            WorkflowExecutionCanonicalCodec.RehydrateEvent(nonCanonical, item.Digest, header));
        Assert.AreEqual(WorkflowExecutionErrorCodes.UnverifiedAuthority, error.Category);
    }

    [TestMethod]
    public void Execution_provenance_projection_is_deterministic_and_authority_bound()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var header = WorkflowExecutionHeader.Create("execution-provenance", authority, policy, researcher, Clock.UtcNow);
        var item = WorkflowExecutionEvent.Create(
            header, 1, header.Digest, "start", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, Clock.UtcNow,
            "start deterministic work", "attempt-1", 1, inputs: new[] { Ref("search-plan", "plan-1") });
        var journal = WorkflowExecutionJournal.Create(header, authority, policy, ExecutionRecordResolver);
        journal.Append(item);

        var first = WorkflowExecutionProvenanceProjector.Project(journal, item);
        var second = WorkflowExecutionProvenanceProjector.Project(journal, item);

        Assert.AreEqual(first.EventId, second.EventId);
        Assert.AreEqual(first.EventDigest, second.EventDigest);
        Assert.AreEqual(header.ProtocolVersionId, first.ProtocolBinding!.ProtocolVersionId);
        Assert.AreEqual(header.WorkflowDigest, first.WorkflowBinding!.WorkflowDigest);
        Assert.AreEqual("start", first.WorkflowBinding.WorkflowNodeId);
        Assert.IsTrue(first.Inputs.Any(input => input.Digest == item.PreviousDigest));
        Assert.IsTrue(first.Outputs.Any(output => output.Digest == item.Digest));
    }

    [TestMethod]
    public void Execution_workspace_generation_saves_reopens_and_replays_idempotently()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nexus-fe03-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var location = new ResearchWorkspaceLocation(root, Path.Combine(root, ResearchWorkspacePaths.ProjectFileName));
            var project = ResearchWorkspaceProject.Create("FE-03", Clock.UtcNow, "workspace-fe03");
            ResearchWorkspaceStore.WriteProject(location, project);
            var authority = BuildExecutionAuthority();
            var policy = BuildExecutionPolicy(authority);
            var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
            var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
            var header = WorkflowExecutionHeader.Create("execution-workspace", authority, policy, researcher, Clock.UtcNow);
            var item = WorkflowExecutionEvent.Create(
                header, 1, header.Digest, "start", "start", WorkflowExecutionEventKind.WorkStarted,
                WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, Clock.UtcNow,
                "start deterministic work", "attempt-1", 1);

            var committed = ResearchWorkspaceWorkflowExecutionTransaction.Commit(
                location, project, authority, policy, header, new[] { item }, ExecutionRecordResolver);
            var reopenedProject = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
            var reopened = ResearchWorkspaceWorkflowExecutionJournalVerifier.VerifyCurrent(location, reopenedProject, authority, ExecutionRecordResolver);
            var replay = ResearchWorkspaceWorkflowExecutionTransaction.Commit(
                location, reopenedProject, authority, policy, header, new[] { item }, ExecutionRecordResolver);

            Assert.AreEqual(item.Digest, reopened.Journal.Projection.HeadDigest);
            Assert.AreEqual(WorkflowExecutionState.Active, reopened.Journal.Projection.NodeStates["start"]);
            Assert.IsTrue(replay.AlreadyApplied);
            Assert.AreEqual(committed.Manifest.GenerationId, replay.Manifest.GenerationId);
            var generationDirectory = Path.GetDirectoryName(
                ResearchWorkspacePaths.InProject(root, reopenedProject.WorkflowExecutionJournalManifestPath!))!;
            Assert.IsFalse(File.Exists(Path.Combine(generationDirectory, "current-state.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Execution_workspace_generation_rejects_stale_project_and_tampered_artifact()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nexus-fe03-negative-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var location = new ResearchWorkspaceLocation(root, Path.Combine(root, ResearchWorkspacePaths.ProjectFileName));
            var project = ResearchWorkspaceProject.Create("FE-03", Clock.UtcNow, "workspace-fe03-negative");
            ResearchWorkspaceStore.WriteProject(location, project);
            var authority = BuildExecutionAuthority();
            var policy = BuildExecutionPolicy(authority);
            var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
            var header = WorkflowExecutionHeader.Create("execution-workspace-negative", authority, policy, researcher, Clock.UtcNow);
            var commit = ResearchWorkspaceWorkflowExecutionTransaction.Commit(
                location, project, authority, policy, header, Array.Empty<WorkflowExecutionEvent>(), ExecutionRecordResolver);

            var stale = Assert.ThrowsExactly<ResearchWorkspaceConcurrencyException>(() =>
                ResearchWorkspaceWorkflowExecutionTransaction.Commit(
                    location, project, authority, policy, header, Array.Empty<WorkflowExecutionEvent>(), ExecutionRecordResolver));
            StringAssert.Contains(stale.Message.ToLowerInvariant(), "revision");

            var headerArtifact = commit.Manifest.Artifacts.Single(artifact => artifact.Name == "header");
            File.AppendAllText(ResearchWorkspacePaths.InProject(root, headerArtifact.RelativePath), "\n");
            _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                ResearchWorkspaceWorkflowExecutionJournalVerifier.VerifyCurrent(location, commit.Project, authority, ExecutionRecordResolver));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Execution_application_service_previews_then_commits_the_exact_validated_history()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var header = WorkflowExecutionHeader.Create("execution-app-service", authority, policy, researcher, Clock.UtcNow);
        var started = WorkflowExecutionEvent.Create(
            header, 1, header.Digest, "start-app-service", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, Clock.UtcNow,
            "Start deterministic work", "attempt-app-service", 1);
        var change = new WorkflowExecutionJournalChange(
            authority, policy, header, ExecutionRecordResolver, Array.Empty<WorkflowExecutionEvent>(), new[] { started });
        var preview = WorkflowExecutionJournalApplicationService.Preview(change);
        var port = new RecordingExecutionCommitPort(preview);

        var result = WorkflowExecutionJournalApplicationService.Commit(change, port);

        Assert.AreEqual(header.Digest, preview.PriorHeadDigest);
        Assert.AreEqual(WorkflowExecutionState.Active, preview.ResultingNodeStates["start"]);
        Assert.AreEqual(started.Digest, result.HeadDigest);
        Assert.AreEqual(1, port.CommitCount);
    }

    [TestMethod]
    public void Execution_workspace_generation_recovers_from_staging_and_promotion_failures()
    {
        foreach (var point in new[] { ResearchWorkspaceAuthorityFaultPoint.AfterStaging, ResearchWorkspaceAuthorityFaultPoint.AfterPromotion })
        {
            var root = Path.Combine(Path.GetTempPath(), $"nexus-fe03-crash-{point}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            try
            {
                var location = new ResearchWorkspaceLocation(root, Path.Combine(root, ResearchWorkspacePaths.ProjectFileName));
                var project = ResearchWorkspaceProject.Create("FE-03", Clock.UtcNow, $"workspace-fe03-{point}");
                ResearchWorkspaceStore.WriteProject(location, project);
                var authority = BuildExecutionAuthority();
                var policy = BuildExecutionPolicy(authority);
                var actor = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
                var header = WorkflowExecutionHeader.Create($"execution-{point}", authority, policy, actor, Clock.UtcNow);

                _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                    ResearchWorkspaceWorkflowExecutionTransaction.Commit(
                        location, project, authority, policy, header, Array.Empty<WorkflowExecutionEvent>(), ExecutionRecordResolver,
                        current => { if (current == point) throw new InvalidOperationException("injected failure"); }));

                var currentProject = ResearchWorkspaceStore.ReadProject(location.ProjectFilePath);
                Assert.IsNull(currentProject.CurrentWorkflowExecutionJournalGenerationId);
                var staging = ResearchWorkspacePaths.InProject(root, ResearchWorkspacePaths.GenerationStaging);
                Assert.IsFalse(Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any());
                if (point == ResearchWorkspaceAuthorityFaultPoint.AfterPromotion)
                {
                    var quarantine = ResearchWorkspacePaths.InProject(root, ResearchWorkspacePaths.GenerationQuarantine);
                    Assert.IsTrue(Directory.Exists(quarantine) && Directory.EnumerateDirectories(quarantine).Any());
                }
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Execution_workspace_generation_resumes_an_identical_orphan_after_process_crash()
    {
        var sourceRoot = Path.Combine(Path.GetTempPath(), $"nexus-fe03-promoted-source-{Guid.NewGuid():N}");
        var recoveredRoot = Path.Combine(Path.GetTempPath(), $"nexus-fe03-promoted-recovered-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sourceRoot);
        Directory.CreateDirectory(recoveredRoot);
        try
        {
            var project = ResearchWorkspaceProject.Create("FE-03", Clock.UtcNow, "workspace-fe03-promoted");
            var sourceLocation = new ResearchWorkspaceLocation(sourceRoot, Path.Combine(sourceRoot, ResearchWorkspacePaths.ProjectFileName));
            var recoveredLocation = new ResearchWorkspaceLocation(recoveredRoot, Path.Combine(recoveredRoot, ResearchWorkspacePaths.ProjectFileName));
            ResearchWorkspaceStore.WriteProject(sourceLocation, project);
            ResearchWorkspaceStore.WriteProject(recoveredLocation, project);
            var authority = BuildExecutionAuthority();
            var policy = BuildExecutionPolicy(authority);
            var actor = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
            var header = WorkflowExecutionHeader.Create("execution-process-crash", authority, policy, actor, Clock.UtcNow);
            var committed = ResearchWorkspaceWorkflowExecutionTransaction.Commit(
                sourceLocation, project, authority, policy, header, Array.Empty<WorkflowExecutionEvent>(), ExecutionRecordResolver);
            var sourceGeneration = Path.GetDirectoryName(
                ResearchWorkspacePaths.InProject(sourceRoot, committed.Project.WorkflowExecutionJournalManifestPath!))!;
            var recoveredGeneration = sourceGeneration.Replace(sourceRoot, recoveredRoot, StringComparison.Ordinal);
            foreach (var sourceFile in Directory.GetFiles(sourceGeneration, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(recoveredGeneration, Path.GetRelativePath(sourceGeneration, sourceFile));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(sourceFile, target);
            }

            var resumed = ResearchWorkspaceWorkflowExecutionTransaction.Commit(
                recoveredLocation, project, authority, policy, header, Array.Empty<WorkflowExecutionEvent>(), ExecutionRecordResolver);

            Assert.IsFalse(resumed.AlreadyApplied);
            Assert.AreEqual(committed.Manifest.GenerationId, resumed.Manifest.GenerationId);
            Assert.AreEqual(resumed.Project.CurrentWorkflowExecutionJournalGenerationId,
                ResearchWorkspaceStore.ReadProject(recoveredLocation.ProjectFilePath).CurrentWorkflowExecutionJournalGenerationId);
        }
        finally
        {
            Directory.Delete(sourceRoot, recursive: true);
            Directory.Delete(recoveredRoot, recursive: true);
        }
    }

    [TestMethod]
    public void Execution_completion_rejects_wrong_declared_artifact_kind()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var journal = WorkflowExecutionJournal.Create(
            WorkflowExecutionHeader.Create("execution-output-kind", authority, policy, researcher, Clock.UtcNow), authority, policy, ExecutionRecordResolver);
        Append(journal, "start-output-kind", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, "attempt-output-kind", 1);

        var error = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() => Append(
            journal, "complete-output-kind", "start", WorkflowExecutionEventKind.WorkCompleted,
            WorkflowExecutionState.Active, WorkflowExecutionState.Completed, runner, "attempt-output-kind", 1,
            new[] { Ref("wrong-kind", "search-plan") }));

        Assert.AreEqual(WorkflowExecutionErrorCodes.MissingOutput, error.Category);
    }

    [TestMethod]
    public void Execution_completion_rejects_output_digest_that_does_not_resolve()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var journal = WorkflowExecutionJournal.Create(
            WorkflowExecutionHeader.Create("execution-output-resolution", authority, policy, researcher, Clock.UtcNow),
            authority, policy, new MissingExecutionRecordResolver());
        Append(journal, "start-output-resolution", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, "attempt-output-resolution", 1);

        var error = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() => Append(
            journal, "complete-output-resolution", "start", WorkflowExecutionEventKind.WorkCompleted,
            WorkflowExecutionState.Active, WorkflowExecutionState.Completed, runner, "attempt-output-resolution", 1,
            new[] { Ref("workflow-artifact", "search-plan") }));

        Assert.AreEqual(WorkflowExecutionErrorCodes.UnverifiedAuthority, error.Category);
    }

    [TestMethod]
    public void Execution_approval_rejects_duplicate_records_and_actor_role_pairs()
    {
        var authority = BuildExecutionAuthority(minimumApprovals: 2);
        var policy = BuildExecutionPolicy(authority, includeSecondReviewer: true);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var header = WorkflowExecutionHeader.Create("execution-duplicate-approvals", authority, policy, researcher, Clock.UtcNow);
        var journal = WorkflowExecutionJournal.Create(header, authority, policy, ExecutionRecordResolver);
        Append(journal, "start-approval-input", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, "attempt-start", 1);
        Append(journal, "complete-approval-input", "start", WorkflowExecutionEventKind.WorkCompleted,
            WorkflowExecutionState.Active, WorkflowExecutionState.Completed, runner, "attempt-start", 1,
            new[] { Ref("workflow-artifact", "search-plan") });
        Append(journal, "ready-duplicate-approval", "approve", WorkflowExecutionEventKind.DependenciesSatisfied,
            WorkflowExecutionState.Pending, WorkflowExecutionState.Ready, researcher);
        Append(journal, "start-duplicate-approval", "approve", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, researcher, "attempt-approve", 1);
        var approval = new WorkflowExecutionApproval(researcher, Ref("approval-record", "approval-1"));
        var completion = WorkflowExecutionEvent.Create(
            header, journal.Events.Count + 1, journal.Projection.HeadDigest, "complete-duplicate-approval", "approve",
            WorkflowExecutionEventKind.WorkCompleted, WorkflowExecutionState.Active, WorkflowExecutionState.Completed,
            researcher, Clock.UtcNow, "duplicate records are invalid", "attempt-approve", 1,
            approvals: new[] { approval, approval }, decision: Ref("approval-decision", "decision-1"));

        var error = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() => journal.Append(completion));

        Assert.AreEqual(WorkflowExecutionErrorCodes.InvalidApproval, error.Category);
    }

    [TestMethod]
    public void Execution_journal_rejects_stale_head_and_automation_human_authority()
    {
        var authority = BuildExecutionAuthority();
        var policy = BuildExecutionPolicy(authority);
        var researcher = new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist");
        var runner = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var journal = WorkflowExecutionJournal.Create(
            WorkflowExecutionHeader.Create("execution-negative", authority, policy, researcher, Clock.UtcNow), authority, policy, ExecutionRecordResolver);

        Append(journal, "start-1", "start", WorkflowExecutionEventKind.WorkStarted,
            WorkflowExecutionState.Ready, WorkflowExecutionState.Active, runner, attemptId: "attempt-1", attemptSequence: 1);
        Append(journal, "complete-1", "start", WorkflowExecutionEventKind.WorkCompleted,
            WorkflowExecutionState.Active, WorkflowExecutionState.Completed, runner, attemptId: "attempt-1", attemptSequence: 1,
            outputs: new[] { Ref("workflow-artifact", "search-plan") });
        Append(journal, "ready-approve", "approve", WorkflowExecutionEventKind.DependenciesSatisfied,
            WorkflowExecutionState.Pending, WorkflowExecutionState.Ready, researcher);

        var stale = WorkflowExecutionEvent.Create(
            journal.Header, journal.Events.Count + 1, journal.Header.Digest, "stale", "approve",
            WorkflowExecutionEventKind.WorkStarted, WorkflowExecutionState.Ready, WorkflowExecutionState.Active,
            researcher, Clock.UtcNow, "stale append", "approval-attempt", 1);
        var staleError = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() => journal.Append(stale));
        Assert.AreEqual(WorkflowExecutionErrorCodes.InvalidJournalChain, staleError.Category);

        var automation = new WorkflowExecutionActor("runner-1", WorkflowExecutionActorKinds.Automation, "runner");
        var forged = WorkflowExecutionEvent.Create(
            journal.Header, journal.Events.Count + 1, journal.Projection.HeadDigest, "forged", "approve",
            WorkflowExecutionEventKind.WorkStarted, WorkflowExecutionState.Ready, WorkflowExecutionState.Active,
            automation, Clock.UtcNow, "automation cannot approve", "approval-attempt", 1);
        var authorityError = Assert.ThrowsExactly<WorkflowExecutionRuleException>(() => journal.Append(forged));
        Assert.AreEqual(WorkflowExecutionErrorCodes.AutomationHumanAuthority, authorityError.Category);
    }

    [TestMethod]
    public void Workflow_rehydration_rejects_scalar_and_duplicate_identity_tampering()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var authority = ProtocolAuthorities.GetValue(protocol, _ => throw new InvalidOperationException());
        var compiled = new WorkflowCompiler().Compile(BuildInput(protocol, template));
        var input = WorkflowRehydrator.FromCompiled(compiled);
        var resolver = new TestWorkflowAuthorityResolver(authority, template);

        var scalarError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            WorkflowRehydrator.Rehydrate(input with { CompilerVersion = "tampered" }, resolver));
        Assert.AreEqual(WorkflowErrorCodes.WorkflowIdMismatch, scalarError.Category);

        var duplicateError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            WorkflowRehydrator.Rehydrate(
                input with { Nodes = input.Nodes.Concat(new[] { input.Nodes[0] }).ToArray() },
                resolver));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, duplicateError.Category);

        var tamperedNode = input.Nodes[0] with { Label = "tampered" };
        var nodeError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            WorkflowRehydrator.Rehydrate(
                input with { Nodes = new[] { tamperedNode }.Concat(input.Nodes.Skip(1)).ToArray() },
                resolver));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, nodeError.Category);
    }

    [TestMethod]
    public void Workflow_authority_rejects_raw_protocol_and_wrong_template_resolution()
    {
        var protocol = BuildApprovedProtocol();
        var rawCopy = RecastProtocol(protocol, ProtocolStatus.Approved);
        var rawError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            new WorkflowCompiler().Compile(BuildInput(rawCopy, BuildTemplate())));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, rawError.Category);

        var template = BuildTemplate();
        var compiled = new WorkflowCompiler().Compile(BuildInput(protocol, template));
        var authority = ProtocolAuthorities.GetValue(protocol, _ => throw new InvalidOperationException());
        var wrongTemplateMaterial = template with
        {
            TemplateId = "wrong-template",
            TemplateDigest = ContentDigest.Sha256Utf8("placeholder")
        };
        var wrongTemplate = wrongTemplateMaterial with
        {
            TemplateDigest = WorkflowCompiler.ComputeLocalTemplateDigest(wrongTemplateMaterial)
        };
        var resolverError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            WorkflowRehydrator.Rehydrate(
                WorkflowRehydrator.FromCompiled(compiled),
                new TestWorkflowAuthorityResolver(authority, wrongTemplate)));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, resolverError.Category);
    }

    [TestMethod]
    public void Workflow_rehydration_rejects_unaccepted_or_broken_resolved_template_authority()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var authority = ProtocolAuthorities.GetValue(protocol, _ => throw new InvalidOperationException());
        var compiled = new WorkflowCompiler().Compile(BuildInput(protocol, template));
        var input = WorkflowRehydrator.FromCompiled(compiled);

        var wrongSchemaMaterial = template with
        {
            SchemaVersion = "9.9.9",
            TemplateDigest = ContentDigest.Sha256Utf8("placeholder")
        };
        var wrongSchema = wrongSchemaMaterial with
        {
            TemplateDigest = WorkflowCompiler.ComputeLocalTemplateDigest(wrongSchemaMaterial)
        };
        var schemaError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            WorkflowRehydrator.Rehydrate(
                input with { TemplateDigest = wrongSchema.TemplateDigest },
                new TestWorkflowAuthorityResolver(authority, wrongSchema)));
        Assert.AreEqual(WorkflowErrorCodes.UnknownSchemaId, schemaError.Category);

        var brokenRoleMaterial = template with
        {
            Roles = Array.Empty<WorkflowTemplateRole>(),
            TemplateDigest = ContentDigest.Sha256Utf8("placeholder")
        };
        var brokenRole = brokenRoleMaterial with
        {
            TemplateDigest = WorkflowCompiler.ComputeLocalTemplateDigest(brokenRoleMaterial)
        };
        var closureError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            WorkflowRehydrator.Rehydrate(
                input with { TemplateDigest = brokenRole.TemplateDigest },
                new TestWorkflowAuthorityResolver(authority, brokenRole)));
        Assert.AreEqual(WorkflowErrorCodes.UnknownApprovalRole, closureError.Category);
    }

    [TestMethod]
    public void Verified_workflow_does_not_retain_mutable_caller_collections()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var authority = ProtocolAuthorities.GetValue(protocol, _ => throw new InvalidOperationException());
        var compiled = new WorkflowCompiler().Compile(BuildInput(protocol, template));
        var nodes = compiled.Nodes.ToList();
        var input = WorkflowRehydrator.FromCompiled(compiled) with { Nodes = nodes };

        var verified = WorkflowRehydrator.Rehydrate(input, new TestWorkflowAuthorityResolver(authority, template));
        nodes.Clear();

        Assert.AreEqual(compiled.Nodes.Count, verified.Definition.Nodes.Count);
        Assert.IsFalse(verified.Definition.Nodes is WorkflowCompiledNode[]);
        Assert.IsFalse(verified.Definition.Nodes[0].DependsOn is string[]);
        Assert.IsFalse(verified.Definition.ApprovalRequirements[0].RequiredRoles is string[]);
    }

    [TestMethod]
    public void Workflow_rehydration_rejects_tamper_across_compiled_collection_families()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var authority = ProtocolAuthorities.GetValue(protocol, _ => throw new InvalidOperationException());
        var compiled = new WorkflowCompiler().Compile(BuildInput(protocol, template));
        var input = WorkflowRehydrator.FromCompiled(compiled);
        var resolver = new TestWorkflowAuthorityResolver(authority, template);
        var fakeInvalidation = new WorkflowInvalidationPlanEntry(
            "notice",
            "amendment",
            protocol.Id,
            protocol.ContentDigest,
            ContentDigest.Sha256Utf8("amendment"),
            ContentDigest.Sha256Utf8("notice"),
            "review-type",
            ContentDigest.Sha256Utf8("artifact"),
            input.Nodes[0].NodeId,
            "rerun");

        var mutations = new UnverifiedWorkflowDefinition[]
        {
            input with { ResolvedInputBindings = input.ResolvedInputBindings.Concat(new[] { input.ResolvedInputBindings[0] }).ToArray() },
            input with { Edges = input.Edges.Concat(new[] { input.Edges[0] }).ToArray() },
            input with { ApprovalRequirements = input.ApprovalRequirements.Concat(new[] { input.ApprovalRequirements[0] }).ToArray() },
            input with { CapabilityRequirements = input.CapabilityRequirements.Concat(new[] { input.CapabilityRequirements[0] }).ToArray() },
            input with { ArtifactDeclarations = input.ArtifactDeclarations.Concat(new[] { input.ArtifactDeclarations[0] }).ToArray() },
            input with { InvalidationPlanEntries = new[] { fakeInvalidation } },
            input with { Nodes = new[] { input.Nodes[0] with { Produces = new[] { "forged" } } }.Concat(input.Nodes.Skip(1)).ToArray() },
            input with { ApprovalRequirements = new[] { input.ApprovalRequirements[0] with { RequiredRoles = new[] { "forged" } } }.Concat(input.ApprovalRequirements.Skip(1)).ToArray() },
            input with { CapabilityRequirements = new[] { input.CapabilityRequirements[0] with { RequiredScopes = new[] { "forged" } } }.Concat(input.CapabilityRequirements.Skip(1)).ToArray() },
            input with { ArtifactDeclarations = new[] { input.ArtifactDeclarations[0] with { RequiredForGates = new[] { "forged" } } }.Concat(input.ArtifactDeclarations.Skip(1)).ToArray() }
        };

        foreach (var mutation in mutations)
        {
            _ = Assert.ThrowsExactly<WorkflowRuleException>(() => WorkflowRehydrator.Rehydrate(mutation, resolver));
        }
    }

    [TestMethod]
    public void Compile_rejects_non_approved_protocol_statuses()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();

        foreach (var status in new[] { ProtocolStatus.Draft, ProtocolStatus.ReadyForReview, ProtocolStatus.Withdrawn, ProtocolStatus.Superseded })
        {
            var nonApproved = RecastProtocol(protocol, status);
            var input = BuildInput(nonApproved, template);

            var error = Assert.ThrowsExactly<WorkflowRuleException>(
                () => new WorkflowCompiler().Compile(input));
            Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, error.Category);
        }
    }

    [TestMethod]
    public void Compile_rejects_missing_required_scientific_input()
    {
        var protocol = BuildApprovedProtocol(withDecision: false);
        var template = BuildTemplate();
        var input = BuildInput(protocol, template);

        var error = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(input));
        Assert.AreEqual(WorkflowErrorCodes.MissingRequiredInput, error.Category);
    }

    [TestMethod]
    public void Compile_rejects_scientific_conduct_compile_parameter()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var input = BuildInput(
            protocol,
            template,
            compileParameters: new System.Collections.Generic.Dictionary<string, CanonicalJsonValue>
            {
                ["review-type"] = CanonicalJsonValue.From("systematic-review")
            });

        var error = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(input));
        Assert.AreEqual(WorkflowErrorCodes.ConductInputFromCompileParameter, error.Category);
    }

    [TestMethod]
    public void Compile_rejects_undeclared_compile_parameter()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var input = BuildInput(
            protocol,
            template,
            compileParameters: new System.Collections.Generic.Dictionary<string, CanonicalJsonValue>
            {
                ["undeclared"] = CanonicalJsonValue.From("ignored-by-bug")
            });

        var error = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(input));
        Assert.AreEqual(WorkflowErrorCodes.UnknownCompileParameter, error.Category);
    }

    [TestMethod]
    public void Compile_records_declared_optional_execution_parameter_in_digest()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var compiler = new WorkflowCompiler();

        var withoutParameter = compiler.Compile(BuildInput(protocol, template));
        var withParameter = compiler.Compile(BuildInput(
            protocol,
            template,
            compileParameters: new System.Collections.Generic.Dictionary<string, CanonicalJsonValue>
            {
                ["priority"] = CanonicalJsonValue.From("low")
            }));

        Assert.AreNotEqual(withoutParameter.WorkflowDigest, withParameter.WorkflowDigest);
        var binding = withParameter.ResolvedInputBindings.Single(item => item.InputId == "priority");
        Assert.AreEqual("execution_parameter", binding.InputKind);
        Assert.AreEqual("compile-parameter", binding.SourceType);
    }

    [TestMethod]
    public void Compile_requires_explicit_workflow_compile_input()
    {
        var protocol = BuildApprovedProtocol();

        var error = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(protocol));

        Assert.AreEqual(WorkflowErrorCodes.ExplicitCompileInputRequired, error.Category);
    }

    [TestMethod]
    public void Compile_rejects_schema_closure_violations()
    {
        var protocol = BuildApprovedProtocol();
        var missingSchemaTemplate = WithRequiredInputSchema(BuildTemplate(), string.Empty, string.Empty);
        var missingSchemaInput = BuildInput(protocol, missingSchemaTemplate);

        var missing = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(missingSchemaInput));
        Assert.AreEqual(WorkflowErrorCodes.MissingSchemaId, missing.Category);

        var unknownSchemaTemplate = WithRequiredInputSchema(BuildTemplate(), "unknown.schema", "9.9.9");
        var unknownSchemaInput = BuildInput(protocol, unknownSchemaTemplate);

        var unknown = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(unknownSchemaInput));
        Assert.AreEqual(WorkflowErrorCodes.UnknownSchemaId, unknown.Category);

        var missingVersionTemplate = WithRequiredInputSchema(BuildTemplate(), "nexus.review.decision", string.Empty);
        var missingVersionInput = BuildInput(protocol, missingVersionTemplate);

        var missingVersion = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(missingVersionInput));
        Assert.AreEqual(WorkflowErrorCodes.MissingSchemaVersion, missingVersion.Category);
    }

    [TestMethod]
    public void Compile_rejects_stale_digests()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();

        var staleTemplate = template with { TemplateDigest = ContentDigest.Sha256Utf8("stale") };
        var staleTemplateInput = BuildInput(protocol, staleTemplate);
        var templateError = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(staleTemplateInput));
        Assert.AreEqual(WorkflowErrorCodes.StaleTemplateDigest, templateError.Category);

        var staleProtocol = RecastProtocol(protocol, ProtocolStatus.Approved, contentDigest: ContentDigest.Sha256Utf8("stale"));
        var staleProtocolInput = BuildInput(staleProtocol, template);
        var protocolError = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(staleProtocolInput));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, protocolError.Category);
    }

    [TestMethod]
    public void Compile_rejects_graph_validation_errors()
    {
        var protocol = BuildApprovedProtocol();

        var duplicateNodes = BuildInput(protocol, BuildTemplate(duplicateNode: true));
        var duplicate = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(duplicateNodes));
        Assert.AreEqual(WorkflowErrorCodes.DuplicateNodeId, duplicate.Category);

        var unknownEdge = BuildInput(protocol, BuildTemplate(unknownEdgeTarget: true));
        var unknown = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(unknownEdge));
        Assert.AreEqual(WorkflowErrorCodes.UnknownEdgeEndpoint, unknown.Category);

        var selfEdge = BuildInput(protocol, BuildTemplate(selfEdge: true));
        var self = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(selfEdge));
        Assert.AreEqual(WorkflowErrorCodes.SelfEdge, self.Category);

        var cycle = BuildInput(protocol, BuildTemplate(cycle: true));
        var cycleError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(cycle));
        Assert.AreEqual(WorkflowErrorCodes.DependencyCycle, cycleError.Category);
    }

    [TestMethod]
    public void Compile_rejects_artifact_and_capability_validation_failures()
    {
        var protocol = BuildApprovedProtocol();
        var undeclaredArtifact = BuildInput(protocol, BuildTemplate(undeclaredArtifact: true));
        var undeclared = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(undeclaredArtifact));
        Assert.AreEqual(WorkflowErrorCodes.UndeclaredProducedArtifact, undeclared.Category);

        var unknownProducer = BuildInput(protocol, BuildTemplate(unknownArtifactProducer: true));
        var producer = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(unknownProducer));
        Assert.AreEqual(WorkflowErrorCodes.UnknownProducingNode, producer.Category);

        var unknownCapability = BuildInput(protocol, BuildTemplate(unknownCapabilityRef: true));
        var capability = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(unknownCapability));
        Assert.AreEqual(WorkflowErrorCodes.UnknownCapabilityReference, capability.Category);
    }

    [TestMethod]
    public void Compile_rejects_approval_and_hybrid_contract_failures()
    {
        var protocol = BuildApprovedProtocol();
        var unknownRole = BuildInput(protocol, BuildTemplate(approvalRoleUnknown: true));
        var roleError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(unknownRole));
        Assert.AreEqual(WorkflowErrorCodes.UnknownApprovalRole, roleError.Category);

        var automation = BuildInput(protocol, BuildTemplate(approvalAllowsAutomation: true));
        var automationError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(automation));
        Assert.AreEqual(WorkflowErrorCodes.AutomationApprovalAuthority, automationError.Category);

        var invalidHybrid = BuildInput(protocol, BuildTemplate(invalidHybrid: true));
        var hybridError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(invalidHybrid));
        Assert.AreEqual(WorkflowErrorCodes.InvalidHybridNode, hybridError.Category);

        var noRoles = BuildInput(protocol, BuildTemplate(approvalWithNoRoles: true));
        var noRolesError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(noRoles));
        Assert.AreEqual(WorkflowErrorCodes.InvalidApprovalRequirement, noRolesError.Category);

        var zeroApprovals = BuildInput(protocol, BuildTemplate(approvalWithZeroMinimum: true));
        var zeroApprovalError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(zeroApprovals));
        Assert.AreEqual(WorkflowErrorCodes.InvalidApprovalRequirement, zeroApprovalError.Category);
    }

    [TestMethod]
    public void Compile_rejects_invalid_gate_authority_references()
    {
        var protocol = BuildApprovedProtocol();

        var unknownPolicy = BuildInput(protocol, BuildTemplate(unknownGatePolicy: true));
        var policyError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(unknownPolicy));
        Assert.AreEqual(WorkflowErrorCodes.UnknownGatePolicy, policyError.Category);

        var unknownArtifact = BuildInput(protocol, BuildTemplate(unknownGateArtifactRef: true));
        var artifactError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(unknownArtifact));
        Assert.AreEqual(WorkflowErrorCodes.UnknownGateArtifactReference, artifactError.Category);

        var unknownDecision = BuildInput(protocol, BuildTemplate(unknownGateDecisionRef: true));
        var decisionError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(unknownDecision));
        Assert.AreEqual(WorkflowErrorCodes.UnknownGateDecisionReference, decisionError.Category);
    }

    [TestMethod]
    public void Compile_rejects_invalid_waiver_and_invalidation_inputs()
    {
        var protocol = BuildApprovedProtocol(withWaiver: true, waiverExpired: true);
        var template = BuildTemplate(withWaiverPolicy: true);
        var waiverError = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(BuildInput(protocol, template)));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, waiverError.Category);

        var futureWaiverProtocol = BuildApprovedProtocol(withDecision: false, withWaiver: true, waiverExpiresAt: Clock.UtcNow.AddDays(1));
        var futureWaiverError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            new WorkflowCompiler().Compile(BuildInput(futureWaiverProtocol, template)));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, futureWaiverError.Category);

        var amendment = BuildAmendment(BuildApprovedProtocol(withDecision: true));
        var amendedProtocol = RecastProtocol(BuildApprovedProtocol(withDecision: true), ProtocolStatus.Approved, amendment.AmendmentId);
        var invalidationTemplate = BuildTemplate(
            withInvalidationPolicy: true,
            invalidationNoticeArtifactMismatch: true);
        var missingSource = Assert.ThrowsExactly<WorkflowRuleException>(
            () => new WorkflowCompiler().Compile(BuildInput(amendedProtocol, invalidationTemplate)));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, missingSource.Category);

        var suppliedSource = BuildInput(
            amendedProtocol,
            invalidationTemplate,
            amendment: amendment,
            notices: amendment.InvalidationNotices);
        var suppliedSourceError = Assert.ThrowsExactly<WorkflowRuleException>(() =>
            new WorkflowCompiler().Compile(suppliedSource));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, suppliedSourceError.Category);
    }

    [TestMethod]
    public void Compile_rejects_invalidation_until_verified_protocol_authority_exists()
    {
        var previous = BuildApprovedProtocol(withDecision: true);
        var template = BuildTemplate(withInvalidationPolicy: true, hyphenatedInvalidationNode: true);
        var amendment = BuildAmendment(previous, affectedNodeId: "approve-search");
        var amendedProtocol = RecastProtocol(BuildApprovedProtocol(withDecision: true), ProtocolStatus.Approved, amendment.AmendmentId);

        var error = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(BuildInput(
            amendedProtocol,
            template,
            amendment: amendment,
            notices: amendment.InvalidationNotices)));

        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, error.Category);
    }

    [TestMethod]
    public void Compile_accepts_exact_verified_waiver_authority()
    {
        var protocol = BuildApprovedProtocol(withDecision: false, withWaiver: true, waiverExpiresAt: Clock.UtcNow.AddDays(1));
        var waiver = protocol.Waivers.Single();
        var authority = new VerifiedProtocolWaiver(
            waiver,
            ContentDigest.Sha256CanonicalJson(waiver.ToCanonicalJson()),
            ApprovalPolicy.ExplicitCustomSingleResearcher(),
            Array.Empty<VerifiedProtocolSupplementalApproval>());

        var definition = new WorkflowCompiler().Compile(BuildInput(
            protocol,
            BuildTemplate(withWaiverPolicy: true),
            verifiedWaivers: new[] { authority }));

        var binding = definition.ResolvedInputBindings.Single(item => item.InputId == "review-type");
        Assert.AreEqual("protocol-waiver", binding.SourceType);
        Assert.AreEqual(waiver.WaiverId, binding.WaiverId);
        Assert.AreEqual(authority.WaiverDigest, binding.SourceDigest);
    }

    [TestMethod]
    public void Compile_accepts_verified_amendment_and_immutable_notice_membership()
    {
        var state = BuildVerifiedAmendmentState("approve-search");
        var definition = new WorkflowCompiler().Compile(BuildInput(
            state.Produced.Version,
            BuildTemplate(withInvalidationPolicy: true, hyphenatedInvalidationNode: true),
            verifiedAmendment: state.Amendment));

        var entry = definition.InvalidationPlanEntries.Single();
        Assert.AreEqual(state.Amendment.Amendment.AmendmentId, entry.AmendmentId);
        Assert.AreEqual(state.Amendment.InvalidationNotices[0].NoticeId, entry.NoticeId);
        Assert.AreEqual(state.Amendment.AmendmentDigest, entry.AmendmentSourceDigest);
    }

    [TestMethod]
    public void Compile_rejects_missing_extra_duplicate_or_foreign_verified_waiver_authority()
    {
        var protocol = BuildApprovedProtocol(withDecision: false, withWaiver: true, waiverExpiresAt: Clock.UtcNow.AddDays(1));
        var waiver = protocol.Waivers.Single();
        var authority = new VerifiedProtocolWaiver(
            waiver, ContentDigest.Sha256CanonicalJson(waiver.ToCanonicalJson()),
            ApprovalPolicy.ExplicitCustomSingleResearcher(), Array.Empty<VerifiedProtocolSupplementalApproval>());
        var foreignWaiver = waiver with { WaiverId = "foreign-waiver" };
        var foreign = new VerifiedProtocolWaiver(
            foreignWaiver, ContentDigest.Sha256CanonicalJson(foreignWaiver.ToCanonicalJson()),
            ApprovalPolicy.ExplicitCustomSingleResearcher(), Array.Empty<VerifiedProtocolSupplementalApproval>());
        var template = BuildTemplate(withWaiverPolicy: true);

        foreach (var authorities in new[]
        {
            Array.Empty<VerifiedProtocolWaiver>(),
            new[] { authority, foreign },
            new[] { authority, authority },
            new[] { foreign }
        })
        {
            var error = Assert.ThrowsExactly<WorkflowRuleException>(() =>
                new WorkflowCompiler().Compile(BuildInput(protocol, template, verifiedWaivers: authorities)));
            Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, error.Category);
        }
    }

    [TestMethod]
    public void Rehydrate_resolves_verified_waiver_and_amendment_authority_with_digest_parity()
    {
        var waiverProtocol = BuildApprovedProtocol(withDecision: false, withWaiver: true, waiverExpiresAt: Clock.UtcNow.AddDays(1));
        var waiver = waiverProtocol.Waivers.Single();
        var waiverAuthority = new VerifiedProtocolWaiver(
            waiver, ContentDigest.Sha256CanonicalJson(waiver.ToCanonicalJson()),
            ApprovalPolicy.ExplicitCustomSingleResearcher(), Array.Empty<VerifiedProtocolSupplementalApproval>());
        var waiverTemplate = BuildTemplate(withWaiverPolicy: true);
        var waiverDefinition = new WorkflowCompiler().Compile(BuildInput(
            waiverProtocol, waiverTemplate, verifiedWaivers: new[] { waiverAuthority }));
        var waiverVerified = WorkflowRehydrator.Rehydrate(
            WorkflowRehydrator.FromCompiled(waiverDefinition),
            new TestWorkflowAuthorityResolver(
                ProtocolAuthorities.GetValue(waiverProtocol, _ => throw new InvalidOperationException()),
                waiverTemplate,
                new[] { waiverAuthority }));
        Assert.AreEqual(waiverDefinition.WorkflowDigest, waiverVerified.Definition.WorkflowDigest);

        var amendmentState = BuildVerifiedAmendmentState("approve-search");
        var amendmentTemplate = BuildTemplate(withInvalidationPolicy: true, hyphenatedInvalidationNode: true);
        var amendmentDefinition = new WorkflowCompiler().Compile(BuildInput(
            amendmentState.Produced.Version, amendmentTemplate, verifiedAmendment: amendmentState.Amendment));
        var amendmentVerified = WorkflowRehydrator.Rehydrate(
            WorkflowRehydrator.FromCompiled(amendmentDefinition),
            new TestWorkflowAuthorityResolver(
                amendmentState.Produced, amendmentTemplate, amendment: amendmentState.Amendment));
        Assert.AreEqual(amendmentDefinition.WorkflowId, amendmentVerified.Definition.WorkflowId);
        Assert.AreEqual(amendmentDefinition.WorkflowDigest, amendmentVerified.Definition.WorkflowDigest);
    }

    [TestMethod]
    public void Rehydrate_rejects_missing_amendment_authority_and_tampered_notice_membership()
    {
        var state = BuildVerifiedAmendmentState("approve-search");
        var template = BuildTemplate(withInvalidationPolicy: true, hyphenatedInvalidationNode: true);
        var definition = new WorkflowCompiler().Compile(BuildInput(
            state.Produced.Version, template, verifiedAmendment: state.Amendment));
        var input = WorkflowRehydrator.FromCompiled(definition);

        var missing = Assert.ThrowsExactly<WorkflowRuleException>(() => WorkflowRehydrator.Rehydrate(
            input, new TestWorkflowAuthorityResolver(state.Produced, template)));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, missing.Category);

        var entries = input.InvalidationPlanEntries.ToArray();
        entries[0] = entries[0] with { InvalidationNoticeDigest = ContentDigest.Sha256Utf8("tampered") };
        var tampered = Assert.ThrowsExactly<WorkflowRuleException>(() => WorkflowRehydrator.Rehydrate(
            input with { InvalidationPlanEntries = entries },
            new TestWorkflowAuthorityResolver(state.Produced, template, amendment: state.Amendment)));
        Assert.AreEqual(WorkflowErrorCodes.UnverifiedAuthority, tampered.Category);
    }

    [TestMethod]
    public void Compile_rejects_workflow_id_mismatch()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var expected = "workflow-0000000000000000";
        var input = BuildInput(protocol, template, expectedWorkflowId: expected);
        var error = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(input));
        Assert.AreEqual(WorkflowErrorCodes.WorkflowIdMismatch, error.Category);
    }

    private static VerifiedWorkflowDefinition BuildExecutionAuthority(int minimumApprovals = 1, bool automatedStart = true)
    {
        var protocol = BuildApprovedProtocol();
        var source = BuildTemplate();
        var nodes = source.Nodes.Select(node => automatedStart && node.NodeId == "start"
            ? node with { Kind = WorkflowNodeKind.AutomatedTask, Mode = WorkflowNodeMode.Automated }
            : node).ToArray();
        var template = source with
        {
            Nodes = nodes,
            Gates = automatedStart
                ? source.Gates
                : source.Gates.Select(gate => gate with { TargetNodeId = "start" }).ToArray(),
            ApprovalRequirements = source.ApprovalRequirements.Select(requirement =>
                requirement with { MinimumApprovals = minimumApprovals }).ToArray(),
            Roles = source.Roles.Concat(new[]
            {
                new WorkflowTemplateRole("runner", "Runner", "Execute deterministic automated work")
            }).ToArray()
        };
        template = template with { TemplateDigest = WorkflowCompiler.ComputeLocalTemplateDigest(template) };
        var compiled = new WorkflowCompiler().Compile(BuildInput(protocol, template));
        return WorkflowRehydrator.Rehydrate(
            WorkflowRehydrator.FromCompiled(compiled),
            new TestWorkflowAuthorityResolver(
                ProtocolAuthorities.GetValue(protocol, _ => throw new InvalidOperationException()),
                template));
    }

    private static WorkflowExecutionAuthorityPolicy BuildExecutionPolicy(
        VerifiedWorkflowDefinition authority,
        bool includeSecondReviewer = false) =>
        WorkflowExecutionAuthorityPolicy.Create(
            "execution-policy-1",
            Ref("review", "review-1"),
            authority,
            new[]
            {
                new WorkflowExecutionRoleAssignment("researcher-1", "methodologist"),
                new WorkflowExecutionRoleAssignment("runner-1", "runner")
            }.Concat(includeSecondReviewer
                ? new[] { new WorkflowExecutionRoleAssignment("researcher-2", "methodologist") }
                : Array.Empty<WorkflowExecutionRoleAssignment>()),
            new WorkflowExecutionActor("researcher-1", WorkflowExecutionActorKinds.Human, "methodologist"),
            Clock.UtcNow);

    private static WorkflowExecutionEvent Append(
        WorkflowExecutionJournal journal,
        string requestId,
        string nodeId,
        WorkflowExecutionEventKind kind,
        WorkflowExecutionState expected,
        WorkflowExecutionState result,
        WorkflowExecutionActor actor,
        string? attemptId = null,
        int? attemptSequence = null,
        IEnumerable<WorkflowExecutionRecordRef>? outputs = null,
        string? errorCategory = null,
        string? errorSummary = null)
    {
        var item = WorkflowExecutionEvent.Create(
            journal.Header,
            journal.Events.Count + 1,
            journal.Projection.HeadDigest,
            requestId,
            nodeId,
            kind,
            expected,
            result,
            actor,
            Clock.UtcNow,
            $"{kind} for {nodeId}",
            attemptId,
            attemptSequence,
            outputs: outputs,
            errorCategory: errorCategory,
            errorSummary: errorSummary);
        return journal.Append(item);
    }

    private static IReadOnlyList<WorkflowExecutionEvent> BuildBatch(
        WorkflowExecutionJournal journal,
        IEnumerable<string> nodeIds,
        WorkflowExecutionEventKind kind,
        Func<string, WorkflowExecutionState> expectedState,
        WorkflowExecutionState result,
        WorkflowExecutionActor actor,
        WorkflowExecutionRecordRef? invalidationSource = null,
        WorkflowExecutionRecordRef? successorExecution = null)
    {
        var items = new List<WorkflowExecutionEvent>();
        var previous = journal.Projection.HeadDigest;
        var ordinal = journal.Events.Count + 1;
        foreach (var nodeId in nodeIds)
        {
            var item = WorkflowExecutionEvent.Create(
                journal.Header,
                ordinal++,
                previous,
                $"{kind}-{nodeId}",
                nodeId,
                kind,
                expectedState(nodeId),
                result,
                actor,
                Clock.UtcNow,
                $"{kind} {nodeId}",
                invalidationSource: invalidationSource,
                invalidationPolicyRef: kind == WorkflowExecutionEventKind.WorkInvalidated
                    ? journal.Workflow.Definition.Nodes.Single(node => node.NodeId == nodeId).InvalidationPolicyRef
                    : null,
                successorExecution: successorExecution);
            items.Add(item);
            previous = item.Digest;
        }
        return items;
    }

    private static WorkflowExecutionRecordRef Ref(string kind, string id) =>
        new(kind, id, ContentDigest.Sha256Utf8($"{kind}:{id}"));

    private static VerifiedDeduplicationResult BuildScreeningDeduplication()
    {
        var candidate = new DedupCandidateRecord(
            "candidate-1", "Candidate title", true, "work:candidate-1", ["work:candidate-1"], [],
            new DedupSightingRef("search", "trace-1", "source-candidate-1", "search-provider"));
        var result = new DeduplicationResult(
            "dedup-screening-workflow", "nexus.deduplication.result", "1.0.0",
            DeduplicationService.PolicyId, DeduplicationService.PolicyVersion, 0.95d,
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new Dictionary<string, int>(StringComparer.Ordinal)),
            [], [], [candidate], [], [], [], [], [], [], ["no-php-compatibility-claim"]);
        return DeduplicationRehydrator.Rehydrate(new UnverifiedDeduplicationResult(result));
    }

    private static ScreeningCriteria BuildScreeningCriteria(VerifiedProtocolVersion protocol) => new(
        "criteria-workflow", "1.0.0", ScreeningStages.TitleAbstract,
        CanonicalJsonValue.From("include"), CanonicalJsonValue.From("exclude"), true,
        protocol.Version.Id, protocol.Version.ContentDigest.ToString(),
        approvedProtocolDigestScope: DigestScope.ProtocolContent.ToString(),
        approvedProtocolStatus: ScreeningProtocolBindingStatus.Approved,
        currentProtocolContentDigest: protocol.Version.ContentDigest.ToString());

    private sealed class RecordingExecutionCommitPort(WorkflowExecutionJournalPreview preview)
        : IWorkflowExecutionJournalCommitPort
    {
        public int CommitCount { get; private set; }

        public WorkflowExecutionJournalCommitResult Commit(
            VerifiedWorkflowDefinition workflow,
            WorkflowExecutionAuthorityPolicy policy,
            WorkflowExecutionHeader header,
            IReadOnlyList<WorkflowExecutionEvent> events)
        {
            CommitCount++;
            return new WorkflowExecutionJournalCommitResult(
                header.ExecutionId, preview.ResultingHeadDigest, events.Count, AlreadyApplied: false);
        }
    }

    private sealed class TestExecutionRecordResolver : IWorkflowExecutionRecordResolver
    {
        public WorkflowExecutionRecordRef Resolve(string kind, string id) => Ref(kind, id);
    }

    private sealed class MutableExecutionRecordResolver : IWorkflowExecutionRecordResolver
    {
        private readonly Dictionary<(string Kind, string Id), WorkflowExecutionRecordRef> records = [];

        public void Add(WorkflowExecutionRecordRef record) => records[(record.Kind, record.Id)] = record;

        public WorkflowExecutionRecordRef? Resolve(string kind, string id) =>
            records.GetValueOrDefault((kind, id));
    }

    private sealed class MissingExecutionRecordResolver : IWorkflowExecutionRecordResolver
    {
        public WorkflowExecutionRecordRef? Resolve(string kind, string id) => null;
    }

    private static WorkflowCompileInput BuildInput(
        ProtocolVersion protocol,
        WorkflowTemplate template,
        WorkflowTemplate? unknownSchemaTemplate = null,
        IReadOnlyDictionary<string, CanonicalJsonValue>? compileParameters = null,
        ProtocolAmendment? amendment = null,
        IEnumerable<ProtocolInvalidationNotice>? notices = null,
        string? expectedWorkflowId = null,
        IReadOnlyList<VerifiedProtocolWaiver>? verifiedWaivers = null,
        VerifiedProtocolAmendment? verifiedAmendment = null)
    {
        var selectedTemplate = unknownSchemaTemplate ?? template;
        var knownSchemaRefs = new System.Collections.Generic.HashSet<WorkflowSchemaRef>
        {
            new("nexus.workflow-template", "1.0.0"),
            new("nexus.workflow-definition", "1.1.0"),
            new("nexus.review.decision", "1.0.0"),
            new("nexus.workflow.artifact", "1.0.0")
        };

        return new WorkflowCompileInput(
            ProtocolAuthorities.TryGetValue(protocol, out var authority) ? authority : null!,
            selectedTemplate,
            compileParameters ?? new System.Collections.Generic.Dictionary<string, CanonicalJsonValue>(),
            knownSchemaRefs.ToArray(),
            verifiedWaivers,
            verifiedAmendment,
            expectedWorkflowId,
            "nexus-workflow-compiler",
            "1.0.0");
    }

    private static VerifiedAmendmentState BuildVerifiedAmendmentState(string affectedNodeId)
    {
        var previousVersion = BuildApprovedProtocol(withDecision: true);
        var previous = ProtocolAuthorities.GetValue(previousVersion, _ => throw new InvalidOperationException());
        var amendment = BuildAmendment(previousVersion, affectedNodeId);
        amendment = new ProtocolAmendment(
            amendment.AmendmentId, amendment.ProtocolId, amendment.AmendsVersionId, amendment.ProducesVersionId,
            amendment.PreviousContentDigest, amendment.RequestedBy, amendment.RequestedAt, amendment.Rationale,
            amendment.ChangedDecisionKeys, amendment.InvalidationNotices, amendment.InvalidationPlanDigest,
            amendment.ApprovalPolicyId, new[] { "amendment-approval-1" });
        var seed = new ProtocolVersion(
            amendment.ProducesVersionId, previousVersion.ProtocolId, previousVersion.ProjectId, previousVersion.VersionNumber + 1,
            ProtocolStatus.Approved, previousVersion.Template, previousVersion.Intent, previousVersion.Values,
            previousVersion.RequiredDecisions, previousVersion.Decisions, previousVersion.Waivers,
            ContentDigest.Sha256Utf8("placeholder"), previousVersion.ApprovalPolicyId, previousVersion.ApprovalIds,
            Clock.UtcNow, previousVersion.Id, amendmentId: amendment.AmendmentId, unresolvedDecisions: previousVersion.UnresolvedDecisions);
        var producedVersion = new ProtocolVersion(
            seed.Id, seed.ProtocolId, seed.ProjectId, seed.VersionNumber, seed.Status, seed.Template, seed.Intent, seed.Values,
            seed.RequiredDecisions, seed.Decisions, seed.Waivers, seed.ToProtocolContentDigestEnvelope().ComputeDigest(),
            seed.ApprovalPolicyId, seed.ApprovalIds, seed.ApprovedAt, seed.SupersedesVersionId, seed.SupersededByVersionId,
            seed.AmendmentId, seed.UnresolvedDecisions);
        var produced = new VerifiedProtocolVersion(producedVersion, previous.ApprovalPolicy, previous.Approvals);
        ProtocolAuthorities.Add(producedVersion, produced);
        var verified = new VerifiedProtocolAmendment(
            amendment, ContentDigest.Sha256CanonicalJson(amendment.ToCanonicalJson()),
            ApprovalPolicy.ExplicitCustomSingleResearcher(), previous, produced,
            Array.Empty<VerifiedProtocolSupplementalApproval>());
        return new VerifiedAmendmentState(verified, produced);
    }

    private sealed record VerifiedAmendmentState(VerifiedProtocolAmendment Amendment, VerifiedProtocolVersion Produced);

    private static WorkflowTemplate BuildTemplate(
        bool duplicateNode = false,
        bool unknownEdgeTarget = false,
        bool selfEdge = false,
        bool cycle = false,
        bool undeclaredArtifact = false,
        bool unknownArtifactProducer = false,
        bool unknownCapabilityRef = false,
        bool approvalRoleUnknown = false,
        bool approvalAllowsAutomation = false,
        bool approvalWithNoRoles = false,
        bool approvalWithZeroMinimum = false,
        bool unknownGatePolicy = false,
        bool unknownGateArtifactRef = false,
        bool unknownGateDecisionRef = false,
        bool invalidHybrid = false,
        bool withWaiverPolicy = false,
        bool withInvalidationPolicy = false,
        bool invalidationNoticeArtifactMismatch = false,
        bool hyphenatedInvalidationNode = false)
    {
        var approveNodeId = hyphenatedInvalidationNode ? "approve-search" : "approve";
        var requiredInputs = new[]
        {
            new WorkflowTemplateInput(
                "review-type",
                WorkflowTemplateInputKind.ScientificConduct,
                "nexus.review.decision",
                "1.0.0",
                true,
                "review-type"),
            new WorkflowTemplateInput(
                "priority",
                WorkflowTemplateInputKind.ExecutionParameter,
                "nexus.review.decision",
                "1.0.0",
                false,
                null,
                CanonicalJsonValue.From("high"))
        };

        var nodes = new System.Collections.Generic.List<WorkflowTemplateNode>
        {
            new(
                "start",
                WorkflowNodeKind.HumanTask,
                WorkflowNodeMode.Human,
                "Start",
                Array.Empty<string>(),
                new[] { "search-plan" },
                null,
                new[] { invalidHybrid ? "cap.search" : "cap.search" },
                withWaiverPolicy ? "waive-review" : null,
                null,
                null),
            new(
                approveNodeId,
                WorkflowNodeKind.Approval,
                invalidHybrid ? WorkflowNodeMode.Hybrid : WorkflowNodeMode.Human,
                "Approve",
                unknownCapabilityRef ? new[] { "review-type", "unknown-req" } : new[] { "review-type" },
                unknownCapabilityRef ? new[] { "search-plan" } : new[] { "review-plan" },
                invalidHybrid ? null : "approve-review",
                unknownCapabilityRef ? new[] { "cap.unknown" } : new[] { "cap.search" },
                withWaiverPolicy ? "waive-review" : null,
                null,
                null),
            new(
                "execute",
                WorkflowNodeKind.AutomatedTask,
                WorkflowNodeMode.Automated,
                "Execute",
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                new[] { "cap.search" },
                null,
                null,
                null),
            new(
                "finish",
                WorkflowNodeKind.Milestone,
                WorkflowNodeMode.Human,
                "Finish",
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                new[] { "cap.search" },
                null,
                null,
                null)
        };

        if (duplicateNode)
        {
            nodes.Add(nodes[0]);
        }

        var edges = new System.Collections.Generic.List<WorkflowTemplateEdge>
        {
            new("start", approveNodeId),
            new(approveNodeId, "execute"),
            new("execute", unknownEdgeTarget ? "missing" : "finish")
        };

        if (cycle)
        {
            edges.Add(new("finish", "start"));
        }

        if (selfEdge)
        {
            edges.Add(new("start", "start"));
        }

        var roles = new[]
        {
            new WorkflowTemplateRole("methodologist", "Methodologist", "Scientific adjudication")
        };

        var roleRequirement = approvalRoleUnknown ? "mystery-role" : "methodologist";
        var requiredRoles = approvalWithNoRoles
            ? Array.Empty<string>()
            : new[] { roleRequirement };
        var approvalRequirements = new[]
        {
            new WorkflowTemplateApprovalRequirement(
                "approve-review",
                "review-policy",
                "1.0.0",
                "single_reviewer",
                requiredRoles,
                approvalWithZeroMinimum ? 0 : 1,
                false,
                approvalAllowsAutomation)
        };

        var capabilityRequirements = new[]
        {
            new WorkflowTemplateCapabilityRequirement(
                "cap.search",
                "search-capability",
                new[] { "read" },
                "restricted",
                true)
        };

        var waiverPolicies = withWaiverPolicy
            ? new[]
            {
                new WorkflowTemplateWaiverPolicy(
                    "waive-review",
                    new[] { "review-type" },
                    "approve-review",
                    "disclose limitations",
                    "rerun review if required")
            }
            : Array.Empty<WorkflowTemplateWaiverPolicy>();

        var artifacts = new[]
        {
            new WorkflowTemplateArtifactDeclaration(
                "search-plan",
                "workflow-artifact",
                "nexus.workflow.artifact",
                "1.0.0",
                unknownArtifactProducer ? "unknown-node" : "start",
                Array.Empty<string>(),
                null),
            new WorkflowTemplateArtifactDeclaration(
                "review-plan",
                "workflow-artifact",
                "nexus.workflow.artifact",
                "1.0.0",
                "start",
                Array.Empty<string>(),
                null)
        };

        var artifactDeclarations = undeclaredArtifact
            ? new[] { artifacts[0] }
            : artifacts;

        var invalidationPolicies = withInvalidationPolicy
            ? new[]
            {
                new WorkflowTemplateInvalidationPolicy(
                    "p1",
                    new[] { "review-type" },
                    invalidationNoticeArtifactMismatch
                        ? new[] { "search-plan" }
                        : artifactDeclarations.Select(artifact => artifact.ArtifactRef).ToArray(),
                    new[] { approveNodeId },
                    "rerun")
            }
            : Array.Empty<WorkflowTemplateInvalidationPolicy>();

        var gates = new[]
        {
            new WorkflowTemplateGate(
                "g1",
                invalidHybrid ? "start" : approveNodeId,
                unknownGatePolicy ? "unknown-policy" : "approve-review",
                unknownGateArtifactRef ? new[] { "missing-artifact" } : Array.Empty<string>(),
                unknownGateDecisionRef ? new[] { "missing-decision" } : new[] { "review-type" },
                new[] { roleRequirement })
        };

        var template = new WorkflowTemplate(
            "template-rapid-review",
            "1.0.0",
            ContentDigest.Sha256Utf8("template"),
            "nexus.workflow-template",
            "1.0.0",
            requiredInputs,
            nodes.ToArray(),
            edges,
            gates,
            approvalRequirements,
            roles,
            capabilityRequirements,
            waiverPolicies,
            artifactDeclarations,
            invalidationPolicies);

        return template with
        {
            TemplateDigest = WorkflowCompiler.ComputeLocalTemplateDigest(template)
        };
    }

    private static ProtocolVersion BuildApprovedProtocol(
        bool withDecision = true,
        bool withWaiver = false,
        bool waiverExpired = false,
        DateTimeOffset? waiverExpiresAt = null)
    {
        var ids = new SequenceIdGenerator();
        var waiverSuppliesReviewType = withWaiver && !withDecision;
        var requiredDecisionKey = withDecision || waiverSuppliesReviewType ? "review-type" : "other-review-type";
        var draft = ProtocolDraft.Create(
            ids,
            "protocol-1",
            new ProtocolTemplate("template-systematic-review", "1.0.0", ContentDigest.Sha256Utf8("template-systematic-review@1.0.0")),
            new ProtocolIntent("tomato disease screening", "screen evidence"),
            new CanonicalJsonObject(),
            new[]
            {
                new RequiredDecisionDefinition(
                    requiredDecisionKey,
                    "Review type",
                    "Required decision",
                    CanonicalJsonValue.From("string"),
                    "protocol-approval",
                    "protocol-approval",
                    requiredDecisionKey,
                    waiverSuppliesReviewType)
            },
            Researcher,
            Clock);

        if (waiverSuppliesReviewType)
        {
            draft.AddUnresolvedDecision(
                ids,
                requiredDecisionKey,
                "Review type unresolved under approved waiver.",
                "Waiver authorizes workflow planning without this conduct decision.",
                "protocol-approval",
                Researcher,
                Clock,
                blocksProtocolApproval: false);
        }
        else
        {
            draft.RecordDecision(
                ids,
                requiredDecisionKey,
                CanonicalJsonValue.From("systematic-review"),
                Researcher,
                Clock,
                "Decision required by workflow input.");
        }

        if (withWaiver)
        {
            draft.AddWaiver(
                ids,
                "review-type",
                null,
                waiverExpired ? Clock.UtcNow : waiverExpiresAt,
                "Limited scope allowed.",
                "Report the scope limitation.",
                "review-type",
                Researcher,
                Clock,
                ApprovalPolicy.ExplicitCustomSingleResearcher(),
                waiverExpired ? Array.Empty<string>() : new[] { "approval-1" });
        }

        var candidate = draft.CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher(), versionId: "proto-v1");
        var approval = ProtocolApproval.Create(ids, candidate, ApprovalPolicy.ExplicitCustomSingleResearcher(), Researcher, Clock, candidate.ContentDigest);
        var authority = draft.ApproveCandidateVerified(
            candidate,
            ApprovalPolicy.ExplicitCustomSingleResearcher(),
            new[] { approval },
            Clock);
        ProtocolAuthorities.Add(authority.Version, authority);
        return authority.Version;
    }

    private static ProtocolVersion RecastProtocol(
        ProtocolVersion source,
        ProtocolStatus status,
        string? amendmentId = null,
        ContentDigest? contentDigest = null)
    {
        var approvedAt = status == ProtocolStatus.Approved || status == ProtocolStatus.Superseded ? source.ApprovedAt : null;
        var approvalIds = status == ProtocolStatus.Approved || status == ProtocolStatus.Superseded
            ? source.ApprovalIds
            : Array.Empty<string>();

        var recast = new ProtocolVersion(
            source.Id,
            source.ProtocolId,
            source.ProjectId,
            source.VersionNumber,
            status,
            source.Template,
            source.Intent,
            source.Values,
            source.RequiredDecisions,
            source.Decisions,
            source.Waivers,
            contentDigest ?? source.ContentDigest,
            source.ApprovalPolicyId,
            approvalIds,
            approvedAt,
            source.SupersedesVersionId,
            source.SupersededByVersionId,
            amendmentId ?? source.AmendmentId,
            source.UnresolvedDecisions);

        if (!contentDigest.HasValue && !string.Equals(recast.AmendmentId, source.AmendmentId, StringComparison.Ordinal))
        {
            return new ProtocolVersion(
                recast.Id,
                recast.ProtocolId,
                recast.ProjectId,
                recast.VersionNumber,
                recast.Status,
                recast.Template,
                recast.Intent,
                recast.Values,
                recast.RequiredDecisions,
                recast.Decisions,
                recast.Waivers,
                recast.ToProtocolContentDigestEnvelope().ComputeDigest(),
                recast.ApprovalPolicyId,
                recast.ApprovalIds,
                recast.ApprovedAt,
                recast.SupersedesVersionId,
                recast.SupersededByVersionId,
                recast.AmendmentId,
                recast.UnresolvedDecisions);
        }

        return recast;
    }

    private static ProtocolAmendment BuildAmendment(ProtocolVersion version, string affectedNodeId = "approve")
    {
        var ids = new SequenceIdGenerator();
        var notice = new ProtocolInvalidationNotice(
            "notice-1",
            Guid.NewGuid().ToString("D"),
            "review-type",
            ComputeArtifactDigest(new WorkflowTemplateArtifactDeclaration(
                "search-plan",
                "workflow-artifact",
                "nexus.workflow.artifact",
                "1.0.0",
                "start",
                Array.Empty<string>(),
                null)),
            affectedNodeId,
            "screening changed",
            "rerun review",
            Clock.UtcNow);

        return ProtocolAmendment.Create(
            ids,
            version,
            "proto-v2",
            Researcher,
            Clock,
            "Reviewed criteria changed.",
            new[] { "review-type" },
            new[] { notice },
            ApprovalPolicy.ExplicitCustomSingleResearcher());
    }

    private static ContentDigest ComputeArtifactDigest(WorkflowTemplateArtifactDeclaration declaration)
    {
        var canonical = new CanonicalJsonObject()
            .Add("artifact_ref", declaration.ArtifactRef)
            .Add("artifact_kind", declaration.ArtifactKind)
            .Add("schema_id", declaration.SchemaId)
            .Add("schema_version", declaration.SchemaVersion)
            .Add("produced_by_node_id", declaration.ProducedByNodeId);
        return ContentDigest.Sha256CanonicalJson(canonical);
    }

    private static WorkflowTemplate WithRequiredInputSchema(WorkflowTemplate template, string schemaId, string schemaVersion)
    {
        var inputs = template.RequiredInputs.ToArray();
        inputs[0] = new WorkflowTemplateInput(
            inputs[0].InputId,
            inputs[0].InputKind,
            schemaId,
            schemaVersion,
            inputs[0].Required,
            inputs[0].SourceProtocolDecisionKey,
            inputs[0].DefaultValue);
        return template with { RequiredInputs = inputs };
    }

    private sealed class TestWorkflowAuthorityResolver : IWorkflowAuthorityResolver
    {
        private readonly VerifiedProtocolVersion _protocol;
        private readonly WorkflowTemplate _template;
        private readonly IReadOnlyDictionary<string, VerifiedProtocolWaiver> _waivers;
        private readonly VerifiedProtocolAmendment? _amendment;

        public TestWorkflowAuthorityResolver(
            VerifiedProtocolVersion protocol,
            WorkflowTemplate template,
            IEnumerable<VerifiedProtocolWaiver>? waivers = null,
            VerifiedProtocolAmendment? amendment = null)
        {
            _protocol = protocol;
            _template = template;
            _waivers = (waivers ?? Array.Empty<VerifiedProtocolWaiver>())
                .ToDictionary(item => item.Waiver.WaiverId, StringComparer.Ordinal);
            _amendment = amendment;
        }

        public VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId) =>
            string.Equals(protocolVersionId, _protocol.Version.Id, StringComparison.Ordinal) ? _protocol : null!;

        public VerifiedProtocolWaiver ResolveProtocolWaiver(string waiverId) =>
            _waivers.TryGetValue(waiverId, out var waiver) ? waiver : null!;

        public VerifiedProtocolAmendment ResolveProtocolAmendment(string amendmentId) =>
            string.Equals(amendmentId, _amendment?.Amendment.AmendmentId, StringComparison.Ordinal) ? _amendment! : null!;

        public WorkflowTemplate ResolveTemplate(string templateId, string templateVersion, ContentDigest expectedDigest) =>
            string.Equals(templateId, _template.TemplateId, StringComparison.Ordinal) &&
            string.Equals(templateVersion, _template.TemplateVersion, StringComparison.Ordinal) &&
            expectedDigest == _template.TemplateDigest
                ? _template
                : null!;

        public CanonicalJsonValue ResolveCompileParameter(string inputId, ContentDigest expectedValueDigest) => null!;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class SequenceIdGenerator : IIdGenerator
    {
        private int _next = 1;

        public Guid NewId()
        {
            return new Guid(_next++, 0, 0, new byte[8]);
        }
    }
}
