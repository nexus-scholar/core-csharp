using System.Collections.ObjectModel;
using System.Linq;
using NexusScholar.Kernel;

namespace NexusScholar.Provenance;

public sealed class ProvenanceEventTag
{
}

public static class ProvenanceErrorCodes
{
    public const string DuplicateEventId = "duplicate-event-id";
    public const string MissingActor = "missing-actor";
    public const string MissingRequiredInput = "missing-required-input";
    public const string MissingRequiredOutput = "missing-required-output";
    public const string ProjectionNotCanonical = "projection-not-canonical";
    public const string InvalidEventId = "invalid-event-id";
    public const string InvalidAgent = "invalid-agent";
    public const string InvalidBinding = "invalid-binding";
    public const string InvalidTimestamp = "invalid-timestamp";
    public const string StaleEventDigest = "stale-event-digest";
}

public sealed class ProvenanceRuleException : DomainRuleException
{
    public ProvenanceRuleException(string category, string message)
        : base(message)
    {
        Category = Guard.NotBlank(category, nameof(category));
    }

    public string Category { get; }
}

public sealed record ProvenanceActivity(
    string ActivityId,
    string ActivityLabel,
    bool RequiresActor,
    bool RequiresInput,
    bool RequiresOutput)
{
    public string ActivityId { get; } = Guard.NotBlank(ActivityId, nameof(ActivityId));
    public string ActivityLabel { get; } = Guard.NotBlank(ActivityLabel, nameof(ActivityLabel));

    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("activity_id", ActivityId)
            .Add("activity_label", ActivityLabel)
            .Add("requires_actor", RequiresActor)
            .Add("requires_input", RequiresInput)
            .Add("requires_output", RequiresOutput);
    }
}

public sealed record ProvenanceAgent(string AgentId, string AgentKind, string? DisplayName = null)
{
    public static readonly string HumanKind = "human";
    public static readonly string Automation = "automation";
    public static readonly string Plugin = "plugin";
    public static readonly string System = "system";
    public static readonly string Import = "import";

    internal static readonly IReadOnlySet<string> SupportedKinds = new HashSet<string>(StringComparer.Ordinal)
    {
        HumanKind,
        Automation,
        Plugin,
        System,
        Import
    };

    public string AgentId { get; } = AgentId;
    public string AgentKind { get; } = Guard.NotBlank(AgentKind, nameof(AgentKind));

    public static ProvenanceAgent Human(ActorId actorId, string? displayName = null) =>
        new(actorId.Value, HumanKind, displayName);

    public CanonicalJsonObject ToCanonicalJson()
    {
        var obj = new CanonicalJsonObject()
            .Add("agent_id", AgentId)
            .Add("agent_kind", AgentKind);

        if (DisplayName is not null)
        {
            obj.Add("display_name", DisplayName);
        }

        return obj;
    }
}

public sealed record ProvenanceEntityRef(string EntityKind, string EntityId, ContentDigest? Digest = null)
{
    private static readonly string[] NonCanonicalEntityKinds =
    [
        "projection",
        "projection-cache",
        "cache",
        "wiki",
        "generated",
        "generated-narrative",
        "generated_narrative",
        "embedding-index",
        "embedding_index",
        "local-path",
        "local_path",
        "bundle",
        "container"
    ];

    public string EntityKind { get; } = Guard.NotBlank(EntityKind, nameof(EntityKind)).ToLowerInvariant();
    public string EntityId { get; } = Guard.NotBlank(EntityId, nameof(EntityId));

    public static bool IsCanonicalKind(string entityKind)
    {
        return Array.IndexOf(NonCanonicalEntityKinds, entityKind.ToLowerInvariant()) < 0;
    }

    public static void ValidateCanonicalKind(string entityKind)
    {
        if (!IsCanonicalKind(entityKind))
        {
            throw new ProvenanceRuleException(
                ProvenanceErrorCodes.ProjectionNotCanonical,
                $"Entity kind '{entityKind}' is not canonical provenance.");
        }
    }

    public CanonicalJsonObject ToCanonicalJson()
    {
        var obj = new CanonicalJsonObject()
            .Add("entity_kind", EntityKind)
            .Add("entity_id", EntityId);

        if (Digest is not null)
        {
            obj.Add("content_digest", Digest.Value.ToString());
        }

        return obj;
    }
}

