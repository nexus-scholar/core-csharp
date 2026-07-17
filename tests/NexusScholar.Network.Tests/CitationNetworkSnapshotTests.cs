using Microsoft.VisualStudio.TestTools.UnitTesting;
using NexusScholar.Kernel;
using NexusScholar.Network;
using NexusScholar.Shared;

namespace NexusScholar.Network.Tests;

[TestClass]
public sealed class CitationNetworkSnapshotTests
{
    [TestMethod]
    public void Snapshot_is_deterministic_across_node_and_edge_input_order()
    {
        var first = BuildSnapshot(
            new[] { BuildNode("doi:10.1000/paper-b"), BuildNode("doi:10.1000/paper-a") },
            new[]
            {
                BuildEdge("doi:10.1000/paper-b", CitationNetworkCitationTarget.Resolved("doi:10.1000/paper-a"), "row-b"),
                BuildEdge("doi:10.1000/paper-a", CitationNetworkCitationTarget.Unresolved("10.1000/external"), "row-a")
            });

        var second = BuildSnapshot(
            new[] { BuildNode("doi:10.1000/paper-a"), BuildNode("doi:10.1000/paper-b") },
            new[]
            {
                BuildEdge("doi:10.1000/paper-a", CitationNetworkCitationTarget.Unresolved("10.1000/external"), "row-a"),
                BuildEdge("doi:10.1000/paper-b", CitationNetworkCitationTarget.Resolved("doi:10.1000/paper-a"), "row-b")
            });

        Assert.AreEqual(first.SnapshotDigest.ToString(), second.SnapshotDigest.ToString());
        Assert.AreEqual(first.Metrics.NodeCount, second.Metrics.NodeCount);
        Assert.AreEqual(first.Metrics.EdgeCount, second.Metrics.EdgeCount);
        CollectionAssert.AreEqual(
            new[] { "doi:10.1000/paper-a", "doi:10.1000/paper-b" },
            first.Nodes.Select(node => node.NodeId).ToArray());
    }

    [TestMethod]
    public void Snapshot_tracks_citation_degrees_and_isolated_nodes()
    {
        var snapshot = BuildSnapshot(
            new[]
            {
                BuildNode("doi:10.1000/paper-a"),
                BuildNode("doi:10.1000/paper-b"),
                BuildNode("doi:10.1000/paper-c")
            },
            new[]
            {
                BuildEdge("doi:10.1000/paper-a", CitationNetworkCitationTarget.Resolved("doi:10.1000/paper-b"), "row-a"),
                BuildEdge("doi:10.1000/paper-b", CitationNetworkCitationTarget.Unresolved("10.1000/external"), "row-b"),
                BuildEdge("doi:10.1000/paper-a", CitationNetworkCitationTarget.Resolved("doi:10.1000/paper-c"), "row-c")
            });

        Assert.AreEqual(3, snapshot.Metrics.NodeCount);
        Assert.AreEqual(3, snapshot.Metrics.EdgeCount);
        Assert.AreEqual(0, snapshot.Metrics.IsolatedNodeCount);

        var paperA = snapshot.Metrics.NodeDegrees.Single(node => node.NodeId == "doi:10.1000/paper-a");
        var paperB = snapshot.Metrics.NodeDegrees.Single(node => node.NodeId == "doi:10.1000/paper-b");
        var paperC = snapshot.Metrics.NodeDegrees.Single(node => node.NodeId == "doi:10.1000/paper-c");

        Assert.AreEqual(0, paperA.InDegree);
        Assert.AreEqual(2, paperA.OutDegree);
        Assert.AreEqual(1, paperB.InDegree);
        Assert.AreEqual(1, paperB.OutDegree);
        Assert.AreEqual(1, paperC.InDegree);
        Assert.AreEqual(0, paperC.OutDegree);

        CollectionAssert.AreEqual(new[] { "10.1000/external" }, snapshot.UnresolvedTargets.ToArray());
    }

