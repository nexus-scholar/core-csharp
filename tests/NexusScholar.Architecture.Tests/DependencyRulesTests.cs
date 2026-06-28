using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AI;
using NexusScholar.Artifacts;
using NexusScholar.Avalonia.Blocks;
using NexusScholar.Avalonia.Blocks.SampleHost;
using NexusScholar.Bundles;
using NexusScholar.Deduplication;
using NexusScholar.Extensibility;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.Screening;
using NexusScholar.Search;
using NexusScholar.Shared;
using NexusScholar.UiContracts;
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
            typeof(DeduplicationService).Assembly,
            typeof(ContentDigest).Assembly,
            typeof(ArtifactDescriptor).Assembly,
            typeof(ProtocolDraft).Assembly,
            typeof(WorkflowDefinition).Assembly,
            typeof(ResearchEvent).Assembly,
            typeof(WorkId).Assembly,
            typeof(SearchTrace).Assembly,
            typeof(ScreeningService).Assembly,
            typeof(ReviewBundleManifest).Assembly,
            typeof(ExtensionManifest).Assembly,
            typeof(AiTaskPolicy).Assembly,
            typeof(WorkspacePlan).Assembly
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
            typeof(WorkflowDefinition).Assembly,
            typeof(WorkId).Assembly,
            typeof(SearchTrace).Assembly,
            typeof(ScreeningService).Assembly
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
    public void Provenance_project_depends_only_on_kernel_inside_nexus_domain()
    {
        var provenanceAssembly = typeof(ResearchEvent).Assembly;
        var kernelAssemblyName = typeof(IClock).Assembly.GetName().Name;
        var disallowed = provenanceAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, kernelAssemblyName, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Provenance must depend inward only on Kernel. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void Shared_project_depends_only_on_kernel_inside_nexus_domain()
    {
        var sharedAssembly = typeof(WorkId).Assembly;
        var kernelAssemblyName = typeof(IClock).Assembly.GetName().Name;
        var disallowed = sharedAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, kernelAssemblyName, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Shared must depend inward only on Kernel. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void Bundle_project_uses_field_level_bindings_without_outward_domain_references()
    {
        var bundleAssembly = typeof(ReviewBundleManifest).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(WorkId).Assembly.GetName().Name
        };
        var disallowed = bundleAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Bundles must not depend on Protocol, Workflow, Provenance, or Artifacts. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void Search_project_depends_only_on_kernel_and_shared_inside_nexus_domain()
    {
        var searchAssembly = typeof(SearchTrace).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(WorkId).Assembly.GetName().Name
        };
        var disallowed = searchAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Search must depend only on Kernel and Shared inside the domain. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void Deduplication_project_depends_only_on_kernel_shared_and_search_inside_nexus_domain()
    {
        var dedupAssembly = typeof(DeduplicationService).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(WorkId).Assembly.GetName().Name,
            typeof(SearchTrace).Assembly.GetName().Name
        };
        var disallowed = dedupAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Deduplication must depend only on Kernel, Shared, and Search inside the domain. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void Screening_project_depends_only_on_kernel_and_deduplication_inside_nexus_domain()
    {
        var screeningAssembly = typeof(ScreeningService).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(DeduplicationService).Assembly.GetName().Name
        };
        var disallowed = screeningAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Screening must depend only on Kernel and Deduplication inside the domain. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void Core_domain_projects_do_not_reference_ui_contracts()
    {
        var uiContractsAssemblyName = typeof(WorkspacePlan).Assembly.GetName().Name;
        var coreAssemblies = new[]
        {
            typeof(IClock).Assembly,
            typeof(DeduplicationService).Assembly,
            typeof(ContentDigest).Assembly,
            typeof(ArtifactDescriptor).Assembly,
            typeof(ProtocolDraft).Assembly,
            typeof(WorkflowDefinition).Assembly,
            typeof(ResearchEvent).Assembly,
            typeof(WorkId).Assembly,
            typeof(SearchTrace).Assembly,
            typeof(ScreeningService).Assembly,
            typeof(ReviewBundleManifest).Assembly,
            typeof(ExtensionManifest).Assembly,
            typeof(AiTaskPolicy).Assembly
        }.Distinct();

        foreach (var assembly in coreAssemblies)
        {
            var referencesUiContracts = assembly.GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, uiContractsAssemblyName, StringComparison.Ordinal));

            Assert.IsFalse(referencesUiContracts, $"{assembly.GetName().Name} must not reference NexusScholar.UiContracts.");
        }
    }

    [TestMethod]
    public void Avalonia_blocks_references_ui_contracts_without_referencing_core_domain_projects()
    {
        var rendererAssembly = typeof(WorkspacePlanView).Assembly;
        var references = rendererAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
        var coreAssemblyNames = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(DeduplicationService).Assembly.GetName().Name,
            typeof(ContentDigest).Assembly.GetName().Name,
            typeof(ArtifactDescriptor).Assembly.GetName().Name,
            typeof(ProtocolDraft).Assembly.GetName().Name,
            typeof(WorkflowDefinition).Assembly.GetName().Name,
            typeof(ResearchEvent).Assembly.GetName().Name,
            typeof(WorkId).Assembly.GetName().Name,
            typeof(SearchTrace).Assembly.GetName().Name,
            typeof(ScreeningService).Assembly.GetName().Name,
            typeof(ReviewBundleManifest).Assembly.GetName().Name,
            typeof(ExtensionManifest).Assembly.GetName().Name,
            typeof(AiTaskPolicy).Assembly.GetName().Name
        }.Where(name => name is not null).ToArray();

        CollectionAssert.Contains(references, typeof(WorkspacePlan).Assembly.GetName().Name);
        Assert.IsTrue(references.Any(name => name.StartsWith("Avalonia", StringComparison.Ordinal)));

        foreach (var coreAssemblyName in coreAssemblyNames)
        {
            CollectionAssert.DoesNotContain(references, coreAssemblyName);
        }
    }

    [TestMethod]
    public void Avalonia_sample_host_references_renderer_and_ui_contracts_without_referencing_core_domain_projects()
    {
        var hostAssembly = typeof(SampleWorkspaceLoader).Assembly;
        var references = hostAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
        var coreAssemblyNames = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(DeduplicationService).Assembly.GetName().Name,
            typeof(ContentDigest).Assembly.GetName().Name,
            typeof(ArtifactDescriptor).Assembly.GetName().Name,
            typeof(ProtocolDraft).Assembly.GetName().Name,
            typeof(WorkflowDefinition).Assembly.GetName().Name,
            typeof(ResearchEvent).Assembly.GetName().Name,
            typeof(WorkId).Assembly.GetName().Name,
            typeof(SearchTrace).Assembly.GetName().Name,
            typeof(ScreeningService).Assembly.GetName().Name,
            typeof(ReviewBundleManifest).Assembly.GetName().Name,
            typeof(ExtensionManifest).Assembly.GetName().Name,
            typeof(AiTaskPolicy).Assembly.GetName().Name
        }.Where(name => name is not null).ToArray();

        CollectionAssert.Contains(references, typeof(WorkspacePlanView).Assembly.GetName().Name);
        CollectionAssert.Contains(references, typeof(WorkspacePlan).Assembly.GetName().Name);
        Assert.IsTrue(references.Any(name => name.StartsWith("Avalonia", StringComparison.Ordinal)));

        foreach (var coreAssemblyName in coreAssemblyNames)
        {
            CollectionAssert.DoesNotContain(references, coreAssemblyName);
        }
    }

    [TestMethod]
    public void Provenance_event_digest_scope_is_kernel_level()
    {
        var record = ResearchEventFactory.Create(
            new SequenceIdGenerator(),
            new FixedClock(),
            new ProvenanceActivity("protocol-approved", "Protocol approved", false, false, false),
            new ProvenanceEntityRef("protocol-version", "protocol-version-1"),
            new ProvenanceAgent("researcher-1", "human"));

        Assert.AreEqual(DigestScope.ProvenanceEvent, record.ToDigestEnvelope().Scope);
        Assert.AreEqual(record.ToDigestEnvelope().ComputeDigest(), record.EventDigest);
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
