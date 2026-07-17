using System;
using System.IO;
using System.Text.Json;
using Calculator.Managed;

namespace Calculator.Avalonia;

internal static class AppSettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RedmondCalculator");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
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
            return persisted is null
                ? new AppSettings()
                : new AppSettings(
                    persisted.ThemePreference ?? AppThemePreference.Dark,
                    persisted.UseMicaEffect ?? true,
                    cornerStyle,
                    controlStyle);
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
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
        bool? UseMicaEffect,
        WindowCornerStyle? WindowCornerStyle,
        WindowControlStyle? WindowControlStyle,
        bool? UseNativeWindowGeometry,
        bool? UseNativeTitleBar,
        bool? UseSquareWindowCorners,
        bool? UsePlatformCornerRadius,
        bool? UseNativeWindowFrame);

}

internal sealed record AppSettings(
    AppThemePreference ThemePreference = AppThemePreference.Dark,
    bool UseMicaEffect = true,
    WindowCornerStyle WindowCornerStyle = WindowCornerStyle.Windows11,
    WindowControlStyle WindowControlStyle = WindowControlStyle.Windows11)
{
    public PlatformAppearancePreferences ToPlatformAppearance() => new(
        UseMicaEffect,
        WindowCornerStyle,
        WindowControlStyle);
}
