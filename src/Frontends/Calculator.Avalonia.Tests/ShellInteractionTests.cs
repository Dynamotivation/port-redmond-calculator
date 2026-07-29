using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Calculator.Avalonia.Views;
using Calculator.Avalonia.Views.Graphing;
using Calculator.Managed;
using Redmond.Shortcuts;

namespace Calculator.Avalonia.Tests;

internal static class ShellInteractionTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("closed navigation toggle remains hit-testable", ClosedNavigationToggleRemainsHitTestable),
        ("Alt+2 navigates from settings", AltTwoNavigatesFromSettings),
        ("Alt+3 navigates to graphing", AltThreeNavigatesToGraphing),
        ("Alt+H toggles navigation pane", AltHTogglesNavigationPane),
        ("date navigation shortcuts select date calculation", DateNavigationShortcutsSelectDateCalculation),
        ("macOS graph shortcut uses unified binding and text", MacGraphShortcutUsesUnifiedBindingAndText),
        ("opening shell surfaces transfers focus", OpeningShellSurfacesTransfersFocus),
    ];

    private static void ClosedNavigationToggleRemainsHitTestable()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Dark,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11));

        try
        {
            window.Show();
            Pump();

            var viewModel = (CalculatorViewModel)window.DataContext!;
            var toggle = window.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(button => button.Name == "NavigationToggleButton")
                ?? throw new InvalidOperationException("Navigation toggle is missing.");

            Assert(!viewModel.IsNavigationPaneOpen, "navigation pane should start closed");
            Assert(toggle.IsHitTestVisible, "navigation toggle should accept pointer input");
            Assert(
                toggle.GetVisualAncestors().OfType<InputElement>().All(element => element.IsHitTestVisible),
                "a navigation toggle ancestor blocks pointer input while the pane is closed");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void AltTwoNavigatesFromSettings() => Run((window, viewModel) =>
    {
        viewModel.OpenSettingsCommand.Execute(null);
        Pump();
        Assert(viewModel.IsSettingsOpen, "settings should be open before Alt+2");

        window.KeyPressQwerty(PhysicalKey.Digit2, RawInputModifiers.Alt);
        Pump();
        Assert(viewModel.IsScientificMode, "Alt+2 should select scientific mode");
        Assert(!viewModel.IsSettingsOpen, "Alt+2 should close settings");
    });

    private static void AltHTogglesNavigationPane() => Run((window, viewModel) =>
    {
        Assert(!viewModel.IsNavigationPaneOpen, "navigation pane should start closed");
        window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.Alt);
        Pump();
        Assert(viewModel.IsNavigationPaneOpen, "Alt+H should open the navigation pane");
    });

    private static void AltThreeNavigatesToGraphing() => Run((window, viewModel) =>
    {
        window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
        Pump();
        Assert(viewModel.IsGraphingMode, "Alt+3 should select Graphing");
        Assert(
            window.FocusManager?.GetFocusedElement() is Control
            {
                Name: "Plot",
                IsEffectivelyVisible: true,
            },
            "narrow Graphing navigation should focus the visible graph");
    });

    private static void OpeningShellSurfacesTransfersFocus() => Run((window, viewModel) =>
    {
        viewModel.ToggleNavigationPaneCommand.Execute(null);
        Pump();
        var focused = window.FocusManager?.GetFocusedElement();
        Assert(
            focused is Button { DataContext: CalculatorNavigationItem { IsSelected: true } },
            "opening navigation should focus the selected navigation item");

        viewModel.OpenSettingsCommand.Execute(null);
        Pump();
        focused = window.FocusManager?.GetFocusedElement();
        Assert(
            focused is Visual { IsEffectivelyVisible: true } visual
            && visual.GetVisualAncestors().Any(ancestor => ancestor is SettingsView),
            "opening settings should focus its first interactive control");
    });

    private static void DateNavigationShortcutsSelectDateCalculation() => Run((window, viewModel) =>
    {
        window.KeyPressQwerty(PhysicalKey.Digit5, RawInputModifiers.Alt);
        Pump();
        Assert(viewModel.IsDateCalculatorMode, "Alt+5 should select Date Calculation");
        Assert(
            window.FocusManager?.GetFocusedElement() is Control { Name: "CalculationModeSelector" },
            "Date Calculation should focus its calculation-mode selector");

        viewModel.TrySelectNavigationMode(CalculatorViewMode.Standard);
        Pump();
        window.KeyPressQwerty(PhysicalKey.E, RawInputModifiers.Control);
        Pump();
        Assert(viewModel.IsDateCalculatorMode, "Ctrl+E should select Date Calculation");
    });

    private static void MacGraphShortcutUsesUnifiedBindingAndText()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(
            new AppSettings(
                AppThemePreference.Dark,
                "Inter",
                UseMicaEffect: false,
                WindowCornerStyle.Windows11,
                WindowControlStyle.Windows11),
            shortcutPlatformOverride: ShortcutPlatform.MacOS);

        try
        {
            window.Show();
            Pump();
            var viewModel = (CalculatorViewModel)window.DataContext!;
            viewModel.TrySelectNavigationMode(CalculatorViewMode.Graphing);
            Pump();

            var plot = window.GetVisualDescendants()
                .OfType<GraphCanvas>()
                .Single();
            plot.SetViewport(-2, 2, -3, 3);
            Assert(plot.IsManualAdjustment, "the test viewport should begin in manual mode");
            Assert(
                viewModel.Graphing.Strings.ResetViewTooltip.EndsWith("(⌘0)", StringComparison.Ordinal),
                "the graph tooltip did not use the macOS gesture from the shared formatter");
            Assert(
                viewModel.Memory.Strings.StoreTooltip.EndsWith("(⌃M)", StringComparison.Ordinal)
                && viewModel.History.Strings.ToggleTooltip.EndsWith("(⌃H)", StringComparison.Ordinal)
                && viewModel.EnterAlwaysOnTopTooltip.EndsWith("(⌥↑)", StringComparison.Ordinal),
                "shortcut-bearing tooltips did not reflect their actual macOS catalog gestures");

            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Meta);
            Pump();
            Assert(
                !plot.IsManualAdjustment,
                "Command+0 did not dispatch the graph reset binding through the shared shortcut service");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Run(Action<MainWindow, CalculatorViewModel> body)
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Dark,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11));

        try
        {
            window.Show();
            Pump();
            body(window, (CalculatorViewModel)window.DataContext!);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Pump()
    {
        for (var tick = 0; tick < 10; tick++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }
}
