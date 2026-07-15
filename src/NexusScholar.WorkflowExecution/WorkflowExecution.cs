using NexusScholar.Kernel;
using NexusScholar.Workflow;

namespace NexusScholar.WorkflowExecution;

public enum WorkflowExecutionState
{
    Pending,
    Ready,
    Active,
    Blocked,
    Completed,
    Failed,
    Invalidated,
    Superseded
}

public enum WorkflowExecutionEventKind
{
    DependenciesSatisfied,
    WorkStarted,
    WorkBlocked,
    BlockCleared,
    WorkCompleted,
    WorkFailed,
    RetryAuthorized,
    WorkInvalidated,
    SuccessorBound
}

public static class WorkflowExecutionActorKinds
{
    public const string Human = "human";
    public const string Automation = "automation";
}

public static class WorkflowExecutionErrorCodes
{
    public const string UnverifiedAuthority = "unverified-authority";
    public const string InvalidTransition = "invalid-transition";
    public const string StaleJournalHead = "stale-journal-head";
    public const string InvalidJournalChain = "invalid-journal-chain";
    public const string UnknownNode = "unknown-node";
    public const string DependencyIncomplete = "dependency-incomplete";
    public const string UnauthorizedActor = "unauthorized-actor";
    public const string AutomationHumanAuthority = "automation-human-authority";
    public const string InvalidAttempt = "invalid-attempt";
    public const string MissingOutput = "missing-output";
    public const string InvalidApproval = "invalid-approval";
    public const string InvalidInvalidation = "invalid-invalidation";
    public const string ConflictingRequest = "conflicting-request";
}

public sealed class WorkflowExecutionRuleException : DomainRuleException
{
    public WorkflowExecutionRuleException(string category, string message)
        : base(message)
    {
        Category = Guard.NotBlank(category, nameof(category));
    }

    public string Category { get; }
}

public sealed record WorkflowExecutionRecordRef(string Kind, string Id, ContentDigest Digest)
{
    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("kind", Guard.NotBlank(Kind, nameof(Kind)))
        .Add("id", Guard.NotBlank(Id, nameof(Id)))
        .Add("digest", RequireDigest(Digest, nameof(Digest)).ToString());

    internal static ContentDigest RequireDigest(ContentDigest digest, string name) => digest.IsValid
        ? digest
        : throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.UnverifiedAuthority, $"{name} must be valid.");
}

public sealed record WorkflowExecutionActor(string ActorId, string Kind, string Role)
{
    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("actor_id", Guard.NotBlank(ActorId, nameof(ActorId)))
        .Add("kind", NormalizeActorKind(Kind))
        .Add("role", Guard.NotBlank(Role, nameof(Role)));

    internal static string NormalizeActorKind(string kind)
    {
        var normalized = Guard.NotBlank(kind, nameof(kind)).ToLowerInvariant();
        return normalized is WorkflowExecutionActorKinds.Human or WorkflowExecutionActorKinds.Automation
            ? normalized
            : throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.UnauthorizedActor, "Unknown execution actor kind.");
    }
}

public sealed record WorkflowExecutionRoleAssignment(string ActorId, string Role)
{
    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("actor_id", Guard.NotBlank(ActorId, nameof(ActorId)))
        .Add("role", Guard.NotBlank(Role, nameof(Role)));
}

public sealed class WorkflowExecutionAuthorityPolicy
{
    private const string SchemaId = "nexus.workflow-execution.authority-policy";
    private const string SchemaVersion = "1.0.0";

    private WorkflowExecutionAuthorityPolicy(
        string policyId,
        WorkflowExecutionRecordRef executionScope,
        string workflowId,
        ContentDigest workflowDigest,
        string protocolVersionId,
        ContentDigest protocolContentDigest,
        IReadOnlyList<WorkflowExecutionRoleAssignment> assignments,
        WorkflowExecutionActor approvedBy,
        DateTimeOffset approvedAt)
    {
        PolicyId = Guard.NotBlank(policyId, nameof(policyId));
        ExecutionScope = executionScope;
        WorkflowId = Guard.NotBlank(workflowId, nameof(workflowId));
        WorkflowDigest = workflowDigest;
        ProtocolVersionId = Guard.NotBlank(protocolVersionId, nameof(protocolVersionId));
        ProtocolContentDigest = protocolContentDigest;
        Assignments = assignments;
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        Digest = new DigestEnvelope(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion, BuildContent()).ComputeDigest();
    }

    public string PolicyId { get; }
    public WorkflowExecutionRecordRef ExecutionScope { get; }
    public string WorkflowId { get; }
    public ContentDigest WorkflowDigest { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public IReadOnlyList<WorkflowExecutionRoleAssignment> Assignments { get; }
    public WorkflowExecutionActor ApprovedBy { get; }
    public DateTimeOffset ApprovedAt { get; }
    public ContentDigest Digest { get; }

    public static WorkflowExecutionAuthorityPolicy Create(
        string policyId,
        WorkflowExecutionRecordRef executionScope,
        VerifiedWorkflowDefinition workflow,
        IEnumerable<WorkflowExecutionRoleAssignment> assignments,
        WorkflowExecutionActor approvedBy,
        DateTimeOffset approvedAt)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(executionScope);
        ArgumentNullException.ThrowIfNull(assignments);
        ArgumentNullException.ThrowIfNull(approvedBy);
        _ = executionScope.ToCanonicalJson();

        if (WorkflowExecutionActor.NormalizeActorKind(approvedBy.Kind) != WorkflowExecutionActorKinds.Human)
        {
            throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.AutomationHumanAuthority, "Execution authority policy requires human approval.");
        }

