using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class WorkflowFixtureTests
{
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
        "missing-schema-id",
        "unknown-schema-id",
        "missing-schema-version",
        "undeclared-produced-artifact",
        "unknown-producing-node",
        "unknown-capability-reference",
        "missing-required-input",
        "conduct-input-from-compile-parameter",
        "invalid-protocol-status",
        "stale-protocol-digest",
        "stale-template-digest",
        "workflow-id-mismatch",
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
            Assert.AreEqual("gate-4-v1", root.GetProperty("generatorVersion").GetString(), Path.GetFileName(path));
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
}
