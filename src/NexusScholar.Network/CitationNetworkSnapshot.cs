using NexusScholar.Kernel;
using NexusScholar.Shared;

namespace NexusScholar.Network;

public sealed record CitationNetworkNodeDegree(string NodeId, int InDegree, int OutDegree);

public sealed class CitationNetworkSnapshot
{
    private CitationNetworkSnapshot(
        string corpusSnapshotId,
        ContentDigest corpusSnapshotDigest,
        string algorithmId,
        string algorithmVersion,
        IReadOnlyList<CitationNetworkNode> nodes,
        IReadOnlyList<string> unresolvedTargets,
        IReadOnlyList<CitationNetworkEdge> edges,
        CitationNetworkMetrics metrics)
    {
        CorpusSnapshotId = Guard.NotBlank(corpusSnapshotId, nameof(corpusSnapshotId));
        CorpusSnapshotDigest = corpusSnapshotDigest;
        AlgorithmId = Guard.NotBlank(algorithmId, nameof(algorithmId));
        AlgorithmVersion = Guard.NotBlank(algorithmVersion, nameof(algorithmVersion));
        Nodes = nodes;
        UnresolvedTargets = unresolvedTargets;
        Edges = edges;
        Metrics = metrics;
        SnapshotDigest = Envelope().ComputeDigest();
    }

    public string CorpusSnapshotId { get; }

    public ContentDigest CorpusSnapshotDigest { get; }

    public string AlgorithmId { get; }

    public string AlgorithmVersion { get; }

    public IReadOnlyList<CitationNetworkNode> Nodes { get; }

    public IReadOnlyList<string> UnresolvedTargets { get; }

    public IReadOnlyList<CitationNetworkEdge> Edges { get; }

    public CitationNetworkMetrics Metrics { get; }

    public ContentDigest SnapshotDigest { get; }

