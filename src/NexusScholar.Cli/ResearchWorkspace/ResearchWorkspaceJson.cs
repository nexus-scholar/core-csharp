using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceJson
{
    private static readonly JsonSerializerOptions ProjectSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions TraceSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static string Serialize(ResearchWorkspaceProject project)
    {
        return JsonSerializer.Serialize(project, ProjectSerializerOptions).ReplaceLineEndings("\n");
    }

    public static ResearchWorkspaceProject? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ResearchWorkspaceProject>(json, ProjectSerializerOptions);
    }

    public static void WriteProjectFile(string path, ResearchWorkspaceProject project)
    {
        File.WriteAllText(path, Serialize(project) + "\n", Utf8NoBom);
    }

    public static void WriteJsonFile<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, TraceSerializerOptions).ReplaceLineEndings("\n");
        File.WriteAllText(path, json + "\n", Utf8NoBom);
    }
}
