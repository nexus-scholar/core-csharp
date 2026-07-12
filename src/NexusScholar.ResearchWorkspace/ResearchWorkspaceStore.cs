using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NexusScholar.Kernel;

namespace NexusScholar.ResearchWorkspace;

public static class ResearchWorkspaceStore
{
    private static readonly Regex SafeIdentifier = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
        ValidateProject(project);
        ResearchWorkspaceJson.WriteProjectFileAtomic(location.ProjectFilePath, project);
    }

    public static void ValidateProject(ResearchWorkspaceProject project)
    {
        if (!string.Equals(project.Schema, ResearchWorkspaceProject.CurrentSchema, StringComparison.Ordinal))
        {
            throw new JsonException($"Unsupported Nexus project schema: {project.Schema}");
        }

        if (!IsSafeIdentifier(project.WorkspaceId))
        {
            throw new JsonException("Project workspaceId must be a safe identifier.");
        }

        if (string.IsNullOrWhiteSpace(project.Title))
        {
            throw new JsonException("Project title is required.");
        }

        if (!DateTimeOffset.TryParseExact(project.CreatedAt, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _))
        {
            throw new JsonException("Project createdAt must be a UTC timestamp with second precision.");
        }

        if (project.Revision < 0)
        {
            throw new JsonException("Project revision cannot be negative.");
        }

        if (project.Inputs is null)
        {
            throw new JsonException("Project inputs must be an array.");
        }

        if (project.Outputs is null)
        {
            throw new JsonException("Project outputs must be an object.");
        }

        if (project.NonClaims is null || project.NonClaims.Any(string.IsNullOrWhiteSpace) || project.NonClaims.Distinct(StringComparer.Ordinal).Count() != project.NonClaims.Count)
        {
            throw new JsonException("Project nonClaims must contain unique non-empty values.");
        }

        var inputIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var input in project.Inputs)
        {
            if (input is null || !IsSafeIdentifier(input.InputId) || input.Id is not null)
            {
                throw new JsonException("Every input must have one canonical safe inputId.");
            }

            if (!inputIds.Add(input.InputId!))
            {
                throw new JsonException($"Duplicate project inputId: {input.InputId}");
            }

            if (!string.Equals(input.Kind, "search-export", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(input.Source) || string.IsNullOrWhiteSpace(input.Format))
            {
                throw new JsonException($"Input '{input.InputId}' has an unsupported or incomplete type.");
            }

            if (string.IsNullOrWhiteSpace(input.RelativePath) || input.Path is not null ||
                !IsWorkspaceRelative(input.RelativePath) ||
                string.IsNullOrWhiteSpace(input.ImportTracePath) || !IsWorkspaceRelative(input.ImportTracePath))
            {
                throw new JsonException($"Input '{input.InputId}' must use canonical workspace-relative source and trace paths.");
            }

            try
            {
                _ = ContentDigest.Parse(input.Sha256);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                throw new JsonException($"Input '{input.InputId}' has an invalid sha256 digest.", exception);
            }
        }

        foreach (var output in project.Outputs)
        {
            if (string.IsNullOrWhiteSpace(output.Key) || !IsWorkspaceRelative(output.Value))
            {
                throw new JsonException("Project outputs must map non-empty names to workspace-relative paths.");
            }
        }

        if ((project.CurrentGenerationId is null) != (project.GenerationManifestPath is null) ||
            project.CurrentGenerationId is not null && (!IsSafeIdentifier(project.CurrentGenerationId) || !IsWorkspaceRelative(project.GenerationManifestPath!)))
        {
            throw new JsonException("Current generation id and manifest path must be valid and supplied together.");
        }

    }

    private static bool IsSafeIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && SafeIdentifier.IsMatch(value) && !value.Contains("..", StringComparison.Ordinal);

    private static bool IsWorkspaceRelative(string value) =>
        !string.IsNullOrWhiteSpace(value) && !Path.IsPathFullyQualified(value) &&
        !value.Split('/', '\\').Any(segment => segment is "" or "." or "..");
}
