using Avalonia;

namespace NexusScholar.Desktop;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        RegisterProcessDiagnostics();
        try
        {
            if (DesktopReleaseSmoke.IsRequested(args))
            {
                return DesktopReleaseSmoke.Run();
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception exception)
        {
            _ = DesktopCrashDiagnostics.TryRecordFailure(exception, "startup", out _);
            return 70;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();

    private static void RegisterProcessDiagnostics()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                _ = DesktopCrashDiagnostics.TryRecordFailure(exception, "app-domain", out _);
            }
        };
        TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            _ = DesktopCrashDiagnostics.TryRecordFailure(args.Exception, "task-scheduler", out _);
        };
    }
}
