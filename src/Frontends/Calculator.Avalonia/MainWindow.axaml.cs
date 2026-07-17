using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Calculator.Managed;

namespace Calculator.Avalonia;

public partial class MainWindow : Window
{
    private MacOSMicaBackdrop? _micaBackdrop;
    private MacOSWindowControls? _macOSWindowControls;
    private readonly CalculatorViewModel _viewModel;
    private AppSettings _settings;
    private bool _isOpened;

    public MainWindow()
        : this(new AppSettings())
    {
    }

    internal MainWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _viewModel = new CalculatorViewModel(
            settings.ThemePreference,
            settings.ToPlatformAppearance(),
            OperatingSystem.IsMacOS());
        _viewModel.ThemePreferenceChanged += OnThemePreferenceChanged;
        _viewModel.PlatformAppearancePreferencesChanged += OnPlatformAppearancePreferencesChanged;
        DataContext = _viewModel;
        ApplyWindowDecorations(settings.ToPlatformAppearance());
        Opened += (_, _) =>
        {
            _isOpened = true;
            RefreshBackdrop(settings.ToPlatformAppearance());
            RefreshWindowControls(settings.ToPlatformAppearance());
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
        _settings = _settings with
        {
            UseMicaEffect = preferences.UseMicaEffect,
            WindowCornerStyle = preferences.WindowCornerStyle,
            WindowControlStyle = preferences.WindowControlStyle,
        };
        AppSettingsStore.Save(_settings);
        ApplyWindowDecorations(preferences);
        RefreshBackdrop(preferences);
        RefreshWindowControls(preferences);
    }

    private void ApplyWindowDecorations(PlatformAppearancePreferences preferences)
    {
        var usesNativeGeometry = preferences.WindowCornerStyle == WindowCornerStyle.MacOS;
        ExtendClientAreaToDecorationsHint = usesNativeGeometry;
        ExtendClientAreaTitleBarHeightHint = 42;
        WindowDecorations = usesNativeGeometry
            ? global::Avalonia.Controls.WindowDecorations.BorderOnly
            : global::Avalonia.Controls.WindowDecorations.None;
    }

    private void RefreshBackdrop(PlatformAppearancePreferences preferences)
    {
        _micaBackdrop?.Dispose();
        _micaBackdrop = null;

        if (_isOpened && preferences.UseMicaEffect)
        {
            _micaBackdrop = MacOSMicaBackdrop.Attach(this, _viewModel.WindowCornerRadius);
        }

        MacOSMicaBackdrop.InvalidateWindowShadow(this);
    }

    private void RefreshWindowControls(PlatformAppearancePreferences preferences)
    {
        _macOSWindowControls?.Dispose();
        _macOSWindowControls = null;

        if (_isOpened && preferences.WindowControlStyle == WindowControlStyle.MacOS)
        {
            _macOSWindowControls = MacOSWindowControls.Attach(this);
        }
    }

    private async void License_OnClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://github.com/microsoft/calculator/blob/main/LICENSE"));

    private async void ServicesAgreement_OnClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://go.microsoft.com/fwlink/?LinkID=822631"));

    private async void PrivacyStatement_OnClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://go.microsoft.com/fwlink/?LinkID=521839"));

    private async void Feedback_OnClick(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri("https://github.com/Dynamotivation/RedmondCalculator/issues"));
}
