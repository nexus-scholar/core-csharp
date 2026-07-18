using System.Collections.Frozen;
using NexusScholar.Kernel;

namespace NexusScholar.Extensibility;

public enum ExtensionCapability
{
    ReadProtocol,
    ReadArtifacts,
    WriteStagedArtifacts,
    RenderExport
}

internal static class ExtensionCapabilitySets
{
    public static IReadOnlySet<ExtensionCapability> Snapshot(
        IEnumerable<ExtensionCapability> capabilities)
    {
        var snapshot = capabilities.ToArray();
        if (snapshot.Any(capability => !Enum.IsDefined(capability)))
        {
            throw new DomainRuleException("Extension capabilities must use defined values.");
        }

        return snapshot.ToFrozenSet();
    }
}
