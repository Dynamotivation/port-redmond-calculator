using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Calculator.Managed;

namespace Calculator.Avalonia.Tests;

/// <summary>
/// Guards the keyboard pressed-state feedback. This replaced a map of every
/// named button in the window with a per-control claim, so it needs coverage
/// that does not depend on where a button currently lives — the assertions walk
/// the visual tree for whatever carries the pressed class.
/// </summary>
internal static class PressedStateTests
{
    private const string PressedClass = "keyboardPressed";

    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("digit key marks exactly one button", DigitKeyMarksExactlyOneButton),
        ("key release clears the pressed button", KeyReleaseClearsPressedButton),
        ("pressed state follows the active mode", PressedStateFollowsActiveMode),
        ("converter digit shows pressed feedback", ConverterDigitShowsPressedFeedback),
    ];

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static IReadOnlyList<Button> PressedButtons(Visual root) =>
        root.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains(PressedClass))
            .ToList();

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

    private static void Pump()
    {
        for (var tick = 0; tick < 10; tick++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void DigitKeyMarksExactlyOneButton() => Run((window, _) =>
    {
        window.KeyPressQwerty(PhysicalKey.Digit5, RawInputModifiers.None);
        Pump();

        var pressed = PressedButtons(window);
        Assert(pressed.Count == 1, $"expected one pressed button, found {pressed.Count}");
        Assert(
            pressed[0].Content is "5",
            $"expected the 5 button to be pressed, was '{pressed[0].Content}'");
    });

    private static void KeyReleaseClearsPressedButton() => Run((window, _) =>
    {
        window.KeyPressQwerty(PhysicalKey.Digit7, RawInputModifiers.None);
        Pump();
        Assert(PressedButtons(window).Count == 1, "key down should mark a button");

        window.KeyReleaseQwerty(PhysicalKey.Digit7, RawInputModifiers.None);
        Pump();
        Assert(PressedButtons(window).Count == 0, "key up should clear the pressed button");
    });

    /// <summary>
    /// Every mode declares its own keypad, so the same key has to light the
    /// button belonging to the mode on screen — never a hidden one from another.
    /// </summary>
    private static void PressedStateFollowsActiveMode() => Run((window, viewModel) =>
    {
        foreach (var mode in new[]
        {
            CalculatorViewMode.Standard,
            CalculatorViewMode.Scientific,
            CalculatorViewMode.Programmer,
        })
        {
            var item = viewModel.CalculatorNavigationItems.First(candidate => candidate.Mode == mode);
            viewModel.SelectNavigationItemCommand.Execute(item);
            Pump();

            window.KeyPressQwerty(PhysicalKey.Digit1, RawInputModifiers.None);
            Pump();

            var pressed = PressedButtons(window);
            Assert(pressed.Count == 1, $"{mode}: expected one pressed button, found {pressed.Count}");
            Assert(
                pressed[0].IsEffectivelyVisible,
                $"{mode}: the pressed button should be the visible one");

            window.KeyReleaseQwerty(PhysicalKey.Digit1, RawInputModifiers.None);
            Pump();
            Assert(PressedButtons(window).Count == 0, $"{mode}: key up should clear the pressed button");
        }
    });

    private static void ConverterDigitShowsPressedFeedback() => Run((window, viewModel) =>
    {
        var item = viewModel.ConverterNavigationItems.First(candidate => candidate.IsEnabled);
        viewModel.SelectNavigationItemCommand.Execute(item);
        Pump();

        window.KeyPressQwerty(PhysicalKey.Digit5, RawInputModifiers.None);
        Pump();

        var pressed = PressedButtons(window);
        Assert(pressed.Count == 1, $"converter: expected one pressed button, found {pressed.Count}");
        Assert(pressed[0].Content is "5", "converter: expected the 5 button to be pressed");
        Assert(
            viewModel.Converter.FromDisplay.Contains('5'),
            $"converter key should update its display, was '{viewModel.Converter.FromDisplay}'");

        window.KeyReleaseQwerty(PhysicalKey.Digit5, RawInputModifiers.None);
        Pump();
        Assert(PressedButtons(window).Count == 0, "converter: key up should clear pressed feedback");
    });
}
