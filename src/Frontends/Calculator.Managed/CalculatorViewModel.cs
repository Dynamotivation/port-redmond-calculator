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
    public partial bool IsError { get; private set; }

    [ObservableProperty]
    public partial bool IsInputEmpty { get; private set; }


    public HistoryViewModel History { get; }

    public MemoryViewModel Memory { get; }

    public UnitConverterViewModel Converter { get; }

    public CurrencyConverterViewModel Currency { get; }

    public DateCalculatorViewModel DateCalculator { get; }

    public SettingsViewModel Settings { get; }

    public ScientificViewModel Scientific { get; }

    public ProgrammerViewModel Programmer { get; }

    public GraphingViewModel Graphing { get; }


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
    [NotifyPropertyChangedFor(nameof(IsGraphingMode))]
    [NotifyPropertyChangedFor(nameof(IsCalculatorMode))]
    [NotifyPropertyChangedFor(nameof(IsStandardOrScientificMode))]
    [NotifyPropertyChangedFor(nameof(IsUnitConverterMode))]
    [NotifyPropertyChangedFor(nameof(IsCurrencyMode))]
    [NotifyPropertyChangedFor(nameof(IsStaticUnitConverterMode))]
    [NotifyPropertyChangedFor(nameof(IsDateCalculatorMode))]
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


    private readonly WindowPlatformCapabilities _windowPlatformCapabilities;

    public bool UsesNativeWindowGeometry =>
        _windowPlatformCapabilities.SupportsMacOSWindowFeatures
        && Settings.SelectedWindowCornerStyle == WindowCornerStyle.MacOS;
    public bool UsesNativeWindowDecorations => _windowPlatformCapabilities.UsesNativeWindowDecorations;
    public bool UsesNativeWindowFrameGeometry => UsesNativeWindowDecorations || UsesNativeWindowGeometry;
    public bool UsesSquareWindowCorners => Settings.SelectedWindowCornerStyle == WindowCornerStyle.Windows10;
    public bool UsesMacOSWindowControls =>
        _windowPlatformCapabilities.SupportsMacOSWindowFeatures
        && Settings.SelectedWindowControlStyle == WindowControlStyle.MacOS;
    public bool UsesWindowsWindowControls => !UsesMacOSWindowControls;
    public bool ShowsWindowTitleBarContent => UsesNativeWindowDecorations || UsesWindowsWindowControls;
    public bool UsesCustomWindowControls => !UsesNativeWindowDecorations && UsesWindowsWindowControls;
    public bool ShowsSettingsBackInTitleBar => IsSettingsOpen && ShowsWindowTitleBarContent;
    public bool ShowsWindowsAlwaysOnTopExit => IsAlwaysOnTop && UsesWindowsWindowControls;
    public bool ShowsMacOSAlwaysOnTopExit => IsAlwaysOnTop && UsesMacOSWindowControls;
    public bool ShowsWindowsMaximizeButton => !IsAlwaysOnTop && UsesWindowsWindowControls;
    public bool ShowsCustomWindowsMaximizeButton => !IsAlwaysOnTop && UsesCustomWindowControls;
    public double WindowCornerRadius => Settings.SelectedWindowCornerStyle == WindowCornerStyle.Windows11 ? 8 : 0;
    public bool UsesCustomResizeHandles => !UsesNativeWindowDecorations && !UsesNativeWindowGeometry;

    public bool IsStandardMode => CurrentViewMode == CalculatorViewMode.Standard;
    public bool CanEnterAlwaysOnTop => IsStandardMode && !IsAlwaysOnTop;
    public bool IsScientificMode => CurrentViewMode == CalculatorViewMode.Scientific;
    public bool IsProgrammerMode => CurrentViewMode == CalculatorViewMode.Programmer;
    public bool IsGraphingMode => CurrentViewMode == CalculatorViewMode.Graphing;
    public bool IsCalculatorMode => IsStandardMode || IsScientificMode || IsProgrammerMode;
    public bool IsStandardOrScientificMode => IsStandardMode || IsScientificMode;
    public bool IsUnitConverterMode => CurrentViewMode is >= CalculatorViewMode.Volume and <= CalculatorViewMode.Currency;
    public bool IsCurrencyMode => CurrentViewMode == CalculatorViewMode.Currency;
    public bool IsStaticUnitConverterMode => CurrentViewMode is >= CalculatorViewMode.Volume and <= CalculatorViewMode.Angle;
    public bool IsDateCalculatorMode => CurrentViewMode == CalculatorViewMode.Date;

    [ObservableProperty]
    public partial uint OpenParenthesisCount { get; private set; }









    public ObservableCollection<CalculatorNavigationItem> CalculatorNavigationItems { get; } = [];
    public ObservableCollection<CalculatorNavigationItem> ConverterNavigationItems { get; } = [];
    public string CalculatorGroupName { get; }
    public string SettingsName { get; }
    public string BackAutomationName { get; }
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
        WindowPlatformCapabilities? windowPlatformCapabilities = null,
        CultureInfo? numberCulture = null,
        IEnumerable<string>? availableFontFamilies = null,
        string? initialFontFamily = null,
        IReadOnlyDictionary<string, CurrencyProviderPreference>? initialCurrencyProviderPreferences = null,
        Func<string, string, string>? shortcutTextRewriter = null)
    {
        var platformAppearance = initialPlatformAppearance ?? new PlatformAppearancePreferences();
        _windowPlatformCapabilities = windowPlatformCapabilities ?? new WindowPlatformCapabilities();
        var numberFormat = CalculatorNumberFormat.FromCulture(numberCulture);
        DecimalSeparator = numberFormat.DecimalSeparator;
        var appResources = ResourceLoader.GetForViewIndependentUse();
        string ShortcutText(string shortcutId, string resourceName)
        {
            var localizedText = appResources.GetString(resourceName);
            return shortcutTextRewriter?.Invoke(shortcutId, localizedText) ?? localizedText;
        }

        Settings = new SettingsViewModel(
            initialThemePreference,
            platformAppearance,
            _windowPlatformCapabilities.SupportsBackdropSettings,
            _windowPlatformCapabilities.SupportsWindowStyleSettings,
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
            OnPropertyChanged(nameof(UsesNativeWindowFrameGeometry));
            OnPropertyChanged(nameof(UsesSquareWindowCorners));
            OnPropertyChanged(nameof(UsesWindowsWindowControls));
            OnPropertyChanged(nameof(UsesMacOSWindowControls));
            OnPropertyChanged(nameof(ShowsWindowTitleBarContent));
            OnPropertyChanged(nameof(UsesCustomWindowControls));
            OnPropertyChanged(nameof(UsesCustomResizeHandles));
            OnPropertyChanged(nameof(ShowsSettingsBackInTitleBar));
            OnPropertyChanged(nameof(ShowsWindowsAlwaysOnTopExit));
            OnPropertyChanged(nameof(ShowsMacOSAlwaysOnTopExit));
            OnPropertyChanged(nameof(ShowsWindowsMaximizeButton));
            OnPropertyChanged(nameof(ShowsCustomWindowsMaximizeButton));
            OnPropertyChanged(nameof(WindowCornerRadius));
        };
        TitleBarApplicationName = appResources.GetString("AppName");
        ModeDisplayName = appResources.GetString("StandardModeText");
        CalculatorGroupName = appResources.GetString("CalculatorModeTextCaps");
        SettingsName = appResources.GetString("SettingsHeader.Text");
        BackAutomationName = appResources.GetString("TitleBarBackButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        SettingsBackTooltip = appResources.GetString("AboutControlBackButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        EnterAlwaysOnTopTooltip = ShortcutText(
            "window.alwaysOnTop.enter",
            "EnterAlwaysOnTopButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        ExitAlwaysOnTopTooltip = ShortcutText(
            "window.alwaysOnTop.exit",
            "ExitAlwaysOnTopButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip");
        EnterAlwaysOnTopAutomationName = appResources.GetString("EnterAlwaysOnTopButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        ExitAlwaysOnTopAutomationName = appResources.GetString("ExitAlwaysOnTopButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name");
        _calculator = new NativeCalculator(ResourceLoader.GetForViewIndependentUse("CEngineStrings"), numberFormat);
        Programmer = new ProgrammerViewModel(
            _calculator,
            Synchronize,
            ExecuteCalculatorCommand,
            new ProgrammerStrings(
                appResources.GetString("bitwiseButton.Text"),
                appResources.GetString("bitShiftButton.Text"),
                appResources.GetString("arithmeticShiftButton.Content"),
                appResources.GetString("logicalShiftButton.Content"),
                appResources.GetString("rotateCircularButton.Content"),
                appResources.GetString("rotateCarryShiftButton.Content")));
        Scientific = new ScientificViewModel(
            _calculator,
            Synchronize,
            new ScientificStrings(
                appResources.GetString("trigButton.Text"),
                appResources.GetString("funcButton.Text")));
        Memory = new MemoryViewModel(
            _calculator,
            Synchronize,
            new MemoryStrings(
                appResources.GetString("MemoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                ShortcutText(
                    "ClearMemoryButton",
                    "ClearMemoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                ShortcutText(
                    "memButton",
                    "memButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                ShortcutText(
                    "MemRecall",
                    "MemRecall/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                ShortcutText(
                    "MemPlus",
                    "MemPlus/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                ShortcutText(
                    "MemMinus",
                    "MemMinus/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("ClearMemoryItemButton/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name"),
                appResources.GetString("MemPlusItem/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name"),
                appResources.GetString("MemMinusItem/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name")));
        History = new HistoryViewModel(
            _calculator,
            () => Scientific.IsNotation,
            Synchronize,
            new HistoryStrings(
                appResources.GetString("HistoryLabel/Text"),
                appResources.GetString("HistoryEmpty/Text"),
                ShortcutText(
                    "ClearHistory",
                    "ClearHistory/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                ShortcutText(
                    "HistoryButton",
                    "HistoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("DeleteHistoryMenuItem/Text")));
        History.PropertyChanged += (_, _) => NotifyHistoryVisibilityChanged();
        Graphing = new GraphingViewModel(
            new GraphingStrings(
                appResources.GetString("mathRichEditBox.PlaceholderText"),
                appResources.GetString("EquationTextBoxAddPanel/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("VaiablesHeader.Text"),
                ShortcutText(
                    "graph.zoom.in",
                    "zoomInButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                ShortcutText(
                    "graph.zoom.out",
                    "zoomOutButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                ShortcutText(
                    "graph.view.reset",
                    "graphViewButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("GraphSwitchToEquationMode"),
                appResources.GetString("GraphSwitchToGraphMode"),
                appResources.GetString("enableTracingButtonToolTip"),
                appResources.GetString("disableTracingButtonToolTip"),
                appResources.GetString("GraphOptionsHeading.Text"),
                appResources.GetString("GridHeading.Text"),
                appResources.GetString("ResetViewButton.Content"),
                appResources.GetString("UnitsHeading.Text"),
                appResources.GetString("TrigModeRadians.Content"),
                appResources.GetString("TrigModeDegrees.Content"),
                appResources.GetString("TrigModeGradians.Content"),
                appResources.GetString("LineThicknessBoxHeading.Text"),
                appResources.GetString("GraphThemeHeading.Text"),
                appResources.GetString("AlwaysLightTheme.Content"),
                appResources.GetString("MatchAppTheme.Content"),
                appResources.GetString("trigButton.Text"),
                appResources.GetString("inequalityButton.Text"),
                appResources.GetString("funcButton.Text"),
                appResources.GetString("functionAnalysisButton.[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("colorChooserButton.[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("removeButton.[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("LineOptionsHeading.Text"),
                appResources.GetString("LineColorText.Text"),
                appResources.GetString("StyleChooserBoxHeading.Text"),
                appResources.GetString("GraphSettingsXMin.Header"),
                appResources.GetString("GraphSettingsXMax.Header"),
                appResources.GetString("GraphSettingsYMin.Header"),
                appResources.GetString("GraphSettingsYMax.Header"),
                appResources.GetString("MinTextBlock.Text"),
                appResources.GetString("StepTextBlock.Text"),
                appResources.GetString("MaxTextBlock.Text"),
                appResources.GetString("VariableAreaSettings.[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("SmallLineWidthAutomationName"),
                appResources.GetString("MediumLineWidthAutomationName"),
                appResources.GetString("LargeLineWidthAutomationName"),
                appResources.GetString("ExtraLargeLineWidthAutomationName"),
                appResources.GetString("KeyGraphFeaturesLabel/Text"),
                appResources.GetString("equationAnalysisBack/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip"),
                appResources.GetString("KGFEquationTextBox/[using:Windows.UI.Xaml.Automation]AutomationProperties/Name"),
                appResources.GetString("KGFAnalysisNotSupported"),
                appResources.GetString("KGFVariableIsNotX"),
                appResources.GetString("KGFAnalysisCouldNotBePerformed"),
                appResources.GetString("Domain"),
                appResources.GetString("Range"),
                appResources.GetString("XIntercept"),
                appResources.GetString("YIntercept"),
                appResources.GetString("Minima"),
                appResources.GetString("Maxima"),
                appResources.GetString("InflectionPoints"),
                appResources.GetString("VerticalAsymptotes"),
                appResources.GetString("HorizontalAsymptotes"),
                appResources.GetString("ObliqueAsymptotes"),
                appResources.GetString("Parity"),
                appResources.GetString("Monotonicity"),
                appResources.GetString("KGFRangeNone"),
                appResources.GetString("KGFMinimaNone"),
                appResources.GetString("KGFMaximaNone"),
                appResources.GetString("KGFInflectionPointsNone"),
                appResources.GetString("KGFVerticalAsymptotesNone"),
                appResources.GetString("KGFHorizontalAsymptotesNone"),
                appResources.GetString("KGFObliqueAsymptotesNone"),
                appResources.GetString("KGFMonotonicityError"),
                appResources.GetString("KGFTooComplexFeaturesError"),
                appResources.GetString("cutEquationMenuItem.Text"),
                appResources.GetString("copyEquationMenuItem.Text"),
                appResources.GetString("pasteEquationMenuItem.Text"),
                appResources.GetString("undoEquationMenuItem.Text"),
                appResources.GetString("selectAllEquationMenuItem.Text"),
                appResources.GetString("UnexpectedEndOfExpression"),
                ShortcutText(
                    "graph.view.reset",
                    "GraphViewAutomaticBestFitAnnouncement")));
        DateCalculator = new DateCalculatorViewModel(
            new DateCalculatorStrings(
                appResources.GetString("Date_DifferenceOption.Content"),
                appResources.GetString("Date_AddSubtractOption.Content"),
                appResources.GetString("DateCalculationOption.[using:Windows.UI.Xaml.Automation]AutomationProperties.Name"),
                appResources.GetString("DateDiff_FromHeader.Header"),
                appResources.GetString("DateDiff_ToHeader.Header"),
                appResources.GetString("Date_DifferenceLabel.Text"),
                appResources.GetString("AddOption.Content"),
                appResources.GetString("SubtractOption.Content"),
                appResources.GetString("YearsLabel.Text"),
                appResources.GetString("MonthsLabel.Text"),
                appResources.GetString("DaysLabel.Text"),
                appResources.GetString("DateLabel.Text"),
                appResources.GetString("Date_SameDates"),
                appResources.GetString("Date_Day"),
                appResources.GetString("Date_Days"),
                appResources.GetString("Date_Week"),
                appResources.GetString("Date_Weeks"),
                appResources.GetString("Date_Month"),
                appResources.GetString("Date_Months"),
                appResources.GetString("Date_Year"),
                appResources.GetString("Date_Years"),
                appResources.GetString("Date_OutOfBoundMessage"),
                appResources.GetString("CalculationFailed"),
                appResources.GetString("Date_DifferenceResultAutomationName"),
                appResources.GetString("Date_ResultingDateAutomationName")),
            numberCulture);
        var regionCode = GetCurrentRegionCode();
        Currency = new CurrencyConverterViewModel(
            numberFormat,
            CurrencyProviderCatalog.Create(initialCurrencyProviderPreferences));
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
                Scientific.ResetModifiers();
                Synchronize();
                return;
            }
        }

        _calculator.SendCommand(command);
        if (command is CalculatorCommand.Clear or CalculatorCommand.ClearEntry)
        {
            Scientific.ResetModifiers();
        }
        if (Scientific.IsInverse && command is CalculatorCommand.Cube or CalculatorCommand.CubeRoot
            or CalculatorCommand.Root or CalculatorCommand.TwoPowerX
            or CalculatorCommand.LogBaseY or CalculatorCommand.EPowerX)
        {
            Scientific.IsInverse = false;
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
            Scientific.IsTrigInverse = false;
            Scientific.IsTrigHyperbolic = false;
        }
        Synchronize();
    }

    private static bool IsErrorRecoverable(CalculatorCommand command) =>
        command is >= CalculatorCommand.Zero and <= CalculatorCommand.F
            or CalculatorCommand.Decimal
            || (int)command is >= 700 and <= 763;














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
            _calculator.SendCommand(Scientific.SelectedAngle switch
            {
                CalculatorAngleMode.Degrees => CalculatorCommand.Degree,
                CalculatorAngleMode.Radians => CalculatorCommand.Radian,
                _ => CalculatorCommand.Grads,
            });
            if (Scientific.IsNotation)
            {
                _calculator.SendCommand(CalculatorCommand.ScientificNotation);
            }
        }
        else if (isProgrammer)
        {
            _calculator.SetMode(CalculatorMode.Programmer);
            Programmer.ApplyRadixAndWordSize();
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
        return value >= 0 && value < (int)Programmer.SelectedRadix;
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
        if (item.Mode == CalculatorViewMode.Currency)
        {
            _ = Currency.ActivateAsync();
        }
        else
        {
            Currency.Deactivate();
        }
        IsSettingsOpen = false;
        History.CloseOverlay();
        ModeDisplayName = item.Name;
        SetSelectedNavigationItem(item.Mode);

        if (item.Mode is CalculatorViewMode.Standard or CalculatorViewMode.Scientific or CalculatorViewMode.Programmer)
        {
            if (item.Mode == CalculatorViewMode.Standard)
            {
                Scientific.ResetModifiers();
            }
            _calculator.SetMode(item.Mode switch
            {
                CalculatorViewMode.Scientific => CalculatorMode.Scientific,
                CalculatorViewMode.Programmer => CalculatorMode.Programmer,
                _ => CalculatorMode.Standard,
            });
            if (item.Mode == CalculatorViewMode.Programmer)
            {
                Programmer.ResetForModeEntry();
            }
            Synchronize();
        }

        if (item.Group == CalculatorNavigationGroup.Converter && item.Mode != CalculatorViewMode.Currency)
        {
            Converter.SelectCategoryForMode((int)item.Mode);
        }

        await SetNavigationPaneOpenAsync(false);
    }

    /// <summary>
    /// Selects an enabled shell destination from a keyboard accelerator.
    /// Keeping lookup and enabled-state enforcement here makes keyboard and
    /// pointer navigation pass through the same command path.
    /// </summary>
    public bool TrySelectNavigationMode(CalculatorViewMode mode)
    {
        var item = CalculatorNavigationItems
            .Concat(ConverterNavigationItems)
            .FirstOrDefault(candidate => candidate.Mode == mode);
        if (item is null || !item.IsEnabled)
        {
            return false;
        }

        SelectNavigationItemCommand.Execute(item);
        return true;
    }

    [RelayCommand]
    private void SendConverterCommand(string commandName)
    {
        if (IsCurrencyMode)
        {
            Currency.SendCommand(commandName);
        }
        else
        {
            Converter.SendCommandCommand.Execute(commandName);
        }
    }

    [RelayCommand]
    private void SwapConverter()
    {
        if (IsCurrencyMode)
        {
            Currency.Swap();
        }
        else
        {
            Converter.SwapCommand.Execute(null);
        }
    }

    public bool TryDispatchConverterShortcut(string shortcutId) =>
        IsCurrencyMode
            ? Currency.TryDispatchShortcut(shortcutId)
            : Converter.TryDispatchShortcut(shortcutId);

    public bool TryPasteConverter(string? text) =>
        IsCurrencyMode
            ? Currency.TryPaste(text)
            : Converter.TryPaste(text, DecimalSeparator);



    public void Dispose()
    {
        _calculator.Dispose();
        Currency.Dispose();
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
            Programmer.Refresh(IsError, PrimaryDisplay);
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
            resources.GetString("GraphingCalculatorModeText"), "\uF770", true));
        CalculatorNavigationItems.Add(new(CalculatorViewMode.Programmer, CalculatorNavigationGroup.Calculator,
            resources.GetString("ProgrammerModeText"), "\uECCE", true));
        CalculatorNavigationItems.Add(new(CalculatorViewMode.Date, CalculatorNavigationGroup.Calculator,
            resources.GetString("DateCalculationModeText"), "\uE787", true));

        AddConverterNavigationItem(resources, CalculatorViewMode.Currency, "CategoryName_CurrencyText", "\uEB0D");
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
        var accessKeyResource = resourceKey.Replace("Text", "AccessKey", StringComparison.Ordinal);
        ConverterNavigationItems.Add(new(
            mode,
            CalculatorNavigationGroup.Converter,
            resources.GetString(resourceKey),
            glyph,
            isEnabled,
            resources.GetString(accessKeyResource)));
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
