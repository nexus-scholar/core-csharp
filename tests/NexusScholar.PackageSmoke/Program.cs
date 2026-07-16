using System.Reflection;
using System.Text.Json;

var topologyPath = Path.Combine(AppContext.BaseDirectory, "package-topology.json");
using var topology = JsonDocument.Parse(File.ReadAllBytes(topologyPath));
var expectedAssemblies = topology.RootElement.GetProperty("packages")
    .EnumerateArray()
    .Select(package => package.GetString() ?? throw new InvalidDataException("Package id cannot be null."))
    .ToArray();

foreach (var assemblyName in expectedAssemblies)
{
    var assembly = Assembly.Load(assemblyName);
    if (!string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Loaded assembly identity did not match '{assemblyName}'.");
    }
}

Console.WriteLine($"Loaded {expectedAssemblies.Length} Nexus Scholar package assemblies from the local package source.");
