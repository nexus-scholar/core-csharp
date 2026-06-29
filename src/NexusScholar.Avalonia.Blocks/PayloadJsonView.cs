using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NexusScholar.Avalonia.Blocks;

public sealed class PayloadJsonView : UserControl
{
    public void Render(PayloadJsonViewModel payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        Content = payload.HasPayload
            ? new Expander
            {
                Header = "Payload JSON",
                Content = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#20272a")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10),
                    Child = WorkspacePlanView.Text(payload.Json!, 12, foreground: new SolidColorBrush(Color.Parse("#f5f3ed")))
                }
            }
            : WorkspacePlanView.Text("No payload JSON.", 12);
    }
}
