using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NexusScholar.Avalonia.Blocks;

public sealed class BlockActionListView : UserControl
{
    private readonly StackPanel _root = new() { Spacing = 6 };

    public BlockActionListView()
    {
        Content = _root;
    }

    public void Render(IReadOnlyList<BlockActionViewModel> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        _root.Children.Clear();
        if (actions.Count == 0)
        {
            _root.Children.Add(WorkspacePlanView.Text("No actions supplied.", 12));
            return;
        }

        foreach (var action in actions)
        {
            var button = new Button
            {
                Content = action.Label,
                Padding = new Thickness(10, 5)
            };
            button.Click += (_, _) => action.Invoke();

            var panel = new StackPanel { Spacing = 2 };
            panel.Children.Add(button);
            panel.Children.Add(WorkspacePlanView.Text("Placeholder callback only; no Core command is executed.", 12, foreground: new SolidColorBrush(Color.Parse("#5e6870"))));
            panel.Children.Add(WorkspacePlanView.Text(
                $"Kind: {action.Kind}  Requires human confirmation: {action.RequiresHumanConfirmation}  Destructive: {action.IsDestructive}",
                12));

            if (!string.IsNullOrWhiteSpace(action.CommandKind) || !string.IsNullOrWhiteSpace(action.TargetRef))
            {
                panel.Children.Add(WorkspacePlanView.Text($"Command: {action.CommandKind ?? "(none)"}  Target: {action.TargetRef ?? "(none)"}", 12));
            }

            _root.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.Parse("#fbfaf7")),
                BorderBrush = new SolidColorBrush(Color.Parse("#d9dedb")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8),
                Child = panel
            });
        }
    }
}
