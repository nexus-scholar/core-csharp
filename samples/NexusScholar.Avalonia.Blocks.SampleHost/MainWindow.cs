using Avalonia;
using Avalonia.Controls;
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
        Background = new SolidColorBrush(Color.Parse("#f7f4ed"));

        Content = BuildContent();
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

        var panel = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(16)
        };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);

        panel.Children.Add(header);
        panel.Children.Add(statusBar);
        panel.Children.Add(_workspaceView);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#f7f4ed")),
            Child = panel
        };
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
