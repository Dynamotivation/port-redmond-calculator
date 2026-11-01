using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Calculator.Avalonia.Views.Graphing;
using Calculator.Managed;
using Redmond.Shortcuts;

namespace Calculator.Avalonia.Tests;

internal static class ShortcutModifierSettingTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("macOS shortcut modifier defaults to Command", MacModifierDefaultsToCommand),
        ("macOS Control modifier setting rebinds graph shortcuts", MacControlSettingRebindsGraphShortcuts),
        ("switching the macOS modifier applies live and persists", SwitchingModifierAppliesLiveAndPersists),
        ("shortcut modifier setting is hidden off macOS", ModifierSettingHiddenOffMacOS),
        ("shortcut modifier setting round-trips through the store", ModifierSettingRoundTrips),
        ("missing or invalid stored modifier falls back to Command", MissingOrInvalidModifierFallsBack),
    ];

    private static void MacModifierDefaultsToCommand() => RunGraphing(
        ShortcutPlatform.MacOS,
        new AppSettings(UseMicaEffect: false),
        (window, viewModel, plot) =>
        {
            Assert(viewModel.Settings.SupportsShortcutModifierSettings,
                "the modifier setting should be offered on macOS");
            Assert(viewModel.Settings.IsCommandShortcutModifierSelected,
                "Command should be the default macOS shortcut modifier");
            Assert(viewModel.Graphing.Strings.ResetViewTooltip.EndsWith("(⌘0)", StringComparison.Ordinal),
                "the default graph tooltip should name Command");

            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
            Pump();
            Assert(plot.IsManualAdjustment, "Control+0 should not reset the graph while Command is selected");

            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Meta);
            Pump();
            Assert(!plot.IsManualAdjustment, "Command+0 should reset the graph by default");

            viewModel.OpenSettingsCommand.Execute(null);
            Pump();
            Assert(VisibleText(window).Contains(viewModel.Settings.Strings.ShortcutModifierName),
                "the settings page should show the modifier card on macOS");
        });

    private static void MacControlSettingRebindsGraphShortcuts() => RunGraphing(
        ShortcutPlatform.MacOS,
        new AppSettings(UseMicaEffect: false, ShortcutModifier: MacPrimaryModifier.Control),
        (window, viewModel, plot) =>
        {
            Assert(viewModel.Settings.IsControlShortcutModifierSelected,
                "the persisted Control preference should be selected at startup");
            Assert(viewModel.Graphing.Strings.ResetViewTooltip.EndsWith("(⌃0)", StringComparison.Ordinal)
                && viewModel.Graphing.Strings.ZoomInTooltip.Contains("⌃+", StringComparison.Ordinal),
                "graph tooltips should name Control when it is the selected modifier");

            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Meta);
            Pump();
            Assert(plot.IsManualAdjustment, "Command+0 should not reset the graph while Control is selected");

            window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
            Pump();
            Assert(!plot.IsManualAdjustment, "Control+0 should reset the graph when Control is selected");
        });

    private static void SwitchingModifierAppliesLiveAndPersists()
    {
        DeleteStoredSettings();
        try
        {
            RunGraphing(
                ShortcutPlatform.MacOS,
                new AppSettings(UseMicaEffect: false),
                (window, viewModel, plot) =>
                {
                    var tooltipChanges = 0;
                    viewModel.Graphing.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(GraphingViewModel.Strings))
                        {
                            tooltipChanges++;
                        }
                    };

                    viewModel.Settings.SelectShortcutModifierCommand.Execute("Control");
                    Pump();

                    Assert(tooltipChanges == 1, "switching the modifier should re-publish the graph tooltips once");
                    Assert(viewModel.Graphing.Strings.ResetViewTooltip.EndsWith("(⌃0)", StringComparison.Ordinal)
                        && viewModel.Graphing.Strings.ZoomOutTooltip.Contains("⌃-", StringComparison.Ordinal),
                        "graph tooltips should follow the modifier without a restart");
                    Assert(AppSettingsStore.Load().ShortcutModifier == MacPrimaryModifier.Control,
                        "the selected modifier should be persisted");

                    window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Meta);
                    Pump();
                    Assert(plot.IsManualAdjustment, "the previous Command binding should be released");

                    window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
                    Pump();
                    Assert(!plot.IsManualAdjustment, "the Control binding should apply immediately");

                    viewModel.Settings.SelectShortcutModifierCommand.Execute("Command");
                    Pump();
                    Assert(viewModel.Graphing.Strings.ResetViewTooltip.EndsWith("(⌘0)", StringComparison.Ordinal),
                        "switching back should restore the Command tooltip");
                    Assert(AppSettingsStore.Load().ShortcutModifier == MacPrimaryModifier.Command,
                        "switching back should persist Command");
                });
        }
        finally
        {
            DeleteStoredSettings();
        }
    }

    private static void ModifierSettingHiddenOffMacOS()
    {
        foreach (var platform in new[] { ShortcutPlatform.Windows, ShortcutPlatform.Linux })
        {
            RunGraphing(
                platform,
                // A Control value carried over from a Mac must not affect other platforms.
                new AppSettings(UseMicaEffect: false, ShortcutModifier: MacPrimaryModifier.Control),
                (window, viewModel, plot) =>
                {
                    Assert(!viewModel.Settings.SupportsShortcutModifierSettings,
                        $"the modifier setting should be hidden on {platform}");

                    viewModel.OpenSettingsCommand.Execute(null);
                    Pump();
                    var visibleText = VisibleText(window);
                    Assert(!visibleText.Contains(viewModel.Settings.Strings.ShortcutModifierName),
                        $"the settings page should not show the modifier card on {platform}");
                    Assert(visibleText.Contains(viewModel.Settings.Strings.AppFontName),
                        "the settings page should be open while checking for the modifier card");
                    viewModel.TrySelectNavigationMode(CalculatorViewMode.Graphing);
                    Pump();

                    window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Meta);
                    Pump();
                    Assert(plot.IsManualAdjustment, $"Command+0 should not reset the graph on {platform}");

                    window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
                    Pump();
                    Assert(!plot.IsManualAdjustment, $"Control+0 should keep resetting the graph on {platform}");
                });
        }
    }

    private static void ModifierSettingRoundTrips()
    {
        DeleteStoredSettings();
        try
        {
            AppSettingsStore.Save(new AppSettings(ShortcutModifier: MacPrimaryModifier.Control));
            Assert(AppSettingsStore.Load().ShortcutModifier == MacPrimaryModifier.Control,
                "a saved Control modifier should load back");
            AppSettingsStore.Save(new AppSettings(ShortcutModifier: MacPrimaryModifier.Command));
            Assert(AppSettingsStore.Load().ShortcutModifier == MacPrimaryModifier.Command,
                "a saved Command modifier should load back");
        }
        finally
        {
            DeleteStoredSettings();
        }
    }

    private static void MissingOrInvalidModifierFallsBack()
    {
        DeleteStoredSettings();
        try
        {
            Assert(AppSettingsStore.Load().ShortcutModifier == MacPrimaryModifier.Command,
                "a missing settings file should default to Command");

            // A file written before this setting existed.
            WriteStoredSettings("""{"ThemePreference":0,"FontFamily":"Inter","UseMicaEffect":true}""");
            var legacy = AppSettingsStore.Load();
            Assert(legacy.ShortcutModifier == MacPrimaryModifier.Command
                && legacy.ThemePreference == AppThemePreference.Light,
                "an older settings file should load with the Command default");

            WriteStoredSettings("""{"ShortcutModifier":7}""");
            Assert(AppSettingsStore.Load().ShortcutModifier == MacPrimaryModifier.Command,
                "an unknown stored modifier should fall back to Command");
        }
        finally
        {
            DeleteStoredSettings();
        }
    }

    private static void RunGraphing(
        ShortcutPlatform platform,
        AppSettings settings,
        Action<MainWindow, CalculatorViewModel, GraphCanvas> body)
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(settings, enableNativeEffects: false, shortcutPlatformOverride: platform);
        try
        {
            window.Show();
            Pump();
            var viewModel = (CalculatorViewModel)window.DataContext!;
            viewModel.TrySelectNavigationMode(CalculatorViewMode.Graphing);
            Pump();
            var plot = window.GetVisualDescendants().OfType<GraphCanvas>().Single();
            plot.SetViewport(-2, 2, -3, 3);
            Assert(plot.IsManualAdjustment, "the test viewport should begin in manual mode");
            body(window, viewModel, plot);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static string?[] VisibleText(MainWindow window) =>
        window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible)
            .Select(text => text.Text)
            .ToArray();

    private static void WriteStoredSettings(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AppSettingsStore.SettingsPath)!);
        File.WriteAllText(AppSettingsStore.SettingsPath, json);
    }

    private static void DeleteStoredSettings()
    {
        if (File.Exists(AppSettingsStore.SettingsPath))
        {
            File.Delete(AppSettingsStore.SettingsPath);
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
