using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NexusScholar.Kernel;
using NexusScholar.Protocol;

namespace NexusScholar.Workflow;

public enum WorkflowNodeKind
{
    HumanTask,
    Approval,
    AutomatedTask,
    Milestone,
    Review
}

public enum WorkflowNodeMode
{
    Human,
    Automated,
    Hybrid
}

public enum WorkflowTemplateInputKind
{
    ScientificConduct,
    ExecutionParameter
}

public enum WorkflowResolvedInputSourceType
{
    ProtocolDecision,
    ProtocolValue,
    ProtocolWaiver,
    ProtocolAmendment,
    CompileParameter,
    TemplateDefault
}

public static class WorkflowErrorCodes
{
    public const string InvalidProtocolStatus = "invalid-protocol-status";
    public const string StaleProtocolDigest = "stale-protocol-digest";
    public const string StaleTemplateDigest = "stale-template-digest";
    public const string MissingRequiredInput = "missing-required-input";
    public const string ConductInputFromCompileParameter = "conduct-input-from-compile-parameter";
    public const string DuplicateNodeId = "duplicate-node-id";
    public const string UnknownEdgeEndpoint = "unknown-edge-endpoint";
    public const string UnknownNodeRequirement = "unknown-node-requirement";
    public const string SelfEdge = "self-edge";
    public const string DependencyCycle = "dependency-cycle";
    public const string MissingSchemaId = "missing-schema-id";
    public const string UnknownSchemaId = "unknown-schema-id";
    public const string MissingSchemaVersion = "missing-schema-version";
    public const string UndeclaredProducedArtifact = "undeclared-produced-artifact";
    public const string UnknownProducingNode = "unknown-producing-node";
    public const string UnknownCapabilityReference = "unknown-capability-reference";
    public const string UnknownApprovalRole = "unknown-approval-role";
    public const string InvalidApprovalRequirement = "invalid-approval-requirement";
    public const string UnknownGatePolicy = "unknown-gate-policy";
    public const string UnknownGateArtifactReference = "unknown-gate-artifact-ref";
    public const string UnknownGateDecisionReference = "unknown-gate-decision-ref";
    public const string AutomationApprovalAuthority = "automation-approval-authority";
    public const string InvalidHybridNode = "invalid-hybrid-node";
    public const string InvalidWaiver = "invalid-waiver";
    public const string WaivableNodeWithoutWaiverPolicy = "waivable-node-without-waiver-policy";
    public const string MissingWaiverDisclosureMapping = "missing-waiver-disclosure-mapping";
    public const string MissingWaiverConsequenceWarning = "missing-waiver-consequence-warning";
    public const string ExpiredWaiver = "expired-waiver";
    public const string WaiverAffectedRequirementMismatch = "waiver-affected-requirement-mismatch";
    public const string WaiverMissingApprovalBinding = "waiver-missing-approval-binding";
    public const string UnauthorizedWaiver = "unauthorized-waiver";
    public const string MissingInvalidationSource = "missing-invalidation-source";
    public const string StaleInvalidationNotice = "stale-invalidation-notice";
    public const string AffectedArtifactMismatch = "affected-artifact-mismatch";
    public const string AffectedNodeNotFound = "affected-node-not-found";
    public const string WorkflowIdMismatch = "workflow-id-mismatch";
    public const string UnknownCompileParameter = "unknown-compile-parameter";
    public const string ExplicitCompileInputRequired = "explicit-compile-input-required";
}

public sealed class WorkflowRuleException : DomainRuleException
{
    public WorkflowRuleException(string category, string message)
        : base(message)
    {
        Category = Guard.NotBlank(category, nameof(category));
    }

    public string Category { get; }
}

public sealed record WorkflowSchemaRef(string SchemaId, string Version);

public sealed record WorkflowTemplateInput(
    string InputId,
    WorkflowTemplateInputKind InputKind,
    string SchemaId,
    string SchemaVersion,
    bool Required,
    string? SourceProtocolDecisionKey,
    CanonicalJsonValue? DefaultValue = null);

public sealed record WorkflowTemplateNode(
    string NodeId,
    WorkflowNodeKind Kind,
    WorkflowNodeMode Mode,
    string? Label,
    IReadOnlyList<string> Requires,
    IReadOnlyList<string> Produces,
    string? ApprovalRequirementRef,
    IReadOnlyList<string> CapabilityRequirementRefs,
    string? WaiverPolicyRef,
    string? InvalidationPolicyRef,
    string? Condition = null);

public sealed record WorkflowTemplateEdge(string FromNodeId, string ToNodeId, string? Condition = null);

public sealed record WorkflowTemplateRole(string RoleId, string Label, string AuthorityDescription, string? MethodPackRef = null);

public sealed record WorkflowTemplateApprovalRequirement(
    string ApprovalRequirementId,
    string PolicyId,
    string PolicyVersion,
    string PolicyMode,
    IReadOnlyList<string> RequiredRoles,
    int MinimumApprovals,
    bool RequiresDistinctActors,
    bool AllowsAutomation);

public sealed record WorkflowTemplateGate(
    string GateId,
    string TargetNodeId,
    string PolicyRef,
    IReadOnlyList<string> RequiredArtifactRefs,
    IReadOnlyList<string> RequiredDecisionRefs,
    IReadOnlyList<string> RequiredActorRoles);

public sealed record WorkflowTemplateArtifactDeclaration(
    string ArtifactRef,
    string ArtifactKind,
    string SchemaId,
    string SchemaVersion,
    string ProducedByNodeId,
    IReadOnlyList<string> RequiredForGates,
    string? RetentionClass = null);

public sealed record WorkflowTemplateCapabilityRequirement(
    string CapabilityRef,
    string CapabilityKind,
    IReadOnlyList<string> RequiredScopes,
    string DataAccessClass,
    bool EgressAllowed,
    string? PluginCapability = null,
    string? AiTaskPolicyRef = null);

public sealed record WorkflowTemplateWaiverPolicy(
    string WaiverPolicyId,
    IReadOnlyList<string> WaivableRequirementRefs,
    string ApprovalRequirementRef,
    string DisclosureMapping,
    string ConsequenceWarning);

public sealed record WorkflowTemplateInvalidationPolicy(
    string InvalidationPolicyId,
    IReadOnlyList<string> AffectedRequirementRefs,
    IReadOnlyList<string> AffectedArtifactRefs,
    IReadOnlyList<string> AffectedNodeRefs,
    string RequiredAction);

public sealed record WorkflowTemplate(
    string TemplateId,
    string TemplateVersion,
    ContentDigest TemplateDigest,
    string SchemaId,
    string SchemaVersion,
    IReadOnlyList<WorkflowTemplateInput> RequiredInputs,
    IReadOnlyList<WorkflowTemplateNode> Nodes,
    IReadOnlyList<WorkflowTemplateEdge> Edges,
    IReadOnlyList<WorkflowTemplateGate> Gates,
    IReadOnlyList<WorkflowTemplateApprovalRequirement> ApprovalRequirements,
    IReadOnlyList<WorkflowTemplateRole> Roles,
    IReadOnlyList<WorkflowTemplateCapabilityRequirement> CapabilityRequirements,
    IReadOnlyList<WorkflowTemplateWaiverPolicy> WaiverPolicies,
    IReadOnlyList<WorkflowTemplateArtifactDeclaration> ArtifactDeclarations,
    IReadOnlyList<WorkflowTemplateInvalidationPolicy> InvalidationPolicies);

public sealed record WorkflowCompileInput(
    ProtocolVersion ProtocolVersion,
    WorkflowTemplate Template,
    IReadOnlyDictionary<string, CanonicalJsonValue> CompileParameters,
    IReadOnlyList<WorkflowSchemaRef> KnownSchemaRefs,
    IReadOnlyList<ProtocolAmendment>? Amendments = null,
    IReadOnlyList<ProtocolInvalidationNotice>? InvalidationNotices = null,
    string? ExpectedWorkflowId = null,
    string CompilerId = "nexus-workflow-compiler",
    string CompilerVersion = "1.0.0");

public sealed record WorkflowResolvedInputBinding(
    string InputId,
    string InputKind,
    string SchemaId,
    string SchemaVersion,
    string SourceType,
    string SourceRef,
    ContentDigest SourceDigest,
    ContentDigest ValueDigest,
    string? WaiverId = null,
    string? AmendmentId = null);