public sealed record ProvenanceProtocolBinding(
    string ProtocolId,
    string ProtocolVersionId,
    int ProtocolVersionNumber,
    ContentDigest ProtocolContentDigest)
{
    public string ProtocolId { get; } = Guard.NotBlank(ProtocolId, nameof(ProtocolId));
    public string ProtocolVersionId { get; } = Guard.NotBlank(ProtocolVersionId, nameof(ProtocolVersionId));

    public CanonicalJsonObject ToCanonicalJson()
    {
        return new CanonicalJsonObject()
            .Add("protocol_id", ProtocolId)
            .Add("protocol_version_id", ProtocolVersionId)
            .Add("protocol_version_number", ProtocolVersionNumber)
            .Add("protocol_content_digest", ProtocolContentDigest.ToString());
    }
}

public sealed record ProvenanceWorkflowBinding(
    string WorkflowId,
    ContentDigest WorkflowDigest,
    string? WorkflowNodeId = null)
{
    public string WorkflowId { get; } = Guard.NotBlank(WorkflowId, nameof(WorkflowId));

    public CanonicalJsonObject ToCanonicalJson()
    {
        var obj = new CanonicalJsonObject()
            .Add("workflow_id", WorkflowId)
            .Add("workflow_digest", WorkflowDigest.ToString());

        if (WorkflowNodeId is not null)
        {
            obj.Add("workflow_node_id", WorkflowNodeId);
        }

        return obj;
    }
}

public sealed class ResearchEvent
{
    internal ResearchEvent(
        EntityId<ProvenanceEventTag> eventId,
        ProvenanceAgent agent,
        ProvenanceActivity activity,
        DateTimeOffset occurredAt,
        ProvenanceEntityRef subject,
        IReadOnlyList<ProvenanceEntityRef> inputs,
        IReadOnlyList<ProvenanceEntityRef> outputs,
        ContentDigest eventDigest,
        ProvenanceProtocolBinding? protocolBinding = null,
        ProvenanceWorkflowBinding? workflowBinding = null)
    {
        EventId = eventId;
        Id = eventId;
        Agent = agent ?? throw new ArgumentNullException(nameof(agent));
        Activity = activity ?? throw new ArgumentNullException(nameof(activity));
        OccurredAt = occurredAt;
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        ProtocolBinding = protocolBinding;
        WorkflowBinding = workflowBinding;
        Inputs = Snapshot(inputs);
        Outputs = Snapshot(outputs);
        EventDigest = eventDigest;
    }

    public EntityId<ProvenanceEventTag> EventId { get; }

    public EntityId<ProvenanceEventTag> Id { get; }

    public ProvenanceAgent Agent { get; }

    public ProvenanceActivity Activity { get; }

    public DateTimeOffset OccurredAt { get; }

    public ProvenanceEntityRef Subject { get; }

    public IReadOnlyList<ProvenanceEntityRef> Inputs { get; }

    public IReadOnlyList<ProvenanceEntityRef> Outputs { get; }

    public ContentDigest EventDigest { get; }

    public ProvenanceProtocolBinding? ProtocolBinding { get; }

    public ProvenanceWorkflowBinding? WorkflowBinding { get; }

    public CanonicalJsonObject ToDigestCanonicalJson()
    {
        var obj = new CanonicalJsonObject()
            .Add("event_id", EventId.ToString())
            .Add("agent", Agent.ToCanonicalJson())
            .Add("activity", Activity.ToCanonicalJson())
            .AddTimestamp("occurred_at", OccurredAt)
            .Add("subject", Subject.ToCanonicalJson())
            .Add("inputs", CanonicalJsonValue.Array(Inputs.Select(input => input.ToCanonicalJson()).ToArray()))
            .Add("outputs", CanonicalJsonValue.Array(Outputs.Select(output => output.ToCanonicalJson()).ToArray()));

        if (ProtocolBinding is not null)
        {
            obj.Add("protocol_binding", ProtocolBinding.ToCanonicalJson());
        }

        if (WorkflowBinding is not null)
        {
            obj.Add("workflow_binding", WorkflowBinding.ToCanonicalJson());
        }

        return obj;
    }

