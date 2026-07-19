using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.ApplicationModel.Resources;

namespace Calculator.Managed;

public partial class CalculatorViewModel : ObservableObject, IDisposable
{
    private const int NavigationTransitionDurationMilliseconds = 220;
    private readonly NativeCalculator _calculator;
    private readonly NativeUnitConverter _unitConverter;
    private bool synchronizingUnitSelection;

    [ObservableProperty]
    public partial string PrimaryDisplay { get; private set; }

    [ObservableProperty]
    public partial string ExpressionDisplay { get; private set; }

    [ObservableProperty]
    public partial bool IsError { get; private set; }

    [ObservableProperty]
    public partial bool IsInputEmpty { get; private set; }

    [ObservableProperty]
    public partial bool HasMemory { get; private set; }

    public ObservableCollection<CalculatorHistoryEntry> History { get; } = [];
    public ObservableCollection<string> Memory { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsNarrowHistoryPaneVisible))]
    public partial bool IsHistoryOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsDockedHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsNarrowHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsHistoryButtonVisible))]
    [NotifyPropertyChangedFor(nameof(IsHistoryCloseButtonVisible))]
    public partial bool IsHistoryDocked { get; private set; }

    [ObservableProperty]
    public partial bool HasHistory { get; private set; }

    public bool IsHistoryPaneVisible => IsCalculatorMode && (IsHistoryDocked || IsHistoryOpen);
    public bool IsDockedHistoryPaneVisible => IsCalculatorMode && IsHistoryDocked;
    public bool IsNarrowHistoryPaneVisible => IsCalculatorMode && !IsHistoryDocked && IsHistoryOpen;
    public bool IsHistoryButtonVisible => IsCalculatorMode && !IsHistoryDocked;
    public bool IsHistoryCloseButtonVisible => !IsHistoryDocked;
    public string ApplicationName { get; } = "Redmond Calculator";
    public string TitleBarApplicationName { get; }
    public string DecimalSeparator { get; }
    [ObservableProperty]
    public partial string ModeDisplayName { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStandardMode))]
    [NotifyPropertyChangedFor(nameof(IsScientificMode))]
    [NotifyPropertyChangedFor(nameof(IsCalculatorMode))]
    [NotifyPropertyChangedFor(nameof(IsUnitConverterMode))]
    [NotifyPropertyChangedFor(nameof(IsHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsDockedHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsNarrowHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsHistoryButtonVisible))]
    public partial CalculatorViewMode CurrentViewMode { get; private set; } = CalculatorViewMode.Standard;

    [ObservableProperty]
    public partial bool IsNavigationPaneOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSettingsBackInTitleBar))]
    public partial bool IsSettingsOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlwaysOnTopTooltip))]
    [NotifyPropertyChangedFor(nameof(AlwaysOnTopAutomationName))]
    [NotifyPropertyChangedFor(nameof(AlwaysOnTopGlyph))]
    public partial bool IsAlwaysOnTop { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteractWithNavigationToggle))]
    public partial bool IsNavigationPaneTransitioning { get; private set; }

    public bool CanInteractWithNavigationToggle => !IsNavigationPaneTransitioning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightThemeSelected))]
    [NotifyPropertyChangedFor(nameof(IsDarkThemeSelected))]
    [NotifyPropertyChangedFor(nameof(IsSystemThemeSelected))]
    public partial AppThemePreference SelectedThemePreference { get; private set; } = AppThemePreference.Dark;

    public bool IsLightThemeSelected => SelectedThemePreference == AppThemePreference.Light;
    public bool IsDarkThemeSelected => SelectedThemePreference == AppThemePreference.Dark;
    public bool IsSystemThemeSelected => SelectedThemePreference == AppThemePreference.System;
    public event Action<AppThemePreference>? ThemePreferenceChanged;

    public ObservableCollection<string> AvailableFontFamilies { get; } = [];

    [ObservableProperty]
    public partial string SelectedFontFamily { get; set; } = "Inter";

    public event Action<string>? FontPreferenceChanged;

    public bool SupportsPlatformAppearanceSettings { get; }
    public string AppFontName { get; } = "App font";
    public string AppFontDescription { get; } = "Choose the font used for text and numbers";
    public string MicaEffectName { get; } = "Translucent background";
    public string MicaEffectDescription { get; } = "Blur the desktop behind the calculator window";
    public string WindowCornersName { get; } = "Window corners";
    public string WindowCornersDescription { get; } = "Choose the outer window shape";
    public string Windows10CornersName { get; } = "Windows 10 — square";
    public string Windows11CornersName { get; } = "Windows 11 — rounded";
    public string MacOSCornersName { get; } = "macOS — rounded";
    public string WindowControlsName { get; } = "Title bar controls";
    public string WindowControlsDescription { get; } = "Choose title bar geometry independently from window corners";
    public string Windows10WindowControlsName { get; } = "Windows 10";
    public string Windows11WindowControlsName { get; } = "Windows 11";
    public string MacOSWindowControlsName { get; } = "macOS";

    [ObservableProperty]
    public partial bool UseMicaEffect { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowCornerRadius))]
    [NotifyPropertyChangedFor(nameof(UsesCustomResizeHandles))]
    [NotifyPropertyChangedFor(nameof(UsesNativeWindowGeometry))]
    [NotifyPropertyChangedFor(nameof(UsesSquareWindowCorners))]
    [NotifyPropertyChangedFor(nameof(IsWindows10CornerStyleSelected))]
    [NotifyPropertyChangedFor(nameof(IsWindows11CornerStyleSelected))]
    [NotifyPropertyChangedFor(nameof(IsMacOSCornerStyleSelected))]
    public partial WindowCornerStyle SelectedWindowCornerStyle { get; private set; } = WindowCornerStyle.Windows11;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesWindowsWindowControls))]
    [NotifyPropertyChangedFor(nameof(UsesMacOSWindowControls))]
    [NotifyPropertyChangedFor(nameof(ShowsSettingsBackInTitleBar))]
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
    public bool UsesNativeWindowGeometry => SelectedWindowCornerStyle == WindowCornerStyle.MacOS;
    public bool UsesSquareWindowCorners => SelectedWindowCornerStyle == WindowCornerStyle.Windows10;
    public bool UsesWindowsWindowControls => SelectedWindowControlStyle != WindowControlStyle.MacOS;
    public bool UsesMacOSWindowControls => SelectedWindowControlStyle == WindowControlStyle.MacOS;
    public bool ShowsSettingsBackInTitleBar => IsSettingsOpen && UsesWindowsWindowControls;
    public double WindowCornerRadius => SelectedWindowCornerStyle == WindowCornerStyle.Windows11 ? 8 : 0;
    public bool UsesCustomResizeHandles => !UsesNativeWindowGeometry;
    public event Action<PlatformAppearancePreferences>? PlatformAppearancePreferencesChanged;

    public bool IsStandardMode => CurrentViewMode == CalculatorViewMode.Standard;
    public bool IsScientificMode => CurrentViewMode == CalculatorViewMode.Scientific;
    public bool IsCalculatorMode => IsStandardMode || IsScientificMode;
    public bool IsUnitConverterMode => CurrentViewMode is >= CalculatorViewMode.Volume and <= CalculatorViewMode.Angle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScientificAngleLabel))]
    public partial CalculatorAngleMode SelectedScientificAngle { get; private set; } = CalculatorAngleMode.Degrees;

    [ObservableProperty]
    public partial bool IsScientificInverse { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRegularTrigFunctions))]
    [NotifyPropertyChangedFor(nameof(ShowsInverseTrigFunctions))]
    [NotifyPropertyChangedFor(nameof(ShowsHyperbolicTrigFunctions))]
    [NotifyPropertyChangedFor(nameof(ShowsInverseHyperbolicTrigFunctions))]
    public partial bool IsTrigInverse { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsRegularTrigFunctions))]
    [NotifyPropertyChangedFor(nameof(ShowsInverseTrigFunctions))]
    [NotifyPropertyChangedFor(nameof(ShowsHyperbolicTrigFunctions))]
    [NotifyPropertyChangedFor(nameof(ShowsInverseHyperbolicTrigFunctions))]
    public partial bool IsTrigHyperbolic { get; set; }

    [ObservableProperty]
    public partial bool IsScientificNotation { get; private set; }

    [ObservableProperty]
    public partial uint OpenParenthesisCount { get; private set; }

    public string ScientificAngleLabel => SelectedScientificAngle switch
    {
        CalculatorAngleMode.Degrees => "DEG",
        CalculatorAngleMode.Radians => "RAD",
        _ => "GRAD",
    };
    public bool ShowsRegularTrigFunctions => !IsTrigInverse && !IsTrigHyperbolic;
    public bool ShowsInverseTrigFunctions => IsTrigInverse && !IsTrigHyperbolic;
    public bool ShowsHyperbolicTrigFunctions => !IsTrigInverse && IsTrigHyperbolic;
    public bool ShowsInverseHyperbolicTrigFunctions => IsTrigInverse && IsTrigHyperbolic;

    [ObservableProperty]
    public partial string UnitFromDisplay { get; private set; } = "0";

    [ObservableProperty]
    public partial string UnitToDisplay { get; private set; } = "0";

    [ObservableProperty]
    public partial UnitConverterCategory? SelectedUnitCategory { get; set; }

    [ObservableProperty]
    public partial UnitConverterUnit? SelectedFromUnit { get; set; }

    [ObservableProperty]
    public partial UnitConverterUnit? SelectedToUnit { get; set; }

    public ObservableCollection<UnitConverterCategory> UnitCategories { get; } = [];
    public ObservableCollection<UnitConverterUnit> UnitDefinitions { get; } = [];
    public ObservableCollection<string> UnitSuggestions { get; } = [];
    public ObservableCollection<CalculatorNavigationItem> CalculatorNavigationItems { get; } = [];
    public ObservableCollection<CalculatorNavigationItem> ConverterNavigationItems { get; } = [];
    public string CalculatorGroupName { get; }
    public string ConverterGroupName { get; }
    public string SettingsName { get; }
    public string SettingsAppearanceName { get; }
    public string AppThemeName { get; }
    public string AppThemeDescription { get; }
    public string LightThemeName { get; }
    public string DarkThemeName { get; }
    public string SystemThemeName { get; }
    public string BackAutomationName { get; }
    public string AboutGroupName { get; }
    public string AboutLicenseName { get; }
    public string AboutServicesName { get; }
    public string AboutPrivacyName { get; }
    public string FeedbackName { get; }
    public string AboutVersionText { get; } = "Redmond Calculator 0.1.0";
    public string HistoryAutomationName { get; }
    public string HistoryEmptyText { get; }
    public string ClearHistoryTooltip { get; }
    public string MemoryTooltip { get; }
    public string HistoryTooltip { get; }
    public string TrigonometryName { get; }
    public string FunctionName { get; }
    public string SettingsBackTooltip { get; }
    public string ClearMemoryTooltip { get; }
    public string MemoryStoreTooltip { get; }
    public string MemoryRecallTooltip { get; }
    public string MemoryAddTooltip { get; }
    public string MemorySubtractTooltip { get; }
    public string EnterAlwaysOnTopTooltip { get; }
    public string ExitAlwaysOnTopTooltip { get; }
    public string EnterAlwaysOnTopAutomationName { get; }
    public string ExitAlwaysOnTopAutomationName { get; }
    public string AlwaysOnTopTooltip => IsAlwaysOnTop ? ExitAlwaysOnTopTooltip : EnterAlwaysOnTopTooltip;
    public string AlwaysOnTopAutomationName => IsAlwaysOnTop ? ExitAlwaysOnTopAutomationName : EnterAlwaysOnTopAutomationName;
    public string AlwaysOnTopGlyph => IsAlwaysOnTop ? "\uEE47" : "\uEE49";

    public CalculatorViewModel(
        AppThemePreference initialThemePreference = AppThemePreference.Dark,
        PlatformAppearancePreferences? initialPlatformAppearance = null,
        bool supportsPlatformAppearanceSettings = false,
        CultureInfo? numberCulture = null,
        IEnumerable<string>? availableFontFamilies = null,
        string? initialFontFamily = null)
    {
        var platformAppearance = initialPlatformAppearance ?? new PlatformAppearancePreferences();
        var numberFormat = CalculatorNumberFormat.FromCulture(numberCulture);
        DecimalSeparator = numberFormat.DecimalSeparator;
        SelectedThemePreference = initialThemePreference;
        var fontFamilies = (availableFontFamilies ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Append("Inter")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name.Equals("Inter", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name, StringComparer.CurrentCultureIgnoreCase);
        Replace(AvailableFontFamilies, fontFamilies);
        SelectedFontFamily = AvailableFontFamilies.FirstOrDefault(
            name => name.Equals(initialFontFamily, StringComparison.OrdinalIgnoreCase)) ?? "Inter";
        UseMicaEffect = platformAppearance.UseMicaEffect;
        SelectedWindowCornerStyle = platformAppearance.WindowCornerStyle;
        SelectedWindowControlStyle = platformAppearance.WindowControlStyle;
        SupportsPlatformAppearanceSettings = supportsPlatformAppearanceSettings;
        var appResources = ResourceLoader.GetForViewIndependentUse();
        TitleBarApplicationName = appResources.GetString("AppName");
        ModeDisplayName = appResources.GetString("StandardModeText");
        CalculatorGroupName = appResources.GetString("CalculatorModeTextCaps");
        ConverterGroupName = appResources.GetString("ConverterModeTextCaps");
        SettingsName = appResources.GetString("SettingsHeader.Text");
        SettingsAppearanceName = appResources.GetString("SettingsAppearance.Text");
        AppThemeName = appResources.GetString("AppThemeExpander.Header");
        AppThemeDescription = appResources.GetString("AppThemeExpander.Description");
        LightThemeName = appResources.GetString("LightThemeRadioButton.Content");
        DarkThemeName = appResources.GetString("DarkThemeRadioButton.Content");
        SystemThemeName = appResources.GetString("SystemThemeRadioButton.Content");
        BackAutomationName = appResources.GetString("TitleBarBackButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        AboutGroupName = appResources.GetString("AboutGroupTitle.Text");
        AboutLicenseName = appResources.GetString("AboutEULA.Text");
        AboutServicesName = appResources.GetString("AboutControlServicesAgreement.Text");
        AboutPrivacyName = appResources.GetString("AboutControlPrivacyStatement.Text");
        FeedbackName = appResources.GetString("FeedbackButton.Content");
        HistoryAutomationName = appResources.GetString("HistoryLabel/Text");
        HistoryEmptyText = appResources.GetString("HistoryEmpty/Text");
        ClearHistoryTooltip = appResources.GetString("ClearHistory/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        MemoryTooltip = appResources.GetString("MemoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        HistoryTooltip = appResources.GetString("HistoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        TrigonometryName = appResources.GetString("trigButton.Text");
        FunctionName = appResources.GetString("funcButton.Text");
        SettingsBackTooltip = appResources.GetString("AboutControlBackButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        ClearMemoryTooltip = appResources.GetString("ClearMemoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        MemoryStoreTooltip = appResources.GetString("memButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        MemoryRecallTooltip = appResources.GetString("MemRecall/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        MemoryAddTooltip = appResources.GetString("MemPlus/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        MemorySubtractTooltip = appResources.GetString("MemMinus/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        EnterAlwaysOnTopTooltip = appResources.GetString("EnterAlwaysOnTopButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        ExitAlwaysOnTopTooltip = appResources.GetString("ExitAlwaysOnTopButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        EnterAlwaysOnTopAutomationName = appResources.GetString("EnterAlwaysOnTopButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        ExitAlwaysOnTopAutomationName = appResources.GetString("ExitAlwaysOnTopButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        _calculator = new NativeCalculator(ResourceLoader.GetForViewIndependentUse("CEngineStrings"), numberFormat);
        var regionCode = GetCurrentRegionCode();
        _unitConverter = new NativeUnitConverter(appResources, regionCode, numberFormat);
        Replace(UnitCategories, _unitConverter.Categories);
        synchronizingUnitSelection = true;
        SelectedUnitCategory = UnitCategories.FirstOrDefault();
        synchronizingUnitSelection = false;
        if (SelectedUnitCategory is not null)
        {
            _unitConverter.SelectCategory(SelectedUnitCategory.Id);
        }
        SynchronizeUnitConverter();
        BuildNavigationItems(appResources);
        SetSelectedNavigationItem(CalculatorViewMode.Standard);
        PrimaryDisplay = _calculator.PrimaryDisplay;
        ExpressionDisplay = _calculator.ExpressionDisplay;
    }

    [RelayCommand]
    private void SendCommand(string commandName)
    {
        ExecuteCalculatorCommand(Enum.Parse<CalculatorCommand>(commandName, ignoreCase: false));
    }

    public void ExecuteCalculatorCommand(CalculatorCommand command)
    {
        if (IsScientificMode && IsError)
        {
            // UWP first clears the engine for every command received while in
            // error, then forwards only operands. This is why digits and the
            // decimal separator recover immediately while Backspace/Equals
            // merely clear the error display.
            _calculator.SendCommand(CalculatorCommand.Clear);
            if (!IsScientificErrorRecoverable(command))
            {
                IsScientificNotation = false;
                Synchronize();
                return;
            }
        }

        _calculator.SendCommand(command);
        if (command is CalculatorCommand.Clear or CalculatorCommand.ClearEntry)
        {
            IsScientificNotation = false;
        }
        if (IsScientificInverse && command is CalculatorCommand.Cube or CalculatorCommand.CubeRoot
            or CalculatorCommand.Root or CalculatorCommand.TwoPowerX
            or CalculatorCommand.LogBaseY or CalculatorCommand.EPowerX)
        {
            IsScientificInverse = false;
        }
        if (command is CalculatorCommand.Sin or CalculatorCommand.Cos or CalculatorCommand.Tan
            or CalculatorCommand.Sinh or CalculatorCommand.Cosh or CalculatorCommand.Tanh
            or CalculatorCommand.InverseSin or CalculatorCommand.InverseCos or CalculatorCommand.InverseTan
            or CalculatorCommand.InverseSinh or CalculatorCommand.InverseCosh or CalculatorCommand.InverseTanh
            or CalculatorCommand.Sec or CalculatorCommand.Csc or CalculatorCommand.Cot
            or CalculatorCommand.Sech or CalculatorCommand.Csch or CalculatorCommand.Coth
            or CalculatorCommand.InverseSec or CalculatorCommand.InverseCsc or CalculatorCommand.InverseCot
            or CalculatorCommand.InverseSech or CalculatorCommand.InverseCsch or CalculatorCommand.InverseCoth)
        {
            IsTrigInverse = false;
            IsTrigHyperbolic = false;
        }
        Synchronize();
    }

    private static bool IsScientificErrorRecoverable(CalculatorCommand command) =>
        command is >= CalculatorCommand.Zero and <= CalculatorCommand.Nine
            or CalculatorCommand.Decimal;

    [RelayCommand]
    private void CycleScientificAngle()
    {
        SelectedScientificAngle = SelectedScientificAngle switch
        {
            CalculatorAngleMode.Degrees => CalculatorAngleMode.Radians,
            CalculatorAngleMode.Radians => CalculatorAngleMode.Grads,
            _ => CalculatorAngleMode.Degrees,
        };
        _calculator.SendCommand(SelectedScientificAngle switch
        {
            CalculatorAngleMode.Degrees => CalculatorCommand.Degree,
            CalculatorAngleMode.Radians => CalculatorCommand.Radian,
            _ => CalculatorCommand.Grads,
        });
        Synchronize();
    }

    [RelayCommand]
    private void ToggleScientificNotation()
    {
        _calculator.SendCommand(CalculatorCommand.ScientificNotation);
        IsScientificNotation = !IsScientificNotation;
        Synchronize();
    }

    [RelayCommand]
    private void ToggleScientificInverse() => IsScientificInverse = !IsScientificInverse;

    [RelayCommand]
    private void ToggleTrigInverse() => IsTrigInverse = !IsTrigInverse;

    [RelayCommand]
    private void ToggleTrigHyperbolic() => IsTrigHyperbolic = !IsTrigHyperbolic;

    /// <summary>
    /// Cross-platform counterpart of StandardCalculatorViewModel::OnPaste.
    /// Clipboard access stays in the frontend; expression interpretation stays
    /// beside the shared CalculatorManager command surface.
    /// </summary>
    public bool TryPasteStandardExpression(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression) || expression.Length > 512)
        {
            return false;
        }

        var normalized = expression
            .Replace('×', '*')
            .Replace('÷', '/')
            .Replace('−', '-');
        var isScientific = IsScientificMode;
        if (normalized.Any(character =>
                !char.IsWhiteSpace(character)
                && !char.IsDigit(character)
                && character is not '+' and not '-' and not '*' and not '/' and not '=' and not '.' and not ','
                && (!isScientific || character is not '^' and not '%' and not '(' and not ')' and not 'e' and not 'E')))
        {
            return false;
        }
        if (isScientific)
        {
            var parenthesisDepth = 0;
            foreach (var character in normalized)
            {
                if (character == '(')
                {
                    parenthesisDepth++;
                }
                else if (character == ')' && --parenthesisDepth < 0)
                {
                    return false;
                }
            }
            if (parenthesisDepth != 0)
            {
                return false;
            }
        }

        // A history selection intentionally changes only the presentation in
        // Microsoft's view model. Reset the pending engine state before paste
        // so the pasted expression cannot inherit an earlier completed binary
        // operation; memory and history remain intact.
        _calculator.Reset(clearMemory: false);
        if (isScientific)
        {
            _calculator.SetMode(CalculatorMode.Scientific);
            _calculator.SendCommand(SelectedScientificAngle switch
            {
                CalculatorAngleMode.Degrees => CalculatorCommand.Degree,
                CalculatorAngleMode.Radians => CalculatorCommand.Radian,
                _ => CalculatorCommand.Grads,
            });
            if (IsScientificNotation)
            {
                _calculator.SendCommand(CalculatorCommand.ScientificNotation);
            }
        }

        var isFirstLegalCharacter = true;
        var isPreviousOperator = false;
        var sendNegate = false;
        var sentCommand = false;
        var negateStack = new Stack<bool>();

        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            CalculatorCommand? command = character switch
            {
                '0' => CalculatorCommand.Zero,
                '1' => CalculatorCommand.One,
                '2' => CalculatorCommand.Two,
                '3' => CalculatorCommand.Three,
                '4' => CalculatorCommand.Four,
                '5' => CalculatorCommand.Five,
                '6' => CalculatorCommand.Six,
                '7' => CalculatorCommand.Seven,
                '8' => CalculatorCommand.Eight,
                '9' => CalculatorCommand.Nine,
                '.' or ',' => CalculatorCommand.Decimal,
                '+' => CalculatorCommand.Add,
                '-' => CalculatorCommand.Subtract,
                '*' => CalculatorCommand.Multiply,
                '/' => CalculatorCommand.Divide,
                '=' => CalculatorCommand.Equals,
                '^' when isScientific => CalculatorCommand.Power,
                '%' when isScientific => CalculatorCommand.Modulo,
                '(' when isScientific => CalculatorCommand.OpenParenthesis,
                ')' when isScientific => CalculatorCommand.CloseParenthesis,
                'e' or 'E' when isScientific => CalculatorCommand.Exp,
                _ => null,
            };

            if (command is null)
            {
                continue;
            }

            var isOperator = command is CalculatorCommand.Add or CalculatorCommand.Subtract
                or CalculatorCommand.Multiply or CalculatorCommand.Divide
                or CalculatorCommand.Power or CalculatorCommand.Modulo;
            if (isFirstLegalCharacter || isPreviousOperator)
            {
                isFirstLegalCharacter = false;
                isPreviousOperator = false;
                if (command == CalculatorCommand.Subtract)
                {
                    sendNegate = true;
                    continue;
                }
                if (command == CalculatorCommand.Add)
                {
                    continue;
                }
            }

            if (command == CalculatorCommand.OpenParenthesis)
            {
                negateStack.Push(sendNegate);
                sendNegate = false;
                _calculator.SendCommand(CalculatorCommand.OpenParenthesis);
                sentCommand = true;
                isPreviousOperator = true;
                continue;
            }
            else if (command == CalculatorCommand.CloseParenthesis)
            {
                if (negateStack.Count == 0)
                {
                    continue;
                }

                _calculator.SendCommand(CalculatorCommand.CloseParenthesis);
                sentCommand = true;
                if (negateStack.Pop())
                {
                    _calculator.SendCommand(CalculatorCommand.Sign);
                }
                isPreviousOperator = false;
                continue;
            }

            if (sendNegate && (isOperator || command == CalculatorCommand.Equals))
            {
                // Apply a unary minus only after the complete operand has been
                // entered. Sending Sign after its first digit makes the native
                // engine treat the remaining digits as a continuation of an
                // intermediate signed result and can overflow on paste.
                _calculator.SendCommand(CalculatorCommand.Sign);
                sendNegate = false;
            }

            _calculator.SendCommand(command.Value);
            sentCommand = true;
            isPreviousOperator = isOperator;

            if (command == CalculatorCommand.Exp && index + 1 < normalized.Length)
            {
                var exponentSign = normalized[index + 1];
                if (exponentSign == '-')
                {
                    _calculator.SendCommand(CalculatorCommand.Sign);
                    index++;
                }
                else if (exponentSign == '+')
                {
                    index++;
                }
            }
        }

        if (sendNegate && sentCommand)
        {
            _calculator.SendCommand(CalculatorCommand.Sign);
        }

        Synchronize();
        return sentCommand;
    }

    [RelayCommand]
    private void Reset()
    {
        _calculator.Reset();
        Synchronize();
    }

    [RelayCommand]
    private void MemoryStore() { _calculator.MemoryStore(); Synchronize(); }

    [RelayCommand]
    private void MemoryRecall() { _calculator.MemoryRecall(); Synchronize(); }

    [RelayCommand]
    private void MemoryAdd() { _calculator.MemoryAdd(); Synchronize(); }

    [RelayCommand]
    private void MemorySubtract() { _calculator.MemorySubtract(); Synchronize(); }

    [RelayCommand]
    private void MemoryClear() { _calculator.MemoryClear(); Synchronize(); }

    [RelayCommand]
    private void MemoryClearAll() { _calculator.MemoryClearAll(); Synchronize(); }

    [RelayCommand]
    private void ToggleHistory()
    {
        if (!IsHistoryDocked)
        {
            IsHistoryOpen = !IsHistoryOpen;
        }
    }

    [RelayCommand]
    private void CloseHistory() => IsHistoryOpen = false;

    [RelayCommand]
    private void ClearHistory()
    {
        _calculator.HistoryClear();
        Synchronize();
    }

    [RelayCommand]
    private void DeleteHistoryEntry(CalculatorHistoryEntry? entry)
    {
        var index = entry is null ? -1 : History.IndexOf(entry);
        if (index < 0)
        {
            return;
        }

        _calculator.HistoryRemove(checked((nuint)index));
        Synchronize();
    }

    [RelayCommand]
    private void SelectHistoryEntry(CalculatorHistoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        ExpressionDisplay = entry.Expression;
        PrimaryDisplay = entry.Result;
        IsError = false;
        if (!IsHistoryDocked)
        {
            IsHistoryOpen = false;
        }
    }

    public void SetHistoryDocked(bool value)
    {
        IsHistoryDocked = value;
        if (value)
        {
            IsHistoryOpen = false;
        }
    }

    [RelayCommand]
    private Task ToggleNavigationPane() => SetNavigationPaneOpenAsync(!IsNavigationPaneOpen);

    [RelayCommand]
    private Task CloseNavigationPane() => SetNavigationPaneOpenAsync(false);

    [RelayCommand]
    private async Task OpenSettings()
    {
        await SetNavigationPaneOpenAsync(false);
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private void SelectTheme(string preference)
    {
        SelectedThemePreference = Enum.Parse<AppThemePreference>(preference, ignoreCase: false);
    }

    [RelayCommand]
    private void SelectWindowCornerStyle(string style) =>
        SelectedWindowCornerStyle = Enum.Parse<WindowCornerStyle>(style, ignoreCase: false);

    [RelayCommand]
    private void SelectWindowControlStyle(string style) =>
        SelectedWindowControlStyle = Enum.Parse<WindowControlStyle>(style, ignoreCase: false);

    [RelayCommand]
    private async Task SelectNavigationItem(CalculatorNavigationItem? item)
    {
        if (item is null || !item.IsEnabled)
        {
            return;
        }

        CurrentViewMode = item.Mode;
        IsHistoryOpen = false;
        ModeDisplayName = item.Name;
        SetSelectedNavigationItem(item.Mode);

        if (item.Mode is CalculatorViewMode.Standard or CalculatorViewMode.Scientific)
        {
            if (item.Mode == CalculatorViewMode.Standard)
            {
                IsScientificNotation = false;
                IsScientificInverse = false;
                IsTrigInverse = false;
                IsTrigHyperbolic = false;
            }
            _calculator.SetMode(item.Mode == CalculatorViewMode.Scientific
                ? CalculatorMode.Scientific
                : CalculatorMode.Standard);
            Synchronize();
        }

        if (item.Group == CalculatorNavigationGroup.Converter)
        {
            var category = UnitCategories.FirstOrDefault(value => value.Id == (int)item.Mode);
            if (category is not null)
            {
                SelectedUnitCategory = category;
                if (_unitConverter.SelectedUnits.FromUnitId < 0)
                {
                    _unitConverter.SelectCategory(category.Id);
                    SynchronizeUnitConverter();
                }
            }
        }

        await SetNavigationPaneOpenAsync(false);
    }

    [RelayCommand]
    private void SendUnitCommand(string commandName)
    {
        _unitConverter.SendCommand(Enum.Parse<UnitConverterCommand>(commandName, ignoreCase: false));
        SynchronizeUnitDisplays();
    }

    [RelayCommand]
    private void SwapUnits()
    {
        _unitConverter.SwitchActive(UnitToDisplay);
        SynchronizeUnitConverter();
    }

    public void Dispose()
    {
        _calculator.Dispose();
        _unitConverter.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Synchronize()
    {
        PrimaryDisplay = _calculator.PrimaryDisplay;
        ExpressionDisplay = _calculator.ExpressionDisplay;
        IsError = _calculator.IsError;
        IsInputEmpty = _calculator.IsInputEmpty;
        OpenParenthesisCount = _calculator.EventState.ParenthesisCount;

        Replace(History, _calculator.History);
        HasHistory = History.Count != 0;
        Replace(Memory, _calculator.MemoryValues);
        HasMemory = Memory.Count != 0;
    }

    private static void Replace(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    partial void OnSelectedUnitCategoryChanged(UnitConverterCategory? value)
    {
        if (value is null || synchronizingUnitSelection)
        {
            return;
        }
        _unitConverter.SelectCategory(value.Id);
        ModeDisplayName = value.Name;
        CurrentViewMode = (CalculatorViewMode)value.Id;
        SetSelectedNavigationItem(CurrentViewMode);
        SynchronizeUnitConverter();
    }

    partial void OnSelectedFromUnitChanged(UnitConverterUnit? value) => ApplySelectedUnits();
    partial void OnSelectedToUnitChanged(UnitConverterUnit? value) => ApplySelectedUnits();

    partial void OnUseMicaEffectChanged(bool value) => NotifyPlatformAppearanceChanged();
    partial void OnSelectedFontFamilyChanged(string value)
    {
        if (AvailableFontFamilies.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            FontPreferenceChanged?.Invoke(value);
        }
    }
    partial void OnSelectedWindowCornerStyleChanged(WindowCornerStyle value) => NotifyPlatformAppearanceChanged();
    partial void OnSelectedWindowControlStyleChanged(WindowControlStyle value) => NotifyPlatformAppearanceChanged();

    private void NotifyPlatformAppearanceChanged() => PlatformAppearancePreferencesChanged?.Invoke(
        new PlatformAppearancePreferences(
            UseMicaEffect,
            SelectedWindowCornerStyle,
            SelectedWindowControlStyle));

    private void ApplySelectedUnits()
    {
        if (synchronizingUnitSelection || SelectedFromUnit is null || SelectedToUnit is null)
        {
            return;
        }
        _unitConverter.SetUnits(SelectedFromUnit.Id, SelectedToUnit.Id);
        SynchronizeUnitDisplays();
    }

    private void SynchronizeUnitConverter()
    {
        synchronizingUnitSelection = true;
        try
        {
            var units = _unitConverter.Units.Where(unit => !unit.IsWhimsical).ToArray();
            Replace(UnitDefinitions, units);
            var selected = _unitConverter.SelectedUnits;
            SelectedFromUnit = units.FirstOrDefault(unit => unit.Id == selected.FromUnitId);
            SelectedToUnit = units.FirstOrDefault(unit => unit.Id == selected.ToUnitId);
        }
        finally
        {
            synchronizingUnitSelection = false;
        }
        SynchronizeUnitDisplays();
    }

    private void SynchronizeUnitDisplays()
    {
        UnitFromDisplay = _unitConverter.FromDisplay;
        UnitToDisplay = _unitConverter.ToDisplay;
        var abbreviations = _unitConverter.Units.ToDictionary(unit => unit.Id, unit => unit.Abbreviation);
        Replace(UnitSuggestions, _unitConverter.Suggestions.Select(suggestion =>
            abbreviations.TryGetValue(suggestion.UnitId, out var abbreviation)
                ? $"{suggestion.Value} {abbreviation}"
                : suggestion.Value));
    }

    private static string GetCurrentRegionCode()
    {
        try
        {
            return RegionInfo.CurrentRegion.TwoLetterISORegionName;
        }
        catch (ArgumentException)
        {
            return "US";
        }
    }

    private void BuildNavigationItems(ResourceLoader resources)
    {
        CalculatorNavigationItems.Add(new(CalculatorViewMode.Standard, CalculatorNavigationGroup.Calculator,
            resources.GetString("StandardModeText"), "\uE8EF", true));
        CalculatorNavigationItems.Add(new(CalculatorViewMode.Scientific, CalculatorNavigationGroup.Calculator,
            resources.GetString("ScientificModeText"), "\uF196", true));
        CalculatorNavigationItems.Add(new(CalculatorViewMode.Graphing, CalculatorNavigationGroup.Calculator,
            resources.GetString("GraphingCalculatorModeText"), "\uF770", false));
        CalculatorNavigationItems.Add(new(CalculatorViewMode.Programmer, CalculatorNavigationGroup.Calculator,
            resources.GetString("ProgrammerModeText"), "\uECCE", false));
        CalculatorNavigationItems.Add(new(CalculatorViewMode.Date, CalculatorNavigationGroup.Calculator,
            resources.GetString("DateCalculationModeText"), "\uE787", false));

        // Currency remains disabled until its HTTP/cache loader is made portable.
        AddConverterNavigationItem(resources, CalculatorViewMode.Currency, "CategoryName_CurrencyText", "\uEB0D", false);
        AddConverterNavigationItem(resources, CalculatorViewMode.Volume, "CategoryName_VolumeText", "\uF1AA");
        AddConverterNavigationItem(resources, CalculatorViewMode.Length, "CategoryName_LengthText", "\uECC6");
        AddConverterNavigationItem(resources, CalculatorViewMode.Weight, "CategoryName_WeightText", "\uF4C1");
        AddConverterNavigationItem(resources, CalculatorViewMode.Temperature, "CategoryName_TemperatureText", "\uE7A3");
        AddConverterNavigationItem(resources, CalculatorViewMode.Energy, "CategoryName_EnergyText", "\uECAD");
        AddConverterNavigationItem(resources, CalculatorViewMode.Area, "CategoryName_AreaText", "\uE809");
        AddConverterNavigationItem(resources, CalculatorViewMode.Speed, "CategoryName_SpeedText", "\uEADA");
        AddConverterNavigationItem(resources, CalculatorViewMode.Time, "CategoryName_TimeText", "\uE917");
        AddConverterNavigationItem(resources, CalculatorViewMode.Power, "CategoryName_PowerText", "\uE945");
        AddConverterNavigationItem(resources, CalculatorViewMode.Data, "CategoryName_DataText", "\uF20F");
        AddConverterNavigationItem(resources, CalculatorViewMode.Pressure, "CategoryName_PressureText", "\uEC4A");
        AddConverterNavigationItem(resources, CalculatorViewMode.Angle, "CategoryName_AngleText", "\uF515");
    }

    private void AddConverterNavigationItem(
        ResourceLoader resources,
        CalculatorViewMode mode,
        string resourceKey,
        string glyph,
        bool isEnabled = true)
    {
        ConverterNavigationItems.Add(new(mode, CalculatorNavigationGroup.Converter, resources.GetString(resourceKey), glyph, isEnabled));
    }

    private void SetSelectedNavigationItem(CalculatorViewMode mode)
    {
        foreach (var item in CalculatorNavigationItems.Concat(ConverterNavigationItems))
        {
            item.IsSelected = item.Mode == mode;
        }
    }

    private async Task SetNavigationPaneOpenAsync(bool isOpen)
    {
        if (IsNavigationPaneTransitioning || IsNavigationPaneOpen == isOpen)
        {
            return;
        }

        IsNavigationPaneTransitioning = true;
        IsNavigationPaneOpen = isOpen;
        await Task.Delay(NavigationTransitionDurationMilliseconds);
        IsNavigationPaneTransitioning = false;
    }

    partial void OnSelectedThemePreferenceChanged(AppThemePreference value) => ThemePreferenceChanged?.Invoke(value);
}