public sealed record WorkflowCompiledNode(
    string NodeId,
    string Label,
    WorkflowNodeKind Kind,
    WorkflowNodeMode Mode,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string> Produces,
    IReadOnlyList<string> Requires,
    string? ApprovalRequirementRef,
    IReadOnlyList<string> CapabilityRequirementRefs,
    string? WaiverPolicyRef,
    string? InvalidationPolicyRef,
    string? Condition = null);

public sealed record WorkflowCompiledEdge(string FromNodeId, string ToNodeId, string? Condition = null);

public sealed record WorkflowCompiledApprovalRequirement(
    string ApprovalRequirementId,
    string PolicyId,
    string PolicyVersion,
    string PolicyMode,
    IReadOnlyList<string> RequiredRoles,
    int MinimumApprovals,
    bool RequiresDistinctActors,
    bool AllowsAutomation);

public sealed record WorkflowCompiledCapabilityRequirement(
    string CapabilityRef,
    string CapabilityKind,
    IReadOnlyList<string> RequiredScopes,
    string DataAccessClass,
    bool EgressAllowed,
    string? PluginCapability,
    string? AiTaskPolicyRef);

public sealed record WorkflowCompiledArtifactDeclaration(
    string ArtifactRef,
    string ArtifactKind,
    string SchemaId,
    string SchemaVersion,
    string ProducedByNodeId,
    IReadOnlyList<string> RequiredForGates,
    string? RetentionClass);

public sealed record WorkflowInvalidationPlanEntry(
    string NoticeId,
    string AmendmentId,
    string ProducesVersionId,
    ContentDigest PreviousContentDigest,
    ContentDigest AmendmentSourceDigest,
    ContentDigest InvalidationNoticeDigest,
    string AffectedRequirementId,
    ContentDigest AffectedArtifactDigest,
    string AffectedNodeId,
    string RequiredAction);

public sealed record WorkflowDefinition(
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
    IReadOnlyList<WorkflowInvalidationPlanEntry> InvalidationPlanEntries)
{
    public string Id => WorkflowId;
}

public sealed class WorkflowCompiler
{
    private const string WorkflowSchemaId = "nexus.workflow-definition";
    private const string WorkflowSchemaVersion = "1.0.0";
    private const string TemplateSchemaId = "nexus.workflow-template";
    private const string TemplateSchemaVersion = "1.0.0";

    public static ContentDigest ComputeTemplateDigestForTesting(WorkflowTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        return ComputeTemplateDigest(template);
    }

    public WorkflowDefinition Compile(ProtocolVersion protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        throw new WorkflowRuleException(
            WorkflowErrorCodes.ExplicitCompileInputRequired,
            "Workflow compilation requires explicit WorkflowCompileInput with a schema-closed template.");
    }

