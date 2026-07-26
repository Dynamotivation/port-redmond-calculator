using Avalonia.Styling;
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
            vm => vm.ToggleHistoryCommand.Execute(null)),

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
                vm.IsScientificInverse = true;
            }),

        Mode("programmer-dark-default", CalculatorViewMode.Programmer, 524, 773, ThemeVariant.Dark, Opaque),
        Mode("programmer-light-default", CalculatorViewMode.Programmer, 524, 773, ThemeVariant.Light, Opaque),
        // The programmer operator panel drops its labels below 630 keypad DIPs.
        Mode("programmer-dark-labels", CalculatorViewMode.Programmer, 700, 900, ThemeVariant.Dark, Opaque),

        Mode("converter-dark-default", CalculatorViewMode.Length, 524, 773, ThemeVariant.Dark, Opaque),
        Mode("converter-light-default", CalculatorViewMode.Length, 524, 773, ThemeVariant.Light, Opaque),

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
                vm.MemoryStoreCommand.Execute(null);
            }),
        new("standard-dark-error", 524, 773, ThemeVariant.Dark, Opaque,
            vm => EnterExpression(vm, CalculatorCommand.One, CalculatorCommand.Divide, CalculatorCommand.Zero)),
    ];

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
