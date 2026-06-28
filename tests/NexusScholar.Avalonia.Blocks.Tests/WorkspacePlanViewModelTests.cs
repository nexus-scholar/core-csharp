using Avalonia.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Avalonia.Blocks;
using NexusScholar.UiContracts;

namespace NexusScholar.Avalonia.Blocks.Tests;

[TestClass]
public sealed class WorkspacePlanViewModelTests
{
    [TestMethod]
    public void All_sample_plans_prepare_for_rendering()
    {
        foreach (var path in SamplePaths())
        {
            var model = WorkspacePlanViewModel.FromJson(File.ReadAllText(path));

            Assert.IsFalse(string.IsNullOrWhiteSpace(model.WorkspaceId), path);
            Assert.IsTrue(model.Blocks.Count > 0, path);
            Assert.AreEqual("Sample plan: non-authoritative renderer input.", model.AuthorityStatus, path);
        }
    }

    [TestMethod]
    public void View_model_preserves_sample_block_order()
    {
        var model = LoadSample("dedup-review.sample.json");

        CollectionAssert.AreEqual(
            new[]
            {
                "block.dedup.cluster.001",
                "block.dedup.comparison.001",
                "block.dedup.merge-gate.001"
            },
            model.Blocks.Select(block => block.BlockId).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, model.Blocks.Select(block => block.Order).ToArray());
    }

    [TestMethod]
    public void View_model_exposes_evidence_validation_actions_and_payload()
    {
        var model = LoadSample("dedup-review.sample.json");
        var block = model.Blocks[0];

        Assert.IsTrue(block.EvidenceRefs.Count > 0);
        Assert.IsTrue(block.ValidationRefs.Count > 0);
        Assert.IsTrue(block.Actions.Count > 0);
        Assert.IsTrue(block.Payload.HasPayload);
        Assert.AreEqual("ReviewRequired", block.Severity);
        Assert.AreEqual("Sample", block.SourceKind);
    }

    [TestMethod]
    public void Action_view_model_invokes_placeholder_callback_without_core_command()
    {
        BlockActionInvocation? captured = null;
        var model = LoadSample("dedup-review.sample.json", invocation => captured = invocation);

        var action = model.Blocks.SelectMany(block => block.Actions)
            .First(candidate => candidate.ActionId == "accept-merge");
        action.Invoke();

        Assert.IsNotNull(captured);
        Assert.AreEqual("sample.workspace.dedup-review", captured.WorkspaceId);
        Assert.AreEqual("block.dedup.merge-gate.001", captured.BlockId);
        Assert.AreEqual("accept-merge", captured.ActionId);
        Assert.IsTrue(captured.RequiresHumanConfirmation);
        Assert.IsFalse(captured.IsDestructive);
    }

    [TestMethod]
    public void Renderer_project_references_ui_contracts_and_not_core_domain_projects()
    {
        var references = typeof(WorkspacePlanViewModel).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.Contains("NexusScholar.UiContracts", references);
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

    [TestMethod]
    public void Ui_contracts_remains_avalonia_free()
    {
        var references = typeof(WorkspacePlan).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

        Assert.IsFalse(references.Any(reference => reference.StartsWith("Avalonia", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Workspace_text_helper_uses_valid_normal_font_weight_by_default()
    {
        var text = WorkspacePlanView.Text("plain renderer text");

        Assert.AreEqual(FontWeight.Normal, text.FontWeight);
    }

    private static WorkspacePlanViewModel LoadSample(string fileName, BlockActionCallback? actionCallback = null)
    {
        return WorkspacePlanViewModel.FromJson(
            File.ReadAllText(Path.Combine(SampleDirectory(), fileName)),
            actionCallback);
    }

    private static IEnumerable<string> SamplePaths()
    {
        return Directory.EnumerateFiles(SampleDirectory(), "*.sample.json")
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string SampleDirectory()
    {
        return Path.Combine(FindRepositoryRoot(), "samples", "block-plans");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NexusScholar.Core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
