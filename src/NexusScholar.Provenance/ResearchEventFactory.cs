using System.Linq;
using NexusScholar.Kernel;

namespace NexusScholar.Provenance;

public static class ResearchEventFactory
{
    public static ResearchEvent Create(
        IIdGenerator ids,
        IClock clock,
        string activity,
        string subjectType,
        string subjectId,
        ActorId performedBy,
        IEnumerable<ContentDigest>? inputs = null,
        IEnumerable<ContentDigest>? outputs = null)
    {
        return Create(
            ids,
            clock,
            new ProvenanceActivity(activity, Guard.NotBlank(activity, nameof(activity)), true, false, false),
            new ProvenanceEntityRef(Guard.NotBlank(subjectType, nameof(subjectType)), Guard.NotBlank(subjectId, nameof(subjectId))),
            ProvenanceAgent.Human(performedBy),
            inputs?.Select((input, index) => new ProvenanceEntityRef(
                "artifact",
                $"legacy-{activity}-input-{index:D3}",
                input)).ToArray(),
            outputs?.Select((output, index) => new ProvenanceEntityRef(
                "artifact",
                $"legacy-{activity}-output-{index:D3}",
                output)).ToArray());
    }

    public static ResearchEvent Create(
        IIdGenerator ids,
        IClock clock,
        ProvenanceActivity activity,
        ProvenanceEntityRef subject,
        ProvenanceAgent agent,
        IEnumerable<ProvenanceEntityRef>? inputs = null,
        IEnumerable<ProvenanceEntityRef>? outputs = null,
        ProvenanceProtocolBinding? protocolBinding = null,
        ProvenanceWorkflowBinding? workflowBinding = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(agent);

        var normalizedInputs = (inputs ?? Array.Empty<ProvenanceEntityRef>()).ToArray();
        var normalizedOutputs = (outputs ?? Array.Empty<ProvenanceEntityRef>()).ToArray();

        ValidateEntityKind(subject);
        foreach (var input in normalizedInputs)
        {
            ValidateEntityKind(input);
        }

        foreach (var output in normalizedOutputs)
        {
            ValidateEntityKind(output);
        }

        if (activity.RequiresActor && string.IsNullOrWhiteSpace(agent.AgentId))
        {
            throw new ProvenanceRuleException(ProvenanceErrorCodes.MissingActor, "Activity requires an actor.");
        }

        if (activity.RequiresInput)
        {
            if (normalizedInputs.Length == 0 ||
                normalizedInputs.Any(input => input.Digest is null))
            {
                throw new ProvenanceRuleException(
                    ProvenanceErrorCodes.MissingRequiredInput,
                    "Required inputs must include content digests.");
            }
        }

        if (activity.RequiresOutput)
        {
            if (normalizedOutputs.Length == 0 ||
                normalizedOutputs.Any(output => output.Digest is null))
            {
                throw new ProvenanceRuleException(
                    ProvenanceErrorCodes.MissingRequiredOutput,
                    "Required outputs must include content digests.");
            }
        }

        var eventId = EntityId<ProvenanceEventTag>.New(ids);
        var occurredAt = clock.UtcNow;

        var eventDigest = new ResearchEvent(
            eventId,
            agent,
            activity,
            occurredAt,
            subject,
            normalizedInputs,
            normalizedOutputs,
            default,
            protocolBinding,
            workflowBinding).ToDigestEnvelope().ComputeDigest();

        return new ResearchEvent(
            eventId,
            agent,
            activity,
            occurredAt,
            subject,
            normalizedInputs,
            normalizedOutputs,
            eventDigest,
            protocolBinding,
            workflowBinding);
    }

    private static void ValidateEntityKind(ProvenanceEntityRef reference)
    {
        if (!ProvenanceEntityRef.IsCanonicalKind(reference.EntityKind))
        {
            throw new ProvenanceRuleException(
                ProvenanceErrorCodes.ProjectionNotCanonical,
                $"Entity kind '{reference.EntityKind}' is not canonical provenance.");
        }
    }
}
