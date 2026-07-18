using NexusScholar.Kernel;

namespace NexusScholar.Extensibility;

public sealed record CapabilitySelection
{
    private CapabilitySelection(
        string extensionId,
        IReadOnlySet<ExtensionCapability> capabilities)
    {
        ExtensionId = extensionId;
        Capabilities = capabilities;
    }

    public string ExtensionId { get; }

    public IReadOnlySet<ExtensionCapability> Capabilities { get; }

    public static CapabilitySelection Create(
        ExtensionManifest manifest,
        IEnumerable<ExtensionCapability> capabilities)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(capabilities);
        var selected = ExtensionCapabilitySets.Snapshot(capabilities);
        if (selected.Any(capability => !manifest.RequestedCapabilities.Contains(capability)))
        {
            throw new DomainRuleException(
                "Selected extension capabilities must be a subset of the manifest request.");
        }

        return new CapabilitySelection(
            manifest.Id,
            selected);
    }
}