        var knownRoles = workflow.ResolvedTemplate.Roles.Select(role => role.RoleId).ToHashSet(StringComparer.Ordinal);
        var normalized = assignments
            .Select(item => new WorkflowExecutionRoleAssignment(
                Guard.NotBlank(item.ActorId, nameof(item.ActorId)),
                Guard.NotBlank(item.Role, nameof(item.Role))))
            .OrderBy(item => item.ActorId, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0 || normalized.Distinct().Count() != normalized.Length || normalized.Any(item => !knownRoles.Contains(item.Role)))
        {
            throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.UnauthorizedActor, "Execution authority assignments must be unique and use declared Workflow roles.");
        }

        return new WorkflowExecutionAuthorityPolicy(
            policyId,
            executionScope,
            workflow.Definition.WorkflowId,
            workflow.Definition.WorkflowDigest,
            workflow.Definition.ProtocolVersionId,
            workflow.Definition.ProtocolContentDigest,
            Array.AsReadOnly(normalized),
            approvedBy,
            approvedAt);
    }

    public bool Authorizes(WorkflowExecutionActor actor) =>
        Assignments.Any(item => string.Equals(item.ActorId, actor.ActorId, StringComparison.Ordinal) && string.Equals(item.Role, actor.Role, StringComparison.Ordinal));

    public CanonicalJsonObject ToCanonicalJson() => new DigestEnvelope(
        DigestScope.CanonicalJsonRecord,
        SchemaId,
        SchemaVersion,
        BuildContent()).ToCanonicalJsonObject();

    private CanonicalJsonObject BuildContent() => new CanonicalJsonObject()
        .Add("policy_id", PolicyId)
        .Add("execution_scope", ExecutionScope.ToCanonicalJson())
        .Add("workflow_id", WorkflowId)
        .Add("workflow_digest", WorkflowDigest.ToString())
        .Add("protocol_version_id", ProtocolVersionId)
        .Add("protocol_content_digest", ProtocolContentDigest.ToString())
        .Add("assignments", CanonicalJsonValue.Array(Assignments.Select(item => item.ToCanonicalJson()).ToArray()))
        .Add("approved_by", ApprovedBy.ToCanonicalJson())
        .AddTimestamp("approved_at", ApprovedAt);
}

public sealed class WorkflowExecutionHeader
{
    private const string SchemaId = "nexus.workflow-execution.header";
    private const string SchemaVersion = "1.0.0";

    private WorkflowExecutionHeader(
        string executionId,
        WorkflowExecutionAuthorityPolicy policy,
        VerifiedWorkflowDefinition workflow,
        WorkflowExecutionActor createdBy,
        DateTimeOffset createdAt)
    {
        ExecutionId = Guard.NotBlank(executionId, nameof(executionId));
        AuthorityPolicyId = policy.PolicyId;
        AuthorityPolicyDigest = policy.Digest;
        ExecutionScope = policy.ExecutionScope;
        WorkflowId = workflow.Definition.WorkflowId;
        WorkflowDigest = workflow.Definition.WorkflowDigest;
        ProtocolId = workflow.Definition.ProtocolId;
        ProtocolVersionId = workflow.Definition.ProtocolVersionId;
        ProtocolVersionNumber = workflow.Definition.ProtocolVersionNumber;
        ProtocolContentDigest = workflow.Definition.ProtocolContentDigest;
        NodeIds = Array.AsReadOnly(workflow.Definition.Nodes.Select(node => node.NodeId).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Digest = new DigestEnvelope(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion, BuildContent()).ComputeDigest();
    }

    public string ExecutionId { get; }
    public string AuthorityPolicyId { get; }
    public ContentDigest AuthorityPolicyDigest { get; }
    public WorkflowExecutionRecordRef ExecutionScope { get; }
    public string WorkflowId { get; }
    public ContentDigest WorkflowDigest { get; }
    public string ProtocolId { get; }
    public string ProtocolVersionId { get; }
    public int ProtocolVersionNumber { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public IReadOnlyList<string> NodeIds { get; }
    public WorkflowExecutionActor CreatedBy { get; }
    public DateTimeOffset CreatedAt { get; }
    public ContentDigest Digest { get; }

    public static WorkflowExecutionHeader Create(
        string executionId,
        VerifiedWorkflowDefinition workflow,
        WorkflowExecutionAuthorityPolicy policy,
        WorkflowExecutionActor createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(createdBy);
        EnsureAuthorityBinding(workflow, policy);
        if (WorkflowExecutionActor.NormalizeActorKind(createdBy.Kind) != WorkflowExecutionActorKinds.Human || !policy.Authorizes(createdBy))
        {
            throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.UnauthorizedActor, "Execution creation requires an authorized human actor.");
        }

        return new WorkflowExecutionHeader(executionId, policy, workflow, createdBy, createdAt);
    }

    public CanonicalJsonObject ToCanonicalJson() => new DigestEnvelope(
        DigestScope.CanonicalJsonRecord,
        SchemaId,
        SchemaVersion,
        BuildContent()).ToCanonicalJsonObject();

    internal static void EnsureAuthorityBinding(VerifiedWorkflowDefinition workflow, WorkflowExecutionAuthorityPolicy policy)
    {
        if (!string.Equals(policy.WorkflowId, workflow.Definition.WorkflowId, StringComparison.Ordinal) ||
            policy.WorkflowDigest != workflow.Definition.WorkflowDigest ||
            !string.Equals(policy.ProtocolVersionId, workflow.Definition.ProtocolVersionId, StringComparison.Ordinal) ||
            policy.ProtocolContentDigest != workflow.Definition.ProtocolContentDigest)
        {
            throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.UnverifiedAuthority, "Execution authority policy does not bind the verified Workflow and Protocol.");
        }
    }

