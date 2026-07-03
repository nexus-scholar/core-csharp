using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NexusScholar.ResearchWorkspace;

namespace NexusScholar.Desktop.Preview;

public sealed class MainWindow : Window
{
    private static readonly SolidColorBrush BackgroundBrush = new(Color.Parse("#f6f7f9"));
    private static readonly SolidColorBrush PrimaryBrush = new(Color.Parse("#0f766e"));
    private static readonly SolidColorBrush PrimaryDarkBrush = new(Color.Parse("#134e4a"));
    private static readonly SolidColorBrush AccentBrush = new(Color.Parse("#2563eb"));
    private static readonly SolidColorBrush PanelBrush = new(Color.Parse("#ffffff"));
    private static readonly SolidColorBrush MutedPanelBrush = new(Color.Parse("#eef2f6"));
    private static readonly SolidColorBrush SubtlePanelBrush = new(Color.Parse("#f8fafc"));
    private static readonly SolidColorBrush PanelBorderBrush = new(Color.Parse("#d8dee8"));
    private static readonly SolidColorBrush WarningBrush = new(Color.Parse("#b45309"));
    private static readonly SolidColorBrush TextBrush = new(Color.Parse("#111827"));
    private static readonly SolidColorBrush MutedTextBrush = new(Color.Parse("#5f6b7a"));
    private static readonly SolidColorBrush DisabledTextBrush = new(Color.Parse("#94a3b8"));

    private readonly DesktopPreviewViewModel _viewModel;
    private readonly Dictionary<string, Button> _navigationButtons = new(StringComparer.Ordinal);
    private readonly TextBlock _pathText = new()
    {
        MinHeight = 38,
        Foreground = TextBrush,
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(10, 0)
    };
    private readonly StackPanel _content = new()
    {
        Spacing = 16,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly TextBlock _status = new()
    {
        TextWrapping = TextWrapping.Wrap
    };
    private Button? _verifyButton;
    private Button? _analyzeButton;
    private Border? _hostRoot;
    private string _workspacePath = string.Empty;

    public MainWindow()
        : this(new DesktopPreviewViewModel(), initialWorkspacePath: null)
    {
    }

    public MainWindow(DesktopPreviewViewModel viewModel, string? initialWorkspacePath = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        Title = "Nexus Scholar Desktop Preview";
        Width = 1280;
        Height = 860;
        MinWidth = 940;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BackgroundBrush;

        SetWorkspacePath(string.IsNullOrWhiteSpace(initialWorkspacePath)
            ? Environment.CurrentDirectory
            : initialWorkspacePath);
        Content = BuildContent();
        Opened += (_, _) => ApplyHostClientSize();
        SizeChanged += (_, _) => ApplyHostClientSize();

        if (string.IsNullOrWhiteSpace(initialWorkspacePath))
        {
            Render();
        }
        else
        {
            LoadSelectedWorkspace();
        }
    }

    internal static Border BuildHostLayout(Control header, Control navigation, Control workspaceView, Control statusBar)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(workspaceView);
        ArgumentNullException.ThrowIfNull(statusBar);

        var layout = new Grid
        {
            Margin = new Thickness(18),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var workspace = new Grid
        {
            ColumnSpacing = 16,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(260)));
        workspace.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var workspaceScroller = new ScrollViewer
        {
            ClipToBounds = true,
            Content = workspaceView,
            Focusable = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        workspaceScroller.HorizontalAlignment = HorizontalAlignment.Stretch;
        workspaceScroller.VerticalAlignment = VerticalAlignment.Stretch;
        workspaceView.HorizontalAlignment = HorizontalAlignment.Stretch;
        workspaceView.VerticalAlignment = VerticalAlignment.Top;

        Grid.SetColumn(navigation, 0);
        Grid.SetColumn(workspaceScroller, 1);
        workspace.Children.Add(navigation);
        workspace.Children.Add(workspaceScroller);

        Grid.SetRow(header, 0);
        Grid.SetRow(workspace, 1);
        Grid.SetRow(statusBar, 2);
        layout.Children.Add(header);
        layout.Children.Add(workspace);
        layout.Children.Add(statusBar);

        return new Border
        {
            Background = BackgroundBrush,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = layout
        };
    }

    private Control BuildContent()
    {
        _status.Foreground = TextBrush;
        _status.Text = _viewModel.StatusMessage;

        var header = BuildHeader();
        var navigation = BuildNavigation();
        var statusBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#fff8e7")),
            BorderBrush = new SolidColorBrush(Color.Parse("#f2c97d")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 10),
            Margin = new Thickness(0, 14, 0, 0),
            Child = _status
        };

        _hostRoot = BuildHostLayout(header, navigation, _content, statusBar);
        return _hostRoot;
    }

