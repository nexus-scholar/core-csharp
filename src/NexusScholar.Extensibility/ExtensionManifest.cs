using NexusScholar.Kernel;

namespace NexusScholar.Extensibility;

public sealed record ExtensionManifest
{
    private ExtensionManifest(
        string id,
        string version,
        string entryPoint,
        IReadOnlySet<ExtensionCapability> requestedCapabilities)
    {
        Id = id;
        Version = version;
        EntryPoint = entryPoint;
        RequestedCapabilities = requestedCapabilities;
    }

    public string Id { get; }

    public string Version { get; }

    public string EntryPoint { get; }

    public IReadOnlySet<ExtensionCapability> RequestedCapabilities { get; }

    public static ExtensionManifest Create(
        string id,
        string version,
        string entryPoint,
        IEnumerable<ExtensionCapability> requestedCapabilities)
    {
        ArgumentNullException.ThrowIfNull(requestedCapabilities);
        return new ExtensionManifest(
            Guard.NotBlank(id, nameof(id)),
            Guard.NotBlank(version, nameof(version)),
            Guard.NotBlank(entryPoint, nameof(entryPoint)),
            ExtensionCapabilitySets.Snapshot(requestedCapabilities));
    }
}
