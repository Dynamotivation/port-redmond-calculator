using Avalonia.Controls;

namespace Calculator.Avalonia.Views;

/// <summary>
/// Date difference and add/subtract page. Calendar presentation belongs here;
/// all arithmetic and localized result formatting remain in the managed model.
/// </summary>
public partial class DateCalculatorView : UserControl
{
    public DateCalculatorView() => InitializeComponent();

    public void FocusDefault() => CalculationModeSelector.Focus();
}
