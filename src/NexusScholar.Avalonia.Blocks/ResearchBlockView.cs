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
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Child = _root
        };
    }

    public void Render(ResearchBlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);

        _root.Children.Clear();
        _root.Children.Add(WorkspacePlanView.Text($"{block.Order}. {block.Title}", 17, FontWeight.SemiBold));
        _root.Children.Add(WorkspacePlanView.Text($"Mode: {block.Mode}  Severity: {block.Severity}  Source: {block.SourceKind}", 12));
        _root.Children.Add(WorkspacePlanView.Text($"Kind: {block.Kind}  Block: {block.BlockId}", 12));

        if (!string.IsNullOrWhiteSpace(block.Summary))
        {
            _root.Children.Add(WorkspacePlanView.Text(block.Summary, 12));
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
