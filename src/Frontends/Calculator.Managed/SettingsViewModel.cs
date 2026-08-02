using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

/// <summary>
/// Appearance preferences: theme, font, backdrop, window corners and window
/// controls.
/// </summary>
/// <remarks>
/// This holds preference state and nothing else. Applying a preference is
/// somebody else's job — the host listens for the change events and routes them
/// to the window presentation service, so no native window API is reachable
/// from the settings page.
///
/// The predicates the window chrome binds to combine these settings with
/// capabilities injected into the shell view model by the platform frontend.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel(
        AppThemePreference initialTheme,
        PlatformAppearancePreferences initialAppearance,
        bool supportsBackdropSettings,
        bool supportsWindowStyleSettings,
        IEnumerable<string> availableFontFamilies,
        string? initialFontFamily,
        SettingsStrings strings)
    {
        Strings = strings;
        SupportsBackdropSettings = supportsBackdropSettings;
        SupportsWindowStyleSettings = supportsWindowStyleSettings;
        SelectedThemePreference = initialTheme;

        // Inter is the shipped default and sorts first; the rest follow in
        // culture order. Keep this exactly as it was — the settings font list
        // is user-visible.
        var fontFamilies = availableFontFamilies
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Append("Inter")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name.Equals("Inter", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name, StringComparer.CurrentCultureIgnoreCase);
        foreach (var family in fontFamilies)
        {
            AvailableFontFamilies.Add(family);
        }

        SelectedFontFamily = AvailableFontFamilies.FirstOrDefault(
            name => name.Equals(initialFontFamily, StringComparison.OrdinalIgnoreCase)) ?? "Inter";

        UseMicaEffect = initialAppearance.UseMicaEffect;
        SelectedWindowCornerStyle = initialAppearance.WindowCornerStyle;
        SelectedWindowControlStyle = initialAppearance.WindowControlStyle;
    }

    public SettingsStrings Strings { get; }

    public bool SupportsBackdropSettings { get; }

    public bool SupportsWindowStyleSettings { get; }

    public event Action<AppThemePreference>? ThemePreferenceChanged;

    public event Action<string>? FontPreferenceChanged;

    public event Action<PlatformAppearancePreferences>? PlatformAppearanceChanged;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightThemeSelected))]
    [NotifyPropertyChangedFor(nameof(IsDarkThemeSelected))]
    [NotifyPropertyChangedFor(nameof(IsSystemThemeSelected))]
    public partial AppThemePreference SelectedThemePreference { get; private set; } = AppThemePreference.Dark;

    public bool IsLightThemeSelected => SelectedThemePreference == AppThemePreference.Light;
    public bool IsDarkThemeSelected => SelectedThemePreference == AppThemePreference.Dark;
    public bool IsSystemThemeSelected => SelectedThemePreference == AppThemePreference.System;

    public ObservableCollection<string> AvailableFontFamilies { get; } = [];

    [ObservableProperty]
    public partial string SelectedFontFamily { get; set; } = "Inter";

    [ObservableProperty]
    public partial bool UseMicaEffect { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWindows10CornerStyleSelected))]
    [NotifyPropertyChangedFor(nameof(IsWindows11CornerStyleSelected))]
    [NotifyPropertyChangedFor(nameof(IsMacOSCornerStyleSelected))]
    public partial WindowCornerStyle SelectedWindowCornerStyle { get; private set; } = WindowCornerStyle.Windows11;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWindows10WindowControlStyleSelected))]
    [NotifyPropertyChangedFor(nameof(IsWindows11WindowControlStyleSelected))]
    [NotifyPropertyChangedFor(nameof(IsMacOSWindowControlStyleSelected))]
    public partial WindowControlStyle SelectedWindowControlStyle { get; private set; } = WindowControlStyle.Windows11;

    public bool IsWindows10CornerStyleSelected => SelectedWindowCornerStyle == WindowCornerStyle.Windows10;
    public bool IsWindows11CornerStyleSelected => SelectedWindowCornerStyle == WindowCornerStyle.Windows11;
    public bool IsMacOSCornerStyleSelected => SelectedWindowCornerStyle == WindowCornerStyle.MacOS;
    public bool IsWindows10WindowControlStyleSelected => SelectedWindowControlStyle == WindowControlStyle.Windows10;
    public bool IsWindows11WindowControlStyleSelected => SelectedWindowControlStyle == WindowControlStyle.Windows11;
    public bool IsMacOSWindowControlStyleSelected => SelectedWindowControlStyle == WindowControlStyle.MacOS;

    [RelayCommand]
    private void SelectTheme(string preference) =>
        SelectedThemePreference = Enum.Parse<AppThemePreference>(preference, ignoreCase: false);

    [RelayCommand]
    private void SelectWindowCornerStyle(string style) =>
        SelectedWindowCornerStyle = Enum.Parse<WindowCornerStyle>(style, ignoreCase: false);

    [RelayCommand]
    private void SelectWindowControlStyle(string style) =>
        SelectedWindowControlStyle = Enum.Parse<WindowControlStyle>(style, ignoreCase: false);

    public PlatformAppearancePreferences ToPlatformAppearance() =>
        new(UseMicaEffect, SelectedWindowCornerStyle, SelectedWindowControlStyle);

    partial void OnSelectedThemePreferenceChanged(AppThemePreference value) =>
        ThemePreferenceChanged?.Invoke(value);

    partial void OnSelectedFontFamilyChanged(string value)
    {
        if (AvailableFontFamilies.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            FontPreferenceChanged?.Invoke(value);
        }
    }

    partial void OnUseMicaEffectChanged(bool value) => NotifyPlatformAppearanceChanged();

    partial void OnSelectedWindowCornerStyleChanged(WindowCornerStyle value) => NotifyPlatformAppearanceChanged();

    partial void OnSelectedWindowControlStyleChanged(WindowControlStyle value) => NotifyPlatformAppearanceChanged();

    private void NotifyPlatformAppearanceChanged() =>
        PlatformAppearanceChanged?.Invoke(ToPlatformAppearance());
}

/// <summary>Localized strings for the settings page.</summary>
public sealed record SettingsStrings(
    string AppearanceName,
    string AppThemeName,
    string AppThemeDescription,
    string LightThemeName,
    string DarkThemeName,
    string SystemThemeName,
    string AboutGroupName,
    string AboutLicenseName,
    string AboutServicesName,
    string AboutPrivacyName,
    string FeedbackName)
{
    public string AppFontName { get; } = "App font";
    public string AppFontDescription { get; } = "Choose the font used for text and numbers";
    public string MicaEffectName { get; } = "Translucent background";
    public string MicaEffectDescription { get; } = "Blur the desktop behind the calculator window";
    public string AboutVersionText { get; } = "Redmond Calculator 0.1.0";
    public string ProjectLicenseName { get; } = "MIT License";
    public string ThirdPartyNoticesName { get; } = "Third-party notices";
}