    public WorkflowDefinition Compile(WorkflowCompileInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.ProtocolVersion.Status != ProtocolStatus.Approved)
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.InvalidProtocolStatus,
                "Workflow compilation requires an approved protocol version.");
        }

        ValidateAmendmentSourcePresence(input);
        ValidateProtocolDigest(input);
        ValidateTemplateSchemaClosure(input);
        ValidateTemplateDigest(input);

        var template = input.Template;
        var protocol = input.ProtocolVersion;
        var nodes = template.Nodes.Select(normalizeNode).ToArray();

        var nodeIds = new HashSet<string>(nodes.Select(node => node.NodeId), StringComparer.Ordinal);
        if (nodeIds.Count != nodes.Length)
        {
            var duplicate = nodes.GroupBy(node => node.NodeId, StringComparer.Ordinal)
                .First(group => group.Count() > 1);
            throw new WorkflowRuleException(
                WorkflowErrorCodes.DuplicateNodeId,
                $"Duplicate template node id '{duplicate.Key}'.");
        }

        ValidateArtifacts(template, nodeIds);
        ValidateEdges(template.Edges, nodeIds);
        var topoSorted = TopologicalSort(nodes, template.Edges, out _);

        ValidateRoles(template);
        ValidateCapabilityRefs(template);
        ValidateGates(template, nodeIds);
        ValidateNodeRequirements(template);
        ValidateProtocolWaivers(protocol, template);
        var approvalRequirements = ResolveApprovalRequirements(template);
        var capabilityRequirements = ResolveCapabilityRequirements(template);

        var boundInputs = ResolveInputs(protocol, template, input.CompileParameters);

        var workflowId = ComputeWorkflowId(
            protocol,
            template,
            input.CompilerId,
            input.CompilerVersion);

        if (!string.IsNullOrWhiteSpace(input.ExpectedWorkflowId) && input.ExpectedWorkflowId != workflowId)
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.WorkflowIdMismatch,
                "Computed workflow id does not match supplied workflow id.");
        }

        var compiledEdges = template.Edges
            .OrderBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ThenBy(edge => edge.ToNodeId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Condition, StringComparer.Ordinal)
            .Select(edge => new WorkflowCompiledEdge(edge.FromNodeId, edge.ToNodeId, edge.Condition))
            .ToArray();

        var compiledNodes = topoSorted
            .Select(nodeId => nodes.First(node => node.NodeId == nodeId))
            .Select(node => new WorkflowCompiledNode(
                node.NodeId,
                node.Label ?? string.Empty,
                node.Kind,
                node.Mode,
                template.Edges.Where(edge => edge.ToNodeId == node.NodeId).Select(edge => edge.FromNodeId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                node.Produces.ToArray(),
                node.Requires.ToArray(),
                node.ApprovalRequirementRef,
                node.CapabilityRequirementRefs.ToArray(),
                node.WaiverPolicyRef,
                node.InvalidationPolicyRef,
                node.Condition))
            .ToArray();

        var invalidationPlan = BuildInvalidationPlan(protocol, template, input);

        var workflowDigest = ComputeWorkflowDigest(
            workflowId,
            input.CompilerId,
            input.CompilerVersion,
            protocol,
            template,
            boundInputs,
            compiledNodes,
            compiledEdges,
            approvalRequirements,
            capabilityRequirements,
            artifactDeclarations: template.ArtifactDeclarations
                .Select(MapArtifactDeclaration)
                .OrderBy(artifact => artifact.ArtifactRef, StringComparer.Ordinal)
                .ToArray(),
            invalidationPlan);

        return new WorkflowDefinition(
            workflowId,
            workflowDigest,
            Guard.NotBlank(input.CompilerId, nameof(input.CompilerId)),
            Guard.NotBlank(input.CompilerVersion, nameof(input.CompilerVersion)),
            protocol.ProtocolId,
            protocol.Id,
            protocol.VersionNumber,
            protocol.ContentDigest,
            template.TemplateId,
            template.TemplateVersion,
            template.TemplateDigest,
            boundInputs.ToArray(),
            compiledNodes,
            compiledEdges,
            approvalRequirements.ToArray(),
            capabilityRequirements.ToArray(),
            template.ArtifactDeclarations
                .Select(MapArtifactDeclaration)
                .OrderBy(artifact => artifact.ArtifactRef, StringComparer.Ordinal)
                .ToArray(),
            invalidationPlan.ToArray());
    }

    private static WorkflowTemplateNode normalizeNode(WorkflowTemplateNode node)
    {
        return new WorkflowTemplateNode(
            Guard.NotBlank(node.NodeId, nameof(node.NodeId)),
            node.Kind,
            node.Mode,
            node.Label,
            (node.Requires ?? Array.Empty<string>()).Select(id => Guard.NotBlank(id, nameof(node.Requires))).ToArray(),
            (node.Produces ?? Array.Empty<string>()).Select(id => Guard.NotBlank(id, nameof(node.Produces))).ToArray(),
            node.ApprovalRequirementRef,
            (node.CapabilityRequirementRefs ?? Array.Empty<string>())
                .Select(id => Guard.NotBlank(id, nameof(node.CapabilityRequirementRefs))).ToArray(),
            node.WaiverPolicyRef,
            node.InvalidationPolicyRef,
            node.Condition);
    }

    private static void ValidateProtocolDigest(WorkflowCompileInput input)
    {
        var expected = input.ProtocolVersion.ToProtocolContentDigestEnvelope().ComputeDigest();
        if (!string.Equals(input.ProtocolVersion.ContentDigest.Value, expected.Value, StringComparison.Ordinal))
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.StaleProtocolDigest, "Protocol content digest is stale.");
        }
    }

    private static void ValidateAmendmentSourcePresence(WorkflowCompileInput input)
    {
        var amendmentId = input.ProtocolVersion.AmendmentId;
        if (string.IsNullOrWhiteSpace(amendmentId))
        {
            return;
        }

        if (input.Amendments is null ||
            !input.Amendments.Any(amendment => string.Equals(amendment.AmendmentId, amendmentId, StringComparison.Ordinal)))
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.MissingInvalidationSource,
                "Amendment source is required for amended protocol workflow compilation.");
        }
    }

    private static void ValidateTemplateDigest(WorkflowCompileInput input)
    {
        var computedTemplateDigest = ComputeTemplateDigest(input.Template);
        if (!string.Equals(input.Template.TemplateDigest.Value, computedTemplateDigest.Value, StringComparison.Ordinal))
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.StaleTemplateDigest,
                "Template digest does not match compiled template material.");
        }
    }

    private static void ValidateTemplateSchemaClosure(WorkflowCompileInput input)
    {
        var template = input.Template;
        var known = new HashSet<(string SchemaId, string SchemaVersion)>(
            input.KnownSchemaRefs.Select(refs => (refs.SchemaId, refs.Version)),
            new SchemaRefComparer());

        ValidateSchemaRef(TemplateSchemaId, TemplateSchemaVersion, known);
        ValidateSchemaRef(template.SchemaId, template.SchemaVersion, known);

        foreach (var inputDefinition in template.RequiredInputs)
        {
            ValidateSchemaRef(inputDefinition.SchemaId, inputDefinition.SchemaVersion, known);
        }

        foreach (var declaration in template.ArtifactDeclarations)
        {
            ValidateSchemaRef(declaration.SchemaId, declaration.SchemaVersion, known);
        }
    }

    private static void ValidateSchemaRef(string schemaId, string schemaVersion, HashSet<(string SchemaId, string SchemaVersion)> known)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.MissingSchemaId, "Schema id is required.");
        }

        if (string.IsNullOrWhiteSpace(schemaVersion))
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.MissingSchemaVersion, "Schema version is required.");
        }

        if (!known.Contains((schemaId, schemaVersion)))
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.UnknownSchemaId, $"Unknown schema id '{schemaId}:{schemaVersion}'.");
        }
    }

    private static void ValidateEdges(IEnumerable<WorkflowTemplateEdge> edges, IReadOnlySet<string> nodeIds)
    {
        foreach (var edge in edges)
        {
            var from = Guard.NotBlank(edge.FromNodeId, nameof(edge.FromNodeId));
            var to = Guard.NotBlank(edge.ToNodeId, nameof(edge.ToNodeId));

            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                throw new WorkflowRuleException(WorkflowErrorCodes.SelfEdge, "Self-edge is not allowed.");
            }

            if (!nodeIds.Contains(from) || !nodeIds.Contains(to))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.UnknownEdgeEndpoint,
                    "Edge endpoint does not reference an existing node.");
            }
        }
    }

    private static IReadOnlyList<string> TopologicalSort(
        IReadOnlyList<WorkflowTemplateNode> nodes,
        IReadOnlyList<WorkflowTemplateEdge> edges,
        out Dictionary<string, int> indegreeSnapshot)
    {
        var indegree = nodes.ToDictionary(node => node.NodeId, _ => 0, StringComparer.Ordinal);
        var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            outgoing[node.NodeId] = new List<string>();
        }

        foreach (var edge in edges)
        {
            if (!indegree.ContainsKey(edge.FromNodeId) || !indegree.ContainsKey(edge.ToNodeId))
            {
                continue;
            }

            outgoing[edge.FromNodeId].Add(edge.ToNodeId);
            indegree[edge.ToNodeId]++;
        }

        var order = new List<string>();
        var queue = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var item in indegree.Where(item => item.Value == 0))
        {
            queue.Add(item.Key);
        }

        while (queue.Count > 0)
        {
            var current = queue.Min!;
            queue.Remove(current);
            order.Add(current);

            foreach (var next in outgoing[current].OrderBy(node => node, StringComparer.Ordinal))
            {
                indegree[next]--;
                if (indegree[next] == 0)
                {
                    queue.Add(next);
                }
            }
        }

        indegreeSnapshot = indegree.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        if (order.Count != nodes.Count)
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.DependencyCycle, "Dependency cycle detected.");
        }

        return order;
    }

    private static void ValidateArtifacts(WorkflowTemplate template, IReadOnlySet<string> nodeIds)
    {
        var declaredArtifacts = new HashSet<string>(template.ArtifactDeclarations.Select(artifact => artifact.ArtifactRef), StringComparer.Ordinal);
        foreach (var declaration in template.ArtifactDeclarations)
        {
            if (!nodeIds.Contains(declaration.ProducedByNodeId))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.UnknownProducingNode,
                    $"Artifact declaration '{declaration.ArtifactRef}' has unknown producing node.");
            }
        }

        foreach (var node in template.Nodes)
        {
            foreach (var produced in node.Produces)
            {
                if (!declaredArtifacts.Contains(produced))
                {
                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.UndeclaredProducedArtifact,
                        $"Node '{node.NodeId}' references undeclared artifact '{produced}'.");
                }
            }
        }
    }

    private static void ValidateRoles(WorkflowTemplate template)
    {
        var roleIds = new HashSet<string>(template.Roles.Select(role => role.RoleId), StringComparer.Ordinal);

        foreach (var approvalRequirement in template.ApprovalRequirements)
        {
            if (approvalRequirement.RequiredRoles.Count == 0)
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.InvalidApprovalRequirement,
                    $"Approval requirement '{approvalRequirement.ApprovalRequirementId}' must require at least one human role.");
            }

            if (approvalRequirement.MinimumApprovals <= 0)
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.InvalidApprovalRequirement,
                    $"Approval requirement '{approvalRequirement.ApprovalRequirementId}' must require at least one approval.");
            }

            if (approvalRequirement.RequiresDistinctActors &&
                approvalRequirement.MinimumApprovals > approvalRequirement.RequiredRoles.Count)
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.InvalidApprovalRequirement,
                    $"Approval requirement '{approvalRequirement.ApprovalRequirementId}' cannot require more distinct approvals than declared roles.");
            }

            if (approvalRequirement.AllowsAutomation)
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.AutomationApprovalAuthority,
                    $"Approval requirement '{approvalRequirement.ApprovalRequirementId}' allows automation.");
            }

            foreach (var role in approvalRequirement.RequiredRoles)
            {
                if (!roleIds.Contains(role))
                {
                    throw new WorkflowRuleException(WorkflowErrorCodes.UnknownApprovalRole, $"Unknown approval role '{role}'.");
                }
            }
        }

        foreach (var gate in template.Gates)
        {
            foreach (var role in gate.RequiredActorRoles)
            {
                if (!roleIds.Contains(role))
                {
                    throw new WorkflowRuleException(WorkflowErrorCodes.UnknownApprovalRole, $"Unknown gate actor role '{role}'.");
                }
            }
        }
    }

    private static void ValidateCapabilityRefs(WorkflowTemplate template)
    {
        var knownCapabilities = new HashSet<string>(
            template.CapabilityRequirements.Select(capability => capability.CapabilityRef),
            StringComparer.Ordinal);

        foreach (var node in template.Nodes)
        {
            foreach (var capability in node.CapabilityRequirementRefs)
            {
                if (!knownCapabilities.Contains(capability))
                {
                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.UnknownCapabilityReference,
                        $"Unknown capability reference '{capability}' on node '{node.NodeId}'.");
                }
            }
        }
    }

    private static void ValidateGates(WorkflowTemplate template, IReadOnlySet<string> nodeIds)
    {
        var approvalRequirementIds = template.ApprovalRequirements
            .Select(requirement => requirement.ApprovalRequirementId)
            .ToHashSet(StringComparer.Ordinal);
        var artifactRefs = template.ArtifactDeclarations
            .Select(declaration => declaration.ArtifactRef)
            .ToHashSet(StringComparer.Ordinal);
        var decisionRefs = template.RequiredInputs
            .SelectMany(input => string.IsNullOrWhiteSpace(input.SourceProtocolDecisionKey)
                ? new[] { input.InputId }
                : new[] { input.InputId, input.SourceProtocolDecisionKey! })
            .ToHashSet(StringComparer.Ordinal);

        foreach (var gate in template.Gates)
        {
            if (!nodeIds.Contains(gate.TargetNodeId))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.UnknownNodeRequirement,
                    $"Gate '{gate.GateId}' targets missing node '{gate.TargetNodeId}'.");
            }

            if (!approvalRequirementIds.Contains(gate.PolicyRef))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.UnknownGatePolicy,
                    $"Gate '{gate.GateId}' references unknown gate policy '{gate.PolicyRef}'.");
            }

            foreach (var artifactRef in gate.RequiredArtifactRefs)
            {
                if (!artifactRefs.Contains(artifactRef))
                {
                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.UnknownGateArtifactReference,
                        $"Gate '{gate.GateId}' references unknown required artifact '{artifactRef}'.");
                }
            }

            foreach (var decisionRef in gate.RequiredDecisionRefs)
            {
                if (!decisionRefs.Contains(decisionRef))
                {
                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.UnknownGateDecisionReference,
                        $"Gate '{gate.GateId}' references unknown required decision '{decisionRef}'.");
                }
            }
        }
    }

    private static void ValidateNodeRequirements(WorkflowTemplate template)
    {
        var validRequirements = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in template.RequiredInputs)
        {
            validRequirements.Add(input.InputId);
        }

        foreach (var requirement in template.ApprovalRequirements.Select(req => req.ApprovalRequirementId))
        {
            validRequirements.Add(requirement);
        }

        foreach (var policy in template.WaiverPolicies)
        {
            foreach (var requirement in policy.WaivableRequirementRefs)
            {
                validRequirements.Add(requirement);
            }
        }

        foreach (var policy in template.InvalidationPolicies)
        {
            foreach (var requirement in policy.AffectedRequirementRefs)
            {
                validRequirements.Add(requirement);
            }
        }

        foreach (var node in template.Nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ApprovalRequirementRef) &&
                !template.ApprovalRequirements.Any(requirement =>
                    string.Equals(requirement.ApprovalRequirementId, node.ApprovalRequirementRef, StringComparison.Ordinal)))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.UnknownNodeRequirement,
                    $"Node '{node.NodeId}' references unknown approval requirement '{node.ApprovalRequirementRef}'.");
            }

            if (!string.IsNullOrWhiteSpace(node.WaiverPolicyRef) &&
                !template.WaiverPolicies.Any(policy =>
                    string.Equals(policy.WaiverPolicyId, node.WaiverPolicyRef, StringComparison.Ordinal)))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.WaivableNodeWithoutWaiverPolicy,
                    $"Node '{node.NodeId}' references unknown waiver policy '{node.WaiverPolicyRef}'.");
            }

            if (!string.IsNullOrWhiteSpace(node.InvalidationPolicyRef) &&
                !template.InvalidationPolicies.Any(policy =>
                    string.Equals(policy.InvalidationPolicyId, node.InvalidationPolicyRef, StringComparison.Ordinal)))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.MissingInvalidationSource,
                    $"Node '{node.NodeId}' references unknown invalidation policy '{node.InvalidationPolicyRef}'.");
            }

            foreach (var requirementRef in node.Requires)
            {
                if (!validRequirements.Contains(requirementRef))
                {
                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.UnknownNodeRequirement,
                        $"Node '{node.NodeId}' references unknown requirement '{requirementRef}'.");
                }
            }
        }

        foreach (var node in template.Nodes)
        {
            if (node.Mode == WorkflowNodeMode.Hybrid)
            {
                if (node.CapabilityRequirementRefs.Count == 0)
                {
                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.InvalidHybridNode,
                        $"Hybrid node '{node.NodeId}' requires at least one capability requirement.");
                }

                var approvalRequirementIds = template.ApprovalRequirements
                    .Select(requirement => requirement.ApprovalRequirementId)
                    .ToHashSet(StringComparer.Ordinal);
                var humanPath = !string.IsNullOrWhiteSpace(node.ApprovalRequirementRef) ||
                    template.Gates.Any(gate =>
                        string.Equals(gate.TargetNodeId, node.NodeId, StringComparison.Ordinal) &&
                        approvalRequirementIds.Contains(gate.PolicyRef) &&
                        gate.RequiredActorRoles.Count > 0);

                if (!humanPath)
                {
                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.InvalidHybridNode,
                        $"Hybrid node '{node.NodeId}' requires explicit human approval or review.");
                }
            }
        }

        foreach (var waiverPolicy in template.WaiverPolicies)
        {
            if (string.IsNullOrWhiteSpace(waiverPolicy.DisclosureMapping))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.MissingWaiverDisclosureMapping,
                    $"Waiver policy '{waiverPolicy.WaiverPolicyId}' is missing disclosure mapping.");
            }

            if (string.IsNullOrWhiteSpace(waiverPolicy.ConsequenceWarning))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.MissingWaiverConsequenceWarning,
                    $"Waiver policy '{waiverPolicy.WaiverPolicyId}' is missing consequence warning.");
            }
        }
    }

    private static IReadOnlyList<WorkflowResolvedInputBinding> ResolveInputs(
        ProtocolVersion protocol,
        WorkflowTemplate template,
        IReadOnlyDictionary<string, CanonicalJsonValue> compileParameters)
    {
        compileParameters ??= new Dictionary<string, CanonicalJsonValue>(StringComparer.Ordinal);

        var declaredInputs = template.RequiredInputs
            .Select(input => input.InputId)
            .ToHashSet(StringComparer.Ordinal);
        var unknownCompileParameter = compileParameters.Keys
            .FirstOrDefault(key => !declaredInputs.Contains(key));
        if (unknownCompileParameter is not null)
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.UnknownCompileParameter,
                $"Compile parameter '{unknownCompileParameter}' is not declared by the workflow template.");
        }

        var byDecision = protocol.Decisions
            .GroupBy(decision => decision.DecisionKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal);

        var waivers = protocol.Waivers
            .GroupBy(waiver => waiver.AffectedRequirementId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var inputsToResolve = template.RequiredInputs
            .Where(input => input.Required || compileParameters.ContainsKey(input.InputId))
            .OrderBy(input => input.InputId, StringComparer.Ordinal);

        var bindings = new List<WorkflowResolvedInputBinding>();
        foreach (var input in inputsToResolve)
        {
            switch (input.InputKind)
            {
                case WorkflowTemplateInputKind.ScientificConduct:
                    if (compileParameters.TryGetValue(input.InputId, out var compileValue))
                    {
                        throw new WorkflowRuleException(
                            WorkflowErrorCodes.ConductInputFromCompileParameter,
                            $"Scientific conduct input '{input.InputId}' cannot be supplied by compile parameter.");
                    }

                    if (!string.IsNullOrWhiteSpace(input.SourceProtocolDecisionKey) &&
                        byDecision.TryGetValue(input.SourceProtocolDecisionKey, out var decisionValue))
                    {
                        bindings.Add(
                            new WorkflowResolvedInputBinding(
                                input.InputId,
                                "scientific_conduct",
                                input.SchemaId,
                                input.SchemaVersion,
                                WorkflowResolvedInputSourceType.ProtocolDecision.ToWireValue(),
                                input.SourceProtocolDecisionKey!,
                                ContentDigest.Sha256CanonicalJson(CanonicalJsonValue.From(input.SourceProtocolDecisionKey)),
                                ContentDigest.Sha256CanonicalJson(decisionValue),
                                null,
                                null));
                        break;
                    }

                    if (waivers.TryGetValue(input.InputId, out var waiver))
                    {
                        ValidateProtocolWaiver(protocol, input, waiver);
                        var waiverDigest = ContentDigest.Sha256CanonicalJson(waiver.ToCanonicalJson());
                        bindings.Add(
                            new WorkflowResolvedInputBinding(
                                input.InputId,
                                "scientific_conduct",
                                input.SchemaId,
                                input.SchemaVersion,
                                WorkflowResolvedInputSourceType.ProtocolWaiver.ToWireValue(),
                                waiver.WaiverId,
                                waiverDigest,
                                waiverDigest,
                                waiver.WaiverId,
                                null));
                        break;
                    }

                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.MissingRequiredInput,
                        $"Missing required scientific conduct input '{input.InputId}'.");

                case WorkflowTemplateInputKind.ExecutionParameter:
                    if (compileParameters.TryGetValue(input.InputId, out var parameterValue))
                    {
                        var digest = ContentDigest.Sha256CanonicalJson(parameterValue);
                        bindings.Add(
                            new WorkflowResolvedInputBinding(
                                input.InputId,
                                "execution_parameter",
                                input.SchemaId,
                                input.SchemaVersion,
                                WorkflowResolvedInputSourceType.CompileParameter.ToWireValue(),
                                input.InputId,
                                digest,
                                digest,
                                null,
                                null));
                        break;
                    }

                    if (input.DefaultValue is not null)
                    {
                        var defaultDigest = ContentDigest.Sha256CanonicalJson(input.DefaultValue);
                        bindings.Add(
                            new WorkflowResolvedInputBinding(
                                input.InputId,
                                "execution_parameter",
                                input.SchemaId,
                                input.SchemaVersion,
                                WorkflowResolvedInputSourceType.TemplateDefault.ToWireValue(),
                                input.InputId,
                                defaultDigest,
                                defaultDigest,
                                null,
                                null));
                        break;
                    }

                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.MissingRequiredInput,
                        $"Missing required execution parameter '{input.InputId}'.");
                default:
                    throw new InvalidOperationException($"Unsupported workflow input kind '{input.InputKind}'.");
            }
        }

        return bindings;
    }

    private static void ValidateProtocolWaivers(ProtocolVersion protocol, WorkflowTemplate template)
    {
        foreach (var waiver in protocol.Waivers)
        {
            var waiverPolicy = template.WaiverPolicies.FirstOrDefault(policy =>
                policy.WaivableRequirementRefs.Contains(waiver.AffectedRequirementId, StringComparer.Ordinal));

            if (waiverPolicy is null)
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.WaivableNodeWithoutWaiverPolicy,
                    $"Protocol waiver '{waiver.WaiverId}' affects a requirement without a template waiver policy.");
            }

            if (string.IsNullOrWhiteSpace(waiverPolicy.DisclosureMapping) ||
                string.IsNullOrWhiteSpace(waiver.DisclosureMapping))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.MissingWaiverDisclosureMapping,
                    $"Protocol waiver '{waiver.WaiverId}' is missing disclosure mapping.");
            }

            if (string.IsNullOrWhiteSpace(waiverPolicy.ConsequenceWarning) ||
                string.IsNullOrWhiteSpace(waiver.ConsequenceWarning))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.MissingWaiverConsequenceWarning,
                    $"Protocol waiver '{waiver.WaiverId}' is missing consequence warning.");
            }

            if (waiver.ExpiresAt.HasValue && protocol.ApprovedAt.HasValue && waiver.ExpiresAt.Value <= protocol.ApprovedAt.Value)
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.ExpiredWaiver,
                    $"Protocol waiver '{waiver.WaiverId}' expired before workflow compilation authority.");
            }

            if (waiver.ApprovalIds.Count == 0)
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.WaiverMissingApprovalBinding,
                    $"Protocol waiver '{waiver.WaiverId}' is missing approval binding.");
            }

            if (!template.ApprovalRequirements.Any(requirement =>
                string.Equals(requirement.ApprovalRequirementId, waiverPolicy.ApprovalRequirementRef, StringComparison.Ordinal)))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.UnauthorizedWaiver,
                    $"Waiver policy '{waiverPolicy.WaiverPolicyId}' references an unknown approval requirement.");
            }
        }
    }

    private static void ValidateProtocolWaiver(ProtocolVersion protocol, WorkflowTemplateInput input, ProtocolWaiver waiver)
    {
        if (waiver.ExpiresAt.HasValue && protocol.ApprovedAt.HasValue && waiver.ExpiresAt.Value <= protocol.ApprovedAt.Value)
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.ExpiredWaiver, "Expired waiver cannot authorize conduct.");
        }

        if (waiver.ApprovalIds.Count == 0)
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.WaiverMissingApprovalBinding, "Waiver authorization binding is missing.");
        }

        if (string.IsNullOrWhiteSpace(input.SourceProtocolDecisionKey))
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.UnauthorizedWaiver, "Waiver must correspond to a protocol decision.");
        }

        if (!string.Equals(waiver.AffectedRequirementId, input.SourceProtocolDecisionKey, StringComparison.Ordinal))
        {
            throw new WorkflowRuleException(WorkflowErrorCodes.WaiverAffectedRequirementMismatch, "Waiver affected requirement mismatch.");
        }
    }

    private static IEnumerable<WorkflowCompiledApprovalRequirement> ResolveApprovalRequirements(WorkflowTemplate template)
    {
        return template.ApprovalRequirements
            .OrderBy(requirement => requirement.ApprovalRequirementId, StringComparer.Ordinal)
            .Select(requirement => new WorkflowCompiledApprovalRequirement(
                requirement.ApprovalRequirementId,
                requirement.PolicyId,
                requirement.PolicyVersion,
                requirement.PolicyMode,
                requirement.RequiredRoles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
                requirement.MinimumApprovals,
                requirement.RequiresDistinctActors,
                requirement.AllowsAutomation))
            .ToArray();
    }

    private static IEnumerable<WorkflowCompiledCapabilityRequirement> ResolveCapabilityRequirements(WorkflowTemplate template)
    {
        return template.CapabilityRequirements
            .OrderBy(requirement => requirement.CapabilityRef, StringComparer.Ordinal)
            .Select(requirement => new WorkflowCompiledCapabilityRequirement(
                requirement.CapabilityRef,
                requirement.CapabilityKind,
                requirement.RequiredScopes.OrderBy(scope => scope, StringComparer.Ordinal).ToArray(),
                requirement.DataAccessClass,
                requirement.EgressAllowed,
                requirement.PluginCapability,
                requirement.AiTaskPolicyRef))
            .ToArray();
    }

    private static IReadOnlyList<WorkflowInvalidationPlanEntry> BuildInvalidationPlan(
        ProtocolVersion protocol,
        WorkflowTemplate template,
        WorkflowCompileInput input)
    {
        var amendmentId = protocol.AmendmentId;
        if (string.IsNullOrWhiteSpace(amendmentId))
        {
            return Array.Empty<WorkflowInvalidationPlanEntry>();
        }

        var amendment = input.Amendments?.FirstOrDefault(item => string.Equals(item.AmendmentId, amendmentId, StringComparison.Ordinal));
        if (amendment is null)
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.MissingInvalidationSource,
                "Amendment source is required for amended protocol compilation.");
        }

        var notices = amendment.InvalidationNotices.ToList();
        if ((input.InvalidationNotices?.Count ?? 0) > 0)
        {
            notices = input.InvalidationNotices!.ToList();
        }

        if (notices.Count == 0)
        {
            throw new WorkflowRuleException(
                WorkflowErrorCodes.MissingInvalidationSource,
                "No invalidation notices were supplied.");
        }

        var declarations = template.ArtifactDeclarations.ToDictionary(
            declaration => declaration.ArtifactRef,
            declaration => ComputeArtifactDigest(declaration),
            StringComparer.Ordinal);

        var planEntries = new List<WorkflowInvalidationPlanEntry>();
        var policies = template.InvalidationPolicies.ToDictionary(
            policy => policy.InvalidationPolicyId,
            policy => policy,
            StringComparer.Ordinal);

        foreach (var notice in notices)
        {
            if (!string.Equals(notice.SourceAmendmentId, amendmentId, StringComparison.Ordinal))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.StaleInvalidationNotice,
                    "Invalidation notice belongs to a different amendment.");
            }

            _ = Guard.NotBlank(notice.AffectedWorkflowNodeId, nameof(notice.AffectedWorkflowNodeId));

            var policy = policies.Values.FirstOrDefault(item =>
                item.AffectedRequirementRefs.Contains(notice.AffectedRequirementId, StringComparer.Ordinal));

            if (policy is null)
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.MissingInvalidationSource,
                    $"No invalidation policy for affected requirement '{notice.AffectedRequirementId}'.");
            }

            if (!policy.AffectedNodeRefs.Contains(notice.AffectedWorkflowNodeId, StringComparer.Ordinal))
            {
                throw new WorkflowRuleException(
                    WorkflowErrorCodes.AffectedNodeNotFound,
                    $"Invalidation policy does not include affected node '{notice.AffectedWorkflowNodeId}'.");
            }

            var affectedArtifactRefs = policy.AffectedArtifactRefs.ToHashSet(StringComparer.Ordinal);
            if (affectedArtifactRefs.Count > 0)
            {
                var acceptableDigests = affectedArtifactRefs
                    .Where(declarations.ContainsKey)
                    .Select(refId => declarations[refId])
                    .ToHashSet();

                if (!acceptableDigests.Contains(notice.AffectedArtifactDigest))
                {
                    throw new WorkflowRuleException(
                        WorkflowErrorCodes.AffectedArtifactMismatch,
                        "Invalidation artifact digest does not match policy references.");
                }
            }

            planEntries.Add(
                new WorkflowInvalidationPlanEntry(
                    notice.NoticeId,
                    amendment.AmendmentId,
                    amendment.ProducesVersionId,
                    amendment.PreviousContentDigest,
                    ComputeAmendmentSourceDigest(amendment),
                    ContentDigest.Sha256CanonicalJson(notice.ToCanonicalJson()),
                    notice.AffectedRequirementId,
                    notice.AffectedArtifactDigest,
                    notice.AffectedWorkflowNodeId,
                    policy.RequiredAction));
        }

        return planEntries
            .OrderBy(entry => entry.AffectedNodeId, StringComparer.Ordinal)
            .ThenBy(entry => entry.AffectedArtifactDigest.Value, StringComparer.Ordinal)
            .ThenBy(entry => entry.RequiredAction, StringComparer.Ordinal)
            .ToArray();
    }

    private static ContentDigest ComputeTemplateDigest(WorkflowTemplate template)
    {
        return new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            TemplateSchemaId,
            TemplateSchemaVersion,
            BuildTemplateCanonicalJson(template)).ComputeDigest();
    }

    private static WorkflowCompiledArtifactDeclaration MapArtifactDeclaration(WorkflowTemplateArtifactDeclaration artifact)
    {
        return new WorkflowCompiledArtifactDeclaration(
            artifact.ArtifactRef,
            artifact.ArtifactKind,
            artifact.SchemaId,
            artifact.SchemaVersion,
            artifact.ProducedByNodeId,
            artifact.RequiredForGates.ToArray(),
            artifact.RetentionClass);
    }

    private static ContentDigest ComputeArtifactDigest(WorkflowTemplateArtifactDeclaration artifact)
    {
        var canonical = new CanonicalJsonObject()
            .Add("artifact_ref", artifact.ArtifactRef)
            .Add("artifact_kind", artifact.ArtifactKind)
            .Add("schema_id", artifact.SchemaId)
            .Add("schema_version", artifact.SchemaVersion)
            .Add("produced_by_node_id", artifact.ProducedByNodeId);

        return ContentDigest.Sha256CanonicalJson(canonical);
    }

    private static ContentDigest ComputeAmendmentSourceDigest(ProtocolAmendment amendment)
    {
        var canonical = new CanonicalJsonObject()
            .Add("amendment_id", amendment.AmendmentId)
            .Add("protocol_id", amendment.ProtocolId)
            .Add("amends_version_id", amendment.AmendsVersionId)
            .Add("produces_version_id", amendment.ProducesVersionId)
            .Add("previous_content_digest", amendment.PreviousContentDigest.ToString())
            .Add("requested_by", amendment.RequestedBy.ToString())
            .AddTimestamp("requested_at", amendment.RequestedAt)
            .Add("rationale", amendment.Rationale)
            .Add("changed_decision_keys", CanonicalJsonValue.Array(
                amendment.ChangedDecisionKeys
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .Select(CanonicalJsonValue.From)
                    .ToArray()))
            .Add("invalidation_notice_digests", CanonicalJsonValue.Array(
                amendment.InvalidationNotices
                    .OrderBy(notice => notice.NoticeId, StringComparer.Ordinal)
                    .Select(notice => ContentDigest.Sha256CanonicalJson(notice.ToCanonicalJson()).ToString())
                    .Select(CanonicalJsonValue.From)
                    .ToArray()))
            .Add("approval_policy_id", amendment.ApprovalPolicyId)
            .Add("approval_ids", CanonicalJsonValue.Array(
                amendment.ApprovalIds
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .Select(CanonicalJsonValue.From)
                    .ToArray()));

        if (amendment.InvalidationPlanDigest is not null)
        {
            canonical.Add("invalidation_plan_digest", amendment.InvalidationPlanDigest.Value.ToString());
        }

        return ContentDigest.Sha256CanonicalJson(canonical);
    }

    private static ContentDigest ComputeWorkflowDigest(
        string workflowId,
        string compilerId,
        string compilerVersion,
        ProtocolVersion protocol,
        WorkflowTemplate template,
        IEnumerable<WorkflowResolvedInputBinding> inputs,
        IEnumerable<WorkflowCompiledNode> nodes,
        IEnumerable<WorkflowCompiledEdge> edges,
        IEnumerable<WorkflowCompiledApprovalRequirement> approvalRequirements,
        IEnumerable<WorkflowCompiledCapabilityRequirement> capabilityRequirements,
        IEnumerable<WorkflowCompiledArtifactDeclaration> artifactDeclarations,
        IEnumerable<WorkflowInvalidationPlanEntry> invalidationPlanEntries)
    {
        var envelope = new DigestEnvelope(
            DigestScope.CanonicalJsonRecord,
            WorkflowSchemaId,
            WorkflowSchemaVersion,
            BuildWorkflowCanonicalJson(
                workflowId,
                compilerId,
                compilerVersion,
                protocol,
                template,
                inputs,
                nodes,
                edges,
                approvalRequirements,
                capabilityRequirements,
                artifactDeclarations,
                invalidationPlanEntries));
        return envelope.ComputeDigest();
    }

    private static string ComputeWorkflowId(
        ProtocolVersion protocol,
        WorkflowTemplate template,
        string compilerId,
        string compilerVersion)
    {
        var canonicalIdMaterial = new CanonicalJsonObject()
            .Add("compiler_id", Guard.NotBlank(compilerId, nameof(compilerId)))
            .Add("compiler_version", Guard.NotBlank(compilerVersion, nameof(compilerVersion)))
            .Add("protocol_id", protocol.ProtocolId)
            .Add("protocol_version_id", protocol.Id)
            .Add("protocol_content_digest", protocol.ContentDigest.ToString())
            .Add("protocol_version_number", protocol.VersionNumber)
            .Add("template_id", Guard.NotBlank(template.TemplateId, nameof(template.TemplateId)))
            .Add("template_version", Guard.NotBlank(template.TemplateVersion, nameof(template.TemplateVersion)))
            .Add("template_digest", template.TemplateDigest.ToString());

        var digest = ContentDigest.Sha256CanonicalJson(canonicalIdMaterial);
        return $"workflow-{digest.Value[0..16]}";
    }

    private static CanonicalJsonObject BuildTemplateCanonicalJson(WorkflowTemplate template)
    {
        return new CanonicalJsonObject()
            .Add("template_id", template.TemplateId)
            .Add("template_version", template.TemplateVersion)
            .Add("schema_id", template.SchemaId)
            .Add("schema_version", template.SchemaVersion)
            .Add("inputs", CanonicalJsonValue.Array(template.RequiredInputs
                .OrderBy(input => input.InputId, StringComparer.Ordinal)
                .Select(input => input.ToCanonicalJson()).ToArray()))
            .Add("nodes", CanonicalJsonValue.Array(template.Nodes
                .OrderBy(node => node.NodeId, StringComparer.Ordinal)
                .Select(node => node.ToCanonicalJson()).ToArray()))
            .Add("edges", CanonicalJsonValue.Array(template.Edges
                .OrderBy(edge => edge.FromNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.ToNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.Condition, StringComparer.Ordinal)
                .Select(edge => edge.ToCanonicalJson()).ToArray()))
            .Add("approval_requirements", CanonicalJsonValue.Array(template.ApprovalRequirements
                .OrderBy(req => req.ApprovalRequirementId, StringComparer.Ordinal)
                .Select(req => req.ToCanonicalJson()).ToArray()))
            .Add("roles", CanonicalJsonValue.Array(template.Roles
                .OrderBy(role => role.RoleId, StringComparer.Ordinal)
                .Select(role => role.ToCanonicalJson()).ToArray()))
            .Add("capabilities", CanonicalJsonValue.Array(template.CapabilityRequirements
                .OrderBy(capability => capability.CapabilityRef, StringComparer.Ordinal)
                .Select(capability => capability.ToCanonicalJson()).ToArray()))
            .Add("waiver_policies", CanonicalJsonValue.Array(template.WaiverPolicies
                .OrderBy(policy => policy.WaiverPolicyId, StringComparer.Ordinal)
                .Select(policy => policy.ToCanonicalJson()).ToArray()))
            .Add("artifact_declarations", CanonicalJsonValue.Array(template.ArtifactDeclarations
                .OrderBy(artifact => artifact.ArtifactRef, StringComparer.Ordinal)
                .Select(artifact => artifact.ToCanonicalJson()).ToArray()))
            .Add("invalidation_policies", CanonicalJsonValue.Array(template.InvalidationPolicies
                .OrderBy(policy => policy.InvalidationPolicyId, StringComparer.Ordinal)
                .Select(policy => policy.ToCanonicalJson()).ToArray()))
            .Add("gates", CanonicalJsonValue.Array(template.Gates
                .OrderBy(gate => gate.GateId, StringComparer.Ordinal)
                .Select(gate => gate.ToCanonicalJson()).ToArray()));
    }

    private static CanonicalJsonObject BuildWorkflowCanonicalJson(
        string workflowId,
        string compilerId,
        string compilerVersion,
        ProtocolVersion protocol,
        WorkflowTemplate template,
        IEnumerable<WorkflowResolvedInputBinding> inputs,
        IEnumerable<WorkflowCompiledNode> nodes,
        IEnumerable<WorkflowCompiledEdge> edges,
        IEnumerable<WorkflowCompiledApprovalRequirement> approvalRequirements,
        IEnumerable<WorkflowCompiledCapabilityRequirement> capabilityRequirements,
        IEnumerable<WorkflowCompiledArtifactDeclaration> artifactDeclarations,
        IEnumerable<WorkflowInvalidationPlanEntry> invalidationPlanEntries)
    {
        return new CanonicalJsonObject()
            .Add("workflow_id", workflowId)
            .Add("compiler_id", Guard.NotBlank(compilerId, nameof(compilerId)))
            .Add("compiler_version", Guard.NotBlank(compilerVersion, nameof(compilerVersion)))
            .Add("protocol_id", protocol.ProtocolId)
            .Add("protocol_version_id", protocol.Id)
            .Add("protocol_version_number", protocol.VersionNumber)
            .Add("protocol_content_digest", protocol.ContentDigest.ToString())
            .Add("template_id", template.TemplateId)
            .Add("template_version", template.TemplateVersion)
            .Add("template_digest", template.TemplateDigest.ToString())
            .Add("resolved_inputs", CanonicalJsonValue.Array(inputs
                .OrderBy(binding => binding.InputId, StringComparer.Ordinal)
                .Select(input => input.ToCanonicalJson())
                .ToArray()))
            .Add("nodes", CanonicalJsonValue.Array(nodes
                .Select(node => node.ToCanonicalJson())
                .ToArray()))
            .Add("edges", CanonicalJsonValue.Array(edges
                .OrderBy(edge => edge.FromNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.ToNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.Condition, StringComparer.Ordinal)
                .Select(edge => edge.ToCanonicalJson())
                .ToArray()))
            .Add("approval_requirements", CanonicalJsonValue.Array(approvalRequirements
                .OrderBy(req => req.ApprovalRequirementId, StringComparer.Ordinal)
                .Select(req => req.ToCanonicalJson())
                .ToArray()))
            .Add("capability_requirements", CanonicalJsonValue.Array(capabilityRequirements
                .OrderBy(req => req.CapabilityRef, StringComparer.Ordinal)
                .Select(req => req.ToCanonicalJson())
                .ToArray()))
            .Add("artifact_declarations", CanonicalJsonValue.Array(artifactDeclarations
                .OrderBy(declaration => declaration.ArtifactRef, StringComparer.Ordinal)
                .Select(item => item.ToCanonicalJson())
                .ToArray()))
            .Add("invalidation_plan", CanonicalJsonValue.Array(invalidationPlanEntries
                .OrderBy(item => item.AffectedNodeId, StringComparer.Ordinal)
                .ThenBy(item => item.AffectedArtifactDigest.Value, StringComparer.Ordinal)
                .ThenBy(item => item.RequiredAction, StringComparer.Ordinal)
                .Select(item => item.ToCanonicalJson())
                .ToArray()));
    }

}

