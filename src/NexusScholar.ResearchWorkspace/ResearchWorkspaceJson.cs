using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceJson
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

    public static void WriteProjectFileAtomic(string path, ResearchWorkspaceProject project)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            WriteProjectFile(temporaryPath, project);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public static void WriteJsonFile<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, TraceSerializerOptions).ReplaceLineEndings("\n");
        File.WriteAllText(path, json + "\n", Utf8NoBom);
    }

    public static void WriteJsonFile<T>(string path, T value, JsonSerializerOptions options)
    {
        var json = JsonSerializer.Serialize(value, options).ReplaceLineEndings("\n");
        File.WriteAllText(path, json + "\n", Utf8NoBom);
    }

    public static void WriteTextFile(string path, string value)
    {
        File.WriteAllText(path, value.ReplaceLineEndings("\n"), Utf8NoBom);
    }
}
