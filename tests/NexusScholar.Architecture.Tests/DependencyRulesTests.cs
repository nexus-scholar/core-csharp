using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AI;
using NexusScholar.Artifacts;
using NexusScholar.Bundles;
using NexusScholar.Extensibility;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Workflow;

namespace NexusScholar.Architecture.Tests;

[TestClass]
public sealed class DependencyRulesTests
{
    private static readonly string[] ForbiddenPrefixes =
    {
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Avalonia",
        "Amazon.",
        "AWSSDK.",
        "Azure.",
        "Google.",
        "OpenAI",
        "Anthropic",
        "Microsoft.SemanticKernel",
        "System.Net.Http"
    };

    [TestMethod]
    public void Domain_projects_do_not_reference_host_or_provider_frameworks()
    {
        var assemblies = new[]
        {
            typeof(IClock).Assembly,
            typeof(ContentDigest).Assembly,
            typeof(ArtifactDescriptor).Assembly,
            typeof(ProtocolDraft).Assembly,
            typeof(WorkflowDefinition).Assembly,
            typeof(ResearchEvent).Assembly,
            typeof(ReviewBundleManifest).Assembly,
            typeof(ExtensionManifest).Assembly,
            typeof(AiTaskPolicy).Assembly
        }.Distinct();

        foreach (var assembly in assemblies)
        {
            var forbidden = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
                .ToArray();

            Assert.AreEqual(
                0,
                forbidden.Length,
                $"{assembly.GetName().Name} has forbidden references: {string.Join(", ", forbidden)}");
        }
    }

    [TestMethod]
    public void Digest_primitives_are_kernel_level_not_artifact_level()
    {
        var kernelAssembly = typeof(IClock).Assembly;
        var artifactsAssembly = typeof(ArtifactDescriptor).Assembly;

        Assert.AreSame(kernelAssembly, typeof(ContentDigest).Assembly);
        Assert.AreSame(kernelAssembly, typeof(DigestAlgorithm).Assembly);
        Assert.AreSame(kernelAssembly, typeof(DigestScope).Assembly);
        Assert.AreSame(kernelAssembly, typeof(DigestEnvelope).Assembly);
        Assert.AreNotSame(artifactsAssembly, typeof(ContentDigest).Assembly);
    }

    [TestMethod]
    public void Digest_consumers_do_not_depend_on_artifacts_for_digest_vocabulary()
    {
        var artifactAssemblyName = typeof(ArtifactDescriptor).Assembly.GetName().Name;
        var digestConsumerAssemblies = new[]
        {
            typeof(ProtocolDraft).Assembly,
            typeof(ResearchEvent).Assembly,
            typeof(ReviewBundleManifest).Assembly,
            typeof(AiTaskPolicy).Assembly,
            typeof(WorkflowDefinition).Assembly
        };

        foreach (var assembly in digestConsumerAssemblies)
        {
            var referencesArtifacts = assembly.GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, artifactAssemblyName, StringComparison.Ordinal));

            Assert.IsFalse(referencesArtifacts, $"{assembly.GetName().Name} must not depend on NexusScholar.Artifacts for digest vocabulary.");
        }
    }

    [TestMethod]
    public void Protocol_project_depends_only_on_kernel_inside_nexus_domain()
    {
        var protocolAssembly = typeof(ProtocolDraft).Assembly;
        var kernelAssemblyName = typeof(IClock).Assembly.GetName().Name;
        var disallowed = protocolAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, kernelAssemblyName, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Protocol must depend inward only on Kernel. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void Protocol_digest_records_use_kernel_digest_scopes()
    {
        var policy = ApprovalPolicy.ExplicitCustomSingleResearcher();
        var ids = new SequenceIdGenerator();
        var clock = new FixedClock();
        var actor = ProtocolActor.Human("researcher-1");
        var draft = ProtocolDraft.Create(
            ids,
            "project-1",
            new ProtocolTemplate("template", "1.0.0", ContentDigest.Sha256Utf8("template")),
            new ProtocolIntent("subject", "goal"),
            new CanonicalJsonObject(),
            new[]
            {
                new RequiredDecisionDefinition(
                    "review-type",
                    "Review type",
                    "Select review type.",
                    new CanonicalJsonObject().Add("type", "string"),
                    "protocol-approval",
                    "gate",
                    "review-type",
                    false)
            },
            actor,
            clock);

        draft.RecordDecision(ids, "review-type", CanonicalJsonValue.From("scoping-review"), actor, clock);
        var version = draft.CreateApprovalCandidate(ids, policy);
        var approval = ProtocolApproval.Create(ids, version, policy, actor, clock, version.ContentDigest);

        Assert.AreEqual(DigestScope.ProtocolContent, version.ToProtocolContentDigestEnvelope().Scope);
        Assert.AreEqual(DigestScope.ApprovalRecord, approval.ToApprovalRecordDigestEnvelope().Scope);
    }

    private sealed class SequenceIdGenerator : IIdGenerator
    {
        private int _next = 1;

        public Guid NewId() => new(_next++, 0, 0, new byte[8]);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 25, 12, 0, 0, TimeSpan.Zero);
    }
}
