using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace NexusScholar.Avalonia.Blocks.SampleHost;

public sealed class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(SampleWorkspaceLoader.LoadDefaultSamples());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
