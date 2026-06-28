using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using NexusScholar.UiContracts;

namespace NexusScholar.Avalonia.Blocks;

public sealed class WorkspacePlanView : UserControl
{
    private readonly StackPanel _root = new() { Spacing = 12 };

    public WorkspacePlanView()
    {
        Content = new ScrollViewer
        {
            Content = _root,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    public void Render(WorkspacePlan plan, BlockActionCallback? actionCallback = null)
    {
        Render(WorkspacePlanViewModel.FromWorkspacePlan(plan, actionCallback));
    }

    public void Render(WorkspacePlanViewModel plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        _root.Children.Clear();
        _root.Children.Add(Text(plan.Title, 22, FontWeight.SemiBold));
        _root.Children.Add(Text(plan.AuthorityStatus, 13, FontWeight.SemiBold));
        _root.Children.Add(Text($"Mode: {plan.Mode}  Workspace: {plan.WorkspaceId}", 12));

        if (!string.IsNullOrWhiteSpace(plan.Description))
        {
            _root.Children.Add(Text(plan.Description, 12));
        }

        if (plan.ContextRefs.Count > 0)
        {
            var contextRefs = new EvidenceRefListView();
            contextRefs.Render(plan.ContextRefs);
            _root.Children.Add(Headered("Context evidence", contextRefs));
        }

        foreach (var block in plan.Blocks)
        {
            var view = new ResearchBlockView();
            view.Render(block);
            _root.Children.Add(view);
        }
    }

    internal static TextBlock Text(string text, double size = 12, FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight ?? FontWeight.Normal,
            TextWrapping = TextWrapping.Wrap
        };
    }

    internal static Border Headered(string header, Control content)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(Text(header, 13, FontWeight.SemiBold));
        panel.Children.Add(content);

        return new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8),
            Child = panel
        };
    }
}
