using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Calculator.Managed;
using Calculator.Shortcuts;

namespace Calculator.Avalonia;

public partial class MainWindow : Window
{
    private MacOSMicaBackdrop? _micaBackdrop;
    private MacOSWindowControls? _macOSWindowControls;
    private readonly CalculatorViewModel _viewModel;
    private readonly ShortcutService _shortcutService;
    private readonly IReadOnlyList<IDisposable> _shortcutRegistrations;
    private readonly Dictionary<Key, Button> _keyboardPressedButtons = [];
    private readonly IReadOnlySet<string> _calculatorShortcutScope = new HashSet<string>(StringComparer.Ordinal)
    {
        "calculator",
    };
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
            OperatingSystem.IsMacOS(),
            availableFontFamilies: GetInstalledFontFamilyNames(),
            initialFontFamily: settings.FontFamily);
        _viewModel.ThemePreferenceChanged += OnThemePreferenceChanged;
        _viewModel.FontPreferenceChanged += OnFontPreferenceChanged;
        _viewModel.PlatformAppearancePreferencesChanged += OnPlatformAppearancePreferencesChanged;
        DataContext = _viewModel;
        var shortcutPlatform = OperatingSystem.IsWindows()
            ? ShortcutPlatform.Windows
            : OperatingSystem.IsMacOS()
                ? ShortcutPlatform.MacOS
                : OperatingSystem.IsLinux()
                    ? ShortcutPlatform.Linux
                    : ShortcutPlatform.Unknown;
        _shortcutService = new ShortcutService(shortcutPlatform);
        _shortcutRegistrations = ShortcutCatalogLoader.LoadBuiltIn().RegisterAll(_shortcutService);
        AddHandler(KeyDownEvent, OnCalculatorKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnCalculatorKeyUp, RoutingStrategies.Tunnel);
        Deactivated += OnWindowDeactivated;
        SizeChanged += (_, _) => UpdateResponsiveCalculatorLayout(Bounds.Width);
        UpdateResponsiveCalculatorLayout(Bounds.Width);
        ApplyFontFamily(_viewModel.SelectedFontFamily);
        if (!string.Equals(_settings.FontFamily, _viewModel.SelectedFontFamily, StringComparison.Ordinal))
        {
            _settings = _settings with { FontFamily = _viewModel.SelectedFontFamily };
            AppSettingsStore.Save(_settings);
        }
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
            _viewModel.FontPreferenceChanged -= OnFontPreferenceChanged;
            _viewModel.PlatformAppearancePreferencesChanged -= OnPlatformAppearancePreferencesChanged;
            RemoveHandler(KeyDownEvent, OnCalculatorKeyDown);
            RemoveHandler(KeyUpEvent, OnCalculatorKeyUp);
            Deactivated -= OnWindowDeactivated;
            foreach (var registration in _shortcutRegistrations)
            {
                registration.Dispose();
            }
            _viewModel.Dispose();
        };
    }

    private void UpdateResponsiveCalculatorLayout(double width)
    {
        const double historyDockThreshold = 560;
        const double fixedHistoryThreshold = 1024;
        var isDocked = width >= historyDockThreshold;
        _viewModel.SetHistoryDocked(isDocked);

        if (!isDocked)
        {
            CalculatorResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("*,0");
        }
        else if (width >= fixedHistoryThreshold)
        {
            CalculatorResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("*,320");
        }
        else
        {
            // These are the source UWP Calculator proportions: 320*:240*.
            CalculatorResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("320*,240*");
        }
    }

    private void OnCalculatorKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_viewModel.IsStandardMode || _viewModel.IsSettingsOpen || _viewModel.IsNavigationPaneOpen)
        {
            return;
        }

        if (!TryCreateShortcutInput(e, out var input))
        {
            return;
        }

        var result = _shortcutService.Process(input, _calculatorShortcutScope);
        if (!result.WasMatched
            && OperatingSystem.IsMacOS()
            && input.Gesture.Modifiers.HasFlag(ShortcutModifiers.Command)
            && input.Gesture.Key.Value is "C" or "V")
        {
            // The source catalog intentionally preserves Microsoft's Ctrl+C/V
            // declarations. At the platform boundary only, treat the native
            // macOS Command modifier as Control for those two editing commands.
            var normalizedModifiers = (input.Gesture.Modifiers & ~ShortcutModifiers.Command)
                | ShortcutModifiers.Control;
            input = new ShortcutInput(new ShortcutGesture(input.Gesture.Key, normalizedModifiers), input.IsRepeat);
            result = _shortcutService.Process(input, _calculatorShortcutScope);
        }
        if (!result.WasMatched)
        {
            return;
        }

        var match = result[0];
        if (DispatchCalculatorShortcut(match.ShortcutId) && TryGetShortcutButton(match.ShortcutId, out var button))
        {
            if (_keyboardPressedButtons.TryGetValue(e.Key, out var previousButton))
            {
                previousButton.Classes.Remove("keyboardPressed");
            }

            button.Classes.Add("keyboardPressed");
            _keyboardPressedButtons[e.Key] = button;
        }

        e.Handled = result.Handled;
    }

    private void OnCalculatorKeyUp(object? sender, KeyEventArgs e)
    {
        if (_keyboardPressedButtons.Remove(e.Key, out var button))
        {
            button.Classes.Remove("keyboardPressed");
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        foreach (var button in _keyboardPressedButtons.Values)
        {
            button.Classes.Remove("keyboardPressed");
        }
        _keyboardPressedButtons.Clear();
    }

    private static bool TryCreateShortcutInput(KeyEventArgs e, out ShortcutInput input)
    {
        var modifiers = ShortcutModifiers.None;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= ShortcutModifiers.Control;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= ShortcutModifiers.Alt;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= ShortcutModifiers.Shift;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= ShortcutModifiers.Command;

        ShortcutKey shortcutKey;
        var symbol = e.KeySymbol;
        if ((symbol is "." or ",") && modifiers is ShortcutModifiers.None or ShortcutModifiers.Shift)
        {
            shortcutKey = ShortcutKey.Named("DECIMAL");
            modifiers = ShortcutModifiers.None;
        }
        else if (symbol?.Length == 1 && !char.IsControl(symbol[0]))
        {
            var hasCommandModifier = (modifiers & (ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Command)) != 0;
            shortcutKey = hasCommandModifier
                ? ShortcutKey.Named(symbol)
                : ShortcutKey.Character(symbol[0]);
            if (!hasCommandModifier)
            {
                // KeySymbol already contains the layout-resolved shifted glyph
                // (for example '+' or '%'); UWP shortcut resources describe
                // those glyphs without a separate Shift modifier.
                modifiers &= ~ShortcutModifiers.Shift;
            }
        }
        else if (TryMapFallbackKey(e.Key, out var fallbackKey))
        {
            shortcutKey = fallbackKey;
            if (fallbackKey.Kind == ShortcutKeyKind.Character)
            {
                modifiers &= ~ShortcutModifiers.Shift;
            }
        }
        else
        {
            input = default;
            return false;
        }

        input = new ShortcutInput(new ShortcutGesture(shortcutKey, modifiers));
        return true;
    }

    private static bool TryMapFallbackKey(Key key, out ShortcutKey shortcutKey)
    {
        var namedKey = key switch
        {
            Key.Enter or Key.Return => "ENTER",
            Key.Escape => "ESCAPE",
            Key.Delete => "DELETE",
            Key.Back => "BACK",
            Key.Decimal or Key.OemPeriod or Key.OemComma => "DECIMAL",
            Key.C => "C",
            Key.D => "D",
            Key.H => "H",
            Key.L => "L",
            Key.M => "M",
            Key.P => "P",
            Key.Q => "Q",
            Key.R => "R",
            Key.V => "V",
            _ => string.Empty,
        };
        if (namedKey.Length != 0)
        {
            shortcutKey = ShortcutKey.Named(namedKey);
            return true;
        }

        char? character = key switch
        {
            Key.D0 or Key.NumPad0 => '0',
            Key.D1 or Key.NumPad1 => '1',
            Key.D2 or Key.NumPad2 => '2',
            Key.D3 or Key.NumPad3 => '3',
            Key.D4 or Key.NumPad4 => '4',
            Key.D5 or Key.NumPad5 => '5',
            Key.D6 or Key.NumPad6 => '6',
            Key.D7 or Key.NumPad7 => '7',
            Key.D8 or Key.NumPad8 => '8',
            Key.D9 or Key.NumPad9 => '9',
            Key.Add or Key.OemPlus => '+',
            Key.Subtract or Key.OemMinus => '-',
            Key.Multiply => '*',
            Key.Divide or Key.OemQuestion or Key.Oem2 => '/',
            _ => null,
        };
        shortcutKey = character is null ? default : ShortcutKey.Character(character.Value);
        return character is not null;
    }

    private bool DispatchCalculatorShortcut(string shortcutId)
    {
        CalculatorCommand? command = shortcutId switch
        {
            "clearButton" => CalculatorCommand.Clear,
            "clearEntryButton" => CalculatorCommand.ClearEntry,
            "decimalSeparatorButton" => CalculatorCommand.Decimal,
            "divideButton" => CalculatorCommand.Divide,
            "equalButton" => CalculatorCommand.Equals,
            "minusButton" => CalculatorCommand.Subtract,
            "negateButton" => CalculatorCommand.Sign,
            "num0Button" => CalculatorCommand.Zero,
            "num1Button" => CalculatorCommand.One,
            "num2Button" => CalculatorCommand.Two,
            "num3Button" => CalculatorCommand.Three,
            "num4Button" => CalculatorCommand.Four,
            "num5Button" => CalculatorCommand.Five,
            "num6Button" => CalculatorCommand.Six,
            "num7Button" => CalculatorCommand.Seven,
            "num8Button" => CalculatorCommand.Eight,
            "num9Button" => CalculatorCommand.Nine,
            "percentButton" => CalculatorCommand.Percent,
            "plusButton" => CalculatorCommand.Add,
            "squareRootButton" => CalculatorCommand.SquareRoot,
            "backSpaceButton" => CalculatorCommand.Backspace,
            "multiplyButton" => CalculatorCommand.Multiply,
            _ => null,
        };

        if (command is not null)
        {
            _viewModel.ExecuteCalculatorCommand(command.Value);
            return true;
        }

        switch (shortcutId)
        {
            case "HistoryButton": _viewModel.ToggleHistoryCommand.Execute(null); return true;
            case "ClearHistory": _viewModel.ClearHistoryCommand.Execute(null); return true;
            case "ClearMemoryButton": _viewModel.MemoryClearAllCommand.Execute(null); return true;
            case "MemRecall": _viewModel.MemoryRecallCommand.Execute(null); return true;
            case "MemPlus": _viewModel.MemoryAddCommand.Execute(null); return true;
            case "MemMinus": _viewModel.MemorySubtractCommand.Execute(null); return true;
            case "copyButton":
            case "copyButtonAlternate": _ = CopyDisplayToClipboardAsync(); return true;
            case "pasteButton":
            case "pasteButtonAlternate": _ = PasteFromClipboardAsync(); return true;
            default: return false;
        }
    }

    private async System.Threading.Tasks.Task CopyDisplayToClipboardAsync()
    {
        var clipboard = Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(_viewModel.PrimaryDisplay);
        }
    }

    private async System.Threading.Tasks.Task PasteFromClipboardAsync()
    {
        var clipboard = Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var text = await clipboard.TryGetTextAsync();
        _viewModel.TryPasteStandardExpression(text);
    }

    private bool TryGetShortcutButton(string shortcutId, out Button button)
    {
        button = shortcutId switch
        {
            "clearButton" => ClearButton,
            "clearEntryButton" => ClearEntryButton,
            "decimalSeparatorButton" => DecimalButton,
            "divideButton" => DivideButton,
            "equalButton" => EqualsButton,
            "minusButton" => SubtractButton,
            "negateButton" => SignButton,
            "num0Button" => ZeroButton,
            "num1Button" => OneButton,
            "num2Button" => TwoButton,
            "num3Button" => ThreeButton,
            "num4Button" => FourButton,
            "num5Button" => FiveButton,
            "num6Button" => SixButton,
            "num7Button" => SevenButton,
            "num8Button" => EightButton,
            "num9Button" => NineButton,
            "percentButton" => PercentButton,
            "plusButton" => AddButton,
            "squareRootButton" => SquareRootButton,
            "backSpaceButton" => BackspaceButton,
            "multiplyButton" => MultiplyButton,
            "HistoryButton" => HistoryButton,
            "ClearMemoryButton" => MemoryClearButton,
            "MemRecall" => MemoryRecallButton,
            "MemPlus" => MemoryAddButton,
            "MemMinus" => MemorySubtractButton,
            _ => null!,
        };
        return button is not null;
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

    private void OnFontPreferenceChanged(string fontFamily)
    {
        ApplyFontFamily(fontFamily);
        _settings = _settings with { FontFamily = fontFamily };
        AppSettingsStore.Save(_settings);
    }

    private void ApplyFontFamily(string fontFamily)
    {
        FontFamily = new FontFamily(fontFamily);
        // TODO: Derive optional baseline compensation from the resolved
        // typeface's runtime metrics for fonts that Avalonia positions
        // unusually. The default font must remain unadjusted.
    }

    private static IEnumerable<string> GetInstalledFontFamilyNames()
    {
        var systemFonts = FontManager.Current.SystemFonts;
        for (var index = 0; index < systemFonts.Count; index++)
        {
            yield return systemFonts[index].Name;
        }
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
                WindowControlStyle = WindowControlStyle.Windows11,
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
