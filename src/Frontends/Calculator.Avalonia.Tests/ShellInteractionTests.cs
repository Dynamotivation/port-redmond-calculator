using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Calculator.Managed;

namespace Calculator.Avalonia.Tests;

internal static class ShellInteractionTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("closed navigation toggle remains hit-testable", ClosedNavigationToggleRemainsHitTestable),
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
