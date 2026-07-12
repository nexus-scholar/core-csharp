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

        var eventId = EntityId<ProvenanceEventTag>.New(ids);
        var occurredAt = clock.UtcNow;

        var unsigned = new ResearchEvent(
            eventId,
            agent,
            activity,
            occurredAt,
            subject,
            normalizedInputs,
            normalizedOutputs,
            default,
            protocolBinding,
            workflowBinding);
        ProvenanceEventValidator.Validate(unsigned, verifyDigest: false);
        var eventDigest = unsigned.ToDigestEnvelope().ComputeDigest();

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
}
