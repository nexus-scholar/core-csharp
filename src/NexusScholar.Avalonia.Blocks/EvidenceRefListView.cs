using Avalonia.Controls;

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

            _root.Children.Add(WorkspacePlanView.Text(detail, 12));
        }
    }
}
