using NexusScholar.Kernel;
using NexusScholar.Shared;

namespace NexusScholar.Network;

public sealed class CitationNetworkNode
{
    private CitationNetworkNode(string title, WorkIdSet workIds)
    {
        Title = Guard.NotBlank(title, nameof(title));
        WorkIds = workIds ?? throw new ArgumentNullException(nameof(workIds));
        if (WorkIds.Ids.Count == 0 || WorkIds.Primary is null)
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.InvalidCorpusSnapshot,
                "Network nodes require at least one stable work identifier.");
        }

        NodeId = WorkIds.Primary.Value.ToString();
    }

    public string NodeId { get; }

    public string Title { get; }

    public WorkIdSet WorkIds { get; }

    public static CitationNetworkNode FromWork(ScholarlyWork work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (!work.HasStableIdentifier)
        {
            throw new CitationNetworkRuleException(
                CitationNetworkErrorCodes.InvalidCorpusSnapshot,
                "Network nodes must be resolved works with stable identifiers.");
        }

        return new CitationNetworkNode(work.Title, work.WorkIds);
    }

    public CanonicalJsonObject ToCanonicalJson() => new CanonicalJsonObject()
        .Add("node_id", NodeId)
        .Add("title", Title)
        .Add("work_ids", CanonicalJsonValue.Array(WorkIds.Ids.Select(id => CanonicalJsonValue.From(id.ToString())).ToArray()));
}