    public DigestEnvelope ToDigestEnvelope()
    {
        return new DigestEnvelope(
            DigestScope.ProvenanceEvent,
            "nexus.provenance-event",
            "1.0.0",
            ToDigestCanonicalJson());
    }

    public ResearchEvent CloneForStore()
    {
        return new ResearchEvent(
            EventId,
            Agent,
            Activity,
            OccurredAt,
            Subject,
            Inputs.ToArray(),
            Outputs.ToArray(),
            EventDigest,
            ProtocolBinding,
            WorkflowBinding);
    }

    private static IReadOnlyList<ProvenanceEntityRef> Snapshot(IReadOnlyList<ProvenanceEntityRef>? values)
    {
        return new ReadOnlyCollection<ProvenanceEntityRef>((values ?? Array.Empty<ProvenanceEntityRef>()).ToArray());
    }
}

internal static class ProvenanceEventValidator
{
    public static void Validate(ResearchEvent researchEvent, bool verifyDigest)
    {
        ArgumentNullException.ThrowIfNull(researchEvent);
        if (researchEvent.EventId == default)
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.InvalidEventId, "Provenance event id is required.");
        }
        if (researchEvent.OccurredAt.Offset != TimeSpan.Zero)
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.InvalidTimestamp, "Provenance event timestamp must be UTC.");
        }

        ValidateAgent(researchEvent.Agent, researchEvent.Activity.RequiresActor);
        ValidateEntity(researchEvent.Subject);
        foreach (var input in researchEvent.Inputs)
        {
            ValidateEntity(input);
        }
        foreach (var output in researchEvent.Outputs)
        {
            ValidateEntity(output);
        }
        ValidateRequiredEntities(researchEvent.Activity, researchEvent.Inputs, researchEvent.Outputs);
        ValidateBindings(researchEvent.ProtocolBinding, researchEvent.WorkflowBinding);

        if (verifyDigest && (!researchEvent.EventDigest.IsValid ||
            researchEvent.ToDigestEnvelope().ComputeDigest() != researchEvent.EventDigest))
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.StaleEventDigest, "Provenance event digest does not reproduce.");
        }
    }

    private static void ValidateAgent(ProvenanceAgent agent, bool required)
    {
        if (string.IsNullOrWhiteSpace(agent.AgentId))
        {
            throw new ProvenanceRuleException(
                required ? ProvenanceErrorCodes.MissingActor : ProvenanceErrorCodes.InvalidAgent,
                "Provenance agent id is required.");
        }
        if (!ProvenanceAgent.SupportedKinds.Contains(agent.AgentKind))
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.InvalidAgent, "Provenance agent kind is unsupported.");
        }
    }

    private static void ValidateEntity(ProvenanceEntityRef reference)
    {
        ProvenanceEntityRef.ValidateCanonicalKind(reference.EntityKind);
        if (reference.Digest is { IsValid: false })
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.InvalidBinding, "Provenance entity digest is invalid.");
        }
    }

    private static void ValidateRequiredEntities(
        ProvenanceActivity activity,
        IReadOnlyList<ProvenanceEntityRef> inputs,
        IReadOnlyList<ProvenanceEntityRef> outputs)
    {
        if (activity.RequiresInput && (inputs.Count == 0 || inputs.Any(item => item.Digest is null)))
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.MissingRequiredInput, "Required inputs must include content digests.");
        }
        if (activity.RequiresOutput && (outputs.Count == 0 || outputs.Any(item => item.Digest is null)))
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.MissingRequiredOutput, "Required outputs must include content digests.");
        }
    }

    private static void ValidateBindings(ProvenanceProtocolBinding? protocol, ProvenanceWorkflowBinding? workflow)
    {
        if (protocol is not null && (protocol.ProtocolVersionNumber <= 0 || !protocol.ProtocolContentDigest.IsValid))
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.InvalidBinding, "Protocol provenance binding is invalid.");
        }
        if (workflow is not null && (!workflow.WorkflowDigest.IsValid ||
            (workflow.WorkflowNodeId is not null && string.IsNullOrWhiteSpace(workflow.WorkflowNodeId))))
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.InvalidBinding, "Workflow provenance binding is invalid.");
        }
    }
}
