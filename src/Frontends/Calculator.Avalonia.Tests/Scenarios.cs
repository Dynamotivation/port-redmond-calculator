using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Calculator.Avalonia;
using Calculator.Managed;

namespace Calculator.Avalonia.Tests;

internal sealed record Scenario(
    string Name,
    double Width,
    double Height,
    ThemeVariant Theme,
    AppSettings Settings,
    Action<CalculatorViewModel> Arrange)
{
    /// <summary>
    /// Compact always-on-top drops the window minimums to 240x260. Scenarios
    /// below the standard 320x500 floor have to relax them the same way, or the
    /// window clamps back up and the compact layout is never exercised.
    /// </summary>
    public double? MinWidth { get; init; }

    public double? MinHeight { get; init; }

    /// <summary>
    /// Arrange step that needs the view, not just the view model — opening a
    /// popup or a flyout is an interaction with a control, and those surfaces
    /// are exactly the ones the view-model-only scenarios cannot reach.
    /// </summary>
    public Action<MainWindow, CalculatorViewModel>? ArrangeView { get; init; }
}

internal static class Scenarios
{
    private static readonly AppSettings Opaque = new(
        AppThemePreference.Dark,
        FontFamily: "Inter",
        UseMicaEffect: false,
        WindowCornerStyle.Windows11,
        WindowControlStyle.Windows11);

    private static readonly AppSettings Transparent = Opaque with { UseMicaEffect = true };