internal sealed class SchemaRefComparer : IEqualityComparer<(string SchemaId, string SchemaVersion)>
{
    public bool Equals((string SchemaId, string SchemaVersion) x, (string SchemaId, string SchemaVersion) y)
    {
        return StringComparer.Ordinal.Equals(x.SchemaId, y.SchemaId) &&
            StringComparer.Ordinal.Equals(x.SchemaVersion, y.SchemaVersion);
    }

    public int GetHashCode((string SchemaId, string SchemaVersion) obj)
    {
        return HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(obj.SchemaId),
            StringComparer.Ordinal.GetHashCode(obj.SchemaVersion));
    }
}

internal static class WorkflowCompilationExtensions
{
    public static string ToWireValue(this WorkflowTemplateInputKind kind) =>
        kind == WorkflowTemplateInputKind.ScientificConduct
            ? "scientific_conduct"
            : "execution_parameter";

    public static string ToWireValue(this WorkflowResolvedInputSourceType sourceType) =>
        sourceType switch
        {
            WorkflowResolvedInputSourceType.ProtocolDecision => "protocol-decision",
            WorkflowResolvedInputSourceType.ProtocolValue => "protocol-value",
            WorkflowResolvedInputSourceType.ProtocolWaiver => "protocol-waiver",
            WorkflowResolvedInputSourceType.ProtocolAmendment => "protocol-amendment",
            WorkflowResolvedInputSourceType.CompileParameter => "compile-parameter",
            WorkflowResolvedInputSourceType.TemplateDefault => "template-default",
            _ => throw new InvalidOperationException($"Unsupported workflow source type '{sourceType}'.")
        };

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateInput input)
    {
        var obj = new CanonicalJsonObject()
            .Add("input_id", Guard.NotBlank(input.InputId, nameof(input.InputId)))
            .Add("input_kind", input.InputKind.ToWireValue())
            .Add("schema_id", Guard.NotBlank(input.SchemaId, nameof(input.SchemaId)))
            .Add("schema_version", Guard.NotBlank(input.SchemaVersion, nameof(input.SchemaVersion)))
            .Add("required", input.Required);

        if (input.SourceProtocolDecisionKey is not null)
        {
            obj.Add("source_protocol_decision_key", input.SourceProtocolDecisionKey);
        }

        if (input.DefaultValue is not null)
        {
            obj.Add("default_value", CanonicalJsonValue.DeepClone(input.DefaultValue));
        }

        return obj;
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateNode node)
    {
        return new CanonicalJsonObject()
            .Add("node_id", node.NodeId)
            .Add("kind", node.Kind.ToString())
            .Add("mode", node.Mode.ToString().ToLowerInvariant())
            .Add("requires", CanonicalJsonValue.Array(node.Requires.Select(CanonicalJsonValue.From).ToArray()))
            .Add("produces", CanonicalJsonValue.Array(node.Produces.Select(CanonicalJsonValue.From).ToArray()))
            .Add("approval_requirement_ref", node.ApprovalRequirementRef ?? string.Empty)
            .Add("capability_requirement_refs", CanonicalJsonValue.Array(node.CapabilityRequirementRefs
                .Select(CanonicalJsonValue.From).ToArray()))
            .Add("waiver_policy_ref", node.WaiverPolicyRef ?? string.Empty)
            .Add("invalidation_policy_ref", node.InvalidationPolicyRef ?? string.Empty)
            .Add("condition", node.Condition ?? string.Empty);
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateEdge edge)
    {
        var obj = new CanonicalJsonObject()
            .Add("from_node_id", edge.FromNodeId)
            .Add("to_node_id", edge.ToNodeId);

        if (edge.Condition is not null)
        {
            obj.Add("condition", edge.Condition);
        }

        return obj;
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateRole role)
    {
        var obj = new CanonicalJsonObject()
            .Add("role_id", Guard.NotBlank(role.RoleId, nameof(role.RoleId)))
            .Add("label", Guard.NotBlank(role.Label, nameof(role.Label)))
            .Add("authority_description", Guard.NotBlank(role.AuthorityDescription, nameof(role.AuthorityDescription)));

        if (role.MethodPackRef is not null)
        {
            obj.Add("method_pack_ref", role.MethodPackRef);
        }

        return obj;
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateApprovalRequirement requirement)
    {
        return new CanonicalJsonObject()
            .Add("approval_requirement_id", requirement.ApprovalRequirementId)
            .Add("policy_id", requirement.PolicyId)
            .Add("policy_version", requirement.PolicyVersion)
            .Add("policy_mode", requirement.PolicyMode)
            .Add("required_roles", CanonicalJsonValue.Array(requirement.RequiredRoles.Select(CanonicalJsonValue.From).ToArray()))
            .Add("minimum_approvals", requirement.MinimumApprovals)
            .Add("requires_distinct_actors", requirement.RequiresDistinctActors)
            .Add("allows_automation", requirement.AllowsAutomation);
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateGate gate)
    {
        return new CanonicalJsonObject()
            .Add("gate_id", gate.GateId)
            .Add("target_node_id", gate.TargetNodeId)
            .Add("policy_ref", gate.PolicyRef)
            .Add("required_artifact_refs", CanonicalJsonValue.Array(gate.RequiredArtifactRefs.Select(CanonicalJsonValue.From).ToArray()))
            .Add("required_decision_refs", CanonicalJsonValue.Array(gate.RequiredDecisionRefs.Select(CanonicalJsonValue.From).ToArray()))
            .Add("required_actor_roles", CanonicalJsonValue.Array(gate.RequiredActorRoles.Select(CanonicalJsonValue.From).ToArray()));
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateArtifactDeclaration declaration)
    {
        var obj = new CanonicalJsonObject()
            .Add("artifact_ref", declaration.ArtifactRef)
            .Add("artifact_kind", declaration.ArtifactKind)
            .Add("schema_id", declaration.SchemaId)
            .Add("schema_version", declaration.SchemaVersion)
            .Add("produced_by_node_id", declaration.ProducedByNodeId)
            .Add("required_for_gates", CanonicalJsonValue.Array(declaration.RequiredForGates.Select(CanonicalJsonValue.From).ToArray()));

        if (declaration.RetentionClass is not null)
        {
            obj.Add("retention_class", declaration.RetentionClass);
        }

        return obj;
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateCapabilityRequirement requirement)
    {
        return new CanonicalJsonObject()
            .Add("capability_ref", requirement.CapabilityRef)
            .Add("capability_kind", requirement.CapabilityKind)
            .Add("required_scopes", CanonicalJsonValue.Array(requirement.RequiredScopes.Select(CanonicalJsonValue.From).ToArray()))
            .Add("data_access_class", requirement.DataAccessClass)
            .Add("egress_allowed", requirement.EgressAllowed)
            .Add("plugin_capability", requirement.PluginCapability ?? string.Empty)
            .Add("ai_task_policy_ref", requirement.AiTaskPolicyRef ?? string.Empty);
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateWaiverPolicy policy)
    {
        return new CanonicalJsonObject()
            .Add("waiver_policy_id", policy.WaiverPolicyId)
            .Add("waivable_requirement_refs", CanonicalJsonValue.Array(policy.WaivableRequirementRefs.Select(CanonicalJsonValue.From).ToArray()))
            .Add("approval_requirement_ref", policy.ApprovalRequirementRef)
            .Add("disclosure_mapping", policy.DisclosureMapping)
            .Add("consequence_warning", policy.ConsequenceWarning);
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowTemplateInvalidationPolicy policy)
    {
        return new CanonicalJsonObject()
            .Add("invalidation_policy_id", policy.InvalidationPolicyId)
            .Add("affected_requirement_refs", CanonicalJsonValue.Array(policy.AffectedRequirementRefs.Select(CanonicalJsonValue.From).ToArray()))
            .Add("affected_artifact_refs", CanonicalJsonValue.Array(policy.AffectedArtifactRefs.Select(CanonicalJsonValue.From).ToArray()))
            .Add("affected_node_refs", CanonicalJsonValue.Array(policy.AffectedNodeRefs.Select(CanonicalJsonValue.From).ToArray()))
            .Add("required_action", policy.RequiredAction);
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowResolvedInputBinding binding)
    {
        var obj = new CanonicalJsonObject()
            .Add("input_id", binding.InputId)
            .Add("input_kind", binding.InputKind)
            .Add("schema_id", binding.SchemaId)
            .Add("schema_version", binding.SchemaVersion)
            .Add("source_type", binding.SourceType)
            .Add("source_ref", binding.SourceRef)
            .Add("source_digest", binding.SourceDigest.ToString())
            .Add("value_digest", binding.ValueDigest.ToString());

        if (binding.WaiverId is not null)
        {
            obj.Add("waiver_id", binding.WaiverId);
        }

        if (binding.AmendmentId is not null)
        {
            obj.Add("amendment_id", binding.AmendmentId);
        }

        return obj;
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowCompiledNode node)
    {
        return new CanonicalJsonObject()
            .Add("node_id", node.NodeId)
            .Add("kind", node.Kind.ToString())
            .Add("mode", node.Mode.ToString())
            .Add("label", node.Label)
            .Add("depends_on", CanonicalJsonValue.Array(node.DependsOn.Select(CanonicalJsonValue.From).ToArray()))
            .Add("produces", CanonicalJsonValue.Array(node.Produces.Select(CanonicalJsonValue.From).ToArray()))
            .Add("requires", CanonicalJsonValue.Array(node.Requires.Select(CanonicalJsonValue.From).ToArray()))
            .Add("approval_requirement_ref", node.ApprovalRequirementRef ?? string.Empty)
            .Add("capability_requirement_refs", CanonicalJsonValue.Array(node.CapabilityRequirementRefs.Select(CanonicalJsonValue.From).ToArray()))
            .Add("waiver_policy_ref", node.WaiverPolicyRef ?? string.Empty)
            .Add("invalidation_policy_ref", node.InvalidationPolicyRef ?? string.Empty)
            .Add("condition", node.Condition ?? string.Empty);
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowCompiledEdge edge)
    {
        var obj = new CanonicalJsonObject()
            .Add("from_node_id", edge.FromNodeId)
            .Add("to_node_id", edge.ToNodeId);
        if (edge.Condition is not null)
        {
            obj.Add("condition", edge.Condition);
        }

        return obj;
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowCompiledApprovalRequirement requirement)
    {
        return new CanonicalJsonObject()
            .Add("approval_requirement_id", requirement.ApprovalRequirementId)
            .Add("policy_id", requirement.PolicyId)
            .Add("policy_version", requirement.PolicyVersion)
            .Add("policy_mode", requirement.PolicyMode)
            .Add("required_roles", CanonicalJsonValue.Array(requirement.RequiredRoles.Select(CanonicalJsonValue.From).ToArray()))
            .Add("minimum_approvals", requirement.MinimumApprovals)
            .Add("requires_distinct_actors", requirement.RequiresDistinctActors)
            .Add("allows_automation", requirement.AllowsAutomation);
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowCompiledCapabilityRequirement capability)
    {
        var obj = new CanonicalJsonObject()
            .Add("capability_ref", capability.CapabilityRef)
            .Add("capability_kind", capability.CapabilityKind)
            .Add("required_scopes", CanonicalJsonValue.Array(capability.RequiredScopes.Select(CanonicalJsonValue.From).ToArray()))
            .Add("data_access_class", capability.DataAccessClass)
            .Add("egress_allowed", capability.EgressAllowed)
            .Add("plugin_capability", capability.PluginCapability ?? string.Empty)
            .Add("ai_task_policy_ref", capability.AiTaskPolicyRef ?? string.Empty);

        return obj;
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowCompiledArtifactDeclaration artifact)
    {
        var obj = new CanonicalJsonObject()
            .Add("artifact_ref", artifact.ArtifactRef)
            .Add("artifact_kind", artifact.ArtifactKind)
            .Add("schema_id", artifact.SchemaId)
            .Add("schema_version", artifact.SchemaVersion)
            .Add("produced_by_node_id", artifact.ProducedByNodeId)
            .Add("required_for_gates", CanonicalJsonValue.Array(artifact.RequiredForGates.Select(CanonicalJsonValue.From).ToArray()));

        if (artifact.RetentionClass is not null)
        {
            obj.Add("retention_class", artifact.RetentionClass);
        }

        return obj;
    }

    public static CanonicalJsonValue ToCanonicalJson(this WorkflowInvalidationPlanEntry entry)
    {
        return new CanonicalJsonObject()
            .Add("notice_id", entry.NoticeId)
            .Add("amendment_id", entry.AmendmentId)
            .Add("produces_version_id", entry.ProducesVersionId)
            .Add("previous_content_digest", entry.PreviousContentDigest.ToString())
            .Add("amendment_source_digest", entry.AmendmentSourceDigest.ToString())
            .Add("invalidation_notice_digest", entry.InvalidationNoticeDigest.ToString())
            .Add("affected_requirement_id", entry.AffectedRequirementId)
            .Add("affected_artifact_digest", entry.AffectedArtifactDigest.ToString())
            .Add("affected_node_id", entry.AffectedNodeId)
            .Add("required_action", entry.RequiredAction);
    }
}

internal static class CanonicalJsonExtensions
{
    public static CanonicalJsonValue AsJsonArray(this IEnumerable<string> values)
    {
        return CanonicalJsonValue.Array(values.Select(CanonicalJsonValue.From).ToArray());
    }
}
