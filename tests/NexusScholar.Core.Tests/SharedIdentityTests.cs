using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Shared;

namespace NexusScholar.Core.Tests;

[TestClass]
public sealed class SharedIdentityTests
{
    [TestMethod]
    public void Workid_namespace_set_matches_gate_9_contract()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                "doi",
                "arxiv",
                "openalex",
                "s2",
                "pubmed",
                "pmcid",
                "ieee",
                "doaj",
                "internal"
            },
            WorkIdNamespace.ApprovedNamespaces.ToArray());

        var error = Assert.ThrowsExactly<SharedIdentityRuleException>(() => WorkIdNamespace.From("isbn"));
        Assert.AreEqual(SharedIdentityErrorCodes.UnknownWorkIdNamespace, error.Category);
    }

    [TestMethod]
    public void Workid_normalizes_namespace_value_and_known_prefixes()
    {
        Assert.AreEqual("doi:10.1000/xyz", WorkId.From("DOI", " https://doi.org/10.1000/XYZ ").ToString());
        Assert.AreEqual("doi:10.1000/xyz", WorkId.From("doi", "http://dx.doi.org/10.1000/XYZ").ToString());
        Assert.AreEqual("doi:10.1000/xyz", WorkId.From("doi", "doi:10.1000/XYZ").ToString());
        Assert.AreEqual("arxiv:2301.00001", WorkId.From("arxiv", "arXiv:2301.00001").ToString());
        Assert.AreEqual("openalex:w123", WorkId.From("OpenAlex", " W123 ").ToString());
    }

    [TestMethod]
    public void Workid_parse_is_strict()
    {
        Assert.AreEqual(WorkId.From("doi", "10.1000/xyz"), WorkId.Parse("doi:10.1000/xyz"));

        AssertInvalidWorkId("doi");
        AssertInvalidWorkId(":10.1000/xyz");
        AssertInvalidWorkId("doi:");
        AssertInvalidWorkId("doi:arxiv:2301.00001");

        var unknown = Assert.ThrowsExactly<SharedIdentityRuleException>(() => WorkId.Parse("isbn:123"));
        Assert.AreEqual(SharedIdentityErrorCodes.UnknownWorkIdNamespace, unknown.Category);
    }

    [TestMethod]
    public void Blank_workid_constructor_value_is_rejected()
    {
        var error = Assert.ThrowsExactly<SharedIdentityRuleException>(() => WorkId.From("doi", " doi: "));
        Assert.AreEqual(SharedIdentityErrorCodes.BlankWorkIdValue, error.Category);
    }

    [TestMethod]
    public void Workidset_uses_primary_precedence_and_deduplicates_exact_ids()
    {
        var s2 = WorkId.From("s2", "S2-1");
        var arxiv = WorkId.From("arxiv", "2301.00001");
        var doi = WorkId.From("doi", "10.1000/XYZ");

        var set = WorkIdSet.From(s2, arxiv, doi, WorkId.From("doi", "https://doi.org/10.1000/xyz"));

        Assert.AreEqual("doi:10.1000/xyz", set.Primary?.ToString());
        CollectionAssert.AreEqual(
            new[] { "doi:10.1000/xyz", "s2:s2-1", "arxiv:2301.00001" },
            set.Ids.Select(id => id.ToString()).ToArray());
    }

    [TestMethod]
    public void Workidset_add_merge_and_overlap_are_identifier_based()
    {
        var original = WorkIdSet.From(WorkId.From("openalex", "W1"));
        var added = original.Add(WorkId.From("doi", "10.1000/xyz"));
        var merged = added.Merge(WorkIdSet.From(WorkId.From("s2", "S2-1"), WorkId.From("doi", "10.1000/xyz")));

        Assert.AreEqual(1, original.Ids.Count);
        Assert.AreEqual("doi:10.1000/xyz", added.Primary?.ToString());
        Assert.AreEqual(3, merged.Ids.Count);
        Assert.IsTrue(merged.HasOverlapWith(WorkIdSet.From(WorkId.From("s2", "s2-1"))));
        Assert.IsFalse(WorkIdSet.From(WorkId.From("pubmed", "123")).HasOverlapWith(WorkIdSet.From(WorkId.From("pmcid", "123"))));
    }

    [TestMethod]
    public void Scholarly_work_identity_uses_id_overlap_not_title()
    {
        var first = ScholarlyWork.Identified(
            "Same title",
            WorkIdSet.From(WorkId.From("doi", "10.1000/xyz")));
        var overlap = ScholarlyWork.Identified(
            "Different title",
            WorkIdSet.From(WorkId.From("doi", "https://doi.org/10.1000/XYZ"), WorkId.From("s2", "S2-1")));
        var titleOnly = ScholarlyWork.Identified(
            "Same title",
            WorkIdSet.From(WorkId.From("openalex", "W2")));

        Assert.IsTrue(first.IsSameWorkAs(overlap));
        Assert.IsFalse(first.IsSameWorkAs(titleOnly));

        var merged = first.MergeWith(overlap);
        Assert.AreEqual("Same title", merged.Title);
        Assert.AreEqual("doi:10.1000/xyz", merged.PrimaryWorkId?.ToString());
        Assert.IsTrue(merged.WorkIds.Contains(WorkId.From("s2", "s2-1")));
    }

    [TestMethod]
    public void Scholarly_work_rejects_empty_title_and_no_id_without_source_context()
    {
        var emptyTitle = Assert.ThrowsExactly<SharedIdentityRuleException>(() =>
            ScholarlyWork.Identified(" ", WorkIdSet.From(WorkId.From("doi", "10.1000/xyz"))));
        Assert.AreEqual(SharedIdentityErrorCodes.EmptyTitle, emptyTitle.Category);

        var missingSource = Assert.ThrowsExactly<SharedIdentityRuleException>(() =>
            ScholarlyWork.UnresolvedCandidate("Candidate title", " "));
        Assert.AreEqual(SharedIdentityErrorCodes.MissingSourceContext, missingSource.Category);
    }

    [TestMethod]
    public void No_id_candidates_are_admitted_without_primary_identity()
    {
        var candidate = ScholarlyWork.UnresolvedCandidate("Candidate title", "import:provider-row-1");

        Assert.IsTrue(candidate.IsUnresolvedCandidate);
        Assert.IsNull(candidate.PrimaryWorkId);
        Assert.AreEqual("import:provider-row-1", candidate.SourceContext);
    }

    [TestMethod]
    public void Corpus_slice_deduplicates_by_stable_id_overlap_only()
    {
        var first = ScholarlyWork.Identified(
            "Left",
            WorkIdSet.From(WorkId.From("doi", "10.1000/xyz")));
        var overlap = ScholarlyWork.Identified(
            "Right",
            WorkIdSet.From(WorkId.From("doi", "https://doi.org/10.1000/XYZ"), WorkId.From("openalex", "W1")));
        var sameTitleNoOverlap = ScholarlyWork.Identified(
            "Left",
            WorkIdSet.From(WorkId.From("s2", "S2-9")));

        var slice = CorpusSlice.Empty
            .WithWork(first)
            .WithWork(overlap)
            .WithWork(sameTitleNoOverlap);

        Assert.AreEqual(2, slice.Works.Count);
        Assert.AreEqual("Left", slice.Works[0].Title);
        Assert.AreEqual("doi:10.1000/xyz", slice.FindById(WorkId.From("doi", "10.1000/xyz"))?.PrimaryWorkId?.ToString());
        Assert.AreEqual("Left", slice.FindByTitle("left")?.Title);
    }

    [TestMethod]
    public void Corpus_slice_preserves_no_id_candidates_even_with_matching_titles()
    {
        var first = ScholarlyWork.UnresolvedCandidate("Same title", "import:row-1");
        var second = ScholarlyWork.UnresolvedCandidate("Same title", "import:row-2");

        var slice = CorpusSlice.Empty.WithWork(first).WithWork(second);

        Assert.AreEqual(2, slice.Works.Count);
        Assert.AreEqual("import:row-1", slice.Works[0].SourceContext);
        Assert.AreEqual("import:row-2", slice.Works[1].SourceContext);
    }

    [TestMethod]
    public void From_unvalidated_candidates_preserves_raw_candidates_without_dedupe()
    {
        var first = ScholarlyWork.Identified(
            "First",
            WorkIdSet.From(WorkId.From("doi", "10.1000/xyz")));
        var duplicate = ScholarlyWork.Identified(
            "Duplicate",
            WorkIdSet.From(WorkId.From("doi", "10.1000/XYZ")));

        var slice = CorpusSlice.FromUnvalidatedCandidates(new[] { first, duplicate });

        Assert.AreEqual(2, slice.Works.Count);
        Assert.AreEqual("First", slice.Works[0].Title);
        Assert.AreEqual("Duplicate", slice.Works[1].Title);
    }

    [TestMethod]
    public void No_id_candidates_cannot_satisfy_immutable_scientific_membership()
    {
        var slice = CorpusSlice.Empty
            .WithWork(ScholarlyWork.Identified("Identified", WorkIdSet.From(WorkId.From("doi", "10.1000/xyz"))))
            .WithWork(ScholarlyWork.UnresolvedCandidate("Candidate", "import:row-1"));

        var error = Assert.ThrowsExactly<SharedIdentityRuleException>(() => slice.StableMembershipIds());
        Assert.AreEqual(SharedIdentityErrorCodes.NoStableIdentity, error.Category);
    }

    [TestMethod]
    public void Shared_identity_collections_are_snapshot_views()
    {
        var rawData = new Dictionary<string, string> { ["provider"] = "openalex" };
        var work = ScholarlyWork.Identified(
            "Identified",
            WorkIdSet.From(WorkId.From("doi", "10.1000/xyz")),
            rawData: rawData);
        rawData["provider"] = "mutated";

        Assert.AreEqual("openalex", work.RawData["provider"]);
        Assert.IsFalse(work.RawData is Dictionary<string, string>);
        Assert.IsFalse(work.WorkIds.Ids is WorkId[]);
        Assert.IsFalse(CorpusSlice.Empty.WithWork(work).Works is ScholarlyWork[]);
    }

    private static void AssertInvalidWorkId(string value)
    {
        var error = Assert.ThrowsExactly<SharedIdentityRuleException>(() => WorkId.Parse(value));
        Assert.AreEqual(SharedIdentityErrorCodes.InvalidWorkId, error.Category);
    }
}