    private CanonicalJsonObject BuildContent() => new CanonicalJsonObject()
        .Add("execution_id", ExecutionId)
        .Add("authority_policy_id", AuthorityPolicyId)
        .Add("authority_policy_digest", AuthorityPolicyDigest.ToString())
        .Add("execution_scope", ExecutionScope.ToCanonicalJson())
        .Add("workflow_id", WorkflowId)
        .Add("workflow_digest", WorkflowDigest.ToString())
        .Add("protocol_id", ProtocolId)
        .Add("protocol_version_id", ProtocolVersionId)
        .Add("protocol_version_number", ProtocolVersionNumber)
        .Add("protocol_content_digest", ProtocolContentDigest.ToString())
        .Add("node_ids", CanonicalJsonValue.Array(NodeIds.Select(CanonicalJsonValue.From).ToArray()))
        .Add("created_by", CreatedBy.ToCanonicalJson())
        .AddTimestamp("created_at", CreatedAt);
}

public sealed record WorkflowExecutionApproval(WorkflowExecutionActor Actor, WorkflowExecutionRecordRef Record)
{
    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("actor", Actor.ToCanonicalJson())
        .Add("record", Record.ToCanonicalJson());
}

public sealed class WorkflowExecutionEvent
{
    private const string SchemaId = "nexus.workflow-execution.event";
    private const string SchemaVersion = "1.0.0";

    private WorkflowExecutionEvent(
        WorkflowExecutionHeader header,
        int ordinal,
        ContentDigest previousDigest,
        string requestId,
        ContentDigest requestDigest,
        string nodeId,
        WorkflowExecutionEventKind kind,
        WorkflowExecutionState expectedPriorState,
        WorkflowExecutionState resultingState,
        WorkflowExecutionActor actor,
        DateTimeOffset occurredAt,
        string rationale,
        string? attemptId,
        int? attemptSequence,
        IReadOnlyList<WorkflowExecutionRecordRef> inputs,
        IReadOnlyList<WorkflowExecutionRecordRef> outputs,
        IReadOnlyList<WorkflowExecutionApproval> approvals,
        WorkflowExecutionRecordRef? decision,
        string? errorCategory,
        string? errorSummary,
        WorkflowExecutionRecordRef? invalidationSource)
    {
        ExecutionId = header.ExecutionId;
        WorkflowId = header.WorkflowId;
        WorkflowDigest = header.WorkflowDigest;
        ProtocolVersionId = header.ProtocolVersionId;
        ProtocolContentDigest = header.ProtocolContentDigest;
        AuthorityPolicyId = header.AuthorityPolicyId;
        AuthorityPolicyDigest = header.AuthorityPolicyDigest;
        Ordinal = ordinal;
        PreviousDigest = previousDigest;
        RequestId = Guard.NotBlank(requestId, nameof(requestId));
        RequestDigest = requestDigest;
        EventId = $"execution-event-{requestDigest.Value[7..23]}";
        NodeId = Guard.NotBlank(nodeId, nameof(nodeId));
        Kind = kind;
        ExpectedPriorState = expectedPriorState;
        ResultingState = resultingState;
        Actor = actor;
        OccurredAt = occurredAt;
        Rationale = Guard.NotBlank(rationale, nameof(rationale));
        AttemptId = attemptId;
        AttemptSequence = attemptSequence;
        Inputs = inputs;
        Outputs = outputs;
        Approvals = approvals;
        Decision = decision;
        ErrorCategory = errorCategory;
        ErrorSummary = errorSummary;
        InvalidationSource = invalidationSource;
        Digest = new DigestEnvelope(DigestScope.CanonicalJsonRecord, SchemaId, SchemaVersion, BuildContent()).ComputeDigest();
    }

