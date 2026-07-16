using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class Fe07FixtureTests
{
    [TestMethod]
    public void Fe07_catalog_is_complete_local_and_claim_bounded()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "fe07", "catalog.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.AreEqual("nexus.fe07.fixture-catalog.v1", root.GetProperty("schema").GetString());
        Assert.AreEqual("local-fe07-contract", root.GetProperty("sourceKind").GetString());
        Assert.AreEqual("none", root.GetProperty("compatibilityClaim").GetString());
        Assert.AreEqual(7, root.GetProperty("schemas").GetArrayLength());
        Assert.AreEqual(5, root.GetProperty("positiveCases").GetArrayLength());
        Assert.AreEqual(8, root.GetProperty("negativeCases").GetArrayLength());
        CollectionAssert.Contains(root.GetProperty("nonClaims").EnumerateArray().Select(item => item.GetString()).ToArray(),
            "no-statistical-execution-or-correctness");
    }
}
