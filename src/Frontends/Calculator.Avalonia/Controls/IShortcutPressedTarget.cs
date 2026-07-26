namespace Calculator.Avalonia.Controls;

/// <summary>
/// Implemented by controls that own keypad buttons and can therefore render the
/// pressed state for a keyboard shortcut.
/// </summary>
/// <remarks>
/// This is what replaces the window's shortcut-id-to-named-button map. The
/// window normalises key input into a shortcut identifier and offers it to each
/// target in turn; whichever control actually owns a button for that identifier
/// claims it. A control that no longer hosts the relevant button simply returns
/// false, so extracting a keypad never leaves a dangling name behind.
/// </remarks>
public interface IShortcutPressedTarget
{
    /// <summary>
    /// Applies or clears pressed feedback for <paramref name="shortcutId"/>.
    /// </summary>
    /// <returns><c>true</c> when this control owns a button for the shortcut.</returns>
    bool TrySetShortcutPressed(string shortcutId, bool isPressed);
}
