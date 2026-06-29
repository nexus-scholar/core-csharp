using NexusScholar.Bundles;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Workflow;

namespace NexusScholar.Cli;

public static class CliApplication
{
    public const string Usage = "Usage: dotnet run --project src/NexusScholar.Cli -- [doctor|sample|demo]";

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "doctor";

        return command switch
        {
            "doctor" => RunDoctor(output),
            "sample" => RunSample(output),
            "demo" => LocalDemoCommand.Run(output),
            _ => ShowHelp(error)
        };
    }

    private static int RunDoctor(TextWriter output)
    {
        output.WriteLine("Nexus Scholar Core doctor");
        output.WriteLine($"Framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        output.WriteLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        output.WriteLine("Policy: model outputs are proposals; approved protocols are immutable.");
        return 0;
    }

    private static int RunSample(TextWriter output)
    {
        var ids = new GuidV7IdGenerator();
        var clock = new SystemClock();
        var researcher = ActorId.From("local-researcher");

        var draft = ProtocolDraft.Create(ids, "Starter review", new[] { "review-type", "scope" });
        draft.RecordDecision("review-type", "scoping-review", researcher, clock);
        draft.RecordDecision("scope", "agricultural image segmentation", researcher, clock);
        var version = draft.Approve(researcher, clock, ids);
        var workflow = new WorkflowCompiler().Compile(BuildSampleWorkflowInput(version));

        var provenance = new InMemoryProvenanceStore();
        provenance.Append(ResearchEventFactory.Create(
            ids,
            clock,
            "protocol-approved",
            "protocol-version",
            version.Id.ToString(),
            researcher,
            outputs: new[] { version.Digest }));

        var manifest = new ReviewBundleManifest(
            "nexus.review-bundle/v1",
            "sample-project",
            version.Digest,
            workflow.Id,
            clock.UtcNow,
            Array.Empty<BundleArtifact>());
        var verification = new BundleVerifier().Verify(
            manifest,
            new BundleVerificationOptions
            {
                KnownProtocolContentDigests = new Dictionary<string, ContentDigest>(StringComparer.Ordinal)
                {
                    ["sample-project-version"] = version.Digest
                }
            });

        output.WriteLine($"Protocol digest: {version.Digest}");
        output.WriteLine($"Workflow: {workflow.Id} ({workflow.Nodes.Count} nodes)");
        output.WriteLine($"Provenance events: {provenance.ReadAll().Count}");
        output.WriteLine($"Bundle valid: {verification.IsValid}");
        return verification.IsValid ? 0 : 1;
    }

    private static int ShowHelp(TextWriter error)
    {
        error.WriteLine(Usage);
        return 2;
    }

    private static WorkflowCompileInput BuildSampleWorkflowInput(ProtocolVersion version)
    {
        var template = new WorkflowTemplate(
            "local-sample-workflow-template",
            "1.0.0",
            ContentDigest.Sha256Utf8("placeholder"),
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
                    "scope",
                    WorkflowTemplateInputKind.ScientificConduct,
                    "nexus.review.decision",
                    "1.0.0",
                    true,
                    "scope")
            },
            new[]
            {
                new WorkflowTemplateNode(
                    "protocol-approved",
                    WorkflowNodeKind.Milestone,
                    WorkflowNodeMode.Human,
                    "Protocol approved",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    null,
                    Array.Empty<string>(),
                    null,
                    null),
                new WorkflowTemplateNode(
                    "prepare-search",
                    WorkflowNodeKind.HumanTask,
                    WorkflowNodeMode.Human,
                    "Prepare and review search plan",
                    new[] { "review-type", "scope" },
                    new[] { "search-plan" },
                    null,
                    Array.Empty<string>(),
                    null,
                    null),
                new WorkflowTemplateNode(
                    "approve-search",
                    WorkflowNodeKind.Approval,
                    WorkflowNodeMode.Human,
                    "Approve executable search manifest",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "search-approval",
                    Array.Empty<string>(),
                    null,
                    null),
                new WorkflowTemplateNode(
                    "execute-search",
                    WorkflowNodeKind.AutomatedTask,
                    WorkflowNodeMode.Automated,
                    "Execute approved search manifest",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    null,
                    Array.Empty<string>(),
                    null,
                    null),
                new WorkflowTemplateNode(
                    "lock-corpus",
                    WorkflowNodeKind.Approval,
                    WorkflowNodeMode.Human,
                    "Approve immutable corpus snapshot",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "corpus-approval",
                    Array.Empty<string>(),
                    null,
                    null)
            },
            new[]
            {
                new WorkflowTemplateEdge("protocol-approved", "prepare-search"),
                new WorkflowTemplateEdge("prepare-search", "approve-search"),
                new WorkflowTemplateEdge("approve-search", "execute-search"),
                new WorkflowTemplateEdge("execute-search", "lock-corpus")
            },
            new[]
            {
                new WorkflowTemplateGate(
                    "search-approval-gate",
                    "approve-search",
                    "search-approval",
                    new[] { "search-plan" },
                    Array.Empty<string>(),
                    new[] { "researcher" }),
                new WorkflowTemplateGate(
                    "corpus-approval-gate",
                    "lock-corpus",
                    "corpus-approval",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    new[] { "researcher" })
            },
            new[]
            {
                new WorkflowTemplateApprovalRequirement(
                    "search-approval",
                    "local-human-approval",
                    "1.0.0",
                    "single_researcher",
                    new[] { "researcher" },
                    1,
                    false,
                    false),
                new WorkflowTemplateApprovalRequirement(
                    "corpus-approval",
                    "local-human-approval",
                    "1.0.0",
                    "single_researcher",
                    new[] { "researcher" },
                    1,
                    false,
                    false)
            },
            new[]
            {
                new WorkflowTemplateRole("researcher", "Researcher", "Human workflow approval authority.")
            },
            Array.Empty<WorkflowTemplateCapabilityRequirement>(),
            Array.Empty<WorkflowTemplateWaiverPolicy>(),
            new[]
            {
                new WorkflowTemplateArtifactDeclaration(
                    "search-plan",
                    "workflow-artifact",
                    "nexus.workflow.artifact",
                    "1.0.0",
                    "prepare-search",
                    new[] { "search-approval-gate" })
            },
            Array.Empty<WorkflowTemplateInvalidationPolicy>());

        var sealedTemplate = template with
        {
            TemplateDigest = WorkflowCompiler.ComputeTemplateDigestForTesting(template)
        };

        return new WorkflowCompileInput(
            version,
            sealedTemplate,
            new Dictionary<string, CanonicalJsonValue>(StringComparer.Ordinal),
            new[]
            {
                new WorkflowSchemaRef("nexus.workflow-template", "1.0.0"),
                new WorkflowSchemaRef("nexus.workflow-definition", "1.0.0"),
                new WorkflowSchemaRef("nexus.review.decision", "1.0.0"),
                new WorkflowSchemaRef("nexus.workflow.artifact", "1.0.0")
            });
    }
}
