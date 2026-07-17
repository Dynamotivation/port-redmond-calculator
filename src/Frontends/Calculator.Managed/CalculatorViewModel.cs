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
    public partial bool HasMemory { get; private set; }

    public ObservableCollection<string> History { get; } = [];
    public ObservableCollection<string> Memory { get; } = [];
    public string ApplicationName { get; } = "Redmond Calculator";
    [ObservableProperty]
    public partial string ModeDisplayName { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStandardMode))]
    [NotifyPropertyChangedFor(nameof(IsUnitConverterMode))]
    public partial CalculatorViewMode CurrentViewMode { get; private set; } = CalculatorViewMode.Standard;

    [ObservableProperty]
    public partial bool IsNavigationPaneOpen { get; private set; }

    [ObservableProperty]
    public partial bool IsSettingsOpen { get; private set; }

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

    public bool SupportsPlatformAppearanceSettings { get; }
    public string PlatformAppearanceName { get; } = "macOS appearance";
    public string MicaEffectName { get; } = "Translucent background";
    public string MicaEffectDescription { get; } = "Blur the desktop behind the calculator window";
    public string WindowCornersName { get; } = "Window corners";
    public string WindowCornersDescription { get; } = "Choose the outer window shape";
    public string Windows10CornersName { get; } = "Windows 10 — square";
    public string Windows11CornersName { get; } = "Windows 11 — rounded";
    public string MacOSCornersName { get; } = "macOS — rounded";
    public string WindowControlsName { get; } = "Title bar controls";
    public string WindowControlsDescription { get; } = "Choose the window button style";
    public string WindowsWindowControlsName { get; } = "Windows";
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
    [NotifyPropertyChangedFor(nameof(IsWindowsWindowControlStyleSelected))]
    [NotifyPropertyChangedFor(nameof(IsMacOSWindowControlStyleSelected))]
    public partial WindowControlStyle SelectedWindowControlStyle { get; private set; } = WindowControlStyle.Windows;

    public bool IsWindows10CornerStyleSelected => SelectedWindowCornerStyle == WindowCornerStyle.Windows10;
    public bool IsWindows11CornerStyleSelected => SelectedWindowCornerStyle == WindowCornerStyle.Windows11;
    public bool IsMacOSCornerStyleSelected => SelectedWindowCornerStyle == WindowCornerStyle.MacOS;
    public bool IsWindowsWindowControlStyleSelected => SelectedWindowControlStyle == WindowControlStyle.Windows;
    public bool IsMacOSWindowControlStyleSelected => SelectedWindowControlStyle == WindowControlStyle.MacOS;
    public bool UsesNativeWindowGeometry => SelectedWindowCornerStyle == WindowCornerStyle.MacOS;
    public bool UsesSquareWindowCorners => SelectedWindowCornerStyle == WindowCornerStyle.Windows10;
    public bool UsesWindowsWindowControls => SelectedWindowControlStyle == WindowControlStyle.Windows;
    public bool UsesMacOSWindowControls => SelectedWindowControlStyle == WindowControlStyle.MacOS;
    public double WindowCornerRadius => SelectedWindowCornerStyle == WindowCornerStyle.Windows11 ? 8 : 0;
    public bool UsesCustomResizeHandles => !UsesNativeWindowGeometry;
    public event Action<PlatformAppearancePreferences>? PlatformAppearancePreferencesChanged;

    public bool IsStandardMode => CurrentViewMode == CalculatorViewMode.Standard;
    public bool IsUnitConverterMode => CurrentViewMode is >= CalculatorViewMode.Volume and <= CalculatorViewMode.Angle;

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

    public CalculatorViewModel(
        AppThemePreference initialThemePreference = AppThemePreference.Dark,
        PlatformAppearancePreferences? initialPlatformAppearance = null,
        bool supportsPlatformAppearanceSettings = false)
    {
        var platformAppearance = initialPlatformAppearance ?? new PlatformAppearancePreferences();
        SelectedThemePreference = initialThemePreference;
        UseMicaEffect = platformAppearance.UseMicaEffect;
        SelectedWindowCornerStyle = platformAppearance.WindowCornerStyle;
        SelectedWindowControlStyle = platformAppearance.WindowControlStyle;
        SupportsPlatformAppearanceSettings = supportsPlatformAppearanceSettings;
        var appResources = ResourceLoader.GetForViewIndependentUse();
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
        _calculator = new NativeCalculator(ResourceLoader.GetForViewIndependentUse("CEngineStrings"));
        var regionCode = GetCurrentRegionCode();
        _unitConverter = new NativeUnitConverter(appResources, regionCode);
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
        var command = Enum.Parse<CalculatorCommand>(commandName, ignoreCase: false);
        _calculator.SendCommand(command);
        Synchronize();
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
        ModeDisplayName = item.Name;
        SetSelectedNavigationItem(item.Mode);

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

        Replace(History, _calculator.History.Select(entry => $"{entry.Expression}  {entry.Result}"));
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
            resources.GetString("ScientificModeText"), "\uF196", false));
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
