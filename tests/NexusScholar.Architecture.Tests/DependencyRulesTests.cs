using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.AI;
using NexusScholar.AppServices;
using NexusScholar.Artifacts;
using NexusScholar.Avalonia.Blocks;
using NexusScholar.Avalonia.Blocks.SampleHost;
using NexusScholar.Bundles;
using NexusScholar.CorpusSnapshots;
using NexusScholar.Deduplication;
using NexusScholar.Desktop.Preview;
using NexusScholar.Extensibility;
using NexusScholar.FullText;
using NexusScholar.Kernel;
using NexusScholar.Protocol;
using NexusScholar.Provenance;
using NexusScholar.ResearchWorkspace;
using NexusScholar.Screening;
using NexusScholar.Screening.WorkflowExecution;
using NexusScholar.Search;
using NexusScholar.Shared;
using NexusScholar.UiContracts;
using NexusScholar.Workflow;
using NexusScholar.WorkflowExecution;
using NexusScholar.WorkflowExecution.Provenance;

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
            typeof(CorpusSnapshotService).Assembly,
            typeof(DeduplicationService).Assembly,
            typeof(ContentDigest).Assembly,
            typeof(ArtifactDescriptor).Assembly,
            typeof(ProtocolDraft).Assembly,
            typeof(WorkflowDefinition).Assembly,
            typeof(WorkflowExecutionJournal).Assembly,
            typeof(ResearchEvent).Assembly,
            typeof(WorkId).Assembly,
            typeof(SearchTrace).Assembly,
            typeof(ScreeningService).Assembly,
            typeof(FullTextInput).Assembly,
            typeof(ReviewBundleManifest).Assembly,
            typeof(ExtensionManifest).Assembly,
            typeof(AiTaskPolicy).Assembly,
            typeof(WorkspacePlan).Assembly,
            typeof(SearchDedupWorkspacePlanComposer).Assembly
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
    public void WorkflowExecution_project_depends_only_on_kernel_and_workflow_inside_nexus_domain()
    {
        var assembly = typeof(WorkflowExecutionJournal).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(WorkflowDefinition).Assembly.GetName().Name
        };
        var disallowed = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.WorkflowExecution may depend only on Kernel and Workflow. Found: {string.Join(", ", disallowed)}");
        Assert.IsFalse(
            typeof(WorkflowDefinition).Assembly.GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, assembly.GetName().Name, StringComparison.Ordinal)),
            "NexusScholar.Workflow must not depend on WorkflowExecution.");
    }

    [TestMethod]
    public void WorkflowExecution_provenance_bridge_has_only_accepted_inward_dependencies()
    {
        var assembly = typeof(WorkflowExecutionProvenanceProjector).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(ResearchEvent).Assembly.GetName().Name,
            typeof(WorkflowExecutionJournal).Assembly.GetName().Name
        };
        var disallowed = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(0, disallowed.Length,
            $"WorkflowExecution.Provenance has disallowed dependencies: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void Screening_workflow_execution_bridge_has_only_accepted_inward_dependencies()
    {
        var assembly = typeof(ScreeningWorkflowExecutionBridge).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(ScreeningConductJournal).Assembly.GetName().Name,
            typeof(WorkflowExecutionJournal).Assembly.GetName().Name,
            typeof(WorkflowDefinition).Assembly.GetName().Name
        };
        var disallowed = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(0, disallowed.Length,
            $"Screening.WorkflowExecution has disallowed dependencies: {string.Join(", ", disallowed)}");
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
    public void Bundle_project_depends_inward_on_authority_owners_only()
    {
        var bundleAssembly = typeof(ReviewBundleManifest).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(WorkId).Assembly.GetName().Name,
            "NexusScholar.Protocol",
            "NexusScholar.Workflow",
            "NexusScholar.Provenance"
        };
        var disallowed = bundleAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Bundles may depend only on accepted inward authority owners. Found: {string.Join(", ", disallowed)}");
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
    public void CorpusSnapshots_project_depends_only_on_kernel_and_deduplication_inside_nexus_domain()
    {
        var corpusSnapshotsAssembly = typeof(CorpusSnapshotService).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(DeduplicationService).Assembly.GetName().Name
        };
        var disallowed = corpusSnapshotsAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.CorpusSnapshots may depend only on Kernel and Deduplication. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void CorpusSnapshots_source_contains_no_host_storage_provider_or_model_symbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src", "NexusScholar.CorpusSnapshots");
        var source = string.Join(
            "\n",
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var forbidden = new[]
        {
            string.Concat("Http", "Client"),
            string.Concat("System.", "Net.", "Http"),
            string.Concat("File", "."),
            string.Concat("Directory", "."),
            "DbContext",
            string.Concat("Ava", "lonia"),
            "OpenAI",
            "Anthropic",
            "SemanticKernel",
            "ProviderSdk",
            "ProviderClient"
        };

        var matches = forbidden.Where(symbol => source.Contains(symbol, StringComparison.Ordinal)).ToArray();

        Assert.AreEqual(0, matches.Length, $"Forbidden CorpusSnapshots source symbols: {string.Join(", ", matches)}");
    }

    [TestMethod]
    public void Screening_project_depends_inward_on_kernel_deduplication_and_protocol()
    {
        var screeningAssembly = typeof(ScreeningService).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(DeduplicationService).Assembly.GetName().Name,
            "NexusScholar.Protocol"
        };
        var disallowed = screeningAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.Screening may depend only on Kernel, Deduplication, and Protocol. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void FullText_project_depends_inward_on_kernel_and_artifacts()
    {
        var fullTextAssembly = typeof(FullTextInput).Assembly;
        var kernelAssemblyName = typeof(IClock).Assembly.GetName().Name;
        var artifactsAssemblyName = typeof(NexusScholar.Artifacts.ArtifactDescriptor).Assembly.GetName().Name;
        var disallowed = fullTextAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !string.Equals(name, kernelAssemblyName, StringComparison.Ordinal) &&
                !string.Equals(name, artifactsAssemblyName, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.FullText may depend only on Kernel and Artifacts inside the domain. Found: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void AppServices_project_references_only_allowed_nexus_projects()
    {
        var appServicesAssembly = typeof(SearchDedupWorkspacePlanComposer).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(SearchTrace).Assembly.GetName().Name,
            typeof(DeduplicationService).Assembly.GetName().Name,
            typeof(CorpusSnapshotService).Assembly.GetName().Name,
            typeof(WorkspacePlan).Assembly.GetName().Name,
            typeof(WorkflowDefinition).Assembly.GetName().Name,
            typeof(WorkflowExecutionJournal).Assembly.GetName().Name,
            typeof(ScreeningConductJournal).Assembly.GetName().Name
        };
        var disallowed = appServicesAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.AppServices has disallowed Nexus dependencies: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void AppServices_source_contains_no_live_provider_or_host_symbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src", "NexusScholar.AppServices");
        var source = string.Join(
            "\n",
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var forbidden = new[]
        {
            string.Concat("Http", "Client"),
            string.Concat("System.", "Net.", "Http"),
            "DbContext",
            string.Concat("Ava", "lonia"),
            "OpenAI",
            "Anthropic",
            "SemanticKernel",
            "ProviderSdk",
            "ProviderClient",
            string.Concat("File", "."),
            string.Concat("Directory", ".")
        };

        var matches = forbidden
            .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(0, matches.Length, $"Forbidden AppServices source symbols: {string.Join(", ", matches)}");
    }

    [TestMethod]
    public void ResearchWorkspace_project_references_only_allowed_nexus_projects()
    {
        var researchWorkspaceAssembly = typeof(ResearchWorkspaceProject).Assembly;
        var allowed = new[]
        {
            typeof(IClock).Assembly.GetName().Name,
            typeof(SearchTrace).Assembly.GetName().Name,
            typeof(DeduplicationService).Assembly.GetName().Name,
            typeof(CorpusSnapshotService).Assembly.GetName().Name,
            typeof(ResearchEvent).Assembly.GetName().Name,
            typeof(SearchDedupWorkspacePlanComposer).Assembly.GetName().Name,
            typeof(WorkspacePlan).Assembly.GetName().Name,
            typeof(WorkId).Assembly.GetName().Name,
            typeof(WorkflowDefinition).Assembly.GetName().Name,
            typeof(WorkflowExecutionJournal).Assembly.GetName().Name,
            typeof(ScreeningConductJournal).Assembly.GetName().Name,
            typeof(ScreeningWorkflowExecutionBridge).Assembly.GetName().Name,
            typeof(ProtocolVersion).Assembly.GetName().Name
        };
        var disallowed = researchWorkspaceAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("NexusScholar.", StringComparison.Ordinal))
            .Where(name => !allowed.Contains(name, StringComparer.Ordinal))
            .ToArray();

        Assert.AreEqual(
            0,
            disallowed.Length,
            $"NexusScholar.ResearchWorkspace has disallowed Nexus dependencies: {string.Join(", ", disallowed)}");
    }

    [TestMethod]
    public void ResearchWorkspace_source_contains_no_ui_provider_persistence_cloud_or_model_symbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src", "NexusScholar.ResearchWorkspace");
        var source = string.Join(
            "\n",
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var forbidden = new[]
        {
            string.Concat("Http", "Client"),
            string.Concat("System.", "Net.", "Http"),
            "DbContext",
            string.Concat("Ava", "lonia"),
            "OpenAI",
            "Anthropic",
            "SemanticKernel",
            "ProviderSdk",
            "ProviderClient"
        };

        var matches = forbidden
            .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(0, matches.Length, $"Forbidden ResearchWorkspace source symbols: {string.Join(", ", matches)}");
    }

    [TestMethod]
    public void Core_domain_projects_do_not_reference_appservices_or_ui_contracts()
    {
        var appServicesAssemblyName = typeof(SearchDedupWorkspacePlanComposer).Assembly.GetName().Name;
        var uiContractsAssemblyName = typeof(WorkspacePlan).Assembly.GetName().Name;
        var coreAssemblies = new[]
        {
            typeof(IClock).Assembly,
            typeof(CorpusSnapshotService).Assembly,
            typeof(DeduplicationService).Assembly,
            typeof(ContentDigest).Assembly,
            typeof(ArtifactDescriptor).Assembly,
            typeof(ProtocolDraft).Assembly,
            typeof(WorkflowDefinition).Assembly,
            typeof(ResearchEvent).Assembly,
            typeof(WorkId).Assembly,
            typeof(SearchTrace).Assembly,
            typeof(ScreeningService).Assembly,
            typeof(FullTextInput).Assembly,
            typeof(ReviewBundleManifest).Assembly,
            typeof(ExtensionManifest).Assembly,
            typeof(AiTaskPolicy).Assembly,
            typeof(WorkflowExecutionJournal).Assembly,
            typeof(WorkflowExecutionProvenanceProjector).Assembly
        }.Distinct();

        foreach (var assembly in coreAssemblies)
        {
            var referencesUiContracts = assembly.GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, uiContractsAssemblyName, StringComparison.Ordinal));
            var referencesAppServices = assembly.GetReferencedAssemblies()
                .Any(reference => string.Equals(reference.Name, appServicesAssemblyName, StringComparison.Ordinal));

            Assert.IsFalse(referencesUiContracts, $"{assembly.GetName().Name} must not reference NexusScholar.UiContracts.");
            Assert.IsFalse(referencesAppServices, $"{assembly.GetName().Name} must not reference NexusScholar.AppServices.");
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
            typeof(FullTextInput).Assembly.GetName().Name,
            typeof(ReviewBundleManifest).Assembly.GetName().Name,
            typeof(ExtensionManifest).Assembly.GetName().Name,
            typeof(AiTaskPolicy).Assembly.GetName().Name,
            typeof(SearchDedupWorkspacePlanComposer).Assembly.GetName().Name
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
            typeof(FullTextInput).Assembly.GetName().Name,
            typeof(ReviewBundleManifest).Assembly.GetName().Name,
            typeof(ExtensionManifest).Assembly.GetName().Name,
            typeof(AiTaskPolicy).Assembly.GetName().Name,
            typeof(SearchDedupWorkspacePlanComposer).Assembly.GetName().Name
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
    public void Desktop_preview_references_researchworkspace_without_direct_core_domain_or_appservices_references()
    {
        var previewAssembly = typeof(DesktopPreviewViewModel).Assembly;
        var references = previewAssembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();
        var forbiddenDirectAssemblyNames = new[]
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
            typeof(FullTextInput).Assembly.GetName().Name,
            typeof(ReviewBundleManifest).Assembly.GetName().Name,
            typeof(ExtensionManifest).Assembly.GetName().Name,
            typeof(AiTaskPolicy).Assembly.GetName().Name,
            typeof(SearchDedupWorkspacePlanComposer).Assembly.GetName().Name
        }.Where(name => name is not null).ToArray();

        CollectionAssert.Contains(references, typeof(ResearchWorkspaceProject).Assembly.GetName().Name);
        Assert.IsTrue(references.Any(name => name.StartsWith("Avalonia", StringComparison.Ordinal)));

        foreach (var assemblyName in forbiddenDirectAssemblyNames)
        {
            CollectionAssert.DoesNotContain(references, assemblyName);
        }
    }

    [TestMethod]
    public void Desktop_preview_source_contains_no_provider_persistence_cloud_model_or_merge_execution_symbols()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "samples", "NexusScholar.Desktop.Preview");
        var source = string.Join(
            "\n",
            Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        var forbidden = new[]
        {
            string.Concat("Http", "Client"),
            string.Concat("System.", "Net.", "Http"),
            "DbContext",
            "OpenAI",
            "Anthropic",
            "SemanticKernel",
            "ProviderSdk",
            "ProviderClient",
            "AcceptMerge",
            "RejectMerge",
            "MarkUnresolved"
        };

        var matches = forbidden
            .Where(symbol => source.Contains(symbol, StringComparison.Ordinal))
            .ToArray();

        Assert.AreEqual(0, matches.Length, $"Forbidden Desktop Preview source symbols: {string.Join(", ", matches)}");
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NexusScholar.Core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
