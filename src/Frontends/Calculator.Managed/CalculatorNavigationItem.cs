using CommunityToolkit.Mvvm.ComponentModel;

namespace Calculator.Managed;

// Values intentionally match the original CalcViewModel/Common/NavCategory.h
// manifest and its persisted serialization IDs.
public enum CalculatorViewMode
{
    Standard = 0,
    Scientific = 1,
    Programmer = 2,
    Date = 3,
    Volume = 4,
    Length = 5,
    Weight = 6,
    Temperature = 7,
    Energy = 8,
    Area = 9,
    Speed = 10,
    Time = 11,
    Power = 12,
    Data = 13,
    Pressure = 14,
    Angle = 15,
    Currency = 16,
    Graphing = 17,
}

public enum CalculatorNavigationGroup
{
    Calculator,
    Converter,
}

public sealed partial class CalculatorNavigationItem(
    CalculatorViewMode mode,
    CalculatorNavigationGroup group,
    string name,
    string glyph,
    bool isEnabled,
    string accessKey = "") : ObservableObject
{
    public CalculatorViewMode Mode { get; } = mode;
    public CalculatorNavigationGroup Group { get; } = group;
    public string Name { get; } = name;
    public string Glyph { get; } = glyph;
    public bool IsEnabled { get; } = isEnabled;
    public string AccessKey { get; } = accessKey;
    public string AccessKeyText => string.IsNullOrEmpty(AccessKey) ? string.Empty : $"_{AccessKey}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
