using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace NexusScholar.Desktop.Preview;

public sealed class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var initialWorkspacePath = desktop.Args is { Length: > 0 }
                ? desktop.Args[0]
                : null;
            desktop.MainWindow = new MainWindow(new DesktopPreviewViewModel(), initialWorkspacePath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