    public string ExecutionId { get; }
    public string WorkflowId { get; }
    public ContentDigest WorkflowDigest { get; }
    public string ProtocolVersionId { get; }
    public ContentDigest ProtocolContentDigest { get; }
    public string AuthorityPolicyId { get; }
    public ContentDigest AuthorityPolicyDigest { get; }
    public int Ordinal { get; }
    public ContentDigest PreviousDigest { get; }
    public string EventId { get; }
    public string RequestId { get; }
    public ContentDigest RequestDigest { get; }
    public string NodeId { get; }
    public WorkflowExecutionEventKind Kind { get; }
    public WorkflowExecutionState ExpectedPriorState { get; }
    public WorkflowExecutionState ResultingState { get; }
    public WorkflowExecutionActor Actor { get; }
    public DateTimeOffset OccurredAt { get; }
    public string Rationale { get; }
    public string? AttemptId { get; }
    public int? AttemptSequence { get; }
    public IReadOnlyList<WorkflowExecutionRecordRef> Inputs { get; }
    public IReadOnlyList<WorkflowExecutionRecordRef> Outputs { get; }
    public IReadOnlyList<WorkflowExecutionApproval> Approvals { get; }
    public WorkflowExecutionRecordRef? Decision { get; }
    public string? ErrorCategory { get; }
    public string? ErrorSummary { get; }
    public WorkflowExecutionRecordRef? InvalidationSource { get; }
    public ContentDigest Digest { get; }