    [TestMethod]
    public void Snapshot_rejects_missing_citing_node()
    {
        var error = Assert.ThrowsExactly<CitationNetworkRuleException>(() =>
            BuildSnapshot(
                new[] { BuildNode("doi:10.1000/paper-a") },
                new[] { BuildEdge("doi:10.1000/missing", CitationNetworkCitationTarget.Resolved("doi:10.1000/paper-a"), "row-a") }));

        Assert.AreEqual(CitationNetworkErrorCodes.MissingCitingNode, error.Category);
    }

    [TestMethod]
    public void Snapshot_rejects_missing_resolved_target_node()
    {
        var error = Assert.ThrowsExactly<CitationNetworkRuleException>(() =>
            BuildSnapshot(
                new[] { BuildNode("doi:10.1000/paper-a") },
                new[] { BuildEdge("doi:10.1000/paper-a", CitationNetworkCitationTarget.Resolved("doi:10.1000/missing"), "row-a") }));

        Assert.AreEqual(CitationNetworkErrorCodes.MissingResolvedTarget, error.Category);
    }

    [TestMethod]
    public void Snapshot_rejects_duplicate_direct_citation_edges()
    {
        var error = Assert.ThrowsExactly<CitationNetworkRuleException>(() =>
            BuildSnapshot(
                new[]
                {
                    BuildNode("doi:10.1000/paper-a"),
                    BuildNode("doi:10.1000/paper-b")
                },
                new[]
                {
                    BuildEdge("doi:10.1000/paper-a", CitationNetworkCitationTarget.Resolved("doi:10.1000/paper-b"), "row-a"),
                    BuildEdge("doi:10.1000/paper-a", CitationNetworkCitationTarget.Resolved("doi:10.1000/paper-b"), "row-b")
                }));

        Assert.AreEqual(CitationNetworkErrorCodes.DuplicateEdge, error.Category);
    }

    [TestMethod]
    public void Snapshot_rejects_invalid_corpus_snapshot_digest()
    {
        var error = Assert.ThrowsExactly<CitationNetworkRuleException>(() =>
        {
            _ = CitationNetworkSnapshot.Create(
                "snapshot-1",
                default,
                new[] { BuildNode("doi:10.1000/paper-a") },
                Array.Empty<CitationNetworkEdge>());
        });

        Assert.AreEqual(CitationNetworkErrorCodes.InvalidCorpusSnapshot, error.Category);
    }

    [TestMethod]
    public void Snapshot_rejects_unsupported_citation_kind()
    {
        var error = Assert.ThrowsExactly<CitationNetworkRuleException>(() =>
            new CitationNetworkEdge(
                "doi:10.1000/paper-a",
                "bibliographic-coupling",
                CitationNetworkCitationTarget.Resolved("doi:10.1000/paper-a"),
                BuildEvidence("unsupported")));

        Assert.AreEqual(CitationNetworkErrorCodes.UnsupportedEdgeKind, error.Category);
    }

    private static CitationNetworkSnapshot BuildSnapshot(IEnumerable<CitationNetworkNode> nodes, IEnumerable<CitationNetworkEdge> edges) =>
        CitationNetworkSnapshot.Create(
            "corpus-snapshot-1",
            ContentDigest.Sha256Utf8("snapshot-1"),
            nodes,
            edges,
            CitationNetworkSchemas.DirectCitationAlgorithmId,
            CitationNetworkSchemas.DirectCitationAlgorithmVersion);

    private static CitationNetworkNode BuildNode(string workId) => CitationNetworkNode.FromWork(
        ScholarlyWork.Identified("Title", WorkIdSet.From(WorkId.Parse(workId))));

    private static CitationNetworkEdge BuildEdge(
        string sourceNodeId,
        CitationNetworkCitationTarget target,
        string evidenceId) =>
        CitationNetworkEdge.DirectCitation(sourceNodeId, target, BuildEvidence(evidenceId));

    private static CitationNetworkEvidenceRef BuildEvidence(string evidenceId) =>
        new CitationNetworkEvidenceRef("provider", evidenceId, ContentDigest.Sha256Utf8(evidenceId));
}
