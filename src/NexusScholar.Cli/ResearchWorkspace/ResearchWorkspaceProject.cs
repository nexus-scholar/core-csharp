using System.Globalization;
using System.Text.RegularExpressions;

namespace NexusScholar.Cli.ResearchWorkspace;

internal sealed record ResearchWorkspaceProject(
    string Schema,
    string WorkspaceId,
    string Title,
    string CreatedAt,
    IReadOnlyList<ResearchWorkspaceInput> Inputs,
    IReadOnlyDictionary<string, string> Outputs,
    IReadOnlyList<string> NonClaims)
{
    public const string CurrentSchema = "nexus.project.v0";

    private static readonly Regex NonAlphanumericRuns = new("[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static ResearchWorkspaceProject Create(string title, DateTimeOffset createdAt, string? workspaceId = null)
    {
        var normalizedTitle = title.Trim();
        var resolvedWorkspaceId = string.IsNullOrWhiteSpace(workspaceId)
            ? CreateWorkspaceId(normalizedTitle)
            : workspaceId.Trim();

        return new ResearchWorkspaceProject(
            CurrentSchema,
            resolvedWorkspaceId,
            normalizedTitle,
            FormatUtc(createdAt),
            Array.Empty<ResearchWorkspaceInput>(),
            new Dictionary<string, string>(StringComparer.Ordinal),
            new[]
            {
                "local-folder-project",
                "no-live-providers",
                "no-cloud-sync",
                "no-database"
            });
    }

    private static string CreateWorkspaceId(string title)
    {
        var lowercase = title.ToLowerInvariant();
        var slug = NonAlphanumericRuns.Replace(lowercase, "-").Trim('-');

        if (slug.Length == 0)
        {
            throw new ArgumentException("Workspace title must contain at least one letter or digit.", nameof(title));
        }

        return $"workspace-{slug}";
    }

    private static string FormatUtc(DateTimeOffset timestamp)
    {
        return timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }
}
