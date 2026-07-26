using Avalonia.Controls;
using Calculator.Avalonia.Controls;

namespace Calculator.Avalonia.Views;

/// <summary>
/// Programmer mode: radix selection, word size, the bitwise and bit-shift
/// menus, the bit display and the integer keypad.
/// </summary>
/// <remarks>
/// The operator panel drops its labels below 630 DIPs of keypad width. That is
/// measured from this control's own keypad, not from the window, so the shell
/// has no part in programmer-specific layout.
/// </remarks>
public partial class ProgrammerCalculatorView : UserControl, IShortcutPressedTarget
{
    public ProgrammerCalculatorView()
    {
        InitializeComponent();
        ProgrammerNumpadPanel.SizeChanged += (_, _) => UpdateOperatorLabels();
        UpdateOperatorLabels();
    }

    private void UpdateOperatorLabels()
    {
        // CalculatorProgrammerRadixOperators.xaml switches from glyph+text to
        // glyph-only operator-panel buttons below its 630-DIP medium state.
        var showLabels = ProgrammerNumpadPanel.Bounds.Width >= 630;
        ProgrammerBitwiseLabel.IsVisible = showLabels;
        ProgrammerBitShiftLabel.IsVisible = showLabels;
    }

    public bool TrySetShortcutPressed(string shortcutId, bool isPressed)
    {
        var button = shortcutId switch
        {
            "clearButton" => ProgrammerClearButton,
            "clearEntryButton" => ProgrammerClearEntryButton,
            "divideButton" => ProgrammerDivideButton,
            "equalButton" => ProgrammerEqualsButton,
            "minusButton" => ProgrammerSubtractButton,
            "negateButton" => ProgrammerSignButton,
            "num0Button" => ProgrammerZeroButton,
            "num1Button" => ProgrammerOneButton,
            "num2Button" => ProgrammerTwoButton,
            "num3Button" => ProgrammerThreeButton,
            "num4Button" => ProgrammerFourButton,
            "num5Button" => ProgrammerFiveButton,
            "num6Button" => ProgrammerSixButton,
            "num7Button" => ProgrammerSevenButton,
            "num8Button" => ProgrammerEightButton,
            "num9Button" => ProgrammerNineButton,
            "plusButton" => ProgrammerAddButton,
            "backSpaceButton" => ProgrammerBackspaceButton,
            "multiplyButton" => ProgrammerMultiplyButton,
            "modButton" => ProgrammerModuloButton,
            "aButton" => ProgrammerAButton,
            "bButton" => ProgrammerBButton,
            "cButton" => ProgrammerCButton,
            "dButton" => ProgrammerDButton,
            "eButton" => ProgrammerEButton,
            "fButton" => ProgrammerFButton,
            "hexButton" => ProgrammerHexRadixButton,
            "decimalButton" => ProgrammerDecimalRadixButton,
            "octButton" => ProgrammerOctalRadixButton,
            "binaryButton" => ProgrammerBinaryRadixButton,
            "qwordButton" or "dwordButton" or "wordButton" or "byteButton" => ProgrammerWordSizeButton,
            "lshButton" or "lshLogicalButton" or "rolButton" or "rolCarryButton" => ProgrammerLeftShiftButton,
            "rshButton" or "rshLogicalButton" or "rorButton" or "rorCarryButton" => ProgrammerRightShiftButton,
            "openParenthesisButton" => ProgrammerOpenParenthesisButton,
            "closeParenthesisButton" => ProgrammerCloseParenthesisButton,
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
