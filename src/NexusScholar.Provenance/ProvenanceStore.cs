using System.Collections.ObjectModel;
using System.Linq;
using NexusScholar.Kernel;

namespace NexusScholar.Provenance;

public interface IProvenanceStore
{
    void Append(ResearchEvent researchEvent);

    IReadOnlyList<ResearchEvent> ReadAll();
}

public sealed class InMemoryProvenanceStore : IProvenanceStore
{
    private readonly List<ResearchEvent> _events = new();
    private readonly object _sync = new();

    public void Append(ResearchEvent researchEvent)
    {
        ArgumentNullException.ThrowIfNull(researchEvent);
        lock (_sync)
        {
            ProvenanceEventValidator.Validate(researchEvent, verifyDigest: true);
            if (_events.Any(existing => existing.Id == researchEvent.Id))
            {
                throw new ProvenanceRuleException(
                    ProvenanceErrorCodes.DuplicateEventId,
                    $"Research event '{researchEvent.Id}' already exists.");
            }

            _events.Add(researchEvent.CloneForStore());
        }
    }

    public IReadOnlyList<ResearchEvent> ReadAll()
    {
        lock (_sync)
        {
            return new ReadOnlyCollection<ResearchEvent>(_events.Select(eventRecord => eventRecord.CloneForStore()).ToArray());
        }
    }
}
