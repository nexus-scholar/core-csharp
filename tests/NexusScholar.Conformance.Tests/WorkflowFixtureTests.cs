using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Workflow;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class WorkflowFixtureTests
{
    private const string FixtureSourceCommit = "bc2e6e0353b2cc44f837750ed1dd5f570cc1c4fa";
    private static readonly ProtocolActor Researcher = ProtocolActor.Human("researcher-1");
    private static readonly IClock Clock = new FixedClock();

    private static readonly string[] PositiveFixtures =
    {
        "workflow-compile-rapid-review.json",
        "workflow-compile-hybrid-ai-audit.json",
        "workflow-compile-authorized-waiver.json",
        "workflow-compile-invalidation-plan.json",
        "workflow-compile-order-permutation-same-digest.json",
        "workflow-compile-digest-exclusion-stable.json",
        "workflow-compile-digest-inclusion-changed.json"
    };

    private static readonly string[] RequiredNegativeCategories =
    {
        "duplicate-node-id",
        "unknown-edge-endpoint",
        "unknown-node-requirement",
        "self-edge",
        "dependency-cycle",
        "waivable-node-without-waiver-policy",
        "unknown-approval-role",
        "invalid-approval-requirement",
        "unknown-gate-policy",
        "unknown-gate-artifact-ref",
        "unknown-gate-decision-ref",
        "missing-schema-id",
        "unknown-schema-id",
        "missing-schema-version",
        "undeclared-produced-artifact",
        "unknown-producing-node",
        "unknown-capability-reference",
        "unknown-compile-parameter",
        "missing-required-input",
        "conduct-input-from-compile-parameter",
        "invalid-protocol-status",
        "stale-protocol-digest",
        "stale-template-digest",
        "workflow-id-mismatch",
        "explicit-compile-input-required",
        "automation-approval-authority",
        "invalid-hybrid-node",
        "missing-waiver-disclosure-mapping",
        "missing-waiver-consequence-warning",
        "expired-waiver",
        "waiver-affected-requirement-mismatch",
        "waiver-missing-approval-binding",
        "unauthorized-waiver",
        "missing-invalidation-source",
        "stale-invalidation-notice",
        "affected-artifact-mismatch",
        "affected-node-not-found"
    };

    [TestMethod]
    public void Gate_4_workflow_fixtures_have_required_metadata()
    {
        foreach (var path in WorkflowFixturePaths())
        {
            var root = Load(path);

            Assert.AreEqual("local-gate-4-contract", root.GetProperty("sourceKind").GetString(), Path.GetFileName(path));
            Assert.AreEqual(FixtureSourceCommit, root.GetProperty("sourceCommit").GetString(), Path.GetFileName(path));
            Assert.AreEqual("compiler-backed local Gate 4 contract fixture", root.GetProperty("generatorCommand").GetString(), Path.GetFileName(path));
            Assert.AreEqual("gate-4-v2", root.GetProperty("generatorVersion").GetString(), Path.GetFileName(path));
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0005-workflow-template-contract.md", StringComparison.Ordinal)));
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0006-workflow-compiler-semantics.md", StringComparison.Ordinal)));
            _ = ContentDigest.Parse(root.GetProperty("inputDigest").GetString()!);
            _ = ContentDigest.Parse(root.GetProperty("outputDigest").GetString()!);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(rule =>
                string.Equals(rule.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)));
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(rule =>
                string.Equals(rule.GetString(), "no-blueprint-conformance-claim", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void Gate_4_positive_fixture_pack_is_present()
    {
        var names = WorkflowFixturePaths()
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fixture in PositiveFixtures)
        {
            Assert.IsTrue(names.Contains(fixture), $"Missing Gate 4 workflow fixture '{fixture}'.");
        }
    }

    [TestMethod]
    public void Gate_4_positive_fixtures_preserve_non_claim_boundaries()
    {
        foreach (var fixture in PositiveFixtures)
        {
            var root = LoadWorkflowFixture(fixture);
            var item = root.GetProperty("case");

            Assert.IsFalse(item.GetProperty("negative").GetBoolean(), fixture);
            Assert.AreEqual("workflow-compile", item.GetProperty("recordType").GetString(), fixture);
            Assert.AreEqual("planned-workflow-graph", item.GetProperty("outputKind").GetString(), fixture);
            Assert.IsTrue(item.GetProperty("schemaRefs").EnumerateArray().Any(schema =>
                string.Equals(schema.GetProperty("schema_id").GetString(), "nexus.workflow-template", StringComparison.Ordinal)));
            Assert.IsTrue(item.GetProperty("digestExcludes").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "workflow_execution_records", StringComparison.Ordinal)));
            Assert.IsTrue(item.GetProperty("nonClaims").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void Positive_workflow_fixtures_match_compiler_output()
    {
        var errors = new List<string>();
        foreach (var fixture in PositiveFixtures)
        {
            var root = LoadWorkflowFixture(fixture);
            var caseElement = root.GetProperty("case");
            var computation = BuildFixtureComputation(Path.GetFileNameWithoutExtension(fixture));
            var actualWorkflowDigest = OptionalString(caseElement, "workflowDigest", "value");
            var actualTemplateDigest = OptionalString(caseElement, "template", "template_digest");
            var actualProtocolDigest = OptionalString(caseElement, "protocol", "content_digest");

            AddMismatch(errors, fixture, "inputDigest", root.GetProperty("inputDigest").GetString(), computation.InputDigest.ToString());
            AddMismatch(errors, fixture, "outputDigest", root.GetProperty("outputDigest").GetString(), computation.Workflow.WorkflowDigest.ToString());
            AddMismatch(errors, fixture, "workflowId", caseElement.GetProperty("workflowId").GetString(), computation.Workflow.WorkflowId);
            AddMismatch(errors, fixture, "workflowDigest.value", actualWorkflowDigest, computation.Workflow.WorkflowDigest.ToString());
            AddMismatch(errors, fixture, "template.template_digest", actualTemplateDigest, computation.Workflow.TemplateDigest.ToString());
            AddMismatch(errors, fixture, "protocol.content_digest", actualProtocolDigest, computation.Workflow.ProtocolContentDigest.ToString());
        }

        Assert.IsFalse(errors.Count > 0, string.Join(Environment.NewLine, errors));
    }

    [TestMethod]
    public void Digest_fixture_pairs_capture_inclusion_and_exclusion_boundaries()
    {
        var rapid = BuildFixtureComputation("workflow-compile-rapid-review");
        var orderPermutation = BuildFixtureComputation("workflow-compile-order-permutation-same-digest");
        var exclusionStable = BuildFixtureComputation("workflow-compile-digest-exclusion-stable");
        var inclusionChanged = BuildFixtureComputation("workflow-compile-digest-inclusion-changed");

        Assert.AreEqual(rapid.Workflow.WorkflowDigest, orderPermutation.Workflow.WorkflowDigest);
        Assert.AreEqual(rapid.Workflow.WorkflowDigest, exclusionStable.Workflow.WorkflowDigest);
        Assert.AreNotEqual(rapid.Workflow.WorkflowDigest, inclusionChanged.Workflow.WorkflowDigest);
    }

    [TestMethod]
    public void Negative_workflow_fixture_pack_covers_required_error_categories()
    {
        var root = LoadWorkflowFixture("workflow-compile-negative-cases.json");
        var categories = root.GetProperty("case")
            .GetProperty("cases")
            .EnumerateArray()
            .Select(item => item.GetProperty("errorCategory").GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var category in RequiredNegativeCategories)
        {
            Assert.IsTrue(categories.Contains(category), $"Missing Gate 4 negative fixture for '{category}'.");
        }
    }

    private static string? OptionalString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var property in path)
        {
            if (!current.TryGetProperty(property, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();
    }

    private static void AddMismatch(List<string> errors, string fixture, string field, string? actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            errors.Add($"{fixture} {field}: expected {expected}, found {actual}");
        }
    }

    private static FixtureComputation BuildFixtureComputation(string fixtureId)
    {
        var protocol = BuildApprovedProtocol();
        var template = BuildTemplate();
        IReadOnlyDictionary<string, CanonicalJsonValue> compileParameters =
            new Dictionary<string, CanonicalJsonValue>(StringComparer.Ordinal);
        ProtocolAmendment? amendment = null;
        IReadOnlyList<ProtocolInvalidationNotice>? notices = null;

        switch (fixtureId)
        {
            case "workflow-compile-rapid-review":
                break;
            case "workflow-compile-hybrid-ai-audit":
                template = BuildTemplate(templateId: "template-hybrid-ai-audit", hybridAi: true);
                break;
            case "workflow-compile-authorized-waiver":
                protocol = BuildApprovedProtocol(withDecision: false, withWaiver: true, waiverExpiresAt: Clock.UtcNow.AddDays(1));
                template = BuildTemplate(templateId: "template-authorized-waiver", withWaiverPolicy: true);
                break;
            case "workflow-compile-invalidation-plan":
                var previous = BuildApprovedProtocol();
                template = BuildTemplate(templateId: "template-invalidation-plan", withInvalidationPolicy: true, approveNodeId: "approve-search");
                amendment = BuildAmendment(previous, affectedNodeId: "approve-search");
                protocol = RecastProtocol(BuildApprovedProtocol(), ProtocolStatus.Approved, amendment.AmendmentId);
                notices = amendment.InvalidationNotices;
                break;
            case "workflow-compile-order-permutation-same-digest":
                template = BuildTemplate(permutedOrder: true);
                break;
            case "workflow-compile-digest-exclusion-stable":
                break;
            case "workflow-compile-digest-inclusion-changed":
                compileParameters = new Dictionary<string, CanonicalJsonValue>(StringComparer.Ordinal)
                {
                    ["priority"] = CanonicalJsonValue.From("low")
                };
                break;
            default:
                throw new InvalidOperationException($"Unknown workflow fixture '{fixtureId}'.");
        }

        var input = BuildInput(protocol, template, compileParameters, amendment, notices);
        var workflow = new WorkflowCompiler().Compile(input);
        return new FixtureComputation(workflow, ComputeInputDigest(input));
    }

    private static WorkflowCompileInput BuildInput(
        ProtocolVersion protocol,
        WorkflowTemplate template,
        IReadOnlyDictionary<string, CanonicalJsonValue>? compileParameters = null,
        ProtocolAmendment? amendment = null,
        IReadOnlyList<ProtocolInvalidationNotice>? notices = null)
    {
        return new WorkflowCompileInput(
            protocol,
            template,
            compileParameters ?? new Dictionary<string, CanonicalJsonValue>(StringComparer.Ordinal),
            new[]
            {
                new WorkflowSchemaRef("nexus.workflow-template", "1.0.0"),
                new WorkflowSchemaRef("nexus.workflow-definition", "1.0.0"),
                new WorkflowSchemaRef("nexus.review.decision", "1.0.0"),
                new WorkflowSchemaRef("nexus.workflow.artifact", "1.0.0")
            },
            amendment is null ? null : new[] { amendment },
            notices,
            null,
            "nexus-workflow-compiler",
            "1.0.0");
    }

    private static ContentDigest ComputeInputDigest(WorkflowCompileInput input)
    {
        var canonical = new CanonicalJsonObject()
            .Add("compiler_id", input.CompilerId)
            .Add("compiler_version", input.CompilerVersion)
            .Add("protocol_content_digest", input.ProtocolVersion.ContentDigest.ToString())
            .Add("protocol_version_id", input.ProtocolVersion.Id)
            .Add("template_digest", input.Template.TemplateDigest.ToString())
            .Add("template_id", input.Template.TemplateId)
            .Add("template_version", input.Template.TemplateVersion)
            .Add("compile_parameters", CanonicalJsonValue.Array(input.CompileParameters
                .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
                .Select(parameter => new CanonicalJsonObject()
                    .Add("key", parameter.Key)
                    .Add("value", parameter.Value))
                .ToArray()))
            .Add("known_schema_refs", CanonicalJsonValue.Array(input.KnownSchemaRefs
                .OrderBy(item => item.SchemaId, StringComparer.Ordinal)
                .ThenBy(item => item.Version, StringComparer.Ordinal)
                .Select(item => new CanonicalJsonObject()
                    .Add("schema_id", item.SchemaId)
                    .Add("schema_version", item.Version))
                .ToArray()))
            .Add("amendment_ids", CanonicalJsonValue.Array((input.Amendments ?? Array.Empty<ProtocolAmendment>())
                .OrderBy(item => item.AmendmentId, StringComparer.Ordinal)
                .Select(item => CanonicalJsonValue.From(item.AmendmentId))
                .ToArray()))
            .Add("invalidation_notice_ids", CanonicalJsonValue.Array((input.InvalidationNotices ?? Array.Empty<ProtocolInvalidationNotice>())
                .OrderBy(item => item.NoticeId, StringComparer.Ordinal)
                .Select(item => CanonicalJsonValue.From(item.NoticeId))
                .ToArray()));

        return ContentDigest.Sha256CanonicalJson(canonical);
    }

    private static WorkflowTemplate BuildTemplate(
        string templateId = "template-rapid-review",
        bool hybridAi = false,
        bool withWaiverPolicy = false,
        bool withInvalidationPolicy = false,
        bool permutedOrder = false,
        string approveNodeId = "approve")
    {
        var capabilityRef = hybridAi ? "cap.ai-proposal" : "cap.search";
        var nodes = new List<WorkflowTemplateNode>
        {
            new(
                "start",
                WorkflowNodeKind.HumanTask,
                WorkflowNodeMode.Human,
                "Start",
                Array.Empty<string>(),
                new[] { "search-plan" },
                null,
                new[] { "cap.search" },
                withWaiverPolicy ? "waive-review" : null,
                null,
                null),
            new(
                approveNodeId,
                WorkflowNodeKind.Approval,
                hybridAi ? WorkflowNodeMode.Hybrid : WorkflowNodeMode.Human,
                "Approve",
                new[] { "review-type" },
                new[] { "review-plan" },
                "approve-review",
                new[] { capabilityRef },
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

        if (permutedOrder)
        {
            nodes.Reverse();
        }

        var capabilityRequirements = new List<WorkflowTemplateCapabilityRequirement>
        {
            new("cap.search", "search-capability", new[] { "read" }, "restricted", true)
        };

        if (hybridAi)
        {
            capabilityRequirements.Add(new WorkflowTemplateCapabilityRequirement(
                "cap.ai-proposal",
                "ai-proposal-support",
                new[] { "propose" },
                "restricted",
                false,
                null,
                "local-ai-proposal-policy"));
        }

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

        var artifactDeclarations = new[]
        {
            new WorkflowTemplateArtifactDeclaration(
                "search-plan",
                "workflow-artifact",
                "nexus.workflow.artifact",
                "1.0.0",
                "start",
                new[] { "g1" },
                null),
            new WorkflowTemplateArtifactDeclaration(
                "review-plan",
                "workflow-artifact",
                "nexus.workflow.artifact",
                "1.0.0",
                approveNodeId,
                Array.Empty<string>(),
                null)
        };

        var invalidationPolicies = withInvalidationPolicy
            ? new[]
            {
                new WorkflowTemplateInvalidationPolicy(
                    "p1",
                    new[] { "review-type" },
                    artifactDeclarations.Select(artifact => artifact.ArtifactRef).ToArray(),
                    new[] { approveNodeId },
                    "rerun")
            }
            : Array.Empty<WorkflowTemplateInvalidationPolicy>();

        var template = new WorkflowTemplate(
            templateId,
            "1.0.0",
            ContentDigest.Sha256Utf8("template"),
            "nexus.workflow-template",
            "1.0.0",
            new[]
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
            },
            nodes.ToArray(),
            new[]
            {
                new WorkflowTemplateEdge("start", approveNodeId),
                new WorkflowTemplateEdge(approveNodeId, "execute"),
                new WorkflowTemplateEdge("execute", "finish")
            },
            new[]
            {
                new WorkflowTemplateGate(
                    "g1",
                    approveNodeId,
                    "approve-review",
                    new[] { "search-plan" },
                    new[] { "review-type" },
                    new[] { "methodologist" })
            },
            new[]
            {
                new WorkflowTemplateApprovalRequirement(
                    "approve-review",
                    "review-policy",
                    "1.0.0",
                    "single_reviewer",
                    new[] { "methodologist" },
                    1,
                    false,
                    false)
            },
            new[]
            {
                new WorkflowTemplateRole("methodologist", "Methodologist", "Scientific adjudication")
            },
            capabilityRequirements.ToArray(),
            waiverPolicies,
            artifactDeclarations,
            invalidationPolicies);

        return template with
        {
            TemplateDigest = WorkflowCompiler.ComputeTemplateDigestForTesting(template)
        };
    }

    private static ProtocolVersion BuildApprovedProtocol(
        bool withDecision = true,
        bool withWaiver = false,
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
                waiverExpiresAt,
                "Limited scope allowed.",
                "Report the scope limitation.",
                "review-type",
                Researcher,
                Clock,
                ApprovalPolicy.ExplicitCustomSingleResearcher(),
                new[] { "approval-1" });
        }

        var candidate = draft.CreateApprovalCandidate(ids, ApprovalPolicy.ExplicitCustomSingleResearcher(), versionId: "proto-v1");
        var approval = ProtocolApproval.Create(ids, candidate, ApprovalPolicy.ExplicitCustomSingleResearcher(), Researcher, Clock, candidate.ContentDigest);
        return draft.ApproveCandidate(candidate, ApprovalPolicy.ExplicitCustomSingleResearcher(), new[] { approval }, Clock);
    }

    private static ProtocolVersion RecastProtocol(
        ProtocolVersion source,
        ProtocolStatus status,
        string? amendmentId = null)
    {
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
            source.ContentDigest,
            source.ApprovalPolicyId,
            source.ApprovalIds,
            source.ApprovedAt,
            source.SupersedesVersionId,
            source.SupersededByVersionId,
            amendmentId ?? source.AmendmentId,
            source.UnresolvedDecisions);

        if (string.Equals(recast.AmendmentId, source.AmendmentId, StringComparison.Ordinal))
        {
            return recast;
        }

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

    private static ProtocolAmendment BuildAmendment(ProtocolVersion version, string affectedNodeId)
    {
        var ids = new SequenceIdGenerator();
        var notice = new ProtocolInvalidationNotice(
            "notice-1",
            "pending-amendment",
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

    private static JsonElement LoadWorkflowFixture(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "workflow", filename);
        return Load(path);
    }

    private static JsonElement Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string[] WorkflowFixturePaths()
    {
        return Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures", "workflow"), "*.json");
    }

    private sealed record FixtureComputation(WorkflowDefinition Workflow, ContentDigest InputDigest);

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
