using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Shared;

namespace NexusScholar.Conformance.Tests;

[TestClass]
public sealed class SharedIdentityFixtureTests
{
    private const string FixtureSourceKind = "local-gate-9-contract";
    private const string FixtureSourceCommit = "local-gate-9-implementation";

    private static readonly string[] RequiredFixtureIds =
    {
        "shared-identity-workid-normalization",
        "shared-identity-workid-namespaces",
        "shared-identity-workidset-primary",
        "shared-identity-workidset-merge",
        "shared-identity-scholarlywork-merge",
        "shared-identity-no-id-candidate",
        "shared-identity-corpus-slice-dedupe",
        "shared-identity-unvalidated-candidates",
        "shared-identity-title-lookup-helper",
        "shared-identity-bad-workid-string",
        "shared-identity-blank-workid-constructor",
        "shared-identity-empty-title-work",
        "shared-identity-overlap-false",
        "shared-identity-cross-namespace-normalized-clash",
        "shared-identity-spl-object-fallback-probe",
        "shared-identity-title-only-not-identity",
        "shared-identity-no-id-no-dedupe",
        "shared-identity-no-id-snapshot-reject",
        "shared-identity-bad-merge-order"
    };

    [TestMethod]
    public void Gate_9_shared_identity_fixtures_are_present()
    {
        var ids = Directory.GetFiles(SharedIdentityFixtureDirectory(), "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var fixtureId in RequiredFixtureIds)
        {
            Assert.IsTrue(ids.Contains(fixtureId), $"Missing Gate 9 shared identity fixture '{fixtureId}'.");
        }
    }

    [TestMethod]
    public void Gate_9_fixtures_have_required_local_metadata_and_non_claims()
    {
        foreach (var path in Directory.GetFiles(SharedIdentityFixtureDirectory(), "*.json"))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var fixtureId = root.GetProperty("fixtureId").GetString();

            Assert.AreEqual(FixtureSourceKind, root.GetProperty("sourceKind").GetString(), fixtureId);
            Assert.AreEqual(FixtureSourceCommit, root.GetProperty("sourceCommit").GetString(), fixtureId);
            Assert.AreEqual("hand-authored local Gate 9 shared identity fixture", root.GetProperty("generatorCommand").GetString(), fixtureId);
            Assert.AreEqual("gate-9-v1", root.GetProperty("generatorVersion").GetString(), fixtureId);
            Assert.IsTrue(root.GetProperty("sourceRefs").EnumerateArray().Any(value =>
                string.Equals(value.GetString(), "docs/adr/0007-shared-scientific-identity.md", StringComparison.Ordinal)), fixtureId);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(rule =>
                string.Equals(rule.GetString(), "no-php-compatibility-claim", StringComparison.Ordinal)), fixtureId);
            Assert.IsTrue(root.GetProperty("comparisonRules").EnumerateArray().Any(rule =>
                string.Equals(rule.GetString(), "no-generated-php-fixture", StringComparison.Ordinal)), fixtureId);
            _ = ContentDigest.Parse(root.GetProperty("inputDigest").GetString()!);
            _ = ContentDigest.Parse(root.GetProperty("outputDigest").GetString()!);
        }
    }

    [TestMethod]
    public void Positive_shared_identity_fixtures_replay_local_behavior()
    {
        AssertFixtureValues(
            "shared-identity-workid-normalization.json",
            new[]
            {
                WorkId.From("doi", "https://doi.org/10.1000/XYZ").ToString(),
                WorkId.From("arxiv", "arXiv:2301.00001").ToString(),
                WorkId.From("openalex", "W123").ToString()
            },
            "normalized");

        using (var document = LoadJsonFixture("shared-identity-workid-namespaces.json"))
        {
            CollectionAssert.AreEqual(
                document.RootElement.GetProperty("case").GetProperty("approvedNamespaces").EnumerateArray().Select(value => value.GetString()).ToArray(),
                WorkIdNamespace.ApprovedNamespaces.ToArray());
        }

        using (var document = LoadJsonFixture("shared-identity-workidset-primary.json"))
        {
            var ids = document.RootElement.GetProperty("case").GetProperty("ids").EnumerateArray()
                .Select(value => WorkId.Parse(value.GetString()!))
                .ToArray();
            var set = WorkIdSet.From(ids);

            Assert.AreEqual(document.RootElement.GetProperty("case").GetProperty("primary").GetString(), set.Primary?.ToString());
            Assert.IsNull(WorkIdSet.Empty.Primary);
        }

        AssertScholarlyWorkMergeFixture();
        AssertCorpusSliceDedupeFixture();
        AssertNoIdCandidateFixture();
        AssertUnvalidatedCandidatesFixture();
        AssertTitleLookupHelperFixture();
    }

    [TestMethod]
    public void Negative_shared_identity_fixtures_replay_rejections_and_false_identity_cases()
    {
        AssertNegativeCategory(
            "shared-identity-bad-workid-string.json",
            SharedIdentityErrorCodes.InvalidWorkId,
            () => WorkId.Parse("doi:arxiv:2301.00001"));

        AssertNegativeCategory(
            "shared-identity-blank-workid-constructor.json",
            SharedIdentityErrorCodes.BlankWorkIdValue,
            () => WorkId.From("doi", " doi: "));

        AssertNegativeCategory(
            "shared-identity-empty-title-work.json",
            SharedIdentityErrorCodes.EmptyTitle,
            () => ScholarlyWork.Identified(" ", WorkIdSet.From(WorkId.From("doi", "10.1000/xyz"))));

        Assert.IsFalse(WorkIdSet.From(WorkId.From("doi", "10.1000/a")).HasOverlapWith(
            WorkIdSet.From(WorkId.From("doi", "10.1000/b"))));
        Assert.IsFalse(WorkIdSet.From(WorkId.From("pubmed", "123")).HasOverlapWith(
            WorkIdSet.From(WorkId.From("pmcid", "123"))));

        var titleLeft = ScholarlyWork.Identified("Same title", WorkIdSet.From(WorkId.From("doi", "10.1000/a")));
        var titleRight = ScholarlyWork.Identified("Same title", WorkIdSet.From(WorkId.From("openalex", "w2")));
        Assert.IsFalse(titleLeft.IsSameWorkAs(titleRight));

        var noIdSlice = CorpusSlice.Empty
            .WithWork(ScholarlyWork.UnresolvedCandidate("Same title", "import:row-1"))
            .WithWork(ScholarlyWork.UnresolvedCandidate("Same title", "import:row-2"));
        Assert.AreEqual(2, noIdSlice.Works.Count);

        AssertNegativeCategory(
            "shared-identity-no-id-snapshot-reject.json",
            SharedIdentityErrorCodes.NoStableIdentity,
            () => noIdSlice.StableMembershipIds());

        using var mergeOrder = LoadJsonFixture("shared-identity-bad-merge-order.json");
        var merged = ScholarlyWork.Identified("Left title", WorkIdSet.From(WorkId.From("doi", "10.1000/xyz")))
            .MergeWith(ScholarlyWork.Identified("Right title", WorkIdSet.From(WorkId.From("doi", "10.1000/XYZ"))));
        Assert.AreEqual(
            mergeOrder.RootElement.GetProperty("case").GetProperty("expectedTitle").GetString(),
            merged.Title);
    }

    private static void AssertFixtureValues(string fileName, string[] expected, string propertyName)
    {
        using var document = LoadJsonFixture(fileName);
        var actual = document.RootElement.GetProperty("case").GetProperty(propertyName).EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();

        CollectionAssert.AreEqual(expected, actual);
    }

    private static void AssertScholarlyWorkMergeFixture()
    {
        using var document = LoadJsonFixture("shared-identity-scholarlywork-merge.json");
        var fixtureCase = document.RootElement.GetProperty("case");
        var left = ScholarlyWork.Identified(
            fixtureCase.GetProperty("leftTitle").GetString()!,
            WorkIdSet.From(WorkId.From("doi", "10.1000/xyz")));
        var right = ScholarlyWork.Identified(
            fixtureCase.GetProperty("rightTitle").GetString()!,
            WorkIdSet.From(WorkId.From("doi", "https://doi.org/10.1000/XYZ"), WorkId.From("s2", "s2-1")));

        var merged = left.MergeWith(right);

        Assert.AreEqual(fixtureCase.GetProperty("mergedTitle").GetString(), merged.Title);
        CollectionAssert.AreEqual(
            fixtureCase.GetProperty("mergedIds").EnumerateArray().Select(value => value.GetString()).ToArray(),
            merged.WorkIds.Ids.Select(id => id.ToString()).ToArray());
    }

    private static void AssertCorpusSliceDedupeFixture()
    {
        using var document = LoadJsonFixture("shared-identity-corpus-slice-dedupe.json");
        var fixtureCase = document.RootElement.GetProperty("case");
        var slice = CorpusSlice.Empty
            .WithWork(ScholarlyWork.Identified("Left", WorkIdSet.From(WorkId.From("doi", "10.1000/xyz"))))
            .WithWork(ScholarlyWork.Identified("Overlap", WorkIdSet.From(WorkId.From("doi", "https://doi.org/10.1000/XYZ"), WorkId.From("openalex", "w1"))))
            .WithWork(ScholarlyWork.Identified("Other", WorkIdSet.From(WorkId.From("s2", "s2-9"))));

        Assert.AreEqual(fixtureCase.GetProperty("dedupedCount").GetInt32(), slice.Works.Count);
        CollectionAssert.AreEqual(
            fixtureCase.GetProperty("membershipIds").EnumerateArray().Select(value => value.GetString()).ToArray(),
            slice.StableMembershipIds().ToArray());
    }

    private static void AssertNoIdCandidateFixture()
    {
        using var document = LoadJsonFixture("shared-identity-no-id-candidate.json");
        var fixtureCase = document.RootElement.GetProperty("case");
        var candidate = ScholarlyWork.UnresolvedCandidate(
            fixtureCase.GetProperty("title").GetString()!,
            fixtureCase.GetProperty("sourceContext").GetString()!);

        Assert.IsTrue(candidate.IsUnresolvedCandidate);
        Assert.IsNull(candidate.PrimaryWorkId);
    }

    private static void AssertUnvalidatedCandidatesFixture()
    {
        using var document = LoadJsonFixture("shared-identity-unvalidated-candidates.json");
        var fixtureCase = document.RootElement.GetProperty("case");
        var slice = CorpusSlice.FromUnvalidatedCandidates(new[]
        {
            ScholarlyWork.Identified("First", WorkIdSet.From(WorkId.From("doi", "10.1000/xyz"))),
            ScholarlyWork.Identified("Duplicate", WorkIdSet.From(WorkId.From("doi", "10.1000/XYZ")))
        });

        Assert.AreEqual(fixtureCase.GetProperty("preservedCount").GetInt32(), slice.Works.Count);
    }

    private static void AssertTitleLookupHelperFixture()
    {
        using var document = LoadJsonFixture("shared-identity-title-lookup-helper.json");
        var fixtureCase = document.RootElement.GetProperty("case");
        var slice = CorpusSlice.Empty
            .WithWork(ScholarlyWork.Identified(
                fixtureCase.GetProperty("title").GetString()!,
                WorkIdSet.From(WorkId.From("doi", "10.1000/title"))));

        Assert.IsNotNull(slice.FindByTitle(fixtureCase.GetProperty("query").GetString()!));
        Assert.IsFalse(fixtureCase.GetProperty("createsIdentity").GetBoolean());
    }

    private static void AssertNegativeCategory(string fileName, string category, Action action)
    {
        using var document = LoadJsonFixture(fileName);
        var root = document.RootElement;
        Assert.AreEqual(category, root.GetProperty("case").GetProperty("errorCategory").GetString(), fileName);

        var exception = Assert.ThrowsExactly<SharedIdentityRuleException>(action);
        Assert.AreEqual(category, exception.Category, fileName);
    }

    private static JsonDocument LoadJsonFixture(string fileName)
    {
        var path = Path.Combine(SharedIdentityFixtureDirectory(), fileName);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string SharedIdentityFixtureDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "fixtures", "shared-identity");
}
