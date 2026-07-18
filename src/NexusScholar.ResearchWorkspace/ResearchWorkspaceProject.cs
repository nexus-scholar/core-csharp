using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NexusScholar.ResearchWorkspace;

public sealed record ResearchWorkspaceFullTextPointer(
    string GenerationId,
    string ManifestPath,
    string ManifestSha256);

public sealed record ResearchWorkspaceProject(
    string Schema,
    string WorkspaceId,
    string Title,
    string CreatedAt,
    IReadOnlyList<ResearchWorkspaceInput> Inputs,
    IReadOnlyDictionary<string, string> Outputs,
    IReadOnlyList<string> NonClaims,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] long Revision = 0,
    string? CurrentGenerationId = null,
    string? GenerationManifestPath = null,
    string? CurrentAuthorityGenerationId = null,
    string? AuthorityGenerationManifestPath = null,
    string? AuthorityGenerationManifestSha256 = null,
    string? CurrentWorkflowExecutionJournalGenerationId = null,
    string? WorkflowExecutionJournalManifestPath = null,
    string? WorkflowExecutionJournalManifestSha256 = null,
    string? CurrentScreeningConductGenerationId = null,
    string? ScreeningConductManifestPath = null,
    string? ScreeningConductManifestSha256 = null,
    string? CurrentScreeningAuthorityPackageGenerationId = null,
    string? ScreeningAuthorityPackageManifestPath = null,
    string? ScreeningAuthorityPackageManifestSha256 = null,
    string? CurrentFullTextGenerationId = null,
    string? FullTextManifestPath = null,
    string? FullTextManifestSha256 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, ResearchWorkspaceFullTextPointer>? FullTextCases = null,
    string? CurrentReportingWorkflowGenerationId = null,
    string? ReportingWorkflowManifestPath = null,
    string? ReportingWorkflowManifestSha256 = null)
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

    public ResearchWorkspaceProject WithInput(ResearchWorkspaceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return this with { Inputs = Inputs.Concat(new[] { input }).ToArray() };
    }

    public ResearchWorkspaceProject WithOutputs(IReadOnlyDictionary<string, string> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        return this with { Outputs = new Dictionary<string, string>(outputs, StringComparer.Ordinal) };
    }

    public ResearchWorkspaceProject CommitGeneration(
        IReadOnlyDictionary<string, string> outputs,
        string generationId,
        string manifestPath) => this with
        {
            Outputs = new Dictionary<string, string>(outputs, StringComparer.Ordinal),
            Revision = checked(Revision + 1),
            CurrentGenerationId = generationId,
            GenerationManifestPath = manifestPath
        };

    public ResearchWorkspaceProject CommitAuthorityGeneration(
        string authorityGenerationId,
        string manifestPath,
        string manifestSha256) => this with
        {
            Revision = checked(Revision + 1),
            CurrentAuthorityGenerationId = authorityGenerationId,
            AuthorityGenerationManifestPath = manifestPath,
            AuthorityGenerationManifestSha256 = manifestSha256
        };

    public ResearchWorkspaceProject CommitWorkflowExecutionJournalGeneration(
        string generationId,
        string manifestPath,
        string manifestSha256) => this with
        {
            Revision = checked(Revision + 1),
            CurrentWorkflowExecutionJournalGenerationId = generationId,
            WorkflowExecutionJournalManifestPath = manifestPath,
            WorkflowExecutionJournalManifestSha256 = manifestSha256
        };

    public ResearchWorkspaceProject CommitScreeningConductGeneration(
        string generationId,
        string manifestPath,
        string manifestSha256) => this with
        {
            Revision = checked(Revision + 1),
            CurrentScreeningConductGenerationId = generationId,
            ScreeningConductManifestPath = manifestPath,
            ScreeningConductManifestSha256 = manifestSha256
        };

    public ResearchWorkspaceProject CommitScreeningAuthorityPackageGeneration(
        string generationId,
        string manifestPath,
        string manifestSha256) => this with
        {
            Revision = checked(Revision + 1),
            CurrentScreeningAuthorityPackageGenerationId = generationId,
            ScreeningAuthorityPackageManifestPath = manifestPath,
            ScreeningAuthorityPackageManifestSha256 = manifestSha256
        };

    public ResearchWorkspaceProject CommitScreeningAndWorkflowExecutionGenerations(
        string screeningGenerationId,
        string screeningManifestPath,
        string screeningManifestSha256,
        string workflowGenerationId,
        string workflowManifestPath,
        string workflowManifestSha256) => this with
        {
            Revision = checked(Revision + 1),
            CurrentScreeningConductGenerationId = screeningGenerationId,
            ScreeningConductManifestPath = screeningManifestPath,
            ScreeningConductManifestSha256 = screeningManifestSha256,
            CurrentWorkflowExecutionJournalGenerationId = workflowGenerationId,
            WorkflowExecutionJournalManifestPath = workflowManifestPath,
            WorkflowExecutionJournalManifestSha256 = workflowManifestSha256
        };

    public ResearchWorkspaceProject CommitFullTextGeneration(
        string candidateId,
        string generationId,
        string manifestPath,
        string manifestSha256)
    {
        var cases = new Dictionary<string, ResearchWorkspaceFullTextPointer>(
            FullTextCases ?? new Dictionary<string, ResearchWorkspaceFullTextPointer>(StringComparer.Ordinal),
            StringComparer.Ordinal)
        {
            [candidateId] = new(generationId, manifestPath, manifestSha256)
        };
        return this with
        {
            Revision = checked(Revision + 1),
            CurrentFullTextGenerationId = generationId,
            FullTextManifestPath = manifestPath,
            FullTextManifestSha256 = manifestSha256,
            FullTextCases = cases
        };
    }

    public ResearchWorkspaceProject CommitReportingWorkflowGeneration(
        string generationId,
        string manifestPath,
        string manifestSha256) => this with
        {
            Revision = checked(Revision + 1),
            CurrentReportingWorkflowGenerationId = generationId,
            ReportingWorkflowManifestPath = manifestPath,
            ReportingWorkflowManifestSha256 = manifestSha256
        };

    public ResearchWorkspaceProject CommitFullTextGeneration(
        string generationId,
        string manifestPath,
        string manifestSha256) => this with
        {
            Revision = checked(Revision + 1),
            CurrentFullTextGenerationId = generationId,
            FullTextManifestPath = manifestPath,
            FullTextManifestSha256 = manifestSha256
        };

    internal ResearchWorkspaceProject ClearSuccessorBoundDownstreamCurrentState() => this with
    {
        CurrentWorkflowExecutionJournalGenerationId = null,
        WorkflowExecutionJournalManifestPath = null,
        WorkflowExecutionJournalManifestSha256 = null,
        CurrentScreeningConductGenerationId = null,
        ScreeningConductManifestPath = null,
        ScreeningConductManifestSha256 = null,
        CurrentScreeningAuthorityPackageGenerationId = null,
        ScreeningAuthorityPackageManifestPath = null,
        ScreeningAuthorityPackageManifestSha256 = null,
        CurrentFullTextGenerationId = null,
        FullTextManifestPath = null,
        FullTextManifestSha256 = null,
        FullTextCases = null,
        CurrentReportingWorkflowGenerationId = null,
        ReportingWorkflowManifestPath = null,
        ReportingWorkflowManifestSha256 = null
    };

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