    public static WorkflowExecutionEvent Create(
        WorkflowExecutionHeader header,
        int ordinal,
        ContentDigest previousDigest,
        string requestId,
        string nodeId,
        WorkflowExecutionEventKind kind,
        WorkflowExecutionState expectedPriorState,
        WorkflowExecutionState resultingState,
        WorkflowExecutionActor actor,
        DateTimeOffset occurredAt,
        string rationale,
        string? attemptId = null,
        int? attemptSequence = null,
        IEnumerable<WorkflowExecutionRecordRef>? inputs = null,
        IEnumerable<WorkflowExecutionRecordRef>? outputs = null,
        IEnumerable<WorkflowExecutionApproval>? approvals = null,
        WorkflowExecutionRecordRef? decision = null,
        string? errorCategory = null,
        string? errorSummary = null,
        WorkflowExecutionRecordRef? invalidationSource = null)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(actor);
        var inputArray = NormalizeRefs(inputs);
        var outputArray = NormalizeRefs(outputs);
        var approvalArray = (approvals ?? Array.Empty<WorkflowExecutionApproval>()).
            OrderBy(item => item.Actor.ActorId, StringComparer.Ordinal).ThenBy(item => item.Record.Id, StringComparer.Ordinal).ToArray();
        var requestContent = BuildRequestContent(
            header, requestId, nodeId, kind, expectedPriorState, resultingState, actor, rationale,
            attemptId, attemptSequence, inputArray, outputArray, approvalArray, decision, errorCategory, errorSummary, invalidationSource);
        var requestDigest = new DigestEnvelope(DigestScope.CanonicalJsonRecord, "nexus.workflow-execution.request", "1.0.0", requestContent).ComputeDigest();
        return new WorkflowExecutionEvent(
            header, ordinal, WorkflowExecutionRecordRef.RequireDigest(previousDigest, nameof(previousDigest)), requestId,
            requestDigest, nodeId, kind, expectedPriorState, resultingState, actor, occurredAt, rationale,
            attemptId, attemptSequence, Array.AsReadOnly(inputArray), Array.AsReadOnly(outputArray),
            Array.AsReadOnly(approvalArray), decision, errorCategory, errorSummary, invalidationSource);
    }

    public CanonicalJsonObject ToCanonicalJson() => new DigestEnvelope(
        DigestScope.CanonicalJsonRecord,
        SchemaId,
        SchemaVersion,
        BuildContent()).ToCanonicalJsonObject();

    private CanonicalJsonObject BuildContent() => BuildRequestContent(
            ExecutionId, WorkflowId, WorkflowDigest, ProtocolVersionId, ProtocolContentDigest,
            AuthorityPolicyId, AuthorityPolicyDigest, RequestId, NodeId, Kind, ExpectedPriorState,
            ResultingState, Actor, Rationale, AttemptId, AttemptSequence, Inputs, Outputs, Approvals,
            Decision, ErrorCategory, ErrorSummary, InvalidationSource)
        .Add("event_id", EventId)
        .Add("ordinal", Ordinal)
        .Add("previous_digest", PreviousDigest.ToString())
        .Add("request_digest", RequestDigest.ToString())
        .AddTimestamp("occurred_at", OccurredAt);

    private static CanonicalJsonObject BuildRequestContent(
        WorkflowExecutionHeader header, string requestId, string nodeId, WorkflowExecutionEventKind kind,
        WorkflowExecutionState expectedPriorState, WorkflowExecutionState resultingState, WorkflowExecutionActor actor,
        string rationale, string? attemptId, int? attemptSequence, IReadOnlyList<WorkflowExecutionRecordRef> inputs,
        IReadOnlyList<WorkflowExecutionRecordRef> outputs, IReadOnlyList<WorkflowExecutionApproval> approvals,
        WorkflowExecutionRecordRef? decision, string? errorCategory, string? errorSummary,
        WorkflowExecutionRecordRef? invalidationSource) => BuildRequestContent(
            header.ExecutionId, header.WorkflowId, header.WorkflowDigest, header.ProtocolVersionId,
            header.ProtocolContentDigest, header.AuthorityPolicyId, header.AuthorityPolicyDigest, requestId,
            nodeId, kind, expectedPriorState, resultingState, actor, rationale, attemptId, attemptSequence,
            inputs, outputs, approvals, decision, errorCategory, errorSummary, invalidationSource);

    private static CanonicalJsonObject BuildRequestContent(
        string executionId, string workflowId, ContentDigest workflowDigest, string protocolVersionId,
        ContentDigest protocolContentDigest, string authorityPolicyId, ContentDigest authorityPolicyDigest,
        string requestId, string nodeId, WorkflowExecutionEventKind kind, WorkflowExecutionState expectedPriorState,
        WorkflowExecutionState resultingState, WorkflowExecutionActor actor, string rationale, string? attemptId,
        int? attemptSequence, IReadOnlyList<WorkflowExecutionRecordRef> inputs, IReadOnlyList<WorkflowExecutionRecordRef> outputs,
        IReadOnlyList<WorkflowExecutionApproval> approvals, WorkflowExecutionRecordRef? decision,
        string? errorCategory, string? errorSummary, WorkflowExecutionRecordRef? invalidationSource)
    {
        var content = new CanonicalJsonObject()
            .Add("execution_id", executionId)
            .Add("workflow_id", workflowId)
            .Add("workflow_digest", workflowDigest.ToString())
            .Add("protocol_version_id", protocolVersionId)
            .Add("protocol_content_digest", protocolContentDigest.ToString())
            .Add("authority_policy_id", authorityPolicyId)
            .Add("authority_policy_digest", authorityPolicyDigest.ToString())
            .Add("request_id", Guard.NotBlank(requestId, nameof(requestId)))
            .Add("node_id", Guard.NotBlank(nodeId, nameof(nodeId)))
            .Add("event_kind", ToToken(kind))
            .Add("expected_prior_state", ToToken(expectedPriorState))
            .Add("resulting_state", ToToken(resultingState))
            .Add("actor", actor.ToCanonicalJson())
            .Add("rationale", Guard.NotBlank(rationale, nameof(rationale)))
            .Add("inputs", CanonicalJsonValue.Array(inputs.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("outputs", CanonicalJsonValue.Array(outputs.Select(item => item.ToCanonicalJson()).ToArray()))
            .Add("approvals", CanonicalJsonValue.Array(approvals.Select(item => item.ToCanonicalJson()).ToArray()));
        AddOptional(content, "attempt_id", attemptId);
        if (attemptSequence is not null) content.Add("attempt_sequence", attemptSequence.Value);
        if (decision is not null) content.Add("decision", decision.ToCanonicalJson());
        AddOptional(content, "error_category", errorCategory);
        AddOptional(content, "error_summary", errorSummary);
        if (invalidationSource is not null) content.Add("invalidation_source", invalidationSource.ToCanonicalJson());
        return content;
    }

    private static WorkflowExecutionRecordRef[] NormalizeRefs(IEnumerable<WorkflowExecutionRecordRef>? refs) =>
        (refs ?? Array.Empty<WorkflowExecutionRecordRef>())
            .Select(item => { _ = item.ToCanonicalJson(); return item; })
            .OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Id, StringComparer.Ordinal).ThenBy(item => item.Digest.Value, StringComparer.Ordinal)
            .ToArray();

    private static void AddOptional(CanonicalJsonObject obj, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) obj.Add(name, value);
    }

    internal static string ToToken(WorkflowExecutionState value) => value switch
    {
        WorkflowExecutionState.Pending => "pending",
        WorkflowExecutionState.Ready => "ready",
        WorkflowExecutionState.Active => "active",
        WorkflowExecutionState.Blocked => "blocked",
        WorkflowExecutionState.Completed => "completed",
        WorkflowExecutionState.Failed => "failed",
        WorkflowExecutionState.Invalidated => "invalidated",
        WorkflowExecutionState.Superseded => "superseded",
        _ => throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.InvalidTransition, "Unknown execution state.")
    };

    private static string ToToken(WorkflowExecutionEventKind value) => value switch
    {
        WorkflowExecutionEventKind.DependenciesSatisfied => "dependencies-satisfied",
        WorkflowExecutionEventKind.WorkStarted => "work-started",
        WorkflowExecutionEventKind.WorkBlocked => "work-blocked",
        WorkflowExecutionEventKind.BlockCleared => "block-cleared",
        WorkflowExecutionEventKind.WorkCompleted => "work-completed",
        WorkflowExecutionEventKind.WorkFailed => "work-failed",
        WorkflowExecutionEventKind.RetryAuthorized => "retry-authorized",
        WorkflowExecutionEventKind.WorkInvalidated => "work-invalidated",
        WorkflowExecutionEventKind.SuccessorBound => "successor-bound",
        _ => throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.InvalidTransition, "Unknown execution event kind.")
    };
}

public sealed record WorkflowExecutionAttemptProjection(
    string AttemptId,
    int Sequence,
    WorkflowExecutionActor Agent,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    WorkflowExecutionState State,
    IReadOnlyList<WorkflowExecutionRecordRef> Inputs,
    IReadOnlyList<WorkflowExecutionRecordRef> Outputs,
    string? ErrorCategory,
    string? ErrorSummary);