    private Control BuildHeader()
    {
        var title = new TextBlock
        {
            Text = "Nexus Scholar Preview",
            FontSize = 26,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextBrush,
            TextWrapping = TextWrapping.Wrap
        };
        var subtitle = new TextBlock
        {
            Text = "Inspect a local Research Workspace, run safe local verify/analyze actions, and keep APP-01 merge gates locked.",
            FontSize = 14,
            Foreground = MutedTextBrush,
            TextWrapping = TextWrapping.Wrap
        };

        var badges = new WrapPanel
        {
            ItemSpacing = 8,
            LineSpacing = 8
        };
        foreach (var badge in _viewModel.BoundaryBadges)
        {
            badges.Children.Add(Badge(badge, new SolidColorBrush(Color.Parse("#e6f5f2")), PrimaryDarkBrush));
        }

        var openButton = PrimaryButton("Load");
        openButton.Click += (_, _) => LoadSelectedWorkspace();

        var currentButton = SecondaryButton("Current folder");
        currentButton.Click += (_, _) =>
        {
            SetWorkspacePath(Environment.CurrentDirectory);
            LoadWorkspaceFromPath(_workspacePath);
        };

        var browseButton = SecondaryButton("Browse...");
        browseButton.Click += async (_, _) => await BrowseForWorkspaceAsync();

        _verifyButton = SecondaryButton("Verify");
        _verifyButton.Click += (_, _) => RunVerifyAction();

        _analyzeButton = PrimaryButton("Analyze");
        _analyzeButton.Click += (_, _) => RunAnalyzeAction();

        var pathLabel = new TextBlock
        {
            Text = "Workspace folder",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = MutedTextBrush
        };

        var pathRow = new Grid
        {
            ColumnSpacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var pathFrame = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#edf2f7")),
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Child = _pathText
        };

        Grid.SetColumn(pathFrame, 0);
        Grid.SetColumn(openButton, 1);
        Grid.SetColumn(currentButton, 2);
        Grid.SetColumn(browseButton, 3);
        pathRow.Children.Add(pathFrame);
        pathRow.Children.Add(openButton);
        pathRow.Children.Add(currentButton);
        pathRow.Children.Add(browseButton);

        var actionRow = new WrapPanel
        {
            ItemSpacing = 10,
            LineSpacing = 10
        };
        actionRow.Children.Add(_verifyButton);
        actionRow.Children.Add(_analyzeButton);
        actionRow.Children.Add(new TextBlock
        {
            Text = "UI-02A: local verify/analyze only. Import, init, and merge decisions remain outside this preview.",
            FontSize = 12,
            Foreground = MutedTextBrush,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });

        var stack = new StackPanel
        {
            Spacing = 12
        };
        stack.Children.Add(title);
        stack.Children.Add(subtitle);
        stack.Children.Add(badges);
        stack.Children.Add(pathLabel);
        stack.Children.Add(pathRow);
        stack.Children.Add(actionRow);

        return new Border
        {
            Background = PanelBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 16),
            Child = stack
        };
    }

    private Control BuildNavigation()
    {
        _navigationButtons.Clear();
        var list = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        list.Children.Add(new TextBlock
        {
            Text = "Workspace",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = MutedTextBrush,
            Margin = new Thickness(4, 0, 0, 2)
        });

        foreach (var section in DesktopPreviewViewModel.Sections)
        {
            var sectionId = section.Id;
            var button = NavigationButton(section);
            button.PointerPressed += (_, e) =>
            {
                SelectNavigationSection(sectionId);
                e.Handled = true;
            };
            button.Click += (_, _) => SelectNavigationSection(sectionId);
            button.Tapped += (_, _) => SelectNavigationSection(sectionId);
            _navigationButtons[sectionId] = button;
            list.Children.Add(button);
        }

        return new Border
        {
            Background = PanelBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = list
        };
    }

    private void SelectNavigationSection(string sectionId)
    {
        if (!_viewModel.HasWorkspace && !string.Equals(sectionId, "welcome", StringComparison.Ordinal))
        {
            _status.Text = "Load a local Nexus research workspace before opening this section.";
            return;
        }

        _viewModel.SelectSection(sectionId);
        Render();
    }

    private void UpdateNavigationState()
    {
        foreach (var section in DesktopPreviewViewModel.Sections)
        {
            if (!_navigationButtons.TryGetValue(section.Id, out var button))
            {
                continue;
            }

            var enabled = _viewModel.HasWorkspace || string.Equals(section.Id, "welcome", StringComparison.Ordinal);
            var active = string.Equals(section.Id, _viewModel.SelectedSection.Id, StringComparison.Ordinal);
            button.IsEnabled = enabled;
            button.Background = active
                ? PrimaryBrush
                : enabled ? Brushes.Transparent : SubtlePanelBrush;
            button.Foreground = active
                ? Brushes.White
                : enabled ? TextBrush : DisabledTextBrush;
            button.BorderBrush = active ? PrimaryBrush : PanelBorderBrush;
            button.BorderThickness = new Thickness(active ? 1 : 0);
        }
    }

    private void UpdateActionState()
    {
        var enabled = _viewModel.HasWorkspace;
        if (_verifyButton is not null)
        {
            _verifyButton.IsEnabled = enabled;
        }

        if (_analyzeButton is not null)
        {
            _analyzeButton.IsEnabled = enabled;
        }
    }

    private static Button NavigationButton(DesktopPreviewSection section)
    {
        return new Button
        {
            Content = section.Label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = 36,
            Padding = new Thickness(10, 7),
            FontSize = 13,
            Tag = section.Id
        };
    }

    private async Task BrowseForWorkspaceAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Nexus research workspace",
            AllowMultiple = false
        });

        var selected = folders.FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        SetWorkspacePath(selected.Path.LocalPath);
        LoadWorkspaceFromPath(_workspacePath);
    }

    private void LoadSelectedWorkspace()
    {
        LoadWorkspaceFromPath(_workspacePath);
    }

    private void LoadWorkspaceFromPath(string path)
    {
        path = path.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            _status.Text = "Enter or select an existing local workspace folder.";
            return;
        }

        try
        {
            _viewModel.LoadWorkspace(path);
            SetWorkspacePath(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            _status.Text = $"Workspace could not be loaded: {ex.Message}";
            return;
        }

        Render();
    }

    private void RunVerifyAction()
    {
        _viewModel.RunVerify();
        Render();
    }

    private void RunAnalyzeAction()
    {
        _viewModel.RunAnalyze();
        Render();
    }

    private void SetWorkspacePath(string path)
    {
        _workspacePath = path;
        _pathText.Text = string.IsNullOrWhiteSpace(path)
            ? "No folder selected"
            : path;
    }

    private void Render()
    {
        _content.Children.Clear();
        _status.Text = _viewModel.StatusMessage;
        UpdateNavigationState();
        UpdateActionState();

        _content.Children.Add(BuildStateSummary());
        switch (_viewModel.SelectedSection.Id)
        {
            case "welcome":
                RenderWelcome();
                break;
            case "overview":
                RenderOverview();
                break;
            case "evidence":
                RenderEvidenceRecords();
                break;
            case "imports":
                RenderImports();
                break;
            case "verification":
                RenderVerification();
                break;
            case "analysis":
                RenderAnalysis();
                break;
            case "review":
                RenderReviewQueue();
                break;
            case "clusters":
                RenderDuplicateClusters();
                break;
            case "detail":
                RenderDuplicateDetail();
                break;
            case "reports":
                RenderReports();
                break;
            case "diagnostics":
                RenderDiagnostics();
                break;
        }
    }

    private Control BuildStateSummary()
    {
        var overview = _viewModel.Overview;
        var row = new WrapPanel
        {
            ItemSpacing = 10,
            LineSpacing = 10
        };
        row.Children.Add(Badge($"State: {overview.State}", StateBrush(overview.State), Brushes.White));
        row.Children.Add(Badge($"Project: {overview.ProjectTitle ?? "not loaded"}", SubtlePanelBrush, TextBrush));
        row.Children.Add(Badge($"Workspace: {overview.WorkspaceId ?? "missing"}", SubtlePanelBrush, TextBrush));
        row.Children.Add(Badge($"Location: {overview.ProjectLocation}", SubtlePanelBrush, TextBrush));

        return Panel(row);
    }

    private void RenderWelcome()
    {
        _content.Children.Add(Panel(Stack(
            Heading("Open Workspace"),
            Paragraph("Open a folder that contains nexus.project.json. The preview can run local verify/analyze actions, but it never runs init, import, or merge decisions."),
            Paragraph(_viewModel.HasWorkspace
                ? "Workspace loaded. Use the sidebar to inspect evidence, imports, verification, analysis, review items, clusters, and locked decision gates."
                : "No workspace is loaded yet."))));
    }

    private void RenderOverview()
    {
        var overview = _viewModel.Overview;
        _content.Children.Add(Panel(Stack(
            Heading("Project Overview"),
            MetricGrid(new[]
            {
                Metric("Inputs", overview.Verification.InputCount.ToString()),
                Metric("Records", overview.EvidenceRecords.Count.ToString()),
                Metric("Warnings", overview.Verification.ParserWarningCount.ToString()),
                Metric("Review Items", overview.ReviewQueue.Count.ToString()),
                Metric("Exact Clusters", overview.DuplicateClusters.Count.ToString()),
                Metric("Locked Actions", overview.LockedDecisionActions.Count.ToString())
            }))));

        if (overview.AttentionItems.Count > 0)
        {
            _content.Children.Add(Panel(Stack(
                Heading("Needs Attention"),
                ListLines(overview.AttentionItems.Select(item =>
                    $"{item.Code}: {item.Message} {item.Target}".Trim())))));
        }

        _content.Children.Add(Panel(Stack(
            Heading("Workflow"),
            ListLines(overview.WorkflowSteps.Select(step =>
                $"{step.Label}: {step.State}{(step.NextCommand is null ? string.Empty : $" ({step.NextCommand})")}")))));
    }

    private void RenderEvidenceRecords()
    {
        var records = _viewModel.Overview.EvidenceRecords;
        _content.Children.Add(Panel(Stack(
            Heading("Evidence Records"),
            Paragraph($"{records.Count} imported local evidence record(s)."),
            Table(
                new[] { "Title", "Creators", "Year", "Source", "Identifier", "Warnings", "Duplicate State" },
                records.Take(50).Select(record => new[]
                {
                    record.Title,
                    record.Creators,
                    record.Year?.ToString() ?? "",
                    record.Source,
                    record.Identifier ?? "",
                    record.WarningCount.ToString(),
                    record.DuplicateState
                })))));
    }

    private void RenderImports()
    {
        _content.Children.Add(Panel(Stack(
            Heading("Imports"),
            Table(
                new[] { "Import ID", "Source", "Format", "Records", "Imported", "Warnings", "Skipped", "Relative Path" },
                _viewModel.Overview.Imports.Select(import => new[]
                {
                    import.ImportId,
                    import.Source,
                    import.Format,
                    import.RecordCount.ToString(),
                    import.ImportedRecordCount.ToString(),
                    import.ParserWarningCount.ToString(),
                    import.SkippedRecordCount.ToString(),
                    import.RelativePath ?? ""
                })))));
    }

    private void RenderVerification()
    {
        var verification = _viewModel.Overview.Verification;
        _content.Children.Add(Panel(Stack(
            Heading("Verification"),
            MetricGrid(new[]
            {
                Metric("Inputs", verification.InputCount.ToString()),
                Metric("Files unchanged", verification.FilesUnchanged.ToString()),
                Metric("Missing files", verification.MissingFileCount.ToString()),
                Metric("Digest mismatches", verification.DigestMismatchCount.ToString()),
                Metric("Invalid paths", verification.InvalidPathCount.ToString()),
                Metric("Missing traces", verification.MissingImportTraceCount.ToString()),
                Metric("Parser warnings", verification.ParserWarningCount.ToString()),
                Metric("Skipped records", verification.SkippedRecordCount.ToString())
            }))));

        if (_viewModel.Overview.AttentionItems.Count > 0)
        {
            _content.Children.Add(Panel(Stack(
                Heading("Recovery"),
                ListLines(_viewModel.Overview.AttentionItems.Select(RecoveryLine)))));
        }
    }

    private void RenderAnalysis()
    {
        var analysis = _viewModel.Overview.Analysis;
        _content.Children.Add(Panel(Stack(
            Heading("Analysis"),
            MetricGrid(new[]
            {
                Metric("Dedup result", Present(analysis.DeduplicationResultPresent)),
                Metric("Workspace plan", Present(analysis.WorkspacePlanPresent)),
                Metric("Review report", Present(analysis.ReviewReportPresent)),
                Metric("Exact clusters", analysis.ExactDuplicateClusterCount.ToString()),
                Metric("Review candidates", analysis.ReviewRequiredCandidateCount.ToString()),
                Metric("Blocking gates", analysis.BlockingMergeGateCount.ToString())
            }))));
    }

    private void RenderReviewQueue()
    {
        var queue = _viewModel.Overview.ReviewQueue;
        _content.Children.Add(HeadingPanel("Review Queue", $"{queue.Count} review-required candidate pair(s)."));
        foreach (var item in queue)
        {
            var actions = item.LockedActions.Count == 0
                ? Paragraph("No locked action descriptors were attached to this item.")
                : LockedActionList(item.LockedActions.Select(action => action.Label));
            _content.Children.Add(Panel(Stack(new Control[]
            {
                Heading(item.Title),
                Paragraph($"Candidate pair: {item.CandidatePairId}"),
                Paragraph($"Title similarity: {item.TitleSimilarity:0.000} / threshold {item.ThresholdUsed:0.000}"),
                actions
            })));
        }
    }

    private void RenderDuplicateClusters()
    {
        var clusters = _viewModel.Overview.DuplicateClusters;
        _content.Children.Add(Panel(Stack(
            Heading("Duplicate Clusters"),
            Table(
                new[] { "Cluster", "Representative Title", "Members", "Evidence", "Review Required" },
                clusters.Select(cluster => new[]
                {
                    cluster.ClusterId,
                    cluster.RepresentativeTitle,
                    cluster.MemberCount.ToString(),
                    cluster.EvidenceCount.ToString(),
                    YesNo(cluster.ReviewRequired)
                })))));
    }

    private void RenderDuplicateDetail()
    {
        var detail = _viewModel.Overview.DuplicateCandidateDetails.FirstOrDefault();
        if (detail is null)
        {
            _content.Children.Add(HeadingPanel("Duplicate Detail", "No review-required duplicate candidate detail is available."));
            return;
        }

        var split = new Grid
        {
            ColumnSpacing = 12
        };
        split.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        split.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        var left = CandidatePanel("Candidate A", detail.CandidateA);
        var right = CandidatePanel("Candidate B", detail.CandidateB);
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        split.Children.Add(left);
        split.Children.Add(right);

        _content.Children.Add(Panel(Stack(
            Heading("Duplicate Detail"),
            Paragraph($"Pair: {detail.CandidatePairId}"),
            Paragraph($"Title similarity: {detail.TitleSimilarity:0.000} / threshold {detail.ThresholdUsed:0.000}"),
            Heading("Locked Actions"),
            LockedActionList(detail.LockedActions.Select(action => action.Label)),
            split,
            Heading("Evidence Refs"),
            ListLines(detail.EvidenceRefs.Select(evidence => $"{evidence.Kind}: {evidence.Value} {evidence.Scope}".Trim())))));
    }

    private void RenderReports()
    {
        _content.Children.Add(Panel(Stack(
            Heading("Reports"),
            Paragraph($"Deduplication JSON: {ResearchWorkspaceAnalyzer.DeduplicationResultPath}"),
            Paragraph($"Workspace plan JSON: {ResearchWorkspaceAnalyzer.WorkspacePlanPath}"),
            Paragraph($"Review report: {ResearchWorkspaceAnalyzer.ReviewReportPath}"),
            Paragraph("Generated outputs are displayed as project-relative references. This preview does not open, edit, or rewrite them."))));
    }

    private void RenderDiagnostics()
    {
        _content.Children.Add(Panel(Stack(
            Heading("Diagnostics"),
            Paragraph("Desktop preview diagnostics are local-only in UI-02A. This is not a product desktop shell."),
            Paragraph("Only verify/analyze workflow actions are available. Init, import, and merge decision actions remain unavailable."),
            Paragraph("No providers, persistence, database, cloud/API, PDF/OCR, AI/model calls, Core mutation, or executable merge decisions are available."))));
    }

    private static Border CandidatePanel(string label, DuplicateCandidateSummary candidate)
    {
        return Panel(Stack(
            Heading(label),
            Paragraph(candidate.Title),
            Paragraph($"Candidate ID: {candidate.CandidateId}"),
            Paragraph($"Primary work ID: {candidate.PrimaryWorkId ?? "none"}"),
            Paragraph($"Source trace: {candidate.SourceTraceId}"),
            Paragraph($"Source record: {candidate.SourceRecordId}"),
            Paragraph($"Duplicate state: {candidate.DuplicateState}")));
    }

    private void ApplyHostClientSize()
    {
        if (_hostRoot is null)
        {
            return;
        }

        if (ClientSize.Width > 0)
        {
            _hostRoot.Width = ClientSize.Width;
        }

        if (ClientSize.Height > 0)
        {
            _hostRoot.Height = ClientSize.Height;
            if (_hostRoot.Child is Grid layout)
            {
                layout.Height = Math.Max(0, ClientSize.Height - layout.Margin.Top - layout.Margin.Bottom);
            }
        }
    }

    private static Border HeadingPanel(string heading, string text)
    {
        return Panel(Stack(Heading(heading), Paragraph(text)));
    }

    private static StackPanel Stack(params Control[] controls)
    {
        return Stack((IEnumerable<Control>)controls);
    }

    private static StackPanel Stack(IEnumerable<Control> controls)
    {
        var stack = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        foreach (var control in controls)
        {
            stack.Children.Add(control);
        }

        return stack;
    }

    private static TextBlock Heading(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = TextBrush,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock Paragraph(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = MutedTextBrush,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static Border Panel(Control child)
    {
        return new Border
        {
            Background = PanelBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = child
        };
    }

    private static Border Badge(string text, IBrush background, IBrush foreground)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(9, 5),
            Child = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Control MetricGrid(IEnumerable<(string Label, string Value)> metrics)
    {
        var panel = new WrapPanel
        {
            ItemSpacing = 10,
            LineSpacing = 10
        };
        foreach (var metric in metrics)
        {
            panel.Children.Add(new Border
            {
                Background = SubtlePanelBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                MinWidth = 142,
                Padding = new Thickness(12),
                Child = Stack(
                    new TextBlock
                    {
                        Text = metric.Value,
                        FontSize = 22,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = TextBrush,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = metric.Label,
                        FontSize = 12,
                        Foreground = MutedTextBrush,
                        TextWrapping = TextWrapping.Wrap
                    })
            });
        }

        return panel;
    }

    private static (string Label, string Value) Metric(string label, string value)
    {
        return (label, value);
    }

    private static Control ListLines(IEnumerable<string> lines)
    {
        var stack = new StackPanel { Spacing = 6 };
        foreach (var line in lines)
        {
            stack.Children.Add(Paragraph(line));
        }

        if (stack.Children.Count == 0)
        {
            stack.Children.Add(Paragraph("None."));
        }

        return stack;
    }

    private static Control Table(IEnumerable<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var headerArray = headers.ToArray();
        var rowArray = rows.ToArray();
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        for (var column = 0; column < headerArray.Length; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(ColumnWeight(headerArray[column]), GridUnitType.Star)));
        }

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var column = 0; column < headerArray.Length; column++)
        {
            grid.Children.Add(TableCell(headerArray[column], row: 0, column, isHeader: true, isAlternate: false));
        }

        for (var rowIndex = 0; rowIndex < rowArray.Length; rowIndex++)
        {
            var row = rowArray[rowIndex];
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var column = 0; column < headerArray.Length; column++)
            {
                var value = column < row.Count ? row[column] : string.Empty;
                grid.Children.Add(TableCell(
                    value,
                    row: rowIndex + 1,
                    column,
                    isHeader: false,
                    isAlternate: rowIndex % 2 == 1));
            }
        }

        if (rowArray.Length == 0)
        {
            return Paragraph("None.");
        }

        return grid;
    }

    private static Control TableCell(string value, int row, int column, bool isHeader, bool isAlternate)
    {
        var text = new TextBlock
        {
            Text = value,
            Foreground = isHeader ? TextBrush : MutedTextBrush,
            FontSize = isHeader ? 12 : 12,
            FontWeight = isHeader ? FontWeight.SemiBold : FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.None,
            MinWidth = 72
        };

        var border = new Border
        {
            Background = isHeader
                ? MutedPanelBrush
                : isAlternate ? SubtlePanelBrush : PanelBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 7),
            Child = text
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        return border;
    }

    private static double ColumnWeight(string header)
    {
        return header switch
        {
            "Title" or "Representative Title" or "Creators" => 2.1,
            "Identifier" or "Relative Path" => 1.5,
            "Duplicate State" or "Review Required" => 1.25,
            "Year" or "Warnings" or "Skipped" or "Records" or "Imported" or "Members" or "Evidence" => 0.8,
            _ => 1.0
        };
    }

    private static Button PrimaryButton(string text)
    {
        return new Button
        {
            Content = text,
            MinWidth = 88,
            MinHeight = 38,
            Padding = new Thickness(14, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = PrimaryBrush,
            BorderBrush = PrimaryBrush,
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold
        };
    }

    private static Button SecondaryButton(string text)
    {
        return new Button
        {
            Content = text,
            MinWidth = 92,
            MinHeight = 38,
            Padding = new Thickness(14, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = SubtlePanelBrush,
            BorderBrush = PanelBorderBrush,
            Foreground = TextBrush,
            FontWeight = FontWeight.SemiBold
        };
    }

    private static IBrush StateBrush(WorkspaceState state)
    {
        return state switch
        {
            WorkspaceState.Missing => WarningBrush,
            WorkspaceState.NeedsAttention => new SolidColorBrush(Color.Parse("#dc2626")),
            WorkspaceState.ImportedWithWarnings => WarningBrush,
            WorkspaceState.ReviewReady => PrimaryBrush,
            WorkspaceState.Analyzed => AccentBrush,
            _ => PrimaryDarkBrush
        };
    }

    private static Control LockedActionPill(string label)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#fff7ed")),
            BorderBrush = new SolidColorBrush(Color.Parse("#fdba74")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = $"Locked: {label}",
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Foreground = WarningBrush,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Control LockedActionList(IEnumerable<string> labels)
    {
        var panel = new WrapPanel
        {
            ItemSpacing = 8,
            LineSpacing = 8
        };

        foreach (var label in labels)
        {
            panel.Children.Add(LockedActionPill(label));
        }

        if (panel.Children.Count == 0)
        {
            panel.Children.Add(Paragraph("No locked actions."));
        }

        return panel;
    }

    private static string Present(bool value)
    {
        return value ? "present" : "missing";
    }

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }

    private static string RecoveryLine(WorkspaceAttentionItem item)
    {
        var target = string.IsNullOrWhiteSpace(item.Target)
            ? string.Empty
            : $" Target: {item.Target}.";
        return item.Code switch
        {
            "missing-file" => $"Restore the missing local file or remove the input intentionally.{target}",
            "digest-mismatch" => $"Restore the original bytes or re-import the file intentionally before analysis.{target}",
            "invalid-path" => $"Fix the workspace-relative path before verification.{target}",
            "missing-import-trace" => $"Recreate the missing import trace by re-importing the source export intentionally.{target}",
            "missing-generated-output" => $"{item.Message} Run Analyze to regenerate local outputs.",
            _ => $"{item.Message}{target}"
        };
    }
}
