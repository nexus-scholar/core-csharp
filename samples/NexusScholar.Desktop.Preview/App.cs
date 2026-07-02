using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace NexusScholar.Desktop.Preview;

public sealed class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(new DesktopPreviewViewModel());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
