using Avalonia.Controls;
using Calculator.Avalonia.Controls;

namespace Calculator.Avalonia.Views;

/// <summary>
/// The Scientific angle and F-E controls, which sit in the display-controls row
/// above the memory row rather than in the keypad.
/// </summary>
public partial class ScientificDisplayControls : UserControl, IShortcutPressedTarget
{
    public ScientificDisplayControls() => InitializeComponent();

    public bool TrySetShortcutPressed(string shortcutId, bool isPressed)
    {
        var button = shortcutId switch
        {
            "degButton" or "radButton" or "gradButton" => ScientificAngleButton,
            "ftoeButton" => ScientificNotationButton,
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