public sealed class WorkflowExecutionProjection
{
    internal WorkflowExecutionProjection(
        IReadOnlyDictionary<string, WorkflowExecutionState> nodeStates,
        IReadOnlyDictionary<string, IReadOnlyList<WorkflowExecutionAttemptProjection>> attempts,
        ContentDigest headDigest)
    {
        NodeStates = nodeStates;
        Attempts = attempts;
        HeadDigest = headDigest;
    }

    public IReadOnlyDictionary<string, WorkflowExecutionState> NodeStates { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<WorkflowExecutionAttemptProjection>> Attempts { get; }
    public ContentDigest HeadDigest { get; }
}

public sealed class WorkflowExecutionJournal
{
    private readonly List<WorkflowExecutionEvent> _events = new();

    private WorkflowExecutionJournal(
        WorkflowExecutionHeader header,
        VerifiedWorkflowDefinition workflow,
        WorkflowExecutionAuthorityPolicy policy)
    {
        Header = header;
        Workflow = workflow;
        AuthorityPolicy = policy;
        Projection = Project();
    }

    public WorkflowExecutionHeader Header { get; }
    public VerifiedWorkflowDefinition Workflow { get; }
    public WorkflowExecutionAuthorityPolicy AuthorityPolicy { get; }
    public IReadOnlyList<WorkflowExecutionEvent> Events => _events.AsReadOnly();
    public WorkflowExecutionProjection Projection { get; private set; }

    public static WorkflowExecutionJournal Create(
        WorkflowExecutionHeader header,
        VerifiedWorkflowDefinition workflow,
        WorkflowExecutionAuthorityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(policy);
        WorkflowExecutionHeader.EnsureAuthorityBinding(workflow, policy);
        if (!string.Equals(header.WorkflowId, workflow.Definition.WorkflowId, StringComparison.Ordinal) ||
            header.WorkflowDigest != workflow.Definition.WorkflowDigest || header.AuthorityPolicyDigest != policy.Digest)
        {
            throw new WorkflowExecutionRuleException(WorkflowExecutionErrorCodes.UnverifiedAuthority, "Execution header does not resolve to supplied authority.");
        }
        return new WorkflowExecutionJournal(header, workflow, policy);
    }

    public static WorkflowExecutionJournal Rehydrate(
        WorkflowExecutionHeader header,
        IEnumerable<WorkflowExecutionEvent> events,
        VerifiedWorkflowDefinition workflow,
        WorkflowExecutionAuthorityPolicy policy)
    {
        var journal = Create(header, workflow, policy);
        foreach (var item in events ?? throw new ArgumentNullException(nameof(events))) journal.Append(item, allowIdempotentReplay: false);
        return journal;
    }

    public WorkflowExecutionEvent Append(WorkflowExecutionEvent item) => Append(item, allowIdempotentReplay: true);

    private WorkflowExecutionEvent Append(WorkflowExecutionEvent item, bool allowIdempotentReplay)
    {
        ArgumentNullException.ThrowIfNull(item);
        var replay = _events.SingleOrDefault(existing => string.Equals(existing.RequestId, item.RequestId, StringComparison.Ordinal));
        if (replay is not null)
        {
            if (!allowIdempotentReplay || replay.RequestDigest != item.RequestDigest)
                throw Rule(WorkflowExecutionErrorCodes.ConflictingRequest, "Execution request id was reused with different material.");
            return replay;
        }

        var expectedHead = _events.Count == 0 ? Header.Digest : _events[^1].Digest;
        if (item.Ordinal != _events.Count + 1 || item.PreviousDigest != expectedHead)
            throw Rule(WorkflowExecutionErrorCodes.InvalidJournalChain, "Execution event ordinal or previous digest does not extend the current journal.");
        if (!string.Equals(item.ExecutionId, Header.ExecutionId, StringComparison.Ordinal) ||
            !string.Equals(item.WorkflowId, Header.WorkflowId, StringComparison.Ordinal) || item.WorkflowDigest != Header.WorkflowDigest ||
            !string.Equals(item.ProtocolVersionId, Header.ProtocolVersionId, StringComparison.Ordinal) || item.ProtocolContentDigest != Header.ProtocolContentDigest ||
            !string.Equals(item.AuthorityPolicyId, Header.AuthorityPolicyId, StringComparison.Ordinal) || item.AuthorityPolicyDigest != Header.AuthorityPolicyDigest)
            throw Rule(WorkflowExecutionErrorCodes.UnverifiedAuthority, "Execution event authority binding does not match the journal header.");

        ValidateTransition(item);
        _events.Add(item);
        Projection = Project();
        return item;
    }

