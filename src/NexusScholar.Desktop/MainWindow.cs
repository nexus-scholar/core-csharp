using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using NexusScholar.Desktop.AppServices;

namespace NexusScholar.Desktop;

public sealed class MainWindow : Window
{
    private static readonly IBrush AppBackground = Brush("#f4f6f8");
    private static readonly IBrush Surface = Brush("#ffffff");
    private static readonly IBrush SurfaceMuted = Brush("#eef2f5");
    private static readonly IBrush Border = Brush("#cfd6de");
    private static readonly IBrush Text = Brush("#17202a");
    private static readonly IBrush Muted = Brush("#5d6875");
    private static readonly IBrush Primary = Brush("#146c60");
    private static readonly IBrush PrimaryHover = Brush("#0f574d");
    private static readonly IBrush Warning = Brush("#9a5b13");
    private static readonly IBrush Failure = Brush("#a33b35");

    private readonly DesktopWorkspaceViewModel _viewModel;
    private readonly TextBox _workspacePath = Input("Workspace folder");
    private readonly TextBox _title = Input("Research project title");
    private readonly TextBox _workspaceId = Input("Optional stable workspace id");
    private readonly TextBox _backupPath = Input("New backup archive path");
    private readonly TextBox _restoreArchivePath = Input("Backup archive to restore");
    private readonly TextBox _restoreTargetPath = Input("New restore workspace folder");
    private readonly TextBox _sourcePath = Input("Local CSV, RIS, or BibTeX file");
    private readonly ComboBox _source = Choice(new[]
    {
        "Scopus", "Web of Science", "Google Scholar", "OpenAlex", "Semantic Scholar", "Other"
    });
    private readonly ComboBox _format = Choice(new[] { "CSV", "RIS", "BibTeX" });
    private readonly TextBox _inputId = Input("Optional input id");
    private readonly TextBox _query = Input("Optional query text");
    private readonly ComboBox _reviewTarget = Choice(Array.Empty<string>());
    private readonly ComboBox _reviewAction = Choice(Array.Empty<string>());
    private readonly ComboBox _reviewReason = Choice(Array.Empty<string>());
    private readonly ComboBox _supersedesDecision = Choice(Array.Empty<string>());
    private readonly TextBox _actorId = Input("Human actor id");
    private readonly TextBox _actorRole = Input("Policy-assigned role");
    private readonly TextBox _rationale = Input("Decision rationale");
    private readonly Button _reviewDecision = PrimaryButton("Review decision effects");
    private readonly ComboBox _screeningTarget = Choice(Array.Empty<string>());
    private readonly ComboBox _screeningVerdict = Choice(new[] { "include", "exclude" });
    private readonly ComboBox _screeningReason = Choice(Array.Empty<string>());
    private readonly TextBox _screeningActorId = Input("Human actor id");
    private readonly TextBox _screeningActorRole = Input("Assigned Screening role");
    private readonly TextBox _screeningRationale = Input("Decision rationale");
    private readonly Button _screeningDecision = PrimaryButton("Review Screening effects");
    private readonly Button _screeningCorrection = SecondaryButton("Review correction effects");
    private readonly Button _screeningAdjudication = SecondaryButton("Review adjudication effects");
    private readonly Button _screeningHandoff = SecondaryButton("Review handoff effects");
    private readonly ComboBox _fullTextCandidate = Choice(Array.Empty<string>());
    private readonly TextBox _fullTextPath = Input("Local UTF-8 text file");
    private readonly ComboBox _fullTextVerdict = Choice(new[] { "include", "exclude" });
    private readonly TextBox _fullTextActorId = Input("Human actor id");
    private readonly TextBox _fullTextActorRole = Input("Assigned Full Text role");
    private readonly TextBox _fullTextRationale = Input("Full Text decision rationale");
    private readonly TextBox _fullTextInclusion = Input("Locked Full Text inclusion criteria");
    private readonly TextBox _fullTextExclusion = Input("Locked Full Text exclusion criteria");
    private readonly TextBox _fullTextReason = Input("Exclusion reason code");
    private readonly Button _fullTextIntake = SecondaryButton("Review Full Text intake");
    private readonly Button _fullTextReview = PrimaryButton("Review Full Text decision");
    private readonly TextBox _exportId = Input("Stable export id");
    private readonly TextBox _exportActorId = Input("Human export actor id");
    private readonly TextBox _exportActorRole = Input("Human export actor role");
    private readonly Button _reportingAuthority = SecondaryButton("Review reporting authority");
    private readonly Button _reviewExport = PrimaryButton("Review report and export");
    private readonly StackPanel _workspaceContent = new()
    {
        Spacing = 18,
        Margin = new Thickness(28, 24)
    };
    private readonly StackPanel _confirmationContent = new() { Spacing = 10 };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private Border? _confirmationBand;

