using System.Reflection;
using System.Runtime.InteropServices;

namespace NexusScholar.Desktop;

public static class DesktopReleaseIdentity
{
    private static readonly Assembly Assembly = typeof(DesktopReleaseIdentity).Assembly;

    public static string Version
    {
        get
        {
            var value = Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(value))
            {
                return Assembly.GetName().Version?.ToString() ?? "unknown";
            }

            var metadata = value.IndexOf('+', StringComparison.Ordinal);
            return metadata < 0 ? value : value[..metadata];
        }
    }

    public static string Framework => RuntimeInformation.FrameworkDescription;

    public static string RuntimeIdentifier => RuntimeInformation.RuntimeIdentifier;

    public static string Architecture => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
}
