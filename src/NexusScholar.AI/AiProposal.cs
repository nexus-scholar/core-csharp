using System.Collections.ObjectModel;
using System.Text.Json;
using NexusScholar.Kernel;

namespace NexusScholar.AI;

public sealed record AiProposal<T>
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.General);
    private readonly byte[] valueSnapshot;

    public AiProposal(
        AiTaskPolicy policy,
        T value,
        IReadOnlyList<ContentDigest> evidence,
        DateTimeOffset createdAt)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (value is null)
        {
            throw new DomainRuleException("AI proposal value must not be null.");
        }

        valueSnapshot = Snapshot(value);
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Any(item => !item.IsValid))
        {
            throw new DomainRuleException("AI proposal evidence must use valid content digests.");
        }
        if (policy.EvidenceRequired && evidence.Count == 0)
        {
            throw new DomainRuleException("This AI task policy requires proposal evidence.");
        }
        if (createdAt == default || createdAt.Offset != TimeSpan.Zero)
        {
            throw new DomainRuleException("AI proposal creation time must be a non-default UTC timestamp.");
        }

        Evidence = new ReadOnlyCollection<ContentDigest>(evidence.ToArray());
        CreatedAt = createdAt;
    }

    public AiTaskPolicy Policy { get; }

    public string TaskType => Policy.TaskType;

    public T Value => RehydrateValue();

    public IReadOnlyList<ContentDigest> Evidence { get; }

    public DateTimeOffset CreatedAt { get; }

    private static byte[] Snapshot(T value)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, SnapshotOptions);
            _ = Rehydrate(bytes);
            return bytes;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new DomainRuleException($"AI proposal value cannot be snapshotted: {exception.Message}");
        }
    }

    private T RehydrateValue()
    {
        try
        {
            return Rehydrate(valueSnapshot);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new DomainRuleException($"AI proposal value snapshot cannot be rehydrated: {exception.Message}");
        }
    }

    private static T Rehydrate(byte[] bytes) =>
        JsonSerializer.Deserialize<T>(bytes, SnapshotOptions)
        ?? throw new DomainRuleException("AI proposal value snapshot cannot rehydrate to null.");
}
