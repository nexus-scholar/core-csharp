using System.Text;
using System.Text.Json;
using NexusScholar.Desktop.AppServices;

namespace NexusScholar.Desktop;

internal static class DesktopReleaseSmoke
{
    internal const string Argument = "--release-smoke";

    private static readonly DateTimeOffset FixedTime =
        new(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);

    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Any(value => string.Equals(value, Argument, StringComparison.Ordinal));

    public static int Run()
    {
        var root = Path.Combine(Path.GetTempPath(), $"nexus-desktop-release-smoke-{Guid.NewGuid():N}");
        var workspace = Path.Combine(root, "workspace");
        Directory.CreateDirectory(workspace);

        try
        {
            var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
            viewModel.PreviewInitialize(workspace, "Release smoke", "release-smoke", FixedTime);
            RequirePending(viewModel, "initialize");
            viewModel.ConfirmPending();
            RequireCompleted(viewModel, "initialize");

            var sourcePath = Path.Combine(root, "source.csv");
            File.WriteAllText(
                sourcePath,
                "eid,title,author names,year,source title,doi\n" +
                "release-1,Release smoke record,Nexus Scholar,2026,Local Journal,10.1000/release-smoke\n",
                new UTF8Encoding(false));

            viewModel.PreviewImport(
                sourcePath,
                "scopus",
                "csv",
                "release-search",
                "local deterministic release smoke",
                FixedTime);
            RequirePending(viewModel, "import");
            viewModel.ConfirmPending();
            RequireCompleted(viewModel, "import");

            viewModel.PreviewAnalyze(FixedTime);
            RequirePending(viewModel, "analyze");
            viewModel.ConfirmPending();
            RequireCompleted(viewModel, "analyze");

            viewModel.Verify();
            RequireCompleted(viewModel, "verify");

            var liveProviderCapabilityLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name ?? string.Empty)
                .Any(name => name.StartsWith(
                    "NexusScholar.Search.Providers.",
                    StringComparison.Ordinal));
            var output = new
            {
                schema = "nexus.desktop.release-smoke.v1",
                status = "passed",
                version = DesktopReleaseIdentity.Version,
                framework = DesktopReleaseIdentity.Framework,
                runtimeIdentifier = DesktopReleaseIdentity.RuntimeIdentifier,
                architecture = DesktopReleaseIdentity.Architecture,
                workspaceState = viewModel.Overview?.State,
                inputCount = viewModel.Overview?.InputCount,
                importedRecordCount = viewModel.Overview?.ImportedRecordCount,
                localWorkflowCompleted = true,
                liveProviderCapabilityLoaded
            };
            Console.Out.WriteLine(JsonSerializer.Serialize(output));
            return 0;
        }
        catch (Exception exception)
        {
            var output = new
            {
                schema = "nexus.desktop.release-smoke.v1",
                status = "failed",
                failureType = exception.GetType().FullName
            };
            Console.Error.WriteLine(JsonSerializer.Serialize(output));
            return 70;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A smoke cleanup failure must not mask the product validation result.
            }
        }
    }

    private static void RequirePending(DesktopWorkspaceViewModel viewModel, string operation)
    {
        if (!viewModel.HasPendingConfirmation)
        {
            throw new InvalidOperationException($"Release smoke {operation} did not produce an exact confirmation preview.");
        }
    }

    private static void RequireCompleted(DesktopWorkspaceViewModel viewModel, string operation)
    {
        if (viewModel.StatusKind is not (
            DesktopWorkspaceCommandStatus.Succeeded or DesktopWorkspaceCommandStatus.Attention))
        {
            throw new InvalidOperationException($"Release smoke {operation} failed.");
        }
    }
}
