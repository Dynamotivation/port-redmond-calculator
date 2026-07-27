using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Calculator.Avalonia.Controls;
using Calculator.Avalonia.Services;
using Calculator.Avalonia.Services.Platform;
using Calculator.Managed;
using Calculator.Shortcuts;
using Redmond.Shortcuts;

namespace Calculator.Avalonia;

public partial class MainWindow : Window
{
    private readonly CalculatorViewModel _viewModel;
    private readonly ShortcutService _shortcutService;
    private readonly IReadOnlyList<IDisposable> _shortcutRegistrations;
    private readonly Dictionary<Key, string> _pressedShortcutIds = [];
    private IReadOnlyList<IShortcutPressedTarget> _shortcutPressedTargets = [];
    private readonly IReadOnlySet<string> _calculatorShortcutScope = new HashSet<string>(StringComparer.Ordinal)
    {
        "calculator",
    };
    private readonly IReadOnlySet<string> _scientificShortcutScope = new HashSet<string>(StringComparer.Ordinal)
    {
        "calculator",
        "scientific",
    };
    private readonly IReadOnlySet<string> _programmerShortcutScope = new HashSet<string>(StringComparer.Ordinal)
    {
        "calculator",
        "programmer",
    };
    private readonly IReadOnlySet<string> _graphingShortcutScope = new HashSet<string>(StringComparer.Ordinal)
    {
        "graphing",
    };
    private readonly IReadOnlySet<string> _navigationShortcutScope = new HashSet<string>(StringComparer.Ordinal)
    {
        "navigation",
    };
    private readonly IReadOnlySet<string> _converterShortcutScope = new HashSet<string>(StringComparer.Ordinal)
    {
        "converter",
    };
    private readonly IWindowPresentationService _presentation;
    private AppSettings _settings;

    public MainWindow()
        : this(new AppSettings())
    {
    }

