using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NexusScholar.Avalonia.Blocks;

public sealed class EvidenceRefListView : UserControl
{
    private readonly StackPanel _root = new() { Spacing = 4 };

    public EvidenceRefListView()
    {
        Content = _root;
    }

    public void Render(IReadOnlyList<EvidenceRefViewModel> evidenceRefs)
    {
        ArgumentNullException.ThrowIfNull(evidenceRefs);

        _root.Children.Clear();
        if (evidenceRefs.Count == 0)
        {
            _root.Children.Add(WorkspacePlanView.Text("No evidence refs supplied.", 12));
            return;
        }

        foreach (var evidence in evidenceRefs)
        {
            var detail = $"{evidence.Kind}: {evidence.DisplayLabel}";
            if (!string.IsNullOrWhiteSpace(evidence.Digest))
            {
                detail += $"  Digest: {evidence.Digest}";
            }

            if (!string.IsNullOrWhiteSpace(evidence.Scope))
            {
                detail += $"  Scope: {evidence.Scope}";
            }

            _root.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.Parse("#d9dedb")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Child = WorkspacePlanView.Text(detail, 12)
            });
        }
    }
}
