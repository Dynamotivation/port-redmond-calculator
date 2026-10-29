namespace Calculator.Managed;

/// <summary>
/// The key that starts the macOS-specific shortcuts (copy, paste, and graph
/// zoom and reset). Windows and Linux always use Control.
/// </summary>
/// <remarks>
/// The numeric values are persisted; keep Command at 0 so a settings file
/// written before this preference existed keeps the Command default.
/// </remarks>
public enum MacShortcutModifierPreference
{
    Command = 0,
    Control = 1,
}
