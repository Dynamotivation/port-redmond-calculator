using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    public ObservableCollection<string> History { get; } = [];

    public CalculatorViewModel()
    {
        var resourcePath = Path.Combine(AppContext.BaseDirectory, "CEngineStrings.resw");
        _calculator = new NativeCalculator(resourcePath);
        PrimaryDisplay = _calculator.PrimaryDisplay;
        ExpressionDisplay = _calculator.ExpressionDisplay;
    }

    [RelayCommand]
    private void SendCommand(string commandName)
    {
        var command = Enum.Parse<CalculatorCommand>(commandName, ignoreCase: false);
        _calculator.SendCommand(command);
        UpdateDisplay();

        if (command == CalculatorCommand.Equals && !IsError)
        {
            History.Insert(0, string.IsNullOrWhiteSpace(ExpressionDisplay) ? PrimaryDisplay : $"{ExpressionDisplay}  {PrimaryDisplay}");
            while (History.Count > 100)
            {
                History.RemoveAt(History.Count - 1);
            }
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _calculator.Reset();
        UpdateDisplay();
        History.Clear();
    }

    public void Dispose()
    {
        _calculator.Dispose();
        GC.SuppressFinalize(this);
    }

    private void UpdateDisplay()
    {
        PrimaryDisplay = _calculator.PrimaryDisplay;
        ExpressionDisplay = _calculator.ExpressionDisplay;
        IsError = _calculator.IsError;
    }
}
