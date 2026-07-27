using Avalonia.Controls;
using Calculator.Avalonia.Controls;

namespace Calculator.Avalonia.Views;

/// <summary>
/// The unit converter page: category and unit selectors, both value fields,
/// the suggestion list and the converter keypad.
/// </summary>
public partial class UnitConverterView : UserControl, IShortcutPressedTarget
{
    public UnitConverterView() => InitializeComponent();

    public bool TrySetShortcutPressed(string shortcutId, bool isPressed)
    {
        var button = shortcutId switch
        {
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
            "decimalSeparatorButton" => DecimalButton,
            "converterNegateButton" => NegateButton,
            "backSpaceButton" => BackspaceButton,
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
