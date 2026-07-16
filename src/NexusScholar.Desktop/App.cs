using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using NexusScholar.Desktop.AppServices;

namespace NexusScholar.Desktop;

public sealed class App : Application
{
    public App()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var initialPath = desktop.Args is { Length: > 0 } ? desktop.Args[0] : null;
            desktop.MainWindow = new MainWindow(
                new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade()),
                initialPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