    public MainWindow()
        : this(new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade()), null)
    {
    }

    public MainWindow(DesktopWorkspaceViewModel viewModel, string? initialWorkspacePath)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Title = "Nexus Scholar";
        Width = 1360;
        Height = 900;
        MinWidth = 980;
        MinHeight = 680;
        Background = AppBackground;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        SetAutomationName(_fullTextCandidate, "Full Text candidate");
        SetAutomationName(_fullTextVerdict, "Full Text verdict");
        SetAutomationName(_reportingAuthority, "Review reporting authority");
        SetAutomationName(_reviewExport, "Review report and export");
        _source.SelectedIndex = 0;
        _format.SelectedIndex = 0;
        _reviewAction.SelectionChanged += (_, _) => { UpdateReviewReasons(); UpdateReviewPreviewAvailability(); };
        _reviewTarget.SelectionChanged += (_, _) => { UpdateSupersessionChoices(); UpdateReviewPreviewAvailability(); };
        _reviewReason.SelectionChanged += (_, _) => UpdateReviewPreviewAvailability();
        _actorId.TextChanged += (_, _) => UpdateReviewPreviewAvailability();
        _actorRole.TextChanged += (_, _) => UpdateReviewPreviewAvailability();
        _reviewDecision.Click += (_, _) =>
        {
            _viewModel.PreviewDeduplicationReview(
                _reviewTarget.SelectedItem?.ToString() ?? string.Empty,
                _reviewAction.SelectedItem?.ToString() ?? string.Empty,
                _reviewReason.SelectedItem?.ToString() ?? string.Empty,
                _rationale.Text,
                _actorId.Text ?? string.Empty,
                _actorRole.Text ?? string.Empty,
                _supersedesDecision.SelectedItem?.ToString(),
                DateTimeOffset.UtcNow);
            Render();
        };
        _screeningTarget.SelectionChanged += (_, _) => UpdateScreeningPreviewAvailability();
        _screeningVerdict.SelectionChanged += (_, _) => UpdateScreeningPreviewAvailability();
        _screeningReason.SelectionChanged += (_, _) => UpdateScreeningPreviewAvailability();
        _screeningActorId.TextChanged += (_, _) => UpdateScreeningPreviewAvailability();
        _screeningActorRole.TextChanged += (_, _) => UpdateScreeningPreviewAvailability();
        _screeningRationale.TextChanged += (_, _) => UpdateScreeningPreviewAvailability();
        _screeningDecision.Click += (_, _) =>
        {
            _viewModel.PreviewScreeningReview(
                _screeningTarget.SelectedItem?.ToString() ?? string.Empty,
                _screeningVerdict.SelectedItem?.ToString() ?? string.Empty,
                _screeningActorId.Text ?? string.Empty,
                _screeningActorRole.Text ?? string.Empty,
                _screeningRationale.Text ?? string.Empty,
                _screeningReason.SelectedItem?.ToString(),
                DateTimeOffset.UtcNow);
            Render();
        };
        _screeningCorrection.Click += (_, _) =>
        {
            var target = SelectedScreeningTarget();
            var current = target?.CurrentDecisions.SingleOrDefault(item =>
                string.Equals(item.ActorId, _screeningActorId.Text, StringComparison.Ordinal) &&
                item.Kind is "review" or "correction");
            _viewModel.PreviewScreeningResolution(
                target?.CandidateId ?? string.Empty,
                "correction",
                _screeningVerdict.SelectedItem?.ToString() ?? string.Empty,
                _screeningActorId.Text ?? string.Empty,
                _screeningActorRole.Text ?? string.Empty,
                _screeningRationale.Text ?? string.Empty,
                _screeningReason.SelectedItem?.ToString(),
                current?.DecisionDigest,
                null,
                [],
                DateTimeOffset.UtcNow);
            Render();
        };
        _screeningAdjudication.Click += (_, _) =>
        {
            var target = SelectedScreeningTarget();
            var conflict = target?.Conflicts.SingleOrDefault(item => !item.Resolved);
            _viewModel.PreviewScreeningResolution(
                target?.CandidateId ?? string.Empty,
                "adjudication",
                _screeningVerdict.SelectedItem?.ToString() ?? string.Empty,
                _screeningActorId.Text ?? string.Empty,
                _screeningActorRole.Text ?? string.Empty,
                _screeningRationale.Text ?? string.Empty,
                _screeningReason.SelectedItem?.ToString(),
                null,
                conflict?.ConflictId,
                conflict?.SourceDecisionDigests ?? [],
                DateTimeOffset.UtcNow);
            Render();
        };
        _screeningHandoff.Click += (_, _) =>
        {
            _viewModel.PreviewScreeningHandoff(
                _screeningActorId.Text ?? string.Empty,
                _screeningActorRole.Text ?? string.Empty,
                _screeningRationale.Text ?? string.Empty,
                DateTimeOffset.UtcNow);
            Render();
        };
        _fullTextIntake.Click += (_, _) =>
        {
            _viewModel.PreviewFullTextIntake(
                _fullTextCandidate.SelectedItem?.ToString() ?? string.Empty,
                _fullTextPath.Text ?? string.Empty,
                _fullTextActorId.Text ?? string.Empty,
                DateTimeOffset.UtcNow);
            Render();
        };
        _fullTextReview.Click += (_, _) =>
        {
            var verdict = _fullTextVerdict.SelectedItem?.ToString() ?? string.Empty;
            _viewModel.PreviewFullTextReview(
                _fullTextCandidate.SelectedItem?.ToString() ?? string.Empty,
                verdict, _fullTextActorId.Text ?? string.Empty,
                _fullTextActorRole.Text ?? string.Empty,
                _fullTextRationale.Text ?? string.Empty,
                _fullTextInclusion.Text ?? string.Empty,
                _fullTextExclusion.Text ?? string.Empty,
                _fullTextReason.Text ?? string.Empty,
                verdict == "exclude" ? _fullTextReason.Text : null,
                DateTimeOffset.UtcNow);
            Render();
        };
        _reportingAuthority.Click += (_, _) =>
        {
            _viewModel.PreviewReportingWorkflow();
            Render();
        };
        _reviewExport.Click += (_, _) =>
        {
            _viewModel.PreviewReviewExport(
                _exportId.Text ?? string.Empty,
                _exportActorId.Text ?? string.Empty,
                _exportActorRole.Text ?? string.Empty,
                DateTimeOffset.UtcNow);
            Render();
        };
        UpdateReviewPreviewAvailability();
        UpdateScreeningPreviewAvailability();
        _workspacePath.Text = string.IsNullOrWhiteSpace(initialWorkspacePath) ? string.Empty : initialWorkspacePath;
        if (!string.IsNullOrWhiteSpace(initialWorkspacePath))
        {
            _viewModel.Open(initialWorkspacePath);
        }

        Content = BuildShell();
        Render();
    }

    internal static Grid BuildShellGrid(Control navigation, Control workspace, Control inspector)
    {
        var grid = new Grid
        {
            ColumnSpacing = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(224)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(300)));
        Grid.SetColumn(navigation, 0);
        Grid.SetColumn(workspace, 1);
        Grid.SetColumn(inspector, 2);
        grid.Children.Add(navigation);
        grid.Children.Add(workspace);
        grid.Children.Add(inspector);
        return grid;
    }

    private Control BuildShell()
    {
        var navigation = BuildNavigation();
        var center = new Grid();
        center.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        center.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        center.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var header = BuildHeader();
        var scroll = new ScrollViewer
        {
            Content = _workspaceContent,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var statusBand = new Border
        {
            Padding = new Thickness(18, 12),
            BorderBrush = Border,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = Surface,
            Child = _status
        };
        Grid.SetRow(header, 0);
        Grid.SetRow(scroll, 1);
        Grid.SetRow(statusBand, 2);
        center.Children.Add(header);
        center.Children.Add(scroll);
        center.Children.Add(statusBand);

        return BuildShellGrid(navigation, center, BuildInspector());
    }

    private Control BuildNavigation()
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(16, 20) };
        panel.Children.Add(new TextBlock
        {
            Text = "NEXUS SCHOLAR",
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = Surface
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Local evidence workspace",
            FontSize = 12,
            Foreground = Brush("#bcd6d1"),
            Margin = new Thickness(0, 0, 0, 22)
        });
        foreach (var label in new[] { "Workspace", "Imports", "Evidence", "Review queue", "Reports" })
        {
            var navigationButton = new Button
            {
                Content = label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 9),
                Background = label == "Workspace" ? Brush("#245f58") : Brushes.Transparent,
                Foreground = Surface,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                IsEnabled = label is "Workspace" or "Imports" or "Evidence" ||
                    label == "Review queue" &&
                    (_viewModel.ReviewQueue is not null || _viewModel.ScreeningQueue is not null)
            };
            SetAutomationName(navigationButton, label);
            panel.Children.Add(navigationButton);
        }
        panel.Children.Add(new TextBlock
        {
            Text = "Human-authorized review is available only when verified local authority queues are present.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brush("#bcd6d1"),
            FontSize = 12,
            Margin = new Thickness(4, 24, 4, 0)
        });
        return new Border { Background = Brush("#123f3a"), Child = panel };
    }

    private Control BuildHeader()
    {
        var title = new StackPanel { Spacing = 2 };
        title.Children.Add(new TextBlock
        {
            Text = "Research workspace",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = Text
        });
        title.Children.Add(new TextBlock
        {
            Text = "Local files, explicit effects, verified state",
            FontSize = 13,
            Foreground = Muted
        });
        return new Border
        {
            Background = Surface,
            BorderBrush = Border,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(28, 16),
            Child = title
        };
    }

    private Control BuildInspector()
    {
        _confirmationBand = new Border
        {
            Background = Surface,
            BorderBrush = Border,
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(18),
            Child = _confirmationContent
        };
        return _confirmationBand;
    }

    private void Render()
    {
        DetachReusableWorkspaceControls();
        _workspaceContent.Children.Clear();
        _workspaceContent.Children.Add(BuildWorkspacePicker());
        _workspaceContent.Children.Add(BuildRecoveryWorkflow());
        if (_viewModel.HasWorkspace)
        {
            _workspaceContent.Children.Add(BuildOverview());
            if (_viewModel.ReviewQueue is { } queue)
            {
                _workspaceContent.Children.Add(BuildDeduplicationReview(queue));
            }
            if (_viewModel.ScreeningQueue is { } screeningQueue)
            {
                _workspaceContent.Children.Add(BuildScreeningReview(screeningQueue));
                _workspaceContent.Children.Add(BuildFullTextWorkflow(screeningQueue));
            }
            _workspaceContent.Children.Add(BuildReportingWorkflow());
            _workspaceContent.Children.Add(BuildImportForm());
        }
        else
        {
            _workspaceContent.Children.Add(BuildInitializeForm());
        }

        RenderConfirmation();
        _status.Text = _viewModel.Status;
        _status.Foreground = _viewModel.StatusKind switch
        {
            DesktopWorkspaceCommandStatus.Failed => Failure,
            DesktopWorkspaceCommandStatus.Stale or DesktopWorkspaceCommandStatus.RecoveryRequired or DesktopWorkspaceCommandStatus.Attention => Warning,
            _ => Text
        };
    }

    private void DetachReusableWorkspaceControls()
    {
        Control[] controls =
        [
            _workspacePath, _title, _workspaceId, _backupPath, _restoreArchivePath,
            _restoreTargetPath, _sourcePath, _source, _format,
            _inputId, _query, _reviewTarget, _reviewAction, _reviewReason,
            _supersedesDecision, _actorId, _actorRole, _rationale, _reviewDecision
            , _screeningTarget, _screeningVerdict, _screeningReason,
            _screeningActorId, _screeningActorRole, _screeningRationale,
            _screeningDecision, _screeningCorrection, _screeningAdjudication,
            _screeningHandoff, _fullTextCandidate, _fullTextPath, _fullTextVerdict,
            _fullTextActorId, _fullTextActorRole, _fullTextRationale,
            _fullTextInclusion, _fullTextExclusion, _fullTextReason,
            _fullTextIntake, _fullTextReview, _exportId, _exportActorId,
            _exportActorRole,
            _reportingAuthority, _reviewExport
        ];

        foreach (var control in controls)
        {
            DetachFromParent(control);
        }
    }

    internal static void DetachFromParent(Control control)
    {
        switch (control.Parent)
        {
            case Panel panel:
                panel.Children.Remove(control);
                break;
            case Decorator decorator when ReferenceEquals(decorator.Child, control):
                decorator.Child = null;
                break;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, control):
                contentControl.Content = null;
                break;
        }
    }

    private Control BuildWorkspacePicker()
    {
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var browse = SecondaryButton("Browse");
        browse.Click += async (_, _) => await BrowseWorkspaceAsync();
        var open = PrimaryButton("Open");
        open.IsDefault = true;
        open.Click += (_, _) =>
        {
            _viewModel.Open(_workspacePath.Text ?? string.Empty);
            Render();
        };
        Grid.SetColumn(_workspacePath, 0);
        Grid.SetColumn(browse, 1);
        Grid.SetColumn(open, 2);
        row.Children.Add(_workspacePath);
        row.Children.Add(browse);
        row.Children.Add(open);
        return Section("Workspace folder", "Open a local Nexus workspace or select a folder to initialize.", row);
    }

    private Control BuildInitializeForm()
    {
        var fields = new StackPanel { Spacing = 10 };
        fields.Children.Add(Labeled("Project title", _title));
        fields.Children.Add(Labeled("Workspace id", _workspaceId));
        var preview = PrimaryButton("Review initialization");
        preview.Click += (_, _) =>
        {
            _viewModel.PreviewInitialize(
                _workspacePath.Text ?? string.Empty,
                _title.Text ?? string.Empty,
                _workspaceId.Text,
                DateTimeOffset.UtcNow);
            Render();
        };
        fields.Children.Add(preview);
        return Section("Initialize", "Creates a local project index and required folders after confirmation.", fields);
    }

    private Control BuildRecoveryWorkflow()
    {
        var panel = new StackPanel { Spacing = 12 };
        var backupPath = new Grid { ColumnSpacing = 10 };
        backupPath.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        backupPath.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var chooseBackup = SecondaryButton("Choose backup destination");
        chooseBackup.Click += async (_, _) => await BrowseBackupDestinationAsync();
        Grid.SetColumn(_backupPath, 0);
        Grid.SetColumn(chooseBackup, 1);
        backupPath.Children.Add(_backupPath);
        backupPath.Children.Add(chooseBackup);
        panel.Children.Add(Labeled("Verified backup", backupPath));

        var reviewBackup = PrimaryButton("Review backup effects");
        reviewBackup.IsEnabled = _viewModel.HasWorkspace;
        reviewBackup.Click += (_, _) =>
        {
            _viewModel.PreviewBackup(_backupPath.Text ?? string.Empty, DateTimeOffset.UtcNow);
            Render();
        };
        panel.Children.Add(reviewBackup);
        panel.Children.Add(new Separator { Margin = new Thickness(0, 6) });

        var restoreArchive = new Grid { ColumnSpacing = 10 };
        restoreArchive.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        restoreArchive.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var chooseArchive = SecondaryButton("Choose backup archive");
        chooseArchive.Click += async (_, _) => await BrowseRestoreArchiveAsync();
        Grid.SetColumn(_restoreArchivePath, 0);
        Grid.SetColumn(chooseArchive, 1);
        restoreArchive.Children.Add(_restoreArchivePath);
        restoreArchive.Children.Add(chooseArchive);
        panel.Children.Add(Labeled("Backup archive", restoreArchive));

        var restoreTarget = new Grid { ColumnSpacing = 10 };
        restoreTarget.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        restoreTarget.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var chooseTarget = SecondaryButton("Choose restore parent");
        chooseTarget.Click += async (_, _) => await BrowseRestoreTargetAsync();
        Grid.SetColumn(_restoreTargetPath, 0);
        Grid.SetColumn(chooseTarget, 1);
        restoreTarget.Children.Add(_restoreTargetPath);
        restoreTarget.Children.Add(chooseTarget);
        panel.Children.Add(Labeled("New workspace folder", restoreTarget));

        var reviewRestore = PrimaryButton("Review restore effects");
        reviewRestore.Click += (_, _) =>
        {
            _viewModel.PreviewRestore(
                _restoreArchivePath.Text ?? string.Empty,
                _restoreTargetPath.Text ?? string.Empty,
                DateTimeOffset.UtcNow);
            Render();
        };
        panel.Children.Add(reviewRestore);
        return Section(
            "Backup and recovery",
            "Backups are manifest-verified local archives. Restore always creates and verifies a new workspace folder.",
            panel);
    }

    private Control BuildOverview()
    {
        var overview = _viewModel.Overview!;
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = overview.ProjectTitle ?? "Untitled workspace",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = Text
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"{overview.State}  |  {overview.WorkspaceId}",
            Foreground = Muted
        });
        var metrics = new Grid { ColumnSpacing = 10 };
        for (var index = 0; index < 4; index++) metrics.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        var values = new[]
        {
            ("Inputs", overview.InputCount.ToString()),
            ("Records", overview.ImportedRecordCount.ToString()),
            ("Warnings", overview.ParserWarningCount.ToString()),
            ("Review", overview.ReviewRequiredCount.ToString())
        };
        for (var index = 0; index < values.Length; index++)
        {
            var metric = Metric(values[index].Item1, values[index].Item2);
            Grid.SetColumn(metric, index);
            metrics.Children.Add(metric);
        }
        panel.Children.Add(metrics);
        var actions = new WrapPanel { ItemSpacing = 10, LineSpacing = 10 };
        var verify = SecondaryButton("Verify");
        verify.Click += (_, _) => { _viewModel.Verify(); Render(); };
        var analyze = PrimaryButton("Review analysis effects");
        analyze.Click += (_, _) => { _viewModel.PreviewAnalyze(DateTimeOffset.UtcNow); Render(); };
        actions.Children.Add(verify);
        actions.Children.Add(analyze);
        panel.Children.Add(actions);
        return Section("Current state", "Read from verified local workspace projections.", panel);
    }

    private Control BuildImportForm()
    {
        var panel = new StackPanel { Spacing = 10 };
        var sourceFile = new Grid { ColumnSpacing = 10 };
        sourceFile.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        sourceFile.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var browse = SecondaryButton("Choose file");
        browse.Click += async (_, _) => await BrowseImportAsync();
        Grid.SetColumn(_sourcePath, 0);
        Grid.SetColumn(browse, 1);
        sourceFile.Children.Add(_sourcePath);
        sourceFile.Children.Add(browse);
        panel.Children.Add(Labeled("Search export", sourceFile));
        var choices = new Grid { ColumnSpacing = 10 };
        choices.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        choices.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(_source, 0);
        Grid.SetColumn(_format, 1);
        choices.Children.Add(_source);
        choices.Children.Add(_format);
        panel.Children.Add(Labeled("Source and format", choices));
        panel.Children.Add(Labeled("Input id", _inputId));
        panel.Children.Add(Labeled("Query", _query));
        var preview = PrimaryButton("Review import effects");
        preview.Click += (_, _) =>
        {
            _viewModel.PreviewImport(
                _sourcePath.Text ?? string.Empty,
                _source.SelectedItem?.ToString() ?? string.Empty,
                _format.SelectedItem?.ToString() ?? string.Empty,
                _inputId.Text,
                _query.Text,
                DateTimeOffset.UtcNow);
            Render();
        };
        panel.Children.Add(preview);

        if (_viewModel.Overview!.Imports.Count > 0)
        {
            panel.Children.Add(new Separator { Margin = new Thickness(0, 8) });
            foreach (var item in _viewModel.Overview.Imports)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"{item.ImportId}  |  {item.Source} / {item.Format}  |  {item.RecordCount} records  |  {item.ParserWarningCount} warnings",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = item.ParserWarningCount > 0 ? Warning : Text
                });
            }
        }
        return Section("Import local evidence", "Only researcher-selected files are read. No provider or network call is available.", panel);
    }

    private Control BuildDeduplicationReview(DesktopDeduplicationReviewQueue queue)
    {
        var panel = new StackPanel { Spacing = 10 };
        if (queue.Targets.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "No current deduplication review targets remain.",
                Foreground = Text
            });
            return Section("Deduplication review", "Verified policy and authority state.", panel);
        }

        _reviewTarget.ItemsSource = queue.Targets.Select(target => target.TargetId).ToArray();
        _reviewTarget.SelectedIndex = -1;
        _reviewAction.ItemsSource = queue.Policy.AllowedActions.ToArray();
        _reviewAction.SelectedIndex = -1;
        UpdateReviewReasons();
        UpdateSupersessionChoices();

        panel.Children.Add(Labeled("Review target", _reviewTarget));
        var actor = new Grid { ColumnSpacing = 10 };
        actor.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        actor.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(_actorId, 0);
        Grid.SetColumn(_actorRole, 1);
        actor.Children.Add(_actorId);
        actor.Children.Add(_actorRole);
        panel.Children.Add(Labeled("Human actor and active role", actor));

        var decision = new Grid { ColumnSpacing = 10 };
        decision.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        decision.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(_reviewAction, 0);
        Grid.SetColumn(_reviewReason, 1);
        decision.Children.Add(_reviewAction);
        decision.Children.Add(_reviewReason);
        panel.Children.Add(Labeled("Policy action and reason", decision));
        panel.Children.Add(Labeled("Rationale", _rationale));
        if (_supersedesDecision.Items.Count > 0)
        {
            panel.Children.Add(Labeled("Correct active decision", _supersedesDecision));
        }

        var selected = SelectedReviewTarget();
        if (selected is not null)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Candidates: {string.Join(", ", selected.CandidateIds)}  |  Evidence: {selected.EvidenceIds.Count}  |  Active decisions: {selected.ActiveDecisions.Count}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Muted
            });
        }
        panel.Children.Add(new TextBlock
        {
            Text = $"Policy: {queue.Policy.PolicyId} {queue.Policy.PolicyVersion}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Muted
        });

        UpdateReviewPreviewAvailability();
        panel.Children.Add(_reviewDecision);
        return Section("Deduplication review", "Every decision is checked against the verified local policy and requires explicit confirmation.", panel);
    }

    private DesktopDeduplicationReviewTarget? SelectedReviewTarget() =>
        _viewModel.ReviewQueue?.Targets.SingleOrDefault(target =>
            string.Equals(target.TargetId, _reviewTarget.SelectedItem?.ToString(), StringComparison.Ordinal));

    private Control BuildScreeningReview(DesktopScreeningReviewQueue queue)
    {
        var panel = new StackPanel { Spacing = 10 };
        _screeningTarget.ItemsSource = queue.Targets.Select(item => item.CandidateId).ToArray();
        _screeningTarget.SelectedIndex = -1;
        _screeningVerdict.SelectedIndex = -1;
        _screeningReason.ItemsSource = queue.ExclusionReasons.ToArray();
        _screeningReason.SelectedIndex = -1;

        panel.Children.Add(Labeled("Title/abstract target", _screeningTarget));
        var actor = new Grid { ColumnSpacing = 10 };
        actor.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        actor.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(_screeningActorId, 0);
        Grid.SetColumn(_screeningActorRole, 1);
        actor.Children.Add(_screeningActorId);
        actor.Children.Add(_screeningActorRole);
        panel.Children.Add(Labeled("Human actor and assigned role", actor));
        panel.Children.Add(Labeled("Decision", _screeningVerdict));
        panel.Children.Add(Labeled("Exclusion reason", _screeningReason));
        panel.Children.Add(Labeled("Rationale", _screeningRationale));
        panel.Children.Add(new TextBlock
        {
            Text = $"Policy {queue.PolicyId} | {queue.RequiredReviewCount} required review(s)",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Muted
        });
        UpdateScreeningPreviewAvailability();
        panel.Children.Add(_screeningDecision);
        var resolutions = new WrapPanel { ItemSpacing = 10, LineSpacing = 10 };
        resolutions.Children.Add(_screeningCorrection);
        resolutions.Children.Add(_screeningAdjudication);
        resolutions.Children.Add(_screeningHandoff);
        panel.Children.Add(resolutions);
        return Section(
            "Title and abstract Screening",
            "Decisions are reconstructed from the verified protocol, criteria, snapshot, corpus binding, and human assignment.",
            panel);
    }

    private void UpdateReviewReasons()
    {
        var action = _reviewAction.SelectedItem?.ToString();
        var reasons = action is not null && _viewModel.ReviewQueue?.Policy.ReasonCodesByAction.TryGetValue(action, out var values) == true
            ? values
            : Array.Empty<string>();
        _reviewReason.ItemsSource = reasons.ToArray();
        _reviewReason.SelectedIndex = -1;
    }

    private void UpdateSupersessionChoices()
    {
        var decisions = SelectedReviewTarget()?.ActiveDecisions ?? Array.Empty<DesktopDeduplicationActiveDecision>();
        _supersedesDecision.ItemsSource = decisions.Select(item => item.DecisionId).ToArray();
        _supersedesDecision.SelectedIndex = decisions.Count == 1 ? 0 : -1;
    }

    private void UpdateReviewPreviewAvailability()
    {
        _reviewDecision.IsEnabled = CanPreviewDeduplicationReview(
            _reviewTarget.SelectedItem?.ToString(),
            _reviewAction.SelectedItem?.ToString(),
            _reviewReason.SelectedItem?.ToString(),
            _actorId.Text,
            _actorRole.Text);
    }

    internal static bool CanPreviewDeduplicationReview(
        string? target,
        string? action,
        string? reason,
        string? actorId,
        string? actorRole) =>
        !string.IsNullOrWhiteSpace(target) &&
        !string.IsNullOrWhiteSpace(action) &&
        !string.IsNullOrWhiteSpace(reason) &&
        !string.IsNullOrWhiteSpace(actorId) &&
        !string.IsNullOrWhiteSpace(actorRole);

    private void UpdateScreeningPreviewAvailability()
    {
        var target = SelectedScreeningTarget();
        var actorId = _screeningActorId.Text;
        var actorRole = _screeningActorRole.Text;
        var rationale = _screeningRationale.Text;
        _screeningDecision.IsEnabled = CanPreviewScreeningReview(
            _screeningTarget.SelectedItem?.ToString(),
            _screeningVerdict.SelectedItem?.ToString(),
            _screeningActorId.Text,
            _screeningActorRole.Text,
            _screeningRationale.Text,
            _screeningReason.SelectedItem?.ToString());
        _screeningCorrection.IsEnabled =
            CanPreviewScreeningReview(
                target?.CandidateId,
                _screeningVerdict.SelectedItem?.ToString(),
                actorId,
                actorRole,
                rationale,
                _screeningReason.SelectedItem?.ToString()) &&
            target!.CurrentDecisions.Count(item =>
                string.Equals(item.ActorId, actorId, StringComparison.Ordinal) &&
                item.Kind is "review" or "correction") == 1;
        _screeningAdjudication.IsEnabled =
            CanPreviewScreeningReview(
                target?.CandidateId,
                _screeningVerdict.SelectedItem?.ToString(),
                actorId,
                actorRole,
                rationale,
                _screeningReason.SelectedItem?.ToString()) &&
            target!.Conflicts.Count(item => !item.Resolved) == 1 &&
            _viewModel.ScreeningQueue!.AdjudicatorRoles.Contains(
                actorRole ?? string.Empty, StringComparer.Ordinal);
        _screeningHandoff.IsEnabled =
            _viewModel.ScreeningQueue?.HandoffReady == true &&
            !string.IsNullOrWhiteSpace(actorId) &&
            !string.IsNullOrWhiteSpace(actorRole) &&
            !string.IsNullOrWhiteSpace(rationale);
    }

    private DesktopScreeningReviewTarget? SelectedScreeningTarget() =>
        _viewModel.ScreeningQueue?.Targets.SingleOrDefault(target =>
            string.Equals(
                target.CandidateId,
                _screeningTarget.SelectedItem?.ToString(),
                StringComparison.Ordinal));

    private Control BuildFullTextWorkflow(DesktopScreeningReviewQueue queue)
    {
        var panel = new StackPanel { Spacing = 10 };
        _fullTextCandidate.ItemsSource = queue.Targets
            .Where(item => item.CurrentVerdict == "include")
            .Select(item => item.CandidateId).ToArray();
        _fullTextCandidate.SelectedIndex = -1;
        _fullTextVerdict.SelectedIndex = -1;
        panel.Children.Add(Labeled("Included candidate", _fullTextCandidate));
        panel.Children.Add(Labeled("Local Full Text file", _fullTextPath));
        var actor = new Grid { ColumnSpacing = 10 };
        actor.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        actor.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(_fullTextActorId, 0);
        Grid.SetColumn(_fullTextActorRole, 1);
        actor.Children.Add(_fullTextActorId);
        actor.Children.Add(_fullTextActorRole);
        panel.Children.Add(Labeled("Human actor and role", actor));
        panel.Children.Add(_fullTextIntake);
        panel.Children.Add(new Separator { Margin = new Thickness(0, 6) });
        panel.Children.Add(Labeled("Full Text verdict", _fullTextVerdict));
        panel.Children.Add(Labeled("Inclusion criteria", _fullTextInclusion));
        panel.Children.Add(Labeled("Exclusion criteria", _fullTextExclusion));
        panel.Children.Add(Labeled("Exclusion reason code", _fullTextReason));
        panel.Children.Add(Labeled("Rationale", _fullTextRationale));
        panel.Children.Add(_fullTextReview);
        return Section(
            "Local Full Text",
            "Intake reads only the selected local file; decisions bind its exact bytes and extraction attempt.",
            panel);
    }

    private Control BuildReportingWorkflow()
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(_reportingAuthority);
        panel.Children.Add(new Separator { Margin = new Thickness(0, 6) });
        panel.Children.Add(Labeled("Export id", _exportId));
        panel.Children.Add(Labeled("Human export actor", _exportActorId));
        panel.Children.Add(Labeled("Human export role", _exportActorRole));
        panel.Children.Add(_reviewExport);
        return Section(
            "Report and export",
            "Finalization requires complete terminal Full Text cases and publishes an exact Bundle v2 ledger entry.",
            panel);
    }

    internal static bool CanPreviewScreeningReview(
        string? target,
        string? verdict,
        string? actorId,
        string? actorRole,
        string? rationale,
        string? exclusionReason) =>
        !string.IsNullOrWhiteSpace(target) &&
        verdict is "include" or "exclude" &&
        !string.IsNullOrWhiteSpace(actorId) &&
        !string.IsNullOrWhiteSpace(actorRole) &&
        !string.IsNullOrWhiteSpace(rationale) &&
        (verdict != "exclude" || !string.IsNullOrWhiteSpace(exclusionReason));

    private void RenderConfirmation()
    {
        _confirmationContent.Children.Clear();
        _confirmationContent.Children.Add(new TextBlock
        {
            Text = "Effect inspector",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = Text
        });
        if (!_viewModel.HasPendingConfirmation)
        {
            _confirmationContent.Children.Add(new TextBlock
            {
                Text = "No command is awaiting confirmation. Preview a write operation to inspect its exact local effects.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Muted
            });
            _confirmationContent.Children.Add(BoundaryNote(scientificDecision: false));
            return;
        }

        _confirmationContent.Children.Add(new TextBlock
        {
            Text = _viewModel.PendingCommandLabel,
            FontWeight = FontWeight.SemiBold,
            Foreground = Text
        });
        foreach (var effect in _viewModel.PendingEffects)
        {
            _confirmationContent.Children.Add(new TextBlock
            {
                Text = $"- {effect}",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Text
            });
        }
        _confirmationContent.Children.Add(new TextBlock
        {
            Text = $"Confirmation token: {_viewModel.PendingConfirmationToken}",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Muted
        });
        _confirmationContent.Children.Add(BoundaryNote(
            _viewModel.PendingReviewPreview is not null ||
            _viewModel.PendingScreeningPreview is not null ||
            _viewModel.PendingScreeningResolutionPreview is not null ||
            _viewModel.PendingScreeningHandoffPreview is not null));
        var confirm = PrimaryButton("Confirm exact effects");
        confirm.Click += (_, _) => { _viewModel.ConfirmPending(); Render(); };
        var cancel = SecondaryButton("Cancel");
        cancel.Click += (_, _) => { _viewModel.CancelPending(); Render(); };
        _confirmationContent.Children.Add(confirm);
        _confirmationContent.Children.Add(cancel);
    }

    private static Control BoundaryNote(bool scientificDecision) => new Border
    {
        Background = Brush("#fff6df"),
        BorderBrush = Brush("#e4c16c"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10),
        Child = new TextBlock
        {
            Text = scientificDecision
                ? "Scientific decision: the named human actor and role must be assigned by the verified local policy. No authentication, provider, AI, or cloud authority is implied."
                : "Operational action only. No scientific actor, decision, provider, AI, or cloud authority is active.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Warning
        }
    };

    private async Task BrowseWorkspaceAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a local research workspace",
            AllowMultiple = false
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            _workspacePath.Text = path;
        }
    }

    private async Task BrowseImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a local Search export",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Search exports") { Patterns = new[] { "*.csv", "*.ris", "*.bib", "*.bibtex" } }
            }
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            _sourcePath.Text = path;
        }
    }

    private async Task BrowseBackupDestinationAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Create a verified Nexus workspace backup",
            SuggestedFileName = "nexus-workspace-backup.zip",
            DefaultExtension = "zip",
            FileTypeChoices =
            [
                new FilePickerFileType("Nexus workspace backup") { Patterns = ["*.zip"] }
            ]
        });
        if (file?.TryGetLocalPath() is { } path)
        {
            _backupPath.Text = path;
        }
    }

    private async Task BrowseRestoreArchiveAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a verified Nexus workspace backup",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Nexus workspace backup") { Patterns = ["*.zip"] }
            ]
        });
        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            _restoreArchivePath.Text = path;
        }
    }

    private async Task BrowseRestoreTargetAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the parent folder for the restored workspace",
            AllowMultiple = false
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path)
        {
            _restoreTargetPath.Text = Path.Combine(path, "nexus-restored-workspace");
        }
    }

    private static Border Section(string heading, string description, Control content)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = heading, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Text });
        panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = Muted });
        panel.Children.Add(content);
        return new Border
        {
            Background = Surface,
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(18),
            Child = panel
        };
    }

    private static Border Metric(string label, string value)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = value, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Text });
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Muted });
        return new Border { Background = SurfaceMuted, CornerRadius = new CornerRadius(4), Padding = new Thickness(12), Child = panel };
    }

    private static Control Labeled(string label, Control control)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Muted });
        panel.Children.Add(control);
        return panel;
    }

    private static TextBox Input(string watermark)
    {
        var input = new TextBox
        {
            PlaceholderText = watermark,
            MinHeight = 38,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Surface,
            Foreground = Text,
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 7),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        SetAutomationName(input, watermark);
        return input;
    }

    private static ComboBox Choice(IEnumerable<string> values) => new()
    {
        ItemsSource = values.ToArray(),
        MinHeight = 38,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Background = Surface,
        Foreground = Text,
        BorderBrush = Border,
        BorderThickness = new Thickness(1)
    };

    private static Button PrimaryButton(string text)
    {
        var button = Button(text, Primary, Surface);
        button.PointerEntered += (_, _) => button.Background = PrimaryHover;
        button.PointerExited += (_, _) => button.Background = Primary;
        return button;
    }

    private static Button SecondaryButton(string text) => Button(text, SurfaceMuted, Text);

    private static Button Button(string text, IBrush background, IBrush foreground)
    {
        var button = new Button
        {
            Content = text,
            Background = background,
            Foreground = foreground,
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(14, 8),
            MinHeight = 38
        };
        SetAutomationName(button, text);
        return button;
    }

    private static void SetAutomationName(Control control, string name) =>
        control.SetValue(AutomationProperties.NameProperty, name);

    private static SolidColorBrush Brush(string value) => new(Color.Parse(value));
}