    public static CitationNetworkSnapshot Create(
        string corpusSnapshotId,
        ContentDigest corpusSnapshotDigest,
        IEnumerable<CitationNetworkNode> nodes,
        IEnumerable<CitationNetworkEdge> edges,
        string algorithmId = CitationNetworkSchemas.DirectCitationAlgorithmId,
        string algorithmVersion = CitationNetworkSchemas.DirectCitationAlgorithmVersion)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);

        if (!corpusSnapshotDigest.IsValid)
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.InvalidCorpusSnapshot,
                "Corpus snapshot digest must be valid.");
        }

        if (!string.Equals(algorithmId, CitationNetworkSchemas.DirectCitationAlgorithmId, StringComparison.Ordinal))
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.UnsupportedAlgorithm,
                "Only the direct-citation algorithm is supported in this slice.");
        }

        if (!string.Equals(algorithmVersion, CitationNetworkSchemas.DirectCitationAlgorithmVersion, StringComparison.Ordinal))
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.UnsupportedAlgorithm,
                "Unknown direct-citation algorithm version.");
        }

        var orderedNodes = nodes
            .Where(node => node is not null)
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        var nodeIds = orderedNodes.Select(node => node!.NodeId).ToArray();
        if (nodeIds.Distinct(StringComparer.Ordinal).Count() != orderedNodes.Length)
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.DuplicateResolvedNode,
                "Resolved node identities must be unique.");
        }

        var nodeById = orderedNodes.ToDictionary(
            node => node.NodeId,
            node => node,
            StringComparer.Ordinal);

        var unresolvedTargetSet = new HashSet<string>(StringComparer.Ordinal);
        var edgeByKey = new HashSet<string>(StringComparer.Ordinal);
        var inDegree = orderedNodes.ToDictionary(node => node.NodeId, _ => 0, StringComparer.Ordinal);
        var outDegree = orderedNodes.ToDictionary(node => node.NodeId, _ => 0, StringComparer.Ordinal);

        var orderedEdges = edges
            .Where(edge => edge is not null)
            .Select(edge =>
            {
                if (!nodeById.ContainsKey(edge!.CitingNodeId))
                {
                    throw new CitationNetworkRuleException(
                        CitationNetworkErrorCodes.MissingCitingNode,
                        $"Citation source node '{edge.CitingNodeId}' is not in the resolved node set.");
                }

                if (!edge.IsDirect)
                {
                    throw new CitationNetworkRuleException(
                        CitationNetworkErrorCodes.UnsupportedEdgeKind,
                        $"Unsupported citation edge kind '{edge.Kind}'.");
                }

                if (!edge.Evidence.Digest.IsValid)
                {
                    throw new CitationNetworkRuleException(
                        CitationNetworkErrorCodes.InvalidEvidence,
                        "All citation edges require valid evidence.");
                }

                if (edge.Target.IsResolved)
                {
                    if (!nodeById.ContainsKey(edge.Target.Value))
                    {
                        throw new CitationNetworkRuleException(
                            CitationNetworkErrorCodes.MissingResolvedTarget,
                            $"Resolved citation target '{edge.Target.Value}' has no matching node.");
                    }
                }
                else
                {
                    unresolvedTargetSet.Add(edge.Target.Value);
                }

                if (!edgeByKey.Add(edge.NormalizedEdgeKey))
                {
                    throw new CitationNetworkRuleException(
                        CitationNetworkErrorCodes.DuplicateEdge,
                        "Duplicate normalized citation edge rejected.");
                }

                outDegree[edge.CitingNodeId]++;
                if (edge.Target.IsResolved)
                {
                    inDegree[edge.Target.Value]++;
                }

                return edge;
            })
            .OrderBy(edge => edge!.NormalizedEdgeKey, StringComparer.Ordinal)
            .ToArray();

        var degrees = orderedNodes
            .Select(node => new CitationNetworkNodeDegree(node.NodeId, inDegree[node.NodeId], outDegree[node.NodeId]))
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .ToArray();

        var metrics = new CitationNetworkMetrics(
            orderedNodes.Length,
            orderedEdges.Length,
            degrees.Count(item => item.InDegree == 0 && item.OutDegree == 0),
            Array.AsReadOnly(degrees));

        return new CitationNetworkSnapshot(
            corpusSnapshotId,
            corpusSnapshotDigest,
            algorithmId,
            algorithmVersion,
            Array.AsReadOnly(orderedNodes),
            unresolvedTargetSet.OrderBy(target => target, StringComparer.Ordinal).ToList().AsReadOnly(),
            Array.AsReadOnly(orderedEdges),
            metrics);
    }

    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("schema_id", CitationNetworkSchemas.SnapshotSchemaId)
        .Add("schema_version", CitationNetworkSchemas.SnapshotSchemaVersion)
        .Add("algorithm", new CanonicalJsonObject()
            .Add("id", AlgorithmId)
            .Add("version", AlgorithmVersion))
        .Add("corpus_snapshot_id", CorpusSnapshotId)
        .Add("corpus_snapshot_digest", CorpusSnapshotDigest.ToString())
        .Add("resolved_nodes", CanonicalJsonValue.Array(Nodes.Select(node => node.ToCanonicalJson()).ToArray()))
        .Add("unresolved_targets", CanonicalJsonValue.Array(UnresolvedTargets.Select(CanonicalJsonValue.From).ToArray()))
        .Add("direct_citations", CanonicalJsonValue.Array(Edges.Select(edge => edge.ToCanonicalJson()).ToArray()))
        .Add("metrics", Metrics.ToCanonicalJson());

    public byte[] ToCanonicalBytes() => CanonicalJsonSerializer.SerializeToUtf8Bytes(ToCanonicalJson());

    private DigestEnvelope Envelope() => new(
        DigestScope.CanonicalJsonRecord,
        CitationNetworkSchemas.SnapshotSchemaId,
        CitationNetworkSchemas.SnapshotSchemaVersion,
        ToCanonicalJson());
}

public sealed class CitationNetworkMetrics
{
    public CitationNetworkMetrics(
        int nodeCount,
        int edgeCount,
        int isolatedNodeCount,
        IReadOnlyList<CitationNetworkNodeDegree> nodeDegrees)
    {
        NodeCount = nodeCount;
        EdgeCount = edgeCount;
        IsolatedNodeCount = isolatedNodeCount;
        NodeDegrees = nodeDegrees;
    }

    public int NodeCount { get; }

    public int EdgeCount { get; }

    public int IsolatedNodeCount { get; }

    public IReadOnlyList<CitationNetworkNodeDegree> NodeDegrees { get; }

    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("node_count", NodeCount)
        .Add("edge_count", EdgeCount)
        .Add("isolated_node_count", IsolatedNodeCount)
        .Add("node_degrees", CanonicalJsonValue.Array(
            NodeDegrees.Select(degree => new CanonicalJsonObject()
                    .Add("node_id", degree.NodeId)
                    .Add("in_degree", degree.InDegree)
                    .Add("out_degree", degree.OutDegree))
                .ToArray()));
}
