using System.Collections.ObjectModel;

namespace Calculator.Shortcuts;

public enum ShortcutDisplayStyle
{
    Automatic,
    Text,
    Symbols,
}

public sealed class ShortcutDisplayOptions
{
    public ShortcutDisplayOptions(
        ShortcutDisplayStyle style = ShortcutDisplayStyle.Automatic,
        string separator = "+",
        bool concatenateSymbols = false,
        IReadOnlyDictionary<ShortcutModifiers, string>? modifierNames = null,
        IReadOnlyDictionary<string, string>? namedKeyNames = null)
    {
        ArgumentNullException.ThrowIfNull(separator);

        Style = style;
        Separator = separator;
        ConcatenateSymbols = concatenateSymbols;
        ModifierNames = new ReadOnlyDictionary<ShortcutModifiers, string>(
            modifierNames is null
                ? new Dictionary<ShortcutModifiers, string>()
                : new Dictionary<ShortcutModifiers, string>(modifierNames));
        NamedKeyNames = new ReadOnlyDictionary<string, string>(
            namedKeyNames is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(namedKeyNames, StringComparer.OrdinalIgnoreCase));
    }

    public ShortcutDisplayStyle Style { get; }

    public string Separator { get; }

    public bool ConcatenateSymbols { get; }

    public IReadOnlyDictionary<ShortcutModifiers, string> ModifierNames { get; }

    public IReadOnlyDictionary<string, string> NamedKeyNames { get; }

    public static ShortcutDisplayOptions ForPlatform(ShortcutPlatform platform) => platform switch
    {
        ShortcutPlatform.MacOS => new ShortcutDisplayOptions(
            ShortcutDisplayStyle.Symbols,
            separator: string.Empty,
            concatenateSymbols: true,
            modifierNames: new Dictionary<ShortcutModifiers, string>
            {
                [ShortcutModifiers.Control] = "⌃",
                [ShortcutModifiers.Alt] = "⌥",
                [ShortcutModifiers.Shift] = "⇧",
                [ShortcutModifiers.Command] = "⌘",
                [ShortcutModifiers.Super] = "◆",
            },
            namedKeyNames: CreateMacKeyNames()),
        ShortcutPlatform.Linux => CreateTextOptions("Ctrl", "Alt", "Shift", "Command", "Super"),
        _ => CreateTextOptions("Ctrl", "Alt", "Shift", "Command", "Win"),
    };

    private static ShortcutDisplayOptions CreateTextOptions(
        string control,
        string alt,
        string shift,
        string command,
        string super) =>
        new(
            ShortcutDisplayStyle.Text,
            modifierNames: new Dictionary<ShortcutModifiers, string>
            {
                [ShortcutModifiers.Control] = control,
                [ShortcutModifiers.Alt] = alt,
                [ShortcutModifiers.Shift] = shift,
                [ShortcutModifiers.Command] = command,
                [ShortcutModifiers.Super] = super,
            },
            namedKeyNames: CreateTextKeyNames());

    private static Dictionary<string, string> CreateTextKeyNames() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["BACK"] = "Backspace",
        ["BACKSPACE"] = "Backspace",
        ["DELETE"] = "Delete",
        ["DOWN"] = "Down",
        ["END"] = "End",
        ["ENTER"] = "Enter",
        ["ESC"] = "Esc",
        ["ESCAPE"] = "Esc",
        ["HOME"] = "Home",
        ["INSERT"] = "Insert",
        ["LEFT"] = "Left",
        ["PAGEDOWN"] = "Page Down",
        ["PAGEUP"] = "Page Up",
        ["RIGHT"] = "Right",
        ["SPACE"] = "Space",
        ["UP"] = "Up",
    };

    private static Dictionary<string, string> CreateMacKeyNames() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["BACK"] = "⌫",
        ["BACKSPACE"] = "⌫",
        ["DELETE"] = "⌦",
        ["DOWN"] = "↓",
        ["END"] = "↘",
        ["ENTER"] = "↩",
        ["ESC"] = "Esc",
        ["ESCAPE"] = "Esc",
        ["HOME"] = "↖",
        ["LEFT"] = "←",
        ["PAGEDOWN"] = "⇟",
        ["PAGEUP"] = "⇞",
        ["RIGHT"] = "→",
        ["SPACE"] = "Space",
        ["UP"] = "↑",
    };
}

public sealed class ShortcutDisplayFormatter
{
    private static readonly ShortcutModifiers[] ModifierOrder =
    [
        ShortcutModifiers.Control,
        ShortcutModifiers.Alt,
        ShortcutModifiers.Shift,
        ShortcutModifiers.Command,
        ShortcutModifiers.Super,
    ];

    private readonly ShortcutDisplayOptions _options;

    public ShortcutDisplayFormatter(ShortcutDisplayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string Format(ShortcutGesture gesture)
    {
        var parts = new List<string>();
        foreach (var modifier in ModifierOrder)
        {
            if (gesture.Modifiers.HasFlag(modifier))
            {
                parts.Add(GetModifierName(modifier));
            }
        }

        parts.Add(GetKeyName(gesture.Key));
        var separator = _options.Style == ShortcutDisplayStyle.Symbols && _options.ConcatenateSymbols
            ? string.Empty
            : _options.Separator;
        return string.Join(separator, parts);
    }

    private string GetModifierName(ShortcutModifiers modifier) =>
        _options.ModifierNames.TryGetValue(modifier, out var name)
            ? name
            : modifier.ToString();

    private string GetKeyName(ShortcutKey key) =>
        key.Kind == ShortcutKeyKind.Named && _options.NamedKeyNames.TryGetValue(key.Value, out var name)
            ? name
            : key.Value;
}