    private void ValidateTransition(WorkflowExecutionEvent item)
    {
        var node = Workflow.Definition.Nodes.SingleOrDefault(candidate => string.Equals(candidate.NodeId, item.NodeId, StringComparison.Ordinal))
            ?? throw Rule(WorkflowExecutionErrorCodes.UnknownNode, "Execution event references an unknown Workflow node.");
        var current = Projection.NodeStates[item.NodeId];
        if (item.Kind is WorkflowExecutionEventKind.WorkInvalidated or WorkflowExecutionEventKind.SuccessorBound)
            throw Rule(WorkflowExecutionErrorCodes.InvalidInvalidation, "Runtime invalidation is not admitted until complete batch propagation validation is implemented.");
        if (current != item.ExpectedPriorState || !Allowed(item.Kind, current, item.ResultingState))
            throw Rule(WorkflowExecutionErrorCodes.InvalidTransition, "Execution event does not match the current state or closed transition table.");
        if (!AuthorityPolicy.Authorizes(item.Actor))
            throw Rule(WorkflowExecutionErrorCodes.UnauthorizedActor, "Execution actor and role are not authorized by the bound policy.");

        if (item.Kind == WorkflowExecutionEventKind.DependenciesSatisfied)
        {
            var predecessors = Workflow.Definition.Edges.Where(edge => edge.ToNodeId == node.NodeId).Select(edge => edge.FromNodeId);
            if (predecessors.Any(id => Projection.NodeStates[id] != WorkflowExecutionState.Completed))
                throw Rule(WorkflowExecutionErrorCodes.DependencyIncomplete, "Workflow node dependencies are not complete and current.");
        }

        ValidateAttempt(item);
        ValidateHumanAuthority(node, item);
        ValidateOutputs(node, item);
    }

    private void ValidateAttempt(WorkflowExecutionEvent item)
    {
        var attemptKinds = item.Kind is WorkflowExecutionEventKind.WorkStarted or WorkflowExecutionEventKind.WorkCompleted or
            WorkflowExecutionEventKind.WorkFailed or WorkflowExecutionEventKind.WorkBlocked or WorkflowExecutionEventKind.BlockCleared;
        if (attemptKinds && (string.IsNullOrWhiteSpace(item.AttemptId) || item.AttemptSequence is null || item.AttemptSequence <= 0))
            throw Rule(WorkflowExecutionErrorCodes.InvalidAttempt, "Work attempt events require a stable id and positive sequence.");
        if (!attemptKinds && item.AttemptId is not null)
            throw Rule(WorkflowExecutionErrorCodes.InvalidAttempt, "This transition cannot bind an attempt.");

        var attempts = Projection.Attempts[item.NodeId];
        if (item.Kind == WorkflowExecutionEventKind.WorkStarted)
        {
            if (attempts.Any(value => value.AttemptId == item.AttemptId) || attempts.Any(value => value.EndedAt is null) ||
                item.AttemptSequence != attempts.Count + 1)
                throw Rule(WorkflowExecutionErrorCodes.InvalidAttempt, "Attempt identity or sequence would overwrite history.");
        }
        else if (item.Kind is WorkflowExecutionEventKind.WorkCompleted or WorkflowExecutionEventKind.WorkFailed)
        {
            var current = attempts.LastOrDefault();
            if (current is null || current.AttemptId != item.AttemptId || current.Sequence != item.AttemptSequence || current.EndedAt is not null)
                throw Rule(WorkflowExecutionErrorCodes.InvalidAttempt, "Attempt completion does not bind the current open attempt.");
            if (item.Kind == WorkflowExecutionEventKind.WorkFailed && (string.IsNullOrWhiteSpace(item.ErrorCategory) || string.IsNullOrWhiteSpace(item.ErrorSummary)))
                throw Rule(WorkflowExecutionErrorCodes.InvalidAttempt, "Failed work requires an error category and summary.");
        }
        else if (item.Kind is WorkflowExecutionEventKind.WorkBlocked or WorkflowExecutionEventKind.BlockCleared)
        {
            var current = attempts.LastOrDefault();
            if (current is null || current.AttemptId != item.AttemptId || current.Sequence != item.AttemptSequence || current.EndedAt is not null)
                throw Rule(WorkflowExecutionErrorCodes.InvalidAttempt, "Block transitions must bind the current open attempt.");
        }
    }

    private void ValidateHumanAuthority(WorkflowCompiledNode node, WorkflowExecutionEvent item)
    {
        if (item.Kind is not (WorkflowExecutionEventKind.WorkStarted or WorkflowExecutionEventKind.WorkCompleted)) return;
        var humanNode = node.Kind is WorkflowNodeKind.HumanTask or WorkflowNodeKind.Approval || node.Mode is WorkflowNodeMode.Human or WorkflowNodeMode.Hybrid;
        if (!humanNode) return;
        if (WorkflowExecutionActor.NormalizeActorKind(item.Actor.Kind) != WorkflowExecutionActorKinds.Human)
            throw Rule(WorkflowExecutionErrorCodes.AutomationHumanAuthority, "Automation cannot start or complete human-authority work.");

        var roles = Workflow.ResolvedTemplate.Gates.Where(gate => gate.TargetNodeId == node.NodeId)
            .SelectMany(gate => gate.RequiredActorRoles).ToHashSet(StringComparer.Ordinal);
        var requirement = string.IsNullOrWhiteSpace(node.ApprovalRequirementRef) ? null :
            Workflow.Definition.ApprovalRequirements.SingleOrDefault(value => value.ApprovalRequirementId == node.ApprovalRequirementRef);
        if (requirement is not null) roles.UnionWith(requirement.RequiredRoles);
        if (roles.Count == 0 || !roles.Contains(item.Actor.Role))
            throw Rule(WorkflowExecutionErrorCodes.UnauthorizedActor, "Human node role authority is absent or does not admit this actor role.");
        if (item.Kind == WorkflowExecutionEventKind.WorkCompleted && item.Decision is null)
            throw Rule(WorkflowExecutionErrorCodes.InvalidApproval, "Human work completion requires a digest-bound conduct record.");

        if (node.Kind == WorkflowNodeKind.Approval && item.Kind == WorkflowExecutionEventKind.WorkCompleted)
        {
            if (requirement is null || item.Approvals.Count < requirement.MinimumApprovals ||
                item.Approvals.Any(value => WorkflowExecutionActor.NormalizeActorKind(value.Actor.Kind) != WorkflowExecutionActorKinds.Human ||
                    !AuthorityPolicy.Authorizes(value.Actor) || !requirement.RequiredRoles.Contains(value.Actor.Role, StringComparer.Ordinal)) ||
                (requirement.RequiresDistinctActors && item.Approvals.Select(value => value.Actor.ActorId).Distinct(StringComparer.Ordinal).Count() != item.Approvals.Count))
                throw Rule(WorkflowExecutionErrorCodes.InvalidApproval, "Approval completion does not satisfy the compiled human approval requirement.");
        }
    }

