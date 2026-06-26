using NexusScholar.Kernel;

namespace NexusScholar.Artifacts;

public sealed class ArtifactTag
{
}

public sealed record ArtifactDescriptor(
    EntityId<ArtifactTag> Id,
    string MediaType,
    long SizeBytes,
    ContentDigest Digest,
    string LogicalName)
{
    public static ArtifactDescriptor Create(
        IIdGenerator ids,
        string mediaType,
        long sizeBytes,
        ContentDigest digest,
        string logicalName)
    {
        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        }

        return new ArtifactDescriptor(
            EntityId<ArtifactTag>.New(ids),
            Guard.NotBlank(mediaType, nameof(mediaType)),
            sizeBytes,
            digest,
            Guard.NotBlank(logicalName, nameof(logicalName)));
    }
}
