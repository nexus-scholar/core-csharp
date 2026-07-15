using NexusScholar.Kernel;
using NexusScholar.Screening;
using NexusScholar.Screening.FullText;

namespace NexusScholar.AppServices;

public sealed record FullTextScreeningConductChange(
    FullTextScreeningConductPolicy Policy,
    FullTextScreeningConductHeader Header,
    IReadOnlyList<IFullTextScreeningConductEntry> CurrentEntries,
    IReadOnlyList<IFullTextScreeningConductEntry> ProposedEntries);

public sealed record FullTextScreeningConductPreview(
    string ConductId,
    ContentDigest CurrentHeadDigest,
    ContentDigest ResultingHeadDigest,
    int CurrentEntryCount,
    int ResultingEntryCount,
    IReadOnlyDictionary<string, ScreeningConductOutcome> Outcomes,
    IReadOnlyList<ScreeningConductConflict> Conflicts,
    bool HandoffReady);

public sealed record FullTextScreeningConductCommitResult(
    string ConductId,
    ContentDigest HeadDigest,
    int EntryCount,
    bool AlreadyApplied);

public interface IFullTextScreeningConductCommitPort
{
    FullTextScreeningConductCommitResult Commit(
        FullTextScreeningConductPolicy policy,
        FullTextScreeningConductHeader header,
        IReadOnlyList<IFullTextScreeningConductEntry> entries);
}

public static class FullTextScreeningConductApplicationService
{
    public static FullTextScreeningConductPreview Preview(FullTextScreeningConductChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var current = FullTextScreeningConductJournal.RehydrateEntries(change.Header, change.Policy, change.CurrentEntries);
        var combined = change.CurrentEntries.Concat(change.ProposedEntries).ToArray();
        var resulting = FullTextScreeningConductJournal.RehydrateEntries(change.Header, change.Policy, combined);
        return new FullTextScreeningConductPreview(
            change.Header.ConductId, current.Projection.HeadDigest, resulting.Projection.HeadDigest,
            change.CurrentEntries.Count, combined.Length, resulting.Projection.Outcomes,
            resulting.Projection.Conflicts, resulting.Projection.HandoffReady);
    }

    public static FullTextScreeningConductCommitResult Commit(
        FullTextScreeningConductChange change,
        IFullTextScreeningConductCommitPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        var preview = Preview(change);
        var entries = change.CurrentEntries.Concat(change.ProposedEntries).ToArray();
        var result = port.Commit(change.Policy, change.Header, entries);
        if (result.ConductId != preview.ConductId || result.HeadDigest != preview.ResultingHeadDigest || result.EntryCount != preview.ResultingEntryCount)
            throw new InvalidOperationException("Full Text conduct commit result does not match the validated preview.");
        return result;
    }
}
