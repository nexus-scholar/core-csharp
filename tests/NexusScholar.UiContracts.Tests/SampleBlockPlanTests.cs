using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.UiContracts;

namespace NexusScholar.UiContracts.Tests;

[TestClass]
public sealed class SampleBlockPlanTests
{
    private static readonly string[] RendererSpecificTerms =
    {
        string.Concat("Ava", "lonia"),
        string.Concat("X", "AML"),
        string.Concat("MA", "UI"),
        string.Concat("CSS"),
        string.Concat("HTML"),
        string.Concat("DOM"),
        string.Concat("mobile-view"),
        string.Concat("web-route")
    };

    [TestMethod]
    public void Import_warning_sample_loads_as_workspace_plan()
    {
        var plan = LoadSample("import-warning.sample.json");

        Assert.AreEqual("sample.workspace.import-warning", plan.WorkspaceId);
        Assert.IsTrue(plan.Blocks.Any(block => block.Kind == KnownBlockKinds.ImportWarningSummary));
        Assert.IsTrue(plan.Blocks.Any(block => block.ValidationRefs.Any(validation => validation.Code.Contains("parser", StringComparison.Ordinal) || validation.Code.Contains("stable-identifier", StringComparison.Ordinal))));
    }

    [TestMethod]
    public void Dedup_review_sample_loads_as_workspace_plan()
    {
        var plan = LoadSample("dedup-review.sample.json");

        Assert.AreEqual("sample.workspace.dedup-review", plan.WorkspaceId);
        Assert.IsTrue(plan.Blocks.Any(block => block.Kind == KnownBlockKinds.DedupCandidateCluster && block.Severity == BlockSeverity.ReviewRequired));
        Assert.IsTrue(plan.Blocks.Any(block => block.Kind == KnownBlockKinds.HumanGateMergeDecision));
        Assert.IsTrue(plan.Blocks.SelectMany(block => block.Actions).Any(action => action.Kind == BlockActionKind.AcceptMerge));
    }

    [TestMethod]
    public void Bundle_verification_sample_loads_as_workspace_plan()
    {
        var plan = LoadSample("bundle-verification.sample.json");

        Assert.AreEqual("sample.workspace.bundle-verification", plan.WorkspaceId);
        Assert.IsTrue(plan.Blocks.Any(block => block.Kind == "nexus.block.bundle.verification-summary"));
        Assert.IsTrue(plan.Blocks.SelectMany(block => block.Actions).Any(action => action.Kind == BlockActionKind.ExportBundle));
    }

    [TestMethod]
    public void Sample_workspace_block_order_is_preserved()
    {
        var plan = LoadSample("dedup-review.sample.json");

        CollectionAssert.AreEqual(
            new[]
            {
                "block.dedup.cluster.001",
                "block.dedup.comparison.001",
                "block.dedup.merge-gate.001"
            },
            plan.Blocks.Select(block => block.BlockId).ToArray());
    }

    [TestMethod]
    public void Every_sample_block_action_and_evidence_ref_has_required_text()
    {
        foreach (var plan in LoadAllSamples())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(plan.WorkspaceId), plan.WorkspaceId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(plan.Title), plan.WorkspaceId);
            AssertNonAuthoritative(plan);

            foreach (var block in plan.Blocks)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(block.BlockId), plan.WorkspaceId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(block.Kind), block.BlockId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(block.Title), block.BlockId);

                foreach (var action in block.Actions)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(action.ActionId), block.BlockId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(action.Label), action.ActionId);
                }

                foreach (var evidence in block.EvidenceRefs)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(evidence.Kind), block.BlockId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(evidence.Value), evidence.Kind);
                }
            }
        }
    }

    [TestMethod]
    public void Every_sample_payload_is_valid_object_root_json()
    {
        foreach (var block in LoadAllSamples().SelectMany(plan => plan.Blocks))
        {
            if (block.PayloadJson is null)
            {
                continue;
            }

            using var document = JsonDocument.Parse(block.PayloadJson);
            Assert.AreEqual(JsonValueKind.Object, document.RootElement.ValueKind, block.BlockId);
            Assert.IsTrue(document.RootElement.TryGetProperty("sample", out var sampleFlag) && sampleFlag.GetBoolean(), block.BlockId);
            Assert.IsTrue(document.RootElement.TryGetProperty("nonAuthoritative", out var nonAuthority) && nonAuthority.GetBoolean(), block.BlockId);
        }
    }

    [TestMethod]
    public void Samples_remain_renderer_neutral()
    {
        foreach (var samplePath in Directory.EnumerateFiles(SampleDirectory(), "*.sample.json"))
        {
            var content = File.ReadAllText(samplePath);
            foreach (var term in RendererSpecificTerms)
            {
                Assert.IsFalse(content.Contains(term, StringComparison.OrdinalIgnoreCase), $"{Path.GetFileName(samplePath)} contains renderer-specific term {term}.");
            }
        }
    }

    private static IReadOnlyList<WorkspacePlan> LoadAllSamples()
    {
        return Directory.EnumerateFiles(SampleDirectory(), "*.sample.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => LoadSample(Path.GetFileName(path)))
            .ToArray();
    }

    private static WorkspacePlan LoadSample(string fileName)
    {
        var path = Path.Combine(SampleDirectory(), fileName);
        var json = File.ReadAllText(path);
        var plan = JsonSerializer.Deserialize<WorkspacePlan>(json, UiContractJson.SerializerOptions);
        Assert.IsNotNull(plan);

        return plan;
    }

    private static void AssertNonAuthoritative(WorkspacePlan plan)
    {
        Assert.IsTrue(plan.Description?.Contains("not Core authority", StringComparison.OrdinalIgnoreCase) == true, plan.WorkspaceId);
        Assert.IsTrue(plan.Description?.Contains("not an ADR", StringComparison.OrdinalIgnoreCase) == true, plan.WorkspaceId);
        Assert.IsTrue(plan.Description?.Contains("not a scientific fixture", StringComparison.OrdinalIgnoreCase) == true, plan.WorkspaceId);
        Assert.IsTrue(plan.Description?.Contains("not a PHP compatibility fixture", StringComparison.OrdinalIgnoreCase) == true, plan.WorkspaceId);
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
