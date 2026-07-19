using System.Linq;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NexusScholar.Desktop;
using NexusScholar.Desktop.AppServices;

namespace NexusScholar.Desktop.Acceptance.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DesktopAcceptanceTests
{
    private static HeadlessUnitTestSession? _session;

    [ClassInitialize]
    public static void Initialize(TestContext _)
    {
        _session = HeadlessUnitTestSession.StartNew(
            typeof(DesktopAcceptanceAppBuilder),
            AvaloniaTestIsolationLevel.PerAssembly);
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        _session?.Dispose();
        _session = null;
    }

    [TestMethod]
    public void End_to_end_initialization_import_analyze_verify_backup_restore_reopen_with_scaling_and_focus()
    {
        using var workspaceFolder = new TemporaryDirectory();
        var workspacePath = Path.Combine(workspaceFolder.Path, "workspace");
        var importedSourcePath = Path.Combine(workspaceFolder.Path, "search-export.csv");
        var backupPath = Path.Combine(workspaceFolder.Path, "workspace-backup.zip");
        var restoredWorkspace = Path.Combine(workspaceFolder.Path, "workspace-restored");
        WriteSourceFixture(importedSourcePath);

        Session.Dispatch(
            () =>
            {
                var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
                var window = new MainWindow(viewModel, null);
                window.Show();
                window.Activate();
                PumpUi(window);
                try
                {
                    AssertAutomationNameCoverage(window,
                        [
                            "Workspace",
                            "Imports",
                            "Evidence",
                            "Workspace folder",
                            "Research project title",
                            "Optional stable workspace id",
                            "Open",
                            "Review initialization"
                        ]);
                    AssertHasNoAutomationLabel<Button>(window, "Review import effects");
                    AssertHasNoAutomationLabel<Button>(window, "Review analysis effects");
                    AssertHasNoAutomationLabel<Button>(window, "Verify");

                    AssertKeyboardFocusTransitions(window!, "Workspace folder", "Review initialization");

                    SetText(window!, "Workspace folder", workspacePath);
                    SetText(window!, "Research project title", "Acceptance Workspace");
                    SetText(window!, "Optional stable workspace id", "RR-05");

                    ClickButton(window!, "Review initialization");
                    Assert.IsTrue(viewModel.HasPendingConfirmation);
                    Assert.AreEqual("Initialize local workspace", viewModel.PendingCommandLabel);
                    ClickButton(window!, "Confirm exact effects");
                    Assert.IsFalse(viewModel.HasPendingConfirmation);
                    Assert.IsTrue(viewModel.HasWorkspace);

                    AssertHasNoAutomationLabel<Button>(window, "Review initialization");
                    AssertAutomationNameCoverage(window!,
                        [
                            "Review import effects",
                            "Local CSV, RIS, or BibTeX file",
                            "Review analysis effects",
                            "Verify",
                            "New backup archive path",
                            "Review backup effects",
                            "Backup archive to restore",
                            "New restore workspace folder",
                            "Review restore effects"
                        ]);

                    SetText(window!, "Local CSV, RIS, or BibTeX file", importedSourcePath);
                    SetText(window!, "Optional input id", "rr05-input");
                    SetText(window!, "Optional query text", "RR-05 deterministic local import");
                    ClickButton(window!, "Review import effects");
                    Assert.IsTrue(viewModel.HasPendingConfirmation);
                    Assert.AreEqual("Import local Search export", viewModel.PendingCommandLabel);
                    ClickButton(window!, "Confirm exact effects");
                    Assert.IsFalse(viewModel.HasPendingConfirmation);
                    Assert.IsNotNull(viewModel.Overview);
                    Assert.AreEqual(1, viewModel.Overview!.ImportedRecordCount);

                    ClickButton(window!, "Review analysis effects");
                    Assert.IsTrue(viewModel.HasPendingConfirmation);
                    Assert.AreEqual("Analyze imported evidence", viewModel.PendingCommandLabel);
                    ClickButton(window!, "Confirm exact effects");
                    Assert.IsFalse(viewModel.HasPendingConfirmation);
                    Assert.IsNotNull(viewModel.Overview);

                    ClickButton(window!, "Verify");
                    Assert.IsFalse(viewModel.HasPendingConfirmation);
                    Assert.IsTrue(viewModel.StatusKind is DesktopWorkspaceCommandStatus.Succeeded or DesktopWorkspaceCommandStatus.Attention);
                    AssertUsableLayout(window!,
                        [
                            "Review analysis effects",
                            "Verify",
                            "Review backup effects",
                            "Review restore effects"
                        ],
                        [1d, 1.25d, 1.5d]);

                    SetText(window!, "New backup archive path", backupPath);
                    ClickButton(window!, "Review backup effects");
                    Assert.IsTrue(viewModel.HasPendingConfirmation);
                    Assert.AreEqual("Create verified workspace backup", viewModel.PendingCommandLabel);
                    Assert.AreEqual(DesktopWorkspaceRecoveryKinds.Backup, viewModel.PendingRecoveryPreview!.OperationKind);
                    ClickButton(window!, "Confirm exact effects");
                    Assert.IsTrue(viewModel.StatusKind is DesktopWorkspaceCommandStatus.Succeeded or DesktopWorkspaceCommandStatus.Attention);
                    Assert.IsTrue(File.Exists(backupPath), $"Expected backup archive at '{backupPath}'.");

                    SetText(window!, "Backup archive to restore", backupPath);
                    SetText(window!, "New restore workspace folder", restoredWorkspace);
                    ClickButton(window!, "Review restore effects");
                    Assert.IsTrue(viewModel.HasPendingConfirmation);
                    Assert.AreEqual("Restore verified workspace backup", viewModel.PendingCommandLabel);
                    Assert.AreEqual(DesktopWorkspaceRecoveryKinds.Restore, viewModel.PendingRecoveryPreview!.OperationKind);
                    ClickButton(window!, "Confirm exact effects");
                    Assert.IsFalse(viewModel.HasPendingConfirmation);
                    Assert.IsTrue(viewModel.StatusKind is DesktopWorkspaceCommandStatus.Succeeded or DesktopWorkspaceCommandStatus.Attention);
                    Assert.AreEqual(Path.GetFullPath(restoredWorkspace), viewModel.WorkspacePath);

                    SetText(window!, "Workspace folder", workspacePath);
                    ClickButton(window!, "Open");
                    Assert.AreEqual(Path.GetFullPath(workspacePath), viewModel.WorkspacePath);
                    AssertHasNoAutomationLabel<Button>(window, "Review initialization");
                    Assert.IsTrue(viewModel.Overview!.ImportedRecordCount > 0);
                }
                finally
                {
                    window.Close();
                }
            },
            CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void Recovery_preview_rejects_missing_archive_without_confirmation()
    {
        using var workspaceFolder = new TemporaryDirectory();
        var workspacePath = Path.Combine(workspaceFolder.Path, "workspace");
        var backupPath = Path.Combine(workspaceFolder.Path, "workspace-backup.zip");
        var missingArchivePath = Path.Combine(workspaceFolder.Path, "missing-archive.zip");
        var invalidRestorePath = Path.Combine(workspaceFolder.Path, "restore-missing");
        File.WriteAllText(backupPath, "placeholder archive");

        Session.Dispatch(
            () =>
            {
                var viewModel = new DesktopWorkspaceViewModel(new DesktopWorkspaceCommandFacade());
                var window = new MainWindow(viewModel, null);
                window.Show();
                window.Activate();
                PumpUi(window);
                try
                {
                    AssertAutomationNameCoverage(window,
                        [
                            "Workspace",
                            "Workspace folder",
                            "Research project title",
                            "Review initialization",
                            "Open"
                        ]);
                    AssertHasNoAutomationLabel<Button>(window, "Review import effects");

                    SetText(window, "Workspace folder", workspacePath);
                    SetText(window, "Research project title", "Failure Coverage Workspace");
                    ClickButton(window, "Review initialization");
                    ClickButton(window, "Confirm exact effects");
                    Assert.IsTrue(viewModel.HasWorkspace);
                    AssertHasNoAutomationLabel<Button>(window, "Review initialization");

                    SetText(window, "Backup archive to restore", missingArchivePath);
                    SetText(window, "New restore workspace folder", invalidRestorePath);
                    ClickButton(window, "Review restore effects");

                    Assert.AreEqual(DesktopWorkspaceCommandStatus.Failed, viewModel.StatusKind);
                    Assert.AreEqual("No command pending", viewModel.PendingCommandLabel);
                    Assert.IsFalse(viewModel.HasPendingConfirmation);
                    Assert.AreEqual("The selected backup archive does not exist.", viewModel.Status);
                }
                finally
                {
                    window.Close();
                }
            },
            CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void AssertAutomationNameCoverage(Control root, string[] automationNames)
    {
        foreach (var name in automationNames)
        {
            _ = FindByAutomationName<Control>(root, name);
        }
    }

    private static HeadlessUnitTestSession Session =>
        _session ?? throw new InvalidOperationException("The desktop acceptance test session is not initialized.");

    private static bool HasAutomationLabel<T>(Control root, string automationName) where T : Control
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .Any(control => string.Equals(
                control.GetValue(AutomationProperties.NameProperty) as string,
                automationName,
                StringComparison.Ordinal));
    }

    private static void AssertHasNoAutomationLabel<T>(Control root, string automationName) where T : Control
    {
        Assert.IsFalse(HasAutomationLabel<T>(root, automationName), $"Did not expect '{automationName}' at this UI state.");
    }

    private static void AssertKeyboardFocusTransitions(MainWindow window, string firstFocusName, string secondFocusName)
    {
        var first = FindByAutomationName<Control>(window, firstFocusName);
        var second = FindByAutomationName<Control>(window, secondFocusName);

        first.Focus();
        Assert.IsTrue(first.IsFocused, $"Expected '{firstFocusName}' to accept focus.");

        second.Focus();
        Assert.IsTrue(second.IsFocused, $"Expected '{secondFocusName}' to accept focus.");
    }

    private static void AssertUsableLayout(MainWindow window, string[] controlAutomationNames, double[] scales)
    {
        var controls = controlAutomationNames
            .Select(name => (Name: name, Control: FindByAutomationName<Button>(window, name)))
            .ToArray();
        var previousPixelSize = default(PixelSize?);

        foreach (var scale in scales)
        {
            window.SetRenderScaling(scale);
            window.InvalidateVisual();
            window.UpdateLayout();

            foreach (var control in controls)
            {
                BringIntoClientViewport(window, control.Control);
                Assert.IsTrue(control.Control.IsVisible, $"Expected '{control.Name}' to remain visible at scale {scale}.");
                Assert.IsTrue(control.Control.IsHitTestVisible, $"Expected '{control.Name}' to remain interactive at scale {scale}.");
                Assert.IsTrue(control.Control.Bounds.Width > 0, $"Expected '{control.Name}' to retain width at scale {scale}.");
                Assert.IsTrue(control.Control.Bounds.Height > 0, $"Expected '{control.Name}' to retain height at scale {scale}.");

                var center = FindInputPoint(window, control.Control, control.Name);
                AssertPointInsideClient(window, center, control.Name, scale);
            }

            Assert.IsTrue(window.Bounds.Width > 0);
            Assert.IsTrue(window.Bounds.Height > 0);

            using var frame = window.CaptureRenderedFrame();
            Assert.IsNotNull(frame, $"Expected a rendered frame at scale {scale}.");
            Assert.IsTrue(frame.PixelSize.Width > 0);
            Assert.IsTrue(frame.PixelSize.Height > 0);
            if (previousPixelSize is { } previous)
            {
                Assert.IsTrue(
                    frame.PixelSize.Width > previous.Width && frame.PixelSize.Height > previous.Height,
                    $"Expected physical frame dimensions to increase at scale {scale}.");
            }

            previousPixelSize = frame.PixelSize;
        }

        window.SetRenderScaling(1d);
        PumpUi(window);
    }

    private static T FindByAutomationName<T>(Control root, string automationName) where T : Control
    {
        return root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control => string.Equals(
                control.GetValue(AutomationProperties.NameProperty) as string,
                automationName,
                StringComparison.Ordinal)) ??
            throw new AssertFailedException($"No control found with automation name '{automationName}'.");
    }

    private static void SetText(MainWindow window, string automationName, string value)
    {
        var input = FindByAutomationName<TextBox>(window, automationName);
        BringIntoClientViewport(window, input);
        Assert.IsTrue(input.Focus(), $"Expected '{automationName}' to accept keyboard focus.");
        window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        window.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
        window.KeyTextInput(value);
        PumpUi(window);
        Assert.AreEqual(value, input.Text, $"Text input did not reach '{automationName}'.");
    }

    private static void ClickButton(MainWindow window, string automationName)
    {
        var button = FindByAutomationName<Button>(window, automationName);
        BringIntoClientViewport(window, button);
        Assert.IsTrue(button.IsEnabled, $"Expected '{automationName}' to be enabled.");
        Assert.IsTrue(button.IsVisible, $"Expected '{automationName}' to be visible.");
        Assert.IsTrue(button.IsHitTestVisible, $"Expected '{automationName}' to accept pointer input.");

        var center = FindInputPoint(window, button, automationName);
        AssertPointInsideClient(window, center, automationName, window.RenderScaling);
        window.MouseMove(center, RawInputModifiers.None);
        window.MouseDown(center, MouseButton.Left, RawInputModifiers.LeftMouseButton);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
        PumpUi(window);
    }

    private static string DescribeScroll(Control control)
    {
        var scroll = control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (scroll is null)
        {
            return "no scroll ancestor";
        }

        var localCenter = new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d);
        return $"scroll bounds {scroll.Bounds}, viewport {scroll.Viewport}, extent {scroll.Extent}, " +
            $"offset {scroll.Offset}, control center {control.TranslatePoint(localCenter, scroll)}";
    }

    private static void BringIntoClientViewport(MainWindow window, Control control)
    {
        control.BringIntoView();
        window.UpdateLayout();

        var scroll = control.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (scroll is not null)
        {
            var localCenter = new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d);
            var centerInScroll = control.TranslatePoint(localCenter, scroll);
            if (centerInScroll is { } center &&
                (center.Y < 100 || center.Y > scroll.Bounds.Height - 100))
            {
                var maximumOffset = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                var desiredOffset = Math.Clamp(
                    scroll.Offset.Y + center.Y - scroll.Bounds.Height / 2d,
                    0,
                    maximumOffset);
                scroll.Offset = new Vector(scroll.Offset.X, desiredOffset);
                window.UpdateLayout();
            }
        }

        PumpUi(window);
    }

