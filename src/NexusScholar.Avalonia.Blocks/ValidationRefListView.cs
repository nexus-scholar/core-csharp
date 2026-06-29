using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NexusScholar.Avalonia.Blocks;

public sealed class ValidationRefListView : UserControl
{
    private readonly StackPanel _root = new() { Spacing = 4 };

    public ValidationRefListView()
    {
        Content = _root;
    }

    public void Render(IReadOnlyList<ValidationRefViewModel> validationRefs)
    {
        ArgumentNullException.ThrowIfNull(validationRefs);

        _root.Children.Clear();
        if (validationRefs.Count == 0)
        {
            _root.Children.Add(WorkspacePlanView.Text("No validation refs supplied.", 12));
            return;
        }

        foreach (var validation in validationRefs)
        {
            var detail = $"{validation.Severity}: {validation.Code}";
            if (!string.IsNullOrWhiteSpace(validation.Target))
            {
                detail += $"  Target: {validation.Target}";
            }

            if (!string.IsNullOrWhiteSpace(validation.Message))
            {
                detail += $"  {validation.Message}";
            }

            _root.Children.Add(new Border
            {
                Background = Brushes.White,
                BorderBrush = WorkspacePlanView.SeverityBrush(validation.Severity),
                BorderThickness = new Thickness(3, 1, 1, 1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Child = WorkspacePlanView.Text(detail, 12)
            });
        }
    }
}
