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
    private static readonly SolidColorBrush BackgroundBrush = new(Color.Parse("#f4f2ea"));
    private static readonly SolidColorBrush HeaderBrush = new(Color.Parse("#123b3a"));
    private static readonly SolidColorBrush AccentBrush = new(Color.Parse("#1f6f68"));
    private static readonly SolidColorBrush PanelBrush = new(Color.Parse("#ffffff"));
    private static readonly SolidColorBrush MutedPanelBrush = new(Color.Parse("#ece8dd"));
    private static readonly SolidColorBrush PanelBorderBrush = new(Color.Parse("#d8d2c4"));
    private static readonly SolidColorBrush WarningBrush = new(Color.Parse("#b7791f"));
    private static readonly SolidColorBrush TextBrush = new(Color.Parse("#1f2933"));
    private static readonly SolidColorBrush MutedTextBrush = new(Color.Parse("#68727d"));

    private readonly DesktopPreviewViewModel _viewModel;
    private readonly TextBox _pathBox = new()
    {
        PlaceholderText = "Select an existing local Nexus research workspace folder"
    };
    private readonly StackPanel _content = new()
    {
        Spacing = 12,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly TextBlock _status = new()
    {
        TextWrapping = TextWrapping.Wrap
    };
    private Border? _hostRoot;

    public MainWindow()
        : this(new DesktopPreviewViewModel())
    {
    }

    public MainWindow(DesktopPreviewViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        Title = "Nexus Scholar Desktop Preview";
        Width = 1220;
        Height = 860;
        MinWidth = 860;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BackgroundBrush;

        _pathBox.Text = Environment.CurrentDirectory;
        Content = BuildContent();
        Opened += (_, _) => ApplyHostClientSize();
        SizeChanged += (_, _) => ApplyHostClientSize();

        Render();
    }

    internal static Border BuildHostLayout(Control header, Control navigation, Control workspaceView, Control statusBar)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(navigation);
        ArgumentNullException.ThrowIfNull(workspaceView);
        ArgumentNullException.ThrowIfNull(statusBar);

        var layout = new Grid
        {
            Margin = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var workspace = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(240)));
        workspace.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var workspaceScroller = new ScrollViewer
        {
            ClipToBounds = true,
            Content = workspaceView,
            Focusable = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible
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
            Background = new SolidColorBrush(Color.Parse("#fff7df")),
            BorderBrush = WarningBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 12, 0, 0),
            Child = _status
        };

        _hostRoot = BuildHostLayout(header, navigation, _content, statusBar);
        return _hostRoot;
    }

    private Control BuildHeader()
    {
        var title = new TextBlock
        {
            Text = "Nexus Scholar desktop preview",
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        };
        var subtitle = new TextBlock
        {
            Text = "Read-only local workspace inspection. Review decisions remain locked APP-01 placeholders.",
            Foreground = new SolidColorBrush(Color.Parse("#d7e7e2")),
            TextWrapping = TextWrapping.Wrap
        };

        var badges = new WrapPanel
        {
            ItemSpacing = 8,
            LineSpacing = 8
        };
        foreach (var badge in _viewModel.BoundaryBadges)
        {
            badges.Children.Add(Badge(badge, new SolidColorBrush(Color.Parse("#d7e7e2")), new SolidColorBrush(Color.Parse("#0e2f2e"))));
        }

        var openButton = new Button
        {
            Content = "Open",
            MinWidth = 84,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        openButton.Click += (_, _) => LoadWorkspaceFromTextBox();

        var browseButton = new Button
        {
            Content = "Browse",
            MinWidth = 84,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        browseButton.Click += async (_, _) => await BrowseForWorkspaceAsync();

        var pathRow = new Grid
        {
            ColumnSpacing = 8
        };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        pathRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetColumn(_pathBox, 0);
        Grid.SetColumn(openButton, 1);
        Grid.SetColumn(browseButton, 2);
        pathRow.Children.Add(_pathBox);
        pathRow.Children.Add(openButton);
        pathRow.Children.Add(browseButton);

        var stack = new StackPanel
        {
            Spacing = 10
        };
        stack.Children.Add(title);
        stack.Children.Add(subtitle);
        stack.Children.Add(badges);
        stack.Children.Add(pathRow);

        return new Border
        {
            Background = HeaderBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 12),
            Child = stack
        };
    }

    private Control BuildNavigation()
    {
        var list = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(0, 0, 12, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        foreach (var section in DesktopPreviewViewModel.Sections)
        {
            var button = new Button
            {
                Content = section.Label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Tag = section.Id,
                IsEnabled = _viewModel.HasWorkspace || section.Id == "welcome"
            };
            button.Click += (_, _) =>
            {
                if (button.Tag is string id)
                {
                    _viewModel.SelectSection(id);
                    Render();
                }
            };
            list.Children.Add(button);
        }

        return new Border
        {
            Background = PanelBrush,
            BorderBrush = PanelBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = list
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

        _pathBox.Text = selected.Path.LocalPath;
        LoadWorkspaceFromTextBox();
    }

    private void LoadWorkspaceFromTextBox()
    {
        var path = _pathBox.Text;
        if (string.IsNullOrWhiteSpace(path))
        {
            _status.Text = "Enter or select an existing local workspace folder.";
            return;
        }

        try
        {
            _viewModel.LoadWorkspace(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            _status.Text = $"Workspace could not be loaded: {ex.Message}";
            return;
        }

        Render();
    }

    private void Render()
    {
        _content.Children.Clear();
        _status.Text = _viewModel.StatusMessage;

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
            ItemSpacing = 8,
            LineSpacing = 8
        };
        row.Children.Add(Badge($"State: {overview.State}", AccentBrush, Brushes.White));
        row.Children.Add(Badge($"Project: {overview.ProjectTitle ?? "not loaded"}", MutedPanelBrush, TextBrush));
        row.Children.Add(Badge($"Workspace: {overview.WorkspaceId ?? "missing"}", MutedPanelBrush, TextBrush));
        row.Children.Add(Badge($"Location: {overview.ProjectLocation}", MutedPanelBrush, TextBrush));

        return Panel(row);
    }

    private void RenderWelcome()
    {
        _content.Children.Add(Panel(Stack(
            Heading("Open Workspace"),
            Paragraph("Select an existing local Nexus research workspace. This preview reads generated local outputs and does not run init, import, verify, analyze, or merge decisions."),
            Paragraph(_viewModel.HasWorkspace
                ? "Workspace loaded. Use the sidebar to inspect evidence records, review queue items, clusters, and locked decision gates."
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
                ? new[] { Paragraph("No locked action descriptors were attached to this item.") }
                : item.LockedActions.Select(action => LockedActionButton(action.Label)).ToArray<Control>();
            _content.Children.Add(Panel(Stack(new Control[]
            {
                Heading(item.Title),
                Paragraph($"Candidate pair: {item.CandidatePairId}"),
                Paragraph($"Title similarity: {item.TitleSimilarity:0.000} / threshold {item.ThresholdUsed:0.000}")
            }.Concat(actions))));
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
                    Present(cluster.ReviewRequired)
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
            split,
            Heading("Evidence Refs"),
            ListLines(detail.EvidenceRefs.Select(evidence => $"{evidence.Kind}: {evidence.Value} {evidence.Scope}".Trim())),
            Heading("Locked Actions"),
            Stack(detail.LockedActions.Select(action => LockedActionButton(action.Label))))));
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
            Paragraph("Desktop preview diagnostics are read-only in UI-01."),
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
            Spacing = 8,
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
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = TextBrush,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static TextBlock Paragraph(string text)
    {
        return new TextBlock
        {
            Text = text,
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
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14),
            Child = child
        };
    }

    private static Border Badge(string text, IBrush background, IBrush foreground)
    {
        return new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 4, 8, 4),
            Child = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            }
        };
    }

    private static Control MetricGrid(IEnumerable<(string Label, string Value)> metrics)
    {
        var panel = new WrapPanel
        {
            ItemSpacing = 8,
            LineSpacing = 8
        };
        foreach (var metric in metrics)
        {
            panel.Children.Add(new Border
            {
                Background = MutedPanelBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                MinWidth = 150,
                Padding = new Thickness(10),
                Child = Stack(
                    new TextBlock
                    {
                        Text = metric.Value,
                        FontSize = 20,
                        FontWeight = FontWeight.Bold,
                        Foreground = TextBrush,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = metric.Label,
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
        var stack = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        stack.Children.Add(new Border
        {
            Background = MutedPanelBrush,
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8),
            Child = TextRow(headers)
        });

        var rowCount = 0;
        foreach (var row in rows)
        {
            rowCount++;
            stack.Children.Add(new Border
            {
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8),
                Child = TextRow(row)
            });
        }

        if (rowCount == 0)
        {
            stack.Children.Add(Paragraph("None."));
        }

        return stack;
    }

    private static Control TextRow(IEnumerable<string> values)
    {
        var row = new WrapPanel
        {
            ItemSpacing = 12,
            LineSpacing = 4
        };
        foreach (var value in values)
        {
            row.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = TextBrush,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 110,
                MaxWidth = 280
            });
        }

        return row;
    }

    private static Button LockedActionButton(string label)
    {
        return new Button
        {
            Content = label,
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private static string Present(bool value)
    {
        return value ? "present" : "missing";
    }
}
