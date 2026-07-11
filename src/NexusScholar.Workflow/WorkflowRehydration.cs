using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Workflow;

public sealed record UnverifiedWorkflowDefinition(
    string WorkflowId,
    ContentDigest WorkflowDigest,
    string CompilerId,
    string CompilerVersion,
    string ProtocolId,
    string ProtocolVersionId,
    int ProtocolVersionNumber,
    ContentDigest ProtocolContentDigest,
    string TemplateId,
    string TemplateVersion,
    ContentDigest TemplateDigest,
    IReadOnlyList<WorkflowResolvedInputBinding> ResolvedInputBindings,
    IReadOnlyList<WorkflowCompiledNode> Nodes,
    IReadOnlyList<WorkflowCompiledEdge> Edges,
    IReadOnlyList<WorkflowCompiledApprovalRequirement> ApprovalRequirements,
    IReadOnlyList<WorkflowCompiledCapabilityRequirement> CapabilityRequirements,
    IReadOnlyList<WorkflowCompiledArtifactDeclaration> ArtifactDeclarations,
    IReadOnlyList<WorkflowInvalidationPlanEntry> InvalidationPlanEntries);

public interface IWorkflowAuthorityResolver
{
    VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId);

    WorkflowTemplate ResolveTemplate(string templateId, string templateVersion, ContentDigest expectedDigest);

    CanonicalJsonValue ResolveCompileParameter(string inputId, ContentDigest expectedValueDigest);
}

public sealed class VerifiedWorkflowDefinition
{
    internal VerifiedWorkflowDefinition(
        WorkflowDefinition definition,
        VerifiedProtocolVersion protocolAuthority)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        ProtocolAuthority = protocolAuthority ?? throw new ArgumentNullException(nameof(protocolAuthority));
    }

    public WorkflowDefinition Definition { get; }

    public VerifiedProtocolVersion ProtocolAuthority { get; }

}

