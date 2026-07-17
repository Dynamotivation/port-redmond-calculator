using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Calculator.Managed;

namespace Calculator.Avalonia;

public partial class MainWindow : Window
{
    private MacOSMicaBackdrop? _micaBackdrop;
    private MacOSWindowControls? _macOSWindowControls;
    private readonly CalculatorViewModel _viewModel;
    private AppSettings _settings;
    private bool _isOpened;
    private int _presentationVersion;

    public MainWindow()
        : this(new AppSettings())
    {
    }

    internal MainWindow(AppSettings settings)
    {
        InitializeComponent();
        var appearance = settings.ToPlatformAppearance();
        _settings = settings;
        _viewModel = new CalculatorViewModel(
            settings.ThemePreference,
            appearance,
            OperatingSystem.IsMacOS());
        _viewModel.ThemePreferenceChanged += OnThemePreferenceChanged;
        _viewModel.PlatformAppearancePreferencesChanged += OnPlatformAppearancePreferencesChanged;
        DataContext = _viewModel;
        ApplyWindowDecorations(appearance);
        Opened += (_, _) =>
        {
            _isOpened = true;
            RefreshBackdrop(appearance);
            RefreshWindowPresentation(appearance);
        };
        Closed += (_, _) =>
        {
            _isOpened = false;
            _micaBackdrop?.Dispose();
            _macOSWindowControls?.Dispose();
            _viewModel.ThemePreferenceChanged -= OnThemePreferenceChanged;
            _viewModel.PlatformAppearancePreferencesChanged -= OnPlatformAppearancePreferencesChanged;
            _viewModel.Dispose();
        };
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void BeginResize(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(edge, e);
        }
    }

    private void ResizeNorthWest_OnPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.NorthWest, e);
    private void ResizeNorth_OnPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.North, e);
    private void ResizeNorthEast_OnPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.NorthEast, e);
    private void ResizeWest_OnPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.West, e);
    private void ResizeEast_OnPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.East, e);
    private void ResizeSouthWest_OnPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.SouthWest, e);
    private void ResizeSouth_OnPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.South, e);
    private void ResizeSouthEast_OnPointerPressed(object? sender, PointerPressedEventArgs e) => BeginResize(WindowEdge.SouthEast, e);

    private void Minimize_OnClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void AlwaysOnTop_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.IsAlwaysOnTop = !_viewModel.IsAlwaysOnTop;
        Topmost = _viewModel.IsAlwaysOnTop;
    }

    private void Maximize_OnClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void OnThemePreferenceChanged(AppThemePreference preference)
    {
        App.ApplyThemePreference(preference);
        _settings = _settings with { ThemePreference = preference };
        AppSettingsStore.Save(_settings);
    }

    private void OnPlatformAppearancePreferencesChanged(PlatformAppearancePreferences preferences)
    {
        var previousPreferences = _settings.ToPlatformAppearance();
        var wasUsingNativeTitleBar = UsesFullNativeTitleBar(previousPreferences);
        var requiresStagedHostedControls = _isOpened
            && wasUsingNativeTitleBar
            && preferences.WindowCornerStyle != WindowCornerStyle.MacOS
            && preferences.WindowControlStyle == WindowControlStyle.MacOS;
        var presentationVersion = ++_presentationVersion;

        _macOSWindowControls?.Dispose();
        _macOSWindowControls = null;

        _settings = _settings with
        {
            UseMicaEffect = preferences.UseMicaEffect,
            WindowCornerStyle = preferences.WindowCornerStyle,
            WindowControlStyle = preferences.WindowControlStyle,
        };
        AppSettingsStore.Save(_settings);

        if (requiresStagedHostedControls)
        {
            var teardownPreferences = preferences with
            {
                WindowCornerStyle = WindowCornerStyle.MacOS,
                WindowControlStyle = WindowControlStyle.Windows,
            };
            ApplyWindowDecorations(teardownPreferences);
            RefreshBackdrop(teardownPreferences);

            Dispatcher.UIThread.Post(() =>
            {
                if (!_isOpened
                    || presentationVersion != _presentationVersion
                    || _settings.ToPlatformAppearance() != preferences)
                {
                    return;
                }

                ApplyWindowDecorations(preferences);
                RefreshBackdrop(preferences);
                RefreshWindowPresentation(preferences);
            }, DispatcherPriority.Background);
            return;
        }

        ApplyWindowDecorations(preferences);
        RefreshBackdrop(preferences);
        RefreshWindowPresentation(preferences, deferFullNativeTitleBar: true);
    }

    private void ApplyWindowDecorations(PlatformAppearancePreferences preferences)
    {
        var usesNativeTitleBar = UsesFullNativeTitleBar(preferences);
        var usesNativeGeometry = preferences.WindowCornerStyle == WindowCornerStyle.MacOS;
        ExtendClientAreaToDecorationsHint = usesNativeGeometry;
        ExtendClientAreaTitleBarHeightHint = 42;
        WindowDecorations = usesNativeTitleBar
            ? global::Avalonia.Controls.WindowDecorations.Full
            : usesNativeGeometry
                ? global::Avalonia.Controls.WindowDecorations.BorderOnly
                : global::Avalonia.Controls.WindowDecorations.None;
    }

    private void RefreshBackdrop(PlatformAppearancePreferences preferences)
    {
        _micaBackdrop?.Dispose();
        _micaBackdrop = null;

        if (_isOpened && preferences.UseMicaEffect)
        {
            var cornerRadius = preferences.WindowCornerStyle == WindowCornerStyle.Windows11 ? 8 : 0;
            _micaBackdrop = MacOSMicaBackdrop.Attach(this, cornerRadius);
        }

        MacOSMicaBackdrop.InvalidateWindowShadow(this);
    }

    private void RefreshWindowPresentation(
        PlatformAppearancePreferences preferences,
        bool deferFullNativeTitleBar = false)
    {
        if (!_isOpened)
        {
            return;
        }

        if (UsesFullNativeTitleBar(preferences))
        {
            if (deferFullNativeTitleBar)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_isOpened && UsesFullNativeTitleBar(_settings.ToPlatformAppearance()))
                    {
                        MacOSNativeTitleBar.Apply(this, enabled: true);
                        MacOSMicaBackdrop.InvalidateWindowShadow(this);
                    }
                }, DispatcherPriority.Background);
            }
            else
            {
                MacOSNativeTitleBar.Apply(this, enabled: true);
            }
        }
        else if (UsesStandaloneMacOSControls(preferences))
        {
            _macOSWindowControls = MacOSWindowControls.Attach(this);
        }
    }

    private static bool UsesFullNativeTitleBar(PlatformAppearancePreferences preferences) =>
        preferences.WindowCornerStyle == WindowCornerStyle.MacOS
        && preferences.WindowControlStyle == WindowControlStyle.MacOS;

    private static bool UsesStandaloneMacOSControls(PlatformAppearancePreferences preferences) =>
        preferences.WindowCornerStyle != WindowCornerStyle.MacOS
        && preferences.WindowControlStyle == WindowControlStyle.MacOS;

    private async void License_OnClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://github.com/microsoft/calculator/blob/main/LICENSE"));

    private async void ServicesAgreement_OnClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://go.microsoft.com/fwlink/?LinkID=822631"));

    private async void PrivacyStatement_OnClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://go.microsoft.com/fwlink/?LinkID=521839"));

    private async void Feedback_OnClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://github.com/Dynamotivation/RedmondCalculator/issues"));
}
