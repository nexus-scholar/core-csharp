using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Desktop.Preview;

public sealed class DesktopPreviewViewModel
{
    private const string LockedDecisionMessage =
        "Decision execution is locked in this preview. No Core record was mutated and no file was written.";

    private readonly Func<string, WorkspaceOverviewReadModel> _buildReadModel;

    public DesktopPreviewViewModel()
        : this(ResearchWorkspaceReadModelBuilder.Build)
    {
    }

    internal DesktopPreviewViewModel(Func<string, WorkspaceOverviewReadModel> buildReadModel)
    {
        _buildReadModel = buildReadModel ?? throw new ArgumentNullException(nameof(buildReadModel));
        Overview = _buildReadModel(Environment.CurrentDirectory);
        WorkspacePath = string.Empty;
        SelectedSection = Overview.State == WorkspaceState.Missing ? Sections[0] : Sections[1];
        StatusMessage = StatusFor(Overview);
    }

    public static IReadOnlyList<DesktopPreviewSection> Sections { get; } = new[]
    {
        new DesktopPreviewSection("welcome", "Open Workspace"),
        new DesktopPreviewSection("overview", "Project Overview"),
        new DesktopPreviewSection("evidence", "Evidence Records"),
        new DesktopPreviewSection("imports", "Imports"),
        new DesktopPreviewSection("verification", "Verification"),
        new DesktopPreviewSection("analysis", "Analysis"),
        new DesktopPreviewSection("review", "Review Queue"),
        new DesktopPreviewSection("clusters", "Duplicate Clusters"),
        new DesktopPreviewSection("detail", "Duplicate Detail"),
        new DesktopPreviewSection("reports", "Reports"),
        new DesktopPreviewSection("diagnostics", "Diagnostics")
    };

    public WorkspaceOverviewReadModel Overview { get; private set; }

    public DesktopPreviewSection SelectedSection { get; private set; }

    public string WorkspacePath { get; private set; }

    public string StatusMessage { get; private set; }

    public IReadOnlyList<string> BoundaryBadges { get; } = new[]
    {
        "local folder",
        "researcher-supplied files",
        "no providers",
        "safe verify/analyze",
        "read-only review",
        "locked merge gates"
    };

    public bool HasWorkspace => Overview.State != WorkspaceState.Missing;

    public void LoadWorkspace(string workspacePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);

        WorkspacePath = workspacePath;
        Overview = _buildReadModel(workspacePath);
        SelectedSection = Overview.State == WorkspaceState.Missing ? Sections[0] : Sections[1];
        StatusMessage = StatusFor(Overview);
    }

    public void SelectSection(string sectionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);

        SelectedSection = Sections.Single(section => string.Equals(section.Id, sectionId, StringComparison.Ordinal));
        StatusMessage = StatusFor(Overview);
    }

    public string InvokeLockedDecisionAction(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        var action = Overview.LockedDecisionActions.SingleOrDefault(candidate =>
            string.Equals(candidate.ActionId, actionId, StringComparison.Ordinal));
        StatusMessage = action is null
            ? "No locked APP-01 merge action was found for that identifier."
            : $"{action.Label}: {LockedDecisionMessage}";

        return StatusMessage;
    }

    public ResearchWorkspaceActionResult RunVerify()
    {
        var path = ActionWorkspacePath();
        var result = ResearchWorkspaceWorkflowActions.Verify(path);
        RefreshAfterAction(path, result, "verification");
        return result;
    }

    public ResearchWorkspaceActionResult RunAnalyze()
    {
        var path = ActionWorkspacePath();
        var result = ResearchWorkspaceWorkflowActions.Analyze(path);
        RefreshAfterAction(path, result, "analysis");
        return result;
    }

    private string ActionWorkspacePath()
    {
        return string.IsNullOrWhiteSpace(WorkspacePath)
            ? Environment.CurrentDirectory
            : WorkspacePath;
    }

    private void RefreshAfterAction(string workspacePath, ResearchWorkspaceActionResult result, string successSectionId)
    {
        Overview = _buildReadModel(workspacePath);
        SelectedSection = result.Completed && HasWorkspace
            ? Sections.Single(section => string.Equals(section.Id, successSectionId, StringComparison.Ordinal))
            : Overview.State == WorkspaceState.Missing ? Sections[0] : SelectedSection;
        StatusMessage = result.Message;
    }

    private static string StatusFor(WorkspaceOverviewReadModel overview)
    {
        return overview.State == WorkspaceState.Missing
            ? "No local Nexus research workspace is loaded. Choose a folder that contains nexus.project.json."
            : $"Desktop preview loaded: {overview.ProjectTitle} ({overview.State}). Location: {overview.ProjectLocation}.";
    }
}

public sealed record DesktopPreviewSection(string Id, string Label);
