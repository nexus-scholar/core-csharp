using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Avalonia.Blocks.SampleHost;
using NexusScholar.UiContracts;

namespace NexusScholar.Avalonia.Blocks.SampleHost.Tests;

[TestClass]
public sealed class SampleWorkspaceLoaderTests
{
    [TestMethod]
    public void Expected_sample_files_are_found_and_loaded()
    {
        var samples = SampleWorkspaceLoader.LoadFromRepositoryRoot(FindRepositoryRoot());

        CollectionAssert.AreEqual(
            SampleWorkspaceLoader.ExpectedSampleFileNames.ToArray(),
            samples.Select(sample => sample.FileName).ToArray());
    }

    [TestMethod]
    public void Loaded_samples_are_workspace_plans()
    {
        var samples = SampleWorkspaceLoader.LoadFromRepositoryRoot(FindRepositoryRoot());

        Assert.AreEqual(3, samples.Count);
        foreach (var sample in samples)
        {
            Assert.IsInstanceOfType<WorkspacePlan>(sample.Plan);
            Assert.IsTrue(sample.Plan.WorkspaceId.StartsWith("sample.workspace.", StringComparison.Ordinal));
            Assert.IsTrue(sample.Plan.Blocks.Count > 0);
            Assert.IsTrue(sample.Plan.Description?.Contains("not Core authority", StringComparison.OrdinalIgnoreCase) == true);
        }
    }

    [TestMethod]
    public void Sample_host_references_renderer_and_ui_contracts_without_core_domain_projects()
    {
        var references = typeof(SampleWorkspaceLoader).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        CollectionAssert.Contains(references, "NexusScholar.Avalonia.Blocks");
        CollectionAssert.Contains(references, "NexusScholar.UiContracts");
        Assert.IsTrue(references.Any(reference => reference.StartsWith("Avalonia", StringComparison.Ordinal)));

        CollectionAssert.DoesNotContain(references, "NexusScholar.Kernel");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Protocol");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Workflow");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Artifacts");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Provenance");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Shared");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Search");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Deduplication");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Screening");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Bundles");
        CollectionAssert.DoesNotContain(references, "NexusScholar.Extensibility");
        CollectionAssert.DoesNotContain(references, "NexusScholar.AI");
    }

    private static string FindRepositoryRoot()
    {
        return SampleWorkspaceLoader.FindRepositoryRoot(AppContext.BaseDirectory);
    }
}
