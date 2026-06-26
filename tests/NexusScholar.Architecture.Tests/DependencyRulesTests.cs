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
}
