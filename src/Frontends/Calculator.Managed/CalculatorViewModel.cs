using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.ApplicationModel.Resources;

namespace Calculator.Managed;

public partial class CalculatorViewModel : ObservableObject, IDisposable
{
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
    public string HistoryAutomationName { get; }

    public CalculatorViewModel()
    {
        var appResources = ResourceLoader.GetForViewIndependentUse();
        ModeDisplayName = appResources.GetString("StandardModeText");
        CalculatorGroupName = appResources.GetString("CalculatorModeTextCaps");
        ConverterGroupName = appResources.GetString("ConverterModeTextCaps");
        SettingsName = appResources.GetString("SettingsHeader.Text");
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
    private void ToggleNavigationPane()
    {
        IsNavigationPaneOpen = !IsNavigationPaneOpen;
    }

    [RelayCommand]
    private void CloseNavigationPane() => IsNavigationPaneOpen = false;

    [RelayCommand]
    private void SelectNavigationItem(CalculatorNavigationItem? item)
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

        IsNavigationPaneOpen = false;
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
}
