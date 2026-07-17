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

    public static AppThemePreference LoadThemePreference()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return AppThemePreference.Dark;
            }

            var settings = JsonSerializer.Deserialize<PersistedSettings>(File.ReadAllText(SettingsPath));
            return settings?.ThemePreference ?? AppThemePreference.Dark;
        }
        catch (IOException)
        {
            return AppThemePreference.Dark;
        }
        catch (JsonException)
        {
            return AppThemePreference.Dark;
        }
        catch (UnauthorizedAccessException)
        {
            return AppThemePreference.Dark;
        }
    }

    public static void SaveThemePreference(AppThemePreference preference)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var temporaryPath = SettingsPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new PersistedSettings(preference)));
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

    private sealed record PersistedSettings(AppThemePreference ThemePreference);
}
