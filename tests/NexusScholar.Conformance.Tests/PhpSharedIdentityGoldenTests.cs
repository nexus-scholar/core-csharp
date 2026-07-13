using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Shared;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class PhpSharedIdentityGoldenTests
{
    private const string FixtureSetId = "php-shared-identity-v1";
    [TestMethod]
    public void Manifest_binds_pinned_source_and_exact_fixture_bytes()
    {
        using var manifest = Load("manifest.json");
        using var sourceLock = JsonDocument.Parse(File.ReadAllBytes(SourceLockPath()));
        var root = manifest.RootElement;
        var phpReference = sourceLock.RootElement.GetProperty("php_reference");

        Assert.AreEqual(FixtureSetId, root.GetProperty("fixtureSetId").GetString());
        Assert.AreEqual("pinned-php-observable-behavior", root.GetProperty("sourceKind").GetString());
        Assert.AreEqual(phpReference.GetProperty("repository").GetString(), root.GetProperty("sourceRepository").GetString());
        Assert.AreEqual(phpReference.GetProperty("commit").GetString(), root.GetProperty("sourceCommit").GetString());
        Assert.AreEqual("shared-identity-v1", root.GetProperty("generatorVersion").GetString());
        Assert.AreEqual(DigestFixture("input.json"), root.GetProperty("inputDigest").GetString());
        Assert.AreEqual(DigestFixture("expected.json"), root.GetProperty("outputDigest").GetString());
        Assert.AreEqual(DigestFile(SourceLockPath()), root.GetProperty("sourceLockDigest").GetString());
        Assert.AreEqual(DigestFixture("comparison.json"), root.GetProperty("classificationDigest").GetString());
        Assert.AreEqual(0, root.GetProperty("ignoredNondeterminism").GetArrayLength());
    }

    [TestMethod]
    public void Every_php_case_has_one_reviewed_classification()
    {
        using var expected = Load("expected.json");
        using var comparison = Load("comparison.json");

        var caseIds = expected.RootElement.GetProperty("cases").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var classifications = comparison.RootElement.GetProperty("classifications").EnumerateArray().ToArray();
        var classifiedIds = classifications
            .Select(item => item.GetProperty("caseId").GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(caseIds, classifiedIds);
        Assert.AreEqual(classifiedIds.Length, classifiedIds.Distinct(StringComparer.Ordinal).Count());
        foreach (var classification in classifications)
        {
            var value = classification.GetProperty("classification").GetString();
            CollectionAssert.Contains(
                new[]
                {
                    "equivalent_serialization",
                    "intentional_change",
                    "php_defect",
                    "csharp_defect",
                    "unresolved_specification_conflict"
                },
                value);
            Assert.AreNotEqual("csharp_defect", value, "H25 cannot close with a known C# defect.");
            Assert.AreNotEqual("unresolved_specification_conflict", value, "H25 cannot close with an unresolved specification conflict.");
            Assert.IsTrue(classification.GetProperty("authorityRefs").GetArrayLength() > 0);
        }
    }

    [TestMethod]
    public void Equivalent_php_cases_replay_with_identical_semantics_in_csharp()
    {
        using var input = Load("input.json");
        using var expected = Load("expected.json");
        using var comparison = Load("comparison.json");

        var inputs = input.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.Clone(), StringComparer.Ordinal);
        var expectedResults = expected.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("id").GetString()!,
                item => JsonNode.Parse(item.GetProperty("result").GetRawText())!,
                StringComparer.Ordinal);
        var equivalentCases = comparison.RootElement.GetProperty("classifications").EnumerateArray()
            .Where(item => item.GetProperty("classification").GetString() == "equivalent_serialization")
            .Select(item => item.Clone());

        foreach (var classification in equivalentCases)
        {
            var caseId = classification.GetProperty("caseId").GetString()!;
            var actual = Replay(inputs[caseId]);
            var comparisonRule = classification.TryGetProperty("comparisonRule", out var rule)
                ? rule.GetString()
                : "exact";
            AssertEquivalent(caseId, comparisonRule!, expectedResults[caseId], actual);
        }
    }

    [TestMethod]
    public void Intentional_changes_match_adr_0007_boundaries()
    {
        var parseException = Assert.ThrowsExactly<SharedIdentityRuleException>(
            () => WorkId.Parse("doi:arxiv:2301.00001"));
        Assert.AreEqual(SharedIdentityErrorCodes.InvalidWorkId, parseException.Category);

        var blankException = Assert.ThrowsExactly<SharedIdentityRuleException>(
            () => WorkId.From("doi", "doi:"));
        Assert.AreEqual(SharedIdentityErrorCodes.BlankWorkIdValue, blankException.Category);

        var work = ScholarlyWork.Identified(
            "Same instance",
            WorkIdSet.From(WorkId.From("doi", "10.1000/unsafe")),
            "test");
        var unvalidated = CorpusSlice.FromUnvalidatedCandidates(new[] { work, work });
        Assert.AreEqual(2, unvalidated.Works.Count);

        using var expected = Load("expected.json");
        Assert.AreEqual("doi:arxiv:2301.00001", Result(expected, "parse-multiple-separators").GetProperty("normalized").GetString());
        Assert.AreEqual("doi:", Result(expected, "blank-direct-constructor").GetProperty("normalized").GetString());
        Assert.AreEqual(1, Result(expected, "unsafe-same-instance").GetProperty("count").GetInt32());
    }

    private static JsonObject Replay(JsonElement fixtureCase)
    {
        return fixtureCase.GetProperty("operation").GetString() switch
        {
            "normalize-identifiers" => new JsonObject
            {
                ["normalized"] = new JsonArray(ReadIds(fixtureCase.GetProperty("identifiers"))
                    .Select(id => JsonValue.Create(id.ToString())).ToArray<JsonNode?>())
            },
            "primary-identifier" => new JsonObject
            {
                ["primary"] = ReadIdSet(fixtureCase.GetProperty("identifiers")).Primary?.ToString()
            },
            "identifier-overlap" => new JsonObject
            {
                ["overlap"] = ReadIdSet(fixtureCase.GetProperty("left"))
                    .HasOverlapWith(ReadIdSet(fixtureCase.GetProperty("right")))
            },
            "merge-identifier-sets" => IdSetResult(
                ReadIdSet(fixtureCase.GetProperty("left")).Merge(ReadIdSet(fixtureCase.GetProperty("right")))),
            "merge-works" => MergeWorks(fixtureCase),
            "dedupe-corpus" => DedupeCorpus(fixtureCase),
            "no-id-candidates" => NoIdCandidates(fixtureCase),
            "title-lookup" => TitleLookup(fixtureCase),
            _ => throw new AssertFailedException($"Unsupported equivalent operation '{fixtureCase.GetProperty("operation").GetString()}'.")
        };
    }

    private static void AssertEquivalent(string caseId, string comparisonRule, JsonNode expected, JsonNode actual)
    {
        if (comparisonRule == "unordered_identifier_set")
        {
            var expectedIds = expected["ids"]!.AsArray().Select(value => value!.GetValue<string>())
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var actualIds = actual["ids"]!.AsArray().Select(value => value!.GetValue<string>())
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expectedIds, actualIds, caseId);
            return;
        }

        Assert.AreEqual("exact", comparisonRule, $"Unknown comparison rule for '{caseId}'.");
        Assert.IsTrue(
            JsonNode.DeepEquals(expected, actual),
            $"Semantic mismatch for PHP fixture case '{caseId}'.\nExpected: {expected}\nActual: {actual}");
    }

    private static JsonObject MergeWorks(JsonElement fixtureCase)
    {
        var left = ReadWork(fixtureCase.GetProperty("left"));
        var right = ReadWork(fixtureCase.GetProperty("right"));
        var merged = left.MergeWith(right);
        return new JsonObject
        {
            ["sameWork"] = left.IsSameWorkAs(right),
            ["title"] = merged.Title,
            ["ids"] = IdArray(merged.WorkIds)
        };
    }

    private static JsonObject DedupeCorpus(JsonElement fixtureCase)
    {
        var slice = CorpusSlice.Empty;
        foreach (var work in fixtureCase.GetProperty("works").EnumerateArray())
        {
            slice = slice.WithWork(ReadWork(work));
        }

        var works = new JsonArray(slice.Works.Select(work => (JsonNode)new JsonObject
        {
            ["title"] = work.Title,
            ["ids"] = IdArray(work.WorkIds)
        }).ToArray());
        return new JsonObject { ["count"] = slice.Works.Count, ["works"] = works };
    }

    private static JsonObject NoIdCandidates(JsonElement fixtureCase)
    {
        var left = ReadWork(fixtureCase.GetProperty("left"));
        var right = ReadWork(fixtureCase.GetProperty("right"));
        var slice = CorpusSlice.Empty.WithWork(left).WithWork(right);
        return new JsonObject { ["count"] = slice.Works.Count, ["sameWork"] = left.IsSameWorkAs(right) };
    }

    private static JsonObject TitleLookup(JsonElement fixtureCase)
    {
        var slice = CorpusSlice.Empty.WithWork(ReadWork(fixtureCase.GetProperty("work")));
        return new JsonObject
        {
            ["foundTitle"] = slice.FindByTitle(fixtureCase.GetProperty("query").GetString()!)?.Title
        };
    }

    private static ScholarlyWork ReadWork(JsonElement element)
    {
        var title = element.GetProperty("title").GetString()!;
        var source = element.GetProperty("sourceProvider").GetString()!;
        var ids = ReadIdSet(element.GetProperty("ids"));
        return ids.Ids.Count == 0
            ? ScholarlyWork.UnresolvedCandidate(title, source)
            : ScholarlyWork.Identified(title, ids, source);
    }

    private static WorkIdSet ReadIdSet(JsonElement element) => WorkIdSet.From(ReadIds(element));

    private static WorkId[] ReadIds(JsonElement element) => element.EnumerateArray()
        .Select(id => WorkId.From(id.GetProperty("namespace").GetString()!, id.GetProperty("value").GetString()!))
        .ToArray();

    private static JsonObject IdSetResult(WorkIdSet ids) => new() { ["ids"] = IdArray(ids) };

    private static JsonArray IdArray(WorkIdSet ids) => new(
        ids.Ids.Select(id => JsonValue.Create(id.ToString())).ToArray<JsonNode?>());

    private static JsonElement Result(JsonDocument document, string caseId) =>
        document.RootElement.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == caseId)
            .GetProperty("result");

    private static string DigestFixture(string fileName) => DigestFile(Path.Combine(FixtureDirectory(), fileName));

    private static string DigestFromBytes(byte[] bytes) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(bytes))}";

    private static string DigestFile(string path) => DigestFromBytes(File.ReadAllBytes(path));

    private static JsonDocument Load(string fileName) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureDirectory(), fileName)));

    private static string FixtureDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "php-golden", "shared-identity", "v1");

    private static string SourceLockPath() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "php-golden", "SOURCE.lock.json");
}
