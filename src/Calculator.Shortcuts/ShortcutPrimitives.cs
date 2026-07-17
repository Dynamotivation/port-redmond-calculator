namespace Calculator.Shortcuts;

public enum ShortcutPlatform
{
    Unknown,
    Windows,
    MacOS,
    Linux,
}

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Command = 1 << 3,
    Super = 1 << 4,
}

public enum ShortcutKeyKind
{
    Named,
    Character,
}

public readonly record struct ShortcutKey
{
    private ShortcutKey(string value, ShortcutKeyKind kind)
    {
        Value = value;
        Kind = kind;
    }

    public string Value { get; }

    public ShortcutKeyKind Kind { get; }

    public static ShortcutKey Named(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new ShortcutKey(value.Trim().ToUpperInvariant(), ShortcutKeyKind.Named);
    }

    public static ShortcutKey Character(char value) =>
        new(char.ToUpperInvariant(value).ToString(), ShortcutKeyKind.Character);

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct ShortcutGesture
{
    public ShortcutGesture(ShortcutKey key, ShortcutModifiers modifiers = ShortcutModifiers.None)
    {
        if (string.IsNullOrWhiteSpace(key.Value))
        {
            throw new ArgumentException("A shortcut gesture must have a key.", nameof(key));
        }

        Key = key;
        Modifiers = modifiers;
    }

    public ShortcutKey Key { get; }

    public ShortcutModifiers Modifiers { get; }
}

public readonly record struct ShortcutInput(ShortcutGesture Gesture, bool IsRepeat = false);
