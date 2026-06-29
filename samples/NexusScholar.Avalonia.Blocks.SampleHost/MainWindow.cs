using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace NexusScholar.Avalonia.Blocks.SampleHost;

public sealed class MainWindow : Window
{
    private readonly IReadOnlyList<SampleWorkspace> _samples;
    private readonly WorkspacePlanView _workspaceView = new();
    private readonly TextBlock _status = new()
    {
        TextWrapping = TextWrapping.Wrap
    };
    private Border? _hostRoot;

    public MainWindow()
        : this(SampleWorkspaceLoader.LoadDefaultSamples())
    {
    }

    public MainWindow(IReadOnlyList<SampleWorkspace> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0)
        {
            throw new ArgumentException("At least one sample workspace is required.", nameof(samples));
        }

        _samples = samples;

        Title = "Nexus Scholar Avalonia Blocks Sample Host";
        Width = 1180;
        Height = 840;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.Parse("#f7f4ed"));

        Content = BuildContent();
        Opened += (_, _) => ApplyHostClientSize();
        SizeChanged += (_, _) => ApplyHostClientSize();

        RenderSample(_samples[0]);
    }

    private Control BuildContent()
    {
        var selector = new ComboBox
        {
            MinWidth = 320,
            MaxWidth = 440,
            ItemsSource = _samples.Select(sample => sample.DisplayName).ToArray(),
            SelectedIndex = 0
        };
        selector.SelectionChanged += (_, _) =>
        {
            if (selector.SelectedIndex >= 0 && selector.SelectedIndex < _samples.Count)
            {
                RenderSample(_samples[selector.SelectedIndex]);
            }
        };

        var headerContent = new StackPanel { Spacing = 10 };
        headerContent.Children.Add(new TextBlock
        {
            Text = "Nexus Scholar Core sample host",
            FontSize = 26,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        headerContent.Children.Add(new TextBlock
        {
            Text = "Visual inspection only. Sample plans are non-authoritative; actions are placeholder callbacks and do not call Core.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.Parse("#d7e7e2"))
        });
        headerContent.Children.Add(selector);

        _status.Text = "Ready. No Core calls, persistence, AI, or scientific mutation are available in this host.";
        _status.Foreground = new SolidColorBrush(Color.Parse("#3b3428"));

        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#123b3a")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 12),
            Child = headerContent
        };

        var statusBar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#fff7df")),
            BorderBrush = new SolidColorBrush(Color.Parse("#b7791f")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 12, 0, 0),
            Child = _status
        };

        _hostRoot = BuildHostLayout(header, _workspaceView, statusBar);
        return _hostRoot;
    }

    internal static Border BuildHostLayout(Control header, Control workspaceView, Control statusBar)
    {
        ArgumentNullException.ThrowIfNull(header);
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

        Grid.SetRow(header, 0);
        Grid.SetRow(workspaceScroller, 1);
        Grid.SetRow(statusBar, 2);

        layout.Children.Add(header);
        layout.Children.Add(workspaceScroller);
        layout.Children.Add(statusBar);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#f7f4ed")),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = layout
        };
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

    private void RenderSample(SampleWorkspace sample)
    {
        _workspaceView.Render(sample.Plan, invocation =>
        {
            _status.Text =
                $"Placeholder action callback: {invocation.ActionId} on {invocation.BlockId}. No Core command was called.";
        });

        _status.Text = $"Loaded {sample.FileName}. This is a sample harness only.";
    }
}
