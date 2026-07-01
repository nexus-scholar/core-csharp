using System.Text;
using System.Text.Json;

namespace NexusScholar.Cli.ResearchWorkspace;

internal static class ResearchWorkspaceJson
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static string Serialize(ResearchWorkspaceProject project)
    {
        return JsonSerializer.Serialize(project, SerializerOptions).ReplaceLineEndings("\n");
    }

    public static ResearchWorkspaceProject? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ResearchWorkspaceProject>(json, SerializerOptions);
    }

    public static void WriteProjectFile(string path, ResearchWorkspaceProject project)
    {
        File.WriteAllText(path, Serialize(project) + "\n", Utf8NoBom);
    }
}
