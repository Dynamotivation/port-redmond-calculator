using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Calculator.Managed;
using Windows.ApplicationModel.Resources;

namespace Calculator.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ResourceLoader.Configure(new ResourceLoaderConfiguration(Path.Combine(AppContext.BaseDirectory, "Resources")));

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = AppSettingsStore.Load();
            ApplyThemePreference(settings.ThemePreference);
            desktop.MainWindow = new MainWindow(settings);
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal static void ApplyThemePreference(AppThemePreference preference)
    {
        if (Current is null)
        {
            return;
        }

        Current.RequestedThemeVariant = preference switch
        {
            AppThemePreference.Light => ThemeVariant.Light,
            AppThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
