using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class PhpCompatibilityEvidenceClosureTests
{
    private const string FixtureSetId = "php-citation-export-observations-v1";

    private static readonly string[] H29CaseIds =
    {
        "bibliographic-coupling-weighted-pairs",
        "bibliography-format-vocabulary",
        "bibtex-article",
        "bibtex-preprint",
        "co-citation-weighted-pairs",
        "direct-builder-known-references-only",
        "duplicate-normalized-edge-deduplicated",
        "export-filename-extension-validation",
        "external-cited-node-accepted",
        "graph-type-vocabulary",
        "graphml-corpus-nodes-and-escaping",
        "missing-citing-node-rejected",
        "namespace-qualified-nodes",
        "network-export-format-vocabulary"
    };

    private static readonly string[] H29SourceRefs =
    {
        "src/CitationNetwork/Application/Builder/CitationGraphBuilder.php",
        "src/CitationNetwork/Domain/CitationGraph.php",
        "src/CitationNetwork/Domain/CitationGraphType.php",
        "src/CitationNetwork/Domain/CitationLink.php",
        "src/Dissemination/Application/Support/ValidatesExportFilename.php",
        "src/Dissemination/Domain/BibliographyFormat.php",
        "src/Dissemination/Domain/NetworkExportFormat.php",
        "src/Dissemination/Infrastructure/Serializer/BibTexSerializer.php",
        "src/Dissemination/Infrastructure/Serializer/GraphMlSerializer.php",
        "src/Shared/Domain/CorpusSlice.php",
        "src/Shared/Domain/ScholarlyWork.php",
        "src/Shared/ValueObject/Author.php",
        "src/Shared/ValueObject/AuthorList.php",
        "src/Shared/ValueObject/Venue.php",
        "src/Shared/ValueObject/WorkId.php",
        "src/Shared/ValueObject/WorkIdNamespace.php",
        "src/Shared/ValueObject/WorkIdSet.php",
        "tests/Unit/CitationNetwork/Application/Builder/CitationGraphBuilderTest.php",
        "tests/Unit/CitationNetwork/Domain/CitationGraphTest.php",
        "tests/Unit/Dissemination/Infrastructure/BibTexSerializerTest.php",
        "tests/Unit/Dissemination/Infrastructure/GraphMlSerializerTest.php"
    };

    [TestMethod]
    public void H29_manifest_binds_pinned_source_and_evidence_digests()
    {
        using var manifest = LoadH29("manifest.json");
        using var sourceLock = JsonDocument.Parse(File.ReadAllBytes(SourceLockPath()));
        var root = manifest.RootElement;
        var phpReference = sourceLock.RootElement.GetProperty("php_reference");

        Assert.AreEqual(FixtureSetId, root.GetProperty("fixtureSetId").GetString());
        Assert.AreEqual("1.0.0", root.GetProperty("schemaVersion").GetString());
        Assert.AreEqual("pinned-php-observable-behavior", root.GetProperty("sourceKind").GetString());
        Assert.AreEqual(phpReference.GetProperty("repository").GetString(), root.GetProperty("sourceRepository").GetString());
        Assert.AreEqual(phpReference.GetProperty("commit").GetString(), root.GetProperty("sourceCommit").GetString());
        Assert.AreEqual("citation-export-observations-v1", root.GetProperty("generatorVersion").GetString());
        Assert.AreEqual(
            "php scripts/php-golden/citation-export-observations.php --php-reference \"$PHP_REFERENCE\" --source-lock specs/SOURCE.lock.json --input fixtures/php-golden/citation-export/v1/input.json --comparison fixtures/php-golden/citation-export/v1/comparison.json --output fixtures/php-golden/citation-export/v1/expected.json --manifest fixtures/php-golden/citation-export/v1/manifest.json",
            root.GetProperty("generatorCommand").GetString());
        Assert.AreEqual(DigestH29("input.json"), root.GetProperty("inputDigest").GetString());
        Assert.AreEqual(DigestH29("expected.json"), root.GetProperty("outputDigest").GetString());
        Assert.AreEqual(DigestFile(SourceLockPath()), root.GetProperty("sourceLockDigest").GetString());
        Assert.AreEqual(DigestH29("comparison.json"), root.GetProperty("classificationDigest").GetString());

        var sourceRefs = ReadStrings(root.GetProperty("sourceRefs"));
        CollectionAssert.AreEqual(H29SourceRefs, sourceRefs);
        CollectionAssert.AllItemsAreUnique(sourceRefs);
        CollectionAssert.AreEqual(
            new[]
            {
                "PHP 8.3 or later",
                "git is available",
                "PHP reference tracked files are clean",
                "no network access or Composer dependencies are required",
                "no Mbsoft graph dependencies are required for fixture generation",
                "no Laravel persistence path is exercised",
                "runtime IDs, timestamps, object hashes, and persistence state are excluded from generated outputs",
                "UTF-8 JSON with LF line endings"
            },
            ReadStrings(root.GetProperty("environmentAssumptions")));
        CollectionAssert.AreEqual(
            new[]
            {
                "generated citation graph ids",
                "generated corpus slice ids",
                "retrieved timestamps in ScholarlyWork"
            },
            ReadStrings(root.GetProperty("ignoredNondeterminism")));
        CollectionAssert.AreEqual(
            new[]
            {
                "compare PHP observable behavior only, not any C# replay targets",
                "compare normalized outputs derived from deterministic fixed-case inputs",
                "ignore runtime object identity, storage paths, and internal IDs"
            },
            ReadStrings(root.GetProperty("comparisonRules")));
    }

    [TestMethod]
    public void H29_inventory_is_exact_and_every_case_is_intentional_non_adoption()
    {
        using var input = LoadH29("input.json");
        using var expected = LoadH29("expected.json");
        using var comparison = LoadH29("comparison.json");

        var inputIds = CaseIds(input.RootElement);
        var expectedIds = CaseIds(expected.RootElement);
        var classifications = comparison.RootElement.GetProperty("classifications").EnumerateArray().ToArray();
        var classifiedIds = classifications.Select(item => item.GetProperty("caseId").GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();

        CollectionAssert.AreEqual(H29CaseIds, inputIds);
        CollectionAssert.AreEqual(H29CaseIds, expectedIds);
        CollectionAssert.AreEqual(H29CaseIds, classifiedIds);
        Assert.AreEqual(14, classifications.Length);

        foreach (var classification in classifications)
        {
            Assert.AreEqual("intentional_change", classification.GetProperty("classification").GetString());
            Assert.AreEqual("intentional-non-adoption-no-csharp-replay", classification.GetProperty("comparisonRule").GetString());
            CollectionAssert.AreEqual(
                new[]
                {
                    "docs/adr/0027-phase-7-citation-network-dissemination-evidence-boundary.md",
                    "docs/port/PORT-MATRIX.csv"
                },
                ReadStrings(classification.GetProperty("authorityRefs")));
            StringAssert.Contains(classification.GetProperty("rationale").GetString()!, "No CSharp replay target");
        }
    }

    [TestMethod]
    public void H29_php_observations_are_pinned_without_fabricating_csharp_parity()
    {
        using var expected = LoadH29("expected.json");
        var cases = expected.RootElement.GetProperty("cases").EnumerateArray()
            .ToDictionary(item => item.GetProperty("id").GetString()!, item => item.GetProperty("result").Clone(), StringComparer.Ordinal);

        CollectionAssert.AreEqual(
            new[] { "citation", "co_citation", "bibliographic_coupling" },
            ReadStrings(cases["graph-type-vocabulary"].GetProperty("values")));

        var bibliographyFormats = cases["bibliography-format-vocabulary"];
        Assert.AreEqual(5, bibliographyFormats.GetProperty("formats").GetArrayLength());
        AssertFormat(bibliographyFormats, "bibtex", "bib", "application/x-bibtex");
        AssertFormat(bibliographyFormats, "ris", "ris", "application/x-research-info-systems");
        AssertFormat(bibliographyFormats, "csv", "csv", "text/csv");
        AssertFormat(bibliographyFormats, "json", "json", "application/json");
        AssertFormat(bibliographyFormats, "jsonl", "jsonl", "application/x-jsonlines");

        var networkFormats = cases["network-export-format-vocabulary"];
        Assert.AreEqual(3, networkFormats.GetProperty("formats").GetArrayLength());
        AssertFormat(networkFormats, "gexf", "gexf", "application/xml");
        AssertFormat(networkFormats, "graphml", "graphml", "application/xml");
        AssertFormat(networkFormats, "cytoscape", "json", "application/json");

        var nodes = cases["namespace-qualified-nodes"];
        Assert.AreEqual(2, nodes.GetProperty("nodeCount").GetInt32());
        CollectionAssert.AreEqual(
            new[] { "doi:10.1000/alpha", "doi:10.1000/beta" },
            nodes.GetProperty("nodes").EnumerateArray().Select(node => node.GetProperty("primaryId").GetString()!).ToArray());

        var missing = cases["missing-citing-node-rejected"];
        Assert.IsTrue(missing.GetProperty("rejected").GetBoolean());
        Assert.AreEqual("missing-citing-node", missing.GetProperty("errorCategory").GetString());

        var external = cases["external-cited-node-accepted"];
        Assert.IsTrue(external.GetProperty("accepted").GetBoolean());
        Assert.IsFalse(external.GetProperty("externalIsGraphNode").GetBoolean());
        Assert.AreEqual(1, external.GetProperty("edgeCount").GetInt32());
        AssertEdge(external.GetProperty("edges")[0], "doi:10.1000/citing", "doi:10.2000/external", 1);

        var duplicate = cases["duplicate-normalized-edge-deduplicated"];
        Assert.AreEqual(1, duplicate.GetProperty("edgeCount").GetInt32());
        AssertEdge(duplicate.GetProperty("edges")[0], "doi:10.1000/citing-duplicate", "doi:10.1000/target", 1);

        var direct = cases["direct-builder-known-references-only"];
        Assert.AreEqual(2, direct.GetProperty("edgeCount").GetInt32());
        AssertEdge(direct.GetProperty("edges")[0], "doi:10.1000/casing-primary", "doi:10.1000/known-one", 1);
        AssertEdge(direct.GetProperty("edges")[1], "doi:10.1000/casing-primary", "doi:10.1000/known-two", 1);

        var coCitation = cases["co-citation-weighted-pairs"];
        Assert.AreEqual(1, coCitation.GetProperty("edgeCount").GetInt32());
        AssertEdge(coCitation.GetProperty("edges")[0], "doi:10.1000/cited-c", "doi:10.1000/cited-d", 2);

        var couplingEdges = cases["bibliographic-coupling-weighted-pairs"].GetProperty("edges");
        Assert.AreEqual(3, couplingEdges.GetArrayLength());
        AssertEdge(couplingEdges[0], "doi:10.1000/couple-a", "doi:10.1000/couple-b", 2);
        AssertEdge(couplingEdges[1], "doi:10.1000/couple-a", "doi:10.1000/couple-c", 1);
        AssertEdge(couplingEdges[2], "doi:10.1000/couple-b", "doi:10.1000/couple-c", 1);

        var article = cases["bibtex-article"].GetProperty("content").GetString()!;
        StringAssert.StartsWith(article, "@article{doi:10.1000/article-001,");
        StringAssert.Contains(article, "author = {Richard Feynman and Isaac Newton}");
        StringAssert.Contains(article, "journal = {Journal of Data Pipelines}");

        var preprint = cases["bibtex-preprint"].GetProperty("content").GetString()!;
        StringAssert.StartsWith(preprint, "@misc{arxiv:2401.12345,");
        StringAssert.Contains(preprint, "note = {arXiv}");

        var graphMl = cases["graphml-corpus-nodes-and-escaping"].GetProperty("content").GetString()!;
        StringAssert.Contains(graphMl, "Ampersand &amp; &lt;angle&gt; and \"quote\"");
        Assert.IsFalse(graphMl.Contains("Ampersand & <angle>", StringComparison.Ordinal));

        var filenameChecks = cases["export-filename-extension-validation"].GetProperty("checks");
        Assert.IsTrue(filenameChecks[0].GetProperty("result").GetProperty("passed").GetBoolean());
        Assert.IsFalse(filenameChecks[1].GetProperty("result").GetProperty("passed").GetBoolean());
        Assert.IsFalse(filenameChecks[2].GetProperty("result").GetProperty("passed").GetBoolean());
        AssertFilenameError(
            filenameChecks[1],
            "graphml",
            "citation-network.graphml",
            "gexf",
            "Export filename extension must match format graphml (.gexf): citation-network.graphml");
        AssertFilenameError(
            filenameChecks[2],
            "cytoscape",
            "citation-network.GEXF",
            "json",
            "Export filename extension must match format cytoscape (.json): citation-network.GEXF");
    }

    [TestMethod]
    public void Phase_7_fixture_families_are_complete_and_have_no_open_csharp_defect_or_spec_conflict()
    {
        var families = new[]
        {
            new FixtureFamily("shared-identity/v1", "php-shared-identity-v1", 12, 9, 3, 0),
            new FixtureFamily("search/v1", "php-search-v1", 18, 15, 3, 0),
            new FixtureFamily("deduplication/v1", "php-deduplication-v1", 16, 8, 8, 0),
            new FixtureFamily("screening-fulltext/v1", "php-screening-fulltext-v1", 26, 16, 9, 1),
            new FixtureFamily("citation-export/v1", FixtureSetId, 14, 0, 14, 0)
        };

        using var sourceLock = JsonDocument.Parse(File.ReadAllBytes(SourceLockPath()));
        var sourceCommit = sourceLock.RootElement.GetProperty("php_reference").GetProperty("commit").GetString();

        foreach (var family in families)
        {
            var root = FixtureRoot(family.Path);
            foreach (var filename in new[] { "input.json", "expected.json", "comparison.json", "manifest.json" })
            {
                Assert.IsTrue(File.Exists(Path.Combine(root, filename)), $"Missing {family.Path}/{filename}");
            }

            using var expected = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "expected.json")));
            using var comparison = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "comparison.json")));
            using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "manifest.json")));
            var classifications = comparison.RootElement.GetProperty("classifications").EnumerateArray().ToArray();

            Assert.AreEqual(family.FixtureSetId, manifest.RootElement.GetProperty("fixtureSetId").GetString(), family.Path);
            Assert.AreEqual(sourceCommit, manifest.RootElement.GetProperty("sourceCommit").GetString(), family.Path);
            Assert.AreEqual(family.Total, expected.RootElement.GetProperty("cases").GetArrayLength(), family.Path);
            Assert.AreEqual(family.Total, classifications.Length, family.Path);
            Assert.AreEqual(family.Equivalent, Count(classifications, "equivalent_serialization"), family.Path);
            Assert.AreEqual(family.Intentional, Count(classifications, "intentional_change"), family.Path);
            Assert.AreEqual(family.PhpDefects, Count(classifications, "php_defect"), family.Path);
            Assert.AreEqual(0, Count(classifications, "csharp_defect"), family.Path);
            Assert.AreEqual(0, Count(classifications, "unresolved_specification_conflict"), family.Path);
        }

        var repositoryRoot = RepositoryRoot();
        Assert.IsTrue(Directory.Exists(Path.Combine(repositoryRoot, "src", "NexusScholar.Network")));
        Assert.IsTrue(File.Exists(Path.Combine(
            repositoryRoot,
            "docs",
            "adr",
            "0043-citation-graph-snapshot-and-basic-metrics.md")));
        var productionSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
        foreach (var forbiddenDeclaration in new[]
                 {
                     "namespace NexusScholar.CitationNetwork",
                     "namespace NexusScholar.Dissemination",
                     "class CitationGraph",
                     "record CitationGraph",
                     "class CitationGraphBuilder",
                     "class BibTexSerializer",
                     "class GraphMlSerializer",
                     "enum NetworkExportFormat",
                     "enum BibliographyFormat"
                 })
        {
            Assert.IsFalse(
                productionSource.Contains(forbiddenDeclaration, StringComparison.Ordinal),
                $"H29 non-adoption boundary was invalidated by production declaration '{forbiddenDeclaration}'.");
        }
        var inventory = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "port", "PHASE-7-COMPATIBILITY-CLAIM-INVENTORY.md"));
        var inventoryRows = inventory.Split('\n', StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("| H", StringComparison.Ordinal) && !line.StartsWith("| H item", StringComparison.Ordinal))
            .Select(ParseInventoryRow)
            .ToDictionary(columns => columns[0], columns => columns, StringComparer.Ordinal);
        Assert.AreEqual(5, inventoryRows.Count);

        var comparatorPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["H25"] = "tests/NexusScholar.Conformance.Tests/PhpSharedIdentityGoldenTests.cs",
            ["H26"] = "tests/NexusScholar.Conformance.Tests/PhpSearchGoldenTests.cs",
            ["H27"] = "tests/NexusScholar.Conformance.Tests/PhpDeduplicationGoldenTests.cs",
            ["H28"] = "tests/NexusScholar.Conformance.Tests/PhpScreeningFullTextGoldenTests.cs",
            ["H29"] = "tests/NexusScholar.Conformance.Tests/PhpCompatibilityEvidenceClosureTests.cs"
        };
        var scopedClaimPins = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["H25"] = "normalization, overlap identity, primary precedence",
            ["H26"] = "query/cache/provider selection/raw trace handling",
            ["H27"] = "Exact namespace matching, transitive clustering",
            ["H28"] = "Screening and local Full Text overlap",
            ["H29"] = "Evidence-only graph construction, vocabulary, BibTeX, local GraphML"
        };
        var uncoveredPins = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["H25"] = "runtime-object fallback parity",
            ["H26"] = "Live providers, provider SDKs, imported-export PHP parity",
            ["H27"] = "Snapshot equality, persistence, app-run projections",
            ["H28"] = "OCR, PDF extraction, live retrieval, network behavior",
            ["H29"] = "Metrics, shortest paths, snowballing, external graph serializers"
        };

        for (var index = 0; index < families.Length; index++)
        {
            var family = families[index];
            var job = $"H{index + 25}";
            var columns = inventoryRows[job];
            StringAssert.Contains(columns[1], family.FixtureSetId);
            Assert.AreEqual(family.Total.ToString(System.Globalization.CultureInfo.InvariantCulture), columns[2], job);
            Assert.AreEqual(family.Equivalent.ToString(System.Globalization.CultureInfo.InvariantCulture), columns[3], job);
            StringAssert.StartsWith(columns[4], family.Intentional.ToString(System.Globalization.CultureInfo.InvariantCulture));
            StringAssert.StartsWith(columns[5], family.PhpDefects.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.AreEqual("0", columns[6], job);
            StringAssert.Contains(columns[7], scopedClaimPins[job]);
            StringAssert.Contains(columns[8], comparatorPaths[job]);
            StringAssert.Contains(columns[9], uncoveredPins[job]);
            StringAssert.Contains(columns[9], "unclaimed");
        }

        StringAssert.Contains(inventory, "Broad PHP compatibility remains unclaimed");
    }

    private static int Count(JsonElement[] classifications, string classification) =>
        classifications.Count(item => item.GetProperty("classification").GetString() == classification);

    private static void AssertFormat(JsonElement result, string name, string extension, string mimeType)
    {
        var format = result.GetProperty("formats").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);
        Assert.AreEqual(extension, format.GetProperty("extension").GetString());
        Assert.AreEqual(mimeType, format.GetProperty("mimeType").GetString());
    }

    private static void AssertEdge(JsonElement edge, string from, string to, double weight)
    {
        Assert.AreEqual(from, edge.GetProperty("from").GetString());
        Assert.AreEqual(to, edge.GetProperty("to").GetString());
        Assert.AreEqual(weight, edge.GetProperty("weight").GetDouble());
    }

    private static void AssertFilenameError(
        JsonElement check,
        string format,
        string filename,
        string extension,
        string error)
    {
        Assert.AreEqual(format, check.GetProperty("format").GetString());
        Assert.AreEqual(filename, check.GetProperty("filename").GetString());
        Assert.AreEqual(extension, check.GetProperty("extension").GetString());
        Assert.AreEqual("extension-mismatch", check.GetProperty("result").GetProperty("errorCategory").GetString());
        Assert.AreEqual(error, check.GetProperty("result").GetProperty("error").GetString());
    }

    private static string[] CaseIds(JsonElement root) => root.GetProperty("cases").EnumerateArray()
        .Select(item => item.GetProperty("id").GetString()!)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static JsonDocument LoadH29(string filename) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(FixtureRoot("citation-export/v1"), filename)));

    private static string DigestH29(string filename) =>
        DigestFile(Path.Combine(FixtureRoot("citation-export/v1"), filename));

    private static string FixtureRoot(string path) =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "php-golden", path.Replace('/', Path.DirectorySeparatorChar));

    private static string SourceLockPath() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "php-golden", "SOURCE.lock.json");

    private static string DigestFile(string path) =>
        $"sha256:{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))}";

    private static string[] ReadStrings(JsonElement element) =>
        element.EnumerateArray().Select(value => value.GetString()!).ToArray();

    private static string[] ParseInventoryRow(string row) =>
        row.Trim('|').Split('|', StringSplitOptions.TrimEntries);

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "NexusScholar.Core.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record FixtureFamily(
        string Path,
        string FixtureSetId,
        int Total,
        int Equivalent,
        int Intentional,
        int PhpDefects);
}
