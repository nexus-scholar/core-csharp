using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class ProtocolFixtureTests
{
    private static readonly string[] PositiveFixtures =
    {
        "protocol-draft-valid-v1.json",
        "protocol-approved-single-v1.json",
        "protocol-approved-dual-v1.json",
        "protocol-amended-v1.json",
        "protocol-waiver-valid-v1.json",
        "protocol-deviation-valid-v1.json"
    };

    private static readonly string[] RequiredNegativeCategories =
    {
        "missing-required-decision",
        "blocking-unresolved-decision",
        "duplicate-decision",
        "post-approval-mutation",
        "unauthorized-approval",
        "stale-content-digest",
        "invalid-amendment",
        "invalid-waiver",
        "invalid-deviation",
        "same-actor-dual-approval",
        "automation-cannot-approve"
    };

    [TestMethod]
    public void Minimal_protocol_fixture_remains_discovery_only()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "protocol-minimal.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.AreEqual("nexus.review-protocol/v1", root.GetProperty("schema").GetString());
        Assert.IsTrue(root.GetProperty("subject").GetString()?.Length > 0);
        Assert.AreEqual(2, root.GetProperty("required_decisions").GetArrayLength());
        Assert.AreEqual("scoping-review", root.GetProperty("decisions").GetProperty("review-type").GetString());
    }

    [TestMethod]
    public void Gate_3_protocol_fixtures_have_required_metadata()
    {
        foreach (var path in ProtocolFixturePaths())
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            Assert.AreEqual("local-gate-3-contract", root.GetProperty("sourceKind").GetString(), Path.GetFileName(path));
            Assert.AreEqual("gate-3-v1", root.GetProperty("generatorVersion").GetString(), Path.GetFileName(path));
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0003-protocol-record-contract.md", StringComparison.Ordinal)));
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0004-protocol-approval-semantics.md", StringComparison.Ordinal)));
            _ = ContentDigest.Parse(root.GetProperty("inputDigest").GetString()!);
            _ = ContentDigest.Parse(root.GetProperty("outputDigest").GetString()!);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(rule =>
                string.Equals(rule.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void Gate_3_protocol_fixtures_have_replayable_case_digests()
    {
        foreach (var path in ProtocolFixturePaths())
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var serializedCase = JsonSerializer.Serialize(
                root.GetProperty("case"),
                new JsonSerializerOptions
                {
                    WriteIndented = false
                });
            var digest = ContentDigest.Sha256Utf8(serializedCase);

            Assert.AreEqual(
                root.GetProperty("inputDigest").GetString(),
                digest.ToString(),
                Path.GetFileName(path));
            Assert.AreEqual(
                root.GetProperty("outputDigest").GetString(),
                digest.ToString(),
                Path.GetFileName(path));
        }
    }

    [TestMethod]
    public void Gate_3_positive_fixture_pack_is_present()
    {
        var names = ProtocolFixturePaths()
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fixture in PositiveFixtures)
        {
            Assert.IsTrue(names.Contains(fixture), $"Missing Gate 3 protocol fixture '{fixture}'.");
        }
    }

    [TestMethod]
    public void Approved_protocol_fixtures_separate_content_and_approval_digest_scopes()
    {
        foreach (var fixture in new[] { "protocol-approved-single-v1.json", "protocol-approved-dual-v1.json" })
        {
            var root = LoadProtocolFixture(fixture);
            var version = root.GetProperty("case").GetProperty("protocol_version");

            Assert.AreEqual("protocol-content", version.GetProperty("content_digest").GetProperty("scope").GetString());
            Assert.AreEqual("approval-record", version.GetProperty("approval_records")[0].GetProperty("approval_record_digest").GetProperty("scope").GetString());
            Assert.IsFalse(version.GetProperty("digest_material_excludes").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "protocol-content", StringComparison.Ordinal)));
            Assert.IsTrue(version.GetProperty("digest_material_excludes").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "approval_records", StringComparison.Ordinal)));
        }
    }

    [TestMethod]
    public void Negative_protocol_fixtures_cover_required_error_categories()
    {
        var categories = ProtocolFixturePaths()
            .Select(Load)
            .Where(root => root.GetProperty("case").TryGetProperty("negative", out var negative) && negative.GetBoolean())
            .Select(root => root.GetProperty("case").GetProperty("errorCategory").GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var category in RequiredNegativeCategories)
        {
            Assert.IsTrue(categories.Contains(category), $"Missing negative fixture for '{category}'.");
        }
    }

    [TestMethod]
    public void Key_value_digest_material_is_explicitly_rejected_by_fixture()
    {
        var root = LoadProtocolFixture("protocol-invalid-key-value-digest-material-v1.json");
        var rejected = root.GetProperty("case").GetProperty("rejectedDigestMaterial");

        Assert.AreEqual("key=value-lines", rejected.GetProperty("format").GetString());
        Assert.AreEqual("stale-content-digest", root.GetProperty("case").GetProperty("errorCategory").GetString());
    }

    private static JsonElement LoadProtocolFixture(string filename)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "protocol", filename);
        return Load(path);
    }

    private static JsonElement Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static string[] ProtocolFixturePaths()
    {
        return Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures", "protocol"), "*.json");
    }
}
