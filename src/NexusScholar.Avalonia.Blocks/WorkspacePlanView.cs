using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using NexusScholar.UiContracts;

namespace NexusScholar.Avalonia.Blocks;

public sealed class WorkspacePlanView : Border
{
    private readonly StackPanel _root = new() { Spacing = 12 };
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#0f766e"));
    private static readonly IBrush AccentDark = new SolidColorBrush(Color.Parse("#0b4f4a"));
    private static readonly IBrush BorderLine = new SolidColorBrush(Color.Parse("#d9dedb"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#5e6870"));
    private static readonly IBrush Paper = new SolidColorBrush(Color.Parse("#fbfaf7"));
    private static readonly IBrush Surface = Brushes.White;
    private static readonly IBrush SurfaceTint = new SolidColorBrush(Color.Parse("#f4f1ea"));

    public WorkspacePlanView()
    {
        _root.Margin = new Thickness(2);

        Background = Paper;
        Child = _root;
    }

    public void Render(WorkspacePlan plan, BlockActionCallback? actionCallback = null)
    {
        Render(WorkspacePlanViewModel.FromWorkspacePlan(plan, actionCallback));
    }

    public void Render(WorkspacePlanViewModel plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        _root.Children.Clear();
        _root.Children.Add(BuildPlanHeader(plan));

        if (!string.IsNullOrWhiteSpace(plan.Description))
        {
            _root.Children.Add(Text(plan.Description, 13, foreground: Muted));
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

    internal static TextBlock Text(string text, double size = 12, FontWeight? weight = null, IBrush? foreground = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight ?? FontWeight.Normal,
            Foreground = foreground ?? Brushes.Black,
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
            Background = SurfaceTint,
            BorderBrush = BorderLine,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = panel
        };
    }

    internal static Border Badge(string text, IBrush? background = null, IBrush? foreground = null)
    {
        return new Border
        {
            Background = background ?? SurfaceTint,
            BorderBrush = BorderLine,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 3),
            Child = Text(text, 12, FontWeight.SemiBold, foreground ?? AccentDark)
        };
    }

    internal static IBrush SeverityBrush(string severity)
    {
        return severity switch
        {
            "Blocking" or "Error" => new SolidColorBrush(Color.Parse("#9f3a38")),
            "ReviewRequired" or "Warning" => new SolidColorBrush(Color.Parse("#b7791f")),
            _ => Accent
        };
    }

    private static Border BuildPlanHeader(WorkspacePlanViewModel plan)
    {
        var badges = new WrapPanel
        {
            ItemSpacing = 6,
            LineSpacing = 6
        };
        badges.Children.Add(Badge(plan.AuthorityStatus, new SolidColorBrush(Color.Parse("#e4f2ee"))));
        badges.Children.Add(Badge($"Mode: {plan.Mode}"));
        badges.Children.Add(Badge($"Workspace: {plan.WorkspaceId}"));

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Text(plan.Title, 24, FontWeight.Bold));
        panel.Children.Add(badges);

        return new Border
        {
            Background = Surface,
            BorderBrush = BorderLine,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = panel
        };
    }
}
