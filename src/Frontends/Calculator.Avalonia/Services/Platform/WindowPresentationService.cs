using System;
using Avalonia;
using Avalonia.Controls;
using Calculator.Managed;
using Redmond.Avalonia.Controls;
using Redmond.Avalonia.Windowing;
using SharedCornerStyle = Redmond.Avalonia.Controls.WindowCornerStyle;
using SharedControlStyle = Redmond.Avalonia.Controls.WindowControlStyle;

namespace Calculator.Avalonia.Services.Platform;

/// <summary>
/// Keeps Calculator-specific compact-overlay behavior local while delegating
/// reusable window materials and decorations to Redmond.Avalonia.Windowing.
/// </summary>
/// <remarks>
/// Native platform calls are owned by the shared windowing project. Disabling
/// native effects keeps headless rendering deterministic.
/// </remarks>
internal sealed class WindowPresentationService : IWindowPresentationService
{
    private readonly Window _window;
    private readonly CalculatorViewModel _viewModel;
    private readonly WindowAppearanceController _appearanceController;

    private bool _hasNormalPlacement;
    private PixelPoint _normalPosition;
    private Size _normalSize;
    private WindowState _normalState;
    private Size _compactSize = new(320, 394);

    public WindowPresentationService(
        Window window,
        Border surface,
        CalculatorViewModel viewModel,
        PlatformAppearancePreferences initialPreferences,
        bool enableNativeEffects = true)
    {
        _window = window;
        _viewModel = viewModel;
        _appearanceController = WindowAppearanceController.Attach(
            window,
            surface,
            ToSharedOptions(initialPreferences),
            enableNativeEffects: enableNativeEffects);
    }

    public void ApplyAppearance(PlatformAppearancePreferences preferences) =>
        _appearanceController.Apply(ToSharedOptions(preferences));

    public void EnterCompactOverlay()
    {
        if (!_viewModel.CanEnterAlwaysOnTop)
        {
            return;
        }

        _normalState = _window.WindowState;
        _normalPosition = _window.Position;
        _normalSize = _window.Bounds.Size;
        _hasNormalPlacement = true;

        _viewModel.History.CloseCommand.Execute(null);
        _viewModel.CloseNavigationPaneCommand.Execute(null);
        _window.WindowState = WindowState.Normal;
        _window.MinWidth = 240;
        _window.MinHeight = 260;
        _window.Width = Math.Max(_window.MinWidth, _compactSize.Width);
        _window.Height = Math.Max(_window.MinHeight, _compactSize.Height);
        _viewModel.IsAlwaysOnTop = true;
        _window.Topmost = true;
    }

    public void ExitCompactOverlay()
    {
        if (!_viewModel.IsAlwaysOnTop)
        {
            return;
        }

        _compactSize = _window.Bounds.Size;
        _window.Topmost = false;
        _viewModel.IsAlwaysOnTop = false;
        _window.MinWidth = 320;
        _window.MinHeight = 500;

        if (_hasNormalPlacement)
        {
            _window.Width = Math.Max(_window.MinWidth, _normalSize.Width);
            _window.Height = Math.Max(_window.MinHeight, _normalSize.Height);
            _window.Position = _normalPosition;
            _window.WindowState = _normalState;
        }
    }

    public void Dispose() => _appearanceController.Dispose();

    private static WindowAppearanceOptions ToSharedOptions(
        PlatformAppearancePreferences preferences) =>
        new(
            preferences.UseMicaEffect
                ? TranslucentBackgroundMode.WhenSelected
                : TranslucentBackgroundMode.Never,
            preferences.WindowCornerStyle switch
            {
                Calculator.Managed.WindowCornerStyle.Windows10 => SharedCornerStyle.Windows10,
                Calculator.Managed.WindowCornerStyle.MacOS => SharedCornerStyle.MacOS,
                _ => SharedCornerStyle.Windows11,
            },
            preferences.WindowControlStyle switch
            {
                Calculator.Managed.WindowControlStyle.Windows10 => SharedControlStyle.Windows10,
                Calculator.Managed.WindowControlStyle.MacOS => SharedControlStyle.MacOS,
                _ => SharedControlStyle.Windows11,
            });
}
