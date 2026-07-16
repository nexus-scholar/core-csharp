using Avalonia;
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
    private readonly TextBox _sourcePath = Input("Local CSV, RIS, or BibTeX file");
    private readonly ComboBox _source = Choice(new[]
    {
        "Scopus", "Web of Science", "Google Scholar", "OpenAlex", "Semantic Scholar", "Other"
    });
    private readonly ComboBox _format = Choice(new[] { "CSV", "RIS", "BibTeX" });
    private readonly TextBox _inputId = Input("Optional input id");
    private readonly TextBox _query = Input("Optional query text");
    private readonly StackPanel _workspaceContent = new() { Spacing = 18 };
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

        _source.SelectedIndex = 0;
        _format.SelectedIndex = 0;
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
            Padding = new Thickness(28, 24),
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
            panel.Children.Add(new Button
            {
                Content = label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 9),
                Background = label == "Workspace" ? Brush("#245f58") : Brushes.Transparent,
                Foreground = Surface,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                IsEnabled = label is "Workspace" or "Imports" or "Evidence"
            });
        }
        panel.Children.Add(new TextBlock
        {
            Text = "Scientific decisions are unavailable in this slice.",
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
        _workspaceContent.Children.Clear();
        _workspaceContent.Children.Add(BuildWorkspacePicker());
        if (_viewModel.HasWorkspace)
        {
            _workspaceContent.Children.Add(BuildOverview());
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
            _confirmationContent.Children.Add(BoundaryNote());
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
            Text = $"Confirmation token: {_viewModel.PendingPreview!.ConfirmationToken}",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Muted
        });
        _confirmationContent.Children.Add(BoundaryNote());
        var confirm = PrimaryButton("Confirm exact effects");
        confirm.Click += (_, _) => { _viewModel.ConfirmPending(); Render(); };
        var cancel = SecondaryButton("Cancel");
        cancel.Click += (_, _) => { _viewModel.CancelPending(); Render(); };
        _confirmationContent.Children.Add(confirm);
        _confirmationContent.Children.Add(cancel);
    }

    private static Control BoundaryNote() => new Border
    {
        Background = Brush("#fff6df"),
        BorderBrush = Brush("#e4c16c"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(10),
        Child = new TextBlock
        {
            Text = "Operational action only. No scientific actor, decision, provider, AI, or cloud authority is active.",
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

    private static TextBox Input(string watermark) => new()
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

    private static Button Button(string text, IBrush background, IBrush foreground) => new()
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

    private static SolidColorBrush Brush(string value) => new(Color.Parse(value));
}
