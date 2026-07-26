using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Calculator.Avalonia.Controls;

/// <summary>
/// The six compact memory buttons above the keypad, plus the popup listing the
/// individual stored values.
/// </summary>
public partial class MemoryPanel : UserControl, IShortcutPressedTarget
{
    public MemoryPanel() => InitializeComponent();

    public bool TrySetShortcutPressed(string shortcutId, bool isPressed)
    {
        var button = shortcutId switch
        {
            "ClearMemoryButton" => MemoryClearButton,
            "MemRecall" => MemoryRecallButton,
            "MemPlus" => MemoryAddButton,
            "MemMinus" => MemorySubtractButton,
            _ => null,
        };

        if (button is null)
        {
            return false;
        }

        button.Classes.Set("keyboardPressed", isPressed);
        return true;
    }

    private void MemoryFlyout_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control)
        {
            MemoryPopup.IsOpen = !MemoryPopup.IsOpen;
            e.Handled = true;
        }
    }
}
