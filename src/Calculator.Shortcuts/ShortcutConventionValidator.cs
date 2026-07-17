namespace Calculator.Shortcuts;

public enum ShortcutConventionSeverity
{
    Information,
    Warning,
    Error,
}

public enum ShortcutConventionIssueKind
{
    ReservedByOperatingSystem,
    CommonApplicationCollision,
    NonIdiomaticModifier,
    LayoutDependentInput,
    FunctionKeyRequiresSpecialHandling,
    FocusScopeRequired,
    UnavailableKey,
}

public sealed record ShortcutConventionIssue(
    string ShortcutId,
    ShortcutPlatform Platform,
    ShortcutGesture Gesture,
    ShortcutConventionIssueKind Kind,
    ShortcutConventionSeverity Severity,
    string Message);

public interface IShortcutConventionValidator
{
    IReadOnlyList<ShortcutConventionIssue> Validate(
        IEnumerable<ShortcutDefinition> definitions,
        ShortcutPlatform platform);
}

public sealed class ShortcutConventionValidator : IShortcutConventionValidator
{
    public IReadOnlyList<ShortcutConventionIssue> Validate(
        IEnumerable<ShortcutDefinition> definitions,
        ShortcutPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var issues = new List<ShortcutConventionIssue>();
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            foreach (var gesture in definition.ResolveGestures(platform).Distinct())
            {
                ValidateGesture(definition, platform, gesture, issues);
            }
        }

