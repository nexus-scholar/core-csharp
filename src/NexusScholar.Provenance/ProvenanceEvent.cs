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
    public ResearchEvent(
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
