using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NexusScholar.Avalonia.Blocks;

public sealed class ResearchBlockView : UserControl
{
    private readonly StackPanel _root = new() { Spacing = 8 };

    public ResearchBlockView()
    {
        Content = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#d9dedb")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 2),
            Child = _root
        };
    }

    public void Render(ResearchBlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);

        _root.Children.Clear();
        if (Content is Border border)
        {
            border.BorderBrush = WorkspacePlanView.SeverityBrush(block.Severity);
            border.BorderThickness = new Thickness(4, 1, 1, 1);
        }

        _root.Children.Add(WorkspacePlanView.Text($"{block.Order}. {block.Title}", 18, FontWeight.Bold));

        var metadata = new WrapPanel
        {
            ItemSpacing = 6,
            LineSpacing = 6
        };
        metadata.Children.Add(WorkspacePlanView.Badge($"Mode: {block.Mode}"));
        metadata.Children.Add(WorkspacePlanView.Badge($"Severity: {block.Severity}", foreground: WorkspacePlanView.SeverityBrush(block.Severity)));
        metadata.Children.Add(WorkspacePlanView.Badge($"Source: {block.SourceKind}"));
        metadata.Children.Add(WorkspacePlanView.Badge($"Kind: {block.Kind}"));
        _root.Children.Add(metadata);

        _root.Children.Add(WorkspacePlanView.Text($"Block: {block.BlockId}", 12, foreground: new SolidColorBrush(Color.Parse("#5e6870"))));

        if (!string.IsNullOrWhiteSpace(block.Summary))
        {
            _root.Children.Add(WorkspacePlanView.Text(block.Summary, 13));
        }

        var evidence = new EvidenceRefListView();
        evidence.Render(block.EvidenceRefs);
        _root.Children.Add(WorkspacePlanView.Headered("Evidence", evidence));

        var validations = new ValidationRefListView();
        validations.Render(block.ValidationRefs);
        _root.Children.Add(WorkspacePlanView.Headered("Validation", validations));

        var actions = new BlockActionListView();
        actions.Render(block.Actions);
        _root.Children.Add(WorkspacePlanView.Headered("Actions", actions));

        var payload = new PayloadJsonView();
        payload.Render(block.Payload);
        _root.Children.Add(payload);
    }
}
