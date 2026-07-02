using System.Text.Json;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceStore
{
    public static ResearchWorkspaceLocation? FindFrom(string startingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startingDirectory);

        var current = new DirectoryInfo(Path.GetFullPath(startingDirectory));
        while (current is not null)
        {
            var projectFile = Path.Combine(current.FullName, ResearchWorkspacePaths.ProjectFileName);
            if (File.Exists(projectFile))
            {
                return new ResearchWorkspaceLocation(current.FullName, projectFile);
            }

            current = current.Parent;
        }

        return null;
    }

    public static ResearchWorkspaceProject ReadProject(string projectFile)
    {
        var project = ResearchWorkspaceJson.Deserialize(File.ReadAllText(projectFile));
        if (project is null)
        {
            throw new JsonException("Project file did not contain an object.");
        }

        ValidateProject(project);
        return project;
    }

    public static void WriteProject(ResearchWorkspaceLocation location, ResearchWorkspaceProject project)
    {
        ResearchWorkspaceJson.WriteProjectFile(location.ProjectFilePath, project);
    }

    private static void ValidateProject(ResearchWorkspaceProject project)
    {
        if (string.IsNullOrWhiteSpace(project.Schema))
        {
            throw new JsonException("Project schema is required.");
        }

        if (string.IsNullOrWhiteSpace(project.WorkspaceId))
        {
            throw new JsonException("Project workspaceId is required.");
        }

        if (string.IsNullOrWhiteSpace(project.Title))
        {
            throw new JsonException("Project title is required.");
        }

        if (project.Inputs is null)
        {
            throw new JsonException("Project inputs must be an array.");
        }

        if (project.Outputs is null)
        {
            throw new JsonException("Project outputs must be an object.");
        }
    }
}
