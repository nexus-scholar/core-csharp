using Avalonia.Controls;

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
                Content = WorkspacePlanView.Text(payload.Json!, 12)
            }
            : WorkspacePlanView.Text("No payload JSON.", 12);
    }
}
