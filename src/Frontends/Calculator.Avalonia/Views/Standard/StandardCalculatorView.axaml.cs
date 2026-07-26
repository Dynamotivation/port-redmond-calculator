using Avalonia.Controls;
using Calculator.Avalonia.Controls;

namespace Calculator.Avalonia.Views;

/// <summary>
/// The Standard keypad.
/// </summary>
/// <remarks>
/// Standard is deliberately the smallest mode view: it declares its keypad and
/// nothing else. The display, memory row and history it appears alongside are
/// shared controls owned by the surrounding workspace, not by this mode.
/// </remarks>
public partial class StandardCalculatorView : UserControl, IShortcutPressedTarget
{
    public StandardCalculatorView() => InitializeComponent();

    public bool TrySetShortcutPressed(string shortcutId, bool isPressed)
    {
        var button = shortcutId switch
        {
            "clearButton" => ClearButton,
            "clearEntryButton" => ClearEntryButton,
            "decimalSeparatorButton" => DecimalButton,
            "divideButton" => DivideButton,
            "equalButton" => EqualsButton,
            "minusButton" => SubtractButton,
            "negateButton" => SignButton,
            "num0Button" => ZeroButton,
            "num1Button" => OneButton,
            "num2Button" => TwoButton,
            "num3Button" => ThreeButton,
            "num4Button" => FourButton,
            "num5Button" => FiveButton,
            "num6Button" => SixButton,
            "num7Button" => SevenButton,
            "num8Button" => EightButton,
            "num9Button" => NineButton,
            "percentButton" => PercentButton,
            "plusButton" => AddButton,
            "squareRootButton" => SquareRootButton,
            "backSpaceButton" => BackspaceButton,
            "multiplyButton" => MultiplyButton,
            _ => null,
        };

        if (button is null)
        {
            return false;
        }

        button.Classes.Set("keyboardPressed", isPressed);
        return true;
    }
}
