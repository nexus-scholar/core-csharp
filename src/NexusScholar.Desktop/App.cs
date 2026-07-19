using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using NexusScholar.Desktop.AppServices;

namespace NexusScholar.Desktop;

public sealed class App : Application
{
    public App()
    {
        Styles.Add(new FluentTheme());
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var initialPath = desktop.Args is { Length: > 0 } ? desktop.Args[0] : null;
            var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
            var latestReport = DesktopCrashDiagnostics.GetLatestReportPath();
            if (latestReport is not null)
            {
                viewModel.ApplyStartupDiagnosticNotice(latestReport);
            }
            desktop.MainWindow = new MainWindow(
                viewModel,
                initialPath);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnDispatcherUnhandledException(
        object? sender,
        DispatcherUnhandledExceptionEventArgs args)
    {
        _ = DesktopCrashDiagnostics.TryRecordFailure(args.Exception, "ui-dispatcher", out _);
    }
}
