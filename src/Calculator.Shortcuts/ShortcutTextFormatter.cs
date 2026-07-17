using System.Text.RegularExpressions;

namespace Calculator.Shortcuts;

public enum ShortcutTextRewriteMode
{
    TemplateOnly,
    ReplaceTrailingParenthetical,
    ReplaceOrAppend,
}

public sealed partial class ShortcutTextFormatter
{
    public const string Placeholder = "{shortcut}";

    public const string AlternativesPlaceholder = "{shortcuts}";

    public string FormatTemplate(
        string localizedTemplate,
        string shortcutDisplayText,
        string? allShortcutDisplayText = null)
    {
        ArgumentNullException.ThrowIfNull(localizedTemplate);
        ArgumentNullException.ThrowIfNull(shortcutDisplayText);
        return localizedTemplate
            .Replace(AlternativesPlaceholder, allShortcutDisplayText ?? shortcutDisplayText, StringComparison.Ordinal)
            .Replace(Placeholder, shortcutDisplayText, StringComparison.Ordinal);
    }

    public string Rewrite(
        string localizedText,
        string shortcutDisplayText,
        ShortcutTextRewriteMode mode = ShortcutTextRewriteMode.TemplateOnly,
        string? allShortcutDisplayText = null)
    {
        ArgumentNullException.ThrowIfNull(localizedText);
        ArgumentNullException.ThrowIfNull(shortcutDisplayText);

        if (localizedText.Contains(Placeholder, StringComparison.Ordinal) ||
            localizedText.Contains(AlternativesPlaceholder, StringComparison.Ordinal))
        {
            return FormatTemplate(localizedText, shortcutDisplayText, allShortcutDisplayText);
        }

        if (mode == ShortcutTextRewriteMode.TemplateOnly)
        {
            return localizedText;
        }

        var match = TrailingParenthetical().Match(localizedText);
        if (match.Success)
        {
            var opening = match.Groups["open"].Value;
            var closing = opening == "（" ? "）" : ")";
            return localizedText[..match.Index] + opening + shortcutDisplayText + closing;
        }

        return mode == ShortcutTextRewriteMode.ReplaceOrAppend
            ? $"{localizedText} ({shortcutDisplayText})"
            : localizedText;
    }

    [GeneratedRegex(@"(?<open>\(|（)[^()（）]*(?:\)|）)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingParenthetical();
}
