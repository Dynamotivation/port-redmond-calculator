namespace Calculator.Managed;

public enum WindowCornerStyle
{
    Windows10,
    Windows11,
    MacOS,
}

public enum WindowControlStyle
{
    Windows11 = 0,
    // Preserve the old persisted numeric value and source-level name while
    // callers migrate from the formerly undifferentiated Windows option.
    Windows = Windows11,
    MacOS = 1,
    Windows10 = 2,
}

public sealed record PlatformAppearancePreferences(
    bool UseMicaEffect = true,
    WindowCornerStyle WindowCornerStyle = WindowCornerStyle.Windows11,
    WindowControlStyle WindowControlStyle = WindowControlStyle.Windows11);
