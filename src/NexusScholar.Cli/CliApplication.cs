using NexusScholar.Bundles;
using NexusScholar.Cli.ResearchWorkspace;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Workflow;

namespace NexusScholar.Cli;

public static class CliApplication
{
    public const string Usage = "Usage: dotnet run --project src/NexusScholar.Cli -- [doctor|sample|demo|init|status|import|verify|analyze|review|clusters|dedup decide|screening status]";

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        return Run(args, output, error, Directory.GetCurrentDirectory(), () => DateTimeOffset.UtcNow);
    }

    internal static int Run(
        string[] args,
        TextWriter output,
        TextWriter error,
        string workingDirectory,
        Func<DateTimeOffset> utcNow)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(utcNow);

        var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "doctor";

        return command switch
        {
            "doctor" => RunDoctor(output),
            "sample" => RunSample(output),
            "demo" => LocalDemoCommand.Run(output),
            "init" => ResearchWorkspaceInitCommand.Run(args.Skip(1).ToArray(), output, error, workingDirectory, utcNow),
            "status" => ResearchWorkspaceStatusCommand.Run(output, error, workingDirectory),
            "import" => SearchImportWorkspaceCommand.Run(args.Skip(1).ToArray(), output, error, workingDirectory, utcNow),
            "verify" => ResearchWorkspaceVerifyCommand.Run(args.Skip(1).ToArray(), output, error, workingDirectory),
            "analyze" => ResearchWorkspaceAnalyzeCommand.Run(args.Skip(1).ToArray(), output, error, workingDirectory),
            "review" => ResearchWorkspaceReviewCommand.Run(args.Skip(1).ToArray(), output, error, workingDirectory),
            "clusters" => ResearchWorkspaceClustersCommand.Run(args.Skip(1).ToArray(), output, error, workingDirectory),
            "dedup" when args.Skip(1).FirstOrDefault()?.Equals("decide", StringComparison.OrdinalIgnoreCase) == true =>
                DeduplicationDecideCommand.Run(args.Skip(2).ToArray(), output, error, workingDirectory, utcNow),
            "screening" when args.Skip(1).FirstOrDefault()?.Equals("status", StringComparison.OrdinalIgnoreCase) == true =>
                ScreeningStatusCommand.Run(output, error, workingDirectory),
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
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var candidate = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(
            ids,
            candidate,
            policy,
            ProtocolActor.Human(researcher),
            clock,
            candidate.ContentDigest);
        var protocolAuthority = draft.ApproveCandidateVerified(candidate, policy, new[] { approval }, clock);
        var version = protocolAuthority.Version;
        var workflow = new WorkflowCompiler().Compile(BuildSampleWorkflowInput(protocolAuthority));

        var provenance = new InMemoryProvenanceStore();
        var provenanceEvent = ResearchEventFactory.Create(
            ids,
            clock,
            "protocol-approved",
            "protocol-version",
            version.Id.ToString(),
            researcher,
            outputs: new[] { version.Digest });
        provenance.Append(provenanceEvent);

        var manifest = new ReviewBundleManifest(
            "sample-bundle",
            researcher.ToString(),
            new BundleProtocolBinding(
                version.ProtocolId,
                version.Id,
                version.VersionNumber,
                BundleConstants.ApprovedProtocolStatus,
                version.ContentDigest),
            Array.Empty<BundleArtifactEntry>(),
            Array.Empty<BundleSchemaRef>(),
            clock.UtcNow,
            new BundleWorkflowBinding(
                workflow.WorkflowId,
                workflow.WorkflowDigest,
                workflow.TemplateId,
                workflow.TemplateVersion,
                workflow.TemplateDigest,
                workflow.ProtocolVersionId,
                workflow.ProtocolContentDigest),
            new[]
            {
                new BundleProvenanceBinding(
                    provenanceEvent.EventId.ToString(),
                    provenanceEvent.EventDigest,
                    provenanceEvent.Activity.ActivityId,
                    provenanceEvent.OccurredAt,
                    provenanceEvent.Agent.AgentId)
            });
        var verification = new BundleVerifier().Verify(
            manifest,
            new BundleVerificationOptions
            {
                AuthorityResolver = new SampleBundleAuthorityResolver(protocolAuthority, workflow, provenanceEvent)
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

    private sealed class SampleBundleAuthorityResolver : IBundleAuthorityResolver
    {
        private readonly VerifiedProtocolVersion _protocol;
        private readonly WorkflowDefinition _workflow;
        private readonly ResearchEvent _event;

        public SampleBundleAuthorityResolver(
            VerifiedProtocolVersion protocol,
            WorkflowDefinition workflow,
            ResearchEvent @event)
        {
            _protocol = protocol;
            _workflow = workflow;
            _event = @event;
        }

        public VerifiedProtocolVersion ResolveProtocolVersion(string id) => id == _protocol.Version.Id ? _protocol : null!;
        public WorkflowDefinition ResolveWorkflowDefinition(string id) => id == _workflow.WorkflowId ? _workflow : null!;
        public ResearchEvent ResolveProvenanceEvent(string id) => id == _event.EventId.ToString() ? _event : null!;
    }

    private static WorkflowCompileInput BuildSampleWorkflowInput(VerifiedProtocolVersion protocolAuthority)
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
            TemplateDigest = WorkflowCompiler.ComputeLocalTemplateDigest(template)
        };

        return new WorkflowCompileInput(
            protocolAuthority,
            sealedTemplate,
            new Dictionary<string, CanonicalJsonValue>(StringComparer.Ordinal),
            new[]
            {
                new WorkflowSchemaRef("nexus.workflow-template", "1.0.0"),
                new WorkflowSchemaRef("nexus.workflow-definition", "1.1.0"),
                new WorkflowSchemaRef("nexus.review.decision", "1.0.0"),
                new WorkflowSchemaRef("nexus.workflow.artifact", "1.0.0")
            });
    }
}
