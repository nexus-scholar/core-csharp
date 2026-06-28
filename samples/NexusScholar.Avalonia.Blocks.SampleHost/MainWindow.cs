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
        Width = 1100;
        Height = 800;
        MinWidth = 760;
        MinHeight = 520;

        Content = BuildContent();
        RenderSample(_samples[0]);
    }

    private Control BuildContent()
    {
        var selector = new ComboBox
        {
            MinWidth = 320,
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

        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = "Phase 3.5 sample harness",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "Visual inspection only. Sample plans are non-authoritative; actions are placeholder callbacks and do not call Core.",
            TextWrapping = TextWrapping.Wrap
        });
        header.Children.Add(selector);

        _status.Text = "Ready. No Core calls, persistence, AI, or scientific mutation are available in this host.";

        var panel = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(12)
        };

        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_status, Dock.Bottom);

        panel.Children.Add(header);
        panel.Children.Add(_status);
        panel.Children.Add(_workspaceView);

        return panel;
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
