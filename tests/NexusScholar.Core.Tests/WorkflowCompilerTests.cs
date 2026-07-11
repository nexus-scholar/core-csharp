using System;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Workflow;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class WorkflowCompilerTests
{
    private static readonly ProtocolActor Researcher = ProtocolActor.Human("researcher-1");
    private static readonly IClock Clock = new FixedClock();
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
    public void Compile_rejects_workflow_id_mismatch()
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        var expected = "workflow-0000000000000000";
        var input = BuildInput(protocol, template, expectedWorkflowId: expected);
        var error = Assert.ThrowsExactly<WorkflowRuleException>(() => new WorkflowCompiler().Compile(input));
        Assert.AreEqual(WorkflowErrorCodes.WorkflowIdMismatch, error.Category);
    }

    private static WorkflowCompileInput BuildInput(
        ProtocolVersion protocol,
        WorkflowTemplate template,
        WorkflowTemplate? unknownSchemaTemplate = null,
        IReadOnlyDictionary<string, CanonicalJsonValue>? compileParameters = null,
        ProtocolAmendment? amendment = null,
        IEnumerable<ProtocolInvalidationNotice>? notices = null,
        string? expectedWorkflowId = null)
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
            amendment is null ? null : new[] { amendment },
            notices?.ToArray(),
            expectedWorkflowId,
            "nexus-workflow-compiler",
            "1.0.0");
    }

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

        public TestWorkflowAuthorityResolver(VerifiedProtocolVersion protocol, WorkflowTemplate template)
        {
            _protocol = protocol;
            _template = template;
        }

        public VerifiedProtocolVersion ResolveProtocolVersion(string protocolVersionId) =>
            string.Equals(protocolVersionId, _protocol.Version.Id, StringComparison.Ordinal) ? _protocol : null!;

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
