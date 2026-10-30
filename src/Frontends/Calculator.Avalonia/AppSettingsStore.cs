using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Calculator.Managed;
using Redmond.Shortcuts;

namespace Calculator.Avalonia;

internal static class AppSettingsStore
{
    private static string DefaultFontFamily =>
        OperatingSystem.IsWindows() ? "Segoe UI Variable" : "Inter";

    /// <summary>
    /// The settings file. Tests point this at their own directory so they never
    /// touch the user's preferences.
    /// </summary>
    internal static string SettingsPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RedmondCalculator",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings(FontFamily: DefaultFontFamily);
            }

            var persisted = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(SettingsPath));
            var cornerStyle = persisted?.WindowCornerStyle
                ?? (persisted?.UseNativeWindowGeometry ?? persisted?.UsePlatformCornerRadius ?? false
                    ? WindowCornerStyle.MacOS
                    : persisted?.UseSquareWindowCorners ?? false
                        ? WindowCornerStyle.Windows10
                        : WindowCornerStyle.Windows11);
            var controlStyle = persisted?.WindowControlStyle
                ?? (persisted?.UseNativeTitleBar ?? persisted?.UseNativeWindowFrame ?? false
                    ? WindowControlStyle.MacOS
                    : WindowControlStyle.Windows11);
            var currencyPreferences = persisted?.CurrencyProviderPreferences
                ?? persisted?.CurrencyProviders?.ToDictionary(
                    pair => pair.Key,
                    pair => new CurrencyProviderPreference(
                        IsConsented: false,
                        IsVisibleInSelector: pair.Value),
                    StringComparer.Ordinal)
                ?? new Dictionary<string, CurrencyProviderPreference>(StringComparer.Ordinal);
            return persisted is null
                ? new AppSettings(FontFamily: DefaultFontFamily)
                : new AppSettings(
                    persisted.ThemePreference ?? AppThemePreference.Dark,
                    persisted.FontFamily ?? DefaultFontFamily,
                    persisted.UseMicaEffect ?? true,
                    cornerStyle,
                    controlStyle,
                    currencyPreferences,
                    persisted.ShortcutModifier is { } shortcutModifier
                        && Enum.IsDefined(shortcutModifier)
                        ? shortcutModifier
                        : MacPrimaryModifier.Command);
        }
        catch (IOException)
        {
            return new AppSettings(FontFamily: DefaultFontFamily);
        }
        catch (JsonException)
        {
            return new AppSettings(FontFamily: DefaultFontFamily);
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings(FontFamily: DefaultFontFamily);
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings));
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (IOException)
        {
            // Theme switching remains functional when persistence is unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Theme switching remains functional when persistence is unavailable.
        }
    }

    private sealed record PersistedSettings(
        AppThemePreference? ThemePreference,
        string? FontFamily,
        bool? UseMicaEffect,
        WindowCornerStyle? WindowCornerStyle,
        WindowControlStyle? WindowControlStyle,
        bool? UseNativeWindowGeometry,
        bool? UseNativeTitleBar,
        bool? UseSquareWindowCorners,
        bool? UsePlatformCornerRadius,
        bool? UseNativeWindowFrame,
        Dictionary<string, CurrencyProviderPreference>? CurrencyProviderPreferences,
        Dictionary<string, bool>? CurrencyProviders,
        MacPrimaryModifier? ShortcutModifier);

}

internal sealed record AppSettings(
    AppThemePreference ThemePreference = AppThemePreference.Dark,
    string FontFamily = "Inter",
    bool UseMicaEffect = true,
    WindowCornerStyle WindowCornerStyle = WindowCornerStyle.Windows11,
    WindowControlStyle WindowControlStyle = WindowControlStyle.Windows11,
    IReadOnlyDictionary<string, CurrencyProviderPreference>? CurrencyProviderPreferences = null,
    MacPrimaryModifier ShortcutModifier = MacPrimaryModifier.Command)
{
    public PlatformAppearancePreferences ToPlatformAppearance() => new(
        UseMicaEffect,
        WindowCornerStyle,
        WindowControlStyle);
}
