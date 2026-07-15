using NexusScholar.Screening;
using NexusScholar.Workflow;
using NexusScholar.WorkflowExecution;

namespace NexusScholar.Screening.WorkflowExecution;

public static class ScreeningWorkflowExecutionBridge
{
    public const string DecisionRecordKind = "screening-conduct-decision";

    public static WorkflowExecutionRecordRef CreateHumanTaskDecisionReference(
        ScreeningConductJournal journal,
        ScreeningConductDecision decision,
        WorkflowExecutionActor workflowActor)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(workflowActor);
        if (!journal.Decisions.Any(item => item.Digest == decision.Digest))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnverifiedConductAuthority, "Workflow completion requires an accepted Screening decision.");
        if (!string.Equals(decision.Actor.ActorId, workflowActor.ActorId, StringComparison.Ordinal) ||
            !string.Equals(decision.Actor.Role, workflowActor.Role, StringComparison.Ordinal) ||
            !string.Equals(decision.Actor.Kind, workflowActor.Kind, StringComparison.Ordinal) ||
            !string.Equals(workflowActor.Kind, WorkflowExecutionActorKinds.Human, StringComparison.Ordinal))
            throw new ScreeningRuleException(ScreeningErrorCodes.UnauthorizedReviewer, "Workflow and Screening actors must identify the same authorized human and role.");
        return new WorkflowExecutionRecordRef(DecisionRecordKind, decision.DecisionId, decision.Digest);
    }

    public static WorkflowExecutionEvent CreateHumanTaskCompletion(
        ScreeningConductJournal screeningJournal,
        ScreeningConductDecision decision,
        WorkflowExecutionJournal executionJournal,
        WorkflowExecutionActor workflowActor,
        string requestId,
        string nodeId,
        DateTimeOffset occurredAt,
        IEnumerable<WorkflowExecutionRecordRef>? outputs = null)
    {
        ArgumentNullException.ThrowIfNull(executionJournal);
        if (!string.Equals(screeningJournal.Header.ProtocolVersionId, executionJournal.Header.ProtocolVersionId, StringComparison.Ordinal) ||
            screeningJournal.Header.ProtocolContentDigest != executionJournal.Header.ProtocolContentDigest)
            throw new ScreeningRuleException(ScreeningErrorCodes.InvalidProtocolBinding, "Screening and Workflow execution must bind the same Protocol authority.");
        if (!executionJournal.Projection.NodeStates.TryGetValue(nodeId, out var state) || state != WorkflowExecutionState.Active)
            throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.InvalidTransition, "Screening human-task completion requires an active Workflow node.");
        var node = executionJournal.Workflow.Definition.Nodes.SingleOrDefault(item => item.NodeId == nodeId);
        if (node?.Kind != WorkflowNodeKind.HumanTask)
            throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.InvalidTransition, "Screening conduct can complete only a compiled human-task node.");
        var attempt = executionJournal.Projection.Attempts[nodeId].LastOrDefault(item => item.EndedAt is null)
            ?? throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.InvalidAttempt, "Screening human-task completion requires one open attempt.");
        var reference = CreateHumanTaskDecisionReference(screeningJournal, decision, workflowActor);
        var item = WorkflowExecutionEvent.Create(
            executionJournal.Header,
            executionJournal.Events.Count + 1,
            executionJournal.Projection.HeadDigest,
            requestId,
            nodeId,
            WorkflowExecutionEventKind.WorkCompleted,
            WorkflowExecutionState.Active,
            WorkflowExecutionState.Completed,
            workflowActor,
            occurredAt,
            decision.Rationale,
            attempt.AttemptId,
            attempt.Sequence,
            outputs: outputs,
            decision: reference);
        return item;
    }
}
