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

    [ObservableProperty]
    public partial string PrimaryDisplay { get; private set; }

    [ObservableProperty]
    public partial string ExpressionDisplay { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreProgrammerHexDigitsEnabled))]
    [NotifyPropertyChangedFor(nameof(AreProgrammerEightAndNineEnabled))]
    [NotifyPropertyChangedFor(nameof(AreProgrammerTwoThroughSevenEnabled))]
    public partial bool IsError { get; private set; }

    [ObservableProperty]
    public partial bool IsInputEmpty { get; private set; }


    public HistoryViewModel History { get; }

    public MemoryViewModel Memory { get; }

    public UnitConverterViewModel Converter { get; }

    public SettingsViewModel Settings { get; }



    public bool IsHistoryPaneVisible => IsCalculatorMode && !IsAlwaysOnTop && (History.IsDocked || History.IsOpen);
    public bool IsDockedHistoryPaneVisible => IsCalculatorMode && !IsAlwaysOnTop && History.IsDocked;
    public bool IsNarrowHistoryPaneVisible => IsCalculatorMode && !IsAlwaysOnTop && !History.IsDocked && History.IsOpen;
    public bool IsHistoryButtonVisible => IsStandardOrScientificMode && !IsAlwaysOnTop && !History.IsDocked;
    public string ApplicationName { get; } = "Redmond Calculator";
    public string TitleBarApplicationName { get; }
    public string DecimalSeparator { get; }
    [ObservableProperty]
    public partial string ModeDisplayName { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStandardMode))]
    [NotifyPropertyChangedFor(nameof(IsScientificMode))]
    [NotifyPropertyChangedFor(nameof(IsProgrammerMode))]
    [NotifyPropertyChangedFor(nameof(IsCalculatorMode))]
    [NotifyPropertyChangedFor(nameof(IsStandardOrScientificMode))]
    [NotifyPropertyChangedFor(nameof(IsUnitConverterMode))]
    [NotifyPropertyChangedFor(nameof(IsHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsDockedHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsNarrowHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsHistoryButtonVisible))]
    [NotifyPropertyChangedFor(nameof(CanEnterAlwaysOnTop))]
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
    [NotifyPropertyChangedFor(nameof(CanEnterAlwaysOnTop))]
    [NotifyPropertyChangedFor(nameof(IsHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsDockedHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsNarrowHistoryPaneVisible))]
    [NotifyPropertyChangedFor(nameof(IsHistoryButtonVisible))]
    [NotifyPropertyChangedFor(nameof(ShowsWindowsAlwaysOnTopExit))]
    [NotifyPropertyChangedFor(nameof(ShowsMacOSAlwaysOnTopExit))]
    [NotifyPropertyChangedFor(nameof(ShowsWindowsMaximizeButton))]
    public partial bool IsAlwaysOnTop { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInteractWithNavigationToggle))]
    public partial bool IsNavigationPaneTransitioning { get; private set; }

    public bool CanInteractWithNavigationToggle => !IsNavigationPaneTransitioning;






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


    public bool UsesNativeWindowGeometry => Settings.SelectedWindowCornerStyle == WindowCornerStyle.MacOS;
    public bool UsesSquareWindowCorners => Settings.SelectedWindowCornerStyle == WindowCornerStyle.Windows10;
    public bool UsesWindowsWindowControls => Settings.SelectedWindowControlStyle != WindowControlStyle.MacOS;
    public bool UsesMacOSWindowControls => Settings.SelectedWindowControlStyle == WindowControlStyle.MacOS;
    public bool ShowsSettingsBackInTitleBar => IsSettingsOpen && UsesWindowsWindowControls;
    public bool ShowsWindowsAlwaysOnTopExit => IsAlwaysOnTop && UsesWindowsWindowControls;
    public bool ShowsMacOSAlwaysOnTopExit => IsAlwaysOnTop && UsesMacOSWindowControls;
    public bool ShowsWindowsMaximizeButton => !IsAlwaysOnTop && UsesWindowsWindowControls;
    public double WindowCornerRadius => Settings.SelectedWindowCornerStyle == WindowCornerStyle.Windows11 ? 8 : 0;
    public bool UsesCustomResizeHandles => !UsesNativeWindowGeometry;

    public bool IsStandardMode => CurrentViewMode == CalculatorViewMode.Standard;
    public bool CanEnterAlwaysOnTop => IsStandardMode && !IsAlwaysOnTop;
    public bool IsScientificMode => CurrentViewMode == CalculatorViewMode.Scientific;
    public bool IsProgrammerMode => CurrentViewMode == CalculatorViewMode.Programmer;
    public bool IsCalculatorMode => IsStandardMode || IsScientificMode || IsProgrammerMode;
    public bool IsStandardOrScientificMode => IsStandardMode || IsScientificMode;
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
    [NotifyPropertyChangedFor(nameof(IsProgrammerHexadecimal))]
    [NotifyPropertyChangedFor(nameof(IsProgrammerDecimal))]
    [NotifyPropertyChangedFor(nameof(IsProgrammerOctal))]
    [NotifyPropertyChangedFor(nameof(IsProgrammerBinary))]
    [NotifyPropertyChangedFor(nameof(AreProgrammerHexDigitsEnabled))]
    [NotifyPropertyChangedFor(nameof(AreProgrammerEightAndNineEnabled))]
    [NotifyPropertyChangedFor(nameof(AreProgrammerTwoThroughSevenEnabled))]
    public partial CalculatorProgrammerRadix SelectedProgrammerRadix { get; private set; } = CalculatorProgrammerRadix.Decimal;

    [ObservableProperty]
    public partial CalculatorProgrammerWordSize SelectedProgrammerWordSize { get; private set; } = CalculatorProgrammerWordSize.Qword;

    [ObservableProperty]
    public partial bool IsProgrammerBitFlipMode { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgrammerArithmeticShift))]
    [NotifyPropertyChangedFor(nameof(IsProgrammerLogicalShift))]
    [NotifyPropertyChangedFor(nameof(IsProgrammerRotateShift))]
    [NotifyPropertyChangedFor(nameof(IsProgrammerRotateCarryShift))]
    public partial CalculatorProgrammerShiftMode SelectedProgrammerShiftMode { get; private set; } = CalculatorProgrammerShiftMode.Arithmetic;

    [ObservableProperty] public partial string ProgrammerHexDisplay { get; private set; } = "0";
    [ObservableProperty] public partial string ProgrammerDecimalDisplay { get; private set; } = "0";
    [ObservableProperty] public partial string ProgrammerOctalDisplay { get; private set; } = "0";
    [ObservableProperty] public partial string ProgrammerBinaryDisplay { get; private set; } = "0";

    public bool IsProgrammerHexadecimal => SelectedProgrammerRadix == CalculatorProgrammerRadix.Hexadecimal;
    public bool IsProgrammerDecimal => SelectedProgrammerRadix == CalculatorProgrammerRadix.Decimal;
    public bool IsProgrammerOctal => SelectedProgrammerRadix == CalculatorProgrammerRadix.Octal;
    public bool IsProgrammerBinary => SelectedProgrammerRadix == CalculatorProgrammerRadix.Binary;
    public bool IsProgrammerArithmeticShift => SelectedProgrammerShiftMode == CalculatorProgrammerShiftMode.Arithmetic;
    public bool IsProgrammerLogicalShift => SelectedProgrammerShiftMode == CalculatorProgrammerShiftMode.Logical;
    public bool IsProgrammerRotateShift => SelectedProgrammerShiftMode == CalculatorProgrammerShiftMode.Rotate;
    public bool IsProgrammerRotateCarryShift => SelectedProgrammerShiftMode == CalculatorProgrammerShiftMode.RotateCarry;
    public bool AreProgrammerHexDigitsEnabled => IsProgrammerHexadecimal && !IsError;
    public bool AreProgrammerEightAndNineEnabled => SelectedProgrammerRadix is CalculatorProgrammerRadix.Decimal or CalculatorProgrammerRadix.Hexadecimal && !IsError;
    public bool AreProgrammerTwoThroughSevenEnabled => SelectedProgrammerRadix != CalculatorProgrammerRadix.Binary && !IsError;
    public string ProgrammerWordSizeLabel => SelectedProgrammerWordSize.ToString().ToUpperInvariant();
    public ObservableCollection<CalculatorProgrammerBitGroup> ProgrammerBitGroups { get; } = [];






    public ObservableCollection<CalculatorNavigationItem> CalculatorNavigationItems { get; } = [];
    public ObservableCollection<CalculatorNavigationItem> ConverterNavigationItems { get; } = [];
    public string CalculatorGroupName { get; }
    public string SettingsName { get; }
    public string BackAutomationName { get; }
    public string TrigonometryName { get; }
    public string FunctionName { get; }
    public string BitwiseName { get; }
    public string BitShiftName { get; }
    public string ArithmeticShiftName { get; }
    public string LogicalShiftName { get; }
    public string RotateCircularShiftName { get; }
    public string RotateCarryShiftName { get; }
    public string SettingsBackTooltip { get; }
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
        var appResources = ResourceLoader.GetForViewIndependentUse();
        Settings = new SettingsViewModel(
            initialThemePreference,
            platformAppearance,
            supportsPlatformAppearanceSettings,
            availableFontFamilies ?? [],
            initialFontFamily,
            new SettingsStrings(
                appResources.GetString("SettingsAppearance.Text"),
                appResources.GetString("AppThemeExpander.Header"),
                appResources.GetString("AppThemeExpander.Description"),
                appResources.GetString("LightThemeRadioButton.Content"),
                appResources.GetString("DarkThemeRadioButton.Content"),
                appResources.GetString("SystemThemeRadioButton.Content"),
                appResources.GetString("AboutGroupTitle.Text"),
                appResources.GetString("AboutEULA.Text"),
                appResources.GetString("AboutControlServicesAgreement.Text"),
                appResources.GetString("AboutControlPrivacyStatement.Text"),
                appResources.GetString("FeedbackButton.Content")));
        Settings.PropertyChanged += (_, _) =>
        {
            // The window chrome predicates below are derived from settings, so
            // they have to be re-raised when a preference changes.
            OnPropertyChanged(nameof(UsesNativeWindowGeometry));
            OnPropertyChanged(nameof(UsesSquareWindowCorners));
            OnPropertyChanged(nameof(UsesWindowsWindowControls));
            OnPropertyChanged(nameof(UsesMacOSWindowControls));
            OnPropertyChanged(nameof(UsesCustomResizeHandles));
            OnPropertyChanged(nameof(ShowsSettingsBackInTitleBar));
            OnPropertyChanged(nameof(ShowsWindowsAlwaysOnTopExit));
            OnPropertyChanged(nameof(ShowsMacOSAlwaysOnTopExit));
            OnPropertyChanged(nameof(ShowsWindowsMaximizeButton));
            OnPropertyChanged(nameof(WindowCornerRadius));
        };
        TitleBarApplicationName = appResources.GetString("AppName");
        ModeDisplayName = appResources.GetString("StandardModeText");
        CalculatorGroupName = appResources.GetString("CalculatorModeTextCaps");
        SettingsName = appResources.GetString("SettingsHeader.Text");
        BackAutomationName = appResources.GetString("TitleBarBackButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        TrigonometryName = appResources.GetString("trigButton.Text");
        FunctionName = appResources.GetString("funcButton.Text");
        BitwiseName = appResources.GetString("bitwiseButton.Text");
        BitShiftName = appResources.GetString("bitShiftButton.Text");
        ArithmeticShiftName = appResources.GetString("arithmeticShiftButton.Content");
        LogicalShiftName = appResources.GetString("logicalShiftButton.Content");
        RotateCircularShiftName = appResources.GetString("rotateCircularButton.Content");
        RotateCarryShiftName = appResources.GetString("rotateCarryShiftButton.Content");
        SettingsBackTooltip = appResources.GetString("AboutControlBackButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        EnterAlwaysOnTopTooltip = appResources.GetString("EnterAlwaysOnTopButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        ExitAlwaysOnTopTooltip = appResources.GetString("ExitAlwaysOnTopButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        EnterAlwaysOnTopAutomationName = appResources.GetString("EnterAlwaysOnTopButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        ExitAlwaysOnTopAutomationName = appResources.GetString("ExitAlwaysOnTopButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        _calculator = new NativeCalculator(ResourceLoader.GetForViewIndependentUse("CEngineStrings"), numberFormat);
        Memory = new MemoryViewModel(
            _calculator,
            Synchronize,
            new MemoryStrings(
                appResources.GetString("MemoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("ClearMemoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("memButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("MemRecall/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("MemPlus/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("MemMinus/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("ClearMemoryItemButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name"),
                appResources.GetString("MemPlusItem/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name"),
                appResources.GetString("MemMinusItem/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name")));
        History = new HistoryViewModel(
            _calculator,
            () => IsScientificNotation,
            Synchronize,
            new HistoryStrings(
                appResources.GetString("HistoryLabel/Text"),
                appResources.GetString("HistoryEmpty/Text"),
                appResources.GetString("ClearHistory/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("HistoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("DeleteHistoryMenuItem/Text")));
        History.PropertyChanged += (_, _) => NotifyHistoryVisibilityChanged();
        BuildProgrammerBitGroups();
        var regionCode = GetCurrentRegionCode();
        Converter = new UnitConverterViewModel(
            new NativeUnitConverter(appResources, regionCode, numberFormat),
            appResources.GetString("ConverterModeTextCaps"));
        Converter.CategorySelected += category =>
        {
            // Choosing a category is a mode change, so the shell owns what
            // follows from it.
            ModeDisplayName = category.Name;
            CurrentViewMode = (CalculatorViewMode)category.Id;
            SetSelectedNavigationItem(CurrentViewMode);
        };
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
        if (IsCalculatorMode && IsError)
        {
            // UWP first clears the engine for every command received while in
            // error, then forwards only operands. This is why digits and the
            // decimal separator recover immediately while Backspace/Equals
            // merely clear the error display.
            _calculator.SendCommand(CalculatorCommand.Clear);
            if (!IsErrorRecoverable(command))
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

    private static bool IsErrorRecoverable(CalculatorCommand command) =>
        command is >= CalculatorCommand.Zero and <= CalculatorCommand.F
            or CalculatorCommand.Decimal
            || (int)command is >= 700 and <= 763;

    [RelayCommand]
    private void SelectProgrammerRadix(string radixName)
    {
        var radix = Enum.Parse<CalculatorProgrammerRadix>(radixName, ignoreCase: false);
        SelectedProgrammerRadix = radix;
        _calculator.SendCommand(radix switch
        {
            CalculatorProgrammerRadix.Hexadecimal => CalculatorCommand.Hex,
            CalculatorProgrammerRadix.Decimal => CalculatorCommand.Dec,
            CalculatorProgrammerRadix.Octal => CalculatorCommand.Oct,
            _ => CalculatorCommand.Bin,
        });
        Synchronize();
    }

    [RelayCommand]
    private void CycleProgrammerWordSize()
    {
        SelectProgrammerWordSize((SelectedProgrammerWordSize switch
        {
            CalculatorProgrammerWordSize.Qword => CalculatorProgrammerWordSize.Dword,
            CalculatorProgrammerWordSize.Dword => CalculatorProgrammerWordSize.Word,
            CalculatorProgrammerWordSize.Word => CalculatorProgrammerWordSize.Byte,
            _ => CalculatorProgrammerWordSize.Qword,
        }).ToString());
    }

    [RelayCommand]
    private void SelectProgrammerWordSize(string wordSizeName)
    {
        SelectedProgrammerWordSize = Enum.Parse<CalculatorProgrammerWordSize>(wordSizeName, ignoreCase: false);
        _calculator.SendCommand(SelectedProgrammerWordSize switch
        {
            CalculatorProgrammerWordSize.Qword => CalculatorCommand.Qword,
            CalculatorProgrammerWordSize.Dword => CalculatorCommand.Dword,
            CalculatorProgrammerWordSize.Word => CalculatorCommand.Word,
            _ => CalculatorCommand.Byte,
        });
        OnPropertyChanged(nameof(ProgrammerWordSizeLabel));
        Synchronize();
    }

    [RelayCommand]
    private void ToggleProgrammerBitFlip() => IsProgrammerBitFlipMode = !IsProgrammerBitFlipMode;

    [RelayCommand]
    private void SelectProgrammerShiftMode(string modeName) =>
        SelectedProgrammerShiftMode = Enum.Parse<CalculatorProgrammerShiftMode>(modeName, ignoreCase: false);

    [RelayCommand]
    private void ExecuteProgrammerLeftShift()
    {
        ExecuteCalculatorCommand(SelectedProgrammerShiftMode switch
        {
            CalculatorProgrammerShiftMode.Rotate => CalculatorCommand.RotateLeft,
            CalculatorProgrammerShiftMode.RotateCarry => CalculatorCommand.RotateLeftCarry,
            _ => CalculatorCommand.LeftShift,
        });
    }

    [RelayCommand]
    private void ExecuteProgrammerRightShift()
    {
        ExecuteCalculatorCommand(SelectedProgrammerShiftMode switch
        {
            CalculatorProgrammerShiftMode.Logical => CalculatorCommand.LogicalRightShift,
            CalculatorProgrammerShiftMode.Rotate => CalculatorCommand.RotateRight,
            CalculatorProgrammerShiftMode.RotateCarry => CalculatorCommand.RotateRightCarry,
            _ => CalculatorCommand.RightShift,
        });
    }

    [RelayCommand]
    private void FlipProgrammerBit(CalculatorProgrammerBit? bit)
    {
        if (bit is null || !bit.IsEnabled || IsError)
        {
            return;
        }
        _calculator.SendCommand((CalculatorCommand)(700 + bit.Index));
        Synchronize();
    }

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
        var isProgrammer = IsProgrammerMode;
        if (normalized.Any(character =>
                !char.IsWhiteSpace(character)
                && !IsValidPasteDigit(character, isProgrammer)
                && character is not '+' and not '-' and not '*' and not '/' and not '='
                && (!isProgrammer && character is not '.' and not ',')
                && (!(isScientific || isProgrammer) || character is not '%' and not '(' and not ')')
                && (!isScientific || character is not '^' and not 'e' and not 'E')))
        {
            return false;
        }
        if (isScientific || isProgrammer)
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
        else if (isProgrammer)
        {
            _calculator.SetMode(CalculatorMode.Programmer);
            _calculator.SendCommand(SelectedProgrammerRadix switch
            {
                CalculatorProgrammerRadix.Hexadecimal => CalculatorCommand.Hex,
                CalculatorProgrammerRadix.Decimal => CalculatorCommand.Dec,
                CalculatorProgrammerRadix.Octal => CalculatorCommand.Oct,
                _ => CalculatorCommand.Bin,
            });
            _calculator.SendCommand(SelectedProgrammerWordSize switch
            {
                CalculatorProgrammerWordSize.Qword => CalculatorCommand.Qword,
                CalculatorProgrammerWordSize.Dword => CalculatorCommand.Dword,
                CalculatorProgrammerWordSize.Word => CalculatorCommand.Word,
                _ => CalculatorCommand.Byte,
            });
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
                'a' or 'A' when isProgrammer => CalculatorCommand.A,
                'b' or 'B' when isProgrammer => CalculatorCommand.B,
                'c' or 'C' when isProgrammer => CalculatorCommand.C,
                'd' or 'D' when isProgrammer => CalculatorCommand.D,
                'e' or 'E' when isProgrammer => CalculatorCommand.E,
                'f' or 'F' when isProgrammer => CalculatorCommand.F,
                '.' or ',' when !isProgrammer => CalculatorCommand.Decimal,
                '+' => CalculatorCommand.Add,
                '-' => CalculatorCommand.Subtract,
                '*' => CalculatorCommand.Multiply,
                '/' => CalculatorCommand.Divide,
                '=' => CalculatorCommand.Equals,
                '^' when isScientific => CalculatorCommand.Power,
                '%' when isScientific || isProgrammer => CalculatorCommand.Modulo,
                '(' when isScientific || isProgrammer => CalculatorCommand.OpenParenthesis,
                ')' when isScientific || isProgrammer => CalculatorCommand.CloseParenthesis,
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

    private bool IsValidPasteDigit(char character, bool isProgrammer)
    {
        if (!isProgrammer)
        {
            return char.IsDigit(character);
        }

        var value = character switch
        {
            >= '0' and <= '9' => character - '0',
            >= 'a' and <= 'f' => character - 'a' + 10,
            >= 'A' and <= 'F' => character - 'A' + 10,
            _ => -1,
        };
        return value >= 0 && value < (int)SelectedProgrammerRadix;
    }

    [RelayCommand]
    private void Reset()
    {
        _calculator.Reset();
        Synchronize();
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
    private async Task SelectNavigationItem(CalculatorNavigationItem? item)
    {
        if (item is null || !item.IsEnabled)
        {
            return;
        }

        CurrentViewMode = item.Mode;
        History.CloseOverlay();
        ModeDisplayName = item.Name;
        SetSelectedNavigationItem(item.Mode);

        if (item.Mode is CalculatorViewMode.Standard or CalculatorViewMode.Scientific or CalculatorViewMode.Programmer)
        {
            if (item.Mode == CalculatorViewMode.Standard)
            {
                IsScientificNotation = false;
                IsScientificInverse = false;
                IsTrigInverse = false;
                IsTrigHyperbolic = false;
            }
            _calculator.SetMode(item.Mode switch
            {
                CalculatorViewMode.Scientific => CalculatorMode.Scientific,
                CalculatorViewMode.Programmer => CalculatorMode.Programmer,
                _ => CalculatorMode.Standard,
            });
            if (item.Mode == CalculatorViewMode.Programmer)
            {
                SelectedProgrammerRadix = CalculatorProgrammerRadix.Decimal;
                SelectedProgrammerWordSize = CalculatorProgrammerWordSize.Qword;
                IsProgrammerBitFlipMode = false;
                SelectedProgrammerShiftMode = CalculatorProgrammerShiftMode.Arithmetic;
                OnPropertyChanged(nameof(ProgrammerWordSizeLabel));
            }
            Synchronize();
        }

        if (item.Group == CalculatorNavigationGroup.Converter)
        {
            Converter.SelectCategoryForMode((int)item.Mode);
        }

        await SetNavigationPaneOpenAsync(false);
    }



    public void Dispose()
    {
        _calculator.Dispose();
        Converter.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The combined visibility rules mix history state with mode and compact
    /// overlay, so they live here and have to be re-raised whenever the child
    /// changes on its own.
    /// </summary>
    private void NotifyHistoryVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsHistoryPaneVisible));
        OnPropertyChanged(nameof(IsDockedHistoryPaneVisible));
        OnPropertyChanged(nameof(IsNarrowHistoryPaneVisible));
        OnPropertyChanged(nameof(IsHistoryButtonVisible));
    }

    private void Synchronize()
    {
        PrimaryDisplay = _calculator.PrimaryDisplay;
        ExpressionDisplay = _calculator.ExpressionDisplay;
        IsError = _calculator.IsError;
        IsInputEmpty = _calculator.IsInputEmpty;
        OpenParenthesisCount = _calculator.EventState.ParenthesisCount;

        History.Refresh(_calculator.History);
        Memory.Refresh(_calculator.MemoryValues.Select((value, index) =>
            new CalculatorMemoryEntry(checked((nuint)index), value)));

        if (IsProgrammerMode)
        {
            SynchronizeProgrammer();
        }
    }

    private void SynchronizeProgrammer()
    {
        if (!IsError)
        {
            ProgrammerHexDisplay = _calculator.GetResultForRadix(16);
            ProgrammerDecimalDisplay = _calculator.GetResultForRadix(10);
            ProgrammerOctalDisplay = _calculator.GetResultForRadix(8);
            ProgrammerBinaryDisplay = _calculator.GetResultForRadix(2);
            if (IsProgrammerBinary && ProgrammerBinaryDisplay != "0")
            {
                var binaryDigitCount = ProgrammerBinaryDisplay.Count(character => character is '0' or '1');
                var padding = (4 - binaryDigitCount % 4) % 4;
                ProgrammerBinaryDisplay = new string('0', padding) + ProgrammerBinaryDisplay;
            }
        }
        else
        {
            ProgrammerHexDisplay = ProgrammerDecimalDisplay = ProgrammerOctalDisplay = ProgrammerBinaryDisplay = PrimaryDisplay;
        }

        var rawBinary = IsError ? string.Empty : _calculator.GetResultForRadix(2, 64, false);
        rawBinary = new string(rawBinary.Where(character => character is '0' or '1').ToArray());
        var width = (int)SelectedProgrammerWordSize;
        foreach (var group in ProgrammerBitGroups)
        {
            foreach (var bit in group.Bits)
            {
                bit.IsEnabled = bit.Index < width && !IsError;
                var sourceIndex = rawBinary.Length - 1 - bit.Index;
                bit.IsSet = sourceIndex >= 0 && rawBinary[sourceIndex] == '1';
            }
        }
        OnPropertyChanged(nameof(AreProgrammerHexDigitsEnabled));
        OnPropertyChanged(nameof(AreProgrammerEightAndNineEnabled));
        OnPropertyChanged(nameof(AreProgrammerTwoThroughSevenEnabled));
    }

    private void BuildProgrammerBitGroups()
    {
        for (var highBit = 63; highBit >= 3; highBit -= 4)
        {
            var bits = new ObservableCollection<CalculatorProgrammerBit>();
            for (var bit = highBit; bit > highBit - 4; bit--)
            {
                bits.Add(new CalculatorProgrammerBit(bit));
            }
            ProgrammerBitGroups.Add(new CalculatorProgrammerBitGroup((highBit - 3).ToString(CultureInfo.InvariantCulture), bits));
        }
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
            resources.GetString("ProgrammerModeText"), "\uECCE", true));
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

}