    private static void ValidateOutputs(WorkflowCompiledNode node, WorkflowExecutionEvent item)
    {
        if (item.Kind != WorkflowExecutionEventKind.WorkCompleted) return;
        var outputIds = item.Outputs.Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        if (node.Produces.Any(required => !outputIds.Contains(required)))
            throw Rule(WorkflowExecutionErrorCodes.MissingOutput, "Work completion is missing a declared output artifact reference.");
    }

    private WorkflowExecutionProjection Project()
    {
        var states = Workflow.Definition.Nodes.ToDictionary(
            node => node.NodeId,
            node => Workflow.Definition.Edges.Any(edge => edge.ToNodeId == node.NodeId) ? WorkflowExecutionState.Pending : WorkflowExecutionState.Ready,
            StringComparer.Ordinal);
        var attempts = Workflow.Definition.Nodes.ToDictionary(node => node.NodeId, _ => new List<WorkflowExecutionAttemptProjection>(), StringComparer.Ordinal);
        var head = Header.Digest;
        foreach (var item in _events)
        {
            states[item.NodeId] = item.ResultingState;
            var nodeAttempts = attempts[item.NodeId];
            if (item.Kind == WorkflowExecutionEventKind.WorkStarted)
                nodeAttempts.Add(new WorkflowExecutionAttemptProjection(item.AttemptId!, item.AttemptSequence!.Value, item.Actor, item.OccurredAt, null, WorkflowExecutionState.Active, item.Inputs, Array.Empty<WorkflowExecutionRecordRef>(), null, null));
            else if (item.Kind is WorkflowExecutionEventKind.WorkCompleted or WorkflowExecutionEventKind.WorkFailed)
            {
                var current = nodeAttempts[^1];
                nodeAttempts[^1] = current with { EndedAt = item.OccurredAt, State = item.ResultingState, Outputs = item.Outputs, ErrorCategory = item.ErrorCategory, ErrorSummary = item.ErrorSummary };
            }
            else if (item.Kind is WorkflowExecutionEventKind.WorkBlocked or WorkflowExecutionEventKind.BlockCleared)
            {
                var current = nodeAttempts[^1];
                nodeAttempts[^1] = current with { State = item.ResultingState };
            }
            head = item.Digest;
        }
        return new WorkflowExecutionProjection(
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, WorkflowExecutionState>(states),
            new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<WorkflowExecutionAttemptProjection>>(
                attempts.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<WorkflowExecutionAttemptProjection>)pair.Value.AsReadOnly(), StringComparer.Ordinal)),
            head);
    }

    private static bool Allowed(WorkflowExecutionEventKind kind, WorkflowExecutionState prior, WorkflowExecutionState result) =>
        (kind, prior, result) switch
        {
            (WorkflowExecutionEventKind.DependenciesSatisfied, WorkflowExecutionState.Pending, WorkflowExecutionState.Ready) => true,
            (WorkflowExecutionEventKind.WorkStarted, WorkflowExecutionState.Ready, WorkflowExecutionState.Active) => true,
            (WorkflowExecutionEventKind.WorkBlocked, WorkflowExecutionState.Active, WorkflowExecutionState.Blocked) => true,
            (WorkflowExecutionEventKind.BlockCleared, WorkflowExecutionState.Blocked, WorkflowExecutionState.Active) => true,
            (WorkflowExecutionEventKind.WorkCompleted, WorkflowExecutionState.Active, WorkflowExecutionState.Completed) => true,
            (WorkflowExecutionEventKind.WorkFailed, WorkflowExecutionState.Active, WorkflowExecutionState.Failed) => true,
            (WorkflowExecutionEventKind.RetryAuthorized, WorkflowExecutionState.Failed, WorkflowExecutionState.Ready) => true,
            _ => false
        };

    private static WorkflowExecutionRuleException Rule(string category, string message) => new(category, message);
}
