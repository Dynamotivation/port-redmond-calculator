using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.ApplicationModel.Resources;

namespace Calculator.Managed;

public partial class CalculatorViewModel : ObservableObject, IDisposable
{
    private readonly NativeCalculator _calculator;

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
    public string ModeDisplayName { get; }
    public string HistoryAutomationName { get; }

    public CalculatorViewModel()
    {
        var appResources = ResourceLoader.GetForViewIndependentUse();
        ModeDisplayName = appResources.GetString("StandardModeText");
        HistoryAutomationName = appResources.GetString("HistoryLabel/Text");
        _calculator = new NativeCalculator(ResourceLoader.GetForViewIndependentUse("CEngineStrings"));
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

    public void Dispose()
    {
        _calculator.Dispose();
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
}