        return issues.AsReadOnly();
    }

    private static void ValidateGesture(
        ShortcutDefinition definition,
        ShortcutPlatform platform,
        ShortcutGesture gesture,
        ICollection<ShortcutConventionIssue> issues)
    {
        if (gesture.Key.Kind == ShortcutKeyKind.Character && !char.IsLetterOrDigit(gesture.Key.Value[0]))
        {
            Add(ShortcutConventionIssueKind.LayoutDependentInput, ShortcutConventionSeverity.Warning,
                "Typed punctuation depends on the active keyboard layout; adapters must use text input.");
        }

        if (definition.Scope is null &&
            (gesture.Modifiers == ShortcutModifiers.None || gesture.Modifiers == ShortcutModifiers.Shift))
        {
            Add(ShortcutConventionIssueKind.FocusScopeRequired, ShortcutConventionSeverity.Warning,
                "Unmodified and Shift-only input should be restricted to an explicit focus or mode scope.");
        }

        if (IsFunctionKey(gesture.Key))
        {
            Add(ShortcutConventionIssueKind.FunctionKeyRequiresSpecialHandling, ShortcutConventionSeverity.Warning,
                platform == ShortcutPlatform.MacOS
                    ? "Function keys may control hardware features and can require Fn on macOS."
                    : "Function-key availability and behavior should be verified on the target keyboard.");
        }

        switch (platform)
        {
            case ShortcutPlatform.MacOS:
                ValidateMacOS(gesture, Add);
                break;
            case ShortcutPlatform.Windows:
                ValidateWindows(gesture, Add);
                break;
            case ShortcutPlatform.Linux:
                ValidateLinux(gesture, Add);
                break;
        }

        return;

        void Add(ShortcutConventionIssueKind kind, ShortcutConventionSeverity severity, string message) =>
            issues.Add(new ShortcutConventionIssue(definition.Id, platform, gesture, kind, severity, message));
    }

    private static void ValidateMacOS(
        ShortcutGesture gesture,
        Action<ShortcutConventionIssueKind, ShortcutConventionSeverity, string> add)
    {
        if (gesture.Modifiers.HasFlag(ShortcutModifiers.Control) &&
            !gesture.Modifiers.HasFlag(ShortcutModifiers.Command))
        {
            add(ShortcutConventionIssueKind.NonIdiomaticModifier, ShortcutConventionSeverity.Warning,
                "Control is not the usual macOS application-command modifier; review a Command-based binding.");
        }

        if (gesture.Key.Value == "INSERT")
        {
            add(ShortcutConventionIssueKind.UnavailableKey, ShortcutConventionSeverity.Error,
                "Insert is absent from standard Mac keyboards.");
        }

        if (gesture.Modifiers.HasFlag(ShortcutModifiers.Alt) && IsArrow(gesture.Key))
        {
            add(ShortcutConventionIssueKind.CommonApplicationCollision, ShortcutConventionSeverity.Error,
                "Option plus an arrow is a standard macOS text-navigation combination.");
        }

        if (gesture.Modifiers.HasFlag(ShortcutModifiers.Alt) && IsLetterOrDigit(gesture.Key))
        {
            add(ShortcutConventionIssueKind.LayoutDependentInput, ShortcutConventionSeverity.Warning,
                "Option plus a letter or number may produce a special character on the active macOS keyboard layout.");
        }

        if (gesture.Modifiers == ShortcutModifiers.Shift && IsArrow(gesture.Key))
        {
            add(ShortcutConventionIssueKind.CommonApplicationCollision, ShortcutConventionSeverity.Warning,
                "Shift plus an arrow is conventional text selection and must remain focus-scoped.");
        }

        if (gesture.Key.Value == "DELETE")
        {
            add(ShortcutConventionIssueKind.UnavailableKey, ShortcutConventionSeverity.Warning,
                "Forward Delete commonly requires Fn+Delete on a Mac keyboard; verify the intended deletion semantics.");
        }

        if (gesture.Modifiers == ShortcutModifiers.Command &&
            gesture.Key.Kind == ShortcutKeyKind.Character &&
            "QHMW".Contains(gesture.Key.Value, StringComparison.Ordinal))
        {
            add(ShortcutConventionIssueKind.CommonApplicationCollision, ShortcutConventionSeverity.Error,
                "This Command shortcut has a conventional macOS application or window action.");
        }

        if (gesture.Modifiers == ShortcutModifiers.Command && gesture.Key.Value == "SPACE")
        {
            add(ShortcutConventionIssueKind.ReservedByOperatingSystem, ShortcutConventionSeverity.Error,
                "Command+Space is normally reserved for Spotlight.");
        }
    }

    private static void ValidateWindows(
        ShortcutGesture gesture,
        Action<ShortcutConventionIssueKind, ShortcutConventionSeverity, string> add)
    {
        if (gesture.Modifiers == ShortcutModifiers.Alt && gesture.Key.Value == "F4")
        {
            add(ShortcutConventionIssueKind.CommonApplicationCollision, ShortcutConventionSeverity.Error,
                "Alt+F4 is the conventional Windows close-window shortcut.");
        }

        if (gesture.Modifiers.HasFlag(ShortcutModifiers.Super))
        {
            add(ShortcutConventionIssueKind.ReservedByOperatingSystem, ShortcutConventionSeverity.Warning,
                "Windows-key combinations are commonly reserved by the shell.");
        }
    }

    private static void ValidateLinux(
        ShortcutGesture gesture,
        Action<ShortcutConventionIssueKind, ShortcutConventionSeverity, string> add)
    {
        if (gesture.Modifiers == ShortcutModifiers.Alt && gesture.Key.Value == "F4")
        {
            add(ShortcutConventionIssueKind.CommonApplicationCollision, ShortcutConventionSeverity.Error,
                "Alt+F4 is conventionally handled by Linux desktop environments.");
        }

        if (gesture.Modifiers.HasFlag(ShortcutModifiers.Super))
        {
            add(ShortcutConventionIssueKind.ReservedByOperatingSystem, ShortcutConventionSeverity.Warning,
                "Super-key combinations may be intercepted by the desktop environment.");
        }
    }

    private static bool IsFunctionKey(ShortcutKey key) =>
        key.Kind == ShortcutKeyKind.Named &&
        key.Value.Length is 2 or 3 &&
        key.Value[0] == 'F' &&
        int.TryParse(key.Value.AsSpan(1), out var number) &&
        number is >= 1 and <= 24;

    private static bool IsArrow(ShortcutKey key) =>
        key.Value is "UP" or "DOWN" or "LEFT" or "RIGHT";

    private static bool IsLetterOrDigit(ShortcutKey key) =>
        key.Value.Length == 1 && char.IsLetterOrDigit(key.Value[0]);
}