    internal MainWindow(AppSettings settings, bool enableNativeEffects = true)
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
        _viewModel.Settings.ThemePreferenceChanged += OnThemePreferenceChanged;
        _viewModel.Settings.FontPreferenceChanged += OnFontPreferenceChanged;
        _viewModel.Settings.PlatformAppearanceChanged += OnAppearancePreferencesChanged;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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
        TitleBarChrome.DragRequested += TitleBarChrome_OnDragRequested;
        TitleBarChrome.MinimizeRequested += TitleBarChrome_OnMinimizeRequested;
        TitleBarChrome.MaximizeRequested += TitleBarChrome_OnMaximizeRequested;
        TitleBarChrome.CloseRequested += TitleBarChrome_OnCloseRequested;
        TitleBarChrome.AlwaysOnTopToggleRequested += TitleBarChrome_OnAlwaysOnTopToggleRequested;
        _shortcutPressedTargets =
            [MemoryRow, StandardView, ScientificView, ScientificControls, ProgrammerView, ConverterView];
        NarrowHistory.DismissRequested += NarrowHistory_OnDismissRequested;
        Deactivated += OnWindowDeactivated;
        SizeChanged += (_, _) => UpdateResponsiveCalculatorLayout(Bounds.Width, Bounds.Height);
        UpdateResponsiveCalculatorLayout(Bounds.Width, Bounds.Height);
        UpdateCalculatorModeLayout();
        ApplyFontFamily(_viewModel.Settings.SelectedFontFamily);
        if (!string.Equals(_settings.FontFamily, _viewModel.Settings.SelectedFontFamily, StringComparison.Ordinal))
        {
            _settings = _settings with { FontFamily = _viewModel.Settings.SelectedFontFamily };
            AppSettingsStore.Save(_settings);
        }
        var presentation = new WindowPresentationService(
            this, WindowSurface, _viewModel, appearance, enableNativeEffects);
        _presentation = presentation;
        Closed += (_, _) =>
        {
            _presentation.Dispose();
            _viewModel.Settings.ThemePreferenceChanged -= OnThemePreferenceChanged;
            _viewModel.Settings.FontPreferenceChanged -= OnFontPreferenceChanged;
            _viewModel.Settings.PlatformAppearanceChanged -= OnAppearancePreferencesChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
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

    private void UpdateResponsiveCalculatorLayout(double width, double height)
    {
        Display.IsCompactOverlay = _viewModel.IsAlwaysOnTop;

        if (_viewModel.IsAlwaysOnTop)
        {
            _viewModel.History.SetDocked(false);
            CalculatorResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("*,0");
            Display.Size = height >= 260
                ? CalculatorDisplaySize.Medium
                : CalculatorDisplaySize.Small;
            return;
        }

        if (!_viewModel.IsCalculatorMode)
        {
            _viewModel.History.SetDocked(false);
            CalculatorResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("*,0");
            return;
        }

        const double historyDockThreshold = 560;
        var isDocked = width >= historyDockThreshold;
        var usesFixedHistoryWidth = (width >= 768 && height >= 1366)
            || (width >= 1024 && height >= 768);
        _viewModel.History.SetDocked(isDocked);

        if (!isDocked)
        {
            CalculatorResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("*,0");
        }
        else if (usesFixedHistoryWidth)
        {
            CalculatorResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("*,320");
        }
        else
        {
            // These are the source UWP Calculator proportions: 320*:240*.
            CalculatorResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("320*,240*");
        }

        // Calculator.xaml has three height states for the result row, and the
        // middle threshold depends on the mode. The band is decided here
        // because it is measured against the window; the metrics that follow
        // from it belong to CalculatorDisplay.
        var mediumThreshold = _viewModel.IsProgrammerMode ? 640
            : _viewModel.IsScientificMode ? 544
            : 1;

        Display.Size = height >= 800 ? CalculatorDisplaySize.Large
            : height >= mediumThreshold ? CalculatorDisplaySize.Medium
            : CalculatorDisplaySize.Small;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalculatorViewModel.CurrentViewMode))
        {
            if (_viewModel.IsAlwaysOnTop && !_viewModel.IsStandardMode)
            {
                _presentation.ExitCompactOverlay();
            }

            UpdateCalculatorModeLayout();
            UpdateResponsiveCalculatorLayout(Bounds.Width, Bounds.Height);
            if (_viewModel.IsDateCalculatorMode)
            {
                Dispatcher.UIThread.Post(DateView.FocusDefault, DispatcherPriority.Input);
            }
            else if (_viewModel.IsGraphingMode)
            {
                Dispatcher.UIThread.Post(GraphingView.FocusDefault, DispatcherPriority.Input);
            }
        }
        else if (e.PropertyName == nameof(CalculatorViewModel.IsAlwaysOnTop))
        {
            UpdateCalculatorModeLayout();
            UpdateResponsiveCalculatorLayout(Bounds.Width, Bounds.Height);
        }
        else if (e.PropertyName == nameof(CalculatorViewModel.IsNavigationPaneOpen))
        {
            Dispatcher.UIThread.Post(
                _viewModel.IsNavigationPaneOpen
                    ? ShellNavigation.FocusSelectedItem
                    : _viewModel.IsDateCalculatorMode
                        ? DateView.FocusDefault
                        : ShellNavigation.FocusToggle,
                DispatcherPriority.Input);
        }
        else if (e.PropertyName == nameof(CalculatorViewModel.IsSettingsOpen))
        {
            Dispatcher.UIThread.Post(
                _viewModel.IsSettingsOpen
                    ? SettingsPage.FocusFirstInteractiveControl
                    : _viewModel.IsDateCalculatorMode
                        ? DateView.FocusDefault
                        : ShellNavigation.FocusToggle,
                DispatcherPriority.Input);
        }
    }

    private void UpdateCalculatorModeLayout()
    {
        if (_viewModel.IsAlwaysOnTop)
        {
            CalculatorPageContent.RowDefinitions[0].Height = new GridLength(0);
            CalculatorPageContent.RowDefinitions[1].Height = new GridLength(0);
            CalculatorPageContent.RowDefinitions[2].Height = new GridLength(72, GridUnitType.Star);
            CalculatorPageContent.RowDefinitions[3].Height = new GridLength(0);
            CalculatorPageContent.RowDefinitions[3].MinHeight = 0;
            CalculatorPageContent.RowDefinitions[4].Height = new GridLength(0);
            CalculatorPageContent.RowDefinitions[5].Height = new GridLength(308, GridUnitType.Star);
            return;
        }

        var scientific = _viewModel.IsScientificMode;
        var programmer = _viewModel.IsProgrammerMode;
        CalculatorPageContent.RowDefinitions[0].Height = new GridLength(48);
        CalculatorPageContent.RowDefinitions[1].Height = new GridLength(22, GridUnitType.Star);
        CalculatorPageContent.RowDefinitions[2].Height = new GridLength(72, GridUnitType.Star);
        CalculatorPageContent.RowDefinitions[4].Height = new GridLength(32, GridUnitType.Star);
        var displayControlsRow = CalculatorPageContent.RowDefinitions[3];
        displayControlsRow.Height = new GridLength(programmer ? 96 : scientific ? 32 : 0, programmer || scientific ? GridUnitType.Star : GridUnitType.Pixel);
        displayControlsRow.MinHeight = programmer ? 96 : scientific ? 32 : 0;
        CalculatorPageContent.RowDefinitions[5].Height = new GridLength(programmer ? 268 : scientific ? 276 : 308, GridUnitType.Star);
    }



    private void OnCalculatorKeyDown(object? sender, KeyEventArgs e)
    {
        if (!TryCreateShortcutInput(e, out var input))
        {
            return;
        }

        // Navigation is a shell scope, so it remains active while settings or
        // the navigation pane has focus and before any page-specific routing.
        var result = ProcessShortcut(input, _navigationShortcutScope);
        if (result.WasMatched)
        {
            if (DispatchCalculatorShortcut(result[0].ShortcutId))
            {
                e.Handled = result.Handled;
            }
            return;
        }

        // Page shortcuts must not leak through an open shell surface.
        if (_viewModel.IsSettingsOpen || _viewModel.IsNavigationPaneOpen)
        {
            return;
        }

        if (_viewModel.IsCalculatorMode)
        {
            result = ProcessShortcut(
                input,
                _viewModel.IsScientificMode
                    ? _scientificShortcutScope
                    : _viewModel.IsProgrammerMode
                        ? _programmerShortcutScope
                        : _calculatorShortcutScope);
        }
        else if (_viewModel.IsUnitConverterMode)
        {
            // Converter-specific definitions win collisions such as F9. Digits,
            // decimal, clear and backspace intentionally remain in Microsoft's
            // shared calculator scope, so fall back to that scope afterwards.
            result = ProcessShortcut(input, _converterShortcutScope);
            if (!result.WasMatched)
            {
                result = ProcessShortcut(input, _calculatorShortcutScope);
            }
        }
        else if (_viewModel.IsGraphingMode)
        {
            result = ProcessShortcut(input, _graphingShortcutScope);
        }
        else
        {
            return;
        }

        if (!result.WasMatched)
        {
            return;
        }

        var match = result[0];
        var dispatched = _viewModel.IsUnitConverterMode
            ? _viewModel.Converter.TryDispatchShortcut(match.ShortcutId)
            : _viewModel.IsGraphingMode
                ? DispatchGraphingShortcut(match.ShortcutId)
                : DispatchCalculatorShortcut(match.ShortcutId);
        if (dispatched)
        {
            // Holding a key that maps to a different button than the one this
            // key last pressed has to release the earlier one first.
            if (_pressedShortcutIds.TryGetValue(e.Key, out var previousShortcutId))
            {
                SetShortcutPressed(previousShortcutId, isPressed: false);
            }

            if (SetShortcutPressed(match.ShortcutId, isPressed: true))
            {
                _pressedShortcutIds[e.Key] = match.ShortcutId;
            }
            else
            {
                _pressedShortcutIds.Remove(e.Key);
            }
        }

        // Observe-only or unsupported page gestures (notably Space on a
        // focused converter ComboBox) continue to Avalonia's control handling.
        e.Handled = dispatched && result.Handled;
    }

    private ShortcutProcessResult ProcessShortcut(
        ShortcutInput input,
        IReadOnlySet<string> scope)
    {
        var result = _shortcutService.Process(input, scope);
        if (result.WasMatched
            || !OperatingSystem.IsMacOS()
            || !input.Gesture.Modifiers.HasFlag(ShortcutModifiers.Command)
            || input.Gesture.Key.Value is not ("C" or "V"))
        {
            return result;
        }

        // The source catalog intentionally preserves Microsoft's Ctrl+C/V
        // declarations. At the platform boundary only, treat native Command as
        // Control for those two editing commands.
        var normalizedModifiers = (input.Gesture.Modifiers & ~ShortcutModifiers.Command)
            | ShortcutModifiers.Control;
        var normalizedInput = new ShortcutInput(
            new ShortcutGesture(input.Gesture.Key, normalizedModifiers),
            input.IsRepeat);
        return _shortcutService.Process(normalizedInput, scope);
    }

    private void OnCalculatorKeyUp(object? sender, KeyEventArgs e)
    {
        if (_pressedShortcutIds.Remove(e.Key, out var shortcutId))
        {
            SetShortcutPressed(shortcutId, isPressed: false);
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        foreach (var shortcutId in _pressedShortcutIds.Values)
        {
            SetShortcutPressed(shortcutId, isPressed: false);
        }
        _pressedShortcutIds.Clear();
    }

    /// <summary>
    /// Offers the shortcut to each control that owns keypad buttons, then falls
    /// back to the buttons still declared in this window. As modes are
    /// extracted, entries leave TryGetShortcutButton and the fallback shrinks.
    /// </summary>
    private bool SetShortcutPressed(string shortcutId, bool isPressed)
    {
        foreach (var target in _shortcutPressedTargets)
        {
            // A hidden mode still has its buttons in the tree, so only the
            // mode on screen may claim a shortcut.
            if (target is Visual { IsEffectivelyVisible: false })
            {
                continue;
            }

            if (target.TrySetShortcutPressed(shortcutId, isPressed))
            {
                return true;
            }
        }

        if (!TryGetShortcutButton(shortcutId, out var button))
        {
            return false;
        }

        button.Classes.Set("keyboardPressed", isPressed);
        return true;
    }

    private void NarrowHistory_OnDismissRequested(object? sender, EventArgs e)
    {
        if (_viewModel.IsNarrowHistoryPaneVisible)
        {
            _viewModel.History.CloseCommand.Execute(null);
        }
    }



    private bool TryCreateShortcutInput(KeyEventArgs e, out ShortcutInput input)
    {
        var modifiers = ShortcutModifiers.None;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= ShortcutModifiers.Control;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= ShortcutModifiers.Alt;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= ShortcutModifiers.Shift;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= ShortcutModifiers.Command;

        ShortcutKey shortcutKey;
        var symbol = e.KeySymbol;
        var hasCommandModifier =
            (modifiers & (ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Command)) != 0;
        if (hasCommandModifier
            && TryMapFallbackKey(e.Key, modifiers & ~ShortcutModifiers.Shift, out var modifiedFallbackKey)
            && modifiedFallbackKey.Value.Length == 1
            && char.IsLetterOrDigit(modifiedFallbackKey.Value[0]))
        {
            // macOS Option modifies KeySymbol (Option+3, for example, becomes
            // '£'). Navigation accelerators describe the physical alphanumeric
            // key, so modified chords must prefer Avalonia's physical key.
            shortcutKey = ShortcutKey.Named(modifiedFallbackKey.Value.ToUpperInvariant());
        }
        else if (!_viewModel.IsProgrammerMode
            && (symbol is "." or ",")
            && modifiers is ShortcutModifiers.None or ShortcutModifiers.Shift)
        {
            shortcutKey = ShortcutKey.Named("DECIMAL");
            modifiers = ShortcutModifiers.None;
        }
        else if (symbol?.Length == 1 && !char.IsControl(symbol[0]) && !char.IsWhiteSpace(symbol[0]))
        {
            var isLetterKey = char.IsLetter(symbol[0]);
            shortcutKey = hasCommandModifier || isLetterKey
                ? ShortcutKey.Named(symbol)
                : ShortcutKey.Character(symbol[0]);
            if (isLetterKey)
            {
                shortcutKey = ShortcutKey.Named(symbol.ToUpperInvariant());
            }
            if (!hasCommandModifier && !isLetterKey)
            {
                // KeySymbol already contains the layout-resolved shifted glyph
                // (for example '+' or '%'); UWP shortcut resources describe
                // those glyphs without a separate Shift modifier.
                modifiers &= ~ShortcutModifiers.Shift;
            }
        }
        else if (TryMapFallbackKey(e.Key, modifiers, out var fallbackKey))
        {
            shortcutKey = fallbackKey;
            if ((modifiers & (ShortcutModifiers.Control | ShortcutModifiers.Alt | ShortcutModifiers.Command)) != 0
                && fallbackKey.Kind == ShortcutKeyKind.Character
                && fallbackKey.Value.Length == 1
                && char.IsLetterOrDigit(fallbackKey.Value[0]))
            {
                shortcutKey = ShortcutKey.Named(fallbackKey.Value.ToUpperInvariant());
            }
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

        if (string.IsNullOrWhiteSpace(shortcutKey.Value))
        {
            input = default;
            return false;
        }

        input = new ShortcutInput(new ShortcutGesture(shortcutKey, modifiers));
        return true;
    }

    private static bool TryMapFallbackKey(Key key, ShortcutModifiers modifiers, out ShortcutKey shortcutKey)
    {
        var namedKey = key switch
        {
            Key.Enter or Key.Return => "ENTER",
            Key.Escape => "ESCAPE",
            Key.Delete => "DELETE",
            Key.Back => "BACK",
            Key.Space => "SPACE",
            Key.Up => "UP",
            Key.Down => "DOWN",
            Key.Left => "LEFT",
            Key.Right => "RIGHT",
            Key.Decimal or Key.OemPeriod or Key.OemComma => "DECIMAL",
            Key.F3 => "F3",
            Key.F4 => "F4",
            Key.F5 => "F5",
            Key.F9 => "F9",
            >= Key.A and <= Key.Z => key.ToString().ToUpperInvariant(),
            _ => string.Empty,
        };
        if (namedKey.Length != 0)
        {
            shortcutKey = ShortcutKey.Named(namedKey);
            return true;
        }

        var isShifted = modifiers.HasFlag(ShortcutModifiers.Shift);
        char? character = (key, isShifted) switch
        {
            (Key.D1, true) => '!',
            (Key.D3, true) => '#',
            (Key.D5, true) => '%',
            (Key.D6, true) => '^',
            (Key.D7, true) => '&',
            (Key.D8, true) => '*',
            (Key.OemPipe, true) => '|',
            (Key.OemPipe, false) => '\\',
            (Key.OemComma, true) => '<',
            (Key.OemPeriod, true) => '>',
            (Key.OemTilde, true) => '~',
            (Key.D0 or Key.NumPad0, _) => '0',
            (Key.D1 or Key.NumPad1, _) => '1',
            (Key.D2 or Key.NumPad2, _) => '2',
            (Key.D3 or Key.NumPad3, _) => '3',
            (Key.D4 or Key.NumPad4, _) => '4',
            (Key.D5 or Key.NumPad5, _) => '5',
            (Key.D6 or Key.NumPad6, _) => '6',
            (Key.D7 or Key.NumPad7, _) => '7',
            (Key.D8 or Key.NumPad8, _) => '8',
            (Key.D9 or Key.NumPad9, _) => '9',
            (Key.Add, _) or (Key.OemPlus, true) => '+',
            (Key.OemPlus, false) => '=',
            (Key.Subtract or Key.OemMinus, _) => '-',
            (Key.Multiply, _) => '*',
            (Key.Divide or Key.OemQuestion or Key.Oem2, _) => '/',
            (Key.OemOpenBrackets, _) => '[',
            (Key.OemCloseBrackets, _) => ']',
            _ => null,
        };
        shortcutKey = character is null ? default : ShortcutKey.Character(character.Value);
        return character is not null;
    }

    private bool DispatchCalculatorShortcut(string shortcutId)
    {
        switch (CalculatorShortcutRouter.Dispatch(_viewModel, shortcutId))
        {
            case CalculatorShortcutOutcome.CopyDisplay:
                if (_viewModel.IsSettingsOpen || _viewModel.IsNavigationPaneOpen)
                {
                    return false;
                }
                _ = CopyDisplayToClipboardAsync();
                return true;
            case CalculatorShortcutOutcome.PasteExpression:
                if (_viewModel.IsSettingsOpen
                    || _viewModel.IsNavigationPaneOpen
                    || _viewModel.IsDateCalculatorMode)
                {
                    return false;
                }
                _ = PasteFromClipboardAsync();
                return true;
            case CalculatorShortcutOutcome.EnterAlwaysOnTop:
                if (_viewModel.IsStandardMode && !_viewModel.IsAlwaysOnTop)
                {
                    _presentation.EnterCompactOverlay();
                }
                return true;
            case CalculatorShortcutOutcome.ExitAlwaysOnTop:
                if (_viewModel.IsAlwaysOnTop)
                {
                    _presentation.ExitCompactOverlay();
                }
                return true;
            case CalculatorShortcutOutcome.Handled:
                return true;
            default:
                return false;
        }
    }

    private bool DispatchGraphingShortcut(string shortcutId)
    {
        switch (shortcutId)
        {
            case "graphViewButton":
                GraphingView.ResetView();
                return true;
            case "plotButton":
                GraphingView.FocusGraph();
                return true;
            default:
                // Character shortcuts such as x, y and ^ are catalogued for
                // graphing but remain native TextBox input in this frontend.
                return false;
        }
    }

    private async System.Threading.Tasks.Task CopyDisplayToClipboardAsync()
    {
        var clipboard = Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(
                _viewModel.IsUnitConverterMode
                    ? _viewModel.Converter.FromDisplay
                    : _viewModel.IsDateCalculatorMode
                        ? _viewModel.DateCalculator.IsDateDiffMode
                            ? _viewModel.DateCalculator.DateDiffResult
                            : _viewModel.DateCalculator.DateResult
                    : _viewModel.PrimaryDisplay);
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
        if (_viewModel.IsUnitConverterMode)
        {
            _viewModel.Converter.TryPaste(text, _viewModel.DecimalSeparator);
        }
        else
        {
            _viewModel.TryPasteStandardExpression(text);
        }
    }

    /// <summary>
    /// Pressed-state fallback for buttons still declared by this window. Every
    /// keypad now claims its own through IShortcutPressedTarget; the history
    /// toggle is the last one left in the shared mode header.
    /// </summary>
    private bool TryGetShortcutButton(string shortcutId, out Button button)
    {
        button = shortcutId == "HistoryButton" ? HistoryButton : null!;
        return button is not null;
    }

    private void TitleBarChrome_OnDragRequested(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void TitleBarChrome_OnMinimizeRequested(object? sender, EventArgs e) =>
        WindowState = WindowState.Minimized;

    // Compact always-on-top has two entry points: the button in the mode header
    // that enters it, and the title-bar button that leaves it again.
    private void TitleBarChrome_OnAlwaysOnTopToggleRequested(object? sender, EventArgs e) =>
        ToggleCompactAlwaysOnTop();

    private void AlwaysOnTop_OnClick(object? sender, RoutedEventArgs e) =>
        ToggleCompactAlwaysOnTop();

    private void ToggleCompactAlwaysOnTop()
    {
        if (_viewModel.IsAlwaysOnTop)
        {
            _presentation.ExitCompactOverlay();
        }
        else
        {
            _presentation.EnterCompactOverlay();
        }
    }




    private void TitleBarChrome_OnMaximizeRequested(object? sender, EventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void TitleBarChrome_OnCloseRequested(object? sender, EventArgs e) => Close();

    private void OnAppearancePreferencesChanged(PlatformAppearancePreferences preferences)
    {
        // Persist first: the service reads the stored preferences back when it
        // stages a title-bar change across dispatcher frames.
        _settings = _settings with
        {
            UseMicaEffect = preferences.UseMicaEffect,
            WindowCornerStyle = preferences.WindowCornerStyle,
            WindowControlStyle = preferences.WindowControlStyle,
        };
        AppSettingsStore.Save(_settings);
        _presentation.ApplyAppearance(preferences);
    }

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










}
