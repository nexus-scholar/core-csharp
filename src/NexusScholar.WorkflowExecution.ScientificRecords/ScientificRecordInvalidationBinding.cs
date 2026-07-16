using NexusScholar.Appraisal;
using NexusScholar.Extraction;
using NexusScholar.Kernel;
using NexusScholar.Synthesis;
using NexusScholar.WorkflowExecution;

namespace NexusScholar.WorkflowExecution.ScientificRecords;

public static class ScientificRecordInvalidationSchema
{
    public const string Id = "nexus.workflow-execution.scientific-record-invalidation";
    public const string Version = "1.0.0";
}

public sealed class ScientificRecordInvalidationRuleException : InvalidOperationException
{
    public ScientificRecordInvalidationRuleException(string message) : base(message) { }
}

public sealed record ScientificRecordInvalidationTarget(string Kind, ContentDigest Digest)
{
    internal CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("kind", Kind).Add("digest", Digest.ToString());
}

public sealed class VerifiedScientificRecordInvalidationBinding
{
    private VerifiedScientificRecordInvalidationBinding(
        string bindingId,
        string amendmentId,
        ContentDigest amendmentDigest,
        IReadOnlyList<ScientificRecordInvalidationTarget> targets,
        IReadOnlyList<string> workflowNodeIds)
    {
        BindingId = bindingId; AmendmentId = amendmentId; AmendmentDigest = amendmentDigest;
        Targets = targets; WorkflowNodeIds = workflowNodeIds;
        Envelope = new DigestEnvelope(DigestScope.CanonicalJsonRecord, ScientificRecordInvalidationSchema.Id, ScientificRecordInvalidationSchema.Version,
            new CanonicalJsonObject().Add("binding_id", bindingId).Add("amendment_id", amendmentId).Add("amendment_digest", amendmentDigest.ToString())
                .Add("targets", CanonicalJsonValue.Array(targets.Select(item => item.ToCanonicalJson()).ToArray()))
                .Add("workflow_node_ids", CanonicalJsonValue.Array(workflowNodeIds.Select(CanonicalJsonValue.From).ToArray())));
    }
    public string BindingId { get; }
    public string AmendmentId { get; }
    public ContentDigest AmendmentDigest { get; }
    public IReadOnlyList<ScientificRecordInvalidationTarget> Targets { get; }
    public IReadOnlyList<string> WorkflowNodeIds { get; }
    public DigestEnvelope Envelope { get; }
    public ContentDigest Digest => Envelope.ComputeDigest();
    public WorkflowExecutionRecordRef ToWorkflowRecordRef() => new("scientific-record-invalidation", BindingId, Digest);

    public static VerifiedScientificRecordInvalidationBinding Create(
        string bindingId,
        IEnumerable<string> workflowNodeIds,
        ExtractionAmendmentInvalidation? extraction = null,
        AppraisalAmendmentInvalidation? appraisal = null,
        SynthesisInvalidation? synthesis = null)
    {
        var authorities = new List<(string Id, ContentDigest Digest)>();
        var targets = new List<ScientificRecordInvalidationTarget>();
        if (extraction is not null)
        {
            authorities.Add((extraction.Amendment.Amendment.AmendmentId, extraction.Amendment.AmendmentDigest));
            targets.AddRange(extraction.AffectedRecordDigests.Select(item => new ScientificRecordInvalidationTarget("extraction-record", item)));
        }
        if (appraisal is not null)
        {
            authorities.Add((appraisal.Amendment.Amendment.AmendmentId, appraisal.Amendment.AmendmentDigest));
            targets.AddRange(appraisal.AffectedAppraisalDigests.Select(item => new ScientificRecordInvalidationTarget("appraisal-record", item)));
        }
        if (synthesis is not null)
        {
            authorities.Add((synthesis.AmendmentId, synthesis.AmendmentDigest));
            targets.AddRange(synthesis.TargetDigests.Select(item => new ScientificRecordInvalidationTarget("synthesis-plan", item)));
        }
        if (authorities.Count == 0 || authorities.Any(item => item != authorities[0]))
            throw new ScientificRecordInvalidationRuleException("Scientific record invalidations must bind one exact Protocol amendment.");
        var orderedTargets = targets.Distinct().OrderBy(item => item.Kind, StringComparer.Ordinal).ThenBy(item => item.Digest.ToString(), StringComparer.Ordinal).ToArray();
        if (orderedTargets.Count() != targets.Count || orderedTargets.Any(item => !item.Digest.IsValid))
            throw new ScientificRecordInvalidationRuleException("Scientific record invalidation targets must be valid and unique.");
        var nodes = (workflowNodeIds ?? throw new ArgumentNullException(nameof(workflowNodeIds))).Select(Required).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
        if (nodes.Length == 0) throw new ScientificRecordInvalidationRuleException("At least one affected Workflow node is required.");
        return new VerifiedScientificRecordInvalidationBinding(Required(bindingId), authorities[0].Id, authorities[0].Digest, Array.AsReadOnly(orderedTargets), Array.AsReadOnly(nodes));
    }

    private static string Required(string value) => !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new ScientificRecordInvalidationRuleException("Binding identifiers are required.");
}
