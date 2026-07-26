using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

/// <summary>
/// Scientific-only presentation state: the angle unit, the 2nd and hyp
/// modifiers, and scientific notation.
/// </summary>
/// <remarks>
/// The engine still owns the arithmetic. What lives here is which variant of a
/// key the scientific keypad is currently offering — 2nd swaps square for cube,
/// hyp swaps sin for sinh — plus the angle unit and the F-E toggle. The shell
/// reads <see cref="IsInverse"/> when mapping a command and
/// <see cref="IsNotation"/> when recalling history, because those are decisions
/// about the shared session rather than about this view.
/// </remarks>
public sealed partial class ScientificViewModel : ObservableObject
{
    private readonly NativeCalculator _calculator;
    private readonly Action _synchronize;

    public ScientificViewModel(NativeCalculator calculator, Action synchronize, ScientificStrings strings)
    {
        _calculator = calculator;
        _synchronize = synchronize;
        Strings = strings;
    }

    public ScientificStrings Strings { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AngleLabel))]
    public partial CalculatorAngleMode SelectedAngle { get; private set; } = CalculatorAngleMode.Degrees;

    /// <summary>The 2nd modifier, which swaps the inverse operator column.</summary>
    [ObservableProperty]
    public partial bool IsInverse { get; set; }

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
    public partial bool IsNotation { get; private set; }

    public string AngleLabel => SelectedAngle switch
    {
        CalculatorAngleMode.Degrees => "DEG",
        CalculatorAngleMode.Radians => "RAD",
        _ => "GRAD",
    };

    public bool ShowsRegularTrigFunctions => !IsTrigInverse && !IsTrigHyperbolic;
    public bool ShowsInverseTrigFunctions => IsTrigInverse && !IsTrigHyperbolic;
    public bool ShowsHyperbolicTrigFunctions => !IsTrigInverse && IsTrigHyperbolic;
    public bool ShowsInverseHyperbolicTrigFunctions => IsTrigInverse && IsTrigHyperbolic;

    [RelayCommand]
    private void CycleAngle()
    {
        SelectedAngle = SelectedAngle switch
        {
            CalculatorAngleMode.Degrees => CalculatorAngleMode.Radians,
            CalculatorAngleMode.Radians => CalculatorAngleMode.Grads,
            _ => CalculatorAngleMode.Degrees,
        };
        _calculator.SendCommand(SelectedAngle switch
        {
            CalculatorAngleMode.Degrees => CalculatorCommand.Degree,
            CalculatorAngleMode.Radians => CalculatorCommand.Radian,
            _ => CalculatorCommand.Grads,
        });
        _synchronize();
    }

    [RelayCommand]
    private void ToggleNotation()
    {
        _calculator.SendCommand(CalculatorCommand.ScientificNotation);
        IsNotation = !IsNotation;
        _synchronize();
    }

    [RelayCommand]
    private void ToggleInverse() => IsInverse = !IsInverse;

    [RelayCommand]
    private void ToggleTrigInverseModifier() => IsTrigInverse = !IsTrigInverse;

    [RelayCommand]
    private void ToggleTrigHyperbolicModifier() => IsTrigHyperbolic = !IsTrigHyperbolic;

    /// <summary>
    /// Leaving for Standard drops every scientific modifier, matching the source
    /// application: returning to Scientific starts from the unmodified keypad.
    /// </summary>
    public void ResetModifiers()
    {
        IsNotation = false;
        IsInverse = false;
        IsTrigInverse = false;
        IsTrigHyperbolic = false;
    }
}

/// <summary>Localized strings for the scientific flyout headers.</summary>
public sealed record ScientificStrings(string TrigonometryName, string FunctionName);