    // 524x773 is the shipping default size; 900x773 clears the 560 history dock
    // threshold; 1100x900 clears the (w>=1024 && h>=768) fixed-history-width
    // rule; 320x394 is the compact always-on-top size.
    public static IReadOnlyList<Scenario> All { get; } =
    [
        Standard("standard-dark-default", 524, 773, ThemeVariant.Dark, Opaque),
        Standard("standard-light-default", 524, 773, ThemeVariant.Light, Opaque),
        // NOTE: no mica/transparent scenario. Suppressing the AppKit backdrop
        // was not enough to make one reproducible -- the instability is the
        // headless compositor's handling of a translucent window surface, not
        // the native call. Identical code re-run differs by up to 9/255 across
        // most of the frame in light theme, and intermittently by 3/255 in
        // dark. Backdrop parity stays a manual check; a baseline that fails one
        // run in three is worse than none.

        // Height bands for the result row: >=800, the mid band, and the small band.
        Standard("standard-dark-tall", 524, 820, ThemeVariant.Dark, Opaque),
        Standard("standard-dark-short", 340, 520, ThemeVariant.Dark, Opaque),

        // History docked (>=560 wide) and the fixed-320 variant (>=1024x768).
        Standard("standard-dark-history-docked", 900, 773, ThemeVariant.Dark, Opaque),
        Standard("standard-light-history-docked", 900, 773, ThemeVariant.Light, Opaque),
        Standard("standard-dark-history-fixed-width", 1100, 900, ThemeVariant.Dark, Opaque),

        // Narrow history is the overlay presentation below the dock threshold.
        new("standard-dark-history-narrow", 524, 773, ThemeVariant.Dark, Opaque,
            vm => vm.History.ToggleCommand.Execute(null)),

        new("standard-dark-navigation-open", 524, 773, ThemeVariant.Dark, Opaque,
            vm => vm.ToggleNavigationPaneCommand.Execute(null)),

        Mode("scientific-dark-default", CalculatorViewMode.Scientific, 524, 773, ThemeVariant.Dark, Opaque),
        Mode("scientific-light-default", CalculatorViewMode.Scientific, 524, 773, ThemeVariant.Light, Opaque),
        // Scientific size classes are measured on the keypad panel: small,
        // medium (>=527x523) and large (>=878x851).
        Mode("scientific-dark-medium", CalculatorViewMode.Scientific, 700, 900, ThemeVariant.Dark, Opaque),
        Mode("scientific-dark-large", CalculatorViewMode.Scientific, 1000, 1250, ThemeVariant.Dark, Opaque),
        new("scientific-dark-second", 524, 773, ThemeVariant.Dark, Opaque,
            vm =>
            {
                SwitchTo(vm, CalculatorViewMode.Scientific);
                vm.Scientific.IsInverse = true;
            }),

        Mode("programmer-dark-default", CalculatorViewMode.Programmer, 524, 773, ThemeVariant.Dark, Opaque),
        Mode("programmer-light-default", CalculatorViewMode.Programmer, 524, 773, ThemeVariant.Light, Opaque),
        // The programmer operator panel drops its labels below 630 keypad DIPs.
        Mode("programmer-dark-labels", CalculatorViewMode.Programmer, 700, 900, ThemeVariant.Dark, Opaque),

        Mode("converter-dark-default", CalculatorViewMode.Length, 524, 773, ThemeVariant.Dark, Opaque),
        Mode("converter-light-default", CalculatorViewMode.Length, 524, 773, ThemeVariant.Light, Opaque),
        DateDifference("date-dark-difference", ThemeVariant.Dark),
        DateDifference("date-light-difference", ThemeVariant.Light),
        new("date-dark-add", 524, 773, ThemeVariant.Dark, Opaque,
            vm =>
            {
                SwitchTo(vm, CalculatorViewMode.Date);
                vm.DateCalculator.SelectedCalculationIndex = 1;
                vm.DateCalculator.StartDate = new DateTime(2024, 1, 31);
                vm.DateCalculator.MonthsOffset = 1;
            }),

        new("settings-dark", 524, 773, ThemeVariant.Dark, Opaque,
            vm => vm.OpenSettingsCommand.Execute(null)),
        new("settings-light", 524, 773, ThemeVariant.Light, Opaque,
            vm => vm.OpenSettingsCommand.Execute(null)),

        // Compact always-on-top: the shell collapses every row but result+keypad.
        // The short variant sits under the 260 threshold that switches the
        // result row to its 20/18pt minimum.
        new("standard-dark-compact", 320, 394, ThemeVariant.Dark, Opaque,
            vm => vm.IsAlwaysOnTop = true) { MinWidth = 240, MinHeight = 260 },
        new("standard-dark-compact-short", 320, 260, ThemeVariant.Dark, Opaque,
            vm => vm.IsAlwaysOnTop = true) { MinWidth = 240, MinHeight = 250 },

        // Memory and history content, so the list templates are covered too.
        new("standard-dark-history-entries", 900, 773, ThemeVariant.Dark, Opaque,
            vm =>
            {
                EnterExpression(vm, CalculatorCommand.One, CalculatorCommand.Add, CalculatorCommand.Two);
                EnterExpression(vm, CalculatorCommand.Nine, CalculatorCommand.Multiply, CalculatorCommand.Eight);
                vm.Memory.StoreCommand.Execute(null);
            }),
        new("standard-dark-error", 524, 773, ThemeVariant.Dark, Opaque,
            vm => EnterExpression(vm, CalculatorCommand.One, CalculatorCommand.Divide, CalculatorCommand.Zero)),

        // Surfaces that only exist while a popup or flyout is open. Ownership of
        // all of these moved during the decomposition -- the memory popup to
        // MemoryPanel, the trig and function flyouts to ScientificCalculatorView
        // -- so they need to be rendered, not merely compiled.
        new("standard-dark-memory-popup", 524, 773, ThemeVariant.Dark, Opaque,
            vm =>
            {
                vm.ExecuteCalculatorCommand(CalculatorCommand.Four);
                vm.Memory.StoreCommand.Execute(null);
                vm.ExecuteCalculatorCommand(CalculatorCommand.Nine);
                vm.Memory.StoreCommand.Execute(null);
            })
        {
            ArrangeView = (window, _) => Click(window, "MemoryFlyoutButton"),
        },

        new("scientific-dark-trig-flyout", 524, 773, ThemeVariant.Dark, Opaque,
            vm => SwitchTo(vm, CalculatorViewMode.Scientific))
        {
            ArrangeView = (window, _) => OpenFlyout(FindByName<Button>(window, "ScientificTrigButton")),
        },

        // 2nd and hyp change which trig group the open flyout offers. This is
        // the state behind the flyoutStateToggle marker class that replaced the
        // command-reference comparison.
        new("scientific-dark-trig-flyout-hyperbolic", 524, 773, ThemeVariant.Dark, Opaque,
            vm =>
            {
                SwitchTo(vm, CalculatorViewMode.Scientific);
                vm.Scientific.IsTrigInverse = true;
                vm.Scientific.IsTrigHyperbolic = true;
            })
        {
            ArrangeView = (window, _) => OpenFlyout(FindByName<Button>(window, "ScientificTrigButton")),
        },

        new("scientific-dark-function-flyout", 524, 773, ThemeVariant.Dark, Opaque,
            vm => SwitchTo(vm, CalculatorViewMode.Scientific))
        {
            ArrangeView = (window, _) => OpenFlyout(FindByName<Button>(window, "ScientificFunctionButton")),
        },

        // The bit-flip surface replaces the programmer keypad entirely.
        new("programmer-dark-bit-flip", 524, 773, ThemeVariant.Dark, Opaque,
            vm =>
            {
                SwitchTo(vm, CalculatorViewMode.Programmer);
                EnterExpression(vm, CalculatorCommand.Two, CalculatorCommand.Add, CalculatorCommand.Five);
                vm.Programmer.ToggleBitFlipCommand.Execute(null);
            }),

        new("programmer-dark-bitwise-flyout", 524, 773, ThemeVariant.Dark, Opaque,
            vm => SwitchTo(vm, CalculatorViewMode.Programmer))
        {
            ArrangeView = (window, _) => OpenFlyout(ButtonLabelled(window, "ProgrammerBitwiseLabel")),
        },

        new("programmer-dark-bit-shift-flyout", 524, 773, ThemeVariant.Dark, Opaque,
            vm => SwitchTo(vm, CalculatorViewMode.Programmer))
        {
            ArrangeView = (window, _) => OpenFlyout(ButtonLabelled(window, "ProgrammerBitShiftLabel")),
        },
    ];