public static class WorkflowRehydrator
{
    public static VerifiedWorkflowDefinition Rehydrate(
        UnverifiedWorkflowDefinition input,
        IWorkflowAuthorityResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(resolver);

        var protocolAuthority = resolver.ResolveProtocolVersion(Guard.NotBlank(input.ProtocolVersionId, nameof(input.ProtocolVersionId)))
            ?? throw Unverified("The referenced Protocol version could not be resolved.");
        var protocol = protocolAuthority.Version;
        ValidateProtocolBinding(input, protocol);

        var template = resolver.ResolveTemplate(
            Guard.NotBlank(input.TemplateId, nameof(input.TemplateId)),
            Guard.NotBlank(input.TemplateVersion, nameof(input.TemplateVersion)),
            RequireDigest(input.TemplateDigest, nameof(input.TemplateDigest)))
            ?? throw Unverified("The referenced Workflow template could not be resolved.");
        ValidateTemplateBinding(input, template);
        ValidateDefinitionShape(input, template);
        ValidateInputBindings(input, template, protocol, resolver);

        var expectedWorkflowId = WorkflowCompiler.ComputeWorkflowId(
            protocol,
            template,
            input.CompilerId,
            input.CompilerVersion);
        if (!string.Equals(input.WorkflowId, expectedWorkflowId, StringComparison.Ordinal))
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.WorkflowIdMismatch,
                "Persisted Workflow identity does not match resolved Protocol and template authority.");
        }

        var expectedDigest = WorkflowCompiler.ComputeWorkflowDigest(
            expectedWorkflowId,
            input.CompilerId,
            input.CompilerVersion,
            protocol,
            template,
            input.ResolvedInputBindings,
            input.Nodes,
            input.Edges,
            input.ApprovalRequirements,
            input.CapabilityRequirements,
            input.ArtifactDeclarations,
            input.InvalidationPlanEntries);
        if (expectedDigest != RequireDigest(input.WorkflowDigest, nameof(input.WorkflowDigest)))
        {
            throw Unverified("Persisted Workflow digest does not reproduce from resolved authority and content.");
        }

        var definition = new WorkflowDefinition(
            expectedWorkflowId,
            expectedDigest,
            input.CompilerId,
            input.CompilerVersion,
            protocol.ProtocolId,
            protocol.Id,
            protocol.VersionNumber,
            protocol.ContentDigest,
            template.TemplateId,
            template.TemplateVersion,
            template.TemplateDigest,
            input.ResolvedInputBindings,
            input.Nodes,
            input.Edges,
            input.ApprovalRequirements,
            input.CapabilityRequirements,
            input.ArtifactDeclarations,
            input.InvalidationPlanEntries);
        return new VerifiedWorkflowDefinition(definition, protocolAuthority);
    }

    public static UnverifiedWorkflowDefinition FromCompiled(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new UnverifiedWorkflowDefinition(
            definition.WorkflowId,
            definition.WorkflowDigest,
            definition.CompilerId,
            definition.CompilerVersion,
            definition.ProtocolId,
            definition.ProtocolVersionId,
            definition.ProtocolVersionNumber,
            definition.ProtocolContentDigest,
            definition.TemplateId,
            definition.TemplateVersion,
            definition.TemplateDigest,
            definition.ResolvedInputBindings,
            definition.Nodes,
            definition.Edges,
            definition.ApprovalRequirements,
            definition.CapabilityRequirements,
            definition.ArtifactDeclarations,
            definition.InvalidationPlanEntries);
    }

    private static void ValidateProtocolBinding(UnverifiedWorkflowDefinition input, ProtocolVersion protocol)
    {
        if (protocol.Status != ProtocolStatus.Approved ||
            !string.Equals(input.ProtocolId, protocol.ProtocolId, StringComparison.Ordinal) ||
            !string.Equals(input.ProtocolVersionId, protocol.Id, StringComparison.Ordinal) ||
            input.ProtocolVersionNumber != protocol.VersionNumber ||
            input.ProtocolContentDigest != protocol.ContentDigest)
        {
            throw Unverified("Workflow Protocol binding does not match the resolved approved Protocol version.");
        }

        if (protocol.Waivers.Count > 0 || !string.IsNullOrWhiteSpace(protocol.AmendmentId))
        {
            throw Unverified("Workflow rehydration cannot accept waiver or amendment authority before the ADR 0018 prerequisites exist.");
        }
    }

    private static void ValidateTemplateBinding(UnverifiedWorkflowDefinition input, WorkflowTemplate template)
    {
        WorkflowCompiler.ValidateResolvedTemplateAuthority(template);
        if (!string.Equals(input.TemplateId, template.TemplateId, StringComparison.Ordinal) ||
            !string.Equals(input.TemplateVersion, template.TemplateVersion, StringComparison.Ordinal) ||
            input.TemplateDigest != template.TemplateDigest ||
            WorkflowCompiler.ComputeTemplateDigest(template) != template.TemplateDigest)
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.StaleTemplateDigest,
                "Workflow template binding does not match resolved canonical template content.");
        }
    }

    private static void ValidateDefinitionShape(UnverifiedWorkflowDefinition input, WorkflowTemplate template)
    {
        if ((input.InvalidationPlanEntries?.Count ?? 0) > 0)
        {
            throw Unverified("Invalidation authority is unavailable under ADR 0018.");
        }

        EnsureUnique(input.ResolvedInputBindings, item => item.InputId, "resolved input bindings");
        EnsureUnique(input.Nodes, item => item.NodeId, "compiled nodes");
        EnsureUnique(input.Edges, item => $"{item.FromNodeId}\u001f{item.ToNodeId}\u001f{item.Condition}", "compiled edges");
        EnsureUnique(input.ApprovalRequirements, item => item.ApprovalRequirementId, "approval requirements");
        EnsureUnique(input.CapabilityRequirements, item => item.CapabilityRef, "capability requirements");
        EnsureUnique(input.ArtifactDeclarations, item => item.ArtifactRef, "artifact declarations");

        var nodeIds = input.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        foreach (var edge in input.Edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId) || edge.FromNodeId == edge.ToNodeId)
            {
                throw Unverified("Compiled Workflow edges must reference distinct known nodes.");
            }
        }

        var templateNodeIds = template.Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        if (!nodeIds.SetEquals(templateNodeIds))
        {
            throw Unverified("Compiled Workflow nodes do not match the resolved template.");
        }

        var templateInputIds = template.RequiredInputs.Select(item => item.InputId).ToHashSet(StringComparer.Ordinal);
        if (input.ResolvedInputBindings.Any(binding => !templateInputIds.Contains(binding.InputId)))
        {
            throw Unverified("Compiled Workflow contains an input not declared by the resolved template.");
        }

        if (!SetEquals(
                input.ApprovalRequirements.Select(item => item.ApprovalRequirementId),
                template.ApprovalRequirements.Select(item => item.ApprovalRequirementId)) ||
            !SetEquals(
                input.CapabilityRequirements.Select(item => item.CapabilityRef),
                template.CapabilityRequirements.Select(item => item.CapabilityRef)) ||
            !SetEquals(
                input.ArtifactDeclarations.Select(item => item.ArtifactRef),
                template.ArtifactDeclarations.Select(item => item.ArtifactRef)))
        {
            throw Unverified("Compiled Workflow requirements or artifacts do not match the resolved template.");
        }

        ValidateStaticTemplateProjection(input, template);
    }

    private static void ValidateStaticTemplateProjection(UnverifiedWorkflowDefinition input, WorkflowTemplate template)
    {
        foreach (var templateNode in template.Nodes)
        {
            var actual = input.Nodes.Single(node => string.Equals(node.NodeId, templateNode.NodeId, StringComparison.Ordinal));
            var expected = new WorkflowCompiledNode(
                templateNode.NodeId,
                templateNode.Label ?? string.Empty,
                templateNode.Kind,
                templateNode.Mode,
                template.Edges.Where(edge => edge.ToNodeId == templateNode.NodeId)
                    .Select(edge => edge.FromNodeId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                templateNode.Produces.ToArray(),
                templateNode.Requires.ToArray(),
                templateNode.ApprovalRequirementRef,
                templateNode.CapabilityRequirementRefs.ToArray(),
                templateNode.WaiverPolicyRef,
                templateNode.InvalidationPolicyRef,
                templateNode.Condition);
            EnsureCanonicalMatch(actual.ToCanonicalJson(), expected.ToCanonicalJson(), "compiled node");
        }

        EnsureCanonicalSetMatch(
            input.Edges.Select(item => item.ToCanonicalJson()),
            template.Edges.Select(item => new WorkflowCompiledEdge(item.FromNodeId, item.ToNodeId, item.Condition).ToCanonicalJson()),
            "compiled edges");
        EnsureCanonicalSetMatch(
            input.ApprovalRequirements.Select(item => item.ToCanonicalJson()),
            template.ApprovalRequirements.Select(item => new WorkflowCompiledApprovalRequirement(
                item.ApprovalRequirementId,
                item.PolicyId,
                item.PolicyVersion,
                item.PolicyMode,
                item.RequiredRoles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
                item.MinimumApprovals,
                item.RequiresDistinctActors,
                item.AllowsAutomation).ToCanonicalJson()),
            "approval requirements");
        EnsureCanonicalSetMatch(
            input.CapabilityRequirements.Select(item => item.ToCanonicalJson()),
            template.CapabilityRequirements.Select(item => new WorkflowCompiledCapabilityRequirement(
                item.CapabilityRef,
                item.CapabilityKind,
                item.RequiredScopes.OrderBy(scope => scope, StringComparer.Ordinal).ToArray(),
                item.DataAccessClass,
                item.EgressAllowed,
                item.PluginCapability,
                item.AiTaskPolicyRef).ToCanonicalJson()),
            "capability requirements");
        EnsureCanonicalSetMatch(
            input.ArtifactDeclarations.Select(item => item.ToCanonicalJson()),
            template.ArtifactDeclarations.Select(item => new WorkflowCompiledArtifactDeclaration(
                item.ArtifactRef,
                item.ArtifactKind,
                item.SchemaId,
                item.SchemaVersion,
                item.ProducedByNodeId,
                item.RequiredForGates.ToArray(),
                item.RetentionClass).ToCanonicalJson()),
            "artifact declarations");
    }

    private static void ValidateInputBindings(
        UnverifiedWorkflowDefinition input,
        WorkflowTemplate template,
        ProtocolVersion protocol,
        IWorkflowAuthorityResolver resolver)
    {
        foreach (var binding in input.ResolvedInputBindings)
        {
            var declaration = template.RequiredInputs.Single(item =>
                string.Equals(item.InputId, binding.InputId, StringComparison.Ordinal));
            if (!string.Equals(binding.SchemaId, declaration.SchemaId, StringComparison.Ordinal) ||
                !string.Equals(binding.SchemaVersion, declaration.SchemaVersion, StringComparison.Ordinal))
            {
                throw Unverified("Resolved Workflow input schema does not match its template declaration.");
            }

            switch (binding.SourceType)
            {
                case "protocol-decision":
                    var decision = protocol.Decisions.SingleOrDefault(item =>
                        string.Equals(item.DecisionId, binding.SourceRef, StringComparison.Ordinal));
                    if (decision is null ||
                        !string.Equals(decision.DecisionKey, declaration.SourceProtocolDecisionKey, StringComparison.Ordinal) ||
                        binding.SourceDigest != ContentDigest.Sha256CanonicalJson(decision.ToCanonicalJson()) ||
                        binding.ValueDigest != ContentDigest.Sha256CanonicalJson(decision.Value))
                    {
                        throw Unverified("Resolved Workflow decision binding does not match the complete Protocol decision record.");
                    }
                    break;
                case "template-default":
                    if (declaration.DefaultValue is null ||
                        binding.SourceDigest != ContentDigest.Sha256CanonicalJson(declaration.DefaultValue) ||
                        binding.ValueDigest != binding.SourceDigest)
                    {
                        throw Unverified("Resolved Workflow default binding does not match the authoritative template default.");
                    }
                    break;
                case "compile-parameter":
                    var value = resolver.ResolveCompileParameter(binding.InputId, binding.ValueDigest)
                        ?? throw Unverified("Workflow compile parameter content could not be resolved.");
                    var digest = ContentDigest.Sha256CanonicalJson(value);
                    if (digest != binding.ValueDigest || binding.SourceDigest != binding.ValueDigest)
                    {
                        throw Unverified("Resolved Workflow compile parameter digest does not reproduce.");
                    }
                    break;
                default:
                    throw Unverified("Workflow input source authority is unavailable under ADR 0018.");
            }
        }

        var requiredIds = template.RequiredInputs.Where(item => item.Required).Select(item => item.InputId);
        if (!requiredIds.All(id => input.ResolvedInputBindings.Any(binding => binding.InputId == id)))
        {
            throw Unverified("A required Workflow input binding is missing.");
        }
    }

    private static void EnsureCanonicalSetMatch(
        IEnumerable<CanonicalJsonValue> actual,
        IEnumerable<CanonicalJsonValue> expected,
        string description)
    {
        var actualValues = actual.Select(value => CanonicalJsonSerializer.Serialize(value)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var expectedValues = expected.Select(value => CanonicalJsonSerializer.Serialize(value)).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!actualValues.SequenceEqual(expectedValues, StringComparer.Ordinal))
        {
            throw Unverified($"Workflow {description} do not match the resolved template.");
        }
    }

    private static void EnsureCanonicalMatch(CanonicalJsonValue actual, CanonicalJsonValue expected, string description)
    {
        if (!string.Equals(
                CanonicalJsonSerializer.Serialize(actual),
                CanonicalJsonSerializer.Serialize(expected),
                StringComparison.Ordinal))
        {
            throw Unverified($"Workflow {description} does not match the resolved template.");
        }
    }

    private static void EnsureUnique<T>(IReadOnlyList<T>? items, Func<T, string> identity, string description)
    {
        if (items is null)
        {
            throw Unverified($"Workflow {description} are required.");
        }

        var identities = items.Select(identity).Select(value => Guard.NotBlank(value, description)).ToArray();
        if (identities.Distinct(StringComparer.Ordinal).Count() != identities.Length)
        {
            throw Unverified($"Workflow {description} must have unique identities.");
        }
    }

    private static bool SetEquals(IEnumerable<string> left, IEnumerable<string> right) =>
        left.ToHashSet(StringComparer.Ordinal).SetEquals(right);

    private static ContentDigest RequireDigest(ContentDigest digest, string name) => digest.IsValid
        ? digest
        : throw Unverified($"{name} must be a valid digest.");

    private static WorkflowRuleException Unverified(string message) =>
        new(WorkflowErrorCodes.UnverifiedAuthority, message);
}
