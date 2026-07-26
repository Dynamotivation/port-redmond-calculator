using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Calculator.Managed;

namespace Calculator.Avalonia.Services.Platform;

/// <summary>
/// The desktop implementation: Avalonia window properties plus, on macOS, the
/// AppKit backdrop, native title bar and hosted window controls.
/// </summary>
/// <remarks>
/// Every native call in the frontend lives here. The macOS helpers all return
/// early when there is no platform handle, so this type is also safe to
/// construct in a headless host — it simply does nothing visible.
/// </remarks>
internal sealed class WindowPresentationService : IWindowPresentationService
{
    private readonly Window _window;
    private readonly CalculatorViewModel _viewModel;
    private readonly Func<PlatformAppearancePreferences> _currentPreferences;

    /// <summary>
    /// When false, window styling is still applied but the AppKit backdrop,
    /// native title bar and hosted controls are skipped. A headless host has no
    /// real NSWindow behind its platform handle, so those calls would be sent
    /// to something that is not a window and the rendered frame stops being
    /// reproducible. Everything the view layer can observe is unaffected.
    /// </summary>
    private readonly bool _enableNativeEffects;

    private MacOSMicaBackdrop? _micaBackdrop;
    private MacOSWindowControls? _macOSWindowControls;
    private int _presentationVersion;
    private bool _isOpened;

    private bool _hasNormalPlacement;
    private PixelPoint _normalPosition;
    private Size _normalSize;
    private WindowState _normalState;
    private Size _compactSize = new(320, 394);

    public WindowPresentationService(
        Window window,
        CalculatorViewModel viewModel,
        Func<PlatformAppearancePreferences> currentPreferences,
        bool enableNativeEffects = true)
    {
        _window = window;
        _viewModel = viewModel;
        _currentPreferences = currentPreferences;
        _enableNativeEffects = enableNativeEffects;
    }

    public void OnWindowOpened(PlatformAppearancePreferences preferences)
    {
        _isOpened = true;
        RefreshBackdrop(preferences);
        RefreshWindowPresentation(preferences);
    }

    public void ApplyAppearance(PlatformAppearancePreferences preferences)
    {
        var wasUsingNativeTitleBar = UsesFullNativeTitleBar(_currentPreferences());
        var requiresStagedHostedControls = _isOpened
            && wasUsingNativeTitleBar
            && preferences.WindowCornerStyle != WindowCornerStyle.MacOS
            && preferences.WindowControlStyle == WindowControlStyle.MacOS;
        var presentationVersion = ++_presentationVersion;

        _macOSWindowControls?.Dispose();
        _macOSWindowControls = null;

        if (requiresStagedHostedControls)
        {
            // Going from the full native title bar to hosted controls has to
            // tear the title bar down a frame before the controls are added,
            // or AppKit leaves both on screen.
            var teardownPreferences = preferences with
            {
                WindowCornerStyle = WindowCornerStyle.MacOS,
                WindowControlStyle = WindowControlStyle.Windows11,
            };
            ApplyWindowDecorations(teardownPreferences);
            RefreshBackdrop(teardownPreferences);

            Dispatcher.UIThread.Post(() =>
            {
                if (!_isOpened
                    || presentationVersion != _presentationVersion
                    || _currentPreferences() != preferences)
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

    public void ApplyWindowDecorations(PlatformAppearancePreferences preferences)
    {
        var usesNativeTitleBar = UsesFullNativeTitleBar(preferences);
        var usesNativeGeometry = preferences.WindowCornerStyle == WindowCornerStyle.MacOS;
        _window.ExtendClientAreaToDecorationsHint = usesNativeGeometry;
        _window.ExtendClientAreaTitleBarHeightHint = 42;
        _window.WindowDecorations = usesNativeTitleBar
            ? WindowDecorations.Full
            : usesNativeGeometry
                ? WindowDecorations.BorderOnly
                : WindowDecorations.None;
    }

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

    public void Dispose()
    {
        _isOpened = false;
        _micaBackdrop?.Dispose();
        _macOSWindowControls?.Dispose();
    }

    private void RefreshBackdrop(PlatformAppearancePreferences preferences)
    {
        _micaBackdrop?.Dispose();
        _micaBackdrop = null;

        if (!_enableNativeEffects)
        {
            return;
        }

        if (_isOpened && preferences.UseMicaEffect)
        {
            var cornerRadius = preferences.WindowCornerStyle == WindowCornerStyle.Windows11 ? 8 : 0;
            _micaBackdrop = MacOSMicaBackdrop.Attach(_window, cornerRadius);
        }

        MacOSMicaBackdrop.InvalidateWindowShadow(_window);
    }

    private void RefreshWindowPresentation(
        PlatformAppearancePreferences preferences,
        bool deferFullNativeTitleBar = false)
    {
        if (!_isOpened || !_enableNativeEffects)
        {
            return;
        }

        if (UsesFullNativeTitleBar(preferences))
        {
            if (deferFullNativeTitleBar)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_isOpened && UsesFullNativeTitleBar(_currentPreferences()))
                    {
                        MacOSNativeTitleBar.Apply(_window, enabled: true);
                        MacOSMicaBackdrop.InvalidateWindowShadow(_window);
                    }
                }, DispatcherPriority.Background);
            }
            else
            {
                MacOSNativeTitleBar.Apply(_window, enabled: true);
            }
        }
        else if (UsesStandaloneMacOSControls(preferences))
        {
            _macOSWindowControls = MacOSWindowControls.Attach(_window);
        }
    }

    private static bool UsesFullNativeTitleBar(PlatformAppearancePreferences preferences) =>
        preferences.WindowCornerStyle == WindowCornerStyle.MacOS
        && preferences.WindowControlStyle == WindowControlStyle.MacOS;

    private static bool UsesStandaloneMacOSControls(PlatformAppearancePreferences preferences) =>
        preferences.WindowCornerStyle != WindowCornerStyle.MacOS
        && preferences.WindowControlStyle == WindowControlStyle.MacOS;
}