    private static T FindByName<T>(MainWindow window, string name)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().FirstOrDefault(control => control.Name == name)
        ?? throw new InvalidOperationException($"No {typeof(T).Name} named '{name}' in the visual tree.");

    /// <summary>
    /// The programmer operator-panel buttons carry no name of their own, so they
    /// are located through the label inside their content.
    /// </summary>
    private static Button ButtonLabelled(MainWindow window, string labelName) =>
        FindByName<TextBlock>(window, labelName).GetVisualAncestors().OfType<Button>().FirstOrDefault()
        ?? throw new InvalidOperationException($"'{labelName}' has no ancestor button.");

    private static void Click(MainWindow window, string name) =>
        FindByName<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>
    /// Opens a button's attached flyout directly. These buttons open their
    /// flyout through the framework rather than through a command, so raising
    /// Click would not show it.
    /// </summary>
    private static void OpenFlyout(Button button) => button.Flyout?.ShowAt(button);

    private static Scenario Standard(
        string name,
        double width,
        double height,
        ThemeVariant theme,
        AppSettings settings) =>
        new(name, width, height, theme, settings, static _ => { });

    private static Scenario Mode(
        string name,
        CalculatorViewMode mode,
        double width,
        double height,
        ThemeVariant theme,
        AppSettings settings) =>
        new(name, width, height, theme, settings, vm => SwitchTo(vm, mode));

    private static Scenario DateDifference(string name, ThemeVariant theme) =>
        new(name, 524, 773, theme, Opaque,
            vm =>
            {
                SwitchTo(vm, CalculatorViewMode.Date);
                vm.DateCalculator.FromDate = new DateTime(2024, 1, 1);
                vm.DateCalculator.ToDate = new DateTime(2025, 2, 10);
            });

    private static void SwitchTo(CalculatorViewModel viewModel, CalculatorViewMode mode)
    {
        var item = viewModel.CalculatorNavigationItems
            .Concat(viewModel.ConverterNavigationItems)
            .FirstOrDefault(candidate => candidate.Mode == mode)
            ?? throw new InvalidOperationException($"No navigation item for {mode}.");
        viewModel.SelectNavigationItemCommand.Execute(item);
    }

    private static void EnterExpression(CalculatorViewModel viewModel, params CalculatorCommand[] commands)
    {
        foreach (var command in commands)
        {
            viewModel.ExecuteCalculatorCommand(command);
        }

        viewModel.ExecuteCalculatorCommand(CalculatorCommand.Equals);
    }
}