    private static void PumpUi(MainWindow window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static Point FindInputPoint(MainWindow window, Control control, string automationName)
    {
        var localCenter = new Point(control.Bounds.Width / 2d, control.Bounds.Height / 2d);
        Point approximate;
        try
        {
            approximate = window.PointToClient(control.PointToScreen(localCenter));
        }
        catch (InvalidOperationException exception)
        {
            throw new AssertFailedException($"Unable to locate '{automationName}' in the window.", exception);
        }

        var x = Math.Clamp(approximate.X, 0.5d, window.ClientSize.Width - 0.5d);
        for (var y = 0.5d; y < window.ClientSize.Height; y += 1d)
        {
            var candidate = new Point(x, y);
            if (InputHitsControl(window, candidate, control))
            {
                return candidate;
            }
        }

        for (var y = 2d; y < window.ClientSize.Height; y += 4d)
        {
            for (var scanX = 2d; scanX < window.ClientSize.Width; scanX += 4d)
            {
                var candidate = new Point(scanX, y);
                if (InputHitsControl(window, candidate, control))
                {
                    return candidate;
                }
            }
        }

        throw new AssertFailedException(
            $"No hit-testable point found for '{automationName}'; approximate {approximate} " +
            $"hit {DescribeHit(window, approximate)}; {DescribeScroll(control)}; " +
            $"visual path {DescribeVisualPath(control)}.");
    }

    private static bool InputHitsControl(MainWindow window, Point point, Control control)
    {
        return window.InputHitTest(point) is Visual hit &&
            hit.GetSelfAndVisualAncestors().Contains(control);
    }

    private static string DescribeHit(MainWindow window, Point point)
    {
        if (window.InputHitTest(point) is not Visual hit)
        {
            return "nothing";
        }

        return string.Join(
            " > ",
            hit.GetSelfAndVisualAncestors().Select(visual =>
            {
                var name = visual is Control hitControl
                    ? hitControl.GetValue(AutomationProperties.NameProperty) as string
                    : null;
                return string.IsNullOrWhiteSpace(name)
                    ? $"{visual.GetType().Name}[{visual.Bounds}]"
                    : $"{visual.GetType().Name}({name})[{visual.Bounds}]";
            }));
    }

    private static string DescribeVisualPath(Control control)
    {
        return string.Join(
            " > ",
            control.GetSelfAndVisualAncestors().Select(visual =>
                $"{visual.GetType().Name}[{visual.Bounds};visible={visual.IsVisible};clip={visual.ClipToBounds}]"));
    }

    private static void AssertPointInsideClient(
        MainWindow window,
        Point point,
        string automationName,
        double scale)
    {
        Assert.IsTrue(
            point.X >= 0 &&
            point.Y >= 0 &&
            point.X <= window.ClientSize.Width &&
            point.Y <= window.ClientSize.Height,
            $"Expected '{automationName}' to be inside the client viewport at scale {scale}.");
    }

    private static void WriteSourceFixture(string path)
    {
        File.WriteAllText(
            path,
            "eid,title,author names,year,source title,doi\n" +
            "rr05-1,Deterministic local record,Nexus Scholar,2026,Local Journal,10.1000/rr05", new UTF8Encoding(false));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nexus-desktop-acceptance-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

public static class DesktopAcceptanceAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
}
