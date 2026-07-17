namespace Calculator.Managed;

public enum WindowCornerStyle
{
    Windows10,
    Windows11,
    MacOS,
}

public enum WindowControlStyle
{
    Windows,
    MacOS,
}

public sealed record PlatformAppearancePreferences(
    bool UseMicaEffect = true,
    WindowCornerStyle WindowCornerStyle = WindowCornerStyle.Windows11,
    WindowControlStyle WindowControlStyle = WindowControlStyle.Windows);
